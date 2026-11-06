namespace DevilGate.Scripts.Data;

/// <summary>
/// A fixed set of item slots. Used for the 12-slot backpack and the 12-slot
/// chest storage. Slot order is stable for UI and saves.
/// </summary>
public sealed class InventoryState
{
    public const int SlotCount = 12;

    public ItemStack?[] Slots { get; } = new ItemStack?[SlotCount];

    public ItemStack? this[int index]
    {
        get => Slots[index];
        set => Slots[index] = value;
    }

    public int FirstEmptySlot()
    {
        for (var i = 0; i < SlotCount; i++)
        {
            if (Slots[i] == null)
            {
                return i;
            }
        }
        return -1;
    }

    public int CountEmptySlots()
    {
        var n = 0;
        for (var i = 0; i < SlotCount; i++)
        {
            if (Slots[i] == null)
            {
                n++;
            }
        }
        return n;
    }

    public bool IsFull => CountEmptySlots() == 0;

    /// <summary>Total quantity of a given item across all slots.</summary>
    public int CountItem(string itemId)
    {
        var total = 0;
        foreach (var slot in Slots)
        {
            if (slot != null && slot.ItemId == itemId)
            {
                total += slot.Quantity;
            }
        }
        return total;
    }

    public InventoryState Clone()
    {
        var clone = new InventoryState();
        for (var i = 0; i < SlotCount; i++)
        {
            clone.Slots[i] = Slots[i]?.Clone();
        }
        return clone;
    }
}
