using DevilGate.Scripts.Systems;
using Godot;
namespace DevilGate.Scripts;

public partial class ComfortMenu : PanelContainer, ICloseable
{
    public event Action? CloseRequested;
    public override void _Ready()
    {
        AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color("14212ff5"), BorderColor = new Color("47746f"), BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1, CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8, ContentMarginLeft = 18, ContentMarginRight = 18, ContentMarginTop = 14, ContentMarginBottom = 14 });
        CustomMinimumSize = new Vector2(480, 0);
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 12); AddChild(v);
        v.AddChild(new Label { Text = "Pause · Settings", HorizontalAlignment = HorizontalAlignment.Center });
        var resume = new Button { Text = "Resume" }; v.AddChild(resume); resume.Pressed += () => CloseRequested?.Invoke();
        var status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        void Persist() { status.Text = ComfortSettings.Save() == Error.Ok ? "Preferences saved" : "Could not save preferences; changes apply for this session."; }
        void Slider(string text, float value, float min, float max, Action<float> set)
        {
            var row = new HBoxContainer(); v.AddChild(row);
            var label = new Label { Text = text, CustomMinimumSize = new Vector2(190, 0) }; row.AddChild(label);
            var slider = new HSlider { MinValue = min, MaxValue = max, Step = .05, Value = value, SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(220, 28), TooltipText = text };
            row.AddChild(slider); slider.ValueChanged += x => { set((float)x); Persist(); };
        }
        void Toggle(string text, bool value, Action<bool> set)
        {
            var check = new CheckButton { Text = text + (value ? " · On" : " · Off"), ButtonPressed = value }; v.AddChild(check);
            check.Toggled += x => { check.Text = text + (x ? " · On" : " · Off"); set(x); Persist(); };
        }
        Slider("Master volume", ComfortSettings.Master, 0, 1, x => ComfortSettings.Master = x);
        Slider("Effects volume", ComfortSettings.Effects, 0, 1, x => ComfortSettings.Effects = x);
        Slider("Ambience volume", ComfortSettings.Ambience, 0, 1, x => ComfortSettings.Ambience = x);
        Slider("UI / text scale", ComfortSettings.TextScale, 1, 1.2f, x => ComfortSettings.TextScale = x);
        Toggle("Faster battle presentation", ComfortSettings.FastBattle, x => ComfortSettings.FastBattle = x);
        Toggle("Reduce motion", ComfortSettings.ReducedMotion, x => ComfortSettings.ReducedMotion = x);
        Toggle("Reduce flashes", ComfortSettings.ReducedFlash, x => ComfortSettings.ReducedFlash = x);
        var quit = new Button { Text = "Quit game" }; v.AddChild(quit);
        quit.Pressed += () => GameSession.Instance?.RequestQuit(this);
        v.AddChild(status);
        resume.GrabFocus();
    }
}
