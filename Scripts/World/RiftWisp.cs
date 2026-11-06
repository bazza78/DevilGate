using Godot;
using DevilGate.Scripts.Systems;
namespace DevilGate.Scripts.World;
public partial class RiftWisp : Node2D
{
    public static Vector2 EncounterPoint=>SafehouseLayout.Iso(8,7);
    private float _time;
    public override void _Process(double delta)
    {
        _time+=(float)delta;var r=GameSession.Instance?.CurrentRun;
        Visible=r!=null && WoodlandBattle.Unlocked(r) && r.WoodlandBattle.Stage<2;QueueRedraw();
    }
    public override void _Draw()
    {
        var p=new Vector2(0,-25+Mathf.Sin(_time*3)*4);
        DrawCircle(p,20,new Color(.55f,.35f,1,.10f));DrawCircle(p,12,new Color(.65f,.4f,1,.55f));DrawCircle(p,6,new Color("e6ceff"));
        for(int i=0;i<5;i++)DrawLine(p+Vector2.FromAngle(_time+i*Mathf.Tau/5)*13,p+Vector2.FromAngle(_time+i*Mathf.Tau/5+.4f)*23,new Color("b59aff"),2,true);
    }
}
