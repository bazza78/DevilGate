using DevilGate.Scripts.Data;
using DevilGate.Scripts.Definitions;
using DevilGate.Scripts.Systems;
using Godot;
namespace DevilGate.Scripts.Checks;

public partial class ArcherChecks : Node
{
    private int _failed;
    private void Check(bool ok,string text){GD.Print($"ARCHER {(ok?"PASS":"FAIL")}: {text}");if(!ok)_failed++;}
    public static RunState NewArcher(string gender=Ids.GenderFemale)
    {
        var arch=GameCatalog.Instance.GetArchetype(Ids.ArchetypeArcher);
        var r=RunFactory.CreateNewRun(new CharacterCreationResult {Name="Sol",ArchetypeId=arch.Id,GenderId=gender,Attributes=arch.CreateSuggestedAttributes()},GameCatalog.Instance);
        r.InductionStage=3;r.MagicAwakened=true;r.LearnedSpellIds=StarterTraining.SuggestedPair(r);return r;
    }
    private static RunState Battle(params string[] pair)
    {
        var r=NewArcher();r.LearnedSpellIds=pair;r.LocationId="woodland";WoodlandBattle.Start(r);return r;
    }
    public override async void _Ready()
    {
        SaveServiceTestDirectory.UseTempDirectory();
        try
        {
            foreach(string gender in new[]{Ids.GenderMale,Ids.GenderFemale})
            {
                var r=NewArcher(gender);var session=GameSession.Instance!;session.SetCurrentRunForTesting(r);r.CreationStage=0;
                var mirror=GD.Load<PackedScene>("res://Scenes/CharacterCreation.tscn").Instantiate<CharacterCreationController>();mirror.RoomMode="mirror";AddChild(mirror);
                Check(TestingRoster.AllowsArchetype(Ids.ArchetypeArcher)&&TestingRoster.AllowsGender(gender,Ids.ArchetypeArcher),$"{gender} archer available");
                Check(mirror.GetNode<TextureRect>("%PortraitTexture").Texture?.ResourcePath==$"res://Assets/Portraits/archer_{gender}.png",$"{gender} supplied portrait shown");
                mirror.GetNode<LineEdit>("%NameEdit").Text="Sol";mirror.GetNode<Button>("%BeginButton").EmitSignal(BaseButton.SignalName.Pressed);mirror.QueueFree();
                var rack=GD.Load<PackedScene>("res://Scenes/CharacterCreation.tscn").Instantiate<CharacterCreationController>();rack.RoomMode="rack";AddChild(rack);rack.GetNode<Button>("%BeginButton").EmitSignal(BaseButton.SignalName.Pressed);
                Check(r.CreationStage==2&&r.Backpack.CountItem(Ids.ItemTrainingBow)==1,$"{gender} archer receives correct kit");rack.QueueFree();
                r.MagicAwakened=false;r.LearnedSpellIds=Array.Empty<string>();Check(StarterTraining.Awaken(r)&&StarterTraining.Choose(r,new[]{"precision_shot","field_dressing"}),$"{gender} archer attunes and equips two skills");
                Check(!StarterTraining.Choose(r,new[]{"ember_bolt","field_dressing"})&&!StarterTraining.Choose(r,new[]{"field_dressing","field_dressing"}),"cross-class and duplicate selections rejected");
            }
            var run=Battle("precision_shot","field_dressing");var feedback=new BattleTurnFeedback();int energy=run.Character.Mana;
            WoodlandBattle.Act(run,"precision_shot",new ScriptedDieSource(4,1),feedback);
            Check(feedback.PlayerHit==true&&run.WoodlandBattle.EnemyHp==10&&run.Character.Mana==energy-3,"Precision Shot uses trained Precision/Agility (4+4+2 meets DC10), deals 8 and spends 3 focus");
            feedback=new();WoodlandBattle.Act(run,"precision_shot",new ScriptedDieSource(3,1),feedback);
            Check(feedback.PlayerHit==false&&run.WoodlandBattle.EnemyHp==10&&run.Character.Mana==energy-6,"miss spends focus and deals no damage");
            run.Character.Hp=10;feedback=new();WoodlandBattle.Act(run,"field_dressing",new ScriptedDieSource(20),feedback);
            Check(feedback.PlayerHealing==8&&feedback.PlayerRoll==null&&feedback.EnemyDamageTaken==4&&run.Character.Hp==14,"Field Dressing automatically restores 8 before one enemy response");
            run.Character.Hp=CharacterRules.MaxHealth(run.Character)-2;feedback=new();WoodlandBattle.Act(run,"field_dressing",new ScriptedDieSource(1),feedback);
            Check(feedback.PlayerHealing==2&&run.Character.Hp==CharacterRules.MaxHealth(run.Character),"Field Dressing caps at maximum HP");
            energy=run.Character.Mana;feedback=new();WoodlandBattle.Act(run,"field_dressing",new ScriptedDieSource(20),feedback);
            Check(!feedback.ActionResolved&&run.Character.Mana==energy&&WoodlandBattlePanel.UnavailableReason(run,run.WoodlandBattle,"field_dressing",false)=="health already full","Field Dressing at full health spends neither turn nor focus and explains why");
            var evade=Battle("evasive_stance","pinning_shot");feedback=new();
            WoodlandBattle.Act(evade,"evasive_stance",new ScriptedDieSource(10),feedback);
            Check(!feedback.EnemyHit&&feedback.EnemyDamageTaken==0,"Evasive Stance makes attack total 12 miss defence 13");
            feedback=new();WoodlandBattle.Act(evade,"evasive_stance",new ScriptedDieSource(12),feedback);
            Check(feedback.EnemyHit&&feedback.EnemyDamageTaken==4&&evade.WoodlandBattle.Veil==2,"refresh does not stack defence or absorb damage");
            run=Battle("evasive_stance","pinning_shot");feedback=new();WoodlandBattle.Act(run,"evasive_stance",new ScriptedDieSource(20),feedback);
            Check(feedback.PlayerRoll==null&&feedback.EnemyDamageBlocked==0&&feedback.EnemyDamageTaken==4&&run.WoodlandBattle.Veil==2,"Evasive Stance automatically grants defence, absorbs no damage and counts enemy responses");
            run.Backpack[0]=new ItemStack(Ids.ItemSaveCrystal,2);GameSession.Instance!.SetCurrentRunForTesting(run);
            Check(GameSession.Instance.SaveGame().Success&&GameSession.Instance.TryLoadGame(out _),"active archer battle saves and loads through validated disk slot");
            run=GameSession.Instance.CurrentRun!;Check(run.WoodlandBattle.Veil==2&&run.Character.ArchetypeId==Ids.ArchetypeArcher,"Evasive Stance duration and class survive load");
            feedback=new();WoodlandBattle.Act(run,"pinning_shot",new ScriptedDieSource(20,1),feedback);
            Check(run.WoodlandBattle.EnemyHp==14&&run.WoodlandBattle.Frost==1&&run.WoodlandBattle.Veil==1,"Pinning Shot deals 4 and slows for two responses, including this one");
            WoodlandBattle.Act(run,"guard",new ScriptedDieSource(20));Check(run.WoodlandBattle.Veil==0,"Evasive Stance expires after exactly three responses");
            run=Battle("precision_shot","field_dressing");feedback=new();WoodlandBattle.Act(run,"staff",new ScriptedDieSource(6,1),feedback);
            Check(feedback.PlayerHit==true&&run.WoodlandBattle.EnemyHp==14&&run.Character.Mana==CharacterRules.MaxMana(run.Character),"free bow shot uses Agility (6+4), costs zero focus and deals 4");
            run.Character.Mana=0;feedback=new();WoodlandBattle.Act(run,"precision_shot",new ScriptedDieSource(20),feedback);Check(!feedback.ActionResolved&&run.WoodlandBattle.EnemyHp==14,"no-focus skill is atomic");
            run=NewArcher();run.WoodlandBattle.Stage=2;run.WoodlandBattle.EnemyHp=0;run.LocationId="safehouse";
            Check(EchoMission.Accept(run),"archer can accept Echo");run.LocationId="grove";EchoMission.Inspect(run);
            var method=EchoMission.AttemptMethod(run,new ScriptedDieSource(6));Check(method?.Success==true&&method.Total==12,"relay preparation uses trained Precision/Agility, not Arcana");
            Check(WoodlandBattle.Start(run),"archer can face guardian");
            while(run.FilterBattle.Stage==1)WoodlandBattle.Act(run,"staff",new ScriptedDieSource(20,1));
            Check(run.Echo.Stage==3&&EchoMission.HearRecording(run)&&EchoMission.Collect(run),"archer can finish guardian and recover sample");run.LocationId="guild";
            Check(EchoMission.Complete(run)&&!EchoMission.Complete(run),"archer completes Echo with one-time reward");
            run=Battle("field_dressing","evasive_stance");run.Character.Hp-=15;GameSession.Instance.SetCurrentRunForTesting(run);
            var panel=new WoodlandBattlePanel {Dice=new ScriptedDieSource(1)};AddChild(panel);
            var mend=panel.FindChildren("*","Button",true,false).OfType<Button>().Single(b=>b.Text.StartsWith("Field Dressing"));mend.EmitSignal(BaseButton.SignalName.Pressed);mend.EmitSignal(BaseButton.SignalName.Pressed);
            while(panel.IsAnimating)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            Check(run.Character.Hp==CharacterRules.MaxHealth(run.Character)-7&&run.Character.Mana==CharacterRules.MaxMana(run.Character)-3,"animated Field Dressing heals exactly once despite duplicate input");panel.QueueFree();
        }
        catch(Exception ex){GD.PrintErr(ex);_failed++;}
        finally{SaveServiceTestDirectory.Cleanup();}
        GD.Print($"ARCHER CHECKS: {_failed} failures");GetTree().Quit(_failed==0?0:1);
    }
}
