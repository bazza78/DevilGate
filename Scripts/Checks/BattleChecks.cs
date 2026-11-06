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
            WoodlandBattle.Act(r,"staff",new Die(20));Check(r.WoodlandBattle.EnemyHp==hp-4,"staff works without mana");
            Check(WoodlandBattle.Retreat(r) && r.WoodlandBattle.Stage==0,"retreat resets enemy encounter");
            WoodlandBattle.Start(r);r.Character.Mana=30;
            WoodlandBattle.Act(r,"arcane_ward",new Die(20));Check(r.WoodlandBattle.Ward==2,"ward persists and ticks after enemy turn");
            var clone=r.Clone();clone.WoodlandBattle.EnemyHp=1;Check(r.WoodlandBattle.EnemyHp==18,"battle clones independently");
            r.Backpack[0]=new ItemStack(Ids.ItemSaveCrystal,3);
            SaveService.DirectoryOverride="/tmp/devilgate-battle-check";
            Check(session.SaveGame().Success && session.TryLoadGame(out _),"active battle saves and loads on disk");r=session.CurrentRun!;
            Check(r.WoodlandBattle.Stage==1 && r.WoodlandBattle.Ward==2,"battle phase and buffs restored");
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
