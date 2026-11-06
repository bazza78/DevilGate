using Godot;
using DevilGate.Scripts.Systems;
namespace DevilGate.Scripts;

/// <summary>Presentation only: spell timing never changes the resolved dice or combat state.</summary>
public partial class BattleArena : Control
{
    public bool IsConstruct {get;set;}
    public bool IsKnight {get;set;}
    public bool IsArcher {get;set;}
    public bool IsHealer {get;set;}
    public bool IsTank {get;set;}
    public string Effect {get;private set;}="";
    public float Progress {get;private set;}
    public bool LastActionHit {get;set;}=true;
    public bool LastActionAbsorbed {get;set;}
    public bool EnemyDefeated {get;set;}
    public bool PlayerDefeated {get;set;}
    private int _wardResponses,_stanceResponses,_veilResponses;
    private float _wardFade,_stanceFade,_veilFade,_healPulse;
    public int WardResponses
    {
        get=>_wardResponses;
        set{if(_wardResponses>0 && value==0)_wardFade=1f;_wardResponses=value;}
    }
    public int StanceResponses
    {
        get=>_stanceResponses;
        set{if(_stanceResponses>0 && value==0)_stanceFade=1f;_stanceResponses=value;}
    }
    public int VeilResponses
    {
        get=>_veilResponses;
        set{if(_veilResponses>0 && value==0)_veilFade=1f;_veilResponses=value;}
    }
    public bool HasActiveOrFadingProtection=>_wardResponses>0 || _stanceResponses>0 || _veilResponses>0 || _wardFade>0 || _stanceFade>0 || _veilFade>0;
    private float _time,_enemyLife=1;
    private Texture2D? _cloak;
    public Vector2 PlayerPoint=>new(Size.X*.22f,Size.Y*.77f);
    public Vector2 EnemyPoint=>new(Size.X*.78f,Size.Y*.77f);
    public override void _Ready(){CustomMinimumSize=new Vector2(0,225);MouseFilter=MouseFilterEnum.Ignore;_cloak=GD.Load<Texture2D>("res://Assets/Environment/cloaked-traveller-v1.png");ClipContents=true;}
    public override void _Process(double delta)
    {
        var dt=(float)delta;
        if (!ComfortSettings.ReducedMotion) _time+=dt;
        if(Effect!=""){Progress=Mathf.Min(1,Progress+dt/(.65f*(float)ComfortSettings.BattleTime));if(Progress>=1)Effect="";}
        _enemyLife=Mathf.MoveToward(_enemyLife,EnemyDefeated?0:1,dt*2);
        _wardFade=Mathf.Max(0,_wardFade-dt*1.4f);
        _stanceFade=Mathf.Max(0,_stanceFade-dt*1.4f);
        _veilFade=Mathf.Max(0,_veilFade-dt*1.4f);
        _healPulse=Mathf.Max(0,_healPulse-dt*1.3f);
        QueueRedraw();
    }
    public void Play(string effect,bool hit=true,bool absorbed=false)
    {
        effect = effect == "shield_wall" ? "arcane_ward" : effect == "rally" ? "second_wind" : effect == "evasive_stance" ? "shadow_veil" : effect == "field_dressing" ? "second_wind" : effect == "sanctuary" ? "iron_stance" : effect == "mend" ? "second_wind" : effect;
        Effect=effect;Progress=0;LastActionHit=hit;LastActionAbsorbed=absorbed;
        if(effect is "potion" or "second_wind" || (effect is "withering_touch" or "rallying_strike" && hit))_healPulse=ComfortSettings.ReducedFlash ? 0 : 1f;
    }
    public void Float(string text,bool enemy,Color colour,int fontSize=20,float offset=0)
    {
        if (ComfortSettings.ReducedMotion) offset = 0;
        var point=(enemy?EnemyPoint:PlayerPoint)+new Vector2(-45,-133+offset);
        var label=new Label {Text=text,Position=point,MouseFilter=MouseFilterEnum.Ignore};
        label.AddThemeFontSizeOverride("font_size",fontSize);label.Modulate=colour;
        label.AddThemeColorOverride("font_shadow_color",Colors.Black);label.AddThemeConstantOverride("shadow_offset_x",1);label.AddThemeConstantOverride("shadow_offset_y",1);
        AddChild(label);
        var tween=CreateTween().SetParallel();tween.TweenProperty(label,"position",point+new Vector2(0,ComfortSettings.ReducedMotion ? 0 : -24),.95);tween.TweenProperty(label,"modulate:a",0,.95);
        tween.Chain().TweenCallback(Callable.From(()=>label.QueueFree()));
    }
    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero,Size),new Color("172c33"));
        DrawColoredPolygon(new[]{new Vector2(0,Size.Y),new Vector2(Size.X*.38f,Size.Y*.48f),new Vector2(Size.X*.62f,Size.Y*.48f),new Vector2(Size.X,Size.Y)},new Color("3a5254"));
        for(int i=0;i<9;i++)
        {
            var x=i*Size.X/8;
            DrawLine(new Vector2(x,0),new Vector2(x+12,Size.Y*.55f),new Color("203d3c"),15,true);
            DrawCircle(new Vector2(x,10),35,new Color("25473e"));
        }
        var a=PlayerPoint;var b=EnemyPoint;
        Shadow(a,30);Shadow(b,27*_enemyLife);
        var bob=ComfortSettings.ReducedMotion ? 0 : Mathf.Sin(_time*2)*2;
        // Distinct anticipation -> strike -> recovery rhythm for melee attacks; recoil on incoming hits.
        float lunge=0;
        if(Effect is "staff" or "crushing_blow" or "shield_bash" or "commanding_strike" or "rallying_strike" && !(Effect=="staff" && (IsHealer || IsArcher)))
            lunge=Progress<.2f ? -6f*(Progress/.2f) : Progress<.65f ? Mathf.Lerp(-6f,26f,(Progress-.2f)/.45f) : Mathf.Lerp(26f,0f,(Progress-.65f)/.35f);
        else if(Effect=="enemy" && LastActionHit && Progress is >.45f and <.88f)
            lunge=-10f*Mathf.Sin((Progress-.45f)/.43f*Mathf.Pi);
        float enemyHitOffset=(Effect!="" && Effect is not ("enemy" or "arcane_ward" or "guard" or "shadow_veil" or "potion" or "mana_potion" or "iron_stance" or "second_wind") && LastActionHit && Progress is >.45f and <.88f)
            ? 12f*Mathf.Sin((Progress-.45f)/.43f*Mathf.Pi) : 0f;
        if (ComfortSettings.ReducedMotion) { lunge = 0; enemyHitOffset = 0; }
        if(_cloak!=null)DrawTextureRectRegion(_cloak,new Rect2(a+new Vector2(-28+lunge,-112+bob),new Vector2(56,112)),new Rect2(650,10,470,877),new Color(1,1,1,PlayerDefeated ? .35f : 1));
        if(!PlayerDefeated)
        {
            if(WardResponses>0)
            {
                DrawArc(a+new Vector2(8,-56),38,-Mathf.Pi*.42f,Mathf.Pi*.42f,28,new Color("8bdaef",.82f),3,true);
                if(IsKnight)
                {
                    var shield=new[]{a+new Vector2(12,-83),a+new Vector2(40,-74),a+new Vector2(36,-43),a+new Vector2(21,-26),a+new Vector2(6,-43),a+new Vector2(2,-74)};
                    DrawColoredPolygon(shield,new Color("72d0e2",.16f));
                    DrawPolyline(shield.Append(shield[0]).ToArray(),new Color("a4e6f0",.85f),2,true);
                    DrawLine(a+new Vector2(21,-76),a+new Vector2(21,-38),new Color("a4e6f0",.6f),1,true);
                }
            }
            else if(_wardFade>0)
            {
                float expand=(1f-_wardFade)*14f;
                DrawArc(a+new Vector2(8,-56),38+expand,-Mathf.Pi*.42f,-Mathf.Pi*.08f,12,new Color("8bdaef",_wardFade*.75f),2,true);
                DrawArc(a+new Vector2(8,-56),38+expand,Mathf.Pi*.08f,Mathf.Pi*.42f,12,new Color("8bdaef",_wardFade*.75f),2,true);
            }
            if(VeilResponses>0)
                DrawArc(a+new Vector2(0,-56),44,Mathf.Pi*.55f,Mathf.Pi*1.45f,28,new Color(IsArcher?"89dcc2":"ad87eb",.78f),2,true);
            else if(_veilFade>0)
                DrawArc(a+new Vector2(0,-56),44+(1f-_veilFade)*12f,Mathf.Pi*.55f,Mathf.Pi*1.45f,20,new Color(IsArcher?"89dcc2":"ad87eb",_veilFade*.7f),2,true);
            if(StanceResponses>0)
            {
                var brace=new Color("e5a766",.9f);
                DrawPolyline(new[]{a+new Vector2(-26,-82+bob),a+new Vector2(-34,-54+bob),a+new Vector2(-26,-26+bob)},brace,4,true);
                DrawPolyline(new[]{a+new Vector2(26,-82+bob),a+new Vector2(34,-54+bob),a+new Vector2(26,-26+bob)},brace,4,true);
            }
            else if(_stanceFade>0)
            {
                float drift=(1f-_stanceFade)*12f;
                var brace=new Color("e5a766",_stanceFade*.8f);
                DrawPolyline(new[]{a+new Vector2(-26-drift,-82),a+new Vector2(-34-drift,-54),a+new Vector2(-26-drift,-26)},brace,3,true);
                DrawPolyline(new[]{a+new Vector2(26+drift,-82),a+new Vector2(34+drift,-54),a+new Vector2(26+drift,-26)},brace,3,true);
            }
            if(_healPulse>0)
            {
                var healCol=new Color("8df3a7",_healPulse*.85f);
                DrawArc(a+new Vector2(0,-56),34+(1f-_healPulse)*10f,0,Mathf.Tau,32,healCol,2,true);
                for(int i=0;i<3;i++)
                {
                    var hpPos=a+new Vector2(-18+i*18,-36-(1f-_healPulse)*36f-i*8);
                    DrawLine(hpPos+new Vector2(-4,0),hpPos+new Vector2(4,0),healCol,2,true);
                    DrawLine(hpPos+new Vector2(0,-4),hpPos+new Vector2(0,4),healCol,2,true);
                }
            }
        }
        var w=b+new Vector2(enemyHitOffset,-55+Mathf.Sin(_time*3)*5);
        if(IsConstruct)
        {
            DrawSetTransform(w,0,Vector2.One*_enemyLife);
            // Articulated construct legs & heavy shoulder pylons.
            DrawLine(new Vector2(-20,16),new Vector2(-28,36),new Color("5b7678"),8,true);
            DrawLine(new Vector2(20,16),new Vector2(28,36),new Color("5b7678"),8,true);
            DrawRect(new Rect2(-32,-22,10,28),new Color("526d78"));
            DrawRect(new Rect2(22,-22,10,28),new Color("526d78"));
            DrawRect(new Rect2(-24,-28,48,52),new Color("3a5059"));
            DrawPolyline(new[]{new Vector2(-24,-28),new Vector2(24,-28),new Vector2(24,24),new Vector2(-24,24),new Vector2(-24,-28)},new Color("99b6bb"),3,true);
            DrawLine(new Vector2(-16,-18),new Vector2(16,-18),new Color("efb270"),3,true);
            DrawArc(Vector2.Zero,16,_time*1.5f,_time*1.5f+Mathf.Pi*1.4f,20,new Color("c394ff"),2,true);
            DrawCircle(Vector2.Zero,12,new Color("985de0"));DrawCircle(Vector2.Zero,6,new Color("e7bbff"));
            DrawSetTransform(Vector2.Zero,0,Vector2.One);
        }
        else
        {
            DrawCircle(w,32*_enemyLife,new Color(.62f,.32f,1,.12f*_enemyLife));
            DrawCircle(w,19*_enemyLife,new Color(.65f,.4f,1,.65f*_enemyLife));
            DrawCircle(w,10*_enemyLife,new Color(.95f,.85f,1,_enemyLife));
            if(_enemyLife>.1f)for(int i=0;i<5;i++)DrawLine(w+Vector2.FromAngle(_time+i*Mathf.Tau/5)*20*_enemyLife,w+Vector2.FromAngle(_time+i*Mathf.Tau/5+.4f)*34*_enemyLife,new Color(.75f,.6f,1,_enemyLife),2,true);
        }
        if(Effect=="" || ComfortSettings.ReducedFlash || ComfortSettings.ReducedMotion)return;
        var from=a+new Vector2(12,-64);var to=w;var t=Progress;
        if(Effect=="iron_stance")
        {
            var brace=new Color("efb270",1-.35f*t);
            float spread=Mathf.Sin(t*Mathf.Pi)*8;
            DrawPolyline(new[]{a+new Vector2(-30-spread,-86),a+new Vector2(-40-spread,-54),a+new Vector2(-30-spread,-22)},brace,4,true);
            DrawPolyline(new[]{a+new Vector2(30+spread,-86),a+new Vector2(40+spread,-54),a+new Vector2(30+spread,-22)},brace,4,true);
        }
        else if(Effect is "arcane_ward" or "guard" or "shadow_veil" or "potion" or "mana_potion" or "second_wind")
        {
            var colour=Effect is "potion" or "second_wind"?new Color("8df3a7"):Effect=="mana_potion"?new Color("64b8ed"):Effect=="shadow_veil"?new Color(IsArcher?"89dcc2":"ad87eb"):new Color("8bdaef");
            DrawArc(a+new Vector2(0,-52),40+Mathf.Sin(t*Mathf.Pi)*12,-Mathf.Pi*.9f,Mathf.Pi*.9f,48,colour,3,true);
            for(int i=0;i<10;i++)DrawCircle(a+new Vector2(Mathf.Sin(i*2+t*5)*30,-20-t*85-i*3),2,colour);
        }
        else if(Effect is "staff" or "crushing_blow" or "shield_bash" or "commanding_strike" or "rallying_strike" && !(Effect == "staff" && (IsHealer || IsArcher)))
        {
            if(IsKnight)
            {
                var handle=from+new Vector2(lunge,13);
                var direction=new Vector2(.55f,-.84f);var side=new Vector2(-direction.Y,direction.X);
                var guard=handle+direction*13;var tip=guard+direction*(Effect=="commanding_strike"?52:44);
                DrawLine(handle,guard,new Color("495e70"),5,true);
                DrawLine(guard-side*10,guard+side*10,new Color("a9c5d8"),4,true);
                DrawColoredPolygon(new[]{guard-side*5,tip,guard+side*5},new Color("d5edf6"));
                DrawLine(guard,tip,new Color("6acfdc"),1,true);
            }
            else if(IsTank || Effect is "crushing_blow" or "shield_bash")
            {
                // Heavy scrap-bound club silhouette (distinct from mage focus staff).
                var handle=from+new Vector2(lunge,12);
                var head=from+new Vector2(lunge+34,-34);
                DrawLine(handle,head,new Color("8c6239"),6,true);
                DrawLine(handle.Lerp(head,.45f),head,new Color("a8794d"),12,true);
                DrawLine(handle.Lerp(head,.62f)+new Vector2(-4,-2),handle.Lerp(head,.62f)+new Vector2(4,2),new Color("b7c9ce"),3,true);
                DrawLine(handle.Lerp(head,.84f)+new Vector2(-5,-2),handle.Lerp(head,.84f)+new Vector2(5,2),new Color("b7c9ce"),3,true);
            }
            else
            {
                var handle=from+new Vector2(lunge,15);
                var tip=from+new Vector2(lunge+35,-40);
                DrawLine(handle,tip,new Color("d7bd8b"),4,true);
                DrawCircle(tip,5,new Color("8bdaef"));
            }
            if(Effect=="shield_bash")DrawArc(from+new Vector2(lunge+22,-8),22,-Mathf.Pi*.45f,Mathf.Pi*.45f,20,new Color("8bdaef"),5,true);
            if(Effect=="crushing_blow")DrawArc(w,26,-Mathf.Pi*.7f,Mathf.Pi*.2f,20,new Color("e5a766"),5,true);
            if(t>.4f)
            {
                if(LastActionHit)DrawArc(w,30+(t-.4f)*25f,-1.3f+t,1.5f+t,24,new Color("ffd89e",1.2f-t),4,true);
                else DrawLine(w+new Vector2(-18,-28),w+new Vector2(22,-42),new Color("bfccd9",1.1f-t),2,true);
            }
        }
        else if(IsArcher && Effect is "staff" or "precision_shot" or "pinning_shot")
        {
            // Draw, loose, flight, then impact; the resolver has already committed this turn.
            var bow=a+new Vector2(16,-64);
            var wood=new Color("b69264");
            DrawArc(bow,24,-Mathf.Pi*.44f,Mathf.Pi*.44f,24,wood,3,true);
            var upper=bow+Vector2.FromAngle(-Mathf.Pi*.44f)*24;
            var lower=bow+Vector2.FromAngle(Mathf.Pi*.44f)*24;
            var nock=bow+new Vector2(t<.25f?-10*(t/.25f):t<.4f?-10*(1-(t-.25f)/.15f):0,0);
            DrawPolyline(new[]{upper,nock,lower},new Color("cde3d5"),1,true);
            var aim=LastActionHit?to:to+new Vector2(20,-32);
            var direction=(aim-bow).Normalized();var side=new Vector2(-direction.Y,direction.X);
            var tip=bow.Lerp(aim,Mathf.Clamp((t-.25f)/.48f,0,1));
            var col=new Color(Effect=="precision_shot"?"ffe5a4":Effect=="pinning_shot"?"88e8ca":"d5e8d7");
            if(t<.8f)
            {
                DrawLine(tip-direction*26,tip,col,Effect=="precision_shot"?3:2,true);
                DrawColoredPolygon(new[]{tip+direction*5,tip-direction*5+side*4,tip-direction*5-side*4},col);
            }
            if(t>.65f && LastActionHit)
            {
                float radius=8+(t-.65f)*35;
                DrawArc(to,radius,0,Mathf.Tau,24,new Color(col,1-t),2,true);
                if(Effect=="pinning_shot")DrawArc(EnemyPoint,24,0,Mathf.Tau,24,new Color(col,1-t),2,true);
            }
        }
        else
        {
            if(Effect=="enemy"){from=w;to=a+new Vector2(0,-55);}
            var aim=LastActionHit ? to : to+new Vector2(Effect=="enemy"?-22:22,-32);
            var p=from.Lerp(aim,Mathf.Clamp(t*1.4f,0,1));
            var colour=Effect switch {"staff" when IsHealer=>new Color("9cf4dc"),"radiant_bolt"=>new Color("ffe6a3"),"pacifying_light"=>new Color("a4f0e3"),"ember_bolt"=>new Color("ff8a37"),"frost_touch"=>new Color("92eaff"),"static_spark"=>new Color("c8b4ff"),"withering_touch"=>new Color("ad80cf"),"mage_light"=>new Color("fff3a3"),_=>new Color("c093ff")};
            if(Effect=="static_spark")
            {
                var points=Enumerable.Range(0,12).Select(i=>from.Lerp(aim,i/11f)+new Vector2(0,i is 0 or 11?0:Mathf.Sin(i*13+_time*40)*14)).ToArray();
                DrawPolyline(points,new Color("6e78ed"),7,true);DrawPolyline(points,colour,2,true);
            }
            else
            {
                for(int i=5;i>=0;i--)DrawCircle(p-(aim-from).Normalized()*i*6+new Vector2(0,Mathf.Sin(i+t*20)*i),Mathf.Max(2,11-i),new Color(colour.R,colour.G,colour.B,.15f+.10f*(5-i)));
                if(Effect=="frost_touch")DrawColoredPolygon(new[]{p+new Vector2(0,-17),p+new Vector2(8,0),p+new Vector2(0,17),p+new Vector2(-8,0)},colour);
                else DrawCircle(p,7,colour);
                if(Effect=="ember_bolt")DrawColoredPolygon(new[]{p+new Vector2(-14,6),p+new Vector2(-4,-16),p+new Vector2(3,-6),p+new Vector2(8,6)},new Color("ffd98b"));
            }
            if(t>.68f)
            {
                if(LastActionHit)
                {
                    DrawArc(to,(t-.68f)*105,0,Mathf.Tau,32,new Color(colour.R,colour.G,colour.B,1-t),3,true);
                    if(Effect=="enemy" && LastActionAbsorbed)
                        DrawArc(a+new Vector2(0,-54),36,-Mathf.Pi*.7f,Mathf.Pi*.7f,24,new Color("efb270",1-t),4,true);
                }
                else if(Effect=="enemy" && (WardResponses>0 || VeilResponses>0))
                {
                    var wardCol=WardResponses>0?new Color("8bdaef",1-t):new Color("ad87eb",1-t);
                    DrawArc(a+new Vector2(10,-56),44+(t-.68f)*30f,-Mathf.Pi*.5f,Mathf.Pi*.5f,28,wardCol,4,true);
                }
            }
        }
    }
    private void Shadow(Vector2 p,float radius){DrawSetTransform(p,0,new Vector2(1,.35f));DrawCircle(Vector2.Zero,radius,new Color(0,0,0,.2f));DrawSetTransform(Vector2.Zero,0,Vector2.One);}
}
