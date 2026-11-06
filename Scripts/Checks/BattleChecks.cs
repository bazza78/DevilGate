using Godot;
using DevilGate.Scripts.Data;
using DevilGate.Scripts.Systems;
using DevilGate.Scripts.World;
namespace DevilGate.Scripts.Checks;
public partial class BattleChecks : Node
{
    private sealed class Die(int n):IDieSource {public int RollD20()=>n;}
    private int _fail;
    private void Check(bool ok,string text){GD.Print($"BATTLE {(ok?"PASS":"FAIL")}: {text}");if(!ok)_fail++;}
    private async Task Frames(int n){for(int i=0;i<n;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);}
    public override async void _Ready()
    {
        try
        {
            var session=GameSession.Instance!;session.StartTemporaryRun();var r=session.CurrentRun!;
            r.LocationId="woodland";
            Check(!WoodlandBattle.Start(r),"registration trip cannot trigger fight");
            r.MagicAwakened=true;r.InductionStage=3;
            Check(!WoodlandBattle.Start(r),"must choose two skills first");
            r.LearnedSpellIds=new[]{"ember_bolt","arcane_ward"};
            Check(WoodlandBattle.Start(r),"awakened mage can begin encounter");
            var hp=r.WoodlandBattle.EnemyHp;
            WoodlandBattle.Act(r,"shadow_veil",new Die(20));Check(r.WoodlandBattle.EnemyHp==hp,"unlearned spells rejected");
            r.Character.Mana=0;WoodlandBattle.Act(r,"ember_bolt",new Die(20));Check(r.WoodlandBattle.EnemyHp==hp,"insufficient mana rejects cast");
            // Mana potion in battle: rejected when none carried or when full; otherwise restores up to 10 mana and costs the player's turn.
            var noMp=new BattleTurnFeedback();
            WoodlandBattle.Act(r,"mana_potion",new Die(1),noMp);
            Check(!noMp.ActionResolved && r.Character.Mana==0,"mana potion without carried stack does not resolve a turn");
            r.Backpack[2]=new ItemStack(Ids.ItemManaPotion,1);
            r.Character.Mana=30;
            var fullMp=new BattleTurnFeedback();
            WoodlandBattle.Act(r,"mana_potion",new Die(1),fullMp);
            Check(!fullMp.ActionResolved && r.Backpack[2]?.Quantity==1,"mana potion at full mana is not consumed");
            r.Character.Mana=5;r.WoodlandBattle.Ward=2;
            var usedMp=new BattleTurnFeedback();
            WoodlandBattle.Act(r,"mana_potion",new Die(1),usedMp);
            Check(usedMp.ActionResolved && usedMp.PlayerEnergyRestored==10 && r.Character.Mana==15 && r.Backpack[2]==null && r.WoodlandBattle.Ward==1,"mana potion restores 10 mana, consumes stack and costs one enemy-response turn");
            r.WoodlandBattle.Ward=0;r.Character.Mana=0;
            WoodlandBattle.Act(r,"staff",new Die(20));Check(r.WoodlandBattle.EnemyHp==hp-4,"staff works without mana");
            Check(WoodlandBattle.Retreat(r) && r.WoodlandBattle.Stage==0,"retreat resets enemy encounter");
            WoodlandBattle.Start(r);r.Character.Mana=30;
            // Missed cast: arcane_ward on a low roll spends mana without granting or extending ward.
            var missedWard=new BattleTurnFeedback();
            WoodlandBattle.Act(r,"arcane_ward",new Die(1),missedWard);
            Check(missedWard.PlayerHit==false && r.WoodlandBattle.Ward==0 && r.Character.Mana==27,"missed arcane ward does not activate ward");
            r.Character.Hp=30;
            WoodlandBattle.Act(r,"arcane_ward",new Die(20));Check(r.WoodlandBattle.Ward==2,"ward persists and ticks after enemy turn");
            // Missed recast while ward is active does not refresh duration; existing ward ticks down normally.
            var cloneMiss=r.Clone();
            WoodlandBattle.Act(cloneMiss,"arcane_ward",new Die(1));
            Check(cloneMiss.WoodlandBattle.Ward==1,"missed arcane ward recast does not refresh active ward");
            // Ward -> Guard: Guard preserves a longer active ward (2 -> 1 after enemy response, not 1 -> 0) and shares +4 defence (14, not 18).
            var wardGuard=r.Clone();wardGuard.Character.Hp=30;
            var guardFeedback=new BattleTurnFeedback();
            var guardLog=WoodlandBattle.Act(wardGuard,"guard",new Die(11),guardFeedback);
            Check(wardGuard.WoodlandBattle.Ward==1 && !guardFeedback.EnemyHit && guardLog.Contains("vs defence 14"),"guard preserves longer active ward and shares +4 defence without stacking");
            // Next turn still has the preserved 1 remaining enemy response from Arcane Ward; roll 11 (+2=13 vs 14) still misses, then ward expires to 0.
            var thirdTurn=new BattleTurnFeedback();
            WoodlandBattle.Act(wardGuard,"staff",new Die(11),thirdTurn);
            Check(wardGuard.WoodlandBattle.Ward==0 && !thirdTurn.EnemyHit,"preserved ward protects on third enemy response then expires");
            // Expired ward: once Ward reaches 0, roll 11 (+2=13 vs base defence 10) hits for 4 damage.
            var expiredTurn=new BattleTurnFeedback();
            var expiredLog=WoodlandBattle.Act(wardGuard,"staff",new Die(11),expiredTurn);
            Check(expiredTurn.EnemyHit && expiredTurn.EnemyDamageTaken==4 && expiredLog.Contains("vs defence 10"),"expired ward removes +4 defence on subsequent enemy response");
            var clone=r.Clone();clone.WoodlandBattle.EnemyHp=1;Check(r.WoodlandBattle.EnemyHp==18,"battle clones independently");
            r.Backpack[0]=new ItemStack(Ids.ItemSaveCrystal,3);
            SaveService.DirectoryOverride="/tmp/devilgate-battle-check";
            Check(session.SaveGame().Success && session.TryLoadGame(out _),"active battle saves and loads on disk");r=session.CurrentRun!;
            Check(r.WoodlandBattle.Stage==1 && r.WoodlandBattle.Ward==2,"battle phase and buffs restored");
            // Saved duration after ward -> Guard (1 enemy response left) round-trips and matches panel display.
            WoodlandBattle.Act(r,"guard",new Die(1));
            Check(r.WoodlandBattle.Ward==1,"ward -> guard leaves 1 enemy response remaining");
            Check(session.SaveGame().Success && session.TryLoadGame(out _) && session.CurrentRun!.WoodlandBattle.Ward==1,"saved 1-response ward duration restores from disk");
            r=session.CurrentRun!;
            var wardPanel=new WoodlandBattlePanel();AddChild(wardPanel);
            Check(wardPanel.FindChildren("*","Label",true,false).OfType<Label>().Any(l=>l.Text.Contains("Ward: +4 defence (hit chance only, 0 absorbed), 1 enemy response remaining")),"battle panel shows remaining ward enemy responses and hit-chance distinction");
            var guardBtn=wardPanel.FindChildren("*","Button",true,false).OfType<Button>().Single(b=>b.Text.StartsWith("Guard"));
            var wardBtn=wardPanel.FindChildren("*","Button",true,false).OfType<Button>().Single(b=>b.Text.StartsWith("Arcane Ward"));
            Check(guardBtn.TooltipText.Contains("1 enemy response") && guardBtn.TooltipText.Contains("preserves a longer active ward") && wardBtn.TooltipText.Contains("3 enemy responses") && MageTraining.Spells.Single(s=>s.Id=="arcane_ward").Description.Contains("3 enemy responses"),"selection, battle status and tooltips share enemy response duration convention");
            wardPanel.QueueFree();
            var gold=r.CarriedGold;var xp=r.Character.Xp;
            for(int i=0;i<3;i++)WoodlandBattle.Act(r,"ember_bolt",new Die(20));
            Check(r.WoodlandBattle.Stage==2 && r.WoodlandBattle.EnemyHp==0,"offensive magic can win");
            Check(r.CarriedGold==gold+5 && r.Character.Xp==xp+20,"victory reward correct");
            WoodlandBattle.Act(r,"staff",new Die(20));Check(!WoodlandBattle.Start(r) && r.CarriedGold==gold+5,"won encounter cannot repeat or pay twice");
            r.WoodlandBattle=new WoodlandBattleState();WoodlandBattle.Start(r);r.Character.Hp=1;
            WoodlandBattle.Act(r,"staff",new Die(20));Check(r.WoodlandBattle.Stage==3,"defeat handled");
            WoodlandBattle.Retreat(r);Check(r.Character.Hp==CharacterRules.MaxHealth(r.Character.Attributes)/2,"guild rescue restores half health");
            r.WoodlandBattle=new WoodlandBattleState();r.LearnedSpellIds=new[]{"arcane_ward","shadow_veil"};r.Character.Hp=30;
            WoodlandBattle.Start(r);for(int i=0;i<5;i++)WoodlandBattle.Act(r,"staff",new Die(20));
            Check(r.WoodlandBattle.Stage==2,"defensive spell selection can win using staff");
            foreach(var spell in MageTraining.Spells)
            {
                r.WoodlandBattle=new WoodlandBattleState();r.Character.Hp=30;r.Character.Mana=30;
                r.LearnedSpellIds=new[]{spell.Id,spell.Id=="arcane_ward"?"ember_bolt":"arcane_ward"};
                WoodlandBattle.Start(r);WoodlandBattle.Act(r,spell.Id,new Die(20));
                Check(r.Character.Mana==27 && WoodlandBattle.Validate(r.WoodlandBattle)==null,"spell resolves: "+spell.Name);
            }
            // Verify proximity trigger through the actual scene, plus save-resume modal behaviour.
            r.WoodlandBattle=new WoodlandBattleState();r.Character.Hp=30;r.PositionX=RiftWisp.EncounterPoint.X;r.PositionY=RiftWisp.EncounterPoint.Y;r.WakingIntroductionSeen=true;
            var house=GD.Load<PackedScene>("res://Scenes/Safehouse.tscn").Instantiate<SafehouseController>();AddChild(house);await Frames(45);
            var ui=house.GetChildren().OfType<InteractionController>().Single();
            Check(ui.TopPanel is WoodlandBattlePanel && r.WoodlandBattle.Stage==1,"walking to encounter opens battle panel");
            ui.HandleEscape();Check(ui.HasOpenPanel,"Escape cannot bypass active battle");
        }
        catch(Exception e){GD.PrintErr(e);_fail++;}
        GD.Print($"BATTLE CHECKS: {_fail} failures");GetTree().Quit(_fail==0?0:1);
    }
}
