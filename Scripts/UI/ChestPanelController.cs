using DevilGate.Scripts.Data;
using DevilGate.Scripts.Definitions;
using DevilGate.Scripts.Systems;
using Godot;

namespace DevilGate.Scripts;

/// <summary>
/// Chest storage panel: backpack and chest card grids, select-for-details,
/// one/whole-stack transfers in both directions and "Take gold". All rules
/// live in InventoryService; this panel only draws GameSession state and
/// reports OperationResult reasons. Reopening simply redraws — nothing is
/// reseeded or reset.
/// </summary>
public partial class ChestPanelController : PanelContainer, ICloseable
{
    private enum Side { Backpack, Chest }

    private static readonly Color StatusOk = new("7fd6a8");
    private static readonly Color StatusFail = new("e08a8a");

    /// <summary>Fired by the Close button; the stack owner handles popping.</summary>
    public event Action? CloseRequested;

    private Label _goldLabel = null!;
    private Label _backpackLabel = null!;
    private Label _chestLabel = null!;
    private GridContainer _backpackGrid = null!;
    private GridContainer _chestGrid = null!;
    private readonly List<ItemCardController> _backpackCards = new();
    private readonly List<ItemCardController> _chestCards = new();
    private TextureRect _detailsIcon = null!;
    private Label _detailsName = null!;
    private Label _detailsText = null!;
    private Label _statusLabel = null!;
    private Button _takeGoldButton = null!;
    private Button _moveOneButton = null!;
    private Button _moveStackButton = null!;
    private Button _closeButton = null!;

    private Side _selSide;
    private int _selSlot = -1;
    private ItemStack? _selStack;
    private ItemDefinition? _selDef;

    public int SelectedSlot => _selSlot;
    public bool IsChestSelection => _selSlot >= 0 && _selSide == Side.Chest;
    public bool IsBackpackSelection => _selSlot >= 0 && _selSide == Side.Backpack;
    public IReadOnlyList<ItemCardController> BackpackCards => _backpackCards;
    public IReadOnlyList<ItemCardController> ChestCards => _chestCards;

    private static readonly PackedScene CardScene =
        GD.Load<PackedScene>("res://Scenes/ItemCard.tscn");

    public override void _Ready()
    {
        _goldLabel = GetNode<Label>("%GoldLabel");
        _backpackLabel = GetNode<Label>("%BackpackLabel");
        _chestLabel = GetNode<Label>("%ChestLabel");
        _backpackGrid = GetNode<GridContainer>("%BackpackGrid");
        _chestGrid = GetNode<GridContainer>("%ChestGrid");
        _detailsIcon = GetNode<TextureRect>("VBox/DetailsPanel/DetailsHBox/DetailsIcon");
        _detailsName = GetNode<Label>("%DetailsName");
        _detailsText = GetNode<Label>("%DetailsText");
        _detailsName.ClipText = false;
        _detailsName.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _statusLabel = GetNode<Label>("%StatusLabel");
        _takeGoldButton = GetNode<Button>("%TakeGoldButton");
        _moveOneButton = GetNode<Button>("%MoveOneButton");
        _moveStackButton = GetNode<Button>("%MoveStackButton");
        _closeButton = GetNode<Button>("%CloseButton");

        GetNode<Button>("VBox/ActionBar/FooterCloseButton").Pressed += () => CloseRequested?.Invoke();
        _closeButton.Pressed += () => CloseRequested?.Invoke();
        _takeGoldButton.Pressed += OnTakeGold;
        _moveOneButton.Pressed += () => DoTransfer(wholeStack: false);
        _moveStackButton.Pressed += () => DoTransfer(wholeStack: true);

        if (GameSession.Instance != null)
        {
            GameSession.Instance.StateChanged += OnExternalStateChange;
        }

        var takeAll = new Button { Text = "Take all that fits", TooltipText = "Chest slot order; whole stacks only. Stacks that do not fit stay in the chest. Gold is collected too." };
        _takeGoldButton.GetParent().AddChild(takeAll);
        takeAll.Pressed += () => {
            var run = GameSession.Instance?.CurrentRun;
            if (run == null) return;
            var summary = InventoryService.TakeAll(run, GameCatalog.Instance);
            GameSession.Instance!.EmitChanged();
            SetStatus(summary, StatusOk);
        };
        Refresh();
    }

    public override void _ExitTree()
    {
        if (GameSession.Instance != null)
        {
            GameSession.Instance.StateChanged -= OnExternalStateChange;
        }
    }

    private void OnExternalStateChange() => Refresh();

    // ------------------------------------------------------------------
    // Rendering (reads GameSession only — never mutates except via service)
    // ------------------------------------------------------------------

    public void Refresh()
    {
        var run = GameSession.Instance?.CurrentRun;
        var catalog = GameCatalog.Instance;

        if (run == null)
        {
            ClearGrid(_backpackGrid, _backpackCards);
            ClearGrid(_chestGrid, _chestCards);
            ClearSelection();
            _backpackLabel.Text = "Backpack — 0/12";
            _chestLabel.Text = "Chest — 0/12";
            _goldLabel.Text = "Chest gold: 0";
            _takeGoldButton.Disabled = true;
            SetStatus("No run in progress.", StatusFail);
            return;
        }

        _backpackLabel.Text = $"Backpack — {CountUsed(run.Backpack)}/{InventoryState.SlotCount}";
        _chestLabel.Text = $"Chest — {CountUsed(run.Chest.Inventory)}/{InventoryState.SlotCount}";
        _goldLabel.Text = $"Chest gold: {run.Chest.Gold}";
        _takeGoldButton.Disabled = run.Chest.Gold <= 0;

        FillGrid(_backpackGrid, _backpackCards, run.Backpack, Side.Backpack, catalog);
        FillGrid(_chestGrid, _chestCards, run.Chest.Inventory, Side.Chest, catalog);
        RestoreSelection(run);
    }

    private static int CountUsed(InventoryState inv)
    {
        var n = 0;
        for (var i = 0; i < InventoryState.SlotCount; i++)
        {
            if (inv[i] != null) n++;
        }
        return n;
    }

    private static void ClearGrid(GridContainer grid, List<ItemCardController> cards)
    {
        foreach (var child in grid.GetChildren())
        {
            grid.RemoveChild(child);
            child.QueueFree();
        }
        cards.Clear();
    }

    private void FillGrid(GridContainer grid, List<ItemCardController> cards, InventoryState inv, Side side, GameCatalog catalog)
    {
        if (cards.Count != InventoryState.SlotCount)
        {
            ClearGrid(grid, cards);
            for (var slot = 0; slot < InventoryState.SlotCount; slot++)
            {
                var card = CardScene.Instantiate<ItemCardController>();
                card.Selected += s => OnCardSelected(side, s);
                card.StackMoveRequested += s => { OnCardSelected(side, s); DoTransfer(true); };
                grid.AddChild(card);
                cards.Add(card);
            }
        }

        for (var slot = 0; slot < InventoryState.SlotCount; slot++)
        {
            var stack = inv[slot];
            ItemDefinition? def = null;
            if (stack != null)
            {
                catalog.TryGetItem(stack.ItemId, out def);
            }
            cards[slot].Setup(slot, stack, def);
        }
    }

    private void RestoreSelection(RunState run)
    {
        if (_selSlot >= 0 && _selSlot < InventoryState.SlotCount)
        {
            var inv = _selSide == Side.Backpack ? run.Backpack : run.Chest.Inventory;
            if (inv[_selSlot] != null)
            {
                OnCardSelected(_selSide, _selSlot, preserveStatus: true);
                return;
            }
        }
        ClearSelection();
    }

    private void UpdateCardHighlights()
    {
        for (var i = 0; i < _backpackCards.Count; i++)
        {
            _backpackCards[i].SetSelected(_selSide == Side.Backpack && _selSlot == i);
        }
        for (var i = 0; i < _chestCards.Count; i++)
        {
            _chestCards[i].SetSelected(_selSide == Side.Chest && _selSlot == i);
        }
    }

    // ------------------------------------------------------------------
    // Selection + details
    // ------------------------------------------------------------------

    private void OnCardSelected(Side side, int slot, bool preserveStatus = false)
    {
        var run = GameSession.Instance?.CurrentRun;
        if (run == null) return;

        var inv = side == Side.Backpack ? run.Backpack : run.Chest.Inventory;
        var stack = inv[slot];
        if (stack == null)
        {
            ClearSelection();
            return;
        }

        var catalog = GameCatalog.Instance;
        catalog.TryGetItem(stack.ItemId, out var def);
        if (def == null) return;

        _selSide = side;
        _selSlot = slot;
        _selStack = stack;
        _selDef = def;
        UpdateCardHighlights();

        _detailsIcon.Texture = def.LoadIcon();
        _detailsIcon.Visible = _detailsIcon.Texture != null;
        _detailsName.Text = $"{def.DisplayName}  x{stack.Quantity}";
        _detailsText.Text = $"{def.Description}\n{ItemCardController.EffectText(def)} • {_selSide} slot {_selSlot + 1}";

        _moveOneButton.Disabled = false;
        _moveStackButton.Disabled = !def.IsStackable;
        if (!preserveStatus) SetStatus("", StatusOk);
    }

    private void ClearSelection()
    {
        _selSlot = -1;
        _selStack = null;
        _selDef = null;
        UpdateCardHighlights();
        _detailsIcon.Texture = null;
        _detailsIcon.Visible = false;
        _detailsName.Text = "Select an item";
        _detailsText.Text = "Click for details. Shift-click moves a whole stack; visible buttons also transfer items.";
        _moveOneButton.Disabled = true;
        _moveStackButton.Disabled = true;
    }

    // ------------------------------------------------------------------
    // Actions (all go through InventoryService; atomic on failure)
    // ------------------------------------------------------------------

    private void DoTransfer(bool wholeStack)
    {
        var run = GameSession.Instance?.CurrentRun;
        if (run == null || _selStack == null || _selDef == null) return;

        var (src, dst) = _selSide == Side.Backpack
            ? (run.Backpack, run.Chest.Inventory)
            : (run.Chest.Inventory, run.Backpack);

        var itemName = _selDef.DisplayName;
        var result = wholeStack
            ? InventoryService.MoveStack(src, _selSlot, dst, -1, GameCatalog.Instance)
            : InventoryService.MoveOne(src, _selSlot, dst, -1, GameCatalog.Instance);

        if (result.Success)
        {
            GameSession.Instance!.EmitChanged();
            var what = wholeStack ? $"whole stack of {itemName}" : $"1× {itemName}";
            SetStatus($"Moved {what}.", StatusOk);
        }
        else
        {
            SetStatus(result.Reason ?? "Move failed.", StatusFail);
        }
    }

    private void OnTakeGold()
    {
        var run = GameSession.Instance?.CurrentRun;
        if (run == null) return;

        var taken = run.Chest.Gold;
        var result = InventoryService.CollectChestGold(run);
        if (result.Success)
        {
            GameSession.Instance!.EmitChanged();
            SetStatus($"Took {taken} gold.", StatusOk);
        }
        else
        {
            SetStatus(result.Reason ?? "No gold to take.", StatusFail);
        }
    }

    private void SetStatus(string text, Color color)
    {
        _statusLabel.Text = text;
        _statusLabel.AddThemeColorOverride("font_color", color);
    }
}
