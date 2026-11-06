using Godot;
using DevilGate.Scripts.Systems;
namespace DevilGate.Scripts.Checks;
public partial class VisualBattleChecks : Node
{
    private sealed class Dice:IDieSource {private int _n;public int RollD20()=>++_n%2==1?20:1;}
    private sealed class LowDie:IDieSource {public int RollD20()=>1;}
    private int _failed;
    private void Check(bool ok,string message){GD.Print($"VISUAL BATTLE {(ok?"PASS":"FAIL")}: {message}");if(!ok)_failed++;}
    private async Task Frames(int n){for(int i=0;i<n;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    public override async void _Ready()
    {
        GameSession.Instance!.StartTemporaryRun();var r=GameSession.Instance.CurrentRun!;
        r.LocationId="woodland";r.MagicAwakened=true;r.InductionStage=3;r.LearnedSpellIds=new[]{"ember_bolt","arcane_ward"};WoodlandBattle.Start(r);
        var panel=new WoodlandBattlePanel {Dice=new Dice()};AddChild(panel);
        var turnLabel=panel.FindChildren("BattleTurnLabel","Label",true,false).OfType<Label>().Single();
        var unavailableLabel=panel.FindChildren("UnavailableActionsLabel","Label",true,false).OfType<Label>().Single();
        var hintBar=panel.FindChildren("FirstCombatHintBar","PanelContainer",true,false).OfType<PanelContainer>().Single();
        var dismissHint=panel.FindChildren("DismissHintButton","Button",true,false).OfType<Button>().Single();
        Check(turnLabel.Text=="Turn: Your action","turn indicator starts on player action");
        Check(panel.ActionOrder.SequenceEqual(new[]{"staff","guard","ember_bolt","arcane_ward","potion"}),"actions follow Attack/Guard -> Skills -> Items order");
        Check(hintBar.Visible && !r.BattleHintDismissed,"first-combat hint visible on first encounter");
        dismissHint.EmitSignal(BaseButton.SignalName.Pressed);
        Check(!hintBar.Visible && r.BattleHintDismissed && Data.SaveData.FromRun(r).ToRunState().BattleHintDismissed,"dismissed first-combat hint hides and persists in save data");
        Check(unavailableLabel.Visible && unavailableLabel.Text.Contains("Health potion: no potion"),"unavailable action reason shown without clicking disabled button");
        r.Backpack[0]=new Data.ItemStack(Data.Ids.ItemHealthPotion,1);GameSession.Instance.EmitChanged();
        Check(unavailableLabel.Text.Contains("Health potion: health already full"),"potion at full health explains health already full");
        r.Character.Mana=0;GameSession.Instance.EmitChanged();
        Check(unavailableLabel.Text.Contains("insufficient mana"),"low energy explains insufficient mana");
        r.Backpack[1]=new Data.ItemStack(Data.Ids.ItemManaPotion,1);r.Character.Mana=20;GameSession.Instance.EmitChanged();
        var itemsBtn=panel.FindChildren("BattleItemsButton","Button",true,false).OfType<Button>().Single();
        var chooser=panel.FindChildren("BattleItemChooser","PanelContainer",true,false).OfType<PanelContainer>().Single();
        var hpPotionBtn=panel.FindChildren("UseHealthPotionButton","Button",true,false).OfType<Button>().Single();
        var mpPotionBtn=panel.FindChildren("UseManaPotionButton","Button",true,false).OfType<Button>().Single();
        var closeItemsBtn=panel.FindChildren("CloseItemChooserButton","Button",true,false).OfType<Button>().Single();
        Check(!itemsBtn.Disabled && !chooser.Visible,"items button enabled when a carried potion can be used");
        itemsBtn.EmitSignal(BaseButton.SignalName.Pressed);
        Check(chooser.Visible && hpPotionBtn.Disabled && !mpPotionBtn.Disabled,"compact item chooser opens and enables only usable potion");
        closeItemsBtn.EmitSignal(BaseButton.SignalName.Pressed);
        Check(!chooser.Visible && r.Character.Mana==20,"cancelling item chooser does not spend turn or potion");
        itemsBtn.EmitSignal(BaseButton.SignalName.Pressed);
        var defaultDice=panel.Dice;panel.Dice=new LowDie();
        mpPotionBtn.EmitSignal(BaseButton.SignalName.Pressed);
        while(panel.IsAnimating)await Frames(1);
        panel.Dice=defaultDice;
        Check(!chooser.Visible && r.Character.Mana==30 && r.Backpack[1]==null,"using mana potion from chooser restores mana and consumes item");
        r.Backpack[0]=null;GameSession.Instance.EmitChanged();
        var ward=panel.FindChildren("*","Button",true,false).OfType<Button>().Single(b=>b.Text.StartsWith("Arcane Ward"));
        ward.EmitSignal(BaseButton.SignalName.Pressed);
        while(panel.IsAnimating)await Frames(1);
        var chips=panel.FindChildren("BattleStatusChips","HBoxContainer",true,false).OfType<HBoxContainer>().Single();
        Check(chips.Visible && chips.GetChildCount()==1 && chips.FindChildren("*","Label",true,false).OfType<Label>().Any(l=>l.Text.Contains("◇ Ward +4 DEF") && l.Text.Contains("2 enemy responses")),"active ward renders icon+label+duration status chip");
        var fire=panel.FindChildren("*","Button",true,false).OfType<Button>().Single(b=>b.Text.StartsWith("Ember Bolt"));
        var bars=panel.FindChildren("*","ProgressBar",true,false).OfType<ProgressBar>().ToArray();
        Check(bars.Count(b=>b.Name=="PlayerHealthBar"||b.Name=="EnemyHealthBar")==2 && bars.Any(b=>b.Name=="BattleEnergyBar"),"both health bars and battle energy bar present");
        fire.EmitSignal(BaseButton.SignalName.Pressed);fire.EmitSignal(BaseButton.SignalName.Pressed);
        Check(panel.IsAnimating && r.Character.Mana==24,"repeat clicks cannot take another turn while animating");
        Check(unavailableLabel.Text.Contains("turn resolving") && turnLabel.Text.Contains("Turn:"),"turn resolving state explained during animation");
        Check(panel.FindChildren("*","Button",true,false).OfType<Button>().Where(b=>b.Visible && b.Text!="Show roll log").All(b=>b.Disabled),"actions and save/retreat lock during animation");
        while(panel.IsAnimating)await Frames(1);
        Check(bars.Single(b=>b.Name=="EnemyHealthBar").Value==10,"enemy health bar reaches resolved damage");
        Check(bars.Single(b=>b.Name=="PlayerHealthBar").Value==30,"missed counterattack leaves player bar unchanged");
        for(int i=0;i<2;i++){fire.EmitSignal(BaseButton.SignalName.Pressed);while(panel.IsAnimating)await Frames(1);}
        Check(r.WoodlandBattle.Stage==2 && bars.Single(b=>b.Name=="EnemyHealthBar").Value==0 && turnLabel.Text=="Turn: Victory","victory animation ends at zero health and updates turn label");
        Check(r.Character.Xp==20 && r.CarriedGold==5,"animation does not duplicate victory rewards");
        var arena=panel.FindChildren("*","BattleArena",true,false).OfType<BattleArena>().FirstOrDefault() ?? panel.GetChildren().OfType<VBoxContainer>().Single().GetChildren().OfType<BattleArena>().Single();
        Check(arena.EnemyDefeated,"enemy disappearance follows victory");
        GD.Print($"VISUAL BATTLE CHECKS: {_failed} failures");GetTree().Quit(_failed==0?0:1);
    }
}
