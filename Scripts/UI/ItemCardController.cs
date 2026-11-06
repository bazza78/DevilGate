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

    public int SlotIndex { get; private set; } = -1;
    public ItemStack? Stack { get; private set; }

    /// <summary>Fired with the slot index when the card is clicked.</summary>
    public event Action<int>? Selected;

    private StyleBoxFlat? _selectedStyle;
    private bool _selected;

    private TextureRect Icon => GetNode<TextureRect>("Content/Icon");
    private Label NameLabel => GetNode<Label>("Content/Info/NameLabel");
    private Label InfoLabel => GetNode<Label>("Content/Info/InfoLabel");
    private Label EffectLabel => GetNode<Label>("Content/Info/EffectLabel");
    private Control Content => GetNode<Control>("Content");
    private Control EmptyState => GetNode<Control>("EmptyState");

    public override void _Ready()
    {
        _selectedStyle = new StyleBoxFlat
        {
            BgColor = new Color("141a2b"),
            BorderColor = SelectedBorder,
            BorderWidthLeft = 2, BorderWidthTop = 2,
            BorderWidthRight = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
            CornerRadiusBottomRight = 6, CornerRadiusBottomLeft = 6,
            ContentMarginLeft = 8f, ContentMarginRight = 8f,
            ContentMarginTop = 6f, ContentMarginBottom = 6f,
        };
    }

    /// <summary>slot = inventory slot index; stack/def null renders an empty slot.</summary>
    public void Setup(int slot, ItemStack? stack, ItemDefinition? def)
    {
        SlotIndex = slot;
        Stack = stack;
        SetSelected(false);

        var empty = stack == null || def == null;
        Content.Visible = !empty;
        EmptyState.Visible = empty;
        if (empty)
        {
            // Dim the whole card for empty slots.
            Modulate = new Color(1, 1, 1, 0.55f);
            return;
        }
        Modulate = Colors.White;

        Icon.Texture = def!.LoadIcon();
        Icon.Visible = Icon.Texture != null;
        NameLabel.Text = def.DisplayName;
        InfoLabel.Text = $"x{stack!.Quantity} • {CategoryName(def.Category)}";
        EffectLabel.Text = EffectText(def);
    }

    public void SetSelected(bool selected)
    {
        _selected = selected;
        if (_selectedStyle != null)
        {
            if (selected)
            {
                AddThemeStyleboxOverride("panel", _selectedStyle);
            }
            else
            {
                RemoveThemeStyleboxOverride("panel");
            }
        }
    }

    public bool IsSelected => _selected;

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            Selected?.Invoke(SlotIndex);
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
        ItemCategory.Weapon => $"Power {def.Power}",
        ItemCategory.Armour => $"Armour {def.ArmourValue}",
        ItemCategory.HealthPotion => $"Restores {def.HealAmount} health",
        ItemCategory.ManaPotion => $"Restores {def.ManaAmount} mana",
        ItemCategory.SaveCrystal => "Spent to record a save",
        _ => "",
    };
}
