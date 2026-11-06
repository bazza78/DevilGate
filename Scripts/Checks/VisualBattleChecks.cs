using Godot;
using DevilGate.Scripts.Systems;
namespace DevilGate.Scripts.Checks;
public partial class VisualBattleChecks : Node
{
    private sealed class Dice:IDieSource {private int _n;public int RollD20()=>++_n%2==1?20:1;}
    private int _failed;
    private void Check(bool ok,string message){GD.Print($"VISUAL BATTLE {(ok?"PASS":"FAIL")}: {message}");if(!ok)_failed++;}
    private async Task Frames(int n){for(int i=0;i<n;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    public override async void _Ready()
    {
        GameSession.Instance!.StartTemporaryRun();var r=GameSession.Instance.CurrentRun!;
        r.LocationId="woodland";r.MagicAwakened=true;r.InductionStage=3;r.LearnedSpellIds=new[]{"ember_bolt","arcane_ward"};WoodlandBattle.Start(r);
        var panel=new WoodlandBattlePanel {Dice=new Dice()};AddChild(panel);
        var fire=panel.FindChildren("*","Button",true,false).OfType<Button>().Single(b=>b.Text.StartsWith("Ember Bolt"));
        var bars=panel.FindChildren("*","ProgressBar",true,false).OfType<ProgressBar>().ToArray();
        Check(bars.Count(b=>b.Name=="PlayerHealthBar"||b.Name=="EnemyHealthBar")==2 && bars.Any(b=>b.Name=="BattleEnergyBar"),"both health bars and battle energy bar present");
        fire.EmitSignal(BaseButton.SignalName.Pressed);fire.EmitSignal(BaseButton.SignalName.Pressed);
        Check(panel.IsAnimating && r.Character.Mana==27,"repeat clicks cannot take another turn while animating");
        Check(panel.FindChildren("*","Button",true,false).OfType<Button>().Where(b=>b.Visible && b.Text!="Show roll log").All(b=>b.Disabled),"actions and save/retreat lock during animation");
        while(panel.IsAnimating)await Frames(1);
        Check(bars.Single(b=>b.Name=="EnemyHealthBar").Value==10,"enemy health bar reaches resolved damage");
        Check(bars.Single(b=>b.Name=="PlayerHealthBar").Value==30,"missed counterattack leaves player bar unchanged");
        for(int i=0;i<2;i++){fire.EmitSignal(BaseButton.SignalName.Pressed);while(panel.IsAnimating)await Frames(1);}
        Check(r.WoodlandBattle.Stage==2 && bars.Single(b=>b.Name=="EnemyHealthBar").Value==0,"victory animation ends at zero health");
        Check(r.Character.Xp==20 && r.CarriedGold==5,"animation does not duplicate victory rewards");
        var arena=panel.FindChildren("*","BattleArena",true,false).OfType<BattleArena>().FirstOrDefault() ?? panel.GetChildren().OfType<VBoxContainer>().Single().GetChildren().OfType<BattleArena>().Single();
        Check(arena.EnemyDefeated,"enemy disappearance follows victory");
        GD.Print($"VISUAL BATTLE CHECKS: {_failed} failures");GetTree().Quit(_failed==0?0:1);
    }
}
