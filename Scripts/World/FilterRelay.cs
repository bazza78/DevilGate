using Godot;
namespace DevilGate.Scripts.World;

/// <summary>Code-drawn relay set beside the path; its shared footprint drives navigation.</summary>
public partial class FilterRelay : Node2D
{
    public static Vector2 Anchor=>SafehouseLayout.Iso(7,6.1f);
    public static Vector2 Approach=>SafehouseLayout.Iso(7,7.1f);
    public static Vector2 Focus=>Anchor+new Vector2(0,-32);
    public static Vector2[] Footprint=>new[]{Anchor+new Vector2(-25,-12),Anchor+new Vector2(0,-24),Anchor+new Vector2(25,-12),Anchor};
    public override void _Process(double delta)=>QueueRedraw();
    public override void _Draw()
    {
        var stage=GameSession.Instance?.CurrentRun?.Echo.Stage??0;
        IsoDraw.Box(this,Vector2.Zero,25,12,47,new Color("8ca4a8"),new Color("3b5863"),new Color("28414c"));
        DrawRect(new Rect2(-18,-43,18,24),new Color("162e3a"));
        for(int y=0;y<4;y++)DrawLine(new Vector2(-16,-39+y*5),new Vector2(-2,-39+y*5),new Color("7c959d"),2,true);
        var colour=new Color(stage>=4?"81e1b4":"c48cf1");
        DrawCircle(new Vector2(10,-31),6,colour);DrawCircle(new Vector2(10,-31),2,new Color("e5f6ed"));
        DrawLine(new Vector2(0,-47),new Vector2(0,-65),new Color("8da9ad"),3,true);
        DrawCircle(new Vector2(0,-66),3,colour);
    }
}
