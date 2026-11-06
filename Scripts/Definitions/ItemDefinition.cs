using DevilGate.Scripts.Data;
using Godot;

namespace DevilGate.Scripts.Definitions;

/// <summary>
/// Static description of one item kind. Descriptive combat values (Power,
/// Armour) are for future use and do not affect prototype rules.
/// </summary>
public partial class ItemDefinition : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public string Description { get; set; } = "";
    [Export] public ItemCategory Category { get; set; } = ItemCategory.Weapon;
    [Export] public int StackLimit { get; set; } = 1;

    // Descriptive future-combat values (prototype does not use them).
    [Export] public int Power { get; set; }
    [Export] public int ArmourValue { get; set; }
    [Export] public int HealAmount { get; set; }
    [Export] public int ManaAmount { get; set; }

    /// <summary>Empty means equippable by every archetype.</summary>
    [Export] public string ArchetypeId { get; set; } = "";

    [Export] public string IconPath { get; set; } = "";

    public bool IsStackable => StackLimit > 1;

    public bool IsEquippableBy(string archetypeId)
    {
        return Category is ItemCategory.Weapon or ItemCategory.Armour
            && (ArchetypeId.Length == 0 || ArchetypeId == archetypeId);
    }

    public Texture2D? LoadIcon()
    {
        if (IconPath.Length == 0 || !ResourceLoader.Exists(IconPath))
        {
            return null;
        }
        return GD.Load<Texture2D>(IconPath);
    }
}
