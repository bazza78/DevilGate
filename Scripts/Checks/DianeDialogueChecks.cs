using Godot;
namespace DevilGate.Scripts.Checks;
public partial class DianeDialogueChecks : Node
{
    public override void _Ready()
    {
        var failed=0;
        void Check(bool ok,string text){GD.Print($"DIANE {(ok?"PASS":"FAIL")}: {text}");if(!ok)failed++;}
        void Press(Control panel,string text)=>panel.FindChildren("*","Button",true,false).OfType<Button>().Single(b=>b.Text==text).EmitSignal(BaseButton.SignalName.Pressed);
        GameSession.Instance!.StartTemporaryRun();var run=GameSession.Instance.CurrentRun!;run.LocationId="guild";run.InductionStage=1;
        var ui=new InteractionController();AddChild(ui);ui.ShowGuildContact();var panel=ui.TopPanel!;
        Check(panel.FindChildren("SpeakerPortrait","TextureRect",true,false).OfType<TextureRect>().Single().Texture?.ResourcePath=="res://Assets/Portraits/NPCs/diane.png","supplied portrait present");
        Press(panel,"Why does everyone wear robes?");Check(panel.GetNode<Label>("%BodyLabel").Text.Contains("filtration seals"),"robe lore accessible");
        Press(panel,"Register with Diane");Check(run.InductionStage==2,"registration works while reading lore");
        Check(!panel.FindChildren("*","Button",true,false).OfType<Button>().Single(b=>b.Text=="Register with Diane").Visible,"registration button hidden after success");
        Press(panel,"Why does everyone wear robes?");Press(panel,"Back to our conversation");
        Check(panel.GetNode<Label>("%BodyLabel").Text.Contains("received (key items)"),"return from lore preserves registration result");
        Press(panel,"Close");Check(!ui.HasOpenPanel,"portrait conversation closes normally");
        ui.ShowGuildContact();panel=ui.TopPanel!;
        Check(!panel.FindChildren("*","Button",true,false).OfType<Button>().Any(b=>b.Text=="Register with Diane"),"reopening conversation cannot register again");
        Check(panel.FindChildren("SpeakerPortrait","TextureRect",true,false).Count==1,"portrait appears on repeat conversations");
        GD.Print($"DIANE DIALOGUE CHECKS: {failed} failures");GetTree().Quit(failed==0?0:1);
    }
}
