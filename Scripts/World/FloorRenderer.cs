using Godot;

namespace DevilGate.Scripts.World;

/// <summary>Draws the isometric floor tiles and the dark ground apron.</summary>
public partial class FloorRenderer : Node2D
{
    private static readonly Color GroundColor = new("14161d");
    private static readonly Color FloorA = new("34444c");
    private static readonly Color FloorB = new("304048");
    private static readonly Color GridColor = new("27363e");

    public override void _Draw()
    {
        // Ground apron behind everything.
        DrawRect(new Rect2(-3000, -3000, 7000, 7000), GroundColor);

        foreach (var (i, j) in SafehouseLayout.FloorTiles())
        {
            var diamond = SafehouseLayout.TileDiamond(i, j);
            var color = ((i + j) & 1) == 0 ? FloorA : FloorB;
            var centre = SafehouseLayout.Iso(i+.5f,j+.5f);
            // Soft local light, sampled per tile so it follows the authored floor.
            var cool = Mathf.Exp(-centre.DistanceSquaredTo(SafehouseLayout.Iso(5,1))/18000f);
            var warm = Mathf.Exp(-centre.DistanceSquaredTo(SafehouseLayout.BedFocus)/7000f);
            color = color.Lerp(new Color("64828a"), cool*.22f);
            color = color.Lerp(new Color("9c8873"), warm*.25f);
            if (i >= 12) color = color.Darkened(0.12f);
            DrawPolygon(diamond,new[] {color.Lightened(.025f),color,color.Darkened(.025f),color});
            // Inlaid service strips give the safehouse a restrained cyberpunk accent.
            if (j == 0 || (i >= 12 && j == 2))
                DrawLine(diamond[0].Lerp(diamond[3], 0.25f), diamond[1].Lerp(diamond[2], 0.25f), new Color("387d83"), 2f, true);

            // Subtle tile seam.
            var closed = new Vector2[5];
            diamond.CopyTo(closed, 0);
            closed[4] = diamond[0];
            DrawPolyline(closed, GridColor, 1f, true);
        }
        DrawRug(1.1f,5.4f,4.1f,7.7f);

    }

    private void DrawRug(float i0, float j0, float i1, float j1)
    {
        var corners = new[] { SafehouseLayout.Iso(i0,j0), SafehouseLayout.Iso(i1,j0),
            SafehouseLayout.Iso(i1,j1), SafehouseLayout.Iso(i0,j1) };
        DrawColoredPolygon(corners,new Color("495561"));
        var centre = (corners[0]+corners[2])*.5f;
        var border = corners.Select(p => p.Lerp(centre,.08f)).ToList();
        border.Add(border[0]);
        DrawPolyline(border.ToArray(),new Color("9c9386"),1.2f,true);
        for(float j=j0+.2f;j<j1;j+=.18f)
            DrawLine(SafehouseLayout.Iso(i0+.15f,j),SafehouseLayout.Iso(i1-.15f,j),new Color(.8f,.8f,.8f,.10f),1,true);
    }
}
