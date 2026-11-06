namespace DevilGate.Scripts.Data;

/// <summary>Identity, vitals and progress for one run's character.</summary>
public sealed class CharacterState
{
    public string RunId { get; set; } = "";
    public string Name { get; set; } = "";
    public string ArchetypeId { get; set; } = "";
    public string GenderId { get; set; } = "male";
    public AttributeSet Attributes { get; set; } = new();
    public int Hp { get; set; } = 1;
    public int Mana { get; set; } = 1;
    public int Level { get; set; } = 1;
    public int Xp { get; set; }
    public DiagnosticState Diagnostic { get; set; } = new();

    public CharacterState Clone() => new()
    {
        RunId = RunId,
        Name = Name,
        ArchetypeId = ArchetypeId,
        GenderId = GenderId,
        Attributes = Attributes.Clone(),
        Hp = Hp,
        Mana = Mana,
        Level = Level,
        Xp = Xp,
        Diagnostic = Diagnostic.Clone(),
    };
}

/// <summary>Outcome of the optional terminal diagnostic, persisted per run.</summary>
public sealed class DiagnosticState
{
    public bool Attempted { get; set; }
    public bool Success { get; set; }

    public DiagnosticState Clone() => new() { Attempted = Attempted, Success = Success };
}
