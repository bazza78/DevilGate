using DevilGate.Scripts.Data;
using DevilGate.Scripts.Definitions;
using DevilGate.Scripts.Systems;
using Godot;

namespace DevilGate.Scripts;

/// <summary>
/// Autoloaded shared session owner. Holds the current run state and exposes
/// cross-scene services. UI reads state from here; it never keeps its own copy.
/// </summary>
public partial class GameSession : Node
{
    public static GameSession? Instance { get; private set; }

    private bool _saving;

    /// <summary>The active run, or null while on title / character creation.</summary>
    public RunState? CurrentRun { get; private set; }

    /// <summary>Incremented on every state change; used for unsaved-change tracking.</summary>
    public int StateVersion { get; private set; }

    /// <summary>StateVersion at the last successful save/load; null if never saved.</summary>
    public int? SavedVersion { get; private set; }

    public string? LastSaveTime { get; private set; }

    public bool HasUnsavedChanges =>
        CurrentRun != null && SavedVersion != StateVersion;

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        GetTree().AutoAcceptQuit = false;
        ComfortSettings.Load();
        AddChild(new GameAudio());
        AddChild(new UiComfort());
    }

    public override void _ExitTree()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private bool _quitDialog;
    public void RequestQuit(Node owner)
    {
        if (_quitDialog) return;
        foreach (var player in GetTree().GetNodesInGroup("route_player").OfType<World.PlayerController>()) { player.Stop(); player.WritePositionToRun(); }
        if (!HasUnsavedChanges) { GetTree().Quit(); return; }
        _quitDialog = true;
        var dialog = new ConfirmationDialog { Title = "Unsaved progress", DialogText = "Quit and lose progress since your last crystal save?", ProcessMode = ProcessModeEnum.Always };
        owner.AddChild(dialog);
        dialog.Confirmed += () => GetTree().Quit();
        dialog.Canceled += () => { _quitDialog = false; dialog.QueueFree(); };
        dialog.PopupCentered();
    }
    public override void _Notification(int what)
    {
        if (!IsInsideTree()) return;
        if (what == NotificationWMCloseRequest) RequestQuit(this);
        if (what == NotificationApplicationFocusOut && HasRun) GetTree().Paused = true;
        if (what == NotificationApplicationFocusIn) GetTree().Paused = false;
    }

    public bool HasRun => CurrentRun != null;

    public bool HasSave() => SaveService.SaveExists();

    /// <summary>Create and activate a new run. Replaces any current run.</summary>
    public void StartNewRun(CharacterCreationResult creation)
    {
        CurrentRun = RunFactory.CreateNewRun(creation, GameCatalog.Instance);
        SavedVersion = null;
        EmitChanged();
    }

    public void BeginAdventure()
    {
        StartTemporaryRun();
        var run = CurrentRun!;
        if (TestingRoster.Enabled) run.Character.GenderId=Ids.GenderFemale;
        run.CreationStage = 0;
        run.GuildTerminalVisited = false;
        // The rack supplies equipment after identity and training are confirmed.
        for (int i=0;i<InventoryState.SlotCount;i++)
        {
            var stack = run.Chest.Inventory[i];
            if (stack != null && (stack.ItemId == Ids.ItemBasicJacket ||
                GameCatalog.Instance.GetItem(stack.ItemId).Category == ItemCategory.Weapon))
                run.Chest.Inventory[i] = null;
        }
        EmitChanged();
    }

    public void ClearRun()
    {
        CurrentRun = null;
        SavedVersion = null;
        EmitChanged();
    }

    /// <summary>Test/dev hook: install a mage wanderer run directly.</summary>
    public void StartTemporaryRun()
    {
        var catalog = GameCatalog.Instance;
        var archetype = catalog.GetArchetype(Ids.ArchetypeMage);
        StartNewRun(new CharacterCreationResult
        {
            Name = "Wanderer",
            ArchetypeId = archetype.Id,
            GenderId = Ids.GenderMale,
            Attributes = archetype.CreateSuggestedAttributes(),
        });
    }

    /// <summary>Test hook: install a run directly (used by headless checks).</summary>
    public void SetCurrentRunForTesting(RunState run)
    {
        CurrentRun = run;
        SavedVersion = null;
        EmitChanged();
    }

    // ------------------------------------------------------------------
    // Saves (one manual slot; one carried crystal per successful save)
    // ------------------------------------------------------------------

    /// <summary>Carried save crystals in the backpack (chest crystals can't be spent remotely).</summary>
    public int CarriedSaveCrystals()
    {
        return CurrentRun?.Backpack.CountItem(Ids.ItemSaveCrystal) ?? 0;
    }

    /// <summary>
    /// Save the current run. The snapshot already contains the crystal
    /// deduction; the in-memory deduction is only committed after the write
    /// succeeds, so a failed write spends nothing.
    /// </summary>
    public SaveService.SaveResult SaveGame()
    {
        var run = CurrentRun;
        if (run == null)
        {
            return SaveService.SaveResult.Fail("No character to save.");
        }
        if (_saving)
        {
            return SaveService.SaveResult.Fail("A save is already in progress.");
        }
        if (CarriedSaveCrystals() <= 0)
        {
            return SaveService.SaveResult.Fail("No save crystals carried. Crystals left in the chest cannot be spent.");
        }

        _saving = true;
        try
        {
            var snapshot = SaveData.FromRun(run);
            snapshot.SavedAtUtc = DateTimeOffset.UtcNow.ToString("O");
            if (!snapshot.ConsumeOneSaveCrystal())
            {
                return SaveService.SaveResult.Fail("No save crystal could be consumed.");
            }

            var write = SaveService.WriteSave(snapshot);
            if (!write.Success)
            {
                return write;
            }

            // Commit: the live run becomes exactly the saved post-consumption state.
            CurrentRun = snapshot.ToRunState();
            EmitChanged();
            SavedVersion = StateVersion;
            LastSaveTime = snapshot.SavedAtUtc;
            return SaveService.SaveResult.Ok();
        }
        finally
        {
            _saving = false;
        }
    }

    /// <summary>Load the save slot into a fresh run. Returns error text on failure.</summary>
    public bool TryLoadGame(out string error)
    {
        error = "";
        if (!SaveService.TryLoadSave(out var data, out error) || data == null)
        {
            return false;
        }

        CurrentRun = data.ToRunState();
        EmitChanged();
        SavedVersion = StateVersion;
        LastSaveTime = data.SavedAtUtc;
        return true;
    }

    public void MarkUnsaved() => StateVersion++;

    /// <summary>Announce that run state changed so UI can redraw.</summary>
    public void EmitChanged()
    {
        StateVersion++;
        StateChanged?.Invoke();
    }

    /// <summary>Fired whenever run state (inventory, vitals, xp, ...) changes.</summary>
    public event Action? StateChanged;
}
