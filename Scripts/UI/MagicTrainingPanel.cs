using Godot;
using DevilGate.Scripts.Systems;
namespace DevilGate.Scripts;

/// <summary>Two freely mixed starter spells; changes apply only on confirmation.</summary>
public partial class MagicTrainingPanel : PanelContainer, ICloseable
{
    public event Action? CloseRequested;
    private readonly Dictionary<string,CheckBox> _choices=new();
    private Label _equipped=null!;
    private Label _result=null!;
    private Label _remaining=null!;
    private Button _confirm=null!;
    public string? CloseNotice {get;private set;}
    public string[] SelectedIds=>_choices.Where(p=>p.Value.ButtonPressed).Select(p=>p.Key).ToArray();
    public bool HasUnconfirmedChanges
    {
        get
        {
            var equipped=GameSession.Instance?.CurrentRun?.LearnedSpellIds ?? Array.Empty<string>();
            var selected=SelectedIds;
            return selected.Length!=equipped.Length || !selected.ToHashSet().SetEquals(equipped);
        }
    }
    private static string SkillNames(Data.RunState run,IEnumerable<string> ids)
    {
        var catalog=StarterTraining.Skills(run);
        var names=ids.Select(id=>catalog.FirstOrDefault(s=>s.Id==id)?.Name ?? id).ToArray();
        return names.Length==0?"None":string.Join(", ",names);
    }
    public string? PrepareClose()
    {
        var run=GameSession.Instance?.CurrentRun;
        if(run==null || !HasUnconfirmedChanges){CloseNotice=null;return null;}
        var equipped=run.LearnedSpellIds.Length==2?SkillNames(run,run.LearnedSpellIds):"none equipped";
        CloseNotice=$"Selection not confirmed — equipped pair unchanged ({equipped}).";
        _result.Text=CloseNotice;
        return CloseNotice;
    }
    public override void _Ready()
    {
        var run=GameSession.Instance!.CurrentRun!;
        CustomMinimumSize=new Vector2(850,680);
        AddThemeStyleboxOverride("panel",new StyleBoxFlat {BgColor=new Color("17232f"),ContentMarginLeft=20,ContentMarginRight=20,ContentMarginTop=16,ContentMarginBottom=16,CornerRadiusTopLeft=8,CornerRadiusTopRight=8,CornerRadiusBottomLeft=8,CornerRadiusBottomRight=8});
        var column=new VBoxContainer();column.AddThemeConstantOverride("separation",8);AddChild(column);
        var header=new HBoxContainer();column.AddChild(header);
        header.AddChild(new Label {Text=StarterTraining.IsArcher(run)?"Recover your archer training":StarterTraining.IsHealer(run)?"Recover your healer training":StarterTraining.IsTank(run)?"Remember your vanguard training":"Awaken your magic",SizeFlagsHorizontal=SizeFlags.ExpandFill});
        var close=new Button {Text="Close",CustomMinimumSize=new Vector2(90,36)};header.AddChild(close);
        close.Pressed+=()=>{PrepareClose();CloseRequested?.Invoke();};
        column.AddChild(new Label {Text="Choose two class skills and confirm to equip them. Highlighted changes do not apply until confirmed."});
        _equipped=new Label {Name="EquippedPairLabel",AutowrapMode=TextServer.AutowrapMode.WordSmart};
        _equipped.AddThemeColorOverride("font_color",new Color("a8f0d8"));
        column.AddChild(_equipped);
        _remaining=new Label {Name="SelectionStatusLabel",AutowrapMode=TextServer.AutowrapMode.WordSmart};column.AddChild(_remaining);
        var scroll=new ScrollContainer {CustomMinimumSize=new Vector2(0,320),SizeFlagsVertical=SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};column.AddChild(scroll);
        var list=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill};scroll.AddChild(list);
        foreach(var spell in StarterTraining.Skills(run))
        {
            var box=new CheckBox {Text=$"{spell.Name}  ·  {spell.School}",ButtonPressed=run.LearnedSpellIds.Contains(spell.Id)};
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
            foreach(var (id,box) in _choices)box.SetPressedNoSignal(StarterTraining.SuggestedPair(GameSession.Instance!.CurrentRun!).Contains(id));
            RefreshSelection();
        };
        _confirm=new Button {Text="Confirm two skills"};buttons.AddChild(_confirm);
        _confirm.Pressed+=()=>
        {
            var current=GameSession.Instance!.CurrentRun!;
            if (!StarterTraining.Choose(current,_choices.Where(p=>p.Value.ButtonPressed).Select(p=>p.Key))) return;
            CloseNotice=null;
            GameSession.Instance.EmitChanged();
            RefreshSelection(confirmedNow:true);
        };
        _result=new Label {Name="TrainingResultLabel",AutowrapMode=TextServer.AutowrapMode.WordSmart,CustomMinimumSize=new Vector2(0,70)};column.AddChild(_result);
        RefreshSelection();
    }
    private void RefreshSelection(bool confirmedNow=false)
    {
        var run=GameSession.Instance!.CurrentRun!;
        var skills=StarterTraining.Skills(run);
        var selectedIds=SelectedIds;
        int count=selectedIds.Length;
        bool unconfirmed=HasUnconfirmedChanges;
        string equippedText=run.LearnedSpellIds.Length==2?SkillNames(run,run.LearnedSpellIds):"None yet";
        string selectedText=count>0?SkillNames(run,selectedIds):"None";
        _equipped.Text=unconfirmed
            ? $"Equipped pair (active for battle): {equippedText}   |   Unconfirmed selection: {selectedText}"
            : $"Equipped pair (active for battle): {equippedText}";
        _remaining.Text=count == 2
            ? (unconfirmed ? "Selected: 2/2 (unconfirmed) · Press 'Confirm two skills' to equip this pair" : "Selected: 2/2 (equipped) · Untick a skill to choose another")
            : $"Selected: {count}/2 · Choose two skills, then press 'Confirm two skills'";
        _confirm.Disabled=count!=2;
        foreach(var (id,box) in _choices)
        {
            var spell=skills.Single(s=>s.Id==id);
            bool isEquipped=run.LearnedSpellIds.Contains(id);
            bool selected=box.ButtonPressed;
            box.Text=isEquipped && selected ? $"{spell.Name}  ·  {spell.School}  [Equipped]"
                : isEquipped && !selected ? $"{spell.Name}  ·  {spell.School}  [Equipped · unticked]"
                : !isEquipped && selected ? $"{spell.Name}  ·  {spell.School}  [Selected · unconfirmed]"
                : $"{spell.Name}  ·  {spell.School}";
            box.Disabled=count>=2 && !selected;
            var normal=new StyleBoxFlat
            {
                BgColor=new Color(selected ? (isEquipped ? "245c58" : "234b63") : "1b2937"),
                BorderColor=new Color(selected ? (isEquipped ? "93f1cc" : "7fd4ff") : (isEquipped ? "5ca892" : "344453")),
                BorderWidthLeft=selected || isEquipped ? 4 : 1,
                BorderWidthRight=1,BorderWidthTop=1,BorderWidthBottom=1,
                ContentMarginLeft=8,ContentMarginRight=8,ContentMarginTop=5,ContentMarginBottom=5
            };
            box.AddThemeStyleboxOverride("normal",normal);
            box.AddThemeStyleboxOverride("pressed",normal);
            var hover=(StyleBoxFlat)normal.Duplicate();hover.BgColor=new Color(selected ? "326e67" : "334459");
            box.AddThemeStyleboxOverride("hover",hover);box.AddThemeStyleboxOverride("hover_pressed",hover);
            box.AddThemeColorOverride("font_color",selected ? new Color("d0ffe5") : new Color("d4dce5"));
            box.AddThemeColorOverride("font_pressed_color",new Color("d0ffe5"));
            box.AddThemeColorOverride("font_hover_pressed_color",Colors.White);
        }
        if(confirmedNow)
            _result.Text=$"Skills equipped: {equippedText}.\n{OpeningGuidance.Next(run).Text}";
        else if(unconfirmed)
            _result.Text=run.LearnedSpellIds.Length==2
                ? $"Unconfirmed selection ({selectedText}). Closing without confirming keeps your equipped pair ({equippedText}) unchanged."
                : $"Unconfirmed selection ({selectedText}). Closing without confirming leaves no skills equipped. Press 'Confirm two skills' to equip.";
        else
            _result.Text=run.LearnedSpellIds.Length==2
                ? OpeningGuidance.Next(run).Text
                : "Confirm your pair to equip these skills for battle.";
    }
}
