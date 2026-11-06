using DevilGate.Scripts.Data;
using Godot;

namespace DevilGate.Scripts.Definitions;

/// <summary>
/// One of the five fixed archetype/species combinations. Resources are the
/// authored source of truth; stable lowercase IDs connect them to save data.
/// </summary>
public partial class ArchetypeDefinition : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public string SpeciesId { get; set; } = "";
    [Export] public string SpeciesDisplayName { get; set; } = "";
    [Export] public Color ThemeColor { get; set; } = Colors.White;
    [Export] public string WeaponItemId { get; set; } = "";
    [Export] public string[] TrainedSkillIds { get; set; } = System.Array.Empty<string>();

    [Export] public int SuggestedMight { get; set; } = 1;
    [Export] public int SuggestedAgility { get; set; } = 1;
    [Export] public int SuggestedEndurance { get; set; } = 1;
    [Export] public int SuggestedIntellect { get; set; } = 1;
    [Export] public int SuggestedWillpower { get; set; } = 1;
    [Export] public int SuggestedPresence { get; set; } = 1;

    public AttributeSet CreateSuggestedAttributes() => new()
    {
        Might = SuggestedMight,
        Agility = SuggestedAgility,
        Endurance = SuggestedEndurance,
        Intellect = SuggestedIntellect,
        Willpower = SuggestedWillpower,
        Presence = SuggestedPresence,
    };

    public bool HasTrainedSkill(string skillId) => TrainedSkillIds.Contains(skillId);

    /// <summary>Portrait key contract: &lt;archetype&gt;_&lt;male|female&gt;.</summary>
    public string PortraitKey(string genderId) => $"{Id}_{genderId}";

    public Texture2D? LoadPortrait(string genderId)
    {
        var supplied = $"res://Assets/Portraits/{PortraitKey(genderId)}.png";
        if (ResourceLoader.Exists(supplied)) return GD.Load<Texture2D>(supplied);
        var path = $"res://Assets/Placeholders/Portraits/{PortraitKey(genderId)}.png";
        return ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
    }
}
