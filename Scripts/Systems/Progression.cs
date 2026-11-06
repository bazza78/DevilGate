using DevilGate.Scripts.Data;
namespace DevilGate.Scripts.Systems;

public static class Progression
{
    public static long Threshold(int level)=>45L*Math.Max(1,level)*(Math.Max(1,level)-1)/2;
    public static long ToNextLevel(CharacterState c)=>Math.Max(0,Threshold(c.Level+1)-c.Xp);
    public static int Award(CharacterState c,int amount)
    {
        if(amount<0)throw new ArgumentOutOfRangeException(nameof(amount));
        c.Xp=(int)Math.Min(int.MaxValue,(long)c.Xp+amount);
        return Reconcile(c);
    }
    // Also upgrades pre-progression saves using their existing total XP. Never awards XP twice.
    public static int Reconcile(CharacterState c)
    {
        int before=c.Level;
        while(c.Xp>=Threshold(c.Level+1))c.Level++;
        if(c.Level>before){c.Hp=CharacterRules.MaxHealth(c);c.Mana=CharacterRules.MaxMana(c);}
        return c.Level-before;
    }
    public static string Summary(CharacterState c)=>$"Level {c.Level} · XP {c.Xp}/{Threshold(c.Level+1)} · {ToNextLevel(c)} to next level";
    public static string VictorySummary(RunState r)
    {
        var c=r.Character;
        bool justLevelled=c.Level>1 && c.Xp-20<Threshold(c.Level);
        return "Victory! +20 XP · +5 gold"+(justLevelled?$"\nLEVEL {c.Level}! +5 maximum HP and +2 maximum {StarterTraining.Resource(r).ToLowerInvariant()} per level. Vitals fully restored.":"")
            +$"\n{ToNextLevel(c)} XP to level {c.Level+1}. XP comes from completed objectives and victories.";
    }
}
