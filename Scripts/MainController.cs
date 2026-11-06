using Godot;

namespace DevilGate.Scripts;

/// <summary>
/// Root scene. Owns switching between the title, character creation and
/// safehouse screens.
/// </summary>
public partial class MainController : Node
{
    private Node? _currentScreen;

    public override void _Ready()
    {
        ShowTitle();
    }

    public void ShowTitle() => ShowScreen("res://Scenes/Title.tscn");

    public void ShowCharacterCreation() => ShowScreen("res://Scenes/CharacterCreation.tscn");

    public void ShowSafehouse() => ShowScreen("res://Scenes/Safehouse.tscn");

    private void ShowScreen(string path)
    {
        if (_currentScreen != null)
        {
            foreach (var player in _currentScreen.GetChildren().OfType<World.PlayerController>()) player.TrackPosition = false;
            _currentScreen.ProcessMode = ProcessModeEnum.Disabled;
            _currentScreen.QueueFree();
            _currentScreen = null;
        }

        var packed = GD.Load<PackedScene>(path);
        if (packed == null)
        {
            GD.PushError($"MainController: failed to load scene {path}");
            return;
        }

        _currentScreen = packed.Instantiate<Node>();
        AddChild(_currentScreen);
    }
}
