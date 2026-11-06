using Godot;
using DevilGate.Scripts.Data;
using DevilGate.Scripts.Systems;
using DevilGate.Scripts.World;
namespace DevilGate.Scripts.Checks;
public partial class ProgressionChecks : Node
{
    private sealed class Dice:IDieSource {private int _count;public int RollD20()=>++_count%2==1?20:1;}
    private int _failed;
    private void Check(bool ok,string message){GD.Print($"PROGRESSION {(ok?"PASS":"FAIL")}: {message}");if(!ok)_failed++;}
    private static void Press(Control panel,string text)=>panel.FindChildren("*","Button",true,false).OfType<Button>().Single(b=>b.Text==text).EmitSignal(BaseButton.SignalName.Pressed);
    private async Task Frame()=>await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    public override async void _Ready()
    {
        try
        {
            var session=GameSession.Instance!;session.StartTemporaryRun();var r=session.CurrentRun!;
            var ui=new InteractionController();AddChild(ui);
            Check(!ui.FindChildren("*","Button",true,false).OfType<Button>().Any(b=>b.Text.StartsWith("Sound:")),"persistent sound control removed");
            r.LocationId="guild";r.InductionStage=1;ui.ShowGuildContact();var panel=ui.TopPanel!;
            Press(panel,"About my Arcanist record");Check(panel.GetNode<Label>("%BodyLabel").Text.Contains("Register with Diane first"),"mage record locked until registration");
            Press(panel,"Back to our conversation");Press(panel,"Register with Diane");Press(panel,"About my Arcanist record");
            Check(panel.GetNode<Label>("%BodyLabel").Text.Contains("another world") && panel.GetNode<Label>("%BodyLabel").Text.Contains("ASHFALL"),"mage receives personal Ashfall story");
            if(DisplayServer.GetName()!="headless")
            {await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);using(var shot=GetViewport().GetTexture().GetImage())shot.SavePng("/tmp/devilgate-mage-story.png");}
            Press(panel,"Back to our conversation");Check(panel.GetNode<Label>("%BodyLabel").Text.Contains("received (key items)"),"story returns to current registration result");Press(panel,"Close");
            r.LocationId="safehouse";Check(InductionMission.Complete(r) && r.Character.Xp==25 && r.Character.Level==1,"induction gives XP without premature level-up");
            StarterTraining.Awaken(r);StarterTraining.Choose(r,new[]{"ember_bolt","arcane_ward"});
            Check(OpeningGuidance.Next(r).Target=="outside_door" && OpeningGuidance.Next(r).Text.Contains("Woodland path"),"trained player directed to first battle");
            ui.ShowMagicTraining();panel=ui.TopPanel!;Check(!panel.FindChildren("*","Button",true,false).OfType<Button>().Any(b=>b.Text.StartsWith("Practice")),"training has no practice controls");
            Press(panel,"Confirm two skills");Check(panel.FindChildren("*","Label",true,false).OfType<Label>().Any(l=>l.Text.Contains("Woodland path")),"skill confirmation explains next destination");Press(panel,"Close");
            ui.ShowDoorPanel();Check(ui.TopPanel!.GetNode<Label>("%BodyLabel").Text.Contains("Willow grove"),"exit description explains expanded route");Press(ui.TopPanel!,"Close");
            r.LocationId="woodland";WoodlandBattle.Start(r);r.Character.Hp=12;
            ui.ShowWoodlandBattle();var battle=(WoodlandBattlePanel)ui.TopPanel!;battle.Dice=new Dice();
            var energy=battle.FindChildren("BattleEnergyBar","ProgressBar",true,false).OfType<ProgressBar>().Single();
            Check(energy.Value==30,"battle energy bar starts at current mana");
            Press(battle,"Ember Bolt · 3 mana");while(battle.IsAnimating)await Frame();
            Check(energy.Value==27 && battle.FindChildren("BattleEnergyLabel","Label",true,false).OfType<Label>().Single().Text.Contains("27 / 30"),"mana count and bar update after cast");
            for(int i=0;i<2;i++){Press(battle,"Ember Bolt · 3 mana");while(battle.IsAnimating)await Frame();}
            if(DisplayServer.GetName()!="headless")
            {await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);using(var shot=GetViewport().GetTexture().GetImage())shot.SavePng("/tmp/devilgate-first-level-up.png");}
            Check(r.WoodlandBattle.Stage==2 && r.Character.Level==2 && r.Character.Xp==45,"normal first wisp reaches level two");
            Check(r.Character.Hp==35 && r.Character.Mana==32 && energy.Value==32 && energy.MaxValue==32,"level-up applies max bonuses and restores vitals");
            Check(battle.FindChildren("*","Label",true,false).OfType<Label>().Any(l=>l.Text.Contains("LEVEL 2!") && l.Text.Contains("90 XP")),"victory explains level reward and next XP target");
            Check(OpeningGuidance.Next(r).Text.Contains("Echo in the Filters"),"cleared wisp points to next contract");
            var hp=r.Character.Hp;var gold=r.CarriedGold;WoodlandBattle.Act(r,"staff");
            Check(r.Character.Xp==45 && r.Character.Hp==hp && r.CarriedGold==gold,"victory cannot repeat level rewards or gold");
            r.Backpack[0]=new ItemStack(Ids.ItemSaveCrystal,3);SaveService.DirectoryOverride="/tmp/devilgate-progression-checks";
            Check(session.SaveGame().Success && session.TryLoadGame(out _),"levelled character saves and loads with increased vitals");r=session.CurrentRun!;
            Check(r.Character.Level==2 && r.Character.Hp==35 && r.Character.Mana==32,"disk load preserves level and vitals without extra rewards");
            r.Character.Hp=34;r.Backpack[1]=new ItemStack(Ids.ItemHealthPotion,1);
            InventoryService.UseBackpackItem(r,1,Definitions.GameCatalog.Instance);Check(r.Character.Hp==35,"potions respect levelled maximum health");
            Check(Progression.Threshold(2)==45 && Progression.Threshold(3)==135 && Progression.Threshold(4)==270,"increasing cumulative thresholds");
            Check(Progression.Award(r.Character,89)==0 && r.Character.Level==2,"next level cannot trigger early");
            Check(Progression.Award(r.Character,1)==1 && r.Character.Level==3 && r.Character.Hp==40 && r.Character.Mana==34,"later level needs full increasing XP cost");
            Check(Progression.Award(r.Character,135)==1 && r.Character.Level==4,"third increment requires 135 XP");
            var old=SaveData.FromRun(r);old.Level=1;old.Xp=45;old.Hp=12;old.Mana=2;var migrated=old.ToRunState();
            Check(migrated.Character.Level==2 && migrated.Character.Xp==45 && migrated.CarriedGold==r.CarriedGold,"pre-progression saves reconcile existing XP without duplicating money");
            Press(battle,"Continue");
            // Real guild scene exit label must draw above all furniture.
            session.StartTemporaryRun();r=session.CurrentRun!;r.LocationId="guild";r.WakingIntroductionSeen=true;
            var house=GD.Load<PackedScene>("res://Scenes/Safehouse.tscn").Instantiate<SafehouseController>();AddChild(house);await Frame();
            var exit=house.GetNode<Label>("ReturnDestinationLabel");
            Check(exit.Text=="← Town square" && exit.ZIndex>house.GetChildren().OfType<FurnitureSprite>().Max(f=>f.ZIndex),"full guild exit caption renders above furnishings");
        }
        catch(Exception e){GD.PrintErr(e);_failed++;}
        GD.Print($"PROGRESSION CHECKS: {_failed} failures");GetTree().Quit(_failed==0?0:1);
    }
}
