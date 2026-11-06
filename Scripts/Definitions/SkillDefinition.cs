using DevilGate.Scripts.Data;
using Godot;

namespace DevilGate.Scripts.Definitions;

/// <summary>A named skill. Trained-ness comes from the character's archetype.</summary>
public partial class SkillDefinition : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public AttributeKind Attribute { get; set; } = AttributeKind.Intellect;
    [Export] public string Description { get; set; } = "";
}
