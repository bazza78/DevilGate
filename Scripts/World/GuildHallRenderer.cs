using Godot;
namespace DevilGate.Scripts.World;

/// <summary>Guild architecture and floor decoration; all solid props are separate sorted objects.</summary>
public partial class GuildHallRenderer : Node2D
{
    private static Vector2 P(float i,float j)=>SafehouseLayout.Iso(i,j);
    public override void _Draw()
    {
        DrawRect(new Rect2(-3000,-3000,7000,7000),new Color("14212b"));
        for(int i=0;i<14;i++)for(int j=0;j<9;j++)
        {
            var corners=SafehouseLayout.TileDiamond(i,j);
            var c=new Color((i+j)%2==0?"59666b":"536167");
            DrawPolygon(corners,new[]{c.Lightened(.035f),c,c.Darkened(.03f),c});
            DrawPolyline(new[]{corners[0],corners[1],corners[2],corners[3],corners[0]},new Color("45575d"),.7f,true);
        }
        // A visible front fascia makes this a substantial room rather than a paper-thin floor.
        FrontEdge(P(0,9),P(14,9),new Color("2b414b"));
        FrontEdge(P(14,9),P(14,0),new Color("20343f"));
        // Reception apron and woven waiting/briefing mats establish separate grounded work areas.
        Inset(8.3f,2.2f,11.8f,5.4f,new Color("4b5e60"),new Color("81918a"));
        Inset(1.2f,3.8f,3.8f,6.1f,new Color("294a50"),new Color("8a8f71"));
        Inset(11.1f,6.6f,13.8f,8.8f,new Color("2f5053"),new Color("92936f"));
        // Woven teal runner through the entrance and toward registration.
        FloorPatch(2.2f,5.9f,10.8f,7.5f,new Color("345454"));
        FloorPatch(9.2f,3.8f,10.8f,7.5f,new Color("345454"));
        DrawLine(P(3,6.05f),P(9,6.05f),new Color("a8956e"),1.5f,true);
        DrawLine(P(3,7.35f),P(10.65f,7.35f),new Color("a8956e"),1.5f,true);
        Wall(P(0,9),P(0,0));Wall(P(0,0),P(14,0));
        Banner(P(0,4.8f),P(0,6.4f));Banner(P(8.5f,0),P(10,0));
        // Abstract brass guild compass inlay. Decorative, never a movement blocker.
        var centre=P(7,4.4f);
        DrawSetTransform(centre,0,new Vector2(1,.5f));
        DrawArc(Vector2.Zero,35,0,Mathf.Tau,64,new Color("b9a77c"),2,true);
        DrawPolyline(new[]{new Vector2(0,-27),new Vector2(9,-7),new Vector2(27,0),new Vector2(9,7),new Vector2(0,27),new Vector2(-9,7),new Vector2(-27,0),new Vector2(-9,-7),new Vector2(0,-27)},new Color("d7c397"),2,true);
        DrawSetTransform(Vector2.Zero,0,Vector2.One);
        GroundShadows();
        var door=CourtyardRenderer.Door;
        DrawColoredPolygon(new[]{door+new Vector2(-17,-8),door+new Vector2(17,8),door+new Vector2(17,-53),door+new Vector2(-17,-69)},new Color("253d48"));
        DrawPolyline(new[]{door+new Vector2(-17,-8),door+new Vector2(-17,-69),door+new Vector2(17,-53),door+new Vector2(17,8)},new Color("bea981"),3,true);
    }
    private void FrontEdge(Vector2 a,Vector2 b,Color colour)
    {
        var down=new Vector2(0,12);
        DrawColoredPolygon(new[]{a,b,b+down,a+down},colour);
        DrawLine(a,b,new Color("a2ada0"),1.5f,true);
        DrawLine(a+down,b+down,new Color("162a34"),2,true);
    }
    private void Inset(float i,float j,float x,float y,Color fill,Color trim)
    {
        FloorPatch(i,j,x,y,fill);
        DrawPolyline(new[]{P(i,j),P(x,j),P(x,y),P(i,y),P(i,j)},trim,1,true);
        var seam=new Color(fill.Lightened(.1f),.5f);
        DrawLine(P(i+.13f,j+.13f),P(x-.13f,j+.13f),seam,1,true);
    }
    private void GroundShadows()
    {
        foreach(var prop in GuildHallLayout.Furnishings)
        {
            var points=GuildHallLayout.Contacts[prop.Cell].Feet.Select(p=>GuildHallLayout.ContactOffset(prop,p)).ToArray();
            var min=new Vector2(points.Min(p=>p.X),points.Min(p=>p.Y));
            var max=new Vector2(points.Max(p=>p.X),points.Max(p=>p.Y));
            var centre=(min+max)*.5f+prop.Anchor;
            var radii=(max-min)*.5f+new Vector2(3,2);
            for(int layer=3;layer>=1;layer--)
            {
                DrawSetTransform(centre,0,radii*(1+layer*.09f));
                DrawCircle(Vector2.Zero,1,new Color(0,0,0,.035f));
            }
            foreach(var point in points)
            {
                float radius=Mathf.Max(3,prop.Width*.043f);
                DrawSetTransform(prop.Anchor+point+new Vector2(0,.4f),0,new Vector2(radius,radius*.35f));
                DrawCircle(Vector2.Zero,1,new Color(0,0,0,.22f));
                DrawCircle(Vector2.Zero,.55f,new Color(0,0,0,.13f));
            }
            DrawSetTransform(Vector2.Zero,0,Vector2.One);
        }
    }
    private void FloorPatch(float i,float j,float x,float y,Color colour)=>DrawColoredPolygon(new[]{P(i,j),P(x,j),P(x,y),P(i,y)},colour);
    private void Wall(Vector2 a,Vector2 b)
    {
        var up=new Vector2(0,-100);
        DrawPolygon(new[]{a,b,b+up,a+up},new[]{new Color("728180"),new Color("7b8984"),new Color("c1bba8"),new Color("b4b3a4")});
        DrawLine(a+new Vector2(0,-9),b+new Vector2(0,-9),new Color("40565c"),7,true);
        DrawLine(a+new Vector2(0,-15),b+new Vector2(0,-15),new Color("b4a176"),2,true);
        DrawLine(a+up,b+up,new Color("dfd4ba"),5,true);
        int panels=(int)(a.DistanceTo(b)/62);
        for(int k=1;k<panels;k++)
        {
            var p=a.Lerp(b,k/(float)panels);
            DrawLine(p+new Vector2(0,-18),p+up+new Vector2(0,5),new Color("8c968d"),1,true);
        }
    }
    private void Banner(Vector2 a,Vector2 b)
    {
        a+=new Vector2(0,-84);b+=new Vector2(0,-84);var d=new Vector2(0,56);
        DrawColoredPolygon(new[]{a,b,b+d,(a+b)*.5f+d+new Vector2(0,8),a+d},new Color("244d55"));
        DrawPolyline(new[]{a+d,a,b,b+d},new Color("c6aa73"),2,true);
        var c=(a+b)*.5f+d*.48f;
        DrawPolyline(new[]{c+new Vector2(0,-13),c+new Vector2(9,0),c+new Vector2(0,13),c+new Vector2(-9,0),c+new Vector2(0,-13)},new Color("d9be83"),2,true);
    }
}
