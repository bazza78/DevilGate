using System.Collections.Generic;
using System.Linq;
using DevilGate.Scripts.Data;
using Godot;

namespace DevilGate.Scripts.Definitions;

/// <summary>
/// Single source of truth for archetypes, items, skills and chest seeding.
/// Definitions are Godot Resources created in code; stable IDs connect them to
/// save data. Access via <see cref="Instance"/>.
/// </summary>
public sealed class GameCatalog
{
    private static GameCatalog? _instance;

    public static GameCatalog Instance => _instance ??= new GameCatalog();

    public const int StartingChestGold = 30;
    public const int BackpackSlotCount = InventoryState.SlotCount;
    public const int ChestSlotCount = InventoryState.SlotCount;

    public IReadOnlyDictionary<string, ArchetypeDefinition> Archetypes { get; }
    public IReadOnlyDictionary<string, ItemDefinition> Items { get; }
    public IReadOnlyDictionary<string, SkillDefinition> Skills { get; }

    private GameCatalog()
    {
        Archetypes = BuildArchetypes().ToDictionary(a => a.Id);
        Items = BuildItems().ToDictionary(i => i.Id);
        Skills = BuildSkills().ToDictionary(s => s.Id);
    }

    public ArchetypeDefinition GetArchetype(string id)
    {
        if (!Archetypes.TryGetValue(id, out var def))
        {
            throw new KeyNotFoundException($"Unknown archetype id '{id}'.");
        }
        return def;
    }

    public bool TryGetArchetype(string id, out ArchetypeDefinition? def)
    {
        return Archetypes.TryGetValue(id, out def);
    }

    public ItemDefinition GetItem(string id)
    {
        if (!Items.TryGetValue(id, out var def))
        {
            throw new KeyNotFoundException($"Unknown item id '{id}'.");
        }
        return def;
    }

    public bool TryGetItem(string id, out ItemDefinition? def)
    {
        return Items.TryGetValue(id, out def);
    }

    public SkillDefinition GetSkill(string id)
    {
        if (!Skills.TryGetValue(id, out var def))
        {
            throw new KeyNotFoundException($"Unknown skill id '{id}'.");
        }
        return def;
    }

    public IEnumerable<ArchetypeDefinition> AllArchetypes =>
        new[] { Archetypes[Ids.ArchetypeMage], Archetypes[Ids.ArchetypeTank], Archetypes[Ids.ArchetypeArcher], Archetypes[Ids.ArchetypeKnight], Archetypes[Ids.ArchetypeHealer] };

    /// <summary>
    /// The archetype-dependent chest contents for a brand-new run. This is
    /// data only — it is applied once by the run factory, never on open/load.
    /// </summary>
    public IReadOnlyList<(string ItemId, int Quantity)> ChestSeedFor(ArchetypeDefinition archetype)
    {
        return new (string, int)[]
        {
            (archetype.WeaponItemId, 1),
            (Ids.ItemBasicJacket, 1),
            (Ids.ItemHealthPotion, 2),
            (Ids.ItemManaPotion, 2),
            (Ids.ItemSaveCrystal, 3),
        };
    }

    private static List<ArchetypeDefinition> BuildArchetypes()
    {
        return new List<ArchetypeDefinition>
        {
            new()
            {
                Id = Ids.ArchetypeMage,
                DisplayName = "Mage",
                SpeciesId = Ids.SpeciesDarkElf,
                SpeciesDisplayName = "Dark Elf",
                ThemeColor = new Color("8a5cf6"),
                WeaponItemId = Ids.ItemApprenticeStaff,
                TrainedSkillIds = new[] { Ids.SkillArcana },
                SuggestedMight = 1, SuggestedAgility = 2, SuggestedEndurance = 2,
                SuggestedIntellect = 4, SuggestedWillpower = 5, SuggestedPresence = 4,
            },
            new()
            {
                Id = Ids.ArchetypeTank,
                DisplayName = "Tank",
                SpeciesId = Ids.SpeciesOrc,
                SpeciesDisplayName = "Orc",
                ThemeColor = new Color("6f9e4a"),
                WeaponItemId = Ids.ItemReinforcedClub,
                TrainedSkillIds = new[] { Ids.SkillAthletics },
                SuggestedMight = 5, SuggestedAgility = 2, SuggestedEndurance = 5,
                SuggestedIntellect = 1, SuggestedWillpower = 2, SuggestedPresence = 3,
            },
            new()
            {
                Id = Ids.ArchetypeArcher,
                DisplayName = "Archer",
                SpeciesId = Ids.SpeciesElf,
                SpeciesDisplayName = "Elf",
                ThemeColor = new Color("4cc38a"),
                WeaponItemId = Ids.ItemTrainingBow,
                TrainedSkillIds = new[] { Ids.SkillPrecision },
                SuggestedMight = 2, SuggestedAgility = 5, SuggestedEndurance = 3,
                SuggestedIntellect = 3, SuggestedWillpower = 2, SuggestedPresence = 3,
            },
            new()
            {
                Id = Ids.ArchetypeKnight,
                DisplayName = "Knight",
                SpeciesId = Ids.SpeciesHuman,
                SpeciesDisplayName = "Human",
                ThemeColor = new Color("6f8fc4"),
                WeaponItemId = Ids.ItemServiceBlade,
                TrainedSkillIds = new[] { Ids.SkillLeadership },
                SuggestedMight = 4, SuggestedAgility = 3, SuggestedEndurance = 4,
                SuggestedIntellect = 2, SuggestedWillpower = 2, SuggestedPresence = 3,
            },
            new()
            {
                Id = Ids.ArchetypeHealer,
                DisplayName = "Healer",
                SpeciesId = Ids.SpeciesHighHuman,
                SpeciesDisplayName = "High Human",
                ThemeColor = new Color("e8c15a"),
                WeaponItemId = Ids.ItemMedicWand,
                TrainedSkillIds = new[] { Ids.SkillMedicine },
                SuggestedMight = 1, SuggestedAgility = 2, SuggestedEndurance = 3,
                SuggestedIntellect = 3, SuggestedWillpower = 5, SuggestedPresence = 4,
            },
        };
    }

    private static List<ItemDefinition> BuildItems()
    {
        ItemDefinition Weapon(string id, string name, string desc, string archetypeId) => new()
        {
            Id = id,
            DisplayName = name,
            Description = desc,
            Category = ItemCategory.Weapon,
            StackLimit = 1,
            Power = 2,
            ArchetypeId = archetypeId,
            IconPath = $"res://Assets/Placeholders/Items/{id}.png",
        };

        return new List<ItemDefinition>
        {
            Weapon(Ids.ItemApprenticeStaff, "Apprentice Focus Staff", "A simple focus staff for apprentice magic.", Ids.ArchetypeMage),
            Weapon(Ids.ItemReinforcedClub, "Reinforced Club", "A heavy club bound with scrap metal.", Ids.ArchetypeTank),
            Weapon(Ids.ItemTrainingBow, "Training Composite Bow", "A light bow for ranged practice.", Ids.ArchetypeArcher),
            Weapon(Ids.ItemServiceBlade, "Service Blade", "A standard-issue sidearm of the guild guard.", Ids.ArchetypeKnight),
            Weapon(Ids.ItemMedicWand, "Medic's Focus Wand", "A focus wand tuned for healing rites.", Ids.ArchetypeHealer),
            new()
            {
                Id = Ids.ItemBasicJacket, DisplayName = "Basic Protective Jacket",
                Description = "A worn jacket that still turns a glancing blow.",
                Category = ItemCategory.Armour, StackLimit = 1, ArmourValue = 1,
                IconPath = $"res://Assets/Placeholders/Items/{Ids.ItemBasicJacket}.png",
            },
            new()
            {
                Id = Ids.ItemHealthPotion, DisplayName = "Health Potion",
                Description = "Restores 15 health when used.",
                Category = ItemCategory.HealthPotion, StackLimit = 9, HealAmount = 15,
                IconPath = $"res://Assets/Placeholders/Items/{Ids.ItemHealthPotion}.png",
            },
            new()
            {
                Id = Ids.ItemManaPotion, DisplayName = "Mana Potion",
                Description = "Restores 10 mana when used.",
                Category = ItemCategory.ManaPotion, StackLimit = 9, ManaAmount = 10,
                IconPath = $"res://Assets/Placeholders/Items/{Ids.ItemManaPotion}.png",
            },
            new()
            {
                Id = Ids.ItemSaveCrystal, DisplayName = "Save Crystal",
                Description = "Crystallised memory. Consumed to record a save.",
                Category = ItemCategory.SaveCrystal, StackLimit = 9,
                IconPath = $"res://Assets/Placeholders/Items/{Ids.ItemSaveCrystal}.png",
            },
        };
    }

    private static List<SkillDefinition> BuildSkills()
    {
        return new List<SkillDefinition>
        {
            new() { Id = Ids.SkillArcana, DisplayName = "Arcana", Attribute = AttributeKind.Willpower, Description = "Weave and shape magical energies." },
            new() { Id = Ids.SkillAthletics, DisplayName = "Athletics", Attribute = AttributeKind.Might, Description = "Physical power, force and endurance." },
            new() { Id = Ids.SkillPrecision, DisplayName = "Precision", Attribute = AttributeKind.Agility, Description = "Accurate ranged attacks and fine control." },
            new() { Id = Ids.SkillLeadership, DisplayName = "Leadership", Attribute = AttributeKind.Presence, Description = "Command, inspire and direct others." },
            new() { Id = Ids.SkillMedicine, DisplayName = "Medicine", Attribute = AttributeKind.Intellect, Description = "Field medicine and anatomical knowledge." },
            new() { Id = Ids.SkillTechnology, DisplayName = "Technology", Attribute = AttributeKind.Intellect, Description = "Machines, terminals and devices." },
        };
    }
}
