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
    private ScrollContainer _backpackScroll = null!;
    private GridContainer _backpackGrid = null!;
    private readonly List<ItemCardController> _backpackCards = new();
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
    private int _savedScrollVertical;
    public event Action? JournalRequested;
    public event Action? SettingsRequested;
    private string? _previousObjective, _previousRun;
    private int _previousInduction, _previousEcho;


    public int SelectedBackpackSlot => _selection == Selection.Backpack ? _selSlot : -1;
    public EquipmentSlotKind? SelectedEquipmentSlot => _selection is Selection.Weapon or Selection.Armour ? _selEquipKind : null;
    public int BackpackScrollVertical
    {
        get => _backpackScroll.ScrollVertical;
        set
        {
            _savedScrollVertical = Math.Max(0, value);
            _backpackScroll.ScrollVertical = _savedScrollVertical;
        }
    }
    public IReadOnlyList<ItemCardController> BackpackCards => _backpackCards;
    public ItemCardController WeaponCard => _weaponCard;
    public ItemCardController ArmourCard => _armourCard;

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
        _backpackScroll = GetNode<ScrollContainer>("%BackpackScroll");
        _backpackGrid = GetNode<GridContainer>("%BackpackGrid");
        _weaponCard = GetNode<ItemCardController>("Root/Sidebar/SidebarMargin/SidebarVBox/EquipRow/WeaponCard");
        _armourCard = GetNode<ItemCardController>("Root/Sidebar/SidebarMargin/SidebarVBox/EquipRow/ArmourCard");
        _detailsName = GetNode<Label>("%DetailsName");
        _detailsText = GetNode<Label>("%DetailsText");
        _detailsName.ClipText = false;
        _detailsName.AutowrapMode = TextServer.AutowrapMode.WordSmart;
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
        portrait.Reparent(header);portrait.CustomMinimumSize=new Vector2(112,144);portrait.SizeFlagsHorizontal=Control.SizeFlags.Fill;
        _portraitTexture.StretchMode=TextureRect.StretchModeEnum.KeepAspectCovered;
        var identity=new VBoxContainer {SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};header.AddChild(identity);
        _nameLabel.Reparent(identity);_infoLabel.Reparent(identity);
        _nameLabel.HorizontalAlignment=HorizontalAlignment.Left;_nameLabel.AddThemeFontSizeOverride("font_size",19);
        _infoLabel.HorizontalAlignment=HorizontalAlignment.Left;_infoLabel.AutowrapMode=TextServer.AutowrapMode.WordSmart;
        _infoLabel.ClipText=false;_infoLabel.MouseFilter=Control.MouseFilterEnum.Pass;
        _infoLabel.CustomMinimumSize=new Vector2(0,74);_infoLabel.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;
        _portraitKeyLabel.Visible=false;
        var actionRow=_equipButton.GetParent<HBoxContainer>();
        _statusLabel.Visible=false;
        _statusLabel.Reparent(column);column.MoveChild(_statusLabel,column.GetChildren().IndexOf(actionRow)+1);
        foreach(var button in new[]{_equipButton,_unequipButton,_useButton})
        {button.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;button.CustomMinimumSize=new Vector2(0,36);}
        var shortcuts = new HBoxContainer(); column.AddChild(shortcuts);
        foreach (var text in new[] { "Journal (J)", "Settings (Esc)" })
        {
            var b = new Button { Text = text, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            shortcuts.AddChild(b);
            b.Pressed += () => { if (text.StartsWith("Journal")) JournalRequested?.Invoke(); else SettingsRequested?.Invoke(); };
        }
        var footer=new HBoxContainer();column.AddChild(footer);
        _saveButton.Reparent(footer);_quitToTitleButton.Reparent(footer);
        foreach(var button in new[]{_saveButton,_quitToTitleButton})
        {button.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;button.CustomMinimumSize=new Vector2(0,36);}
        var backpackScroll=GetNode<ScrollContainer>("Root/Sidebar/SidebarMargin/SidebarVBox/BackpackScroll");
        backpackScroll.SizeFlagsVertical=Control.SizeFlags.Fill;
        backpackScroll.CustomMinimumSize=new Vector2(0,160);
        column.AddThemeConstantOverride("separation",5);
        var parent = column.GetParent();
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        parent.AddChild(scroll); column.Reparent(scroll); column.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
    }

    public override void _Process(double delta)
    {
        if (_saveButton == null) return;
        var session = GameSession.Instance;
        int crystals = session?.CarriedSaveCrystals() ?? 0;
        _saveButton.Text = $"Save{(session?.HasUnsavedChanges == true ? " *" : "")} ({crystals})";
        _saveButton.TooltipText = (session?.HasUnsavedChanges == true ? "Unsaved progress. " : "Progress saved. ") +
            (crystals > 0 ? "Spend one carried crystal to save." : "Take save crystals from the chest first.");
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
        var objective = OpeningGuidance.Next(run).Text;
        if (_previousRun == run.Character.RunId && _previousObjective != null)
        {
            var notices = new List<string>();
            if (_previousInduction < 2 && run.InductionStage >= 2) notices.Add("Guild card and access drive received.");
            if (_previousInduction < 3 && run.InductionStage == 3) notices.Add("Induction reward collected.");
            if (_previousEcho < 4 && run.Echo.Stage >= 4) notices.Add("Relay restored · Filter sample sealed.");
            if (_previousEcho < 5 && run.Echo.Stage == 5) notices.Add("Echo reward collected · 20 gold, 20 XP · Archive unlocked.");
            if (objective != _previousObjective) notices.Add(objective.Split('\n')[0]);
            if (notices.Count > 0) ShowToast(string.Join("\n", notices));
        }
        _previousRun = run.Character.RunId; _previousObjective = objective;
        _previousInduction = run.InductionStage; _previousEcho = run.Echo.Stage;
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
        {
            var learned=run.LearnedSpellIds.Select(id=>StarterTraining.Skills(run).FirstOrDefault(s=>s.Id==id)).Where(s=>s!=null).ToArray();
            _infoLabel.Text += "\nSkills: " + string.Join(", ",learned.Select(s=>s!.Name));
            _infoLabel.TooltipText = string.Join("\n\n",learned.Select(s=>$"{s!.Name} ({s.School})\n{s.Description}"));
        }
        else _infoLabel.TooltipText = "";
        var crystals = GameSession.Instance?.CarriedSaveCrystals() ?? 0;
        _saveButton.Text = crystals > 0 ? $"Save ({crystals} crystal{(crystals == 1 ? "" : "s")})" : "Save";
        _saveButton.Disabled = crystals <= 0;
        _saveButton.TooltipText = crystals > 0
            ? $"{(GameSession.Instance?.HasUnsavedChanges == true ? "Unsaved progress. " : "Progress saved. ")}Spend one carried save crystal to record a save"
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
        RestoreSelection(run, catalog);
    }

    private void RefreshEquipmentCards(RunState run, GameCatalog catalog)
    {
        _weaponCard.Setup(-1, run.Equipment.Weapon, DefOf(run.Equipment.Weapon, catalog), equipped: run.Equipment.Weapon != null, emptyLabel: "Weapon · Empty");
        _armourCard.Setup(-1, run.Equipment.Armour, DefOf(run.Equipment.Armour, catalog), equipped: run.Equipment.Armour != null, emptyLabel: "Armour · Empty");
    }

    private void RefreshBackpack(RunState run, GameCatalog catalog)
    {
        var scrollY = BackpackScrollVertical;
        if (_backpackCards.Count != InventoryState.SlotCount)
        {
            foreach (var child in _backpackGrid.GetChildren())
            {
                _backpackGrid.RemoveChild(child);
                child.QueueFree();
            }
            _backpackCards.Clear();
            for (var slot = 0; slot < InventoryState.SlotCount; slot++)
            {
                var card = CardScene.Instantiate<ItemCardController>();
                card.Selected += s => OnBackpackSelected(s);
                card.Activated += s => { OnBackpackSelected(s); if (!_equipButton.Disabled) OnEquip(); else if (!_useButton.Disabled) OnUse(); };
                _backpackGrid.AddChild(card);
                _backpackCards.Add(card);
            }
        }

        var used = 0;
        for (var slot = 0; slot < InventoryState.SlotCount; slot++)
        {
            var stack = run.Backpack[slot];
            if (stack != null) used++;
            _backpackCards[slot].Setup(slot, stack, DefOf(stack, catalog));
        }
        _backpackLabel.Text = $"Backpack — {used}/{InventoryState.SlotCount}";
        BackpackScrollVertical = scrollY;
    }

    private void RestoreSelection(RunState run, GameCatalog catalog)
    {
        if (_selection == Selection.Backpack && _selSlot >= 0 && _selSlot < InventoryState.SlotCount && run.Backpack[_selSlot] != null)
        {
            OnBackpackSelected(_selSlot);
            return;
        }
        if (_selection is Selection.Weapon or Selection.Armour && run.Equipment[_selEquipKind] != null)
        {
            OnEquipmentSelected(_selEquipKind);
            return;
        }
        ClearSelection();
    }

    private void UpdateCardHighlights()
    {
        _weaponCard.SetSelected(_selection == Selection.Weapon);
        _armourCard.SetSelected(_selection == Selection.Armour);
        for (var i = 0; i < _backpackCards.Count; i++)
        {
            _backpackCards[i].SetSelected(_selection == Selection.Backpack && _selSlot == i);
        }
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
        UpdateCardHighlights();
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
        UpdateCardHighlights();
        ShowDetails(def, stack, extra: "Equipped — Unequip returns it to the backpack.");
        UpdateActionButtons();
    }

    private void ClearSelection()
    {
        _selection = Selection.None;
        _selSlot = -1;
        _selStack = null;
        _selDef = null;
        UpdateCardHighlights();
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
        if (def.Category == ItemCategory.Weapon)
            return $"Basic strike: {WoodlandBattle.BasicDamage(run)} → {WoodlandBattle.BasicStrikeDamage} damage (free; {CharacterRules.DisplayName(StarterTraining.BasicAttribute(run))} roll).";
        if (def.Category == ItemCategory.Armour)
            return $"Defence: {WoodlandBattle.BaseDefence(run)} → 11 before skills. Reduces hit chance; absorbs 0 damage.";
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
        if (run == null || _selection != Selection.Backpack || _selDef == null) return;
        var slot = _selSlot;
        var targetKind = _selDef.Category == ItemCategory.Weapon ? EquipmentSlotKind.Weapon : EquipmentSlotKind.Armour;
        var itemName = _selDef.DisplayName;
        var result = InventoryService.EquipFromBackpack(run, slot, GameCatalog.Instance);
        if (result.Success && run.Backpack[slot] == null)
        {
            _selection = targetKind == EquipmentSlotKind.Weapon ? Selection.Weapon : Selection.Armour;
            _selEquipKind = targetKind;
            _selSlot = -1;
        }
        if (result.Success)
        {
            GameAudio.Instance?.Cue("equip");
            if (!ComfortSettings.ReducedFlash)
            {
                var card = targetKind == EquipmentSlotKind.Weapon ? _weaponCard : _armourCard;
                card.Modulate = new Color("a6e6d3");
                CreateTween().TweenProperty(card, "modulate", Colors.White, .25);
            }
        }
        Report(result, $"Equipped {itemName}.");
    }

    private void OnUnequip()
    {
        var run = GameSession.Instance?.CurrentRun;
        if (run == null || _selection is not (Selection.Weapon or Selection.Armour) || _selDef == null) return;
        var targetSlot = run.Backpack.FirstEmptySlot();
        var itemName = _selDef.DisplayName;
        var result = InventoryService.UnequipToBackpack(run, _selEquipKind);
        if (result.Success && targetSlot >= 0 && run.Backpack[targetSlot] != null)
        {
            _selection = Selection.Backpack;
            _selSlot = targetSlot;
        }
        Report(result, $"Unequipped {itemName}.");
    }

    private void OnUse()
    {
        var run = GameSession.Instance?.CurrentRun;
        if (run == null || _selection != Selection.Backpack || _selDef == null) return;
        var itemName = _selDef.DisplayName;
        var result = InventoryService.UseBackpackItem(run, _selSlot, GameCatalog.Instance);
        if (result.Success) GameAudio.Instance?.Cue("potion");
        Report(result, $"Used {itemName}.");
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
            var time = DateTimeOffset.TryParse(session.LastSaveTime, out var saved) ? saved.ToLocalTime().ToString("HH:mm") : "now";
            SetStatus($"Saved at {time} · 1 crystal spent.", StatusOk);
            ShowToast($"Game saved at {time} · 1 crystal spent.");
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
