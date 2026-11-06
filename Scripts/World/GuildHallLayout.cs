using Godot;
namespace DevilGate.Scripts.World;

/// <summary>Front ground anchors shared by guild art, collision and navigation.</summary>
public static class GuildHallLayout
{
    public const string Atlas="res://Assets/Environment/guild-furniture-v2.png";
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
