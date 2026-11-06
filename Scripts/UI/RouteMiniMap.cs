using Godot;
using DevilGate.Scripts.World;
namespace DevilGate.Scripts;
public partial class RouteMiniMap : Control
{
    private readonly StyleBoxFlat _background=new() {BgColor=new Color("14232f"),CornerRadiusTopLeft=6,CornerRadiusTopRight=6,CornerRadiusBottomLeft=6,CornerRadiusBottomRight=6};
    private static Vector2 Map(Vector2 p)=>new(Mathf.Clamp((p.X-80)/600,0,1)*150+15,Mathf.Clamp((p.Y-200)/380,0,1)*65+82);
    public override void _Ready(){Position=new Vector2(20,18);Size=new Vector2(195,168);MouseFilter=MouseFilterEnum.Stop;}
    public override void _Process(double delta)=>QueueRedraw();
    public override void _Draw()
    {
        var run=GameSession.Instance?.CurrentRun;if(run==null)return;
        var font=ThemeDB.FallbackFont;var index=WorldRoute.Index(run.LocationId);
        DrawStyleBox(_background,new Rect2(Vector2.Zero,Size));
        DrawString(font,new Vector2(10,20),WorldRoute.Name(run.LocationId),fontSize:14,modulate:Colors.White);
        DrawLine(new Vector2(16,42),new Vector2(178,42),new Color("638080"),2,true);
        for(int i=0;i<WorldRoute.Areas.Length;i++)DrawCircle(new Vector2(16+i*162f/(WorldRoute.Areas.Length-1),42),i==index?6:3,new Color(i==index?"a6f4ce":"72868b"));
        DrawString(font,new Vector2(9,65),"Home",fontSize:11,modulate:new Color("afc7cd"));
        DrawString(font,new Vector2(154,65),"Guild",fontSize:11,modulate:new Color("afc7cd"));
        // Local path and player position, mapped to the same authored world coordinates.
        var points=new[]{Map(CourtyardRenderer.Spawn),Map(SafehouseLayout.Iso(10,7)),Map(CourtyardRenderer.ContactApproach)};
        DrawPolyline(points,new Color("8da79e"),5,true);
        var world=new Vector2(run.PositionX,run.PositionY);
        // Position updates during movement via the live player, not just the save snapshot.
        var player=GetTree().GetNodesInGroup("route_player").OfType<Node2D>().FirstOrDefault();
        if(player!=null)world=player.GlobalPosition;
        if(run.LocationId=="grove")
        {
            var relay=Map(FilterRelay.Anchor);
            DrawRect(new Rect2(relay-new Vector2(3,3),new Vector2(6,6)),new Color("efc77f"));
        }
        var local=Map(world);
        DrawCircle(local,4,new Color("a6f4ce"));
        DrawString(font,new Vector2(10,158),"● You     Route to reception",fontSize:11,modulate:new Color("afc7cd"));
    }
}
