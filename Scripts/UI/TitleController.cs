using Godot;
using DevilGate.Scripts.Systems;
using DevilGate.Scripts.World;

namespace DevilGate.Scripts;

public partial class TitleController : Control
{
    private Label _statusLabel = null!;

    public override void _Ready()
    {
        BuildPresentation();
        _statusLabel = GetNode<Label>("%StatusLabel");
        GetNode<Button>("CenterContainer/VBox/NewCharacterButton").Pressed += OnNewCharacterPressed;
        GetNode<Button>("CenterContainer/VBox/QuitButton").Pressed += OnQuitPressed;

        var loadButton = GetNode<Button>("%LoadButton");
        loadButton.Text = "Continue";
        loadButton.Pressed += OnLoadPressed;
        GetNode<Button>("CenterContainer/VBox/NewCharacterButton").GrabFocus();
        if (SaveService.TryLoadSave(out var save, out _) && save != null)
        {
            var run = save.ToRunState();
            var card = new PanelContainer { Name = "ContinueSummary" };
            card.AddThemeStyleboxOverride("panel",new StyleBoxFlat { BgColor=new Color("142a39e8"),BorderColor=new Color("365b66"),BorderWidthLeft=2,ContentMarginLeft=12,ContentMarginRight=12,ContentMarginTop=10,ContentMarginBottom=10,CornerRadiusTopLeft=5,CornerRadiusTopRight=5,CornerRadiusBottomLeft=5,CornerRadiusBottomRight=5 });
            loadButton.GetParent().AddChild(card);
            loadButton.GetParent().MoveChild(card, loadButton.GetIndex() + 1);
            var summary = new HBoxContainer(); summary.AddThemeConstantOverride("separation",12); card.AddChild(summary);
            summary.AddChild(new TextureRect { Texture = Definitions.GameCatalog.Instance.GetArchetype(save.ArchetypeId).LoadPortrait(save.GenderId), CustomMinimumSize = new Vector2(58,76), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
            string time = DateTimeOffset.TryParse(save.SavedAtUtc, out var saved) ? saved.ToLocalTime().ToString("g") : "Time unavailable (older save)";
            summary.AddChild(new Label { Text = $"{save.Name} · Level {save.Level} · {WorldRoute.Name(save.LocationId)}\n{OpeningGuidance.Next(run).Text.Split('\n')[0]}\n{time}", SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(320,0), AutowrapMode = TextServer.AutowrapMode.WordSmart });
        }
        loadButton.Disabled = !(GameSession.Instance?.HasSave() ?? false);
        if (loadButton.Disabled)
        {
            loadButton.TooltipText = "No save file yet. Begin an adventure and use a carried crystal to record your progress.";
            var empty = new Label {Text="No saved memory yet. Your first adventure awaits.",AutowrapMode=TextServer.AutowrapMode.WordSmart};
            empty.AddThemeFontSizeOverride("font_size",13); empty.AddThemeColorOverride("font_color",new Color("a2b9c5"));
            loadButton.GetParent().AddChild(empty); loadButton.GetParent().MoveChild(empty,loadButton.GetIndex()+1);
        }
    }

    private void BuildPresentation()
    {
        var background=new TitleBackdrop(); AddChild(background); MoveChild(background,0);
        var center=GetNode<CenterContainer>("CenterContainer");
        center.AnchorRight=.52f; center.OffsetLeft=64; center.OffsetRight=0; center.OffsetTop=28; center.OffsetBottom=-55;
        var column=GetNode<VBoxContainer>("CenterContainer/VBox"); column.CustomMinimumSize=new Vector2(490,0); column.AddThemeConstantOverride("separation",12);
        var title=column.GetNode<Label>("TitleLabel"); title.HorizontalAlignment=HorizontalAlignment.Left; title.AddThemeFontSizeOverride("font_size",68); title.AddThemeColorOverride("font_color",new Color("ecf4ee"));
        var subtitle=column.GetNode<Label>("Subtitle"); subtitle.Text="MAGIC IN THE STATIC. A LIFE THAT ISN’T YOURS."; subtitle.HorizontalAlignment=HorizontalAlignment.Left; subtitle.AddThemeFontSizeOverride("font_size",12); subtitle.AddThemeColorOverride("font_color",new Color("85dccf"));
        column.GetNode<Control>("Spacer").CustomMinimumSize=new Vector2(0,8);
        var intro=new Label {Text="Another world. An unfamiliar face.\nFind your place beyond the gate.",AutowrapMode=TextServer.AutowrapMode.WordSmart}; intro.AddThemeFontSizeOverride("font_size",20); intro.AddThemeColorOverride("font_color",new Color("c6d6df")); column.AddChild(intro); column.MoveChild(intro,3);
        foreach(var name in new[]{"NewCharacterButton","LoadButton","QuitButton"})
        {
            var button=column.GetNode<Button>(name); bool primary=name=="NewCharacterButton";
            button.CustomMinimumSize=new Vector2(0,name=="QuitButton"?36:50);
            var style=new StyleBoxFlat {BgColor=new Color(primary?"6bd5c0":"142735dc"),BorderColor=new Color(primary?"99f0d5":"42616c"),BorderWidthLeft=1,BorderWidthTop=1,BorderWidthRight=1,BorderWidthBottom=1,CornerRadiusTopLeft=5,CornerRadiusTopRight=5,CornerRadiusBottomLeft=5,CornerRadiusBottomRight=5,ContentMarginLeft=18,ContentMarginRight=18,ContentMarginTop=8,ContentMarginBottom=8};
            button.AddThemeStyleboxOverride("normal",style);
            var hover=(StyleBoxFlat)style.Duplicate();hover.BgColor=style.BgColor.Lightened(.12f);button.AddThemeStyleboxOverride("hover",hover);
            var pressed=(StyleBoxFlat)style.Duplicate();pressed.BgColor=style.BgColor.Darkened(.12f);button.AddThemeStyleboxOverride("pressed",pressed);
            foreach(var state in new[]{"font_color","font_hover_color","font_pressed_color","font_focus_color"})button.AddThemeColorOverride(state,new Color(primary?"0a292e":"deecee"));
            button.AddThemeFontSizeOverride("font_size",16);
        }
        GetNode<Label>("%StatusLabel").CustomMinimumSize=new Vector2(0,0);
        var footer=new Label {Text="Mage · Tank · Archer · Healer · Knight   /   Choose your identity in the mirror",Position=new Vector2(64,0),MouseFilter=MouseFilterEnum.Ignore};
        footer.AnchorTop=1;footer.AnchorBottom=1;footer.OffsetTop=-29;footer.AddThemeFontSizeOverride("font_size",12);footer.AddThemeColorOverride("font_color",new Color("a9c6cb"));AddChild(footer);
        var chapter=new VBoxContainer {MouseFilter=MouseFilterEnum.Ignore}; chapter.AnchorLeft=.61f;chapter.AnchorRight=.94f;chapter.AnchorTop=.78f;chapter.AnchorBottom=.94f;AddChild(chapter);
        var kicker=new Label {Text="THE OPENING CHAPTER"};kicker.AddThemeFontSizeOverride("font_size",12);kicker.AddThemeColorOverride("font_color",new Color("a7e5d7"));chapter.AddChild(kicker);
        var nameLabel=new Label {Text="Echo in the Filters"};nameLabel.AddThemeFontSizeOverride("font_size",28);nameLabel.AddThemeColorOverride("font_shadow_color",new Color("0b1b2b"));nameLabel.AddThemeConstantOverride("shadow_offset_x",2);nameLabel.AddThemeConstantOverride("shadow_offset_y",2);chapter.AddChild(nameLabel);
        chapter.AddChild(new Label {Text="Explore the city’s edge. Follow the signal.",AutowrapMode=TextServer.AutowrapMode.WordSmart});
    }

    private void OnNewCharacterPressed()
    {
        if (HasUnsavedProgress())
        {
            ConfirmDialogHelper.Show(this, "Unsaved progress",
                "Your current character has progress that hasn't been saved. Start a new character anyway?",
                () => BeginAdventure());
            return;
        }
        BeginAdventure();
    }

    private void BeginAdventure()
    {
        GameSession.Instance?.BeginAdventure();
        GetParent<MainController>()?.ShowSafehouse();
    }

    private void OnLoadPressed()
    {
        var session = GameSession.Instance;
        if (session == null) return;

        if (!session.TryLoadGame(out var error))
        {
            _statusLabel.Text = $"Load failed: {error}";
            return;
        }
        _statusLabel.Text = "";
        GetParent<MainController>()?.ShowSafehouse();
    }

    private void OnQuitPressed()
    {
        if (HasUnsavedProgress())
        {
            ConfirmDialogHelper.Show(this, "Unsaved progress",
                "You have progress that hasn't been saved. Quit anyway?",
                () => GetTree().Quit());
            return;
        }
        GetTree().Quit();
    }

    private static bool HasUnsavedProgress() =>
        GameSession.Instance?.HasUnsavedChanges ?? false;
}
