using Godot;
using DevilGate.Scripts.World;
namespace DevilGate.Scripts.Checks;
public partial class GuildHallChecks : Node
{
    private async Task Frame()=>await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
    public override async void _Ready()
    {
        GameSession.Instance!.StartTemporaryRun();var run=GameSession.Instance.CurrentRun!;
        run.LocationId="guild";run.WakingIntroductionSeen=true;run.PositionX=CourtyardRenderer.Spawn.X;run.PositionY=CourtyardRenderer.Spawn.Y;
        var house=GD.Load<PackedScene>("res://Scenes/Safehouse.tscn").Instantiate<SafehouseController>();AddChild(house);
        for(int i=0;i<40;i++)await Frame();
        var player=house.GetChildren().OfType<PlayerController>().Single();var map=player.GetWorld2D().NavigationMap;
        int failures=0;int samples=0;
        foreach(var desired in new[]{GuildHallLayout.Contracts.Anchor+new Vector2(0,35),CourtyardRenderer.Contact+new Vector2(0,55),SafehouseLayout.Iso(10,7.8f),CourtyardRenderer.Spawn})
        {
            var target=NavigationServer2D.MapGetClosestPoint(map,desired);player.MoveTo(target);bool overlap=false;
            for(int i=0;i<500;i++)
            {
                await Frame();samples++;
                foreach(var prop in GuildHallLayout.Furnishings)for(int k=0;k<16;k++)
                    overlap|=Geometry2D.IsPointInPolygon(player.Position+Vector2.FromAngle(k*Mathf.Tau/16)*10,prop.Footprint);
                if(player.Position.DistanceTo(target)<9)break;
            }
            bool reached=player.Position.DistanceTo(target)<15;
            GD.Print($"GUILD route {desired}: reached={reached} penetrated={overlap}");if(!reached||overlap)failures++;
        }
        GD.Print($"GUILD HALL CHECKS: {failures} failures across {samples} movement samples");GetTree().Quit(failures==0?0:1);
    }
}
