using Godot;
namespace DevilGate.Scripts.World;

/// <summary>The saved courtyard ID is retained for compatibility; visually this is a neighbourhood park.</summary>
public partial class CourtyardRenderer : Node2D
{
    public string Area { get; set; } = "courtyard";
    public static readonly Rect2 Bounds=new(0,150,850,480);
    public static Vector2 Spawn=>SafehouseLayout.Iso(3,7);
    public static Vector2 Door=>SafehouseLayout.Iso(2,8);
    public static Vector2 Contact=>SafehouseLayout.Iso(10,3);
    public static Vector2 ContactApproach=>SafehouseLayout.Iso(10,4.5f);
    public static Vector2[] WalkPolygon=>new[] {SafehouseLayout.Iso(1,1),SafehouseLayout.Iso(13,1),SafehouseLayout.Iso(13,9),SafehouseLayout.Iso(1,9)};
    private static Vector2[] Rectangle(float i0,float j0,float i1,float j1)=>new[]{SafehouseLayout.Iso(i0,j0),SafehouseLayout.Iso(i1,j0),SafehouseLayout.Iso(i1,j1),SafehouseLayout.Iso(i0,j1)};
    public static Vector2[][] Paths=>new[]{Rectangle(2,6.2f,11.2f,7.8f),Rectangle(9.2f,2.2f,11.2f,7.8f),Rectangle(1.2f,6.7f,3.8f,8.8f)};
    public static readonly Vector2[] Lamps={SafehouseLayout.Iso(4,5.7f),SafehouseLayout.Iso(8,5.7f),SafehouseLayout.Iso(11.9f,2)};
    public static Vector2[] LampFootprint(Vector2 p)=>Enumerable.Range(0,12).Select(i=>p+Vector2.FromAngle(i*Mathf.Tau/12)*5).ToArray();
    private void Patch(float i0,float j0,float i1,float j1,Color colour)
    {
        DrawColoredPolygon(new[]{SafehouseLayout.Iso(i0,j0),SafehouseLayout.Iso(i1,j0),SafehouseLayout.Iso(i1,j1),SafehouseLayout.Iso(i0,j1)},colour);
    }
    public override void _Draw()
    {
        DrawRect(new Rect2(-3000,-3000,7000,7000),new Color("12242b"));
        if(Area=="town")
        {
            Building(SafehouseLayout.Iso(2,-1.6f),48,80,false);
            Building(SafehouseLayout.Iso(5,-1.6f),48,100,false);
            Building(SafehouseLayout.Iso(10,-1.4f),73,115,true);
        }
        Patch(0,0,14,9,new Color(Area=="town" ? "445a65" : Area=="grove" ? "375e57" : "315b4b"));
        for(int i=0;i<14;i++)for(int j=0;j<9;j++)
        {
            var c=SafehouseLayout.Iso(i+.4f,j+.5f);
            DrawLine(c,c+new Vector2(2,-3),new Color("45725b"),1,true);
            DrawLine(c+new Vector2(13,4),c+new Vector2(14,1),new Color("284d40"),1,true);
        }
        // A separate paved approach turns through the park to the liaison's meeting point.
        Patch(2,6.2f,11.2f,7.8f,new Color("697e7a"));
        Patch(9.2f,2.2f,11.2f,7.8f,new Color("697e7a"));
        Patch(1.2f,6.7f,3.8f,8.8f,new Color("7a8d86"));
        for(int i=3;i<11;i++)
            DrawLine(SafehouseLayout.Iso(i,6.3f),SafehouseLayout.Iso(i,7.7f),new Color("506b66"),1,true);
        for(int j=3;j<8;j++)
            DrawLine(SafehouseLayout.Iso(9.3f,j),SafehouseLayout.Iso(11.1f,j),new Color("506b66"),1,true);
        DrawLine(SafehouseLayout.Iso(3,6.2f),SafehouseLayout.Iso(9.2f,6.2f),new Color("b2b7a1"),2,true);
        DrawLine(SafehouseLayout.Iso(11.2f,2.2f),SafehouseLayout.Iso(11.2f,7.8f),new Color("b2b7a1"),2,true);
        // Trees stay beyond the walkable outline; their trunks cannot be walked through.
        foreach(var ij in new[]{new Vector2(.4f,1),new Vector2(.4f,4),new Vector2(2,.4f),new Vector2(5,.4f),new Vector2(14.2f,4),new Vector2(12.8f,8.8f)})
            if(Area!="town" || ij.X<1) Tree(SafehouseLayout.Iso(ij.X,ij.Y));
        if(Area is "woodland" or "grove")
            foreach(var ij in new[]{new Vector2(7,.3f),new Vector2(10,.3f),new Vector2(13,.3f),new Vector2(.3f,7)})Tree(SafehouseLayout.Iso(ij.X,ij.Y));
        // Neighbourhood wayfinding lanterns along the path.
        foreach(var p in Lamps)
        {
            DrawLine(p,p+new Vector2(0,-33),new Color("293f43"),4,true);
            DrawCircle(p+new Vector2(0,-34),5,new Color("e9d6a1"));
        }
        var gate=Door;
        DrawLine(gate+new Vector2(-16,0),gate+new Vector2(-16,-25),new Color("a7bfb4"),3,true);
        DrawLine(gate+new Vector2(16,12),gate+new Vector2(16,-13),new Color("a7bfb4"),3,true);
    }
    private void Building(Vector2 p,float width,float height,bool guild)
    {
        IsoDraw.Box(this,p,width,width*.45f,height,new Color("9aa9ae"),new Color("536979"),new Color("415767"));
        for(int floor=0;floor<3;floor++)for(int window=0;window<3;window++)
        {
            var q=p+new Vector2(-width+12+window*15,-height+26+floor*22+window*6);
            DrawLine(q,q+new Vector2(0,10),new Color(guild ? "a5dfd2" : "e7c998"),6,true);
        }
        DrawLine(p+new Vector2(-width,-height),p+new Vector2(0,-height+width*.45f),new Color("c9d3cf"),3,true);
        DrawLine(p+new Vector2(0,-height+width*.45f),p+new Vector2(width,-height),new Color("c9d3cf"),3,true);
        if(guild)
        {
            var entry=p+new Vector2(-width*.38f,-width*.28f);
            DrawColoredPolygon(new[]{entry+new Vector2(-10,-5),entry+new Vector2(10,5),entry+new Vector2(10,-29),entry+new Vector2(-10,-39)},new Color("233d4e"));
            DrawLine(entry+new Vector2(-10,-39),entry+new Vector2(10,-29),new Color("7de0cb"),3,true);
            DrawLine(entry+new Vector2(0,-28),entry+new Vector2(0,0),new Color("91afa8"),1,true);
            var q=p+new Vector2(20,-height*.6f);
            DrawColoredPolygon(new[]{q+new Vector2(0,-15),q+new Vector2(12,0),q+new Vector2(0,15),q+new Vector2(-12,0)},new Color("d5bb83"));
            DrawLine(q+new Vector2(0,-9),q+new Vector2(0,9),new Color("314957"),3,true);
        }
    }

    private void Tree(Vector2 p)
    {
        DrawSetTransform(p,0,new Vector2(1,.4f));DrawCircle(Vector2.Zero,30,new Color(0,0,0,.18f));DrawSetTransform(Vector2.Zero,0,Vector2.One);
        DrawLine(p,p+new Vector2(0,-42),new Color("655e48"),7,true);
        DrawCircle(p+new Vector2(0,-46),24,new Color("244d40"));
        DrawCircle(p+new Vector2(-8,-55),21,new Color("3b6b52"));
        DrawCircle(p+new Vector2(8,-62),17,new Color("508064"));
        DrawCircle(p+new Vector2(-2,-68),12,new Color("68936e"));
    }
}
