using DevilGate.Scripts.Systems;

namespace DevilGate.Scripts.Checks;

/// <summary>Deterministic die source for rule checks.</summary>
public sealed class ScriptedDieSource : IDieSource
{
    private readonly Queue<int> _rolls;

    public ScriptedDieSource(params int[] rolls)
    {
        _rolls = new Queue<int>(rolls);
    }

    public int RollD20() => _rolls.Count > 0 ? _rolls.Dequeue() : 10;
}
