using Godot;
namespace DevilGate.Scripts.World;

/// <summary>Separate anchored furnishings; their tile footprints are shared with navigation.</summary>
public partial class StudioDetails : Node2D
{
    public string Kind { get; set; } = "station";
    public override void _Draw()
    {
        var metal = new Color("71858b");
        var dark = new Color("293a48");
        var teal = new Color("6fe6d5");
        if (Kind == "station")
        {
            IsoDraw.Box(this, Vector2.Zero, 43, 21, 32, metal, dark.Lightened(.1f), dark);
            DrawLine(new(-39,-29), new(0,-10), teal, 2, true);
            for (int i = 0; i < 3; i++)
            {
                var p = new Vector2(-18 + i * 17, -36 + i * 2);
                DrawCircle(p, 5, new Color(i == 0 ? "7cddd2" : i == 1 ? "bd9ef0" : "e3b97d"));
                DrawRect(new Rect2(p + new Vector2(-2,-10), new Vector2(4,7)), metal.Lightened(.3f));
            }
            DrawColoredPolygon(new[] {new Vector2(23,-61),new Vector2(30,-45),new Vector2(23,-33),new Vector2(16,-45)}, new Color("b99cf2"));
            DrawLine(new(23,-58),new(23,-36),new Color("eee2ff"),1,true);
        }
        else if (Kind == "rack")
        {
            IsoDraw.Box(this, Vector2.Zero, 39, 13, 66, metal, dark, dark.Darkened(.15f));
            DrawLine(new(-31,-54),new(27,-32),teal,2,true);
            for (int i = 0; i < 3; i++)
            {
                var p = new Vector2(-22 + i*21, -46+i*5);
                DrawLine(p,p+new Vector2(0,29),metal.Lightened(.4f),3,true);
                DrawLine(p+new Vector2(-6,9),p+new Vector2(6,9),new Color("d7b784"),3,true);
            }
        }
        else if (Kind == "sofa")
        {
            IsoDraw.Box(this,Vector2.Zero,53,25,13,dark, dark.Darkened(.1f),dark.Darkened(.3f));
            IsoDraw.Box(this,new Vector2(0,-13),48,21,10,new Color("8b939e"),metal,dark);
            IsoDraw.Box(this,new Vector2(0,-29),47,9,22,new Color("b1acaa"),new Color("7e818c"),metal);
            IsoDraw.Box(this,new Vector2(-36,-16),12,17,15,metal, dark,dark);
            IsoDraw.Box(this,new Vector2(36,-16),12,17,15,metal,dark,dark);
        }
    }
}
