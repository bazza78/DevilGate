using DevilGate.Scripts.Data;
using DevilGate.Scripts.Definitions;
using DevilGate.Scripts.Systems;
using DevilGate.Scripts.World;
using Godot;
namespace DevilGate.Scripts.Checks;

/// <summary>Map rules, modal behaviour, scene replacement and optional real-renderer screenshot capture.</summary>
public partial class MapChecks : Node
{
    private int _failed;
    private void Check(bool ok,string text){GD.Print($"MAP {(ok?"PASS":"FAIL")}: {text}");if(!ok)_failed++;}
    private async Task Wait(double seconds=.3)=>await ToSignal(GetTree().CreateTimer(seconds,false),SceneTreeTimer.SignalName.Timeout);
    private async Task Capture(string name)
    {
        if(DisplayServer.GetName()=="headless")return;
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        var folder=OS.GetCmdlineUserArgs().Contains("--public-shots")?ProjectSettings.GlobalizePath("res://.github/images"):"/tmp/devilgate-review";
        Directory.CreateDirectory(folder);GetViewport().GetTexture().GetImage().SavePng(Path.Combine(folder,name+".png"));
    }
    public override async void _Ready()
    {
        SaveServiceTestDirectory.UseTempDirectory();
        try
        {
            ComfortSettings.TextScale=1;ComfortSettings.ReducedMotion=false;ComfortSettings.ReducedFlash=false;ComfortSettings.FastBattle=false;
            GetWindow().Size=new Vector2I(1280,800);
            var main=GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<MainController>();AddChild(main);await Wait();await Capture("title");
            var session=GameSession.Instance!;session.StartTemporaryRun();var run=session.CurrentRun!;run.Character.Name="Selene";run.Character.GenderId=Ids.GenderFemale;run.WakingIntroductionSeen=true;
            run.InductionStage=2;InductionMission.Complete(run);StarterTraining.Awaken(run);StarterTraining.Choose(run,new[]{"ember_bolt","arcane_ward"});
            InventoryService.TakeAll(run,GameCatalog.Instance);
            for(int i=0;i<12;i++)if(run.Backpack[i] is {} item&&GameCatalog.Instance.GetItem(item.ItemId).Category is ItemCategory.Weapon or ItemCategory.Armour)InventoryService.EquipFromBackpack(run,i,GameCatalog.Instance);
            main.ShowSafehouse();await Wait(2.1);
            SafehouseController House()=>main.GetChildren().OfType<SafehouseController>().Last();
            InteractionController UI()=>House().GetChildren().OfType<InteractionController>().Single();
            var house=House();var hud=house.GetChildren().OfType<HUDController>().Single();hud.BackpackCards.First(c=>c.Stack?.ItemId==Ids.ItemHealthPotion).Select();
            Check(RouteMiniMap.Landmarks("safehouse",run).Any(p=>p.Id=="starter_chest")&&!RouteMiniMap.Landmarks("safehouse",run).Any(p=>p.Id=="route_forward"),"home map shows apartment landmarks, not an outdoor path");
            foreach(var area in WorldRoute.Areas)
            {
                var rect=new Rect2(8,69,179,76);
                Check(RouteMiniMap.Landmarks(area,run).All(p=>rect.HasPoint(RouteMiniMap.Project(p.Point,area,rect))),area+" landmarks fit compact map");
            }
            Check(RouteMiniMap.Landmarks("guild",run).Any(p=>p.Id=="guild_contact")&&RouteMiniMap.Landmarks("grove",run).Any(p=>p.Id=="filter_relay"),"guild and grove have distinct landmarks");
            Check(GetViewport().GetVisibleRect().Encloses(hud.GetNode<Button>("%SaveButton").GetGlobalRect())&&GetViewport().GetVisibleRect().Encloses(hud.GetNode<Button>("%QuitToTitleButton").GetGlobalRect()),"default sidebar keeps Save and Quit visible");
            await Capture("home");
            house._UnhandledInput(new InputEventKey{Keycode=Key.M,Pressed=true});await Wait();
            Check(UI().TopPanel is RouteMapPanel,"M opens expanded map");
            var panel=(RouteMapPanel)UI().TopPanel!;var location=run.LocationId;var position=new Vector2(run.PositionX,run.PositionY);var stateVersion=session.StateVersion;
            foreach(var size in new[]{new Vector2I(1280,800),new Vector2I(960,600)})
            {
                GetWindow().Size=size;ComfortSettings.TextScale=size.X==960?1.2f:1;await Wait();
                Check(GetViewport().GetVisibleRect().Encloses(panel.GetGlobalRect()),"expanded map fits "+size+" at "+ComfortSettings.TextScale+" text scale");
            }
            foreach(var button in panel.FindChildren("MapArea_*","Button",true,false).OfType<Button>())button.EmitSignal(BaseButton.SignalName.Pressed);
            Check(run.LocationId==location&&new Vector2(run.PositionX,run.PositionY)==position&&session.StateVersion==stateVersion,"previewing every area never teleports, grants progress or dirties save");
            panel.FindChildren("MapArea_safehouse","Button",true,false).OfType<Button>().Single().EmitSignal(BaseButton.SignalName.Pressed);
            GetWindow().Size=new Vector2I(1280,800);ComfortSettings.TextScale=1;await Wait();await Capture("map");
            house._UnhandledInput(new InputEventKey{Keycode=Key.M,Pressed=true});Check(!UI().HasOpenPanel,"M closes map");
            var mini=UI().GetChildren().OfType<RouteMiniMap>().Single();mini._GuiInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=true});Check(UI().TopPanel is RouteMapPanel,"clicking minimap opens map");UI().HandleEscape();
            // A replaced screen must never flush its old position over the state being loaded.
            var old=house.GetChildren().OfType<PlayerController>().Single();old.Stop();old.Position=new Vector2(150,350);
            var restored=run.Clone();restored.PositionX=SafehouseLayout.SpawnPoint.X;restored.PositionY=SafehouseLayout.SpawnPoint.Y;session.SetCurrentRunForTesting(restored);main.ShowSafehouse();await Wait(.1);
            Check(!GodotObject.IsInstanceValid(old)||!old.TrackPosition,"replaced player stops tracking position");
            Check(Mathf.Abs(restored.PositionX-SafehouseLayout.SpawnPoint.X)<1&&Mathf.Abs(restored.PositionY-SafehouseLayout.SpawnPoint.Y)<1,"old scene cannot overwrite restored position");
            restored.LocationId="woodland";restored.PositionX=RiftWisp.EncounterPoint.X;restored.PositionY=RiftWisp.EncounterPoint.Y;main.ShowSafehouse();await Wait(2.1);
            if(UI().TopPanel is not WoodlandBattlePanel)UI().ShowWoodlandBattle();
            var battle=(WoodlandBattlePanel)UI().TopPanel!;battle.Dice=new ScriptedDieSource(20,1);
            battle.FindChildren("DismissHintButton","Button",true,false).OfType<Button>().Single().EmitSignal(BaseButton.SignalName.Pressed);
            battle.FindChildren("*","Button",true,false).OfType<Button>().Single(b=>b.Text.StartsWith("Arcane Ward")).EmitSignal(BaseButton.SignalName.Pressed);
            while(battle.IsAnimating)await Wait(.1);
            await Wait(.8);await Capture("battle");
            main.QueueFree();await Wait();
            GC.Collect();GC.WaitForPendingFinalizers();await Wait();
        }
        catch(Exception ex){GD.PrintErr(ex);_failed++;}
        finally{SaveServiceTestDirectory.Cleanup();}
        GD.Print($"MAP CHECKS: {_failed} failures");GetTree().Quit(_failed==0?0:1);
    }
}
