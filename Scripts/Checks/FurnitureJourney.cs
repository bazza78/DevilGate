using Godot;
using DevilGate.Scripts.World;
namespace DevilGate.Scripts.Checks;
public partial class FurnitureJourney : Node
{
    private async Task Frame()=>await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
    public override async void _Ready()
    {
        GameSession.Instance!.StartTemporaryRun();
        var house=GD.Load<PackedScene>("res://Scenes/Safehouse.tscn").Instantiate<SafehouseController>();AddChild(house);
        for(int i=0;i<40;i++)await Frame();
        var player=house.GetChildren().OfType<PlayerController>().Single();
        var map=player.GetWorld2D().NavigationMap;
        int failures=0;int samples=0;
        foreach(var desired in new[]{SafehouseLayout.Iso(2,4),SafehouseLayout.Iso(9,5),SafehouseLayout.Iso(4,2),SafehouseLayout.Iso(9,7),SafehouseLayout.SpawnPoint,SafehouseLayout.ChestApproach,SafehouseLayout.PcApproach})
        {
            var target=NavigationServer2D.MapGetClosestPoint(map,desired);
            player.MoveTo(target);
            bool overlap=false;
            for(int i=0;i<500;i++)
            {
                await Frame();samples++;
                foreach(var prop in FurnitureLayout.All)
                    for(int k=0;k<16;k++)
                        overlap |= Geometry2D.IsPointInPolygon(player.Position+Vector2.FromAngle(k*Mathf.Tau/16)*10,prop.Footprint);
                if(player.Position.DistanceTo(target)<9)break;
            }
            bool reached=player.Position.DistanceTo(target)<15;
            GD.Print($"FURNITURE route to {target}: reached={reached} penetrated={overlap}");
            if(!reached || overlap)failures++;
        }
        GD.Print($"FURNITURE JOURNEY: {failures} failures across {samples} movement samples");GetTree().Quit(failures==0?0:1);
    }
}
