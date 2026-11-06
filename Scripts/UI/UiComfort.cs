using DevilGate.Scripts.Systems;
using Godot;
namespace DevilGate.Scripts;

/// <summary>Applies readable text and keyboard focus to newly opened controls as well as existing menus.</summary>
public partial class UiComfort : Node
{
    private double _elapsed;
    public override void _Process(double delta)
    {
        _elapsed += delta; if (_elapsed < .2) return; _elapsed = 0;
        Visit(GetTree().Root);
    }
    private static void Visit(Node node)
    {
        if (node is Control control && control is Label or Button or RichTextLabel)
        {
            string font = control is RichTextLabel ? "normal_font_size" : "font_size";
            if (!control.HasMeta("comfort_font")) control.SetMeta("comfort_font", control.GetThemeFontSize(font));
            int size = (int)Math.Round((int)control.GetMeta("comfort_font") * ComfortSettings.TextScale);
            if (control.GetThemeFontSize(font) != size) control.AddThemeFontSizeOverride(font, size);
            if (control is Button && !control.HasMeta("comfort_focus"))
            {
                var style = new StyleBoxFlat { BgColor = new Color(0,0,0,0), BorderColor = new Color("f1d59b"), BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2 };
                control.AddThemeStyleboxOverride("focus", style); control.SetMeta("comfort_focus", true);
            }
        }
        foreach (var child in node.GetChildren()) Visit(child);
    }
}
