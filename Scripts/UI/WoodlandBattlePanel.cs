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
    private Label _status=null!,_headline=null!,_playerHealth=null!,_enemyHealth=null!,_mana=null!,_turnLabel=null!,_unavailableLabel=null!;
    private HBoxContainer _statusChips=null!;
    private PanelContainer _hintBar=null!,_itemChooser=null!;
    private Button _dismissHint=null!,_itemsToggle=null!,_hpPotionButton=null!,_mpPotionButton=null!,_closeItemsButton=null!;
    private RichTextLabel _log=null!;
    private ProgressBar _playerBar=null!,_enemyBar=null!,_energyBar=null!;
    private Button _finish=null!,_retreat=null!,_save=null!;
    private BattleArena _arena=null!;
    private bool _busy,_enemyTurnPhase,_itemChooserOpen;
    public bool IsAnimating=>_busy;
    public bool IsItemChooserOpen=>_itemChooserOpen;
    private readonly Dictionary<Button,string> _actions=new();
    private readonly Dictionary<Button,string> _baseTooltips=new();
    public IReadOnlyList<string> ActionOrder=>_actions.Values.ToArray();
    public override void _Ready()
    {
        var run=GameSession.Instance!.CurrentRun!;
        bool tank=StarterTraining.IsTank(run);
        CustomMinimumSize=new Vector2(880,580);
        AddThemeStyleboxOverride("panel",new StyleBoxFlat {BgColor=new Color("192332"),ContentMarginLeft=20,ContentMarginRight=20,ContentMarginTop=15,ContentMarginBottom=15});
        var column=new VBoxContainer();column.AddThemeConstantOverride("separation",6);AddChild(column);
        var header=new HBoxContainer(); header.AddThemeConstantOverride("separation",14); column.AddChild(header);
        header.AddChild(new Label {Text=WoodlandBattle.IsRelay(run)?"RELAY GUARDIAN":"WOODLAND ENCOUNTER",SizeFlagsHorizontal=SizeFlags.ExpandFill});
        _turnLabel=new Label {Name="BattleTurnLabel"};
        _turnLabel.AddThemeFontSizeOverride("font_size",14);
        header.AddChild(_turnLabel);
        var detail=new CheckBox {Text="Show roll log"};
        detail.AddThemeIconOverride("unchecked",GD.Load<Texture2D>("res://Assets/UI/checkbox-empty.svg"));
        detail.AddThemeIconOverride("checked",GD.Load<Texture2D>("res://Assets/UI/checkbox-checked.svg"));
        header.AddChild(detail);

        _hintBar=new PanelContainer {Name="FirstCombatHintBar"};
        _hintBar.AddThemeStyleboxOverride("panel",new StyleBoxFlat {BgColor=new Color("122534"),BorderColor=new Color("46a8c8"),BorderWidthLeft=3,BorderWidthTop=1,BorderWidthRight=1,BorderWidthBottom=1,CornerRadiusTopLeft=5,CornerRadiusTopRight=5,CornerRadiusBottomLeft=5,CornerRadiusBottomRight=5,ContentMarginLeft=10,ContentMarginRight=10,ContentMarginTop=6,ContentMarginBottom=6});
        var hintRow=new HBoxContainer();hintRow.AddThemeConstantOverride("separation",10);_hintBar.AddChild(hintRow);
        var hintText=new Label {Name="FirstCombatHintLabel",Text="First combat tip: Weapon strikes and Guard are free. Class skills cost 3 energy. The enemy responds after each action.",SizeFlagsHorizontal=SizeFlags.ExpandFill,AutowrapMode=TextServer.AutowrapMode.WordSmart};
        hintText.AddThemeFontSizeOverride("font_size",12);hintText.AddThemeColorOverride("font_color",new Color("c8ecff"));
        hintRow.AddChild(hintText);
        _dismissHint=new Button {Name="DismissHintButton",Text="Dismiss hint",CustomMinimumSize=new Vector2(100,26)};
        _dismissHint.Pressed+=()=>{if(_busy)return;GameSession.Instance!.CurrentRun!.BattleHintDismissed=true;GameSession.Instance.EmitChanged();};
        hintRow.AddChild(_dismissHint);
        column.AddChild(_hintBar);

        var vitals=new HBoxContainer();vitals.AddThemeConstantOverride("separation",30);column.AddChild(vitals);
        var left=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill};var right=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill};vitals.AddChild(left);vitals.AddChild(right);
        _playerHealth=new Label();left.AddChild(_playerHealth);_playerBar=Bar(new Color("72d5a0"));_playerBar.Name="PlayerHealthBar";left.AddChild(_playerBar);
        _enemyHealth=new Label();right.AddChild(_enemyHealth);_enemyBar=Bar(new Color("d888b0"));_enemyBar.Name="EnemyHealthBar";right.AddChild(_enemyBar);
        _mana=new Label {Name="BattleEnergyLabel"};_mana.AddThemeFontSizeOverride("font_size",18);_mana.AddThemeColorOverride("font_color",new Color("a3ddff"));left.AddChild(_mana);
        _energyBar=Bar(new Color("64b8ed"));_energyBar.Name="BattleEnergyBar";left.AddChild(_energyBar);
        _arena=new BattleArena {IsConstruct=WoodlandBattle.IsRelay(run),IsTank=tank,IsHealer=StarterTraining.IsHealer(run),IsArcher=StarterTraining.IsArcher(run)};column.AddChild(_arena);
        _headline=new Label {Text="Choose a skill or use your free weapon strike.",AutowrapMode=TextServer.AutowrapMode.WordSmart};column.AddChild(_headline);
        _statusChips=new HBoxContainer {Name="BattleStatusChips"};_statusChips.AddThemeConstantOverride("separation",8);column.AddChild(_statusChips);
        _status=new Label {Name="BattleStatusLabel",AutowrapMode=TextServer.AutowrapMode.WordSmart};_status.AddThemeFontSizeOverride("font_size",12);column.AddChild(_status);
        var grid=new GridContainer {Columns=3};column.AddChild(grid);
        // Consistent action order: Attack/Guard -> Skills -> Items (Retreat in footer).
        AddAction(grid,StarterTraining.AttackName(run)+" · Free","staff",$"4 damage on hit · {CharacterRules.DisplayName(StarterTraining.BasicAttribute(run))} roll vs DC 10 (DC 8 when lit) · Instant · Free (0 {StarterTraining.Resource(run).ToLowerInvariant()}).");
        AddAction(grid,"Guard · Free","guard","+4 defence (hit chance only, 0 absorbed) for 1 enemy response · Automatic (no roll) · Free. Shares +4 defence with Arcane Ward and preserves a longer active ward.");
        foreach(var id in run.LearnedSpellIds)
        {
            var spell=StarterTraining.Skills(run).Single(s=>s.Id==id);
            AddAction(grid,spell.Name+" · 3 "+StarterTraining.Resource(run).ToLowerInvariant(),id,spell.Description);
        }
        _itemsToggle=new Button {Name="BattleItemsButton",Text="Items · Potions",TooltipText="Open the item chooser to use a carried health or mana/focus potion. Using a potion takes your turn.",CustomMinimumSize=new Vector2(0,35),SizeFlagsHorizontal=SizeFlags.ExpandFill};
        grid.AddChild(_itemsToggle);
        _actions[_itemsToggle]="potion";
        _baseTooltips[_itemsToggle]=_itemsToggle.TooltipText;
        _itemsToggle.Pressed+=()=>{if(_busy || WoodlandBattle.Current(GameSession.Instance!.CurrentRun!).Stage!=1)return;_itemChooserOpen=!_itemChooserOpen;Refresh();};

        _itemChooser=new PanelContainer {Name="BattleItemChooser",Visible=false};
        _itemChooser.AddThemeStyleboxOverride("panel",new StyleBoxFlat {BgColor=new Color("132230"),BorderColor=new Color("3a6e8c"),BorderWidthLeft=2,BorderWidthTop=1,BorderWidthRight=1,BorderWidthBottom=1,CornerRadiusTopLeft=5,CornerRadiusTopRight=5,CornerRadiusBottomLeft=5,CornerRadiusBottomRight=5,ContentMarginLeft=10,ContentMarginRight=10,ContentMarginTop=6,ContentMarginBottom=6});
        var chooserRow=new HBoxContainer();chooserRow.AddThemeConstantOverride("separation",8);_itemChooser.AddChild(chooserRow);
        var chooserLabel=new Label {Text="Use potion (costs your turn):",SizeFlagsVertical=SizeFlags.ShrinkCenter};
        chooserLabel.AddThemeFontSizeOverride("font_size",12);chooserLabel.AddThemeColorOverride("font_color",new Color("c8deef"));
        chooserRow.AddChild(chooserLabel);
        _hpPotionButton=new Button {Name="UseHealthPotionButton",CustomMinimumSize=new Vector2(0,30),SizeFlagsHorizontal=SizeFlags.ExpandFill};
        _hpPotionButton.Pressed+=async ()=>{_itemChooserOpen=false;await AnimateTurn("potion");};
        chooserRow.AddChild(_hpPotionButton);
        _mpPotionButton=new Button {Name="UseManaPotionButton",CustomMinimumSize=new Vector2(0,30),SizeFlagsHorizontal=SizeFlags.ExpandFill};
        _mpPotionButton.Pressed+=async ()=>{_itemChooserOpen=false;await AnimateTurn("mana_potion");};
        chooserRow.AddChild(_mpPotionButton);
        _closeItemsButton=new Button {Name="CloseItemChooserButton",Text="Cancel",CustomMinimumSize=new Vector2(72,30)};
        _closeItemsButton.Pressed+=()=>{if(_busy)return;_itemChooserOpen=false;Refresh();};
        chooserRow.AddChild(_closeItemsButton);
        column.AddChild(_itemChooser);

        _unavailableLabel=new Label {Name="UnavailableActionsLabel",AutowrapMode=TextServer.AutowrapMode.WordSmart};
        _unavailableLabel.AddThemeFontSizeOverride("font_size",12);
        _unavailableLabel.AddThemeColorOverride("font_color",new Color("b8c7d9"));
        column.AddChild(_unavailableLabel);
        _log=new RichTextLabel {Visible=false,CustomMinimumSize=new Vector2(0,75),ScrollFollowing=true,Text="Dice results appear here after each turn."};column.AddChild(_log);detail.Toggled+=shown=>{_log.Visible=shown;Refresh();};
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
        var button=new Button {Text=name,TooltipText=tooltip,CustomMinimumSize=new Vector2(0,35),SizeFlagsHorizontal=SizeFlags.ExpandFill};row.AddChild(button);
        _actions[button]=id;_baseTooltips[button]=tooltip;
        button.Pressed+=async ()=>await AnimateTurn(id);
    }
    public static string? UnavailableReason(Data.RunState r,Data.WoodlandBattleState b,string id,bool busy)
    {
        if(busy)return "turn resolving";
        if(b.Stage!=1)return "encounter ended";
        if(id=="potion")
        {
            if(r.Backpack.CountItem(Data.Ids.ItemHealthPotion)<=0)return "no potion";
            if(r.Character.Hp>=CharacterRules.MaxHealth(r.Character))return "health already full";
            return null;
        }
        if(id=="mana_potion")
        {
            if(r.Backpack.CountItem(Data.Ids.ItemManaPotion)<=0)return "no potion";
            if(r.Character.Mana>=CharacterRules.MaxMana(r.Character))return StarterTraining.UsesFocus(r)?"focus already full":"mana already full";
            return null;
        }
        if(r.Character.Mana<WoodlandBattle.Cost(id))
            return StarterTraining.UsesFocus(r)?"insufficient focus":"insufficient mana";
        if(id is "second_wind" or "mend" or "field_dressing" && r.Character.Hp>=CharacterRules.MaxHealth(r.Character))
            return "health already full";
        return null;
    }
    private static PanelContainer MakeChip(string text,Color border,Color bg,Color font)
    {
        var chip=new PanelContainer();
        chip.AddThemeStyleboxOverride("panel",new StyleBoxFlat {BgColor=bg,BorderColor=border,BorderWidthLeft=2,BorderWidthTop=1,BorderWidthRight=1,BorderWidthBottom=1,CornerRadiusTopLeft=4,CornerRadiusTopRight=4,CornerRadiusBottomLeft=4,CornerRadiusBottomRight=4,ContentMarginLeft=8,ContentMarginRight=8,ContentMarginTop=3,ContentMarginBottom=3});
        var label=new Label {Text=text};
        label.AddThemeFontSizeOverride("font_size",11);
        label.AddThemeColorOverride("font_color",font);
        chip.AddChild(label);
        return chip;
    }
    private async Task Pause(double seconds)=>await ToSignal(GetTree().CreateTimer(seconds * ComfortSettings.BattleTime, false),SceneTreeTimer.SignalName.Timeout);
    private async Task ChangeBar(ProgressBar bar,int target,Label label,string name,int maximum)
    {
        var tween=CreateTween();tween.TweenProperty(bar,"value",(double)target,.28 * ComfortSettings.BattleTime).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        await ToSignal(tween,Tween.SignalName.Finished);label.Text=$"{name} · {target}/{maximum}";
    }
    private async Task AnimateTurn(string id)
    {
        if(_busy)return;
        var r=GameSession.Instance!.CurrentRun!;
        if(WoodlandBattle.Current(r).Stage!=1)return;
        _busy=true;_enemyTurnPhase=false;Refresh();
        var b=WoodlandBattle.Current(r);
        int previousHp=r.Character.Hp,previousEnemy=b.EnemyHp,previousWard=b.Ward,previousStance=b.IronStance,previousVeil=b.Veil;
        var feedback=new BattleTurnFeedback();var summary=WoodlandBattle.Act(r,id,Dice,feedback);
        _log.AppendText("\n\n"+summary);
        _mana.Text=$"{StarterTraining.Resource(r)} {r.Character.Mana} / {CharacterRules.MaxMana(r.Character)} · Skills cost 3";
        _energyBar.MaxValue=CharacterRules.MaxMana(r.Character);_energyBar.Value=r.Character.Mana;
        if(!feedback.ActionResolved){_headline.Text=summary;_busy=false;Refresh();return;}
        GameSession.Instance.MarkUnsaved();
        GameAudio.Instance?.Cue(id is "potion" or "mana_potion" ? "potion" : "skill");
        _arena.WardResponses=id=="guard"?Math.Max(previousWard,1):id=="arcane_ward"&&feedback.PlayerHit==true?3:previousWard;
        _arena.StanceResponses=id is "iron_stance" or "sanctuary"?3:previousStance;
        _arena.VeilResponses=id=="evasive_stance" || id=="shadow_veil"&&feedback.PlayerHit==true?3:previousVeil;
        _arena.QueueRedraw();
        _headline.Text=id=="staff"?"You strike with your weapon…":id=="guard"?"You brace for the attack…":id=="potion"?"You drink a health potion…":id=="mana_potion"?"You drink a mana potion…":StarterTraining.Skills(GameSession.Instance!.CurrentRun!).Single(s=>s.Id==id).Name+"…";
        if(feedback.PlayerHit!=false || id is not ("arcane_ward" or "shadow_veil"))_arena.Play(id,feedback.PlayerHit??true);
        if(feedback.PlayerRoll is int roll)_arena.Float($"d20 {roll} · {(feedback.PlayerHit==true?"hit":"miss")}",false,new Color("d8e6ed"),12,-18);
        await Pause(.6);
        int enemyDamage=previousEnemy-WoodlandBattle.Current(r).EnemyHp;
        if(enemyDamage>0)_arena.Float("−"+enemyDamage,true,new Color("ffb88f"),26);
        else if(feedback.PlayerHit==false)_arena.Float(id is "arcane_ward" or "shadow_veil" ? "FIZZLE" : "MISS",id is not ("arcane_ward" or "shadow_veil"),new Color("bfccd9"),18);
        await ChangeBar(_enemyBar,WoodlandBattle.Current(r).EnemyHp,_enemyHealth,WoodlandBattle.EnemyName(r),WoodlandBattle.EnemyMaxHp(r));
            int healed=Math.Min(CharacterRules.MaxHealth(r.Character),previousHp+feedback.PlayerHealing);
            if(healed>previousHp){_arena.Float("+"+(healed-previousHp)+" HP",false,new Color("9cf5b3"),22);await ChangeBar(_playerBar,healed,_playerHealth,r.Character.Name,CharacterRules.MaxHealth(r.Character));}
            if(feedback.PlayerEnergyRestored>0)_arena.Float($"+{feedback.PlayerEnergyRestored} {StarterTraining.Resource(r).ToLowerInvariant()}",false,new Color("7fd4ff"),20);
        if(WoodlandBattle.Current(r).Stage==2)
        {
            _arena.EnemyDefeated=true;await Pause(.5);_headline.Text=WoodlandBattle.VictorySummary(r);
            _playerBar.MaxValue=CharacterRules.MaxHealth(r.Character);
            await ChangeBar(_playerBar,r.Character.Hp,_playerHealth,r.Character.Name,CharacterRules.MaxHealth(r.Character));
        }
        else
        {
            // Healing occurs before the enemy responds; display that intermediate health explicitly.
            _enemyTurnPhase=true;Refresh();
            _headline.Text=WoodlandBattle.EnemyName(r)+" retaliates…";_arena.Play("enemy",feedback.EnemyHit,feedback.EnemyDamageBlocked>0);
            if(feedback.EnemyRoll is int enemyRoll)_arena.Float($"d20 {enemyRoll}",true,new Color("d8c7f4"),12,-18);
            await Pause(.6);
            if(feedback.EnemyHit)
            {
                _arena.Float("−"+feedback.EnemyDamageTaken,false,new Color("ff9797"),26);
                if(feedback.EnemyDamageBlocked>0)_arena.Float($"Absorbed {feedback.EnemyDamageBlocked}",false,new Color("e5a766"),14,24);
            }
            else _arena.Float(_arena.WardResponses>0 ? "DEFLECTED" : _arena.VeilResponses>0 ? StarterTraining.IsArcher(r) ? "EVADED" : "DEFLECTED" : "MISS",false,new Color("c1d9d6"),18);
            await ChangeBar(_playerBar,r.Character.Hp,_playerHealth,r.Character.Name,CharacterRules.MaxHealth(r.Character));
            _arena.PlayerDefeated=WoodlandBattle.Current(r).Stage==3;
            _headline.Text=WoodlandBattle.Current(r).Stage==3?"You fall. A guild patrol can bring you back to safety.":feedback.EnemyHit?$"{WoodlandBattle.EnemyName(r)} hit: {feedback.EnemyRawDamage} base − {feedback.EnemyDamageBlocked} absorbed = {feedback.EnemyDamageTaken} HP. Your turn.":"Attack missed · 0 HP damage. Your turn.";
        }
        _busy=false;_enemyTurnPhase=false;GameSession.Instance.EmitChanged();Refresh();
    }
    private void Refresh()
    {
        var r=GameSession.Instance!.CurrentRun!;var b=WoodlandBattle.Current(r);
        _arena.CustomMinimumSize = new Vector2(0, _log.Visible ? 130 : _itemChooserOpen ? 150 : 225);
        _hintBar.Visible=!r.BattleHintDismissed && !WoodlandBattle.IsRelay(r) && b.Stage==1;
        _dismissHint.Disabled=_busy;
        _turnLabel.Text=b.Stage switch
        {
            2=>"Turn: Victory",
            3=>"Turn: Defeated",
            _=>!_busy?"Turn: Your action":_enemyTurnPhase?$"Turn: {WoodlandBattle.EnemyName(r)} responding…":"Turn: Resolving your action…"
        };
        _turnLabel.AddThemeColorOverride("font_color",b.Stage==2?new Color("9cf5b3"):b.Stage==3?new Color("ff9797"):!_busy?new Color("8be8cb"):_enemyTurnPhase?new Color("f2b3c8"):new Color("9ad4ff"));
        _playerBar.MaxValue=CharacterRules.MaxHealth(r.Character);_enemyBar.MaxValue=WoodlandBattle.EnemyMaxHp(r);
        if(!_busy)
        {
            _playerBar.Value=r.Character.Hp;_enemyBar.Value=b.EnemyHp;
            _playerHealth.Text=$"{r.Character.Name} · {r.Character.Hp}/{_playerBar.MaxValue}";
            _enemyHealth.Text=$"{WoodlandBattle.EnemyName(r)} · {b.EnemyHp}/{WoodlandBattle.EnemyMaxHp(r)}";
            _arena.EnemyDefeated=b.Stage==2;_arena.PlayerDefeated=b.Stage==3;
            if(b.Stage==2)_headline.Text=WoodlandBattle.VictorySummary(r);
            if(b.Stage==3)_headline.Text="Defeated · a guild patrol can bring you to safety.";
            _arena.WardResponses=b.Ward;_arena.StanceResponses=b.IronStance;_arena.VeilResponses=b.Veil;_arena.QueueRedraw();
        }
        _mana.Text=$"{StarterTraining.Resource(r)} {r.Character.Mana} / {CharacterRules.MaxMana(r.Character)} · Skills cost 3";
        _energyBar.MaxValue=CharacterRules.MaxMana(r.Character);_energyBar.Value=r.Character.Mana;
        foreach(var child in _statusChips.GetChildren()){_statusChips.RemoveChild(child);child.QueueFree();}
        if(b.Ward>0)_statusChips.AddChild(MakeChip($"◇ Ward +4 DEF (hit chance) · {WoodlandBattle.Responses(b.Ward)}",new Color("8bdaef"),new Color("122838"),new Color("d4f4ff")));
        if(b.IronStance>0)_statusChips.AddChild(MakeChip($"▤ {StarterTraining.ProtectionName(r)} −2 DMG/hit · {WoodlandBattle.Responses(b.IronStance)}",new Color("e5a766"),new Color("282118"),new Color("ffe6c2")));
        if(b.Veil>0)_statusChips.AddChild(MakeChip($"◐ {StarterTraining.VeilName(r)} +3 DEF (hit chance) · {WoodlandBattle.Responses(b.Veil)}",new Color("c4a5f7"),new Color("211a33"),new Color("eadcff")));
        if(b.Frost>0)_statusChips.AddChild(MakeChip($"▼ Enemy Slowed −2 ATK · {WoodlandBattle.Responses(b.Frost)}",new Color("92eaff"),new Color("142a34"),new Color("d9f8ff")));
        if(b.Lit)_statusChips.AddChild(MakeChip("✦ Exposed · Attack/skill DC 8",new Color("fff3a3"),new Color("2b2918"),new Color("fff9cc")));
        _statusChips.Visible=_statusChips.GetChildCount()>0;
        _status.Text="";
        if(b.Ward>0)_status.Text+=$"Ward: +4 defence (hit chance only, 0 absorbed), {WoodlandBattle.Responses(b.Ward)} remaining   ";
        if(b.IronStance>0)_status.Text+=$"{StarterTraining.ProtectionName(r)}: absorb 2 damage per hit, {WoodlandBattle.Responses(b.IronStance)} remaining   ";
        if(b.Veil>0)_status.Text+=$"{StarterTraining.VeilName(r)}: +3 defence (hit chance only), {WoodlandBattle.Responses(b.Veil)} remaining   ";
        if(b.Frost>0)_status.Text+=$"Enemy attack −2: {WoodlandBattle.Responses(b.Frost)} remaining   ";
        var hpCount=r.Backpack.CountItem(Data.Ids.ItemHealthPotion);
        var mpCount=r.Backpack.CountItem(Data.Ids.ItemManaPotion);
        var resourceLower=StarterTraining.Resource(r).ToLowerInvariant();
        var hpReason=UnavailableReason(r,b,"potion",_busy);
        var mpReason=UnavailableReason(r,b,"mana_potion",_busy);
        _itemsToggle.Text=$"Items · Potions ({hpCount+mpCount})";
        _itemsToggle.Disabled=_busy || b.Stage!=1 || (hpReason!=null && mpReason!=null);
        _itemsToggle.TooltipText=_itemsToggle.Disabled && !_busy && b.Stage==1
            ? $"{_baseTooltips[_itemsToggle]}\nUnavailable: Health potion ({hpReason}), Mana potion ({mpReason})."
            : _baseTooltips[_itemsToggle];

        if(b.Stage!=1)_itemChooserOpen=false;
        _itemChooser.Visible=_itemChooserOpen && b.Stage==1;
        _hpPotionButton.Text=$"Health potion ({hpCount}) · +15 HP";
        _hpPotionButton.Disabled=hpReason!=null;
        _hpPotionButton.TooltipText=hpReason==null
            ? "Restore up to 15 HP using a carried health potion · Automatic (no roll) · Uses your turn."
            : $"Restore up to 15 HP using a carried health potion.\nUnavailable: {hpReason}.";
        _mpPotionButton.Text=$"Mana potion ({mpCount}) · +10 {resourceLower}";
        _mpPotionButton.Disabled=mpReason!=null;
        _mpPotionButton.TooltipText=mpReason==null
            ? $"Restore up to 10 {resourceLower} using a carried mana potion · Automatic (no roll) · Uses your turn."
            : $"Restore up to 10 {resourceLower} using a carried mana potion.\nUnavailable: {mpReason}.";
        _closeItemsButton.Disabled=_busy;

        var unavailableReasons=new List<string>();
        foreach(var (button,id) in _actions)
        {
            if(id=="potion")continue;
            var reason=UnavailableReason(r,b,id,_busy);
            button.Disabled=reason!=null;
            button.TooltipText=reason==null?_baseTooltips[button]:$"{_baseTooltips[button]}\nUnavailable: {reason}.";
            if(reason!=null && !_busy && b.Stage==1)
            {
                var shortName=id=="staff"?StarterTraining.AttackName(r):id=="guard"?"Guard":StarterTraining.Skills(r).Single(s=>s.Id==id).Name;
                unavailableReasons.Add($"{shortName}: {reason}");
            }
        }
        if(!_busy && b.Stage==1)
        {
            if(hpReason!=null)unavailableReasons.Add($"Health potion: {hpReason}");
            if(mpReason!=null)unavailableReasons.Add($"Mana potion: {mpReason}");
        }
        if(_busy && b.Stage==1)_unavailableLabel.Text="Unavailable: turn resolving.";
        else _unavailableLabel.Text=unavailableReasons.Count>0?"Unavailable — "+string.Join(" · ",unavailableReasons):"";
        _unavailableLabel.Visible=!string.IsNullOrEmpty(_unavailableLabel.Text);
        _save.Disabled=_busy || GameSession.Instance.CarriedSaveCrystals()<=0;
        _finish.Visible=b.Stage==2;_finish.Disabled=_busy;
        _retreat.Visible=b.Stage is 1 or 3;_retreat.Disabled=_busy;
        _retreat.Text=b.Stage==3?"Accept guild rescue":"Retreat to safety";
    }
    public void RequestClose(){if(!_busy && WoodlandBattle.Current(GameSession.Instance!.CurrentRun!).Stage==2)CloseRequested?.Invoke();}
    public override void _ExitTree(){if(GameSession.Instance!=null)GameSession.Instance.StateChanged-=Refresh;}
}
