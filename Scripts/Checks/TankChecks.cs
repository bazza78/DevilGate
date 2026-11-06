using Godot;
using DevilGate.Scripts.Data;
using DevilGate.Scripts.Definitions;
using DevilGate.Scripts.Systems;
namespace DevilGate.Scripts.Checks;
public partial class TankChecks : Node
{
    private sealed class Die(int value):IDieSource {public int RollD20()=>value;}
    private int _failed;
    private void Check(bool ok,string name){GD.Print($"TANK {(ok?"PASS":"FAIL")}: {name}");if(!ok)_failed++;}
    private static Button Button(Control panel,string text)=>panel.FindChildren("*","Button",true,false).OfType<Button>().Single(b=>b.Text==text);
    private static void Press(Control panel,string text)=>Button(panel,text).EmitSignal(BaseButton.SignalName.Pressed);
    public override async void _Ready()
    {
        try
        {
            var session=GameSession.Instance!;session.BeginAdventure();var run=session.CurrentRun!;
            var mirror=GD.Load<PackedScene>("res://Scenes/CharacterCreation.tscn").Instantiate<CharacterCreationController>();mirror.RoomMode="mirror";AddChild(mirror);
            Press(mirror,"Male");Press(mirror,"Tank");
            Check(!Button(mirror,"Male").Disabled && Button(mirror,"Male").ButtonPressed,"male tank selectable and preserved when switching class");
            Check(mirror.GetNode<TextureRect>("%PortraitTexture").Texture?.ResourcePath=="res://Assets/Portraits/tank_male.png","supplied male orc portrait appears");
            mirror.GetNode<LineEdit>("%NameEdit").Text="Ash";Press(mirror,"Accept identity");
            Check(run.Character.ArchetypeId==Ids.ArchetypeTank && run.Character.GenderId==Ids.GenderMale,"mirror confirms male tank identity");
            Check(SaveData.FromRun(run).ToRunState().Character.GenderId==Ids.GenderMale,"male tank gender survives save conversion");
            session.BeginAdventure();run=session.CurrentRun!;
            mirror=GD.Load<PackedScene>("res://Scenes/CharacterCreation.tscn").Instantiate<CharacterCreationController>();mirror.RoomMode="mirror";AddChild(mirror);
            Press(mirror,"Tank");Press(mirror,"Female");
            Check(!Button(mirror,"Female").Disabled && Button(mirror,"Female").ButtonPressed,"female tank remains selectable");
            Check(mirror.GetNode<TextureRect>("%PortraitTexture").Texture?.ResourcePath=="res://Assets/Portraits/tank_female.png","supplied tank portrait appears");
            mirror.GetNode<LineEdit>("%NameEdit").Text="Ash";Press(mirror,"Accept identity");
            Check(run.Character.ArchetypeId==Ids.ArchetypeTank && run.Character.GenderId==Ids.GenderFemale && run.CreationStage==1,"mirror confirms tank identity");
            var rack=GD.Load<PackedScene>("res://Scenes/CharacterCreation.tscn").Instantiate<CharacterCreationController>();rack.RoomMode="rack";AddChild(rack);Press(rack,"Confirm training & collect kit");
            Check(run.Character.Attributes.Endurance==5 && run.Character.Hp==45 && run.Backpack.CountItem(Ids.ItemReinforcedClub)==1,"tank stats and starter club granted");
            Check(!StarterTraining.Awaken(run),"training gated until guild induction");
            run.LocationId="guild";InductionMission.Accept(run);
            var ui=new InteractionController();AddChild(ui);ui.ShowGuildContact();var dialogue=ui.TopPanel!;
            Press(dialogue,"About my Vanguard record");Check(dialogue.GetNode<Label>("%BodyLabel").Text.Contains("Register with Diane first"),"sealed record requires registration");
            Press(dialogue,"Back to our conversation");Press(dialogue,"Register with Diane");Press(dialogue,"About my Vanguard record");Check(dialogue.GetNode<Label>("%BodyLabel").Text.Contains("Ashfall"),"registered tank can read personal story");
            Press(dialogue,"Close");run.LocationId="safehouse";InductionMission.Complete(run);
            Check(StarterTraining.Awaken(run) && StarterTraining.Choose(run,new[]{"crushing_blow","iron_stance"}),"tank unlocks and chooses class pair");
            Check(!StarterTraining.Choose(run,new[]{"ember_bolt","iron_stance"}),"cannot mix mage and tank skills");
            Check(OpeningGuidance.Next(run).Text.Contains("Ready to explore"),"tank progression reaches ready state");
            ui.ShowMagicTraining();var training=ui.TopPanel!;
            Check(training.FindChildren("*","CheckBox",true,false).Count==4,"tank training lists four techniques");
            Check(StarterTraining.TankSkills.All(s=>s.Description.Contains("3 focus") && (s.Description.Contains("Athletics (Might) roll vs DC 10") || s.Description.Contains("Automatic (no roll)")) && (s.Description.Contains("Instant") || s.Description.Contains("enemy responses"))),"all tank skills explain effect, roll/automatic, duration and focus cost");
            Press(training,"Close");run.LocationId="woodland";
            Check(WoodlandBattle.Start(run),"trained tank can enter encounter");
            int hp=run.Character.Hp;var feedback=new BattleTurnFeedback();
            WoodlandBattle.Act(run,"iron_stance",new Die(20),feedback);
            Check(run.Character.Hp==hp-2 && feedback.EnemyRawDamage==4 && feedback.EnemyDamageBlocked==2 && feedback.EnemyDamageTaken==2,"stance absorbs 2 from a 4 damage hit");
            Check(run.WoodlandBattle.IronStance==2,"stance includes current enemy turn in duration");
            run.Backpack[5]=new ItemStack(Ids.ItemSaveCrystal,3);SaveService.DirectoryOverride="/tmp/devilgate-tank-checks";
            Check(session.SaveGame().Success && session.TryLoadGame(out _),"tank skill and active battle save loads");run=session.CurrentRun!;
            Check(run.WoodlandBattle.IronStance==2 && run.Character.ArchetypeId==Ids.ArchetypeTank && run.LearnedSpellIds.Contains("iron_stance"),"loaded tank preserves skills and absorption");
            WoodlandBattle.Act(run,"staff",new Die(20));WoodlandBattle.Act(run,"staff",new Die(20));hp=run.Character.Hp;
            WoodlandBattle.Act(run,"staff",new Die(20));Check(run.Character.Hp==hp-4 && run.WoodlandBattle.IronStance==0,"expired stance restores full damage");
            foreach(var skill in StarterTraining.TankSkills)
            {
                run.WoodlandBattle=new();run.Character.Hp=30;run.Character.Mana=15;run.LearnedSpellIds=new[]{skill.Id,skill.Id=="iron_stance"?"crushing_blow":"iron_stance"};WoodlandBattle.Start(run);
                feedback=new();WoodlandBattle.Act(run,skill.Id,new Die(20),feedback);
                Check(feedback.ActionResolved && run.Character.Mana==12 && WoodlandBattle.Validate(run.WoodlandBattle)==null,"technique resolves: "+skill.Name);
                if(skill.Id=="second_wind")Check(feedback.PlayerHealing==8 && run.Character.Hp==34,"recovery occurs before enemy response");
                if(skill.Id=="shield_bash")Check(run.WoodlandBattle.Frost==1 && run.WoodlandBattle.EnemyHp==14,"shield bash damages and weakens enemy");
            }
            // Mage shield regression: it changes hit chance, never incoming damage.
            run.Character.ArchetypeId=Ids.ArchetypeMage;run.Character.Attributes=GameCatalog.Instance.GetArchetype(Ids.ArchetypeMage).CreateSuggestedAttributes();run.Character.Hp=30;run.Character.Mana=30;
            run.LearnedSpellIds=new[]{"arcane_ward","ember_bolt"};run.WoodlandBattle=new();WoodlandBattle.Start(run);feedback=new();
            var result=WoodlandBattle.Act(run,"arcane_ward",new Die(20),feedback);
            Check(run.Character.Hp==26 && feedback.EnemyDamageTaken==4 && feedback.EnemyDamageBlocked==0 && result.Contains("4 base − 0 absorbed = 4 HP"),"ward is defence bonus; landed wisp hit is explicitly 4");
            run.WoodlandBattle=new();run.Character.Hp=30;WoodlandBattle.Start(run);feedback=new();WoodlandBattle.Act(run,"arcane_ward",new Die(11),feedback);
            Check(run.Character.Hp==30 && !feedback.EnemyHit,"ward can prevent a hit through higher defence");
            // Actual animated tank panel must use resolved absorption instead of hard-coded −4.
            run.Character.ArchetypeId=Ids.ArchetypeTank;run.Character.Attributes=GameCatalog.Instance.GetArchetype(Ids.ArchetypeTank).CreateSuggestedAttributes();run.Character.Hp=45;run.Character.Mana=15;run.LearnedSpellIds=new[]{"iron_stance","crushing_blow"};run.WoodlandBattle=new();WoodlandBattle.Start(run);
            var battle=new WoodlandBattlePanel{Dice=new Die(20)};AddChild(battle);Press(battle,"Iron Stance · 3 focus");
            while(battle.IsAnimating)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            Check(battle.FindChildren("PlayerHealthBar","ProgressBar",true,false).OfType<ProgressBar>().Single().Value==43,"animated tank health ends at reduced damage");
            Check(battle.FindChildren("*","Label",true,false).OfType<Label>().Any(l=>l.Text.Contains("4 base − 2 absorbed = 2 HP")),"visible battle summary explains reduction");
            Check(battle.FindChildren("*","Label",true,false).OfType<Label>().Any(l=>l.Text.Contains("Iron Stance: absorb 2 damage per hit, 2 enemy responses remaining")),"tank status displays remaining enemy responses and absorption");
            var stanceButton=battle.FindChildren("*","Button",true,false).OfType<Button>().Single(b=>b.Text.StartsWith("Iron Stance"));
            Check(stanceButton.TooltipText.Contains("3 enemy responses") && StarterTraining.TankSkills.Single(s=>s.Id=="iron_stance").Description.Contains("3 enemy responses"),"tank selection and tooltip share enemy response duration convention");
        }
        catch(Exception e){GD.PrintErr(e);_failed++;}
        GD.Print($"TANK CHECKS: {_failed} failures");GetTree().Quit(_failed==0?0:1);
    }
}
