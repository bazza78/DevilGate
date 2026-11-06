namespace DevilGate.Scripts.Data;
public sealed class WoodlandBattleState
{
    // 0 dormant, 1 active, 2 won, 3 defeated awaiting return to park.
    public int Stage {get;set;}
    public int EnemyHp {get;set;}=18;
    public int Ward {get;set;}
    public int Veil {get;set;}
    public int Frost {get;set;}
    public int IronStance {get;set;}
    public bool Lit {get;set;}
    public WoodlandBattleState Clone()=>(WoodlandBattleState)MemberwiseClone();
}
