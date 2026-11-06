using Godot;
namespace DevilGate.Scripts.World;
public partial class ReceptionCounter : Node2D
{
    public override void _Draw()
    {
        IsoDraw.Box(this,Vector2.Zero,48,17,25,new Color("b9aa8a"),new Color("6b6460"),new Color("555d60"));
        DrawLine(new Vector2(-45,-23),new Vector2(0,-2),new Color("8ee1cf"),2,true);
    }
}
