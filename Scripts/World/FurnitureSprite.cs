using Godot;
namespace DevilGate.Scripts.World;

/// <summary>Samples an isolated atlas cell; ground anchoring is independent of visual height.</summary>
public partial class FurnitureSprite : Node2D
{
    public Node2D? Walker { get; set; }
    public override void _Process(double delta)
    {
        // Reveal a character behind tall artwork, while its ground footprint stays solid.
        bool hiding = Walker != null && Walker.Position.Y < Position.Y &&
            Walker.Position.Y > Position.Y-Width*.78f && Mathf.Abs(Walker.Position.X-Position.X)<Width*.38f;
        var colour=Modulate;
        colour.A=Mathf.MoveToward(colour.A,hiding ? .48f : 1f,(float)delta*4);
        Modulate=colour;
    }
    public string AtlasPath {get;set;}="res://Assets/Environment/studio-furniture-v1.png";
    public int Cell { get; set; }
    public float Width { get; set; } = 120;
    // Draw commands retain a rendering handle, not this managed Resource reference.
    // Keep the atlas alive for as long as this node can be drawn.
    private Texture2D? _texture;
    public override void _Ready() => _texture=GD.Load<Texture2D>(AtlasPath);
    public override void _Draw()
    {
        var texture = _texture;
        if (texture == null) return;
        var cellSize = new Vector2(texture.GetWidth()/3f,texture.GetHeight()/2f);
        var source = new Rect2(new Vector2(Cell%3,Cell/3)*cellSize,cellSize);
        // The sheet contains generous margins; each cell's ground contact is near its bottom.
        var size = new Vector2(Width,Width*cellSize.Y/cellSize.X);
        bool guild = AtlasPath == GuildHallLayout.Atlas;
        var pivot = guild ? GuildHallLayout.Contacts[Cell].Pivot / cellSize : new Vector2(.5f,.94f);
        if (!guild)
        {
            DrawSetTransform(Vector2.Zero, 0, new Vector2(1, .35f));
            DrawCircle(Vector2.Zero, Width * .30f, new Color(0,0,0,.15f));
            DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        }
        // Guild shadows are painted onto the floor before any sorted props/characters;
        // fading an occluding sprite must not lift its shadow off the floor as well.
        DrawTextureRectRegion(texture,new Rect2(-size*pivot,size),source);
    }
}
