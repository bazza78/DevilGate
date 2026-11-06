using Godot;

namespace DevilGate.Scripts.World;

/// <summary>
/// One interior partition wall block (a full wall tile). Drawn as a raised
/// diamond with shaded faces. Origin = the tile's bottom vertex so y-sorting
/// places it correctly among props and the player.
/// </summary>
public partial class InteriorWallBlock : Node2D
{
    private static readonly Color TopColor = new("d8d4cb");
    private static readonly Color FaceNorth = new("333a4a");
    private static readonly Color FaceEast = new("3d4457");
    private static readonly Color FaceSouth = new("9fa5aa");
    private static readonly Color FaceWest = new("858f9b");

    private Vector2[] _diamond = System.Array.Empty<Vector2>();
    private float _height;

    public InteriorWallBlock(int i, int j, float height = SafehouseLayout.WallBlockHeight)
    {
        Position = SafehouseLayout.Iso(i + 1, j + 1); // bottom vertex = ground contact
        _height = height;
        var raw = SafehouseLayout.TileDiamond(i, j);
        _diamond = new Vector2[4];
        for (var k = 0; k < 4; k++)
        {
            _diamond[k] = raw[k] - Position;
        }
    }

    public override void _Draw()
    {
        var up = new Vector2(0, -_height);
        // Faces for each edge (diamond order: top, right, bottom, left).
        DrawFace(_diamond[0], _diamond[1], FaceEast);
        DrawFace(_diamond[1], _diamond[2], FaceSouth);
        DrawFace(_diamond[2], _diamond[3], FaceWest);
        DrawFace(_diamond[3], _diamond[0], FaceNorth);

        // Top cap.
        DrawColoredPolygon(new[]
        {
            _diamond[0] + up, _diamond[1] + up, _diamond[2] + up, _diamond[3] + up,
        }, TopColor);
    }

    private void DrawFace(Vector2 a, Vector2 b, Color color)
    {
        DrawColoredPolygon(new[] { a, b, b + new Vector2(0, -_height), a + new Vector2(0, -_height) }, color);
    }
}
