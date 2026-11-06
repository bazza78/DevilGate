using DevilGate.Scripts.Data;
using DevilGate.Scripts.Definitions;
using DevilGate.Scripts.Systems;
using Godot;
namespace DevilGate.Scripts.Checks;

public partial class KnightChecks : Node
{
    private int _failed;
    private void Check(bool ok,string text){GD.Print($"KNIGHT {(ok?"PASS":"FAIL")}: {text}");if(!ok)_failed++;}
    public static RunState NewKnight()
    {
        var arch=GameCatalog.Instance.GetArchetype(Ids.ArchetypeKnight);
        var run=RunFactory.CreateNewRun(new CharacterCreationResult{Name="Seren",ArchetypeId=arch.Id,GenderId=Ids.GenderFemale,Attributes=arch.CreateSuggestedAttributes()},GameCatalog.Instance);
        run.InductionStage=3;run.MagicAwakened=true;run.LearnedSpellIds=StarterTraining.SuggestedPair(run);run.WakingIntroductionSeen=true;return run;
    }
    private static RunState Battle(params string[] skills){var r=NewKnight();r.LearnedSpellIds=skills;r.LocationId="woodland";WoodlandBattle.Start(r);return r;}
    public override async void _Ready()
    {
        SaveServiceTestDirectory.UseTempDirectory();
        try
        {
            Check(TestingRoster.AllowsArchetype(Ids.ArchetypeKnight)&&TestingRoster.AllowsGender(Ids.GenderFemale,Ids.ArchetypeKnight)&&!TestingRoster.AllowsGender(Ids.GenderMale,Ids.ArchetypeKnight),"female Knight enabled; male Knight unavailable");
            var session=GameSession.Instance!;session.BeginAdventure();var identity=session.CurrentRun!;
            var mirror=GD.Load<PackedScene>("res://Scenes/CharacterCreation.tscn").Instantiate<CharacterCreationController>();mirror.RoomMode="mirror";AddChild(mirror);
            var genders=mirror.GetNode<HBoxContainer>("Margin/HBox/Scroll/LeftVBox/GenderRow").GetChildren().OfType<Button>().ToArray();
            var male=genders.Single(b=>b.Text=="Male");male.ButtonPressed=true;male.EmitSignal(BaseButton.SignalName.Pressed);
            var knight=mirror.GetNode<HBoxContainer>("Margin/HBox/Scroll/LeftVBox/ArchetypeRow").GetChildren().OfType<Button>().Single(b=>b.Text=="Knight");knight.ButtonPressed=true;knight.EmitSignal(BaseButton.SignalName.Pressed);
            Check(male.Disabled&&male.TooltipText.Contains("later")&&genders.Single(b=>b.Text=="Female").ButtonPressed,"switching from a male class to Knight selects female and explains disabled male");
            Check(mirror.GetNode<TextureRect>("%PortraitTexture").Texture?.ResourcePath=="res://Assets/Portraits/knight_female.png","supplied female portrait displayed");
            male.EmitSignal(BaseButton.SignalName.Pressed);mirror.GetNode<LineEdit>("%NameEdit").Text="Seren";mirror.GetNode<Button>("%BeginButton").EmitSignal(BaseButton.SignalName.Pressed);mirror.QueueFree();
            Check(identity.Character.ArchetypeId==Ids.ArchetypeKnight&&identity.Character.GenderId==Ids.GenderFemale&&identity.CreationStage==1,"identity stays female even after disabled male signal");
            var rack=GD.Load<PackedScene>("res://Scenes/CharacterCreation.tscn").Instantiate<CharacterCreationController>();rack.RoomMode="rack";AddChild(rack);rack.GetNode<Button>("%BeginButton").EmitSignal(BaseButton.SignalName.Pressed);
            Check(identity.CreationStage==2&&identity.Backpack.CountItem(Ids.ItemServiceBlade)==1&&rack.GetNode<Label>("%DerivedLabel").Text.Contains("Focus"),"Knight kit grants blade/jacket with Focus summary");rack.QueueFree();
            identity.InductionStage=3;Check(StarterTraining.Awaken(identity)&&StarterTraining.Choose(identity,new[]{"commanding_strike","shield_wall"}),"Knight trains and equips recommended pair");
            Check(!StarterTraining.Choose(identity,new[]{"ember_bolt","shield_wall"})&&!StarterTraining.Choose(identity,new[]{"rally","rally"}),"foreign and duplicate skill pairs rejected");
            var r=Battle("commanding_strike","rallying_strike");var feedback=new BattleTurnFeedback();int mana=r.Character.Mana;
            WoodlandBattle.Act(r,"commanding_strike",new ScriptedDieSource(6,1),feedback);
            Check(feedback.PlayerHit==true&&r.WoodlandBattle.EnemyHp==10&&r.Character.Mana==mana-3,"Commanding Strike: Leadership/Presence (6+2+2), 8 damage, 3 Focus");
            feedback=new();WoodlandBattle.Act(r,"commanding_strike",new ScriptedDieSource(5,1),feedback);
            Check(feedback.PlayerHit==false&&r.WoodlandBattle.EnemyHp==10&&r.Character.Mana==mana-6,"miss spends Focus but deals no damage");
            r.Character.Hp=10;feedback=new();WoodlandBattle.Act(r,"rallying_strike",new ScriptedDieSource(20,20),feedback);
            Check(r.WoodlandBattle.EnemyHp==6&&feedback.PlayerHealing==3&&r.Character.Hp==9,"Rallying Strike: 4 damage and 3 healing before retaliation");
            var hybrid=Battle("commanding_strike","rallying_strike");hybrid.Character.Hp=10;feedback=new();
            WoodlandBattle.Act(hybrid,"rallying_strike",new ScriptedDieSource(5,1),feedback);
            Check(feedback.PlayerHit==false&&feedback.PlayerHealing==0&&hybrid.Character.Hp==10,"missed Rallying Strike cannot heal");
            hybrid.Character.Hp=CharacterRules.MaxHealth(hybrid.Character)-1;feedback=new();
            WoodlandBattle.Act(hybrid,"rallying_strike",new ScriptedDieSource(20,1),feedback);
            Check(feedback.PlayerHealing==1,"Rallying Strike healing caps at maximum HP");
            var refresh=Battle("shield_wall","rally");WoodlandBattle.Act(refresh,"shield_wall",new ScriptedDieSource(1));feedback=new();
            WoodlandBattle.Act(refresh,"shield_wall",new ScriptedDieSource(12),feedback);
            Check(feedback.EnemyHit&&feedback.EnemyDamageTaken==4&&refresh.WoodlandBattle.Ward==2,"Shield Wall recast refreshes duration without stacking or absorption");
            r=Battle("shield_wall","rally");feedback=new();WoodlandBattle.Act(r,"shield_wall",new ScriptedDieSource(11),feedback);
            Check(feedback.PlayerRoll==null&&!feedback.EnemyHit&&r.WoodlandBattle.Ward==2,"automatic Shield Wall grants +4 defence for three responses");
            var guardLog=WoodlandBattle.Act(r,"guard",new ScriptedDieSource(11));
            Check(r.WoodlandBattle.Ward==1&&guardLog.Contains("vs defence 14"),"Guard preserves longer Shield Wall without stacking");
            r.Backpack[0]=new ItemStack(Ids.ItemSaveCrystal,2);session.SetCurrentRunForTesting(r);
            Check(session.SaveGame().Success&&session.TryLoadGame(out _),"female Knight battle saves and loads through validated slot");
            r=session.CurrentRun!;Check(r.WoodlandBattle.Ward==1&&r.Character.GenderId==Ids.GenderFemale,"identity and Shield Wall duration persist");
            r.Character.Hp-=8;feedback=new();WoodlandBattle.Act(r,"rally",new ScriptedDieSource(11),feedback);
            Check(r.WoodlandBattle.Ward==0&&!feedback.EnemyHit,"saved Shield Wall protects one final response then expires");
            r=Battle("rally","shield_wall");r.Character.Hp=10;feedback=new();WoodlandBattle.Act(r,"rally",new ScriptedDieSource(20),feedback);
            Check(feedback.PlayerHealing==8&&feedback.PlayerRoll==null&&r.Character.Hp==14,"Rally heals 8 automatically before the enemy response");
            r.Character.Hp=CharacterRules.MaxHealth(r.Character)-2;feedback=new();WoodlandBattle.Act(r,"rally",new ScriptedDieSource(1),feedback);Check(feedback.PlayerHealing==2,"Rally caps at maximum HP");
            mana=r.Character.Mana;feedback=new();WoodlandBattle.Act(r,"rally",new ScriptedDieSource(20),feedback);Check(!feedback.ActionResolved&&r.Character.Mana==mana&&WoodlandBattlePanel.UnavailableReason(r,r.WoodlandBattle,"rally",false)=="health already full","full-health Rally spends no turn or Focus and explains why");
            r=Battle("commanding_strike","rally");feedback=new();WoodlandBattle.Act(r,"staff",new ScriptedDieSource(7,1),feedback);
            Check(feedback.PlayerHit==true&&r.WoodlandBattle.EnemyHp==14&&r.Character.Mana==CharacterRules.MaxMana(r.Character),"free blade uses Might (7+3), deals 4 and costs no Focus");
            r.Character.Mana=0;feedback=new();WoodlandBattle.Act(r,"commanding_strike",new ScriptedDieSource(20),feedback);Check(!feedback.ActionResolved&&r.WoodlandBattle.EnemyHp==14,"insufficient-Focus action leaves state unchanged");
            r=NewKnight();r.WoodlandBattle.Stage=2;r.WoodlandBattle.EnemyHp=0;Check(EchoMission.Accept(r),"Knight accepts Echo");r.LocationId="grove";EchoMission.Inspect(r);
            var method=EchoMission.AttemptMethod(r,new ScriptedDieSource(8));Check(method?.Success==true&&method.Total==12,"relay preparation uses Leadership/Presence");WoodlandBattle.Start(r);
            while(r.FilterBattle.Stage==1)WoodlandBattle.Act(r,"staff",new ScriptedDieSource(20,1));
            Check(r.Echo.Stage==3&&EchoMission.HearRecording(r)&&EchoMission.Collect(r),"Knight finishes guardian and collects sample");r.LocationId="guild";Check(EchoMission.Complete(r)&&!EchoMission.Complete(r),"Knight completes Echo with reward once");
            r=Battle("shield_wall","rally");session.SetCurrentRunForTesting(r);var panel=new WoodlandBattlePanel{Dice=new ScriptedDieSource(11)};AddChild(panel);
            var shield=panel.FindChildren("*","Button",true,false).OfType<Button>().Single(b=>b.Text.StartsWith("Shield Wall"));shield.EmitSignal(BaseButton.SignalName.Pressed);shield.EmitSignal(BaseButton.SignalName.Pressed);
            while(panel.IsAnimating)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            Check(r.Character.Mana==CharacterRules.MaxMana(r.Character)-3&&r.WoodlandBattle.Ward==2,"duplicate clicks animate and resolve Shield Wall exactly once");
            Check(panel.FindChildren("*","Label",true,false).OfType<Label>().Any(l=>l.Text.Contains("Shield Wall +4 DEF")),"battle status names Shield Wall");panel.QueueFree();
        }
        catch(Exception ex){GD.PrintErr(ex);_failed++;}
        finally{SaveServiceTestDirectory.Cleanup();}
        GD.Print($"KNIGHT CHECKS: {_failed} failures");GetTree().Quit(_failed==0?0:1);
    }
}
