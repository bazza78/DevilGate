using Godot;
using DevilGate.Scripts.Systems;
namespace DevilGate.Scripts;

/// <summary>Two freely mixed starter spells; changes apply only on confirmation.</summary>
public partial class MagicTrainingPanel : PanelContainer, ICloseable
{
    public event Action? CloseRequested;
    private readonly Dictionary<string,CheckBox> _choices=new();
    private Label _result=null!;
    private Label _remaining=null!;
    private Button _confirm=null!;
    public override void _Ready()
    {
        CustomMinimumSize=new Vector2(850,680);
        AddThemeStyleboxOverride("panel",new StyleBoxFlat {BgColor=new Color("17232f"),ContentMarginLeft=20,ContentMarginRight=20,ContentMarginTop=16,ContentMarginBottom=16,CornerRadiusTopLeft=8,CornerRadiusTopRight=8,CornerRadiusBottomLeft=8,CornerRadiusBottomRight=8});
        var column=new VBoxContainer();column.AddThemeConstantOverride("separation",8);AddChild(column);
        var header=new HBoxContainer();column.AddChild(header);
        header.AddChild(new Label {Text=StarterTraining.IsTank(GameSession.Instance!.CurrentRun!)?"Remember your vanguard training":"Awaken your magic",SizeFlagsHorizontal=SizeFlags.ExpandFill});
        var close=new Button {Text="Close",CustomMinimumSize=new Vector2(90,36)};header.AddChild(close);close.Pressed+=()=>CloseRequested?.Invoke();
        column.AddChild(new Label {Text="Choose exactly two class skills. You can change them here for free during testing."});
        _remaining=new Label();column.AddChild(_remaining);
        var scroll=new ScrollContainer {CustomMinimumSize=new Vector2(0,320),SizeFlagsVertical=SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};column.AddChild(scroll);
        var list=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill};scroll.AddChild(list);
        foreach(var spell in StarterTraining.Skills(GameSession.Instance!.CurrentRun!))
        {
            var box=new CheckBox {Text=$"{spell.Name}  ·  {spell.School}",ButtonPressed=GameSession.Instance!.CurrentRun!.LearnedSpellIds.Contains(spell.Id)};
            box.AddThemeIconOverride("unchecked",GD.Load<Texture2D>("res://Assets/UI/checkbox-empty.svg"));
            box.AddThemeIconOverride("checked",GD.Load<Texture2D>("res://Assets/UI/checkbox-checked.svg"));
            box.AddThemeIconOverride("unchecked_disabled",GD.Load<Texture2D>("res://Assets/UI/checkbox-disabled.svg"));
            box.AddThemeIconOverride("checked_disabled",GD.Load<Texture2D>("res://Assets/UI/checkbox-checked.svg"));
            _choices[spell.Id]=box;list.AddChild(box);
            var description=new Label {Text=spell.Description,AutowrapMode=TextServer.AutowrapMode.WordSmart};description.AddThemeFontSizeOverride("font_size",13);list.AddChild(description);
            box.Toggled+=_=>RefreshSelection();
        }
        var buttons=new HBoxContainer();column.AddChild(buttons);
        var suggest=new Button {Text="Suggest balanced pair"};buttons.AddChild(suggest);
        suggest.Pressed+=()=>
        {
            foreach(var (id,box) in _choices)box.SetPressedNoSignal(StarterTraining.IsTank(GameSession.Instance!.CurrentRun!) ? id is "crushing_blow" or "iron_stance" : id is "ember_bolt" or "arcane_ward");
            RefreshSelection();
        };
        _confirm=new Button {Text="Confirm two skills"};buttons.AddChild(_confirm);
        _confirm.Pressed+=()=>
        {
            var run=GameSession.Instance!.CurrentRun!;
            if (!StarterTraining.Choose(run,_choices.Where(p=>p.Value.ButtonPressed).Select(p=>p.Key))) return;
            GameSession.Instance.EmitChanged();
            _result.Text="Skills equipped.\n"+OpeningGuidance.Next(run).Text;
        };
        _result=new Label {Text=GameSession.Instance!.CurrentRun!.LearnedSpellIds.Length==2?OpeningGuidance.Next(GameSession.Instance.CurrentRun).Text:"Confirm your pair to equip these skills for battle.",AutowrapMode=TextServer.AutowrapMode.WordSmart,CustomMinimumSize=new Vector2(0,70)};column.AddChild(_result);
        RefreshSelection();
    }
    private void RefreshSelection()
    {
        int count=_choices.Values.Count(b=>b.ButtonPressed);
        _remaining.Text=count == 2 ? "Selected: 2/2 · Untick a skill to choose another" : $"Selected: {count}/2 · Choose an attack and a support skill, or experiment";
        _confirm.Disabled=count!=2;
        foreach(var box in _choices.Values)
        {
            box.Disabled=count>=2 && !box.ButtonPressed;
            var selected=box.ButtonPressed;
            var normal=new StyleBoxFlat {BgColor=new Color(selected ? "245c58" : "1b2937"),BorderColor=new Color(selected ? "93f1cc" : "344453"),BorderWidthLeft=selected?4:1,BorderWidthRight=1,BorderWidthTop=1,BorderWidthBottom=1,ContentMarginLeft=8,ContentMarginRight=8,ContentMarginTop=5,ContentMarginBottom=5};
            box.AddThemeStyleboxOverride("normal",normal);
            box.AddThemeStyleboxOverride("pressed",normal);
            var hover=(StyleBoxFlat)normal.Duplicate();hover.BgColor=new Color(selected ? "326e67" : "334459");
            box.AddThemeStyleboxOverride("hover",hover);box.AddThemeStyleboxOverride("hover_pressed",hover);
            box.AddThemeColorOverride("font_color",selected ? new Color("d0ffe5") : new Color("d4dce5"));
            box.AddThemeColorOverride("font_pressed_color",new Color("d0ffe5"));
            box.AddThemeColorOverride("font_hover_pressed_color",Colors.White);
        }
    }
}
