using DevilGate.Scripts.Data;
using DevilGate.Scripts.Definitions;
using DevilGate.Scripts.Systems;

namespace DevilGate.Scripts.Systems;

/// <summary>
/// The PC's optional terminal diagnostic: Technology/Intellect at difficulty
/// 12, exactly one attempt per run, +10 XP on success only. The attempt flag
/// lives on CharacterState.Diagnostic so it persists through saves.
/// </summary>
public static class TerminalDiagnostic
{
    public const int Difficulty = 12;
    public const int SuccessXpAward = 10;

    public static SkillCheckSpec Spec { get; } =
        new(AttributeKind.Intellect, Ids.SkillTechnology, Difficulty, "Terminal diagnostic");

    /// <summary>
    /// Resolve the diagnostic once. Returns the DiceResult on the first call;
    /// returns null when the run already attempted it (never rerolls, never
    /// awards XP twice). Pass an IDieSource for deterministic tests.
    /// </summary>
    public static DiceResult? TryRun(RunState run, GameCatalog catalog, IDieSource? dice = null)
    {
        var diagnostic = run.Character.Diagnostic;
        if (diagnostic.Attempted)
        {
            return null;
        }

        var result = new DiceService(dice).Resolve(run.Character, Spec, catalog);
        diagnostic.Attempted = true;
        diagnostic.Success = result.Success;
        if (result.Success)
        {
            Progression.Award(run.Character,SuccessXpAward);
        }
        return result;
    }
}
