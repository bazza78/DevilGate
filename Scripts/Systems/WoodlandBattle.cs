using DevilGate.Scripts.Data;
using DevilGate.Scripts.Definitions;
namespace DevilGate.Scripts.Systems;
public sealed class BattleTurnFeedback
{
    public bool ActionResolved {get;set;}
    public int? PlayerRoll {get;set;}
    public bool? PlayerHit {get;set;}
    public int? EnemyRoll {get;set;}
    public bool EnemyHit {get;set;}
    public int EnemyRawDamage {get;set;}
    public int EnemyDamageBlocked {get;set;}
    public int EnemyDamageTaken {get;set;}
    public int PlayerHealing {get;set;}
}
public static class WoodlandBattle
{
    public static bool Unlocked(RunState r)=>r.MagicAwakened && r.LearnedSpellIds.Length==2 && r.CreationStage==2 && r.InductionStage==3;
    public static bool IsRelay(RunState r)=>r.LocationId=="grove";
    public static WoodlandBattleState Current(RunState r)=>IsRelay(r)?r.FilterBattle:r.WoodlandBattle;
    public static int EnemyMaxHp(RunState r)=>IsRelay(r)?24:18;
    public static string EnemyName(RunState r)=>IsRelay(r)?"Corrupted filter guardian":"Rift wisp";
    public static string RetreatDestination(RunState r)=>IsRelay(r)?"town":"safehouse";
    public static string VictorySummary(RunState r)=>IsRelay(r)?"Guardian disabled! +15 XP\nInspect the relay: read the recording and recover the filter sample.":Progression.VictorySummary(r);
    public static bool Start(RunState r)
    {
        if(IsRelay(r))
        {
            if(r.Echo.Stage!=2||!EchoMission.Available(r)||r.FilterBattle.Stage!=0)return false;
            r.FilterBattle=new WoodlandBattleState{Stage=1,EnemyHp=24};return true;
        }
        if(r.LocationId!="woodland" || !Unlocked(r) || r.WoodlandBattle.Stage!=0)return false;
        r.WoodlandBattle=new WoodlandBattleState {Stage=1};return true;
    }
    public static int Cost(string id)=>id is "staff" or "guard" or "potion"?0:3;
    public static string Act(RunState r,string action,IDieSource? dice=null,BattleTurnFeedback? feedback=null)
    {
        var b=Current(r);
        if(r.LocationId is not ("woodland" or "grove") || b.Stage!=1 || (IsRelay(r)&&r.Echo.Stage!=2))return "No active battle.";
        if(action is not ("staff" or "guard" or "potion") && !r.LearnedSpellIds.Contains(action))return "That skill is not learned.";
        if(action is not ("staff" or "guard" or "potion") && !StarterTraining.Skills(r).Any(s=>s.Id==action))return "Unknown skill.";
        if(r.Character.Mana<Cost(action))return "Not enough energy. Use your free weapon strike or guard.";
        var source=dice??new SystemDieSource();var log="";int damage=0;int hpBefore=r.Character.Hp;
        if(action=="potion")
        {
            var slot=Enumerable.Range(0,InventoryState.SlotCount).FirstOrDefault(i=>r.Backpack[i]?.ItemId==Ids.ItemHealthPotion,-1);
            if(slot<0)return "No health potion carried.";
            if(r.Character.Hp>=CharacterRules.MaxHealth(r.Character))return "Health is already full.";
            r.Backpack[slot]!.Quantity--;if(r.Backpack[slot]!.Quantity==0)r.Backpack[slot]=null;
            r.Character.Hp=Math.Min(CharacterRules.MaxHealth(r.Character),r.Character.Hp+15);log="Health potion: restored up to 15 HP.";
        }
        else if(action=="guard") {b.Ward=1;log="You brace behind your cloak: +4 defence this turn.";}
        else if(action=="iron_stance")
        {
            r.Character.Mana-=Cost(action);b.IronStance=3;
            log="Iron Stance: reduce incoming damage by 2 for three enemy turns.";
        }
        else if(action=="second_wind")
        {
            if(r.Character.Hp>=CharacterRules.MaxHealth(r.Character))return "Health is already full.";
            r.Character.Mana-=Cost(action);r.Character.Hp=Math.Min(CharacterRules.MaxHealth(r.Character),r.Character.Hp+8);
            log=$"Second Wind: restored {r.Character.Hp-hpBefore} HP.";
        }
        else
        {
            r.Character.Mana-=Cost(action);
            var roll=new DiceService(source).Resolve(r.Character,new SkillCheckSpec(action=="staff" || StarterTraining.IsTank(r)?AttributeKind.Might:AttributeKind.Willpower,action=="staff"?null:StarterTraining.IsTank(r)?Ids.SkillAthletics:Ids.SkillArcana,b.Lit?8:10,EnemyName(r)),GameCatalog.Instance);
            if(feedback!=null){feedback.PlayerRoll=roll.DieRoll;feedback.PlayerHit=roll.Success;}
            log=roll.Summary+" — "+(roll.Success?"SUCCESS":"MISS");
            if(roll.Success)
            {
                switch(action)
                {
                    case "staff": damage=4;break;
                    case "crushing_blow": damage=8;break;
                    case "shield_bash": damage=4;b.Frost=2;break;
                    case "ember_bolt": damage=8;break;
                    case "frost_touch": damage=4;b.Frost=2;break;
                    case "static_spark": damage=6;break;
                    case "withering_touch": damage=4;r.Character.Hp=Math.Min(CharacterRules.MaxHealth(r.Character),r.Character.Hp+3);break;
                    case "shadow_veil":b.Veil=3;break;
                    case "arcane_ward":b.Ward=3;break;
                    case "mage_light":b.Lit=true;damage=3;break;
                }
                if(damage>0)log+=$"\n{EnemyName(r)} takes {damage} damage.";
            }
        }
        if(feedback!=null){feedback.ActionResolved=true;feedback.PlayerHealing=r.Character.Hp-hpBefore;}
        b.EnemyHp=Math.Max(0,b.EnemyHp-damage);
        if(b.EnemyHp==0)
        {
            b.Stage=2;
            if(IsRelay(r)){r.Echo.Stage=3;Progression.Award(r.Character,EchoMission.GuardXp);return log+"\n"+VictorySummary(r);}
            Progression.Award(r.Character,20);r.CarriedGold+=5;
            return log+"\nThe rift wisp dissolves. Victory! +20 XP, +5 gold (once).";
        }
        int defence=10+(r.Equipment.Armour!=null?1:0)+(b.Ward>0?4:0)+(b.Veil>0?3:0);
        int die=source.RollD20();int bonus=IsRelay(r)?3:2;
        if(IsRelay(r)&&r.Echo.MethodAttempted)bonus+=r.Echo.Stabilized?-2:1;
        int attack=die+bonus-(b.Frost>0?2:0);
        if(feedback!=null){feedback.EnemyRoll=die;feedback.EnemyHit=attack>=defence;}
        log+=$"\n{EnemyName(r)}: d20({die}) + {bonus}"+(b.Frost>0?" − frost(2)":"")+$" = {attack} vs defence {defence}.";
        if(attack>=defence)
        {
            int raw=IsRelay(r)?5:4;int blocked=b.IronStance>0?2:0;int taken=raw-blocked;
            r.Character.Hp=Math.Max(0,r.Character.Hp-taken);
            if(feedback!=null){feedback.EnemyRawDamage=raw;feedback.EnemyDamageBlocked=blocked;feedback.EnemyDamageTaken=taken;}
            log+=$" {EnemyName(r)} hit: {raw} base − {blocked} absorbed = {taken} HP damage.";
        }
        else log+=" It misses: 0 HP damage.";
        b.IronStance=Math.Max(0,b.IronStance-1);
        b.Ward=Math.Max(0,b.Ward-1);b.Veil=Math.Max(0,b.Veil-1);b.Frost=Math.Max(0,b.Frost-1);
        if(r.Character.Hp==0){b.Stage=3;log+="\nYour vision fades. A guild patrol can bring you back to safety.";}
        return log;
    }
    public static bool Retreat(RunState r)
    {
        var battle=Current(r);
        if(battle.Stage is not (1 or 3))return false;
        if(battle.Stage==3)r.Character.Hp=Math.Max(1,CharacterRules.MaxHealth(r.Character)/2);
        if(IsRelay(r))r.FilterBattle=new WoodlandBattleState{EnemyHp=24};
        else r.WoodlandBattle=new WoodlandBattleState();
        return true;
    }
    public static string? Validate(WoodlandBattleState? b,int maxHp=18)
    {
        if(b==null || b.Stage<0 || b.Stage>3 || b.EnemyHp<0 || b.EnemyHp>maxHp || b.Ward<0 || b.Ward>3 || b.Veil<0 || b.Veil>3 || b.Frost<0 || b.Frost>2 || b.IronStance<0 || b.IronStance>3)return "Invalid woodland battle state.";
        if(b.Stage==2 && b.EnemyHp!=0)return "Invalid defeated enemy state.";
        return null;
    }
}
