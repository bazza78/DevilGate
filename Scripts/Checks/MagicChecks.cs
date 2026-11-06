using Godot;
using DevilGate.Scripts.Data;
using DevilGate.Scripts.Systems;
namespace DevilGate.Scripts.Checks;
public partial class MagicChecks : Node
{
    private sealed class FixedDie(int result):IDieSource {public int RollD20()=>result;}
    private int _fail;
    private void Check(bool ok,string text){GD.Print($"MAGIC {(ok?"PASS":"FAIL")}: {text}");if(!ok)_fail++;}
    public override void _Ready()
    {
        var session=GameSession.Instance!;session.StartTemporaryRun();var run=session.CurrentRun!;
        Check(!MageTraining.Awaken(run),"guild induction required");
        run.InductionStage=3;
        Check(MageTraining.Awaken(run),"attunement unlocks selection");
        Check(!MageTraining.Awaken(run),"attunement cannot repeat");
        Check(!MageTraining.Choose(run,new[]{"ember_bolt"}),"must choose two");
        Check(!MageTraining.Choose(run,new[]{"ember_bolt","ember_bolt"}),"duplicates rejected");
        Check(!MageTraining.Choose(run,new[]{"ember_bolt","bogus"}),"unknown spell rejected");
        Check(MageTraining.Choose(run,new[]{"ember_bolt","shadow_veil"}),"mixing schools allowed");
        Check(run.LearnedSpellIds.Length==2,"exactly two stored");
        var clone=run.Clone();clone.LearnedSpellIds[0]="mage_light";
        Check(run.LearnedSpellIds[0]=="ember_bolt","clone does not share spell array");
        var restored=SaveData.FromRun(run).ToRunState();
        Check(restored.MagicAwakened && restored.LearnedSpellIds.SequenceEqual(run.LearnedSpellIds),"saved skills roundtrip");
        Check(MageTraining.Validate(false,Array.Empty<string>())==null,"old saves without magic valid");
        Check(MageTraining.Validate(false,new[]{"ember_bolt","arcane_ward"})!=null,"invalid unattuned saves rejected");
        Check(MageTraining.Choose(run,new[]{"mage_light","arcane_ward"}),"free prototype retraining works");
        var panel=new MagicTrainingPanel();AddChild(panel);
        Check(!panel.FindChildren("*","Button",true,false).OfType<Button>().Any(b=>b.Text.StartsWith("Practice")),"practice feature removed");
        var boxes=panel.FindChildren("*","CheckBox",true,false).OfType<CheckBox>().ToArray();
        Check(boxes.Length==7 && boxes.Count(b=>b.ButtonPressed)==2,"panel displays seven skills and selected pair");
        Check(boxes.Count(b=>b.Disabled)==5,"third selection disabled");
        var chosen=boxes.First(b=>b.ButtonPressed);
        var unchosen=boxes.First(b=>!b.ButtonPressed);
        Check(((StyleBoxFlat)chosen.GetThemeStylebox("normal")).BgColor != ((StyleBoxFlat)unchosen.GetThemeStylebox("hover")).BgColor,"selected skills remain distinct from hover");
        SaveService.DirectoryOverride="/tmp/devilgate-magic-check";
        run.Backpack[0]=new ItemStack(Ids.ItemSaveCrystal,3);
        Check(session.SaveGame().Success,"crystal save with learned spells succeeds");
        Check(session.TryLoadGame(out _) && session.CurrentRun!.MagicAwakened && session.CurrentRun.LearnedSpellIds.SequenceEqual(new[]{"mage_light","arcane_ward"}),"disk load restores learned spells and attunement");
        GD.Print($"MAGIC CHECKS: {_fail} failures");GetTree().Quit(_fail==0?0:1);
    }
}
