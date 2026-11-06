using DevilGate.Scripts.Data;
using DevilGate.Scripts.Systems;
using Godot;
namespace DevilGate.Scripts.Checks;

public partial class KnightVisualChecks : Node
{
    private int _failed;
    private async Task Frames(int count=20){for(int i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    private async Task Capture(string name,Control panel,int frames=20)
    {
        await Frames(frames);
        if (frames > 1) await ToSignal(GetTree().CreateTimer(.25, false),SceneTreeTimer.SignalName.Timeout);
        bool fits=GetViewport().GetVisibleRect().Encloses(panel.GetGlobalRect());
        GD.Print($"KNIGHT VISUAL {(fits?"PASS":"FAIL")}: {name} {panel.GetGlobalRect()}");if(!fits)_failed++;
        if(DisplayServer.GetName()!="headless")
        {
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng($"/tmp/devilgate-knight/{name}.png");
        }
    }
    public override async void _Ready()
    {
        Directory.CreateDirectory("/tmp/devilgate-knight");
        SaveServiceTestDirectory.UseTempDirectory();
        ComfortSettings.FastBattle=false;ComfortSettings.ReducedMotion=false;ComfortSettings.ReducedFlash=false;
        try
        {
            foreach(int width in new[]{1280,960})
            {
                GetWindow().Size=new Vector2I(width,width==1280?800:600);ComfortSettings.TextScale=width==1280?1:1.2f;
                var title=GD.Load<PackedScene>("res://Scenes/Title.tscn").Instantiate<TitleController>();AddChild(title);
                await Capture($"title-empty-{width}",title.GetNode<Control>("CenterContainer/VBox"));title.QueueFree();await Frames(3);
                var r=KnightChecks.NewKnight();r.Backpack[0]=new ItemStack(Ids.ItemSaveCrystal,2);GameSession.Instance!.SetCurrentRunForTesting(r);GameSession.Instance.SaveGame();
                title=GD.Load<PackedScene>("res://Scenes/Title.tscn").Instantiate<TitleController>();AddChild(title);await Capture($"title-continue-{width}",title.GetNode<Control>("CenterContainer/VBox"));title.QueueFree();await Frames(3);SaveService.DeleteSave();
            }
            foreach(string gender in new[]{Ids.GenderFemale})
            {
                var r=KnightChecks.NewKnight();r.CreationStage=0;GameSession.Instance!.SetCurrentRunForTesting(r);
                var mirror=GD.Load<PackedScene>("res://Scenes/CharacterCreation.tscn").Instantiate<CharacterCreationController>();mirror.RoomMode="mirror";
                var center=new CenterContainer();center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);AddChild(center);center.AddChild(mirror);
                await Capture($"knight-mirror-{gender}",mirror);center.QueueFree();await Frames(3);
            }
            var run=KnightChecks.NewKnight();GameSession.Instance!.SetCurrentRunForTesting(run);
            var training=new MagicTrainingPanel();var root=new CenterContainer();root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);AddChild(root);root.AddChild(training);await Capture("knight-training",training);root.QueueFree();await Frames(3);
            run.LearnedSpellIds=new[]{"commanding_strike","rally"};run.LocationId="woodland";WoodlandBattle.Start(run);run.Character.Hp-=12;
            var battle=new WoodlandBattlePanel{Dice=new ScriptedDieSource(1)};root=new CenterContainer();root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);AddChild(root);root.AddChild(battle);await Capture("knight-battle",battle);
            var mend=battle.FindChildren("*","Button",true,false).OfType<Button>().Single(b=>b.Text.StartsWith("Rally"));mend.EmitSignal(BaseButton.SignalName.Pressed);while(battle.IsAnimating)await Frames(1);await Capture("knight-rally",battle);
            battle.QueueFree();await Frames(3);
            run=KnightChecks.NewKnight();run.LocationId="woodland";WoodlandBattle.Start(run);GameSession.Instance.SetCurrentRunForTesting(run);
            battle=new WoodlandBattlePanel{Dice=new ScriptedDieSource(1,20,1)};root.AddChild(battle);await Frames();
            battle.FindChildren("*","Button",true,false).OfType<Button>().Single(b=>b.Text.StartsWith("Shield Wall")).EmitSignal(BaseButton.SignalName.Pressed);
            while(battle.IsAnimating)await Frames(1);
            await Capture("knight-shield-wall",battle);
            var shot=battle.FindChildren("*","Button",true,false).OfType<Button>().Single(b=>b.Text.StartsWith("Commanding Strike"));shot.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree().CreateTimer(.28,false),SceneTreeTimer.SignalName.Timeout);await Capture("knight-blade",battle,1);
            while(battle.IsAnimating)await Frames(1);
            root.QueueFree(); await Frames(5);
            GC.Collect(); GC.WaitForPendingFinalizers(); await Frames(10);
        }
        catch(Exception ex){GD.PrintErr(ex);_failed++;}
        finally{ComfortSettings.TextScale=1;SaveServiceTestDirectory.Cleanup();}
        GD.Print($"KNIGHT VISUAL CHECKS: {_failed} failures");GetTree().Quit(_failed==0?0:1);
    }
}
