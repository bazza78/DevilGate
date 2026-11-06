using DevilGate.Scripts.Data;
using DevilGate.Scripts.Definitions;
namespace DevilGate.Scripts.Systems;

/// <summary>Class-specific active skills. Legacy save field names remain compatible.</summary>
public static class StarterTraining
{
    public static bool IsSupported(string id) => id is Ids.ArchetypeMage or Ids.ArchetypeTank or Ids.ArchetypeHealer or Ids.ArchetypeArcher or Ids.ArchetypeKnight;
    public static bool IsKnight(RunState run) => run.Character.ArchetypeId == Ids.ArchetypeKnight;
    public static bool IsArcher(RunState run) => run.Character.ArchetypeId == Ids.ArchetypeArcher;
    public static bool UsesFocus(RunState run) => IsTank(run) || IsArcher(run) || IsKnight(run);
    public static string VeilName(RunState run) => IsArcher(run) ? "Evasive Stance" : "Shadow Veil";
    public static bool IsHealer(RunState run) => run.Character.ArchetypeId == Ids.ArchetypeHealer;
    public static string AttackName(RunState run) => IsKnight(run) ? "Blade strike" : IsArcher(run) ? "Bow shot" : IsHealer(run) ? "Wand pulse" : IsTank(run) ? "Club strike" : "Staff strike";
    public static AttributeKind BasicAttribute(RunState run) => IsArcher(run) ? AttributeKind.Agility : IsHealer(run) ? AttributeKind.Willpower : AttributeKind.Might;
    public static AttributeKind SkillAttribute(RunState run) => IsKnight(run) ? AttributeKind.Presence : IsArcher(run) ? AttributeKind.Agility : IsHealer(run) ? AttributeKind.Intellect : IsTank(run) ? AttributeKind.Might : AttributeKind.Willpower;
    public static string CombatSkill(RunState run) => IsKnight(run) ? Ids.SkillLeadership : IsArcher(run) ? Ids.SkillPrecision : IsHealer(run) ? Ids.SkillMedicine : IsTank(run) ? Ids.SkillAthletics : Ids.SkillArcana;
    public static string[] SuggestedPair(RunState run) => IsKnight(run) ? new[] { "commanding_strike", "shield_wall" } : IsArcher(run) ? new[] { "precision_shot", "evasive_stance" } : IsHealer(run) ? new[] { "radiant_bolt", "mend" } : IsTank(run) ? new[] { "crushing_blow", "iron_stance" } : new[] { "ember_bolt", "arcane_ward" };
    public static string WardName(RunState run) => IsKnight(run) ? "Shield Wall" : "Ward";
    public static string ProtectionName(RunState run) => IsHealer(run) ? "Sanctuary" : "Iron Stance";
    public static readonly StarterSpell[] KnightSkills =
    {
        new("commanding_strike", "Commanding Strike", "Assault", "Commit to a decisive blade attack. 8 damage on hit · Leadership (Presence) roll vs DC 10 · Instant · 3 focus.", "Your blade cleaves the crystal's training projection."),
        new("rallying_strike", "Rallying Strike", "Resolve", "4 damage and restore up to 3 HP on hit, before the enemy responds · Leadership (Presence) roll vs DC 10 · Instant · 3 focus.", "A measured strike steadies your breathing."),
        new("shield_wall", "Shield Wall", "Defence", "+4 defence for 3 enemy responses · Automatic (no roll) · 3 focus. Reduces hit chance; absorbs 0 damage. Shares Guard's +4 bonus; refreshes duration rather than stacking. Guard preserves a longer Shield Wall.", "You brace behind a projected shield."),
        new("rally", "Rally", "Recovery", "Restore up to 8 HP before the enemy responds · Automatic (no roll) · Instant · 3 focus. Unavailable at full health.", "You recover your composure without lowering your guard.")
    };
    public static readonly StarterSpell[] ArcherSkills =
    {
        new("precision_shot", "Precision Shot", "Marksmanship", "Draw and release a carefully aimed arrow. 8 damage on hit · Precision (Agility) roll vs DC 10 · Instant · 3 focus.", "Your arrow finds the training echo's centre."),
        new("pinning_shot", "Pinning Shot", "Control", "4 damage and enemy −2 attack for 2 enemy responses on hit · Precision (Agility) roll vs DC 10 · 3 focus.", "A pinning arrow disrupts the training echo's approach."),
        new("evasive_stance", "Evasive Stance", "Evasion", "+3 defence for 3 enemy responses · Automatic (no roll) · 3 focus. Reduces hit chance; absorbs 0 damage. Refreshes duration; does not stack with itself. Guard's +4 defence is separate.", "Your feet settle into a light, balanced stance."),
        new("field_dressing", "Field Dressing", "Recovery", "Restore up to 8 HP before the enemy responds · Automatic (no roll) · Instant · 3 focus. Unavailable at full health.", "You remember how to treat a wound without losing sight of danger.")
    };
    public static readonly StarterSpell[] HealerSkills =
    {
        new("radiant_bolt", "Radiant Bolt", "Radiance", "Channel a focused beam through your wand. 6 damage on hit · Medicine (Intellect) roll vs DC 10 · Instant · 3 mana.", "The crystal answers with a warm thread of light."),
        new("mend", "Mend", "Restoration", "Restore up to 10 HP before the enemy responds · Automatic (no roll) · Instant · 3 mana. Unavailable at full health.", "A healing sigil closes around your hands."),
        new("sanctuary", "Sanctuary", "Protection", "Absorb 2 damage per hit for 3 enemy responses · Automatic (no roll) · 3 mana. Refreshes duration; does not stack. Guard's defence remains separate.", "A protective lattice settles into the lining of your cloak."),
        new("pacifying_light", "Pacifying Light", "Control", "4 damage and enemy −2 attack for 2 enemy responses on hit · Medicine (Intellect) roll vs DC 10 · 3 mana.", "The training echo slows in the crystal's light.")
    };
    public static bool IsTank(RunState run)=>run.Character.ArchetypeId==Ids.ArchetypeTank;
    public static readonly StarterSpell[] TankSkills=
    {
        new("crushing_blow","Crushing Blow","Assault","Deliver a heavy weapon strike. 8 damage on hit · Athletics (Might) roll vs DC 10 · Instant · 3 focus.","Your weapon lands solidly against the crystal's training projection."),
        new("shield_bash","Shield Bash","Control","Drive the target back with a bracing impact. 4 damage and enemy −2 attack for 2 enemy responses on hit · Athletics (Might) roll vs DC 10 · 3 focus.","A projected shield drives the training echo back."),
        new("iron_stance","Iron Stance","Protection","Settle into a reinforced defensive stance. Absorb 2 damage per hit for 3 enemy responses (reduces damage taken, not hit chance) · Automatic (no roll) · 3 focus.","Your stance settles as the crystal lights the protective lining of your armour."),
        new("second_wind","Second Wind","Recovery","Steady your breathing to recover mid-fight. Restore up to 8 HP · Automatic (no roll) · Instant · 3 focus.","You steady your breathing and the suit's recovery systems respond.")
    };
    public static StarterSpell[] SkillsFor(string archetype) => archetype switch
    {
        Ids.ArchetypeKnight => KnightSkills,
        Ids.ArchetypeArcher => ArcherSkills,
        Ids.ArchetypeHealer => HealerSkills,
        Ids.ArchetypeTank => TankSkills,
        _ => MageTraining.Spells,
    };
    public static StarterSpell[] Skills(RunState run)=>SkillsFor(run.Character.ArchetypeId);
    public static string Resource(RunState run)=>UsesFocus(run)?"Focus":"Mana";
    public static bool Eligible(RunState run)=>run.CreationStage==2 && run.InductionStage==3 && run.LocationId=="safehouse" && IsSupported(run.Character.ArchetypeId);
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
        var skills=SkillsFor(archetype);
        if(ids==null||ids.Length is not (0 or 2)||ids.Distinct().Count()!=ids.Length||ids.Any(id=>!skills.Any(s=>s.Id==id)))return "Invalid learned class skills.";
        if(!awakened && ids.Length>0)return "Skills require training attunement.";
        return null;
    }
}
