using DevilGate.Scripts.Data;

namespace DevilGate.Scripts.Data;

/// <summary>
/// Versioned save file DTO (schema version 1). Contains only stable IDs and
/// plain values — never live nodes, textures or Godot resources.
/// </summary>
public sealed class SaveData
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;
    public string RunId { get; set; } = "";
    public string Name { get; set; } = "";
    public string ArchetypeId { get; set; } = "";
    public string GenderId { get; set; } = "";
    public string HouseSceneId { get; set; } = RunState.HouseSceneId;

    public AttributeSet Attributes { get; set; } = new();
    public int Hp { get; set; }
    public int Mana { get; set; }
    public int Level { get; set; } = 1;
    public int Xp { get; set; }
    public int CarriedGold { get; set; }

    public ItemStack?[] BackpackSlots { get; set; } = NewSlots();
    public ItemStack? Weapon { get; set; }
    public ItemStack? Armour { get; set; }
    public ItemStack?[] ChestSlots { get; set; } = NewSlots();
    public int ChestGold { get; set; }

    // Default completed preserves older saves and existing character factories.
    public string LocationId { get; set; } = "safehouse";
    public int InductionStage { get; set; }
    public EchoQuestState Echo {get;set;} = new();
    public WoodlandBattleState FilterBattle {get;set;} = new() {EnemyHp=24};
    public bool HasFilterSample => Echo.Stage==4;
    public WoodlandBattleState WoodlandBattle {get;set;} = new();
    public bool MagicAwakened { get; set; }
    public string[] LearnedSpellIds { get; set; } = Array.Empty<string>();
    public bool GuildTerminalVisited { get; set; } = true;
    public int CreationStage { get; set; } = 2;
    public bool WakingIntroductionSeen { get; set; } = true;
    public float PositionX { get; set; }
    public float PositionY { get; set; }

    public bool DiagnosticAttempted { get; set; }
    public bool DiagnosticSuccess { get; set; }

    private static ItemStack?[] NewSlots() => new ItemStack?[InventoryState.SlotCount];

    public static SaveData FromRun(RunState run)
    {
        var c = run.Character;
        return new SaveData
        {
            Version = CurrentVersion,
            RunId = c.RunId,
            Name = c.Name,
            ArchetypeId = c.ArchetypeId,
            GenderId = c.GenderId,
            HouseSceneId = run.LocationId,
            Echo=run.Echo.Clone(),
            FilterBattle=run.FilterBattle.Clone(),
            WoodlandBattle = run.WoodlandBattle.Clone(),
            LocationId = run.LocationId,
            InductionStage = run.InductionStage,
            MagicAwakened = run.MagicAwakened,
            LearnedSpellIds = (string[])run.LearnedSpellIds.Clone(),
            Attributes = c.Attributes.Clone(),
            Hp = c.Hp,
            Mana = c.Mana,
            Level = c.Level,
            Xp = c.Xp,
            CarriedGold = run.CarriedGold,
            BackpackSlots = CloneSlots(run.Backpack),
            Weapon = run.Equipment.Weapon?.Clone(),
            Armour = run.Equipment.Armour?.Clone(),
            ChestSlots = CloneSlots(run.Chest.Inventory),
            ChestGold = run.Chest.Gold,
            GuildTerminalVisited = run.GuildTerminalVisited,
            CreationStage = run.CreationStage,
            WakingIntroductionSeen = run.WakingIntroductionSeen,
            PositionX = run.PositionX,
            PositionY = run.PositionY,
            DiagnosticAttempted = c.Diagnostic.Attempted,
            DiagnosticSuccess = c.Diagnostic.Success,
        };
    }

    /// <summary>Rebuild a run from validated data. Does not re-seed anything.</summary>
    public RunState ToRunState()
    {
        var run = new RunState
        {
            CarriedGold = CarriedGold,
            Echo=Echo.Clone(),
            FilterBattle=FilterBattle.Clone(),
            WoodlandBattle = WoodlandBattle.Clone(),
            LocationId = World.WorldRoute.Normalize(LocationId),
            InductionStage = InductionStage,
            MagicAwakened = MagicAwakened,
            LearnedSpellIds = (string[])LearnedSpellIds.Clone(),
            GuildTerminalVisited = GuildTerminalVisited,
            CreationStage = CreationStage,
            WakingIntroductionSeen = WakingIntroductionSeen,
            PositionX = PositionX,
            PositionY = PositionY,
        };
        var c = run.Character;
        c.RunId = RunId;
        c.Name = Name;
        c.ArchetypeId = ArchetypeId;
        c.GenderId = GenderId;
        c.Attributes = Attributes.Clone();
        c.Hp = Hp;
        c.Mana = Mana;
        c.Level = Level;
        c.Xp = Xp;
        Systems.Progression.Reconcile(c);
        c.Diagnostic.Attempted = DiagnosticAttempted;
        c.Diagnostic.Success = DiagnosticSuccess;

        CopySlots(BackpackSlots, run.Backpack);
        run.Equipment.Weapon = Weapon?.Clone();
        run.Equipment.Armour = Armour?.Clone();
        CopySlots(ChestSlots, run.Chest.Inventory);
        run.Chest.Gold = ChestGold;
        return run;
    }

    /// <summary>Remove one carried save crystal from the snapshot (post-consumption state).</summary>
    public bool ConsumeOneSaveCrystal()
    {
        if (BackpackSlots == null) return false;
        for (var i = 0; i < BackpackSlots.Length; i++)
        {
            var stack = BackpackSlots[i];
            if (stack != null && stack.ItemId == Ids.ItemSaveCrystal && stack.Quantity > 0)
            {
                stack.Quantity -= 1;
                if (stack.Quantity <= 0) BackpackSlots[i] = null;
                return true;
            }
        }
        return false;
    }

    private static ItemStack?[] CloneSlots(InventoryState inventory)
    {
        var slots = new ItemStack?[InventoryState.SlotCount];
        for (var i = 0; i < InventoryState.SlotCount; i++)
        {
            slots[i] = inventory[i]?.Clone();
        }
        return slots;
    }

    private static void CopySlots(ItemStack?[]? source, InventoryState destination)
    {
        if (source == null) return;
        for (var i = 0; i < InventoryState.SlotCount && i < source.Length; i++)
        {
            destination[i] = source[i]?.Clone();
        }
    }
}
