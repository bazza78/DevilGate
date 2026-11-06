using Godot;

namespace DevilGate.Scripts;

public partial class TitleController : Control
{
    private Label _statusLabel = null!;

    public override void _Ready()
    {
        _statusLabel = GetNode<Label>("%StatusLabel");
        GetNode<Button>("CenterContainer/VBox/NewCharacterButton").Pressed += OnNewCharacterPressed;
        GetNode<Button>("CenterContainer/VBox/QuitButton").Pressed += OnQuitPressed;

        var loadButton = GetNode<Button>("%LoadButton");
        loadButton.Pressed += OnLoadPressed;
        loadButton.Disabled = !(GameSession.Instance?.HasSave() ?? false);
        if (loadButton.Disabled)
        {
            loadButton.TooltipText = "No save file yet.";
        }
    }

    private void OnNewCharacterPressed()
    {
        if (HasUnsavedProgress())
        {
            ConfirmDialogHelper.Show(this, "Unsaved progress",
                "Your current character has progress that hasn't been saved. Start a new character anyway?",
                () => BeginAdventure());
            return;
        }
        BeginAdventure();
    }

    private void BeginAdventure()
    {
        GameSession.Instance?.BeginAdventure();
        GetParent<MainController>()?.ShowSafehouse();
    }

    private void OnLoadPressed()
    {
        var session = GameSession.Instance;
        if (session == null) return;

        if (!session.TryLoadGame(out var error))
        {
            _statusLabel.Text = $"Load failed: {error}";
            return;
        }
        _statusLabel.Text = "";
        GetParent<MainController>()?.ShowSafehouse();
    }

    private void OnQuitPressed()
    {
        if (HasUnsavedProgress())
        {
            ConfirmDialogHelper.Show(this, "Unsaved progress",
                "You have progress that hasn't been saved. Quit anyway?",
                () => GetTree().Quit());
            return;
        }
        GetTree().Quit();
    }

    private static bool HasUnsavedProgress() =>
        GameSession.Instance?.HasUnsavedChanges ?? false;
}
