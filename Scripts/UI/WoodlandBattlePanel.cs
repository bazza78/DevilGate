using Godot;
using DevilGate.Scripts.Systems;
namespace DevilGate.Scripts;

/// <summary>Visual turn sequencing over the atomic combat resolver; controls lock while a turn animates.</summary>
public partial class WoodlandBattlePanel : PanelContainer, ICloseable
{
    public event Action? CloseRequested;
    public event Action? RetreatRequested;
    public Action? SaveRequested;
    public IDieSource? Dice {get;set;}
    private Label _status=null!,_headline=null!,_playerHealth=null!,_enemyHealth=null!,_mana=null!;
    private RichTextLabel _log=null!;
    private ProgressBar _playerBar=null!,_enemyBar=null!,_energyBar=null!;
    private Button _finish=null!,_retreat=null!,_save=null!;
    private BattleArena _arena=null!;
    private bool _busy;
    public bool IsAnimating=>_busy;
    private readonly Dictionary<Button,string> _actions=new();
    public override void _Ready()
    {
        CustomMinimumSize=new Vector2(880,580);
        AddThemeStyleboxOverride("panel",new StyleBoxFlat {BgColor=new Color("192332"),ContentMarginLeft=20,ContentMarginRight=20,ContentMarginTop=15,ContentMarginBottom=15});
        var column=new VBoxContainer();column.AddThemeConstantOverride("separation",8);AddChild(column);
        var header=new HBoxContainer();column.AddChild(header);
        header.AddChild(new Label {Text=WoodlandBattle.IsRelay(GameSession.Instance!.CurrentRun!)?"RELAY GUARDIAN":"WOODLAND ENCOUNTER",SizeFlagsHorizontal=SizeFlags.ExpandFill});
        var detail=new CheckBox {Text="Show roll log"};
        detail.AddThemeIconOverride("unchecked",GD.Load<Texture2D>("res://Assets/UI/checkbox-empty.svg"));
        detail.AddThemeIconOverride("checked",GD.Load<Texture2D>("res://Assets/UI/checkbox-checked.svg"));
        header.AddChild(detail);
        var vitals=new HBoxContainer();vitals.AddThemeConstantOverride("separation",30);column.AddChild(vitals);
        var left=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill};var right=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill};vitals.AddChild(left);vitals.AddChild(right);
        _playerHealth=new Label();left.AddChild(_playerHealth);_playerBar=Bar(new Color("72d5a0"));_playerBar.Name="PlayerHealthBar";left.AddChild(_playerBar);
        _enemyHealth=new Label();right.AddChild(_enemyHealth);_enemyBar=Bar(new Color("d888b0"));_enemyBar.Name="EnemyHealthBar";right.AddChild(_enemyBar);
        _mana=new Label {Name="BattleEnergyLabel"};_mana.AddThemeFontSizeOverride("font_size",18);_mana.AddThemeColorOverride("font_color",new Color("a3ddff"));left.AddChild(_mana);
        _energyBar=Bar(new Color("64b8ed"));_energyBar.Name="BattleEnergyBar";left.AddChild(_energyBar);
        _arena=new BattleArena {IsConstruct=WoodlandBattle.IsRelay(GameSession.Instance!.CurrentRun!)};column.AddChild(_arena);
        _headline=new Label {Text="Choose a skill or use your free weapon strike.",AutowrapMode=TextServer.AutowrapMode.WordSmart};column.AddChild(_headline);
        _status=new Label {AutowrapMode=TextServer.AutowrapMode.WordSmart};_status.AddThemeFontSizeOverride("font_size",12);column.AddChild(_status);
        var grid=new GridContainer {Columns=3};column.AddChild(grid);
        AddAction(grid,StarterTraining.IsTank(GameSession.Instance!.CurrentRun!)?"Club strike · Free":"Staff strike · Free","staff","4 damage on hit. No energy cost.");
        AddAction(grid,"Guard · Free","guard","+4 defence against the next attack.");
        AddAction(grid,"Health potion","potion","Use a carried health potion to restore up to 15 HP. Uses your turn.");
        foreach(var id in GameSession.Instance!.CurrentRun!.LearnedSpellIds)
        {
            var spell=StarterTraining.Skills(GameSession.Instance!.CurrentRun!).Single(s=>s.Id==id);
            var effect=id switch {"ember_bolt"=>"8 damage","frost_touch"=>"4 damage, slow 2 turns","static_spark"=>"6 damage","withering_touch"=>"4 damage, heal 3","crushing_blow"=>"8 damage on hit","shield_bash"=>"4 damage; enemy −2 attack for 2 turns","iron_stance"=>"Absorb 2 damage per hit for 3 enemy turns; automatic","second_wind"=>"Restore up to 8 HP; automatic","arcane_ward"=>"+4 defence for 3 enemy turns: reduces hit chance, not damage. Wisp hits deal 4 HP","shadow_veil"=>"+3 defence, 3 turns","mage_light"=>"3 damage, expose weakness",_=>""};
            AddAction(grid,spell.Name+" · 3 "+StarterTraining.Resource(GameSession.Instance!.CurrentRun!).ToLowerInvariant(),id,effect);
        }
        _log=new RichTextLabel {Visible=false,CustomMinimumSize=new Vector2(0,75),ScrollFollowing=true,Text="Dice results appear here after each turn."};column.AddChild(_log);detail.Toggled+=shown=>_log.Visible=shown;
        var footer=new HBoxContainer();column.AddChild(footer);
        _save=new Button {Text="Save · 1 crystal"};footer.AddChild(_save);_save.Pressed+=()=>{if(_busy)return;SaveRequested?.Invoke();Refresh();};
        _retreat=new Button {Text="Retreat to safety"};footer.AddChild(_retreat);
        _retreat.Pressed+=()=>
        {
            if(_busy || !WoodlandBattle.Retreat(GameSession.Instance!.CurrentRun!))return;
            GameSession.Instance.EmitChanged();CloseRequested?.Invoke();RetreatRequested?.Invoke();
        };
        _finish=new Button {Text="Continue"};footer.AddChild(_finish);_finish.Pressed+=RequestClose;
        GameSession.Instance!.StateChanged+=Refresh;Refresh();
    }
    private static ProgressBar Bar(Color fill)
    {
        var bar=new ProgressBar {CustomMinimumSize=new Vector2(0,18),ShowPercentage=false,SizeFlagsHorizontal=SizeFlags.ExpandFill};
        bar.AddThemeStyleboxOverride("background",new StyleBoxFlat {BgColor=new Color("0c1720"),CornerRadiusTopLeft=5,CornerRadiusTopRight=5,CornerRadiusBottomLeft=5,CornerRadiusBottomRight=5});
        bar.AddThemeStyleboxOverride("fill",new StyleBoxFlat {BgColor=fill,CornerRadiusTopLeft=5,CornerRadiusTopRight=5,CornerRadiusBottomLeft=5,CornerRadiusBottomRight=5});return bar;
    }
    private void AddAction(GridContainer row,string name,string id,string tooltip)
    {
        var button=new Button {Text=name,TooltipText=tooltip,CustomMinimumSize=new Vector2(0,35),SizeFlagsHorizontal=SizeFlags.ExpandFill};row.AddChild(button);_actions[button]=id;
        button.Pressed+=async ()=>await AnimateTurn(id);
    }
    private async Task Pause(double seconds)=>await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);
    private async Task ChangeBar(ProgressBar bar,int target,Label label,string name,int maximum)
    {
        var tween=CreateTween();tween.TweenProperty(bar,"value",(double)target,.28).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        await ToSignal(tween,Tween.SignalName.Finished);label.Text=$"{name} · {target}/{maximum}";
    }
    private async Task AnimateTurn(string id)
    {
        if(_busy)return;
        var r=GameSession.Instance!.CurrentRun!;
        if(WoodlandBattle.Current(r).Stage!=1)return;
        _busy=true;Refresh();
        int previousHp=r.Character.Hp,previousEnemy=WoodlandBattle.Current(r).EnemyHp;
        var feedback=new BattleTurnFeedback();var summary=WoodlandBattle.Act(r,id,Dice,feedback);
        _log.AppendText("\n\n"+summary);
        _mana.Text=$"{StarterTraining.Resource(r)} {r.Character.Mana} / {CharacterRules.MaxMana(r.Character)} · Skills cost 3";
        _energyBar.MaxValue=CharacterRules.MaxMana(r.Character);_energyBar.Value=r.Character.Mana;
        if(!feedback.ActionResolved){_headline.Text=summary;_busy=false;Refresh();return;}
        _headline.Text=id=="staff"?"You strike with your weapon…":id=="guard"?"You brace for the attack…":id=="potion"?"You drink a health potion…":StarterTraining.Skills(GameSession.Instance!.CurrentRun!).Single(s=>s.Id==id).Name+"…";
        _arena.Play(id);
        if(feedback.PlayerRoll is int roll)_arena.Float($"d20 {roll} · {(feedback.PlayerHit==true?"hit":"miss")}",false,new Color("d8e6ed"),12,-18);
        await Pause(.6);
        int enemyDamage=previousEnemy-WoodlandBattle.Current(r).EnemyHp;
        if(enemyDamage>0)_arena.Float("−"+enemyDamage,true,new Color("ffb88f"),26);
        else if(feedback.PlayerHit==false)_arena.Float(id is "arcane_ward" or "shadow_veil" ? "FIZZLE" : "MISS",id is not ("arcane_ward" or "shadow_veil"),new Color("bfccd9"),18);
        await ChangeBar(_enemyBar,WoodlandBattle.Current(r).EnemyHp,_enemyHealth,WoodlandBattle.EnemyName(r),WoodlandBattle.EnemyMaxHp(r));
            int healed=Math.Min(CharacterRules.MaxHealth(r.Character),previousHp+feedback.PlayerHealing);
            if(healed>previousHp){_arena.Float("+"+(healed-previousHp),false,new Color("9cf5b3"),22);await ChangeBar(_playerBar,healed,_playerHealth,r.Character.Name,CharacterRules.MaxHealth(r.Character));}
        if(WoodlandBattle.Current(r).Stage==2)
        {
            _arena.EnemyDefeated=true;await Pause(.5);_headline.Text=WoodlandBattle.VictorySummary(r);
            _playerBar.MaxValue=CharacterRules.MaxHealth(r.Character);
            await ChangeBar(_playerBar,r.Character.Hp,_playerHealth,r.Character.Name,CharacterRules.MaxHealth(r.Character));
        }
        else
        {
            // Healing occurs before the enemy responds; display that intermediate health explicitly.
            _headline.Text=WoodlandBattle.EnemyName(r)+" retaliates…";_arena.Play("enemy");
            if(feedback.EnemyRoll is int enemyRoll)_arena.Float($"d20 {enemyRoll}",true,new Color("d8c7f4"),12,-18);
            await Pause(.6);
            if(feedback.EnemyHit)
            {
                _arena.Float("−"+feedback.EnemyDamageTaken,false,new Color("ff9797"),26);
                if(feedback.EnemyDamageBlocked>0)_arena.Float($"Blocked {feedback.EnemyDamageBlocked}",false,new Color("8bdaef"),14,24);
            }
            else _arena.Float("MISS",false,new Color("c1d9d6"),18);
            await ChangeBar(_playerBar,r.Character.Hp,_playerHealth,r.Character.Name,CharacterRules.MaxHealth(r.Character));
            _arena.PlayerDefeated=WoodlandBattle.Current(r).Stage==3;
            _headline.Text=WoodlandBattle.Current(r).Stage==3?"You fall. A guild patrol can bring you back to safety.":feedback.EnemyHit?$"{WoodlandBattle.EnemyName(r)} hit: {feedback.EnemyRawDamage} base − {feedback.EnemyDamageBlocked} absorbed = {feedback.EnemyDamageTaken} HP. Your turn.":"Attack missed · 0 HP damage. Your turn.";
        }
        _busy=false;GameSession.Instance.EmitChanged();Refresh();
    }
    private void Refresh()
    {
        var r=GameSession.Instance!.CurrentRun!;var b=WoodlandBattle.Current(r);
        _playerBar.MaxValue=CharacterRules.MaxHealth(r.Character);_enemyBar.MaxValue=WoodlandBattle.EnemyMaxHp(r);
        if(!_busy)
        {
            _playerBar.Value=r.Character.Hp;_enemyBar.Value=b.EnemyHp;
            _playerHealth.Text=$"{r.Character.Name} · {r.Character.Hp}/{_playerBar.MaxValue}";
            _enemyHealth.Text=$"{WoodlandBattle.EnemyName(r)} · {b.EnemyHp}/{WoodlandBattle.EnemyMaxHp(r)}";
            _arena.EnemyDefeated=b.Stage==2;_arena.PlayerDefeated=b.Stage==3;
            if(b.Stage==2)_headline.Text=WoodlandBattle.VictorySummary(r);
            if(b.Stage==3)_headline.Text="Defeated · a guild patrol can bring you to safety.";
        }
        _mana.Text=$"{StarterTraining.Resource(r)} {r.Character.Mana} / {CharacterRules.MaxMana(r.Character)} · Skills cost 3";
        _energyBar.MaxValue=CharacterRules.MaxMana(r.Character);_energyBar.Value=r.Character.Mana;
        _status.Text="";
        if(b.Ward>0)_status.Text+=$"Ward: +4 defence (hit chance only), {b.Ward} turns   ";
        if(b.IronStance>0)_status.Text+=$"Iron Stance: absorb 2 per hit, {b.IronStance} turns   ";
        if(b.Veil>0)_status.Text+=$"Veil: {b.Veil} turns   ";
        if(b.Frost>0)_status.Text+=$"Enemy attack −2: {b.Frost} turns   ";
        foreach(var (button,id) in _actions)button.Disabled=_busy || b.Stage!=1 || r.Character.Mana<WoodlandBattle.Cost(id);
        _save.Disabled=_busy || GameSession.Instance.CarriedSaveCrystals()<=0;
        _finish.Visible=b.Stage==2;_finish.Disabled=_busy;
        _retreat.Visible=b.Stage is 1 or 3;_retreat.Disabled=_busy;
        _retreat.Text=b.Stage==3?"Accept guild rescue":"Retreat to safety";
    }
    public void RequestClose(){if(!_busy && WoodlandBattle.Current(GameSession.Instance!.CurrentRun!).Stage==2)CloseRequested?.Invoke();}
    public override void _ExitTree(){if(GameSession.Instance!=null)GameSession.Instance.StateChanged-=Refresh;}
}
