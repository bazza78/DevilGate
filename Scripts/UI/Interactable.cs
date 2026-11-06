using DevilGate.Scripts.World;
using Godot;

namespace DevilGate.Scripts;

/// <summary>
/// A clickable thing in the world. Clicking near <see cref="Focus"/> walks
/// the player to <see cref="Approach"/> and runs <see cref="OnInteract"/>
/// once on arrival.
/// </summary>
public sealed class Interactable
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required Vector2 Focus { get; init; }
    public required Vector2 Approach { get; init; }
    public float ClickRadius { get; init; } = SafehouseLayout.InteractableClickRadius;
    public required Action OnInteract { get; init; }
}
