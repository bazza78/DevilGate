using Godot;
namespace DevilGate.Scripts;

/// <summary>Existing city art with a readable menu scrim; no additional image assets.</summary>
public partial class TitleBackdrop : Control
{
    private Texture2D? _city;
    public override void _Ready()
    {
        MouseFilter=MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _city=GD.Load<Texture2D>("res://Assets/Environment/city-window-v1.png");
        Resized+=QueueRedraw;
    }
    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero,Size),new Color("0a1420"));
        if(_city!=null)
        {
            float scale=Mathf.Max(Size.X/_city.GetWidth(),Size.Y/_city.GetHeight());
            var imageSize=_city.GetSize()*scale;
            DrawTextureRect(_city,new Rect2((Size-imageSize)*.5f,imageSize),false,new Color(.8f,.87f,.93f));
        }
        var dark=new Color(.025f,.055f,.085f,.97f);
        DrawRect(new Rect2(0,0,Size.X*.25f,Size.Y),dark);
        DrawPolygon(new[]{new Vector2(Size.X*.25f,0),new Vector2(Size.X*.85f,0),new Vector2(Size.X*.85f,Size.Y),new Vector2(Size.X*.25f,Size.Y)},new[]{dark,new Color(dark,0),new Color(dark,0),dark});
        DrawRect(new Rect2(0,0,Size.X,7),new Color("79d9cb"));
        DrawRect(new Rect2(0,Size.Y-40,Size.X,40),new Color(.025f,.055f,.085f,.75f));
        DrawLine(new Vector2(Size.X*.6f,Size.Y*.76f),new Vector2(Size.X*.92f,Size.Y*.76f),new Color("b6d6d4",.5f),1,true);
    }
}
