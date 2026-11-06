using Godot;
using DevilGate.Scripts.Data;
using DevilGate.Scripts.Systems;
using DevilGate.Scripts.World;
namespace DevilGate.Scripts.Checks;
public partial class MissionJourney : Node
{
    private int _fail;
    private void Check(bool ok,string text) {GD.Print($"MISSION {(ok?"PASS":"FAIL")}: {text}");if(!ok)_fail++;}
    private async Task Frames(int count) {for(int i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);}
    private static void Press(Control panel,string text)
    {
        foreach(var child in panel.FindChildren("*","Button",true,false))
            if(child is Button b && b.Text==text) {b.EmitSignal(BaseButton.SignalName.Pressed);return;}
        throw new Exception("Button not found: "+text);
    }
    public override async void _Ready()
    {
        try
        {
            var main=GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<MainController>();AddChild(main);
            var session=GameSession.Instance!;session.StartTemporaryRun(); main.ShowSafehouse();await Frames(10);
            SafehouseController House()=>main.GetChildren().OfType<SafehouseController>().Last();
            InteractionController UI()=>House().GetChildren().OfType<InteractionController>().Single();
            UI().ShowTerminalPanel();
            Check(UI().TopPanel is ModalPanelController,"terminal locked before access drive");
            Check(session.CurrentRun!.InductionStage==0,"locked terminal does not accept mission");
            UI().HandleEscape();UI().ShowDoorPanel();Press(UI().TopPanel!,"Go Outside");await Frames(15);
            Check(session.CurrentRun!.InductionStage==1,"leaving starts contact objective");
            Check(session.CurrentRun!.LocationId=="woodland","door loads Woodland path directly");
            Check(!InductionMission.MeetContact(session.CurrentRun!),"cannot register on woodland path");
            Check(!WorldRoute.Adjacent("safehouse","guild"),"route cannot skip to guild");
            var run=session.CurrentRun!;
            SaveService.DirectoryOverride="/tmp/devilgate-mission-check";
            run.Backpack[0]=new ItemStack(Ids.ItemSaveCrystal,3);
            Check(session.SaveGame().Success,"save outside succeeds");
            Check(session.TryLoadGame(out _),"load outside succeeds");
            Check(session.CurrentRun!.LocationId=="woodland" && session.CurrentRun.InductionStage==1,"outside location and mission survive disk roundtrip");
            main.ShowSafehouse();await Frames(15);
            foreach(var destination in new[]{"grove","town","guild"})
            {
                UI().HandleWorldClick(CourtyardRenderer.ContactApproach+new Vector2(0,-20));
                for(int i=0;i<600 && !UI().HasOpenPanel;i++)await Frames(1);
                Press(UI().TopPanel!,"Travel");await Frames(40);
                Check(session.CurrentRun!.LocationId==destination,"walk and travel to "+destination);
                Check(session.SaveGame().Success && session.TryLoadGame(out _) && session.CurrentRun!.LocationId==destination,"save/load in "+destination);
                // Replenish test crystals only; production game still spends carried crystals.
                session.CurrentRun!.Backpack[0]=new ItemStack(Ids.ItemSaveCrystal,3);
                main.ShowSafehouse();await Frames(40);
            }
            UI().HandleWorldClick(GuildHallLayout.Contracts.Anchor+new Vector2(0,-65));
            for(int i=0;i<600 && !UI().HasOpenPanel;i++)await Frames(1);
            Check(UI().HasOpenPanel,"contract board reachable with furnished hall");
            UI().HandleEscape();
            UI().HandleWorldClick(CourtyardRenderer.Contact+new Vector2(0,-28));
            for(int i=0;i<600 && !UI().HasOpenPanel;i++)await Frames(1);
            Check(UI().HasOpenPanel,"walking to Diane opens dialogue");
            var registrationPanel=UI().TopPanel!;
            var portrait=registrationPanel.FindChildren("SpeakerPortrait","TextureRect",true,false).OfType<TextureRect>().Single();
            Check(portrait.Texture?.ResourcePath=="res://Assets/Portraits/NPCs/diane.png","Diane dialogue shows supplied portrait");
            Press(registrationPanel,"Why does everyone wear robes?");
            Check(registrationPanel.GetNode<Label>("%BodyLabel").Text.Contains("filtration seals"),"robe conversation explains polluted air");
            Press(registrationPanel,"Back to our conversation");
            var registerButton=registrationPanel.FindChildren("*","Button",true,false).OfType<Button>().Single(b=>b.Text=="Register with Diane");
            Press(registrationPanel,"Register with Diane");
            Check(!registerButton.Visible,"registration button disappears immediately");
            Check(session.CurrentRun!.InductionStage==2,"Diane grants access drive");
            Check(session.CurrentRun!.HasGuildAccessDrive && session.CurrentRun.HasGuildCard,"guild card and drive granted together");
            Check(SaveData.FromRun(session.CurrentRun!).ToRunState().HasGuildAccessDrive,"drive survives save roundtrip");
            Check(!InductionMission.MeetContact(session.CurrentRun!),"contact cannot advance twice");
            UI().HandleEscape();
            foreach(var destination in new[]{"town","grove","woodland","safehouse"})
            {
                UI().HandleWorldClick(CourtyardRenderer.Door+new Vector2(0,-20));
                for(int i=0;i<600 && !UI().HasOpenPanel;i++)await Frames(1);
                Press(UI().TopPanel!,"Travel");await Frames(40);
                Check(session.CurrentRun!.LocationId==destination,"return to "+destination);
            }
            Check(session.CurrentRun!.LocationId=="safehouse","door returns to apartment");
            var gold=session.CurrentRun.CarriedGold;var xp=session.CurrentRun.Character.Xp;
            UI().ShowTerminalPanel();Press(UI().TopPanel!,"Activate account & claim allowance");
            Check(session.CurrentRun.InductionStage==3,"terminal completes mission");
            Check(session.CurrentRun.CarriedGold==gold+15 && session.CurrentRun.Character.Xp==xp+25,"exact reward granted");
            Check(!InductionMission.Complete(session.CurrentRun),"reward cannot repeat");
            Check(SaveData.FromRun(session.CurrentRun).ToRunState().InductionStage==3,"completion persists");
            var unfinished=session.CurrentRun.Clone();unfinished.CreationStage=0;unfinished.InductionStage=0;
            Check(!InductionMission.Accept(unfinished),"unfinished characters cannot accept mission");
        }
        catch(Exception ex) {GD.PrintErr(ex);_fail++;}
        GD.Print($"MISSION JOURNEY: {_fail} failures");GetTree().Quit(_fail==0?0:1);
    }
}
