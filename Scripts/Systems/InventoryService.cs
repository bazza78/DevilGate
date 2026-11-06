using DevilGate.Scripts.Data;
using DevilGate.Scripts.Definitions;

namespace DevilGate.Scripts.Systems;

/// <summary>Result of an inventory operation, with a player-facing reason on failure.</summary>
public readonly record struct OperationResult(bool Success, string? Reason = null)
{
    public static OperationResult Ok() => new(true);
    public static OperationResult Fail(string reason) => new(false, reason);

    public override string ToString() => Success ? "OK" : $"FAIL: {Reason}";
}

/// <summary>
/// Owns every backpack/chest/equipment/consumable rule. All operations are
/// prevalidated and atomic: on failure nothing changes. UI calls these and
/// only redraws from GameSession state afterwards.
/// </summary>
public static class InventoryService
{
    // ------------------------------------------------------------------
    // Queries
    // ------------------------------------------------------------------

    /// <summary>Quantity of an item carried in the backpack only (not chest/equipment).</summary>
    public static int CountCarried(GameCatalog catalog, RunState run, string itemId)
    {
        catalog.GetItem(itemId); // throws on unknown id — callers pass known ids
        return run.Backpack.CountItem(itemId);
    }

    // ------------------------------------------------------------------
    // Transfers between slot grids (backpack <-> chest)
    // ------------------------------------------------------------------

    /// <summary>Move a single unit from src[srcSlot] into dst[dstSlot].</summary>
    public static OperationResult MoveOne(InventoryState src, int srcSlot, InventoryState dst, int dstSlot, GameCatalog catalog)
    {
        if (!TryGetStack(src, srcSlot, out var stack, out var fail)) return fail;
        var item = catalog.GetItem(stack!.ItemId);

        if (src == dst) return OperationResult.Fail("Source and destination are the same.");
        if (!TryResolveDestination(dst, dstSlot, stack.ItemId, item, out var resolvedSlot, out fail)) return fail;

        var dstStack = dst[resolvedSlot];
        if (dstStack == null)
        {
            dst[resolvedSlot] = new ItemStack(stack.ItemId, 1);
        }
        else
        {
            dstStack.Quantity += 1;
        }

        stack.Quantity -= 1;
        if (stack.Quantity <= 0) src[srcSlot] = null;
        return OperationResult.Ok();
    }

    /// <summary>
    /// Move the entire stack from src[srcSlot] into dst, starting at dstSlot
    /// (or auto-picked when -1). Compatible stacks merge first, then empty
    /// slots. Preflights total capacity: rejects without partial movement.
    /// </summary>
    public static OperationResult MoveStack(InventoryState src, int srcSlot, InventoryState dst, int dstSlot, GameCatalog catalog)
    {
        if (!TryGetStack(src, srcSlot, out var stack, out var fail)) return fail;
        var item = catalog.GetItem(stack!.ItemId);
        var amount = stack.Quantity;

        if (src == dst) return OperationResult.Fail("Source and destination are the same.");
        if (dstSlot >= InventoryState.SlotCount || dstSlot < -1) return OperationResult.Fail("Invalid destination slot.");

        // Explicit destination must be empty or a compatible stack.
        if (dstSlot >= 0)
        {
            var target = dst[dstSlot];
            if (target != null && !CanMerge(target, stack.ItemId, item))
            {
                return OperationResult.Fail("Cannot merge different items.");
            }
        }

        // Build ordered destination slot list: explicit slot first, then any
        // merge-compatible stacks, then empty slots ("merge first, then use
        // empty slots" per PLAN.md).
        var plan = new List<(int Slot, int Capacity)>();
        var remaining = amount;

        void AddCapacity(int slot)
        {
            if (remaining <= 0 || slot < 0 || slot >= InventoryState.SlotCount) return;
            var existing = dst[slot];
            int capacity;
            if (existing == null) capacity = item.StackLimit;
            else if (CanMerge(existing, stack.ItemId, item)) capacity = item.StackLimit - existing.Quantity;
            else return;
            if (capacity <= 0) return;
            var take = Math.Min(capacity, remaining);
            plan.Add((slot, take));
            remaining -= take;
        }

        if (dstSlot >= 0) AddCapacity(dstSlot);
        for (var i = 0; i < InventoryState.SlotCount && remaining > 0; i++)
        {
            if (i == dstSlot) continue;
            var existing = dst[i];
            if (existing != null && existing.ItemId == stack.ItemId) AddCapacity(i);
        }
        for (var i = 0; i < InventoryState.SlotCount && remaining > 0; i++)
        {
            if (i == dstSlot || dst[i] != null) continue;
            AddCapacity(i);
        }

        if (remaining > 0)
        {
            return OperationResult.Fail("Not enough room in the destination.");
        }

        foreach (var (slot, take) in plan)
        {
            var existing = dst[slot];
            if (existing == null)
            {
                dst[slot] = new ItemStack(stack.ItemId, take);
            }
            else
            {
                existing.Quantity += take;
            }
        }

        src[srcSlot] = null;
        return OperationResult.Ok();
    }

    /// <summary>Collect the chest's entire gold balance into carried gold, once.</summary>
    public static OperationResult CollectChestGold(RunState run)
    {
        if (run.Chest.Gold <= 0)
        {
            return OperationResult.Fail("The chest holds no gold.");
        }
        run.CarriedGold += run.Chest.Gold;
        run.Chest.Gold = 0;
        return OperationResult.Ok();
    }

    /// <summary>Deterministic whole-stack transfers. Each failed stack remains intact; gold is collected once.</summary>
    public static string TakeAll(RunState run, GameCatalog catalog)
    {
        int moved = 0, left = 0, gold = run.Chest.Gold;
        for (int i = 0; i < InventoryState.SlotCount; i++)
        {
            var stack = run.Chest.Inventory[i];
            if (stack == null) continue;
            int quantity = stack.Quantity;
            if (MoveStack(run.Chest.Inventory, i, run.Backpack, -1, catalog).Success) moved += quantity;
            else left += quantity;
        }
        if (gold > 0) CollectChestGold(run);
        return $"Took {moved} items and {gold} gold. {left} items remain in the chest.";
    }

    // ------------------------------------------------------------------
    // Equipment
    // ------------------------------------------------------------------

    /// <summary>
    /// Equip the backpack item at slotIndex. Swaps with any occupied equipment
    /// slot in one transaction: the previous item lands in the vacated
    /// backpack slot.
    /// </summary>
    public static OperationResult EquipFromBackpack(RunState run, int slotIndex, GameCatalog catalog)
    {
        if (!TryGetStack(run.Backpack, slotIndex, out var stack, out var fail)) return fail;
        var item = catalog.GetItem(stack!.ItemId);

        if (item.Category is not (ItemCategory.Weapon or ItemCategory.Armour))
        {
            return OperationResult.Fail("That item cannot be equipped.");
        }
        if (!item.IsEquippableBy(run.Character.ArchetypeId))
        {
            return OperationResult.Fail($"Only a {catalog.GetArchetype(item.ArchetypeId).DisplayName} can equip the {item.DisplayName}.");
        }

        var slotKind = item.Category == ItemCategory.Weapon ? EquipmentSlotKind.Weapon : EquipmentSlotKind.Armour;
        var equipped = run.Equipment[slotKind];
        run.Equipment[slotKind] = stack;
        run.Backpack[slotIndex] = equipped;
        return OperationResult.Ok();
    }

    /// <summary>Move an equipped item back to the first free backpack slot.</summary>
    public static OperationResult UnequipToBackpack(RunState run, EquipmentSlotKind slotKind)
    {
        var equipped = run.Equipment[slotKind];
        if (equipped == null)
        {
            return OperationResult.Fail("Nothing is equipped there.");
        }
        var free = run.Backpack.FirstEmptySlot();
        if (free < 0)
        {
            return OperationResult.Fail("Backpack is full — make room first.");
        }
        run.Backpack[free] = equipped;
        run.Equipment[slotKind] = null;
        return OperationResult.Ok();
    }

    // ------------------------------------------------------------------
    // Consumables
    // ------------------------------------------------------------------

    /// <summary>
    /// Use a backpack item. Potions restore health/mana capped at maximum and
    /// are not consumed when already full. Save crystals are spent through the
    /// Save button, not here.
    /// </summary>
    public static OperationResult UseBackpackItem(RunState run, int slotIndex, GameCatalog catalog)
    {
        if (!TryGetStack(run.Backpack, slotIndex, out var stack, out var fail)) return fail;
        var item = catalog.GetItem(stack!.ItemId);
        var character = run.Character;

        switch (item.Category)
        {
            case ItemCategory.HealthPotion:
            {
                var max = CharacterRules.MaxHealth(character);
                if (character.Hp >= max) return OperationResult.Fail("Health is already full.");
                character.Hp = Math.Min(max, character.Hp + item.HealAmount);
                break;
            }
            case ItemCategory.ManaPotion:
            {
                var max = CharacterRules.MaxMana(character);
                if (character.Mana >= max) return OperationResult.Fail($"{StarterTraining.Resource(run)} is already full.");
                character.Mana = Math.Min(max, character.Mana + item.ManaAmount);
                break;
            }
            case ItemCategory.SaveCrystal:
                return OperationResult.Fail("Save crystals are used with the Save button.");
            default:
                return OperationResult.Fail("That item cannot be used.");
        }

        ConsumeOne(run.Backpack, slotIndex);
        return OperationResult.Ok();
    }

    /// <summary>Remove one unit from a slot; clears the slot at zero.</summary>
    public static void ConsumeOne(InventoryState inventory, int slotIndex)
    {
        var stack = inventory[slotIndex];
        if (stack == null) return;
        stack.Quantity -= 1;
        if (stack.Quantity <= 0) inventory[slotIndex] = null;
    }

    // ------------------------------------------------------------------
    // Internals
    // ------------------------------------------------------------------

    private static bool CanMerge(ItemStack existing, string itemId, ItemDefinition item)
    {
        return item.IsStackable && existing.ItemId == itemId && existing.Quantity < item.StackLimit;
    }

    private static bool TryGetStack(InventoryState inventory, int slot, out ItemStack? stack, out OperationResult fail)
    {
        stack = null;
        fail = OperationResult.Ok();
        if (slot < 0 || slot >= InventoryState.SlotCount)
        {
            fail = OperationResult.Fail("Invalid slot.");
            return false;
        }
        stack = inventory[slot];
        if (stack == null || stack.Quantity <= 0)
        {
            fail = OperationResult.Fail("No item in that slot.");
            return false;
        }
        return true;
    }

    /// <summary>Pick a destination slot for a single-unit move.</summary>
    private static bool TryResolveDestination(InventoryState dst, int preferredSlot, string itemId, ItemDefinition item, out int slot, out OperationResult fail)
    {
        slot = -1;
        fail = OperationResult.Ok();

        if (preferredSlot >= 0)
        {
            if (preferredSlot >= InventoryState.SlotCount) { fail = OperationResult.Fail("Invalid destination slot."); return false; }
            var existing = dst[preferredSlot];
            if (existing == null) { slot = preferredSlot; return true; }
            if (existing.ItemId == itemId)
            {
                if (existing.Quantity < item.StackLimit) { slot = preferredSlot; return true; }
                fail = OperationResult.Fail("That stack is full.");
                return false;
            }
            fail = OperationResult.Fail("Cannot merge different items.");
            return false;
        }

        // Prefer merging into an existing compatible stack, then first empty.
        for (var i = 0; i < InventoryState.SlotCount; i++)
        {
            var existing = dst[i];
            if (existing != null && CanMerge(existing, itemId, item)) { slot = i; return true; }
        }
        slot = dst.FirstEmptySlot();
        if (slot < 0)
        {
            fail = OperationResult.Fail("No room in the destination.");
            return false;
        }
        return true;
    }
}
