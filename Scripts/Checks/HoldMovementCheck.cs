using Godot;
using DevilGate.Scripts.World;
namespace DevilGate.Scripts.Checks;
public partial class HoldMovementCheck : Node
{
    private async Task Frames(int n){for(int i=0;i<n;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);}
    public override async void _Ready()
    {
        GameSession.Instance!.StartTemporaryRun();
        var house=GD.Load<PackedScene>("res://Scenes/Safehouse.tscn").Instantiate<SafehouseController>();AddChild(house);await Frames(40);
        var player=house.GetChildren().OfType<PlayerController>().Single();
        var ui=house.GetChildren().OfType<InteractionController>().Single();
        ui.HandleEscape();await Frames(2);
        player.Position=SafehouseLayout.Iso(8,5);await Frames(2);
        var start=player.Position;
        Vector2 Screen(Vector2 p)=>GetViewport().GetScreenTransform()*p;
        Input.ParseInputEvent(new InputEventMouseButton {ButtonIndex=MouseButton.Left,Pressed=true,Position=Screen(start),GlobalPosition=Screen(start)});
        await Frames(15);
        var destination=SafehouseLayout.Iso(5,6);
        Input.ParseInputEvent(new InputEventMouseMotion {Position=Screen(destination),GlobalPosition=Screen(destination),Relative=destination-start,ButtonMask=MouseButtonMask.Left});
        await Frames(65);
        bool moved=player.Position.DistanceTo(start)>30;
        GD.Print($"HOLD CHECK: moved while held={moved} distance={player.Position.DistanceTo(start):F1}");
        player.Stop();var sidebarStop=player.Position;
        Input.ParseInputEvent(new InputEventMouseMotion {Position=Screen(new Vector2(1000,400)),GlobalPosition=Screen(new Vector2(1000,400)),ButtonMask=MouseButtonMask.Left});
        await Frames(20);
        bool sidebarBlocked=player.Position.DistanceTo(sidebarStop)<1;
        GD.Print($"HOLD CHECK: sidebar blocks drag={sidebarBlocked}");
        ui.ShowChestPanel();var stopped=player.Position;
        Input.ParseInputEvent(new InputEventMouseMotion {Position=Screen(new Vector2(200,300)),GlobalPosition=Screen(new Vector2(200,300)),ButtonMask=MouseButtonMask.Left});
        await Frames(25);
        bool blocked=player.Position.DistanceTo(stopped)<1;
        GD.Print($"HOLD CHECK: modal blocks drag={blocked}");
        Input.ParseInputEvent(new InputEventMouseButton {ButtonIndex=MouseButton.Left,Pressed=false,Position=new Vector2(200,300)});
        GetTree().Quit(moved && blocked && sidebarBlocked ? 0:1);
    }
}
