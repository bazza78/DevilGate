using Godot;
namespace DevilGate.Scripts.World;

/// <summary>Front ground anchors shared by guild art, collision and navigation.</summary>
public static class GuildHallLayout
{
    public const string Atlas="res://Assets/Environment/guild-furniture-v2.png";
    // Source pixels in each 512×512 atlas cell. The visible feet differ between cells;
    // a shared 94% pivot leaves several sprites suspended above their contact shadows.
    public sealed record GroundContact(Vector2 Pivot,Vector2[] Feet);
    public static readonly GroundContact[] Contacts =
    {
        new(new(256,459),new[]{new Vector2(58,399),new Vector2(183,458),new Vector2(348,458),new Vector2(453,398)}),
        new(new(256,484),new[]{new Vector2(114,483),new Vector2(82,438),new Vector2(419,415)}),
        new(new(256,468),new[]{new Vector2(113,423),new Vector2(178,467),new Vector2(424,393)}),
        new(new(256,483),new[]{new Vector2(116,482),new Vector2(71,447),new Vector2(397,432)}),
        new(new(256,480),new[]{new Vector2(115,479),new Vector2(68,445),new Vector2(409,431)}),
        new(new(256,463),new[]{new Vector2(134,462),new Vector2(47,363),new Vector2(406,411)}),
    };
    public static Vector2 ContactOffset(FurnitureLayout.Placement prop,Vector2 sourcePoint)=>(sourcePoint-Contacts[prop.Cell].Pivot)*(prop.Width/512f);
    public static readonly FurnitureLayout.Placement[] Furnishings=
    {
        new("reception",0,170,CourtyardRenderer.Contact+new Vector2(0,35),64,23),
        new("contracts",1,138,SafehouseLayout.Iso(4,2),48,17),
        new("waiting_bench",2,128,SafehouseLayout.Iso(2.5f,5),46,22),
        new("equipment_display",3,110,SafehouseLayout.Iso(11.5f,1.5f),39,18),
        new("records",4,105,SafehouseLayout.Iso(1.5f,2.5f),35,18),
        new("briefing_table",5,130,SafehouseLayout.Iso(13,8),48,24),
    };
    public static FurnitureLayout.Placement Contracts=>Furnishings.Single(p=>p.Id=="contracts");
}
