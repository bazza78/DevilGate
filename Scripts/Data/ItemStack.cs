namespace DevilGate.Scripts.Data;

public enum ItemCategory
{
    Weapon,
    Armour,
    HealthPotion,
    ManaPotion,
    SaveCrystal,
}

public enum EquipmentSlotKind
{
    Weapon,
    Armour,
}

/// <summary>A stack of identical items. Plain data; serialized into saves.</summary>
public sealed class ItemStack
{
    public string ItemId { get; set; } = "";
    public int Quantity { get; set; } = 1;

    public ItemStack() { }

    public ItemStack(string itemId, int quantity = 1)
    {
        ItemId = itemId;
        Quantity = quantity;
    }

    public ItemStack Clone() => new(ItemId, Quantity);

    public override string ToString() => $"{ItemId} x{Quantity}";
}
