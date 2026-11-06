using DevilGate.Scripts.Data;
namespace DevilGate.Scripts.Definitions;

/// <summary>Portrait-ready creation roster. The male Knight remains unavailable until supplied.</summary>
public static class TestingRoster
{
    public static bool Enabled { get; set; } = true;
    public static bool AllowsArchetype(string id)=>!Enabled || id is Ids.ArchetypeMage or Ids.ArchetypeTank or Ids.ArchetypeHealer or Ids.ArchetypeArcher or Ids.ArchetypeKnight;
    public static bool AllowsGender(string id,string archetype=Ids.ArchetypeMage)=>archetype==Ids.ArchetypeKnight ? id==Ids.GenderFemale : !Enabled || id==Ids.GenderFemale || (archetype is Ids.ArchetypeMage or Ids.ArchetypeTank or Ids.ArchetypeHealer or Ids.ArchetypeArcher && id==Ids.GenderMale);
    public static string GenderUnavailableReason(string id,string archetype)=>archetype==Ids.ArchetypeKnight && id==Ids.GenderMale ? "The male Knight will be added later. Choose Female to play Knight." : "This character option is not enabled.";
}
