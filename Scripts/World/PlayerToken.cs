using Godot;

namespace DevilGate.Scripts.World;

/// <summary>Tinted character token. Drawn in light grey; Modulate supplies the archetype colour.</summary>
public partial class PlayerToken : Node2D
{
    private static readonly Color Body = new("e8ecf4");
    private static readonly Color Shade = new("b9c0d0");

    private float _phase;
    private float _motion;
    private bool _left;
    public bool IdleMotion { get; set; }
    private Texture2D? _cloak;
    public override void _Ready() => _cloak = GD.Load<Texture2D>("res://Assets/Environment/cloaked-traveller-v1.png");
    public override void _Process(double delta)
    {
        // Measure actual motion so blocked paths and modal pauses do not animate walking.
        var speed = (GetParent() as CharacterBody2D)?.GetRealVelocity().Length() ?? 0;
        var velocity = (GetParent() as CharacterBody2D)?.GetRealVelocity() ?? Vector2.Zero;
        if (Mathf.Abs(velocity.X) > 5) _left = velocity.X < 0;
        _motion = Mathf.MoveToward(_motion,Mathf.Clamp(speed/PlayerController.Speed,0,1),(float)delta*8);
        if (Systems.ComfortSettings.ReducedMotion) _motion = 0;
        else if (IdleMotion) _motion = .12f;
        _phase += (float)delta*10;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var cloak = _cloak;
        if (cloak != null)
        {
            // Cancel archetype tint: the same anonymous cloak is used by every character.
            Modulate = Colors.White;
            DrawSetTransform(new Vector2(0,1),0,new Vector2(1,.4f));
            DrawCircle(Vector2.Zero,11,new Color(0,0,0,.23f));
            var bob = -Mathf.Abs(Mathf.Sin(_phase))*1.8f*_motion;
            var sway = Mathf.Sin(_phase*.5f)*.035f*_motion;
            DrawSetTransform(new Vector2(0,bob),sway,new Vector2((_left ? -1 : 1)*(1+Mathf.Sin(_phase)*.025f*_motion),1));
            DrawTextureRectRegion(cloak,new Rect2(-16,-58,32,60),new Rect2(650,10,470,877));
            DrawSetTransform(Vector2.Zero,0,Vector2.One);
            return;
        }
        // Shadow ellipse.
        DrawSetTransform(new Vector2(0, -2), 0, new Vector2(1, 0.45f));
        DrawCircle(Vector2.Zero, 13, new Color(0, 0, 0, 0.28f));
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);

        // Body: rounded trapezoid.
        var body = new[]
        {
            new Vector2(-9, -26), new Vector2(9, -26),
            new Vector2(12, -2), new Vector2(-12, -2),
        };
        DrawColoredPolygon(body, Body);
        // Head.
        DrawCircle(new Vector2(0, -32), 8, Body);
        // Visor stripe (reads as a face direction marker).
        DrawRect(new Rect2(-6, -35, 12, 3), Shade);
    }
}
