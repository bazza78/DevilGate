using Godot;
namespace DevilGate.Scripts.World;
public partial class ResponseRing : Node2D
{
    private float _life = 1;
    public override void _Process(double delta) { _life -= (float)delta * 2; if (_life <= 0) QueueFree(); else QueueRedraw(); }
    public override void _Draw() { DrawArc(Vector2.Zero, 15 + (1 - _life) * 18, 0, Mathf.Tau, 40, new Color("91d9d0", _life * .6f), 2, true); }
}
