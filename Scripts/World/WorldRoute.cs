using Godot;
namespace DevilGate.Scripts.World;
public static class WorldRoute
{
    public static readonly string[] Areas={"safehouse","woodland","grove","town","guild"};
    public static readonly string[] Names={"Home","Woodland path","Willow grove","Town square","Guild reception"};
    public static string Normalize(string id)=>id=="courtyard"?"woodland":id;
    public static int Index(string id)=>Array.IndexOf(Areas,id);
    public static bool Valid(string id)=>Index(id)>=0;
    public static string Name(string id)=>Valid(id)?Names[Index(id)]:id;
    public static bool Adjacent(string from,string to)=>Valid(from)&&Valid(to)&&Math.Abs(Index(from)-Index(to))==1;
}
