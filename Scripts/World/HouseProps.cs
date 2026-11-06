using Godot;

namespace DevilGate.Scripts.World;

/// <summary>Shared iso-box drawing: a diamond footprint extruded upward.</summary>
public static class IsoDraw
{
    /// <summary>
    /// Draw an iso box. c = centre of the footprint at ground level,
    /// hw/hh = diamond half extents, h = height. Draws the two front faces
    /// then the top.
    /// </summary>
    public static void Box(CanvasItem ci, Vector2 c, float hw, float hh, float h,
        Color top, Color faceRight, Color faceLeft)
    {
        var t = new[]
        {
            c + new Vector2(0, -h - hh), c + new Vector2(hw, -h),
            c + new Vector2(0, -h + hh), c + new Vector2(-hw, -h),
        };
        var b = new[]
        {
            c + new Vector2(0, -hh), c + new Vector2(hw, 0),
            c + new Vector2(0, hh), c + new Vector2(-hw, 0),
        };
        ci.DrawColoredPolygon(new[] { b[1], b[2], t[2], t[1] }, faceRight);
        ci.DrawColoredPolygon(new[] { b[2], b[3], t[3], t[2] }, faceLeft);
        ci.DrawColoredPolygon(t, top);
    }
}

/// <summary>Work table with PC space. Origin = centre of the 2x2 tile footprint on the floor.</summary>
public partial class TableProp : Node2D
{
    private static readonly Color TopWood = new("435564");
    private static readonly Color SideWood = new("293744");
    private static readonly Color LegWood = new("202c37");

    public override void _Draw()
    {
        // Legs first (they reach the floor at the origin).
        DrawRect(new Rect2(-34, -14, 8, 14), LegWood);
        DrawRect(new Rect2(26, -14, 8, 14), LegWood);

        // Tabletop: diamond half 58x29 centred 42px above the floor origin,
        // plus a 10px apron on the two front edges.
        var c = new Vector2(0, -42);
        var top = new[]
        {
            c + new Vector2(0, -29), c + new Vector2(58, 0),
            c + new Vector2(0, 29), c + new Vector2(-58, 0),
        };
        var drop = new Vector2(0, 10);
        // Front-right face (edge right->bottom) and front-left (bottom->left).
        DrawColoredPolygon(new[] { top[1], top[2], top[2] + drop, top[1] + drop }, SideWood);
        DrawColoredPolygon(new[] { top[2], top[3], top[3] + drop, top[2] + drop }, SideWood.Darkened(0.15f));
        DrawColoredPolygon(top, TopWood);
        DrawPolyline(new[] { top[0], top[1], top[2], top[3], top[0] }, new Color("6a8798"), 1.5f, true);
        DrawLine(top[3] + new Vector2(4, 4), top[2] + new Vector2(0, 4), new Color("37bcb6"), 2f, true);
    }
}

/// <summary>Terminal monitor sitting on the table. Origin = point on tabletop.</summary>
public partial class PcProp : Node2D
{
    private static readonly Color Body = new("23262e");
    private static readonly Color Screen = new("0e3a38");
    private static readonly Color Glow = new("46d6c8");
    private static readonly Color Stand = new("171a20");

    public override void _Draw()
    {
        // Keyboard follows the same isometric plane as the table surface.
        DrawColoredPolygon(new[] { new Vector2(-17, 4), new Vector2(7, -8), new Vector2(27, 2), new Vector2(3, 14) }, new Color("172530"));
        for (var row = 0; row < 3; row++)
            DrawLine(new Vector2(-10 + row * 4, 4 + row * 2), new Vector2(7 + row * 4, -4 + row * 2), new Color("71929c"), 1f, true);
        // Stand + base.
        DrawRect(new Rect2(-5, -14, 10, 14), Stand);
        DrawRect(new Rect2(-14, -4, 28, 4), Stand);
        // Monitor body.
        DrawRect(new Rect2(-24, -52, 48, 38), Body);
        // Screen.
        DrawRect(new Rect2(-19, -47, 38, 26), Screen);
        // A couple of glowing UI lines.
        DrawRect(new Rect2(-15, -42, 22, 3), Glow);
        DrawRect(new Rect2(-15, -36, 30, 2), Glow.Darkened(0.3f));
        DrawRect(new Rect2(-15, -31, 18, 2), Glow.Darkened(0.55f));
    }
}

/// <summary>Storage chest. Origin = centre of footprint on the floor.</summary>
public partial class ChestProp : Node2D
{
    private static readonly Color Wood = new("536778");
    private static readonly Color WoodDark = new("344657");
    private static readonly Color WoodTop = new("8b9ba7");
    private static readonly Color Band = new("6bd5ca");
    private static readonly Color Latch = new("b3fff0");

    public override void _Draw()
    {
        // Body box.
        IsoDraw.Box(this, Vector2.Zero, 30, 15, 26, WoodTop, Wood, WoodDark);
        // Lid: a shallower box on top.
        IsoDraw.Box(this, new Vector2(0, -26), 30, 15, 9, WoodTop.Lightened(0.08f), Wood, WoodDark);
        // Metal bands on the front faces + latch.
        DrawLine(new Vector2(-30, -13), new Vector2(-30, -35), Band, 3f);
        DrawLine(new Vector2(30, -13), new Vector2(30, -35), Band, 3f);
        DrawCircle(new Vector2(0, -22), 3.5f, Latch);
    }
}

/// <summary>Decorative bed. Origin = centre of footprint on the floor.</summary>
public partial class BedProp : Node2D
{
    private static readonly Color Frame = new("202c37");
    private static readonly Color FrameDark = new("3a2a18");
    private static readonly Color Mattress = new("758c9d");
    private static readonly Color Pillow = new("dedbd6");

    public override void _Draw()
    {
        // Soft floor shadow and upholstered headboard.
        DrawSetTransform(new Vector2(0,3),0,new Vector2(1,.45f));
        DrawCircle(Vector2.Zero,53,new Color(0,0,0,.22f));
        DrawSetTransform(Vector2.Zero,0,Vector2.One);
        IsoDraw.Box(this,new Vector2(0,-15),48,8,32,new Color("9da3aa"),new Color("596d7b"),FrameDark);
        // Frame box (low).
        IsoDraw.Box(this, Vector2.Zero, 50, 23, 12, Frame, Frame, FrameDark);
        // Mattress slab on top.
        IsoDraw.Box(this, new Vector2(0, -12), 46, 20, 7, Mattress, Mattress.Darkened(0.15f), Mattress.Darkened(0.3f));
        // Pillow at the head (top vertex side = -y in diamond space).
        IsoDraw.Box(this, new Vector2(0, -14), 16, 8, 5, Pillow, Pillow.Darkened(0.1f), Pillow.Darkened(0.2f));
    }
}

/// <summary>Tinted character token lives in PlayerToken.cs (scene-attached).</summary>
