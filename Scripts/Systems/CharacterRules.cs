using DevilGate.Scripts.Data;

namespace DevilGate.Scripts.Systems;

/// <summary>
/// Single source for character-creation rules: point budget, name validation
/// and derived health/mana formulas.
/// </summary>
public static class CharacterRules
{
    public const int AttributeMin = 1;
    public const int AttributeMax = 5;
    public const int BasePointsPerAttribute = 1;
    public const int BonusPoints = 12;
    public const int RequiredTotal = AttributeSet.AttributeCount * BasePointsPerAttribute + BonusPoints; // 18
    public const int MaxNameLength = 24;

    public static string DisplayName(AttributeKind kind) => kind switch
    {
        AttributeKind.Might => "Might",
        AttributeKind.Agility => "Agility",
        AttributeKind.Endurance => "Endurance",
        AttributeKind.Intellect => "Intellect",
        AttributeKind.Willpower => "Willpower",
        AttributeKind.Presence => "Presence",
        _ => kind.ToString(),
    };

    public static string DisplayNameShort(AttributeKind kind) => kind switch
    {
        AttributeKind.Might => "M",
        AttributeKind.Agility => "A",
        AttributeKind.Endurance => "E",
        AttributeKind.Intellect => "I",
        AttributeKind.Willpower => "W",
        AttributeKind.Presence => "P",
        _ => kind.ToString(),
    };

    public static int MaxHealth(AttributeSet attributes,int level=1) => 20 + 5 * attributes.Endurance + 5 * (Math.Max(1,level)-1);

    public static int MaxHealth(CharacterState character) => MaxHealth(character.Attributes,character.Level);

    public static int MaxMana(AttributeSet attributes,int level=1) => 5 + 5 * attributes.Willpower + 2 * (Math.Max(1,level)-1);

    public static int MaxMana(CharacterState character) => MaxMana(character.Attributes,character.Level);

    /// <summary>Points spent above the six base points.</summary>
    public static int BonusSpent(AttributeSet attributes) => attributes.Total - AttributeSet.AttributeCount;

    public static bool IsAllocationComplete(AttributeSet attributes)
    {
        return attributes.Total == RequiredTotal
            && AttributeSet.All.All(k => attributes[k] >= AttributeMin && attributes[k] <= AttributeMax);
    }

    public static bool IsValidName(string? name)
    {
        if (name == null)
        {
            return false;
        }
        var trimmed = name.Trim();
        return trimmed.Length >= 1 && trimmed.Length <= MaxNameLength;
    }

    public static bool CanIncrement(AttributeSet attributes, AttributeKind kind)
    {
        return attributes[kind] < AttributeMax && BonusSpent(attributes) < BonusPoints;
    }

    public static bool CanDecrement(AttributeSet attributes, AttributeKind kind)
    {
        return attributes[kind] > AttributeMin;
    }
}
