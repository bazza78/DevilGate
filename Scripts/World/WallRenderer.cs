using Godot;

namespace DevilGate.Scripts.World;

/// <summary>
/// Draws exterior walls (tall back walls, low front curbs) and the outside
/// door. Static layer: drawn under the y-sorted props/player.
/// </summary>
public partial class WallRenderer : Node2D
{
    private static readonly Color FaceColor = new("b5b3b1");
    private static readonly Color FaceShade = new("8d969e");
    private static readonly Color TopColor = new("d8d4cb");
    private static readonly Color CurbTopColor = new("4a5468");
    private static readonly Color DoorDark = new("0c0e14");
    private static readonly Color DoorFrame = new("46d6c8");

    private static readonly Vector2 HouseCenter = new(400, 370);

    // Keep the custom-drawn window texture alive across managed collections.
    private Texture2D? _cityTexture;
    public override void _Ready() => _cityTexture=GD.Load<Texture2D>("res://Assets/Environment/city-window-v1.png");

    public override void _Draw()
    {
        foreach (var seg in SafehouseLayout.ExteriorWalls())
        {
            DrawWall(seg.A, seg.B, seg.Tall ? SafehouseLayout.WallHeight : SafehouseLayout.CurbHeight);
        }
        DrawDoor();
        DrawWallArt(SafehouseLayout.Iso(0,2.2f),SafehouseLayout.Iso(0,3.7f),new Color("a98dbf"));
        DrawWallArt(SafehouseLayout.Iso(14,2),SafehouseLayout.Iso(15.5f,2),new Color("70c1c0"));
        // Recessed city window on the rear wall, in the same isometric plane.
        var a = SafehouseLayout.Iso(3,0) + new Vector2(0,-65);
        var b = SafehouseLayout.Iso(8,0) + new Vector2(0,-65);
        var down = new Vector2(0,45);
        var city = _cityTexture;
        var glass = new[] {a,b,b+down,a+down};
        if (city != null)
            DrawPolygon(glass,new[] {Colors.White,Colors.White,Colors.White,Colors.White},
                new[] {Vector2.Zero,Vector2.Right,Vector2.One,Vector2.Down},city);
        else DrawColoredPolygon(glass,new Color("172b43"));
        // Glass reflection and a slim central mullion.
        DrawColoredPolygon(new[] {a.Lerp(b,.12f),a.Lerp(b,.19f),a.Lerp(b,.44f)+down,a.Lerp(b,.37f)+down},new Color(0.7f,.9f,1f,.08f));
        DrawLine(a.Lerp(b,.5f),a.Lerp(b,.5f)+down,new Color("415a6b"),2,true);
        DrawPolyline(new[] {a,b,b+down,a+down,a},new Color("4f6877"),3,true);
        DrawLine(a,b,new Color("80e6d8"),2,true);
    }

    private void DrawWallArt(Vector2 a,Vector2 b,Color accent)
    {
        a+=new Vector2(0,-64);b+=new Vector2(0,-64);var down=new Vector2(0,36);
        DrawColoredPolygon(new[]{a,b,b+down,a+down},new Color("293a4b"));
        DrawPolyline(new[]{a,b,b+down,a+down,a},new Color("d2c3a4"),2,true);
        var middle=(a+b)*.5f+down*.5f;
        DrawLine(a.Lerp(b,.2f)+down*.75f,middle-down*.2f,accent,3,true);
        DrawLine(middle-down*.2f,a.Lerp(b,.8f)+down*.75f,accent,3,true);
        DrawCircle(middle+new Vector2(0,-7),3,accent);
    }

    private void DrawWall(Vector2 a, Vector2 b, float height)
    {
        // Inward normal: from edge midpoint toward the house centre.
        var mid = (a + b) * 0.5f;
        var inward = (HouseCenter - mid).Normalized() * 10f;
        var up = new Vector2(0, -height);

        var topA = a + up;
        var topB = b + up;
        var inA = topA + inward;
        var inB = topB + inward;

        // Vertical face.
        DrawPolygon(new[] { a, b, topB, topA },new[] {FaceColor.Darkened(.24f),FaceColor.Darkened(.20f),FaceColor,FaceColor.Lightened(.08f)});
        // Inner face shade (thin strip toward inside).
        // Architectural trim and individually inset vertical wall panels.
        DrawLine(a+new Vector2(0,-4),b+new Vector2(0,-4),new Color("566775"),4,true);
        if (height > 30)
        {
            int count = Mathf.Max(1,(int)(a.DistanceTo(b)/62));
            for (int i=1;i<count;i++)
            {
                var p = a.Lerp(b,i/(float)count);
                DrawLine(p+new Vector2(0,-8),p+up+new Vector2(0,5),new Color("737f88"),1,true);
                DrawLine(p+new Vector2(1,-8),p+up+new Vector2(1,5),new Color("c1c2bc"),1,true);
            }
            DrawLine(a+up+new Vector2(0,6),b+up+new Vector2(0,6),new Color("ece3cc"),2,true);
            DrawLine(a+new Vector2(0,-7),b+new Vector2(0,-7),new Color("5aaca9"),1.5f,true);
        }
        // Top cap.
        DrawColoredPolygon(new[] { topA, topB, inB, inA }, height > 30 ? TopColor : CurbTopColor);
    }

    private void DrawDoor()
    {
        // Door sits on the room A front edge at grid i from the shared layout, width ~1.3 tiles.
        var p1 = SafehouseLayout.Iso(SafehouseLayout.DoorGridI + .35f, 8f);
        var p2 = SafehouseLayout.Iso(SafehouseLayout.DoorGridI - .35f, 8f);
        var mid = (p1 + p2) * 0.5f;
        var inward = (HouseCenter - mid).Normalized() * 16f;
        var up = new Vector2(0, -58f);

        // Dark opening (goes inward and up).
        DrawColoredPolygon(new[] { p1, p2, p2 + inward + up * 0.4f, p1 + inward + up * 0.4f }, DoorDark);
        // Solid sliding leaf, inset seam and access indicator.
        DrawPolygon(new[] {p1,p2,p2+up,p1+up},new[] {new Color("293b4b"),new Color("34495b"),new Color("637584"),new Color("4b6172")});
        DrawLine(mid,mid+up,new Color("182c38"),1.5f,true);
        DrawLine(p1+up*.3f,p1+up*.6f,new Color("b6e4de"),2,true);
        DrawCircle(p1+new Vector2(9,-25),3,new Color("81e3c8"));
        // Frame posts + lintel.
        DrawLine(p1, p1 + up, DoorFrame, 4f, true);
        DrawLine(p2, p2 + up, DoorFrame, 4f, true);
        DrawLine(p1 + up, p2 + up, DoorFrame, 4f, true);
        // Threshold strip on the floor.
        DrawLine(p1, p2, DoorFrame, 2f, true);
    }
}
