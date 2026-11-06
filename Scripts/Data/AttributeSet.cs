namespace DevilGate.Scripts.Data;

/// <summary>The six character attributes, in canonical order.</summary>
public enum AttributeKind
{
    Might = 0,
    Agility = 1,
    Endurance = 2,
    Intellect = 3,
    Willpower = 4,
    Presence = 5,
}

/// <summary>
/// The six attribute scores. Each starts at 1; players may raise each to at
/// most 5. Plain data so it serializes directly into saves.
/// </summary>
public sealed class AttributeSet
{
    public const int AttributeCount = 6;

    public int Might { get; set; } = 1;
    public int Agility { get; set; } = 1;
    public int Endurance { get; set; } = 1;
    public int Intellect { get; set; } = 1;
    public int Willpower { get; set; } = 1;
    public int Presence { get; set; } = 1;

    public static IReadOnlyList<AttributeKind> All { get; } =
        (AttributeKind[])Enum.GetValues(typeof(AttributeKind));

    public int this[AttributeKind kind]
    {
        get => kind switch
        {
            AttributeKind.Might => Might,
            AttributeKind.Agility => Agility,
            AttributeKind.Endurance => Endurance,
            AttributeKind.Intellect => Intellect,
            AttributeKind.Willpower => Willpower,
            AttributeKind.Presence => Presence,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        set
        {
            switch (kind)
            {
                case AttributeKind.Might: Might = value; break;
                case AttributeKind.Agility: Agility = value; break;
                case AttributeKind.Endurance: Endurance = value; break;
                case AttributeKind.Intellect: Intellect = value; break;
                case AttributeKind.Willpower: Willpower = value; break;
                case AttributeKind.Presence: Presence = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
    }

    /// <summary>Sum of all six attributes. A fresh character totals 6.</summary>
    public int Total => Might + Agility + Endurance + Intellect + Willpower + Presence;

    public AttributeSet Clone() => (AttributeSet)MemberwiseClone();
}
