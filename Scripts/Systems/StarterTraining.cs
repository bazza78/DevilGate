using DevilGate.Scripts.Data;
using DevilGate.Scripts.Definitions;
namespace DevilGate.Scripts.Systems;

/// <summary>Class-specific active skills. Legacy save field names remain compatible.</summary>
public static class StarterTraining
{
    public static bool IsTank(RunState run)=>run.Character.ArchetypeId==Ids.ArchetypeTank;
    public static readonly StarterSpell[] TankSkills=
    {
        new("crushing_blow","Crushing Blow","Assault","8 damage on hit. Costs 3 focus.","Your weapon lands solidly against the crystal's training projection."),
        new("shield_bash","Shield Bash","Control","4 damage on hit; the enemy has −2 attack for this and its next turn. Costs 3 focus.","A projected shield drives the training echo back."),
        new("iron_stance","Iron Stance","Protection","Automatically reduce each incoming hit by 2 for three enemy turns. Costs 3 focus.","Your stance settles as the crystal lights the protective lining of your armour."),
        new("second_wind","Second Wind","Recovery","Automatically restore up to 8 HP. Uses your turn. Costs 3 focus.","You steady your breathing and the suit's recovery systems respond.")
    };
    public static StarterSpell[] Skills(RunState run)=>IsTank(run)?TankSkills:MageTraining.Spells;
    public static string Resource(RunState run)=>IsTank(run)?"Focus":"Mana";
    public static bool Eligible(RunState run)=>run.CreationStage==2 && run.InductionStage==3 && run.LocationId=="safehouse" && run.Character.ArchetypeId is Ids.ArchetypeMage or Ids.ArchetypeTank;
    public static bool Awaken(RunState run)
    {
        if(!Eligible(run)||run.MagicAwakened)return false;
        run.MagicAwakened=true;return true;
    }
    public static bool Choose(RunState run,IEnumerable<string> ids)
    {
        var chosen=ids.ToArray();
        if(!Eligible(run)||!run.MagicAwakened||chosen.Length!=2||chosen.Distinct().Count()!=2||chosen.Any(id=>!Skills(run).Any(s=>s.Id==id)))return false;
        run.LearnedSpellIds=(string[])chosen.Clone();return true;
    }
    public static string? Validate(string archetype,bool awakened,string[]? ids)
    {
        var skills=archetype==Ids.ArchetypeTank?TankSkills:MageTraining.Spells;
        if(ids==null||ids.Length is not (0 or 2)||ids.Distinct().Count()!=ids.Length||ids.Any(id=>!skills.Any(s=>s.Id==id)))return "Invalid learned class skills.";
        if(!awakened && ids.Length>0)return "Skills require training attunement.";
        return null;
    }
}
