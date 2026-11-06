using DevilGate.Scripts.Data;
namespace DevilGate.Scripts.Definitions;

/// <summary>Temporary creation restriction. Set Enabled=false to restore the full roster.</summary>
public static class TestingRoster
{
    public static bool Enabled { get; set; } = true;
    public static bool AllowsArchetype(string id)=>!Enabled || id is Ids.ArchetypeMage or Ids.ArchetypeTank;
    public static bool AllowsGender(string id,string archetype=Ids.ArchetypeMage)=>!Enabled || id==Ids.GenderFemale || (archetype is Ids.ArchetypeMage or Ids.ArchetypeTank && id==Ids.GenderMale);
}
