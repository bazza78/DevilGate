using DevilGate.Scripts.Data;
using DevilGate.Scripts.Definitions;
namespace DevilGate.Scripts.Systems;

public sealed record StarterSpell(string Id,string Name,string School,string Description,string Practice);
/// <summary>Starter spells are separate from attribute-check skills such as Arcana.</summary>
public static class MageTraining
{
    public static readonly StarterSpell[] Spells=
    {
        new("ember_bolt","Ember Bolt","Elemental","Shape a small bolt of flame. 8 damage on hit · Arcana (Willpower) roll vs DC 10 (8 while exposed by Mage Light) · Instant · 3 mana.","A bright ember streaks into the crystal and dissolves into sparks."),
        new("frost_touch","Frost Touch","Elemental","Draw heat from a nearby target. 4 damage and enemy −2 attack for 2 enemy responses on hit · Arcana (Willpower) roll vs DC 10 (8 while exposed by Mage Light) · 3 mana.","Delicate frost spreads over the crystal, then melts away."),
        new("static_spark","Static Spark","Elemental","Release a controlled electrical spark. 6 damage on hit · Arcana (Willpower) roll vs DC 10 (8 while exposed by Mage Light) · Instant · 3 mana.","A blue arc leaps between your fingers and the crystal."),
        new("shadow_veil","Shadow Veil","Dark","Gather concealing shadow around yourself. +3 defence (hit chance only) for 3 enemy responses on hit · Arcana (Willpower) roll vs DC 10 (8 while exposed by Mage Light) · 3 mana.","Your cloak darkens as a ribbon of shadow coils around the crystal."),
        new("withering_touch","Withering Touch","Dark","Channel a trace of weakening magic. 4 damage and restore up to 3 HP on hit · Arcana (Willpower) roll vs DC 10 (8 while exposed by Mage Light) · Instant · 3 mana.","The crystal's light dims briefly, then returns as the containment rune absorbs the spell."),
        new("arcane_ward","Arcane Ward","Protective","Raise a translucent deflection ward. +4 defence (hit chance only, 0 absorbed) for 3 enemy responses on hit (shared with Guard; wisp hits deal 4 HP) · Arcana (Willpower) roll vs DC 10 (8 while exposed by Mage Light) · 3 mana.","A translucent deflection ward shimmers between you and the crystal."),
        new("mage_light","Mage Light","Protective","Summon a revealing magical light. 3 damage on hit and lowers attack and spell roll DC from 10 to 8 for the encounter · Arcana (Willpower) roll vs DC 10 (8 while exposed by Mage Light) · 3 mana.","A gentle sphere of light forms above the crystal.")
    };
    public static bool Eligible(RunState run)=>run.CreationStage==2 && run.InductionStage==3 && run.Character.ArchetypeId==Ids.ArchetypeMage && run.LocationId=="safehouse";
    public static bool Awaken(RunState run)
    {
        if (!Eligible(run) || run.MagicAwakened) return false;
        run.MagicAwakened=true;return true;
    }
    public static bool Choose(RunState run,IEnumerable<string> ids)
    {
        var chosen=ids.ToArray();
        if(!Eligible(run) || !run.MagicAwakened || chosen.Length!=2 || chosen.Distinct().Count()!=2 || chosen.Any(id=>!Spells.Any(s=>s.Id==id)))return false;
        run.LearnedSpellIds=(string[])chosen.Clone();return true;
    }
    public static string? Validate(bool awakened,string[]? ids)
    {
        if(ids==null || ids.Length is not (0 or 2) || ids.Distinct().Count()!=ids.Length || ids.Any(id=>!Spells.Any(s=>s.Id==id)))return "Invalid learned magic skills.";
        if(!awakened && ids.Length>0)return "Magic skills require attunement.";
        return null;
    }
}
