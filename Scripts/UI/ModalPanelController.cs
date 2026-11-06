using Godot;

namespace DevilGate.Scripts;

/// <summary>
/// Reusable centred modal panel: title, wrapping body text, dynamic action
/// buttons and a Close button. The owning InteractionController stacks these
/// and blocks world input while any are open.
/// </summary>
public partial class ModalPanelController : PanelContainer, ICloseable
{
    private Label TitleLabel => GetNode<Label>("%TitleLabel");
    private Label BodyLabel => GetNode<Label>("%BodyLabel");
    private HBoxContainer ActionButtons => GetNode<HBoxContainer>("VBox/ButtonRow/ActionButtons");
    private Button CloseButton => GetNode<Button>("%CloseButton");

    /// <summary>Fired by the Close button; the stack owner handles popping.</summary>
    public event Action? CloseRequested;

    public override void _Ready()
    {
        CloseButton.Pressed += () => CloseRequested?.Invoke();
    }

    /// <summary>Safe to call before the panel enters the tree.</summary>
    public void Setup(string title, string body)
    {
        TitleLabel.Text = title;
        BodyLabel.Text = body;
    }

    /// <summary>Optional speaker portrait; other modal layouts keep their compact form.</summary>
    public void SetPortrait(string path)
    {
        var texture=GD.Load<Texture2D>(path);
        if(texture==null)return;
        CustomMinimumSize=new Vector2(860,450);
        var column=GetNode<VBoxContainer>("VBox");
        var content=new HBoxContainer {SizeFlagsVertical=SizeFlags.ExpandFill};
        content.AddThemeConstantOverride("separation",22);column.AddChild(content);column.MoveChild(content,1);
        var frame=new PanelContainer {CustomMinimumSize=new Vector2(250,330)};
        frame.AddThemeStyleboxOverride("panel",new StyleBoxFlat {BgColor=new Color("111c28"),BorderColor=new Color("598b91"),BorderWidthLeft=1,BorderWidthRight=1,BorderWidthTop=1,BorderWidthBottom=1,ContentMarginLeft=5,ContentMarginRight=5,ContentMarginTop=5,ContentMarginBottom=5});
        content.AddChild(frame);
        frame.AddChild(new TextureRect {Name="SpeakerPortrait",Texture=texture,ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,MouseFilter=MouseFilterEnum.Ignore});
        var words=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill};content.AddChild(words);
        var body=BodyLabel;
        body.UniqueNameInOwner=false;body.Owner=null;
        body.Reparent(words);body.Owner=this;body.UniqueNameInOwner=true;
        body.SizeFlagsVertical=SizeFlags.ExpandFill;
        body.VerticalAlignment=VerticalAlignment.Center;
    }

    public void SetBody(string body) => BodyLabel.Text = body;

    /// <summary>Safe to call before the panel enters the tree.</summary>
    public Button AddButton(string text, Action onClick)
    {
        var button = new Button { Text = text };
        button.Pressed += () => onClick();
        ActionButtons.AddChild(button);
        return button;
    }
}
