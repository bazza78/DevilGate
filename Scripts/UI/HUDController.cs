using DevilGate.Scripts.Data;
using DevilGate.Scripts.Definitions;
using DevilGate.Scripts.Systems;
using Godot;

namespace DevilGate.Scripts;

/// <summary>
/// Right sidebar: portrait, identity, vitals, equipment slots, scrollable
/// backpack cards, item details and action buttons (Equip / Unequip / Use).
/// Reads GameSession state only; all mutations go through InventoryService
/// followed by EmitChanged. Save stays disabled until stage 09.
/// </summary>
public partial class HUDController : CanvasLayer
{
    private enum Selection { None, Backpack, Weapon, Armour }

    private static readonly Color StatusOk = new("7fd6a8");
    private static readonly Color StatusFail = new("e08a8a");

    private TextureRect _portraitTexture = null!;
    private ColorRect _portraitFallback = null!;
    private Label _portraitKeyLabel = null!;
    private Label _nameLabel = null!;
    private Label _infoLabel = null!;
    private Label _hpLabel = null!;
    private ProgressBar _hpBar = null!;
    private Label _manaLabel = null!;
    private ProgressBar _manaBar = null!;
    private Label _levelLabel = null!;
    private Label _goldLabel = null!;
    private Label _backpackLabel = null!;
    private GridContainer _backpackGrid = null!;
    private ItemCardController _weaponCard = null!;
    private ItemCardController _armourCard = null!;
    private Label _detailsName = null!;
    private Label _detailsText = null!;
    private Label _statusLabel = null!;
    private Button _equipButton = null!;
    private Button _unequipButton = null!;
    private Button _useButton = null!;
    private Button _saveButton = null!;
    private Button _quitToTitleButton = null!;

    private PanelContainer _toastPanel = null!;
    private Label _toastLabel = null!;
    private Godot.Timer _toastTimer = null!;

    private Selection _selection = Selection.None;
    private int _selSlot = -1;
    private ItemStack? _selStack;
    private ItemDefinition? _selDef;
    private EquipmentSlotKind _selEquipKind;

    /// <summary>Assigned by the safehouse: stops movement and flushes position before a save snapshot.</summary>
    public Action? SettleBeforeSave;

    private static readonly PackedScene CardScene =
        GD.Load<PackedScene>("res://Scenes/ItemCard.tscn");

    public override void _Ready()
    {
        _portraitTexture = GetNode<TextureRect>("Root/Sidebar/SidebarMargin/SidebarVBox/PortraitPanel/PortraitArea/PortraitTexture");
        _portraitFallback = GetNode<ColorRect>("Root/Sidebar/SidebarMargin/SidebarVBox/PortraitPanel/PortraitArea/PortraitFallback");
        _portraitKeyLabel = GetNode<Label>("Root/Sidebar/SidebarMargin/SidebarVBox/PortraitPanel/PortraitArea/PortraitKeyLabel");
        _nameLabel = GetNode<Label>("%NameLabel");
        _infoLabel = GetNode<Label>("%InfoLabel");
        _hpLabel = GetNode<Label>("%HpLabel");
        _hpBar = GetNode<ProgressBar>("%HpBar");
        _manaLabel = GetNode<Label>("%ManaLabel");
        _manaBar = GetNode<ProgressBar>("%ManaBar");
        _levelLabel = GetNode<Label>("%LevelLabel");
        _goldLabel = GetNode<Label>("%GoldLabel");
        _backpackLabel = GetNode<Label>("%BackpackLabel");
        _backpackGrid = GetNode<GridContainer>("%BackpackGrid");
        _weaponCard = GetNode<ItemCardController>("Root/Sidebar/SidebarMargin/SidebarVBox/EquipRow/WeaponCard");
        _armourCard = GetNode<ItemCardController>("Root/Sidebar/SidebarMargin/SidebarVBox/EquipRow/ArmourCard");
        _detailsName = GetNode<Label>("%DetailsName");
        _detailsText = GetNode<Label>("%DetailsText");
        _statusLabel = GetNode<Label>("%StatusLabel");
        _equipButton = GetNode<Button>("%EquipButton");
        _unequipButton = GetNode<Button>("%UnequipButton");
        _useButton = GetNode<Button>("%UseButton");
        _saveButton = GetNode<Button>("%SaveButton");
        _quitToTitleButton = GetNode<Button>("%QuitToTitleButton");
        _toastPanel = GetNode<PanelContainer>("%ToastPanel");
        _toastLabel = GetNode<Label>("Root/ToastPanel/ToastLabel");
        _toastTimer = GetNode<Godot.Timer>("%ToastTimer");
        _toastTimer.Timeout += () => _toastPanel.Visible = false;

        _weaponCard.Selected += _ => OnEquipmentSelected(EquipmentSlotKind.Weapon);
        _armourCard.Selected += _ => OnEquipmentSelected(EquipmentSlotKind.Armour);
        _equipButton.Pressed += OnEquip;
        _unequipButton.Pressed += OnUnequip;
        _useButton.Pressed += OnUse;
        _saveButton.Pressed += OnSave;
        _quitToTitleButton.Pressed += OnQuitToTitle;

        if (GameSession.Instance != null)
        {
            GameSession.Instance.StateChanged += Refresh;
        }

        CompactLayout();
        Refresh();
    }

    private void CompactLayout()
    {
        var column=GetNode<VBoxContainer>("Root/Sidebar/SidebarMargin/SidebarVBox");
        var portrait=GetNode<PanelContainer>("Root/Sidebar/SidebarMargin/SidebarVBox/PortraitPanel");
        var header=new HBoxContainer();header.AddThemeConstantOverride("separation",12);
        column.AddChild(header);column.MoveChild(header,0);
        portrait.Reparent(header);portrait.CustomMinimumSize=new Vector2(128,166);portrait.SizeFlagsHorizontal=Control.SizeFlags.Fill;
        _portraitTexture.StretchMode=TextureRect.StretchModeEnum.KeepAspectCovered;
        var identity=new VBoxContainer {SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};header.AddChild(identity);
        _nameLabel.Reparent(identity);_infoLabel.Reparent(identity);
        _nameLabel.HorizontalAlignment=HorizontalAlignment.Left;_nameLabel.AddThemeFontSizeOverride("font_size",19);
        _infoLabel.HorizontalAlignment=HorizontalAlignment.Left;_infoLabel.AutowrapMode=TextServer.AutowrapMode.WordSmart;
        _infoLabel.CustomMinimumSize=new Vector2(0,90);_infoLabel.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;
        _portraitKeyLabel.Visible=false;
        var actionRow=_equipButton.GetParent<HBoxContainer>();
        _statusLabel.Visible=false;
        _statusLabel.Reparent(column);column.MoveChild(_statusLabel,column.GetChildren().IndexOf(actionRow)+1);
        foreach(var button in new[]{_equipButton,_unequipButton,_useButton})
        {button.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;button.CustomMinimumSize=new Vector2(0,36);}
        var footer=new HBoxContainer();column.AddChild(footer);
        _saveButton.Reparent(footer);_quitToTitleButton.Reparent(footer);
        foreach(var button in new[]{_saveButton,_quitToTitleButton})
        {button.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;button.CustomMinimumSize=new Vector2(0,36);}
        var backpackScroll=GetNode<ScrollContainer>("Root/Sidebar/SidebarMargin/SidebarVBox/BackpackScroll");
        backpackScroll.SizeFlagsVertical=Control.SizeFlags.Fill;
        backpackScroll.CustomMinimumSize=new Vector2(0,180);
        column.AddThemeConstantOverride("separation",6);
    }

    public override void _ExitTree()
    {
        if (GameSession.Instance != null)
        {
            GameSession.Instance.StateChanged -= Refresh;
        }
    }

    // ------------------------------------------------------------------
    // Rendering
    // ------------------------------------------------------------------

    public void Refresh()
    {
        var run = GameSession.Instance?.CurrentRun;
        var catalog = GameCatalog.Instance;

        if (run == null)
        {
            _nameLabel.Text = "-";
            _infoLabel.Text = "-";
            return;
        }

        _portraitTexture.StretchMode=run.CreationStage==0 ? TextureRect.StretchModeEnum.KeepAspectCentered : TextureRect.StretchModeEnum.KeepAspectCovered;
        var c = run.Character;
        catalog.TryGetArchetype(c.ArchetypeId, out var arch);
        _portraitTexture.Texture = arch?.LoadPortrait(c.GenderId);
        _portraitFallback.Color = new Color("1d2c38");
        _portraitKeyLabel.Text = arch?.PortraitKey(c.GenderId) ?? "-";
        _portraitKeyLabel.Visible = false;

        _nameLabel.Text = c.Name;
        _infoLabel.Text = arch == null
            ? c.ArchetypeId
            : $"{arch.DisplayName} • {arch.SpeciesDisplayName} • {c.GenderId}";

        var maxHp = CharacterRules.MaxHealth(c);
        var maxMana = CharacterRules.MaxMana(c);
        _hpLabel.Text = $"Health  {c.Hp} / {maxHp}";
        _hpBar.MaxValue = maxHp;
        _hpBar.Value = c.Hp;
        _manaLabel.Text = $"{StarterTraining.Resource(run)}  {c.Mana} / {maxMana}";
        _manaBar.MaxValue = maxMana;
        _manaBar.Value = c.Mana;
        _levelLabel.Text = Progression.Summary(c);
        _levelLabel.TooltipText="XP comes from completed objectives and victories. Each level grants +5 maximum HP and +2 maximum mana/focus, and restores both.";
        _goldLabel.Text = $"Gold: {run.CarriedGold}" + (run.HasGuildAccessDrive ? "\nGuild card · Access drive" : "");

        if(run.HasFilterSample)_goldLabel.Text+="\nSealed filter sample";
        if(run.Echo.Stage>0)_infoLabel.Text+="\n"+EchoMission.JournalLine(run);
        if (run.LearnedSpellIds.Length>0)
            _infoLabel.Text += "\nSkills:\n" + string.Join("\n",run.LearnedSpellIds.Select(id=>StarterTraining.Skills(run).FirstOrDefault(s=>s.Id==id)?.Name ?? id));
        var crystals = GameSession.Instance?.CarriedSaveCrystals() ?? 0;
        _saveButton.Text = crystals > 0 ? $"Save ({crystals} crystal{(crystals == 1 ? "" : "s")})" : "Save";
        _saveButton.Disabled = crystals <= 0;
        _saveButton.TooltipText = crystals > 0
            ? "Spend one carried save crystal to record a save"
            : "No save crystals carried — take them from the chest first";

        if (run.CreationStage == 0)
        {
            _portraitTexture.Texture = new AtlasTexture { Atlas = GD.Load<Texture2D>("res://Assets/Environment/cloaked-traveller-v1.png"), Region = new Rect2(650,10,470,877) };
            _portraitKeyLabel.Text = "An unfamiliar reflection";
            _nameLabel.Text = "Unknown traveller";
        }
        if (run.CreationStage < 2)
        {
            _infoLabel.Text = run.CreationStage == 0 ? "Look into the mirror beside the bed" : "Choose your training at the equipment rack";
            _hpLabel.Text = "Health — discover your strengths";
            _manaLabel.Text = StarterTraining.Resource(run)+" — training not yet confirmed";
        }
        _hpBar.Visible = run.CreationStage == 2;
        _manaBar.Visible = run.CreationStage == 2;
        RefreshEquipmentCards(run, catalog);
        RefreshBackpack(run, catalog);
        ClearSelection();
    }

    private void RefreshEquipmentCards(RunState run, GameCatalog catalog)
    {
        _weaponCard.Setup(-1, run.Equipment.Weapon, DefOf(run.Equipment.Weapon, catalog));
        _armourCard.Setup(-1, run.Equipment.Armour, DefOf(run.Equipment.Armour, catalog));
    }

    private void RefreshBackpack(RunState run, GameCatalog catalog)
    {
        foreach (var child in _backpackGrid.GetChildren())
        {
            child.QueueFree();
        }

        var used = 0;
        for (var slot = 0; slot < InventoryState.SlotCount; slot++)
        {
            var stack = run.Backpack[slot];
            if (stack != null) used++;
            var card = CardScene.Instantiate<ItemCardController>();
            card.Setup(slot, stack, DefOf(stack, catalog));
            card.Selected += s => OnBackpackSelected(s);
            _backpackGrid.AddChild(card);
        }
        _backpackLabel.Text = $"Backpack — {used}/{InventoryState.SlotCount}";
    }

    private static ItemDefinition? DefOf(ItemStack? stack, GameCatalog catalog)
    {
        if (stack == null) return null;
        catalog.TryGetItem(stack.ItemId, out var def);
        return def;
    }

    // ------------------------------------------------------------------
    // Selection + details + action buttons
    // ------------------------------------------------------------------

    private void OnBackpackSelected(int slot)
    {
        var run = GameSession.Instance?.CurrentRun;
        if (run == null) return;
        var stack = run.Backpack[slot];
        if (stack == null)
        {
            ClearSelection();
            return;
        }
        var catalog = GameCatalog.Instance;
        catalog.TryGetItem(stack.ItemId, out var def);
        if (def == null) return;

        _selection = Selection.Backpack;
        _selSlot = slot;
        _selStack = stack;
        _selDef = def;
        ShowDetails(def, stack, extra: EquipRestrictionNote(def, run));
        UpdateActionButtons();
    }

    private void OnEquipmentSelected(EquipmentSlotKind kind)
    {
        var run = GameSession.Instance?.CurrentRun;
        if (run == null) return;
        var stack = run.Equipment[kind];
        if (stack == null)
        {
            ClearSelection();
            return;
        }
        var catalog = GameCatalog.Instance;
        catalog.TryGetItem(stack.ItemId, out var def);
        if (def == null) return;

        _selection = kind == EquipmentSlotKind.Weapon ? Selection.Weapon : Selection.Armour;
        _selEquipKind = kind;
        _selSlot = -1;
        _selStack = stack;
        _selDef = def;
        ShowDetails(def, stack, extra: "Equipped — Unequip returns it to the backpack.");
        UpdateActionButtons();
    }

    private void ClearSelection()
    {
        _selection = Selection.None;
        _selSlot = -1;
        _selStack = null;
        _selDef = null;
        _detailsName.Text = "Select an item";
        _detailsText.Text = "Click a backpack or equipment card for details.";
        UpdateActionButtons();
    }

    private void ShowDetails(ItemDefinition def, ItemStack stack, string extra)
    {
        _detailsName.Text = $"{def.DisplayName}  x{stack.Quantity}";
        _detailsText.Text = $"{def.Description}\n{ItemCardController.EffectText(def)}\n{extra}".TrimEnd('\n');
    }

    private string EquipRestrictionNote(ItemDefinition def, RunState run)
    {
        if (def.Category is ItemCategory.Weapon or ItemCategory.Armour
            && !def.IsEquippableBy(run.Character.ArchetypeId))
        {
            var required = GameCatalog.Instance.TryGetArchetype(def.ArchetypeId, out var a) && a != null
                ? a.DisplayName
                : def.ArchetypeId;
            return $"Requires the {required} archetype.";
        }
        return "";
    }

    private void UpdateActionButtons()
    {
        var run = GameSession.Instance?.CurrentRun;
        _equipButton.Disabled = true;
        _unequipButton.Disabled = true;
        _useButton.Disabled = true;
        if (run == null || _selDef == null) return;

        switch (_selection)
        {
            case Selection.Backpack:
                _equipButton.Disabled = _selDef.Category is not (ItemCategory.Weapon or ItemCategory.Armour)
                    || !_selDef.IsEquippableBy(run.Character.ArchetypeId);
                _useButton.Disabled = _selDef.Category is not (ItemCategory.HealthPotion or ItemCategory.ManaPotion);
                break;
            case Selection.Weapon:
            case Selection.Armour:
                _unequipButton.Disabled = false;
                break;
        }
    }

    // ------------------------------------------------------------------
    // Actions
    // ------------------------------------------------------------------

    private void OnEquip()
    {
        var run = GameSession.Instance?.CurrentRun;
        if (run == null || _selection != Selection.Backpack) return;
        var result = InventoryService.EquipFromBackpack(run, _selSlot, GameCatalog.Instance);
        Report(result, $"Equipped {_selDef?.DisplayName}.");
    }

    private void OnUnequip()
    {
        var run = GameSession.Instance?.CurrentRun;
        if (run == null || _selection is not (Selection.Weapon or Selection.Armour)) return;
        var result = InventoryService.UnequipToBackpack(run, _selEquipKind);
        Report(result, $"Unequipped {_selDef?.DisplayName}.");
    }

    private void OnUse()
    {
        var run = GameSession.Instance?.CurrentRun;
        if (run == null || _selection != Selection.Backpack) return;
        var result = InventoryService.UseBackpackItem(run, _selSlot, GameCatalog.Instance);
        Report(result, $"Used {_selDef?.DisplayName}.");
    }

    private void Report(OperationResult result, string successText)
    {
        if (result.Success)
        {
            GameSession.Instance?.EmitChanged();
            SetStatus(successText, StatusOk);
        }
        else
        {
            SetStatus(result.Reason ?? "Action failed.", StatusFail);
        }
        Refresh();
    }

    private void SetStatus(string text, Color color)
    {
        _statusLabel.Visible=!string.IsNullOrWhiteSpace(text);
        _statusLabel.Text = text;
        _statusLabel.AddThemeColorOverride("font_color", color);
    }

    // ------------------------------------------------------------------
    // Saving + quitting (stage 09)
    // ------------------------------------------------------------------

    public void RequestSave()=>OnSave();

    private void OnSave()
    {
        var session = GameSession.Instance;
        var run = session?.CurrentRun;
        if (session == null || run == null) return;

        // Confirm overwriting a save that belongs to a different character.
        var overwriteName = ExistingSaveNameForOtherCharacter(run);
        if (overwriteName != null)
        {
            ConfirmDialogHelper.Show(this, "Overwrite save?",
                $"A save already exists for \"{overwriteName}\". Saving will replace it.",
                DoSave);
            return;
        }
        DoSave();
    }

    /// <summary>Name of the saved character when the slot holds a different run, else null.</summary>
    private static string? ExistingSaveNameForOtherCharacter(RunState run)
    {
        if (!Systems.SaveService.TryLoadSave(out var data, out _) || data == null)
        {
            return null; // missing or corrupt — saving will (re)create it
        }
        return data.RunId == run.Character.RunId ? null : data.Name;
    }

    private void DoSave()
    {
        var session = GameSession.Instance;
        if (session == null) return;

        SettleBeforeSave?.Invoke();
        var result = session.SaveGame();
        if (result.Success)
        {
            SetStatus("Game saved.", StatusOk);
        }
        else
        {
            SetStatus(result.Error ?? "Save failed.", StatusFail);
        }
        Refresh();
    }

    private void OnQuitToTitle()
    {
        var session = GameSession.Instance;
        if (session != null && session.HasUnsavedChanges)
        {
            ConfirmDialogHelper.Show(this, "Unsaved progress",
                "You have progress that hasn't been saved. Return to title anyway?",
                ReturnToTitle);
            return;
        }
        ReturnToTitle();
    }

    private void ReturnToTitle()
    {
        GameSession.Instance?.ClearRun();
        if (GetTree().CurrentScene is MainController main)
        {
            main.ShowTitle();
        }
    }

    // ------------------------------------------------------------------
    // Toasts (world-space messages; sidebar has its own status line)
    // ------------------------------------------------------------------

    public void ShowToast(string text)
    {
        _toastLabel.Text = text;
        _toastPanel.Visible = true;
        _toastTimer.Start();
    }
}
