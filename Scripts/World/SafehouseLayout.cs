using Godot;

namespace DevilGate.Scripts.World;

/// <summary>
/// All safehouse geometry in one place. The house is authored in "grid space"
/// (integer-ish tile coordinates) and projected to screen with a 2:1
/// isometric transform. Movement stays continuous in screen space.
///
/// Screen projection:  sx = (i - j) * TileWidth/2 + OriginX
///                    sy = (i + j) * TileHeight/2 + OriginY
/// </summary>
public static class SafehouseLayout
{
    public const float TileWidth = 60f;
    public const float TileHeight = 30f;
    public const float OriginX = 280f;
    public const float OriginY = 190f;

    public const float WallHeight = 82f;
    public const float CurbHeight = 14f;
    public const float WallBlockHeight = 44f;

    /// <summary>How close the player must get to an interactable's approach point.</summary>
    public const float InteractionRange = 56f;

    /// <summary>Screen-space radius for clicking an interactable instead of the floor.</summary>
    public const float InteractableClickRadius = 64f;

    /// <summary>Max distance (px) between a click and the navmesh for it to count as reachable.</summary>
    public const float ReachableClickTolerance = 90f;

    public static Vector2 Iso(float i, float j) =>
        new((i - j) * TileWidth * 0.5f + OriginX, (i + j) * TileHeight * 0.5f + OriginY);

    // ------------------------------------------------------------------
    // Rooms (tile-coordinate rectangles, half-open [i0,i1) x [j0,j1))
    // ------------------------------------------------------------------

    /// <summary>Room A: living/work area with table, PC and outside door.</summary>
    public static readonly Rect2I RoomA = new(0, 0, 12, 8);

    /// <summary>Room B: storage/sleeping alcove with chest and bed.</summary>
    public static readonly Rect2I RoomB = new(12, 2, 6, 6);

    /// <summary>Interior partition wall tiles on room A's east edge (visual + collision + nav blocker).</summary>
    public static readonly (int I, int J)[] WallTiles = Array.Empty<(int I, int J)>();

    /// <summary>Open passage tiles between the rooms (i=12 edge, j in [4,6)).</summary>
    public static readonly (int I, int J)[] PassageTiles = { (12, 4), (12, 5) };

    // Furniture geometry is defined in FurnitureLayout, not coarse tile blockers.

    public static Vector2 SpawnPoint => Iso(14.5f, 4.5f);

    public static Vector2 PotionFocus => Iso(2.5f,2.5f);
    public static Vector2 SofaFocus => Iso(2.5f,6.8f);

    public static Vector2 TableFocus => Iso(6f, 4f);
    public static Vector2 PcFocus => TableFocus + new Vector2(0, -65);
    public static Vector2 PcApproach => Iso(6f, 5.65f);

    public static Vector2 ChestFocus => Iso(16.6f, 6.5f);
    public static Vector2 ChestApproach => ChestFocus + new Vector2(-45,8);

    /// <summary>Door centre on the room A front (j=8) edge.</summary>
    public const float DoorGridI = 6f;
    public static Vector2 DoorFocus => Iso(DoorGridI, 8f);
    public static Vector2 DoorApproach => Iso(DoorGridI, 7.3f);

    public static Vector2 BedFocus => Iso(13.3f, 3.3f);

    /// <summary>Area used to sanity-check loaded save positions.</summary>
    public static readonly Rect2 PlayableBounds = new(0f, 130f, 830f, 470f);

    // ------------------------------------------------------------------
    // Tiles
    // ------------------------------------------------------------------

    /// <summary>All floor tiles (rooms minus interior wall tiles).</summary>
    public static IEnumerable<(int I, int J)> FloorTiles()
    {
        var wall = new HashSet<(int, int)>(WallTiles);
        foreach (var t in RoomTiles(RoomA)) if (!wall.Contains(t)) yield return t;
        foreach (var t in RoomTiles(RoomB)) yield return t;
    }

    private static IEnumerable<(int I, int J)> RoomTiles(Rect2I room)
    {
        for (var i = room.Position.X; i < room.Position.X + room.Size.X; i++)
        {
            for (var j = room.Position.Y; j < room.Position.Y + room.Size.Y; j++)
            {
                yield return (i, j);
            }
        }
    }

    public static bool IsWallTile(int i, int j) => WallTiles.Contains((i, j));

    /// <summary>The four corner points of a tile diamond, ground level.</summary>
    public static Vector2[] TileDiamond(int i, int j)
    {
        return new[] { Iso(i, j), Iso(i + 1, j), Iso(i + 1, j + 1), Iso(i, j + 1) };
    }

    // ------------------------------------------------------------------
    // Exterior walls
    // ------------------------------------------------------------------

    public readonly record struct WallSegment(Vector2 A, Vector2 B, bool Tall);

    /// <summary>
    /// Exterior wall segments in grid space (converted via Iso). Tall = back
    /// walls drawn at full height; short = front curbs. The door gap on the
    /// room A front edge is already split out.
    /// </summary>
    public static IEnumerable<WallSegment> ExteriorWalls()
    {
        // Tall back walls.
        yield return Wall((0f, 8f), (0f, 0f), tall: true);   // room A west edge
        yield return Wall((0f, 0f), (12f, 0f), tall: true);  // room A north edge
        yield return Wall((12f, 2f), (18f, 2f), tall: true); // room B north edge

        // Low front/side curbs.
        yield return Wall((12f, 0f), (12f, 2f), tall: false);              // room A east sliver
        yield return Wall((18f, 2f), (18f, 8f), tall: false);              // room B east edge
        yield return Wall((18f, 8f), (12f, 8f), tall: false);              // room B south edge
        yield return Wall((12f, 8f), (DoorGridI + .35f, 8f), tall: false);             // room A south edge, right of door
        yield return Wall((DoorGridI - .35f, 8f), (0f, 8f), tall: false);              // room A south edge, left of door
    }

    private static WallSegment Wall((float I, float J) a, (float I, float J) b, bool tall) =>
        new(Iso(a.I, a.J), Iso(b.I, b.J), tall);
}
