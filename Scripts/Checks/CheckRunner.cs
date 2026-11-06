using DevilGate.Scripts.Data;
using DevilGate.Scripts.Definitions;
using DevilGate.Scripts.Systems;
using DevilGate.Scripts.World;
using Godot;

namespace DevilGate.Scripts.Checks;

/// <summary>
/// Headless rule checks. Run with:
///   dotnet build
///   /path/to/Godot --headless --path . res://Scenes/Checks.tscn
/// Prints CHECK lines and exits 0 on success, 1 on any failure.
/// Not part of the player-facing game flow.
/// </summary>
public partial class CheckRunner : Node
{
    private int _passed;
    private int _failed;

    public override void _Ready()
    {
        // Keep save checks away from the player's real save slot.
        SaveServiceTestDirectory.UseTempDirectory();

        RunCatalogChecks();
        RunCharacterRuleChecks();
        RunFactoryChecks();
        RunInventoryChecks();
        RunDiceChecks();
        RunSaveChecks();

        SaveServiceTestDirectory.Cleanup();
        GD.Print($"CHECKS DONE: {_passed} passed, {_failed} failed");
        GetTree().Quit(_failed == 0 ? 0 : 1);
    }

    // ------------------------------------------------------------------
    // Harness
    // ------------------------------------------------------------------

    private void Check(bool condition, string name)
    {
        if (condition)
        {
            _passed++;
            GD.Print($"CHECK PASS: {name}");
        }
        else
        {
            _failed++;
            GD.Print($"CHECK FAIL: {name}");
        }
    }

    private void Section(string name) => GD.Print($"--- {name} ---");

    private static CharacterState MageWith(int intellect = 4, int willpower = 5)
    {
        var state = new CharacterState
        {
            RunId = "test",
            Name = "Test",
            ArchetypeId = Ids.ArchetypeMage,
            GenderId = Ids.GenderMale,
        };
        state.Attributes = new AttributeSet
        {
            Might = 1, Agility = 2, Endurance = 2, Intellect = intellect, Willpower = willpower, Presence = 4,
        };
        state.Hp = CharacterRules.MaxHealth(state.Attributes);
        state.Mana = CharacterRules.MaxMana(state.Attributes);
        return state;
    }

    private static RunState NewRunFor(string archetypeId)
    {
        var catalog = GameCatalog.Instance;
        var archetype = catalog.GetArchetype(archetypeId);
        var creation = new CharacterCreationResult
        {
            Name = "Check",
            ArchetypeId = archetypeId,
            GenderId = Ids.GenderMale,
            Attributes = archetype.CreateSuggestedAttributes(),
        };
        return RunFactory.CreateNewRun(creation, catalog);
    }

    // ------------------------------------------------------------------
    // Catalog + character rules (stage 02)
    // ------------------------------------------------------------------

    private void RunCatalogChecks()
    {
        Section("catalog");
        var catalog = GameCatalog.Instance;
        Check(catalog.Archetypes.Count == 5, "catalog has five archetypes");
        Check(catalog.Items.Count == 9, "catalog has nine items");
        Check(catalog.Skills.Count == 6, "catalog has six skills");

        foreach (var arch in catalog.AllArchetypes)
        {
            var attrs = arch.CreateSuggestedAttributes();
            Check(CharacterRules.IsAllocationComplete(attrs), $"{arch.Id} suggested allocation totals 18 in range");
            Check(arch.TrainedSkillIds.Length == 1, $"{arch.Id} has one trained skill");
            Check(catalog.TryGetItem(arch.WeaponItemId, out var w) && w!.Category == ItemCategory.Weapon,
                $"{arch.Id} weapon '{arch.WeaponItemId}' exists and is a weapon");
            Check(w!.IsEquippableBy(arch.Id) && !w.IsEquippableBy(OtherArchetypeId(arch.Id)),
                $"{arch.Id} weapon restricted to its archetype");

            foreach (var gender in new[] { Ids.GenderMale, Ids.GenderFemale })
            {
                Check(arch.LoadPortrait(gender) != null, $"portrait {arch.PortraitKey(gender)} loads");
            }
        }

        foreach (var item in catalog.Items.Values)
        {
            Check(item.LoadIcon() != null, $"item icon {item.Id} loads");
        }
    }

    private static string OtherArchetypeId(string id) =>
        id == Ids.ArchetypeMage ? Ids.ArchetypeTank : Ids.ArchetypeMage;

    private void RunCharacterRuleChecks()
    {
        Section("character rules");

        Check(CharacterRules.IsValidName("A"), "name of length 1 valid");
        Check(CharacterRules.IsValidName("  Hero  "), "name trims to valid");
        Check(CharacterRules.IsValidName(new string('x', 24)), "name of length 24 valid");
        Check(!CharacterRules.IsValidName(""), "empty name invalid");
        Check(!CharacterRules.IsValidName("   "), "whitespace name invalid");
        Check(!CharacterRules.IsValidName(new string('x', 25)), "name of length 25 invalid");
        Check(!CharacterRules.IsValidName(null), "null name invalid");

        var attrs = new AttributeSet();
        Check(!CharacterRules.IsAllocationComplete(attrs), "all-1 allocation incomplete (total 6)");
        Check(CharacterRules.BonusSpent(attrs) == 0, "fresh allocation spends 0 bonus points");

        var full = new AttributeSet { Might = 5, Agility = 5, Endurance = 5 };
        Check(full.Total == 18, "hand-built 5/5/5 allocation totals 18");
        Check(CharacterRules.IsAllocationComplete(full), "5/5/5 allocation complete");

        var over = new AttributeSet { Might = 5, Agility = 5, Endurance = 5, Intellect = 2 };
        Check(over.Total == 19 && !CharacterRules.IsAllocationComplete(over), "total 19 rejected");
        var under = new AttributeSet { Might = 5, Agility = 5, Endurance = 4 };
        Check(under.Total == 17 && !CharacterRules.IsAllocationComplete(under), "total 17 rejected");
        var outOfRange = new AttributeSet { Might = 6, Agility = 5, Endurance = 5 };
        Check(!CharacterRules.IsAllocationComplete(outOfRange), "attribute above 5 rejected");

        Check(CharacterRules.CanIncrement(new AttributeSet(), AttributeKind.Might), "can increment from 1");
        Check(!CharacterRules.CanIncrement(new AttributeSet { Might = 5 }, AttributeKind.Might), "cannot increment at 5");
        var spent = new AttributeSet { Might = 5, Agility = 5, Endurance = 5 };
        Check(CharacterRules.BonusSpent(spent) == CharacterRules.BonusPoints, "bonus spent reaches 12 at total 18");

        Check(CharacterRules.MaxHealth(new AttributeSet { Endurance = 1 }) == 25, "max health E1 = 25");
        Check(CharacterRules.MaxHealth(new AttributeSet { Endurance = 5 }) == 45, "max health E5 = 45");
        Check(CharacterRules.MaxMana(new AttributeSet { Willpower = 1 }) == 10, "max mana W1 = 10");
        Check(CharacterRules.MaxMana(new AttributeSet { Willpower = 5 }) == 30, "max mana W5 = 30");
    }

    // ------------------------------------------------------------------
    // Run factory (stage 02)
    // ------------------------------------------------------------------

    private void RunFactoryChecks()
    {
        Section("run factory");
        var catalog = GameCatalog.Instance;

        foreach (var archetypeId in new[] { Ids.ArchetypeMage, Ids.ArchetypeTank, Ids.ArchetypeArcher, Ids.ArchetypeKnight, Ids.ArchetypeHealer })
        {
            var run = NewRunFor(archetypeId);
            var arch = catalog.GetArchetype(archetypeId);
            var c = run.Character;

            Check(c.ArchetypeId == archetypeId, $"{archetypeId}: archetype id stored");
            Check(c.Attributes.Total == 18, $"{archetypeId}: attributes total 18");
            Check(c.Attributes.Might == arch.SuggestedMight && c.Attributes.Presence == arch.SuggestedPresence,
                $"{archetypeId}: attributes match suggestion");
            Check(c.Hp == CharacterRules.MaxHealth(c.Attributes) && c.Mana == CharacterRules.MaxMana(c.Attributes),
                $"{archetypeId}: starts at full health/mana");
            Check(c.Level == 1 && c.Xp == 0, $"{archetypeId}: level 1, xp 0");
            Check(!c.Diagnostic.Attempted, $"{archetypeId}: diagnostic not attempted");

            Check(run.Backpack.IsFull == false && run.Backpack.CountEmptySlots() == InventoryState.SlotCount,
                $"{archetypeId}: backpack empty");
            Check(run.Equipment.Weapon == null && run.Equipment.Armour == null, $"{archetypeId}: equipment empty");
            Check(run.CarriedGold == 0, $"{archetypeId}: carried gold 0");

            Check(run.Chest.Gold == GameCatalog.StartingChestGold, $"{archetypeId}: chest gold 30");
            Check(run.Chest.Inventory[0]?.ItemId == arch.WeaponItemId && run.Chest.Inventory[0]?.Quantity == 1,
                $"{archetypeId}: chest slot0 is {arch.WeaponItemId}");
            Check(run.Chest.Inventory[1]?.ItemId == Ids.ItemBasicJacket, $"{archetypeId}: chest slot1 jacket");
            Check(run.Chest.Inventory[2]?.ItemId == Ids.ItemHealthPotion && run.Chest.Inventory[2]?.Quantity == 2,
                $"{archetypeId}: chest slot2 health potions x2");
            Check(run.Chest.Inventory[3]?.ItemId == Ids.ItemManaPotion && run.Chest.Inventory[3]?.Quantity == 2,
                $"{archetypeId}: chest slot3 mana potions x2");
            Check(run.Chest.Inventory[4]?.ItemId == Ids.ItemSaveCrystal && run.Chest.Inventory[4]?.Quantity == 3,
                $"{archetypeId}: chest slot4 save crystals x3");
            Check(run.Chest.Inventory.CountEmptySlots() == InventoryState.SlotCount - 5,
                $"{archetypeId}: chest uses exactly 5 slots");
        }

        var runA = NewRunFor(Ids.ArchetypeMage);
        var runB = NewRunFor(Ids.ArchetypeMage);
        Check(runA.Character.RunId != runB.Character.RunId, "two runs have unique run ids");

        runA.Chest.Inventory[0]!.Quantity = 99;
        runA.Character.Attributes.Might = 1;
        Check(runB.Chest.Inventory[0]!.Quantity == 1 && runB.Character.Attributes.Might == 1,
            "runs do not share mutable chest/attribute state");
        Check(runB.Chest.Gold == 30, "second run chest gold unaffected");
    }

    // ------------------------------------------------------------------
    // Inventory rules (stage 06/07 logic, verified headlessly)
    // ------------------------------------------------------------------

    private void RunInventoryChecks()
    {
        Section("inventory");
        var catalog = GameCatalog.Instance;
        var run = NewRunFor(Ids.ArchetypeMage);

        // Move one health potion chest->backpack.
        var chest = run.Chest.Inventory;
        var pack = run.Backpack;
        var r = InventoryService.MoveOne(chest, 2, pack, -1, catalog);
        Check(r.Success && pack[0]?.ItemId == Ids.ItemHealthPotion && pack[0]?.Quantity == 1
            && chest[2]?.Quantity == 1, "move one splits a stack correctly");

        // Merge into existing stack.
        r = InventoryService.MoveOne(chest, 2, pack, 0, catalog);
        Check(r.Success && pack[0]?.Quantity == 2 && chest[2] == null, "move one merges into compatible stack");

        // Different item cannot merge.
        r = InventoryService.MoveOne(chest, 1, pack, 0, catalog);
        Check(!r.Success && chest[1]?.Quantity == 1 && pack[0]?.Quantity == 2, "cannot merge different items");

        // Fill backpack, then test full-destination rejection.
        var full = new InventoryState();
        for (var i = 0; i < InventoryState.SlotCount; i++) full[i] = new ItemStack(Ids.ItemBasicJacket);
        var src = new InventoryState { [0] = new ItemStack(Ids.ItemHealthPotion, 3) };
        r = InventoryService.MoveStack(src, 0, full, -1, catalog);
        Check(!r.Success && src[0]?.Quantity == 3, "whole-stack move into full inventory rejected atomically");

        // Whole-stack spill: a full stack of 9 merges into a partial stack then spills.
        var dst = new InventoryState();
        dst[0] = new ItemStack(Ids.ItemHealthPotion, 4);
        var big = new InventoryState { [0] = new ItemStack(Ids.ItemHealthPotion, 9) };
        r = InventoryService.MoveStack(big, 0, dst, -1, catalog);
        Check(r.Success && dst[0]?.Quantity == 9 && dst[1]?.Quantity == 4 && dst[1]?.ItemId == Ids.ItemHealthPotion,
            "whole-stack spills across merge + empty slots");

        // Conservation: totals unchanged by transfers.
        var before = TotalQuantity(chest) + TotalQuantity(pack);
        InventoryService.MoveOne(chest, 3, pack, -1, catalog);
        InventoryService.MoveOne(pack, 0, chest, -1, catalog);
        Check(TotalQuantity(chest) + TotalQuantity(pack) == before, "item totals conserved across transfers");

        // Gold: take once, second take fails.
        r = InventoryService.CollectChestGold(run);
        Check(r.Success && run.CarriedGold == 30 && run.Chest.Gold == 0, "take gold transfers full balance");
        r = InventoryService.CollectChestGold(run);
        Check(!r.Success && run.CarriedGold == 30, "taking gold twice fails without change");

        // Equipment: equip jacket from chest is impossible (chest!=backpack); move to pack first.
        InventoryService.MoveOne(chest, 1, pack, -1, catalog);
        var jacketSlot = FindSlot(pack, Ids.ItemBasicJacket);
        r = InventoryService.EquipFromBackpack(run, jacketSlot, catalog);
        Check(r.Success && run.Equipment.Armour?.ItemId == Ids.ItemBasicJacket && pack[jacketSlot] == null,
            "equip moves item from backpack to armour slot");

        // Swap with occupied slot: pre-seed weapon slot, equip staff.
        var staffSlot = -1;
        for (var i = 0; i < InventoryState.SlotCount; i++)
        {
            if (pack[i]?.ItemId == Ids.ItemApprenticeStaff) { staffSlot = i; break; }
        }
        if (staffSlot < 0)
        {
            // move staff from chest to backpack
            var cs = FindSlot(chest, Ids.ItemApprenticeStaff);
            InventoryService.MoveOne(chest, cs, pack, -1, catalog);
            staffSlot = FindSlot(pack, Ids.ItemApprenticeStaff);
        }
        run.Equipment.Weapon = new ItemStack(Ids.ItemBasicJacket, 1); // artificial prior weapon
        r = InventoryService.EquipFromBackpack(run, staffSlot, catalog);
        Check(r.Success && run.Equipment.Weapon?.ItemId == Ids.ItemApprenticeStaff
            && pack[staffSlot]?.ItemId == Ids.ItemBasicJacket,
            "equipping swaps previous item into the vacated backpack slot");

        // Wrong-archetype weapon rejected.
        var knightRun = NewRunFor(Ids.ArchetypeKnight);
        var freeSlot = knightRun.Backpack.FirstEmptySlot();
        knightRun.Backpack[freeSlot] = new ItemStack(Ids.ItemApprenticeStaff, 1);
        r = InventoryService.EquipFromBackpack(knightRun, freeSlot, catalog);
        Check(!r.Success && knightRun.Equipment.Weapon == null
            && knightRun.Backpack[freeSlot]?.ItemId == Ids.ItemApprenticeStaff,
            "wrong-archetype weapon equip rejected without change");

        // Unequip into full backpack fails.
        var tankRun = NewRunFor(Ids.ArchetypeTank);
        tankRun.Equipment.Weapon = new ItemStack(Ids.ItemReinforcedClub, 1);
        for (var i = 0; i < InventoryState.SlotCount; i++) tankRun.Backpack[i] = new ItemStack(Ids.ItemHealthPotion, 1);
        r = InventoryService.UnequipToBackpack(tankRun, EquipmentSlotKind.Weapon);
        Check(!r.Success && tankRun.Equipment.Weapon != null, "unequip into full backpack fails cleanly");
        tankRun.Backpack[0] = null;
        r = InventoryService.UnequipToBackpack(tankRun, EquipmentSlotKind.Weapon);
        Check(r.Success && tankRun.Backpack[0]?.ItemId == Ids.ItemReinforcedClub && tankRun.Equipment.Weapon == null,
            "unequip into free slot works");

        // Potions: reduce HP then use.
        var healRun = NewRunFor(Ids.ArchetypeHealer);
        var maxHp = CharacterRules.MaxHealth(healRun.Character.Attributes);
        healRun.Character.Hp = maxHp - 5;
        var hpSlot = healRun.Backpack.FirstEmptySlot();
        healRun.Backpack[hpSlot] = new ItemStack(Ids.ItemHealthPotion, 2);
        r = InventoryService.UseBackpackItem(healRun, hpSlot, catalog);
        Check(r.Success && healRun.Character.Hp == maxHp && healRun.Backpack[hpSlot]?.Quantity == 1,
            "health potion restores to cap and decrements");
        r = InventoryService.UseBackpackItem(healRun, hpSlot, catalog);
        Check(!r.Success && healRun.Backpack[hpSlot]?.Quantity == 1, "health potion not consumed at full health");

        healRun.Character.Mana = 0;
        var mpSlot = healRun.Backpack.FirstEmptySlot();
        healRun.Backpack[mpSlot] = new ItemStack(Ids.ItemManaPotion, 1);
        r = InventoryService.UseBackpackItem(healRun, mpSlot, catalog);
        Check(r.Success && healRun.Character.Mana == 10 && healRun.Backpack[mpSlot] == null,
            "mana potion restores 10 and clears empty stack");

        // Crystal cannot be "used" from inventory.
        var crSlot = healRun.Backpack.FirstEmptySlot();
        healRun.Backpack[crSlot] = new ItemStack(Ids.ItemSaveCrystal, 1);
        r = InventoryService.UseBackpackItem(healRun, crSlot, catalog);
        Check(!r.Success && healRun.Backpack[crSlot]?.Quantity == 1, "save crystal rejected by Use");

        // UI selection & scroll persistence (Card 4a: HUDController, ChestPanelController, ItemCardController).
        var uiRun = NewRunFor(Ids.ArchetypeMage);
        GameSession.Instance!.SetCurrentRunForTesting(uiRun);
        var chestPanel = GD.Load<PackedScene>("res://Scenes/ChestPanel.tscn").Instantiate<ChestPanelController>();
        AddChild(chestPanel);
        // Chest slot 2 has Health Potion x2; click to select.
        chestPanel.ChestCards[2].Select();
        Check(chestPanel.IsChestSelection && chestPanel.SelectedSlot == 2 && chestPanel.ChestCards[2].IsSelected, "chest card click selects slot 2");
        chestPanel.GetNode<Button>("%MoveOneButton").EmitSignal(BaseButton.SignalName.Pressed);
        Check(chestPanel.IsChestSelection && chestPanel.SelectedSlot == 2 && chestPanel.ChestCards[2].IsSelected && uiRun.Chest.Inventory[2]?.Quantity == 1,
            "chest selection persists after MoveOne when stack still has items");
        chestPanel.GetNode<Button>("%MoveOneButton").EmitSignal(BaseButton.SignalName.Pressed);
        Check(chestPanel.SelectedSlot == -1 && !chestPanel.ChestCards[2].IsSelected && chestPanel.GetNode<Button>("%MoveOneButton").Disabled,
            "chest selection clears gracefully when final item in slot is moved");
        // Move starter staff & jacket into backpack as well.
        chestPanel.ChestCards[0].Select();
        chestPanel.GetNode<Button>("%MoveOneButton").EmitSignal(BaseButton.SignalName.Pressed);
        chestPanel.ChestCards[1].Select();
        chestPanel.GetNode<Button>("%MoveOneButton").EmitSignal(BaseButton.SignalName.Pressed);
        chestPanel.QueueFree();

        var hud = GD.Load<PackedScene>("res://Scenes/HUD.tscn").Instantiate<HUDController>();
        AddChild(hud);
        Check(hud.WeaponCard.GetNode<Label>("EmptyState/EmptyLabel").Text == "Weapon · Empty"
            && hud.ArmourCard.GetNode<Label>("EmptyState/EmptyLabel").Text == "Armour · Empty",
            "empty equipment slots label Weapon and Armour distinctly");
        Check(hud.BackpackCards[0].TooltipText.Contains("Health Potion") && hud.BackpackCards[1].TooltipText.Contains("Apprentice Focus Staff")
            && hud.BackpackCards[1].GetNode<Label>("Content/Info/NameLabel").Text == "Focus Staff",
            "item cards show clean short title on card and full item name/details on hover");
        Check(hud.BackpackCards[11].Modulate.A < 0.5f && hud.BackpackCards[0].Modulate.A == 1f,
            "empty backpack slots render quieter than populated item cards");

        hud.BackpackScrollVertical = 42;
        var actualScroll = hud.BackpackScrollVertical; // Godot clamps to the laid-out content range.
        uiRun.Character.Hp = 10;
        // Backpack slot 0 has Health Potion x2; select and use once -> selection & scroll stay.
        hud.BackpackCards[0].Select();
        Check(hud.SelectedBackpackSlot == 0 && hud.BackpackCards[0].IsSelected, "HUD selects backpack slot 0");
        hud.GetNode<Button>("%UseButton").EmitSignal(BaseButton.SignalName.Pressed);
        Check(hud.SelectedBackpackSlot == 0 && hud.BackpackCards[0].IsSelected && uiRun.Backpack[0]?.Quantity == 1 && hud.BackpackScrollVertical == actualScroll,
            "HUD preserves backpack selection and scroll position after using one potion from a stack");
        uiRun.Character.Hp = 10;
        hud.GetNode<Button>("%UseButton").EmitSignal(BaseButton.SignalName.Pressed);
        Check(hud.SelectedBackpackSlot == -1 && !hud.BackpackCards[0].IsSelected && hud.BackpackScrollVertical == actualScroll,
            "HUD clears selection gracefully when final potion is consumed while preserving scroll");

        // Select Apprentice Focus Staff in slot 1 and Equip -> selection follows to equipped Weapon slot.
        hud.BackpackCards[1].Select();
        Check(hud.GetNode<Label>("%DetailsName").Text.Contains("Apprentice Focus Staff"), "details panel shows full unabbreviated item name");
        hud.GetNode<Button>("%EquipButton").EmitSignal(BaseButton.SignalName.Pressed);
        Check(hud.SelectedEquipmentSlot == EquipmentSlotKind.Weapon && hud.WeaponCard.IsSelected && hud.WeaponCard.IsEquipped
            && hud.WeaponCard.GetNode<Label>("Content/Info/InfoLabel").Text.Contains("Equipped • Weapon"),
            "equipping weapon marks card as Equipped and preserves selection on the equipped item");
        hud.GetNode<Button>("%UnequipButton").EmitSignal(BaseButton.SignalName.Pressed);
        Check(hud.SelectedBackpackSlot >= 0 && hud.BackpackCards[hud.SelectedBackpackSlot].IsSelected,
            "unequipping weapon preserves selection on the returned backpack slot");
        hud.QueueFree();
    }

    private static int FindSlot(InventoryState inv, string itemId)
    {
        for (var i = 0; i < InventoryState.SlotCount; i++)
        {
            if (inv[i]?.ItemId == itemId) return i;
        }
        return -1;
    }

    private static int TotalQuantity(InventoryState inv)
    {
        var total = 0;
        foreach (var s in inv.Slots) total += s?.Quantity ?? 0;
        return total;
    }

    // ------------------------------------------------------------------
    // Dice (stage 08 logic, verified headlessly)
    // ------------------------------------------------------------------

    private void RunDiceChecks()
    {
        Section("dice");
        var catalog = GameCatalog.Instance;
        var character = MageWith(intellect: 4, willpower: 5);
        var dice = new DiceService(new ScriptedDieSource());

        var tech = new SkillCheckSpec(AttributeKind.Intellect, Ids.SkillTechnology, 12, "Technology/Intellect");
        var arcana = new SkillCheckSpec(AttributeKind.Willpower, Ids.SkillArcana, 12, "Arcana/Willpower");

        // Technology is untrained for the mage: d20 + (4-1) + 0.
        dice = new DiceService(new ScriptedDieSource(9));
        var res = dice.Resolve(character, tech, catalog);
        Check(res.Total == 12 && res.Success && res.TrainingBonus == 0, "untrained check: 9+3+0=12 meets difficulty");

        dice = new DiceService(new ScriptedDieSource(8));
        res = dice.Resolve(character, tech, catalog);
        Check(res.Total == 11 && !res.Success, "untrained check: 8+3+0=11 fails");

        // Arcana is trained: d20 + (5-1) + 2.
        dice = new DiceService(new ScriptedDieSource(6));
        res = dice.Resolve(character, arcana, catalog);
        Check(res.Total == 12 && res.Success && res.TrainingBonus == 2, "trained check: 6+4+2=12 meets difficulty");

        dice = new DiceService(new ScriptedDieSource(5));
        res = dice.Resolve(character, arcana, catalog);
        Check(res.Total == 11 && !res.Success, "trained check: 5+4+2=11 fails");

        // Die boundaries.
        dice = new DiceService(new ScriptedDieSource(1));
        res = dice.Resolve(character, arcana, catalog);
        Check(res.DieRoll == 1 && res.Total == 7 && !res.Success, "die roll 1 resolves low");
        dice = new DiceService(new ScriptedDieSource(20));
        res = dice.Resolve(character, arcana, catalog);
        Check(res.DieRoll == 20 && res.Total == 26 && res.Success, "die roll 20 resolves high");

        // Tank athletics is trained on Might, not Willpower.
        var tank = new CharacterState { ArchetypeId = Ids.ArchetypeTank, Name = "T" };
        tank.Attributes = new AttributeSet { Might = 5, Agility = 2, Endurance = 5, Intellect = 1, Willpower = 2, Presence = 3 };
        dice = new DiceService(new ScriptedDieSource(6));
        var athletics = new SkillCheckSpec(AttributeKind.Might, Ids.SkillAthletics, 12, "Athletics/Might");
        res = dice.Resolve(tank, athletics, catalog);
        Check(res.Total == 6 + 4 + 2 && res.Success, "tank athletics trained +2 on Might");
        var arcanaOnTank = new SkillCheckSpec(AttributeKind.Willpower, Ids.SkillArcana, 12, "Arcana");
        res = dice.Resolve(tank, arcanaOnTank, catalog);
        Check(res.TrainingBonus == 0, "arcana untrained for tank");

        // Terminal diagnostic: once per run, +10 XP on success only.
        // Healer Intellect 3 -> attribute +2, Technology untrained -> +0.
        var diagRun = NewRunFor(Ids.ArchetypeHealer);
        var xpBefore = diagRun.Character.Xp;
        var first = TerminalDiagnostic.TryRun(diagRun, catalog, new ScriptedDieSource(10));
        Check(first != null && first.Success && first.Total == TerminalDiagnostic.Difficulty,
            "diagnostic: roll meeting DC12 exactly succeeds");
        Check(diagRun.Character.Diagnostic.Attempted && diagRun.Character.Diagnostic.Success,
            "diagnostic marked attempted+success");
        Check(diagRun.Character.Xp == xpBefore + TerminalDiagnostic.SuccessXpAward,
            "diagnostic success awards exactly 10 XP");

        var second = TerminalDiagnostic.TryRun(diagRun, catalog, new ScriptedDieSource(20));
        Check(second == null, "diagnostic cannot rerun after attempt");
        Check(diagRun.Character.Xp == xpBefore + TerminalDiagnostic.SuccessXpAward,
            "duplicate diagnostic attempt awards no extra XP");

        var failRun = NewRunFor(Ids.ArchetypeHealer);
        var failResult = TerminalDiagnostic.TryRun(failRun, catalog, new ScriptedDieSource(9));
        Check(failResult != null && !failResult.Success && failResult.Total == TerminalDiagnostic.Difficulty - 1,
            "diagnostic: one below DC fails");
        Check(failRun.Character.Xp == 0 && failRun.Character.Diagnostic.Attempted && !failRun.Character.Diagnostic.Success,
            "diagnostic failure: attempted, zero XP");

        var snap = SaveData.FromRun(diagRun);
        Check(snap.DiagnosticAttempted && snap.DiagnosticSuccess,
            "diagnostic outcome persists into save DTO");
    }

    // ------------------------------------------------------------------
    // Saves (stage 09 logic, verified headlessly against a temp dir)
    // ------------------------------------------------------------------

    private void RunSaveChecks()
    {
        Section("saves");
        var catalog = GameCatalog.Instance;

        // Build a run, move some state around, snapshot round-trip.
        var run = NewRunFor(Ids.ArchetypeArcher);
        InventoryService.MoveOne(run.Chest.Inventory, 2, run.Backpack, -1, catalog);
        InventoryService.MoveOne(run.Chest.Inventory, 0, run.Backpack, -1, catalog);
        var weaponSlot = FindSlot(run.Backpack, Ids.ItemTrainingBow);
        InventoryService.EquipFromBackpack(run, weaponSlot, catalog);
        run.Character.Hp = 10;
        run.Character.Xp = 10;
        run.Character.Diagnostic.Attempted = true;
        run.Character.Diagnostic.Success = true;
        run.CarriedGold = 7;
        run.PositionX = 123f;
        run.PositionY = 456f;

        var dto = SaveData.FromRun(run);
        var restored = dto.ToRunState();
        Check(restored.Character.Name == run.Character.Name
            && restored.Character.Attributes.Total == run.Character.Attributes.Total
            && restored.Character.Hp == 10 && restored.Character.Xp == 10
            && restored.CarriedGold == 7
            && restored.PositionX == 123f && restored.PositionY == 456f
            && restored.Character.Diagnostic.Attempted && restored.Character.Diagnostic.Success,
            "save DTO round-trips identity, vitals, xp, gold, position, diagnostic");
        Check(restored.Backpack.CountItem(Ids.ItemHealthPotion) == run.Backpack.CountItem(Ids.ItemHealthPotion),
            "backpack contents round-trip");
        Check(restored.Equipment.Weapon?.ItemId == Ids.ItemTrainingBow, "equipment round-trips");
        Check(restored.Chest.Gold == 30 && restored.Chest.Inventory.CountItem(Ids.ItemSaveCrystal) == 3,
            "chest contents round-trip (crystals stay in chest)");
        Check(!ReferenceEquals(restored.Backpack, run.Backpack) && !ReferenceEquals(restored.Character.Attributes, run.Character.Attributes),
            "round-trip produces independent objects");

        // Crystal spend semantics on the DTO.
        var crystalsBefore = dto.BackpackSlots!.Count(s => s?.ItemId == Ids.ItemSaveCrystal) ;
        Check(crystalsBefore == 0, "chest crystals are not in backpack snapshot");
        InventoryService.MoveStack(run.Chest.Inventory, FindSlot(run.Chest.Inventory, Ids.ItemSaveCrystal), run.Backpack, -1, catalog);
        dto = SaveData.FromRun(run);
        Check(dto.ConsumeOneSaveCrystal(), "snapshot can consume one carried crystal");
        Check(run.Backpack.CountItem(Ids.ItemSaveCrystal) == 3, "consuming on snapshot does not touch live run");
        var dto2 = SaveData.FromRun(run);
        dto2.ConsumeOneSaveCrystal();
        Check(dto2.BackpackSlots!.Where(s => s?.ItemId == Ids.ItemSaveCrystal).Sum(s => s!.Quantity) == 2,
            "post-consumption snapshot has one fewer crystal");

        // File round-trip through SaveService (temp dir).
        SaveService.DeleteSave();
        var write = SaveService.WriteSave(dto);
        Check(write.Success, "save file writes to temp dir");
        Check(SaveService.SaveExists(), "save file exists after write");
        Check(SaveService.TryLoadSave(out var loaded, out var loadError) && loaded != null,
            $"save file loads and validates ({loadError})");
        var loadedRun = loaded!.ToRunState();
        Check(loadedRun.Character.RunId == run.Character.RunId
            && loadedRun.Backpack.CountItem(Ids.ItemSaveCrystal) == 2
            && loadedRun.Chest.Gold == 30,
            "loaded run matches saved post-consumption state");

        // Repeated loads do not restore the spent crystal.
        Check(SaveService.TryLoadSave(out var loaded2, out _) && loaded2 != null, "second load succeeds");
        Check(loaded2!.BackpackSlots!.Where(s => s?.ItemId == Ids.ItemSaveCrystal).Sum(s => s!.Quantity) == 2,
            "repeated load keeps post-save crystal count");

        // Corrupt file rejected without touching session.
        File.WriteAllText(SaveService.SavePath, "{ not json !!!");
        Check(!SaveService.TryLoadSave(out _, out var corruptError) && corruptError.Contains("corrupt"),
            $"corrupt save rejected ({corruptError})");

        // Unsupported version rejected.
        File.WriteAllText(SaveService.SavePath, "{\"Version\":99}");
        Check(!SaveService.TryLoadSave(out _, out var versionError) && versionError.Contains("version"),
            $"unsupported version rejected ({versionError})");

        // Unknown item id rejected.
        var bad = SaveData.FromRun(run);
        bad.BackpackSlots![0] = new ItemStack("not_a_real_item", 1);
        File.WriteAllText(SaveService.SavePath, System.Text.Json.JsonSerializer.Serialize(bad));
        Check(!SaveService.TryLoadSave(out _, out var itemError), "unknown item id rejected");

        // Invalid position recovered to spawn, not rejected.
        var badPos = SaveData.FromRun(run);
        badPos.PositionX = 99999f;
        badPos.PositionY = -99999f;
        File.WriteAllText(SaveService.SavePath, System.Text.Json.JsonSerializer.Serialize(badPos));
        Check(SaveService.TryLoadSave(out var posLoaded, out _) && posLoaded != null
            && posLoaded.PositionX == SafehouseLayout.SpawnPoint.X && posLoaded.PositionY == SafehouseLayout.SpawnPoint.Y,
            "out-of-bounds position recovered to spawn");

        // GameSession-level crystal accounting.
        var sessionRun = NewRunFor(Ids.ArchetypeHealer);
        // put 3 crystals in backpack
        for (var i = 0; i < 3; i++)
        {
            var slot = sessionRun.Backpack.FirstEmptySlot();
            if (slot >= 0) sessionRun.Backpack[slot] = new ItemStack(Ids.ItemSaveCrystal, 1);
        }
        Check(GameSessionCarriedCrystals(sessionRun) == 3, "three carried crystals counted");

        // Simulated failed write: snapshot -> consume -> write fails -> nothing committed.
        SaveService.DeleteSave();
        var goodWrite = SaveService.WriteSave(SaveData.FromRun(sessionRun));
        Check(goodWrite.Success, "baseline save writes before failure test");
        SaveServiceTestDirectory.BreakWrites();
        var preSave = SaveData.FromRun(sessionRun);
        preSave.ConsumeOneSaveCrystal();
        var failedWrite = SaveService.WriteSave(preSave);
        Check(!failedWrite.Success, "failed write reports failure");
        SaveServiceTestDirectory.Restore();
        Check(SaveService.SaveExists(), "previous save preserved after failed write");
        Check(SaveService.TryLoadSave(out var stillThere, out _) && stillThere != null,
            "old save still loads after failed write");
        Check(sessionRun.Backpack.CountItem(Ids.ItemSaveCrystal) == 3,
            "failed write consumes no crystal (live run untouched)");

        // Three sequential saves exhaust three crystals.
        var gs = GameSession.Instance;
        Check(gs != null, "GameSession autoload available");
        if (gs != null)
        {
            gs.SetCurrentRunForTesting(sessionRun);
            var crystals = gs.CarriedSaveCrystals();
            Check(crystals == 3, "session sees three carried crystals");
            var s1 = gs.SaveGame();
            Check(s1.Success && gs.CarriedSaveCrystals() == 2, "first save spends one crystal");
            var s2 = gs.SaveGame();
            Check(s2.Success && gs.CarriedSaveCrystals() == 1, "second save spends one crystal");
            var s3 = gs.SaveGame();
            Check(s3.Success && gs.CarriedSaveCrystals() == 0, "third save spends one crystal");
            var s4 = gs.SaveGame();
            Check(!s4.Success, "fourth save rejected: no crystals");
        }
    }

    private static int GameSessionCarriedCrystals(RunState run) => run.Backpack.CountItem(Ids.ItemSaveCrystal);
}
