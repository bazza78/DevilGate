using DevilGate.Scripts.Data;
using DevilGate.Scripts.Definitions;
using DevilGate.Scripts.Systems;
using Godot;

namespace DevilGate.Scripts;

/// <summary>
/// Character creation: name, five archetypes, gender presentation, portrait
/// preview and the 12-point attribute allocation with refunds.
/// </summary>
public partial class CharacterCreationController : Control, ICloseable
{
    public string RoomMode { get; set; } = "";
    public event Action? CloseRequested;
    private GameCatalog _catalog = null!;
    private string _archetypeId = Ids.ArchetypeMage;
    private string _genderId = Ids.GenderFemale;
    private AttributeSet _attributes = new();

    private LineEdit _nameEdit = null!;
    private Label _nameError = null!;
    private Label _speciesLabel = null!;
    private Label _pointsLabel = null!;
    private Label _derivedLabel = null!;
    private Button _beginButton = null!;
    private TextureRect _portraitTexture = null!;
    private ColorRect _portraitFallback = null!;
    private Label _portraitKeyLabel = null!;
    private Label _previewName = null!;
    private Label _previewArchetype = null!;
    private Label _previewSpecies = null!;
    private Label _previewGender = null!;
    private HBoxContainer _archetypeRow = null!;
    private HBoxContainer _genderRow = null!;
    private GridContainer _attrGrid = null!;

    private readonly Dictionary<AttributeKind, Label> _valueLabels = new();
    private readonly Dictionary<AttributeKind, Button> _plusButtons = new();
    private readonly Dictionary<AttributeKind, Button> _minusButtons = new();
    private readonly Dictionary<Button, string> _archetypeButtons = new();
    private readonly Dictionary<Button, string> _genderButtons = new();
    private readonly ButtonGroup _archetypeGroup = new();
    private readonly ButtonGroup _genderGroup = new();

    public override void _Ready()
    {
        _catalog = GameCatalog.Instance;

        _nameEdit = GetNode<LineEdit>("%NameEdit");
        _nameError = GetNode<Label>("%NameError");
        _speciesLabel = GetNode<Label>("%SpeciesLabel");
        _pointsLabel = GetNode<Label>("%PointsLabel");
        _derivedLabel = GetNode<Label>("%DerivedLabel");
        _beginButton = GetNode<Button>("%BeginButton");
        _portraitTexture = GetNode<TextureRect>("%PortraitTexture");
        _portraitFallback = GetNode<ColorRect>("Margin/HBox/RightVBox/PortraitPanel/PortraitBox/PortraitFallback");
        _portraitKeyLabel = GetNode<Label>("%PortraitKeyLabel");
        GetNode<PanelContainer>("Margin/HBox/RightVBox/PortraitPanel").AddThemeStyleboxOverride("panel",new StyleBoxEmpty());
        _portraitTexture.ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize;
        _portraitTexture.StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered;
        _previewName = GetNode<Label>("%PreviewName");
        _previewArchetype = GetNode<Label>("%PreviewArchetype");
        _previewSpecies = GetNode<Label>("%PreviewSpecies");
        _previewGender = GetNode<Label>("%PreviewGender");
        _archetypeRow = GetNode<HBoxContainer>("Margin/HBox/Scroll/LeftVBox/ArchetypeRow");
        _genderRow = GetNode<HBoxContainer>("Margin/HBox/Scroll/LeftVBox/GenderRow");
        _attrGrid = GetNode<GridContainer>("Margin/HBox/Scroll/LeftVBox/AttrGrid");

        if (RoomMode != "" && GameSession.Instance?.CurrentRun is { } run)
        {
            _archetypeId = run.Character.ArchetypeId;
            _genderId = run.Character.GenderId;
            _nameEdit.Text = RoomMode == "mirror" && run.CreationStage == 0 ? "" : run.Character.Name;
        }
        if (TestingRoster.Enabled && RoomMode != "rack" && !TestingRoster.AllowsArchetype(_archetypeId))
        {
            _archetypeId = Ids.ArchetypeMage;
            _genderId = Ids.GenderFemale;
        }
        if (RoomMode != "rack" && !TestingRoster.AllowsGender(_genderId,_archetypeId)) _genderId = Ids.GenderFemale;
        BuildChoiceButtons();
        BuildAttributeRows();

        _nameEdit.TextChanged += OnNameChanged;
        GetNode<Button>("Margin/HBox/Scroll/LeftVBox/ButtonsRow/SuggestedButton").Pressed += OnSuggestedPressed;
        _beginButton.Pressed += OnBeginPressed;
        GetNode<Button>("Margin/HBox/Scroll/LeftVBox/ButtonsRow/BackButton").Pressed += OnBackPressed;

        _attributes = _catalog.GetArchetype(_archetypeId).CreateSuggestedAttributes();
        RefreshAll();
        if (RoomMode != "")
        {
            CustomMinimumSize = new Vector2(1100,680);
            var background = new ColorRect { Color = new Color("17232f"), MouseFilter = MouseFilterEnum.Ignore };
            background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(background); MoveChild(background,0);
            _portraitTexture.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            GetNode<VBoxContainer>("Margin/HBox/RightVBox").CustomMinimumSize = new Vector2(260,0);
            GetNode<PanelContainer>("Margin/HBox/RightVBox/PortraitPanel").CustomMinimumSize = new Vector2(0,320);
            GetNode<PanelContainer>("Margin/HBox/RightVBox/PortraitPanel").SizeFlagsVertical = SizeFlags.Fill;
            _nameError.Visible = false;
            var left = GetNode<VBoxContainer>("Margin/HBox/Scroll/LeftVBox");
            left.GetNode<Label>("TitleLabel").Text = RoomMode == "mirror" ? "Your reflection" : "Remember your training";
            _beginButton.Text = RoomMode == "mirror" ? "Accept identity" : "Confirm training & collect kit";
            left.GetNode<Button>("ButtonsRow/BackButton").Text = "Close";
            foreach (var key in new[] {"AttrHeader","PointsLabel","AttrGrid","DerivedLabel","ButtonsRow/SuggestedButton"})
                left.GetNode<Control>(key).Visible = RoomMode == "rack";
            foreach (var key in new[] {"NameLabel","NameEdit","NameError","ArchetypeLabel","ArchetypeRow","SpeciesLabel","GenderLabel","GenderRow"})
                left.GetNode<Control>(key).Visible = RoomMode == "mirror";
            _nameError.Visible = false;
            GetNode<Label>("Margin/HBox/RightVBox/PreviewHint").Text = RoomMode == "mirror" ? "A stranger looks back. Who are you?" : "Choose your strengths. Your starter kit is waiting.";
        }
    }

    public override void _ExitTree()
    {
        _archetypeGroup.Dispose();
        _genderGroup.Dispose();
    }

    // ------------------------------------------------------------------
    // Construction
    // ------------------------------------------------------------------

    private void BuildChoiceButtons()
    {
        foreach (var archetype in _catalog.AllArchetypes)
        {
            var button = new Button
            {
                Text = archetype.DisplayName,
                ToggleMode = true,
                ButtonGroup = _archetypeGroup,
                CustomMinimumSize = new Vector2(96, 40),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            var id = archetype.Id;
            button.Disabled = !TestingRoster.AllowsArchetype(id);
            if (button.Disabled) button.TooltipText = "Disabled for this test build. Mage, Tank, Archer, Healer and female Knight are available.";
            button.Pressed += () => OnArchetypePressed(id);
            _archetypeButtons[button] = id;
            _archetypeRow.AddChild(button);
            if (id == _archetypeId)
            {
                button.ButtonPressed = true;
            }
        }

        foreach (var (id, label) in new[] { (Ids.GenderMale, "Male"), (Ids.GenderFemale, "Female") })
        {
            var button = new Button
            {
                Text = label,
                ToggleMode = true,
                ButtonGroup = _genderGroup,
                CustomMinimumSize = new Vector2(110, 40),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            button.Disabled = !TestingRoster.AllowsGender(id,_archetypeId);
            if (button.Disabled) button.TooltipText = TestingRoster.GenderUnavailableReason(id,_archetypeId);
            button.Pressed += () => OnGenderPressed(id);
            _genderButtons[button] = id;
            _genderRow.AddChild(button);
            if (id == _genderId)
            {
                button.ButtonPressed = true;
            }
        }
    }

    private void BuildAttributeRows()
    {
        foreach (var kind in AttributeSet.All)
        {
            var nameLabel = new Label
            {
                Text = CharacterRules.DisplayName(kind),
                CustomMinimumSize = new Vector2(90, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            var minus = new Button { Text = "-", CustomMinimumSize = new Vector2(38, 34) };
            var value = new Label
            {
                Text = "1",
                CustomMinimumSize = new Vector2(36, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var plus = new Button { Text = "+", CustomMinimumSize = new Vector2(38, 34) };

            var k = kind;
            minus.Pressed += () => OnAttributeDelta(k, -1);
            plus.Pressed += () => OnAttributeDelta(k, +1);

            _minusButtons[kind] = minus;
            _plusButtons[kind] = plus;
            _valueLabels[kind] = value;

            _attrGrid.AddChild(nameLabel);
            _attrGrid.AddChild(minus);
            _attrGrid.AddChild(value);
            _attrGrid.AddChild(plus);
        }
    }

    // ------------------------------------------------------------------
    // Handlers
    // ------------------------------------------------------------------

    private void OnNameChanged(string text)
    {
        var invalid = text.Length > 0 && !CharacterRules.IsValidName(text);
        _nameError.Visible = invalid;
        RefreshBegin();
        RefreshPreview();
    }

    private void OnArchetypePressed(string id)
    {
        if (!TestingRoster.AllowsArchetype(id)) return;
        // Player-edited allocations are kept; only "Use Suggested Stats"
        // replaces them with the newly selected archetype's suggestion.
        _archetypeId = id;
        if(!TestingRoster.AllowsGender(_genderId,id)) _genderId=Ids.GenderFemale;
        foreach(var (button,gender) in _genderButtons)
        {
            button.Disabled=!TestingRoster.AllowsGender(gender,id);
            button.TooltipText=button.Disabled ? TestingRoster.GenderUnavailableReason(gender,id) : "";
            button.ButtonPressed=gender==_genderId;
        }
        RefreshAll();
    }

    private void OnGenderPressed(string id)
    {
        if (!TestingRoster.AllowsGender(id,_archetypeId)) return;
        _genderId = id;
        RefreshAll();
    }

    private void OnAttributeDelta(AttributeKind kind, int delta)
    {
        if (delta > 0 && CharacterRules.CanIncrement(_attributes, kind))
        {
            _attributes[kind]++;
        }
        else if (delta < 0 && CharacterRules.CanDecrement(_attributes, kind))
        {
            _attributes[kind]--;
        }
        RefreshAttributes();
        RefreshBegin();
    }

    private void OnSuggestedPressed()
    {
        _attributes = _catalog.GetArchetype(_archetypeId).CreateSuggestedAttributes();
        RefreshAttributes();
        RefreshBegin();
    }

    private void OnBeginPressed()
    {
        if (!CharacterRules.IsValidName(_nameEdit.Text) ||
            (RoomMode != "mirror" && !CharacterRules.IsAllocationComplete(_attributes))) return;
        var result = new CharacterCreationResult
        {
            Name = _nameEdit.Text.Trim(),
            ArchetypeId = _archetypeId,
            GenderId = _genderId,
            Attributes = _attributes.Clone(),
        };
        if (RoomMode != "")
        {
            var session = GameSession.Instance!;
            var run = session.CurrentRun!;
            if (RoomMode == "mirror" && run.CreationStage == 0)
            {
                run.Character.Name = result.Name;
                run.Character.ArchetypeId = result.ArchetypeId;
                run.Character.GenderId = result.GenderId;
                run.Character.Attributes = _catalog.GetArchetype(result.ArchetypeId).CreateSuggestedAttributes();
                run.CreationStage = 1;
            }
            else if (RoomMode == "rack" && run.CreationStage == 1)
            {
                var free = Enumerable.Range(0,InventoryState.SlotCount).Where(i => run.Backpack[i] == null).Take(2).ToArray();
                if (free.Length < 2) { _pointsLabel.Text = "Make two free backpack slots before collecting your kit."; return; }
                run.Backpack[free[0]] = new ItemStack(_catalog.GetArchetype(run.Character.ArchetypeId).WeaponItemId,1);
                run.Backpack[free[1]] = new ItemStack(Ids.ItemBasicJacket,1);
                run.Character.Attributes = result.Attributes;
                run.CreationStage = 2;
            }
            else return;
            run.Character.Hp = CharacterRules.MaxHealth(run.Character);
            run.Character.Mana = CharacterRules.MaxMana(run.Character);
            session.EmitChanged();
            CloseRequested?.Invoke();
            return;
        }
        GameSession.Instance?.StartNewRun(result);
        GetParent<MainController>()?.ShowSafehouse();
    }

    private void OnBackPressed()
    {
        if (RoomMode != "") { CloseRequested?.Invoke(); return; }
        GetParent<MainController>()?.ShowTitle();
    }

    // ------------------------------------------------------------------
    // Refresh
    // ------------------------------------------------------------------

    private void RefreshAll()
    {
        RefreshPortrait();
        RefreshSpeciesAndPreview();
        RefreshAttributes();
        RefreshBegin();
    }

    private ArchetypeDefinition CurrentArchetype => _catalog.GetArchetype(_archetypeId);

    private void RefreshPortrait()
    {
        var archetype = CurrentArchetype;
        _portraitTexture.Texture = archetype.LoadPortrait(_genderId);
        _portraitFallback.Color = archetype.ThemeColor.Darkened(0.55f);
        _portraitFallback.Visible=_portraitTexture.Texture==null || _portraitTexture.Texture.ResourcePath.Contains("/Placeholders/");
        _portraitKeyLabel.Text = archetype.PortraitKey(_genderId);
        _portraitKeyLabel.Visible = _portraitTexture.Texture?.ResourcePath.Contains("/Placeholders/") == true;
    }

    private void RefreshSpeciesAndPreview()
    {
        var archetype = CurrentArchetype;
        _speciesLabel.Text = $"Species: {archetype.SpeciesDisplayName}";
        _previewArchetype.Text = $"{archetype.DisplayName} — {archetype.SpeciesDisplayName}";
        _previewSpecies.Text = archetype.SpeciesDisplayName;
        _previewGender.Text = _genderId == Ids.GenderMale ? "Male" : "Female";
        RefreshPreview();
    }

    private void RefreshPreview()
    {
        var name = _nameEdit.Text.Trim();
        _previewName.Text = name.Length > 0 ? name : "-";
    }

    private void RefreshAttributes()
    {
        foreach (var kind in AttributeSet.All)
        {
            _valueLabels[kind].Text = _attributes[kind].ToString();
            _plusButtons[kind].Disabled = !CharacterRules.CanIncrement(_attributes, kind);
            _minusButtons[kind].Disabled = !CharacterRules.CanDecrement(_attributes, kind);
        }
        var spent = CharacterRules.BonusSpent(_attributes);
        _pointsLabel.Text = $"Points remaining: {CharacterRules.BonusPoints - spent}";
        _derivedLabel.Text =
            $"Health: {CharacterRules.MaxHealth(_attributes)}   {(_archetypeId is Ids.ArchetypeTank or Ids.ArchetypeArcher or Ids.ArchetypeKnight?"Focus":"Mana")}: {CharacterRules.MaxMana(_attributes)}";
    }

    private void RefreshBegin()
    {
        _beginButton.Disabled = !(CharacterRules.IsValidName(_nameEdit.Text)
            && (RoomMode == "mirror" || CharacterRules.IsAllocationComplete(_attributes)));
    }
}
