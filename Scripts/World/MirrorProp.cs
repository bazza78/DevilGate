using Godot;
namespace DevilGate.Scripts.World;
public partial class MirrorProp : Node2D
{
    public override void _Draw()
    {
        // Stand, feet and shadow all terminate at the ground anchor.
        DrawSetTransform(new Vector2(0,-5),0,new Vector2(1,.35f));
        DrawCircle(Vector2.Zero,23,new Color(0,0,0,.22f));DrawSetTransform(Vector2.Zero,0,Vector2.One);
        DrawLine(new(-17,-32),new(-17,-20),new Color("536775"),4,true);
        DrawLine(new(17,-12),new(17,0),new Color("536775"),4,true);
        DrawColoredPolygon(new[]{new Vector2(0,-22),new Vector2(23,-11),new Vector2(0,0),new Vector2(-23,-11)},new Color("536775"));
        var points=new[]{new Vector2(-17,-80),new Vector2(17,-60),new Vector2(17,-12),new Vector2(-17,-32)};
        DrawPolygon(points,new[]{new Color("8bc4cd"),new Color("355363"),new Color("8ba7b3"),new Color("c3d4d9")});
        DrawPolyline(new[]{points[0],points[1],points[2],points[3],points[0]},new Color("c8b58f"),3,true);
        DrawLine(new(-10,-67),new(10,-32),new Color(.8f,.95f,1,.5f),3,true);
    }
}
