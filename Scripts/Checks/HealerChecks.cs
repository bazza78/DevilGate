using DevilGate.Scripts.Data;
using DevilGate.Scripts.Definitions;
using DevilGate.Scripts.Systems;
using Godot;
namespace DevilGate.Scripts.Checks;

public partial class HealerChecks : Node
{
    private int _failed;
    private void Check(bool ok,string text){GD.Print($"HEALER {(ok?"PASS":"FAIL")}: {text}");if(!ok)_failed++;}
    public static RunState NewHealer(string gender=Ids.GenderFemale)
    {
        var arch=GameCatalog.Instance.GetArchetype(Ids.ArchetypeHealer);
        var r=RunFactory.CreateNewRun(new CharacterCreationResult {Name="Sol",ArchetypeId=arch.Id,GenderId=gender,Attributes=arch.CreateSuggestedAttributes()},GameCatalog.Instance);
        r.InductionStage=3;r.MagicAwakened=true;r.LearnedSpellIds=new[]{"radiant_bolt","mend"};return r;
    }
    private static RunState Battle(params string[] pair)
    {
        var r=NewHealer();r.LearnedSpellIds=pair;r.LocationId="woodland";WoodlandBattle.Start(r);return r;
    }
    public override async void _Ready()
    {
        SaveServiceTestDirectory.UseTempDirectory();
        try
        {
            foreach(string gender in new[]{Ids.GenderMale,Ids.GenderFemale})
            {
                var r=NewHealer(gender);var session=GameSession.Instance!;session.SetCurrentRunForTesting(r);r.CreationStage=0;
                var mirror=GD.Load<PackedScene>("res://Scenes/CharacterCreation.tscn").Instantiate<CharacterCreationController>();mirror.RoomMode="mirror";AddChild(mirror);
                Check(TestingRoster.AllowsArchetype(Ids.ArchetypeHealer)&&TestingRoster.AllowsGender(gender,Ids.ArchetypeHealer),$"{gender} healer available");
                Check(mirror.GetNode<TextureRect>("%PortraitTexture").Texture?.ResourcePath==$"res://Assets/Portraits/healer_{gender}.png",$"{gender} supplied portrait shown");
                mirror.GetNode<LineEdit>("%NameEdit").Text="Sol";mirror.GetNode<Button>("%BeginButton").EmitSignal(BaseButton.SignalName.Pressed);mirror.QueueFree();
                var rack=GD.Load<PackedScene>("res://Scenes/CharacterCreation.tscn").Instantiate<CharacterCreationController>();rack.RoomMode="rack";AddChild(rack);rack.GetNode<Button>("%BeginButton").EmitSignal(BaseButton.SignalName.Pressed);
                Check(r.CreationStage==2&&r.Backpack.CountItem(Ids.ItemMedicWand)==1,$"{gender} healer receives correct kit");rack.QueueFree();
                r.MagicAwakened=false;r.LearnedSpellIds=Array.Empty<string>();Check(StarterTraining.Awaken(r)&&StarterTraining.Choose(r,new[]{"radiant_bolt","mend"}),$"{gender} healer attunes and equips two skills");
                Check(!StarterTraining.Choose(r,new[]{"ember_bolt","mend"})&&!StarterTraining.Choose(r,new[]{"mend","mend"}),"cross-class and duplicate selections rejected");
            }
            var run=Battle("radiant_bolt","mend");var feedback=new BattleTurnFeedback();int energy=run.Character.Mana;
            WoodlandBattle.Act(run,"radiant_bolt",new ScriptedDieSource(6,1),feedback);
            Check(feedback.PlayerHit==true&&run.WoodlandBattle.EnemyHp==12&&run.Character.Mana==energy-3,"Radiant Bolt uses trained Medicine/Intellect (6+2+2 meets DC10), deals 6 and spends 3 mana");
            feedback=new();WoodlandBattle.Act(run,"radiant_bolt",new ScriptedDieSource(5,1),feedback);
            Check(feedback.PlayerHit==false&&run.WoodlandBattle.EnemyHp==12&&run.Character.Mana==energy-6,"miss spends mana and deals no damage");
            run.Character.Hp=10;feedback=new();WoodlandBattle.Act(run,"mend",new ScriptedDieSource(20),feedback);
            Check(feedback.PlayerHealing==10&&feedback.PlayerRoll==null&&feedback.EnemyDamageTaken==4&&run.Character.Hp==16,"Mend automatically restores 10 before one enemy response");
            run.Character.Hp=CharacterRules.MaxHealth(run.Character)-2;feedback=new();WoodlandBattle.Act(run,"mend",new ScriptedDieSource(1),feedback);
            Check(feedback.PlayerHealing==2&&run.Character.Hp==CharacterRules.MaxHealth(run.Character),"Mend caps at maximum HP");
            energy=run.Character.Mana;feedback=new();WoodlandBattle.Act(run,"mend",new ScriptedDieSource(20),feedback);
            Check(!feedback.ActionResolved&&run.Character.Mana==energy&&WoodlandBattlePanel.UnavailableReason(run,run.WoodlandBattle,"mend",false)=="health already full","Mend at full health spends neither turn nor mana and explains why");
            run=Battle("sanctuary","pacifying_light");feedback=new();WoodlandBattle.Act(run,"sanctuary",new ScriptedDieSource(20),feedback);
            Check(feedback.EnemyDamageBlocked==2&&feedback.EnemyDamageTaken==2&&run.WoodlandBattle.IronStance==2,"Sanctuary absorbs 2 and counts enemy responses");
            run.Backpack[0]=new ItemStack(Ids.ItemSaveCrystal,2);GameSession.Instance!.SetCurrentRunForTesting(run);
            Check(GameSession.Instance.SaveGame().Success&&GameSession.Instance.TryLoadGame(out _),"active healer battle saves and loads through validated disk slot");
            run=GameSession.Instance.CurrentRun!;Check(run.WoodlandBattle.IronStance==2&&run.Character.ArchetypeId==Ids.ArchetypeHealer,"Sanctuary duration and class survive load");
            feedback=new();WoodlandBattle.Act(run,"pacifying_light",new ScriptedDieSource(20,1),feedback);
            Check(run.WoodlandBattle.EnemyHp==14&&run.WoodlandBattle.Frost==1&&run.WoodlandBattle.IronStance==1,"Pacifying Light deals 4 and slows for two responses, including this one");
            WoodlandBattle.Act(run,"guard",new ScriptedDieSource(20));Check(run.WoodlandBattle.IronStance==0,"Sanctuary expires after exactly three responses");
            run=Battle("radiant_bolt","mend");feedback=new();WoodlandBattle.Act(run,"staff",new ScriptedDieSource(6,1),feedback);
            Check(feedback.PlayerHit==true&&run.WoodlandBattle.EnemyHp==14&&run.Character.Mana==CharacterRules.MaxMana(run.Character),"free wand pulse uses Willpower (6+4), costs zero and deals 4");
            run.Character.Mana=0;feedback=new();WoodlandBattle.Act(run,"radiant_bolt",new ScriptedDieSource(20),feedback);Check(!feedback.ActionResolved&&run.WoodlandBattle.EnemyHp==14,"no-mana skill is atomic");
            run=NewHealer();run.WoodlandBattle.Stage=2;run.WoodlandBattle.EnemyHp=0;run.LocationId="safehouse";
            Check(EchoMission.Accept(run),"healer can accept Echo");run.LocationId="grove";EchoMission.Inspect(run);
            var method=EchoMission.AttemptMethod(run,new ScriptedDieSource(8));Check(method?.Success==true&&method.Total==12,"relay preparation uses trained Medicine/Intellect, not Arcana");
            Check(WoodlandBattle.Start(run),"healer can face guardian");
            while(run.FilterBattle.Stage==1)WoodlandBattle.Act(run,"staff",new ScriptedDieSource(20,1));
            Check(run.Echo.Stage==3&&EchoMission.HearRecording(run)&&EchoMission.Collect(run),"healer can finish guardian and recover sample");run.LocationId="guild";
            Check(EchoMission.Complete(run)&&!EchoMission.Complete(run),"healer completes Echo with one-time reward");
            run=Battle("mend","sanctuary");run.Character.Hp-=15;GameSession.Instance.SetCurrentRunForTesting(run);
            var panel=new WoodlandBattlePanel {Dice=new ScriptedDieSource(1)};AddChild(panel);
            var mend=panel.FindChildren("*","Button",true,false).OfType<Button>().Single(b=>b.Text.StartsWith("Mend"));mend.EmitSignal(BaseButton.SignalName.Pressed);mend.EmitSignal(BaseButton.SignalName.Pressed);
            while(panel.IsAnimating)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            Check(run.Character.Hp==CharacterRules.MaxHealth(run.Character)-5&&run.Character.Mana==CharacterRules.MaxMana(run.Character)-3,"animated Mend heals exactly once despite duplicate input");panel.QueueFree();
        }
        catch(Exception ex){GD.PrintErr(ex);_failed++;}
        finally{SaveServiceTestDirectory.Cleanup();}
        GD.Print($"HEALER CHECKS: {_failed} failures");GetTree().Quit(_failed==0?0:1);
    }
}
