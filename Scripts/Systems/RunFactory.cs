using DevilGate.Scripts.Data;
using DevilGate.Scripts.Definitions;
using DevilGate.Scripts.Systems;
using DevilGate.Scripts.World;

namespace DevilGate.Scripts.Systems;

/// <summary>
/// Creates fresh runs. Chest contents are seeded here exactly once -- never on
/// chest open or load.
/// </summary>
public static class RunFactory
{
    public static RunState CreateNewRun(CharacterCreationResult creation, GameCatalog catalog)
    {
        var archetype = catalog.GetArchetype(creation.ArchetypeId);
        var attributes = creation.Attributes.Clone();

        var run = new RunState
        {
            WakingIntroductionSeen = false,
            PositionX = SafehouseLayout.SpawnPoint.X,
            PositionY = SafehouseLayout.SpawnPoint.Y,
        };

        var c = run.Character;
        c.RunId = Guid.NewGuid().ToString("N");
        c.Name = creation.Name.Trim();
        c.ArchetypeId = archetype.Id;
        c.GenderId = creation.GenderId;
        c.Attributes = attributes;
        c.Hp = CharacterRules.MaxHealth(attributes);
        c.Mana = CharacterRules.MaxMana(attributes);
        c.Level = 1;
        c.Xp = 0;
        c.Diagnostic = new DiagnosticState();

        var slot = 0;
        foreach (var (itemId, quantity) in catalog.ChestSeedFor(archetype))
        {
            run.Chest.Inventory[slot++] = new ItemStack(itemId, quantity);
        }
        run.Chest.Gold = GameCatalog.StartingChestGold;

        return run;
    }
}
