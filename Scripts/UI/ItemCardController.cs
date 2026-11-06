using DevilGate.Scripts.Data;
using DevilGate.Scripts.Definitions;
using Godot;

namespace DevilGate.Scripts;

/// <summary>
/// One inventory slot card: placeholder icon, name, quantity/type and a short
/// effect line. Clicking selects it (parent decides what selection means).
/// Shared by the chest panel and the stage-07 sidebar.
/// </summary>
public partial class ItemCardController : PanelContainer
{
    private static readonly Color SelectedBorder = new("46d6c8");
    private static readonly Color EquippedBorder = new("3aa38f");

    public int SlotIndex { get; private set; } = -1;
    public ItemStack? Stack { get; private set; }
    public bool IsEquipped { get; private set; }

    /// <summary>Fired with the slot index when the card is clicked.</summary>
    public event Action<int>? Selected;
    public event Action<int>? Activated;
    public event Action<int>? StackMoveRequested;

    private StyleBoxFlat? _selectedStyle;
    private StyleBoxFlat? _equippedStyle;
    private StyleBoxFlat? _emptyStyle;
    private bool _selected;
    private bool _isEmpty = true;

    private TextureRect Icon => GetNode<TextureRect>("Content/Icon");
    private Label NameLabel => GetNode<Label>("Content/Info/NameLabel");
    private Label InfoLabel => GetNode<Label>("Content/Info/InfoLabel");
    private Label EffectLabel => GetNode<Label>("Content/Info/EffectLabel");
    private Control Content => GetNode<Control>("Content");
    private Control EmptyState => GetNode<Control>("EmptyState");
    private Label EmptyLabel => GetNode<Label>("EmptyState/EmptyLabel");

    public override void _Ready()
    {
        EnsureStyles();
        Content.MouseFilter = MouseFilterEnum.Ignore;
        GetNode<Control>("Content/Info").MouseFilter = MouseFilterEnum.Ignore;
        NameLabel.MouseFilter = MouseFilterEnum.Ignore;
        NameLabel.AddThemeFontSizeOverride("font_size", 11);
        NameLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        InfoLabel.MouseFilter = MouseFilterEnum.Ignore;
        EffectLabel.MouseFilter = MouseFilterEnum.Ignore;
        EmptyState.MouseFilter = MouseFilterEnum.Ignore;
        EmptyLabel.MouseFilter = MouseFilterEnum.Ignore;
        ApplyVisualStyle();
    }

    private void EnsureStyles()
    {
        _selectedStyle ??= new StyleBoxFlat
        {
            BgColor = new Color("16263a"),
            BorderColor = SelectedBorder,
            BorderWidthLeft = 2, BorderWidthTop = 2,
            BorderWidthRight = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
            CornerRadiusBottomRight = 6, CornerRadiusBottomLeft = 6,
            ContentMarginLeft = 8f, ContentMarginRight = 8f,
            ContentMarginTop = 6f, ContentMarginBottom = 6f,
        };
        _equippedStyle ??= new StyleBoxFlat
        {
            BgColor = new Color("12222d"),
            BorderColor = EquippedBorder,
            BorderWidthLeft = 3, BorderWidthTop = 1,
            BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
            CornerRadiusBottomRight = 6, CornerRadiusBottomLeft = 6,
            ContentMarginLeft = 8f, ContentMarginRight = 8f,
            ContentMarginTop = 6f, ContentMarginBottom = 6f,
        };
        _emptyStyle ??= new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.08f, 0.12f, 0.35f),
            BorderColor = new Color(0.16f, 0.20f, 0.28f, 0.42f),
            BorderWidthLeft = 1, BorderWidthTop = 1,
            BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
            CornerRadiusBottomRight = 6, CornerRadiusBottomLeft = 6,
            ContentMarginLeft = 8f, ContentMarginRight = 8f,
            ContentMarginTop = 6f, ContentMarginBottom = 6f,
        };
    }

    /// <summary>slot = inventory slot index; stack/def null renders an empty slot.</summary>
    public void Setup(int slot, ItemStack? stack, ItemDefinition? def, bool equipped = false, string? emptyLabel = null)
    {
        EnsureStyles();
        SlotIndex = slot;
        Stack = stack;
        IsEquipped = equipped;

        var empty = stack == null || def == null;
        _isEmpty = empty;
        Content.Visible = !empty;
        EmptyState.Visible = empty;
        if (empty)
        {
            _selected = false;
            EmptyLabel.Text = emptyLabel ?? "Empty slot";
            TooltipText = emptyLabel != null
                ? $"{emptyLabel} — select a matching item in your backpack and press Equip."
                : "";
            Modulate = new Color(1, 1, 1, 0.42f);
            ApplyVisualStyle();
            return;
        }
        Modulate = Colors.White;

        Icon.Texture = def!.LoadIcon();
        Icon.Visible = Icon.Texture != null;
        NameLabel.Text = CardTitle(def);
        InfoLabel.Text = equipped
            ? $"Equipped • {CategoryName(def.Category)}"
            : $"x{stack!.Quantity} • {CategoryName(def.Category)}";
        InfoLabel.AddThemeColorOverride("font_color", equipped ? new Color("8be8cb") : new Color(0.62f, 0.68f, 0.82f));
        EffectLabel.Text = EffectText(def);
        TooltipText = $"{def.DisplayName} (x{stack!.Quantity}){(equipped ? " • Equipped" : "")}\n{CategoryName(def.Category)} • {EffectText(def)}\n{def.Description}";
        ApplyVisualStyle();
    }

    /// <summary>Clean short label for narrow slot cards; full DisplayName is shown in hover tooltips and details panels.</summary>
    public static string CardTitle(ItemDefinition def) => def.Id switch
    {
        Ids.ItemApprenticeStaff => "Focus Staff",
        Ids.ItemBasicJacket => "Basic Jacket",
        Ids.ItemTrainingBow => "Composite Bow",
        Ids.ItemMedicWand => "Medic's Wand",
        _ => def.DisplayName,
    };

    public void SetSelected(bool selected)
    {
        EnsureStyles();
        _selected = selected && !_isEmpty;
        ApplyVisualStyle();
    }

    private void ApplyVisualStyle()
    {
        if (_isEmpty)
        {
            if (_emptyStyle != null) AddThemeStyleboxOverride("panel", _emptyStyle);
            return;
        }
        if (_selected && _selectedStyle != null)
        {
            AddThemeStyleboxOverride("panel", _selectedStyle);
        }
        else if (IsEquipped && _equippedStyle != null)
        {
            AddThemeStyleboxOverride("panel", _equippedStyle);
        }
        else
        {
            RemoveThemeStyleboxOverride("panel");
        }
    }

    public bool IsSelected => _selected;

    public void Select() => Selected?.Invoke(SlotIndex);

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mouse)
        {
            Select();
            if (mouse.ShiftPressed) StackMoveRequested?.Invoke(SlotIndex);
            else if (mouse.DoubleClick) Activated?.Invoke(SlotIndex);
            AcceptEvent();
        }
    }

    public static string CategoryName(ItemCategory category) => category switch
    {
        ItemCategory.Weapon => "Weapon",
        ItemCategory.Armour => "Armour",
        ItemCategory.HealthPotion => "Health potion",
        ItemCategory.ManaPotion => "Mana potion",
        ItemCategory.SaveCrystal => "Save crystal",
        _ => category.ToString(),
    };

    /// <summary>Short effect line shown on the card (details live in the panel).</summary>
    public static string EffectText(ItemDefinition def) => def.Category switch
    {
        ItemCategory.Weapon => "Strike: 4 damage on hit",
        ItemCategory.Armour => "+1 defence · 0 absorbed",
        ItemCategory.HealthPotion => $"Restores {def.HealAmount} health",
        ItemCategory.ManaPotion => $"Restores {def.ManaAmount} mana/focus",
        ItemCategory.SaveCrystal => "Spent to record a save",
        _ => "",
    };
}
