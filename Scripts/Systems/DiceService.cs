using DevilGate.Scripts.Data;
using DevilGate.Scripts.Definitions;

namespace DevilGate.Scripts.Systems;

/// <summary>Injectable die source so checks can be tested deterministically.</summary>
public interface IDieSource
{
    /// <summary>Return a d20 roll in [1, 20].</summary>
    int RollD20();
}

public sealed class SystemDieSource : IDieSource
{
    private readonly Random _random = new();

    public int RollD20() => _random.Next(1, 21);
}

/// <summary>One risky action check: attribute + optional trained skill vs difficulty.</summary>
public sealed record SkillCheckSpec(
    AttributeKind Attribute,
    string? SkillId,
    int Difficulty,
    string DisplayName);

public sealed record DiceResult(
    int DieRoll,
    int AttributeModifier,
    int TrainingBonus,
    int Total,
    int Difficulty,
    bool Success)
{
    public string Summary =>
        $"d20({DieRoll}) + attribute({AttributeModifier:+0;-#;0}) + training({TrainingBonus:+0;-#;0}) = {Total} vs {Difficulty}";
}

/// <summary>
/// Check formula: d20 + (attribute - 1) + trained bonus >= difficulty.
/// Trained bonus is +2 when the check's skill is trained for the character's
/// archetype, otherwise 0. No criticals in the prototype.
/// </summary>
public sealed class DiceService
{
    public const int TrainedBonus = 2;

    private readonly IDieSource _dice;

    public DiceService(IDieSource? dice = null)
    {
        _dice = dice ?? new SystemDieSource();
    }

    public DiceResult Resolve(CharacterState character, SkillCheckSpec check, GameCatalog catalog)
    {
        var attributeValue = character.Attributes[check.Attribute];
        var attributeModifier = attributeValue - 1;

        var trainingBonus = 0;
        if (check.SkillId != null)
        {
            var archetype = catalog.GetArchetype(character.ArchetypeId);
            if (archetype.HasTrainedSkill(check.SkillId))
            {
                trainingBonus = TrainedBonus;
            }
        }

        var roll = _dice.RollD20();
        var total = roll + attributeModifier + trainingBonus;
        return new DiceResult(roll, attributeModifier, trainingBonus, total, check.Difficulty, total >= check.Difficulty);
    }
}
