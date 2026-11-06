using DevilGate.Scripts.World;
using Godot;
namespace DevilGate.Scripts;
public partial class ArrivalCaption : CanvasLayer
{
    public override void _Ready()
    {
        Layer = 19;
        var label = new Label { Text = WorldRoute.Name(GameSession.Instance!.CurrentRun!.LocationId), Position = new Vector2(250, 96), MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", 24); AddChild(label);
        var cover = new ColorRect { Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore };
        cover.SetAnchorsPreset(Control.LayoutPreset.FullRect); AddChild(cover);
        var tween = CreateTween(); tween.TweenProperty(cover, "modulate:a", 0, .2);
        tween.TweenInterval(1.4); tween.TweenProperty(label, "modulate:a", 0, .4);
        tween.TweenCallback(Callable.From(QueueFree));
    }
}
