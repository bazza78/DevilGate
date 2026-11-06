using Godot;
using DevilGate.Scripts.Data;
using DevilGate.Scripts.Definitions;
using DevilGate.Scripts.Systems;
using DevilGate.Scripts.World;
namespace DevilGate.Scripts.Checks;
public partial class EchoChecks : Node
{
    private sealed class Die(int value):IDieSource {public int RollD20()=>value;}
    private sealed class SafeDice:IDieSource {private int n;public int RollD20()=>++n%2==1?20:1;}
    private int failed;
    private void Check(bool ok,string message){GD.Print($"ECHO {(ok?"PASS":"FAIL")}: {message}");if(!ok)failed++;}
    private static void Press(Control panel,string text)=>panel.FindChildren("*","Button",true,false).OfType<Button>().Single(b=>b.Text==text).EmitSignal(BaseButton.SignalName.Pressed);
    private async Task Frames(int count){for(int i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);}
    private RunState ReadyRun(string archetype)
    {
        var s=GameSession.Instance!;s.StartTemporaryRun();var r=s.CurrentRun!;
        r.Character.ArchetypeId=archetype;r.Character.Attributes=GameCatalog.Instance.GetArchetype(archetype).CreateSuggestedAttributes();
        r.WakingIntroductionSeen=true;
        r.InductionStage=3;r.MagicAwakened=true;r.LearnedSpellIds=archetype==Ids.ArchetypeTank?new[]{"crushing_blow","iron_stance"}:new[]{"ember_bolt","arcane_ward"};
        r.WoodlandBattle=new WoodlandBattleState{Stage=2,EnemyHp=0};Progression.Award(r.Character,45);
        return r;
    }
    public override async void _Ready()
    {
        try
        {
            Check(WorldRoute.Areas.SequenceEqual(new[]{"safehouse","woodland","grove","town","guild"}),"shortened route keeps wisp and relay areas");
            var session=GameSession.Instance!;session.StartTemporaryRun();var r=session.CurrentRun!;
            Check(!EchoMission.Accept(r),"cannot accept before first wisp");
            var legacy=SaveData.FromRun(r);legacy.LocationId="courtyard";Check(legacy.ToRunState().LocationId=="woodland","legacy park save moves to Woodland path");
            foreach(var archetype in new[]{Ids.ArchetypeMage,Ids.ArchetypeTank})
            {
                r=ReadyRun(archetype);r.LocationId="guild";EchoMission.Accept(r);r.LocationId="grove";EchoMission.Inspect(r);
                Check(EchoMission.AttemptMethod(r,new Die(20))?.Success==true,archetype+": optional class preparation succeeds");
                WoodlandBattle.Start(r);var prepared=new BattleTurnFeedback();WoodlandBattle.Act(r,"staff",new Die(8),prepared);
                Check(!prepared.EnemyHit,archetype+": successful preparation weakens guardian attack");
                r=ReadyRun(archetype);Check(EchoMission.Accept(r)&&!EchoMission.Accept(r),archetype+": home acceptance is once-only");
                r.LocationId="grove";Check(EchoMission.Inspect(r),archetype+": relay inspection advances objective");
                Check(EchoMission.AttemptMethod(r,new Die(1))?.Success==false && EchoMission.AttemptMethod(r,new Die(20))==null,archetype+": failed optional roll cannot be farmed");
                Check(WoodlandBattle.Start(r),archetype+": failed check does not block guardian");
                var hp=r.Character.Hp;var feedback=new BattleTurnFeedback();WoodlandBattle.Act(r,"guard",new Die(20),feedback);
                Check(feedback.EnemyRawDamage==5 && r.Character.Hp==hp-5,archetype+": guardian has its own damage profile");
                Check(WoodlandBattle.Retreat(r)&&r.FilterBattle.Stage==0&&r.Echo.Stage==2&&r.Echo.MethodAttempted,archetype+": retreat preserves investigation and failed roll");
                r.Character.Hp=CharacterRules.MaxHealth(r.Character);r.Character.Mana=CharacterRules.MaxMana(r.Character);
                WoodlandBattle.Start(r);r.Character.Hp=1;WoodlandBattle.Act(r,"guard",new Die(20));
                Check(r.FilterBattle.Stage==3&&WoodlandBattle.Retreat(r)&&r.Character.Hp==CharacterRules.MaxHealth(r.Character)/2,archetype+": defeat and guild rescue recover safely");
                r.Character.Hp=CharacterRules.MaxHealth(r.Character);r.Character.Mana=CharacterRules.MaxMana(r.Character);WoodlandBattle.Start(r);
                r.Backpack[0]=new ItemStack(Ids.ItemSaveCrystal,3);SaveService.DirectoryOverride="/tmp/devilgate-echo-checks";
                Check(session.SaveGame().Success&&session.TryLoadGame(out _),archetype+": active guardian battle saves/loads");r=session.CurrentRun!;
                if(archetype==Ids.ArchetypeMage)
                {
                    var resume=GD.Load<PackedScene>("res://Scenes/Safehouse.tscn").Instantiate<SafehouseController>();AddChild(resume);await Frames(45);
                    Check(resume.GetChildren().OfType<InteractionController>().Single().TopPanel is WoodlandBattlePanel,"loaded guardian battle reopens in actual world scene");
                    resume.QueueFree();await Frames(2);
                }
                var old=r.Clone();old.Echo.Stage=0;Check(r.Echo.Stage==2,archetype+": quest clone does not alias live state");
                var dice=new SafeDice();for(int i=0;i<3;i++)WoodlandBattle.Act(r,archetype==Ids.ArchetypeTank?"crushing_blow":"ember_bolt",dice);
                Check(r.Echo.Stage==3&&r.FilterBattle.Stage==2&&r.Character.Xp==60&&r.WoodlandBattle.Stage==2,archetype+": guardian advances quest and awards exactly 15 XP");
                WoodlandBattle.Act(r,"staff");Check(r.Character.Xp==60&&!WoodlandBattle.Start(r),archetype+": guardian cannot repeat XP");
                Check(!EchoMission.Collect(r)&&EchoMission.HearRecording(r),archetype+": recording precedes sample collection");
                for(int i=0;i<InventoryState.SlotCount;i++)r.Backpack[i]=new ItemStack(Ids.ItemHealthPotion,1);
                Check(EchoMission.Collect(r)&&r.HasFilterSample&&!EchoMission.Collect(r),archetype+": full backpack cannot block unique key sample");
                Check(!EchoMission.Complete(r),archetype+": cannot turn in at relay");
                r.LocationId="guild";var gold=r.CarriedGold;
                Check(EchoMission.Complete(r)&&r.Character.Xp==80&&r.CarriedGold==gold+20&&!r.HasFilterSample,archetype+": turn-in totals 35 quest XP and pays 20 gold");
                Check(!EchoMission.Complete(r)&&r.CarriedGold==gold+20&&r.Character.Xp==80,archetype+": reward cannot repeat");
                Check(SaveData.FromRun(r).ToRunState().Echo.Stage==5&&EchoMission.Description(r).Contains("Gate Seven"),archetype+": completion and archive persist");
            }
            // Full click/movement/UI journey, starting at home after the first wisp.
            var main=GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<MainController>();AddChild(main);r=ReadyRun(Ids.ArchetypeMage);main.ShowSafehouse();await Frames(40);
            SafehouseController House()=>main.GetChildren().OfType<SafehouseController>().Last();
            InteractionController UI()=>House().GetChildren().OfType<InteractionController>().Single();
            async Task Travel(bool forward,string expected)
            {
                UI().HandleWorldClick(forward?CourtyardRenderer.ContactApproach+new Vector2(0,-20):CourtyardRenderer.Door+new Vector2(0,-20));
                for(int i=0;i<650&&!UI().HasOpenPanel;i++)await Frames(1);
                Press(UI().TopPanel!,"Travel");await Frames(40);Check(session.CurrentRun!.LocationId==expected,"walk/travel to "+expected);
            }
            UI().ShowTerminalPanel();Press(UI().TopPanel!,"Contracts · Echo in the Filters");Press(UI().TopPanel!,"Accept Echo in the Filters");
            Check(r.Echo.Stage==1,"PC accepts shared contract without visiting Diane");
            Check(UI().TopPanel!.Visible && UI().GetChildren().OfType<CenterContainer>().SelectMany(c=>c.GetChildren().OfType<Control>()).Count(c=>c.Visible)==1,"nested contract hides underlying terminal panel");UI().HandleEscape();UI().HandleEscape();
            UI().ShowDoorPanel();Press(UI().TopPanel!,"Go Outside");await Frames(40);Check(r.LocationId=="woodland","home door skips removed Park screen");
            await Travel(true,"grove");UI().HandleWorldClick(FilterRelay.Focus);
            for(int i=0;i<650&&!UI().HasOpenPanel;i++)await Frames(1);
            Check(UI().HasOpenPanel,"relay is reachable through actual navigation");Press(UI().TopPanel!,"Inspect the relay");Press(UI().TopPanel!,"Manual reset · Face guardian");
            var battle=(WoodlandBattlePanel)UI().TopPanel!;battle.Dice=new SafeDice();
            Check(battle.FindChildren("EnemyHealthBar","ProgressBar",true,false).OfType<ProgressBar>().Single().MaxValue==24,"guardian panel shows 24 HP");
            if(DisplayServer.GetName()!="headless"){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);using var image=GetViewport().GetTexture().GetImage();image.SavePng("/tmp/devilgate-echo-guardian-active.png");}
            for(int i=0;i<3;i++){Press(battle,"Ember Bolt · 3 mana");while(battle.IsAnimating)await Frames(1);}
            Check(r.Echo.Stage==3,"animated manual-route battle completes");
            if(DisplayServer.GetName()!="headless"){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);using var image=GetViewport().GetTexture().GetImage();image.SavePng("/tmp/devilgate-echo-guardian.png");}
            Press(battle,"Continue");UI().ShowFilterRelay();Press(UI().TopPanel!,"Read the maintenance recording");
            Check(UI().TopPanel!.GetNode<Label>("%BodyLabel").Text.Contains("You left the light on"),"recording reveals other-world phrase");
            Press(UI().TopPanel!,"Reset relay & seal sample");UI().HandleEscape();await Travel(true,"town");await Travel(true,"guild");
            UI().ShowGuildContact();Press(UI().TopPanel!,"Echo in the Filters");
            Check(!UI().TopPanel!.FindChildren("*","Button",true,false).OfType<Button>().Any(b=>b.Text.StartsWith("Accept Echo")),"Diane shares PC acceptance and cannot offer twice");
            Press(UI().TopPanel!,"Hand sample to Diane");Check(r.Echo.Stage==5&&r.Character.Xp==80&&r.CarriedGold==20,"entire playable contract pays correctly");
            Check(OpeningGuidance.Next(r).Text.Contains("Free exploration"),"completed chapter names current endpoint");
            r.Backpack[0]=new ItemStack(Ids.ItemSaveCrystal,3);Check(session.SaveGame().Success&&session.TryLoadGame(out _)&&session.CurrentRun!.Echo.Stage==5,"completed contract survives disk reload");
            if(DisplayServer.GetName()!="headless"){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);using var image=GetViewport().GetTexture().GetImage();image.SavePng("/tmp/devilgate-echo-turn-in.png");}
        }
        catch(Exception e){GD.PrintErr(e);failed++;}
        GD.Print($"ECHO CHECKS: {failed} failures");GetTree().Quit(failed==0?0:1);
    }
}
