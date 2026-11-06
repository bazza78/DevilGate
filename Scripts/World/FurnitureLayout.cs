using Godot;
namespace DevilGate.Scripts.World;

/// <summary>Sprite front-foot anchors and matching solid ground polygons. One source for physics and navigation.</summary>
public static class FurnitureLayout
{
    public sealed record Placement(string Id,int Cell,float Width,Vector2 Anchor,float HalfWidth,float HalfDepth)
    {
        public Vector2[] Footprint
        {
            get
            {
                var centre=Anchor+new Vector2(0,-HalfDepth);
                return new[] {centre+new Vector2(0,-HalfDepth),centre+new Vector2(HalfWidth,0),
                    centre+new Vector2(0,HalfDepth),centre+new Vector2(-HalfWidth,0)};
            }
        }
    }
    public static readonly Placement[] All=
    {
        new("workstation",0,150,SafehouseLayout.TableFocus,54,25),
        new("chest",2,86,SafehouseLayout.ChestFocus,31,16),
        new("bed",1,130,SafehouseLayout.BedFocus,47,25),
        new("potions",4,116,SafehouseLayout.PotionFocus,43,19),
        new("rack",5,110,SafehouseLayout.Iso(9,1.5f),37,17),
        new("sofa",3,132,SafehouseLayout.SofaFocus,49,24),
    };
}
