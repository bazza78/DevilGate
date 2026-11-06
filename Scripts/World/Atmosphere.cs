using DevilGate.Scripts.Systems;
using Godot;
namespace DevilGate.Scripts.World;

/// <summary>Local light and drift only; no geometry, collision or ground anchors change.</summary>
public partial class Atmosphere : Node2D
{
    public string Area { get; set; } = "safehouse";
    private float _time;
    public override void _Process(double delta) { if (!ComfortSettings.ReducedMotion) _time += (float)delta; QueueRedraw(); }
    private void Glow(Vector2 at, float radius, Color colour)
    {
        for (int i = 5; i > 0; i--) DrawCircle(at, radius * i / 5, new Color(colour, .018f));
    }
    public override void _Draw()
    {
        if (Area == "safehouse")
        {
            Glow(SafehouseLayout.PcFocus + new Vector2(0, -25), 45, new Color("75d7d9"));
            Glow(SafehouseLayout.PotionFocus + new Vector2(0, -50), 35 + Mathf.Sin(_time) * 2, new Color("c8a8ee"));
            Glow(SafehouseLayout.Iso(3, 1), 95, new Color("d6d6ab"));
            var p = SafehouseLayout.PcFocus + new Vector2(-9, -33);
            for (int i = 0; i < 3; i++) DrawLine(p + new Vector2(0, i * 3), p + new Vector2(8 + Mathf.Sin(_time + i) * 3, i * 3), new Color("99e6dd", .35f), 1, true);
        }
        else if (Area == "guild")
        {
            var p = GuildHallLayout.Contracts.Anchor + new Vector2(0, -35);
            Glow(p, 32, new Color("9bd8cc"));
            DrawCircle(p, 2, new Color("a1ded7", .45f));
        }
        else
        {
            foreach (var p in CourtyardRenderer.Lamps) Glow(p + new Vector2(0, -34), 34, new Color("ebd4a7"));
            if (Area is "woodland" or "grove")
            {
                bool clear = Area == "grove" && GameSession.Instance?.CurrentRun?.Echo.Stage >= 4;
                for (int i = 0; i < (clear ? 6 : 15); i++)
                {
                    float x = 100 + (i * 83 + _time * (5 + i % 3)) % 650;
                    float y = 230 + (i * 47) % 260 + Mathf.Sin(_time * .5f + i) * 8;
                    DrawCircle(new Vector2(x, y), 1.2f, new Color("b5d7bd", .22f));
                }
            }
        }
    }
}
