using Godot;
namespace DevilGate.Scripts;

/// <summary>Presentation only: spell timing never changes the resolved dice or combat state.</summary>
public partial class BattleArena : Control
{
    public bool IsConstruct {get;set;}
    public string Effect {get;private set;}="";
    public float Progress {get;private set;}
    public bool EnemyDefeated {get;set;}
    public bool PlayerDefeated {get;set;}
    private float _time,_enemyLife=1;
    private Texture2D? _cloak;
    public Vector2 PlayerPoint=>new(Size.X*.22f,Size.Y*.77f);
    public Vector2 EnemyPoint=>new(Size.X*.78f,Size.Y*.77f);
    public override void _Ready(){CustomMinimumSize=new Vector2(0,235);MouseFilter=MouseFilterEnum.Ignore;_cloak=GD.Load<Texture2D>("res://Assets/Environment/cloaked-traveller-v1.png");ClipContents=true;}
    public override void _Process(double delta)
    {
        _time+=(float)delta;
        if(Effect!=""){Progress=Mathf.Min(1,Progress+(float)delta/.65f);if(Progress>=1)Effect="";}
        _enemyLife=Mathf.MoveToward(_enemyLife,EnemyDefeated?0:1,(float)delta*2);
        QueueRedraw();
    }
    public void Play(string effect){Effect=effect;Progress=0;}
    public void Float(string text,bool enemy,Color colour,int fontSize=20,float offset=0)
    {
        var point=(enemy?EnemyPoint:PlayerPoint)+new Vector2(-45,-133+offset);
        var label=new Label {Text=text,Position=point,MouseFilter=MouseFilterEnum.Ignore};
        label.AddThemeFontSizeOverride("font_size",fontSize);label.Modulate=colour;
        label.AddThemeColorOverride("font_shadow_color",Colors.Black);label.AddThemeConstantOverride("shadow_offset_x",1);label.AddThemeConstantOverride("shadow_offset_y",1);
        AddChild(label);
        var tween=CreateTween().SetParallel();tween.TweenProperty(label,"position",point+new Vector2(0,-24),.95);tween.TweenProperty(label,"modulate:a",0,.95);
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
        var bob=Mathf.Sin(_time*2)*2;
        float lunge=Effect is "staff" or "crushing_blow" or "shield_bash"?Mathf.Sin(Progress*Mathf.Pi)*22:0;
        if(_cloak!=null)DrawTextureRectRegion(_cloak,new Rect2(a+new Vector2(-28+lunge,-112+bob),new Vector2(56,112)),new Rect2(650,10,470,877),new Color(1,1,1,PlayerDefeated ? .35f : 1));
        var w=b+new Vector2(0,-55+Mathf.Sin(_time*3)*5);
        if(IsConstruct)
        {
            DrawSetTransform(w,0,Vector2.One*_enemyLife);
            DrawLine(new Vector2(-20,0),new Vector2(-30,24),new Color("718f91"),7,true);
            DrawLine(new Vector2(20,0),new Vector2(30,24),new Color("718f91"),7,true);
            DrawRect(new Rect2(-24,-25,48,50),new Color("405761"));
            DrawPolyline(new[]{new Vector2(-24,-25),new Vector2(24,-25),new Vector2(24,25),new Vector2(-24,25),new Vector2(-24,-25)},new Color("99b6bb"),3,true);
            DrawCircle(Vector2.Zero,14,new Color("985de0"));DrawCircle(Vector2.Zero,7,new Color("e7bbff"));
            DrawLine(new Vector2(-12,-15),new Vector2(12,-15),new Color("efb270"),3,true);
            DrawSetTransform(Vector2.Zero,0,Vector2.One);
        }
        else
        {
        DrawCircle(w,32*_enemyLife,new Color(.62f,.32f,1,.12f*_enemyLife));
        DrawCircle(w,19*_enemyLife,new Color(.65f,.4f,1,.65f*_enemyLife));
        DrawCircle(w,10*_enemyLife,new Color(.95f,.85f,1,_enemyLife));
        if(_enemyLife>.1f)for(int i=0;i<5;i++)DrawLine(w+Vector2.FromAngle(_time+i*Mathf.Tau/5)*20*_enemyLife,w+Vector2.FromAngle(_time+i*Mathf.Tau/5+.4f)*34*_enemyLife,new Color(.75f,.6f,1,_enemyLife),2,true);
        }
        if(Effect=="")return;
        var from=a+new Vector2(12,-64);var to=w;var t=Progress;
        if(Effect is "arcane_ward" or "guard" or "shadow_veil" or "potion" or "iron_stance" or "second_wind")
        {
            var colour=Effect is "potion" or "second_wind"?new Color("8df3a7"):Effect=="shadow_veil"?new Color("ad87eb"):new Color("8bdaef");
            DrawArc(a+new Vector2(0,-52),40+Mathf.Sin(t*Mathf.Pi)*12,-Mathf.Pi*.9f,Mathf.Pi*.9f,48,colour,3,true);
            for(int i=0;i<10;i++)DrawCircle(a+new Vector2(Mathf.Sin(i*2+t*5)*30,-20-t*85-i*3),2,colour);
        }
        else if(Effect is "staff" or "crushing_blow" or "shield_bash")
        {
            DrawLine(from+new Vector2(lunge,15),from+new Vector2(lunge+35,-40),new Color("d7bd8b"),4,true);
            if(Effect=="shield_bash")DrawCircle(from+new Vector2(lunge+20,-5),20,new Color("8bdaef"));
            if(Effect=="crushing_blow")DrawLine(from+new Vector2(lunge+25,-35),from+new Vector2(lunge+45,-35),new Color("e5a766"),14,true);
            if(t>.4f)DrawArc(w,32,-1.3f+t,1.5f+t,24,new Color("f3e6cc"),4,true);
        }
        else
        {
            if(Effect=="enemy"){from=w;to=a+new Vector2(0,-55);}
            var p=from.Lerp(to,Mathf.Clamp(t*1.4f,0,1));
            var colour=Effect switch {"ember_bolt"=>new Color("ff8a37"),"frost_touch"=>new Color("92eaff"),"static_spark"=>new Color("c8b4ff"),"withering_touch"=>new Color("ad80cf"),"mage_light"=>new Color("fff3a3"),_=>new Color("c093ff")};
            if(Effect=="static_spark")
            {
                var points=Enumerable.Range(0,12).Select(i=>from.Lerp(to,i/11f)+new Vector2(0,i is 0 or 11?0:Mathf.Sin(i*13+_time*40)*14)).ToArray();
                DrawPolyline(points,new Color("6e78ed"),7,true);DrawPolyline(points,colour,2,true);
            }
            else
            {
                for(int i=5;i>=0;i--)DrawCircle(p-(to-from).Normalized()*i*6+new Vector2(0,Mathf.Sin(i+t*20)*i),Mathf.Max(2,11-i),new Color(colour.R,colour.G,colour.B,.15f+.10f*(5-i)));
                if(Effect=="frost_touch")DrawColoredPolygon(new[]{p+new Vector2(0,-17),p+new Vector2(8,0),p+new Vector2(0,17),p+new Vector2(-8,0)},colour);
                else DrawCircle(p,7,colour);
                if(Effect=="ember_bolt")DrawColoredPolygon(new[]{p+new Vector2(-14,6),p+new Vector2(-4,-16),p+new Vector2(3,-6),p+new Vector2(8,6)},new Color("ffd98b"));
            }
            if(t>.7f)DrawArc(to,(t-.7f)*100,0,Mathf.Tau,32,new Color(colour.R,colour.G,colour.B,1-t),3,true);
        }
    }
    private void Shadow(Vector2 p,float radius){DrawSetTransform(p,0,new Vector2(1,.35f));DrawCircle(Vector2.Zero,radius,new Color(0,0,0,.2f));DrawSetTransform(Vector2.Zero,0,Vector2.One);}
}
