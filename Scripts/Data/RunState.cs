using DevilGate.Scripts.Data;

namespace DevilGate.Scripts.Data;

/// <summary>Equipment slots: one weapon, one armour piece.</summary>
public sealed class EquipmentState
{
    public ItemStack? Weapon { get; set; }
    public ItemStack? Armour { get; set; }

    public ItemStack? this[EquipmentSlotKind kind]
    {
        get => kind == EquipmentSlotKind.Weapon ? Weapon : Armour;
        set
        {
            if (kind == EquipmentSlotKind.Weapon) Weapon = value;
            else Armour = value;
        }
    }

    public EquipmentState Clone() => new() { Weapon = Weapon?.Clone(), Armour = Armour?.Clone() };
}

/// <summary>The 12-slot chest plus its separate gold balance.</summary>
public sealed class ChestState
{
    public InventoryState Inventory { get; } = new();
    public int Gold { get; set; }

    public ChestState Clone()
    {
        var clone = new ChestState { Gold = Gold };
        for (var i = 0; i < InventoryState.SlotCount; i++)
        {
            clone.Inventory[i] = Inventory[i]?.Clone();
        }
        return clone;
    }
}

/// <summary>
/// Everything that makes up one playthrough: character, carried items,
/// equipment, chest contents and world position. Plain data so it can be
/// snapshotted for saves without touching live nodes.
/// </summary>
public sealed class RunState
{
    public const string HouseSceneId = "safehouse";

    public CharacterState Character { get; set; } = new();
    public InventoryState Backpack { get; set; } = new();
    public EquipmentState Equipment { get; set; } = new();
    public ChestState Chest { get; set; } = new();
    public int CarriedGold { get; set; }
    // Default completed preserves older saves and existing character factories.
    public string LocationId { get; set; } = "safehouse";
    public int InductionStage { get; set; }
    public EchoQuestState Echo {get;set;} = new();
    public WoodlandBattleState FilterBattle {get;set;} = new() {EnemyHp=24};
    public bool HasFilterSample => Echo.Stage==4;
    public WoodlandBattleState WoodlandBattle {get;set;} = new();
    public bool MagicAwakened { get; set; }
    public string[] LearnedSpellIds { get; set; } = Array.Empty<string>();
    public bool HasGuildAccessDrive => InductionStage >= 2;
    public bool HasGuildCard => InductionStage >= 2;
    public bool GuildTerminalVisited { get; set; } = true;
    public int CreationStage { get; set; } = 2;
    public bool WakingIntroductionSeen { get; set; } = true;
    public bool BattleHintDismissed { get; set; }
    public float PositionX { get; set; }
    public float PositionY { get; set; }

    public RunState Clone() => new()
    {
        Echo=Echo.Clone(),
        FilterBattle=FilterBattle.Clone(),
        WoodlandBattle = WoodlandBattle.Clone(),
        LocationId = LocationId,
        InductionStage = InductionStage,
        MagicAwakened = MagicAwakened,
        LearnedSpellIds = (string[])LearnedSpellIds.Clone(),
        GuildTerminalVisited = GuildTerminalVisited,
        CreationStage = CreationStage,
        WakingIntroductionSeen = WakingIntroductionSeen,
        BattleHintDismissed = BattleHintDismissed,
        Character = Character.Clone(),
        Backpack = Backpack.Clone(),
        Equipment = Equipment.Clone(),
        Chest = Chest.Clone(),
        CarriedGold = CarriedGold,
        PositionX = PositionX,
        PositionY = PositionY,
    };
}

/// <summary>What the character-creation screen hands to the session.</summary>
public sealed class CharacterCreationResult
{
    public string Name { get; set; } = "";
    public string ArchetypeId { get; set; } = Ids.ArchetypeMage;
    public string GenderId { get; set; } = Ids.GenderMale;
    public AttributeSet Attributes { get; set; } = new();
}
