using DevilGate.Scripts.Data;
using DevilGate.Scripts.Definitions;
using DevilGate.Scripts.Systems;
using Godot;
namespace DevilGate.Scripts.Checks;

/// <summary>Reproducible observation, not a claim about human play feel. No rules are tuned here.</summary>
public partial class BalanceChecks : Node
{
    private sealed class SeedDice(int seed) : IDieSource { private readonly Random _random = new(seed); public int RollD20() => _random.Next(1,21); }
    public override void _Ready()
    {
        int builds = 0, failedBuilds = 0;
        GD.Print("BALANCE CSV: class,gender,pair,runs,wins,wispTurns,guardianTurns,energyUsed,potions,misses,retreats");
        foreach (string archId in new[] { Ids.ArchetypeMage, Ids.ArchetypeTank, Ids.ArchetypeHealer, Ids.ArchetypeArcher })
        foreach (string gender in new[] { Ids.GenderMale, Ids.GenderFemale })
        {
            var arch = GameCatalog.Instance.GetArchetype(archId);
            var skills = archId == Ids.ArchetypeArcher ? StarterTraining.ArcherSkills : archId == Ids.ArchetypeHealer ? StarterTraining.HealerSkills : archId == Ids.ArchetypeTank ? StarterTraining.TankSkills : MageTraining.Spells;
            for (int a = 0; a < skills.Length; a++) for (int b = a + 1; b < skills.Length; b++)
            {
                int wins = 0, wispTurns = 0, guardianTurns = 0, energy = 0, potions = 0, misses = 0, retreats = 0;
                for (int seed = 0; seed < 100; seed++)
                {
                    var r = RunFactory.CreateNewRun(new CharacterCreationResult { Name = "Balance", ArchetypeId = archId, GenderId = gender, Attributes = arch.CreateSuggestedAttributes() }, GameCatalog.Instance);
                    InventoryService.TakeAll(r, GameCatalog.Instance);
                    for (int i = 0; i < 12; i++) if (r.Backpack[i] != null && GameCatalog.Instance.GetItem(r.Backpack[i]!.ItemId).Category is ItemCategory.Weapon or ItemCategory.Armour) InventoryService.EquipFromBackpack(r, i, GameCatalog.Instance);
                    r.InductionStage = 2; InductionMission.Complete(r); StarterTraining.Awaken(r); StarterTraining.Choose(r, new[] { skills[a].Id, skills[b].Id });
                    var die = new SeedDice(seed); bool success = true;
                    foreach (string location in new[] { "woodland", "grove" })
                    {
                        r.LocationId = location;
                        if (location == "grove") { r.Echo.Stage = 2; } // manual reset, no preparation advantage
                        WoodlandBattle.Start(r);
                        int turns = 0;
                        while (WoodlandBattle.Current(r).Stage == 1 && turns++ < 100)
                        {
                            var state = WoodlandBattle.Current(r);
                            string action = "staff";
                            if (r.Character.Hp <= 12 && r.Backpack.CountItem(Ids.ItemHealthPotion) > 0) action = "potion";
                            else if (r.Character.Mana >= 3)
                            {
                                if (r.LearnedSpellIds.Contains("field_dressing") && r.Character.Hp <= CharacterRules.MaxHealth(r.Character) - 8) action = "field_dressing";
                                else if (r.LearnedSpellIds.Contains("evasive_stance") && state.Veil == 0) action = "evasive_stance";
                                else if (r.LearnedSpellIds.Contains("mend") && r.Character.Hp <= CharacterRules.MaxHealth(r.Character) - 10) action = "mend";
                                else if (r.LearnedSpellIds.Contains("sanctuary") && state.IronStance == 0) action = "sanctuary";
                                else if (r.LearnedSpellIds.Contains("second_wind") && r.Character.Hp <= CharacterRules.MaxHealth(r.Character) - 8) action = "second_wind";
                                else if (r.LearnedSpellIds.Contains("iron_stance") && state.IronStance == 0) action = "iron_stance";
                                else if (r.LearnedSpellIds.Contains("arcane_ward") && state.Ward == 0) action = "arcane_ward";
                                else if (r.LearnedSpellIds.Contains("shadow_veil") && state.Veil == 0) action = "shadow_veil";
                                else action = r.LearnedSpellIds.FirstOrDefault(id => id is not ("field_dressing" or "evasive_stance" or "mend" or "sanctuary" or "second_wind" or "iron_stance" or "arcane_ward" or "shadow_veil")) ?? "staff";
                            }
                            var feedback = new BattleTurnFeedback();
                            WoodlandBattle.Act(r, action, die, feedback);
                            if (action is "potion" or "mana_potion") potions++;
                            else energy += WoodlandBattle.Cost(action);
                            if (feedback.PlayerHit == false) misses++;
                        }
                        if (location == "woodland") wispTurns += turns; else guardianTurns += turns;
                        if (WoodlandBattle.Current(r).Stage != 2) { success = false; retreats++; break; }
                    }
                    if (success) wins++;
                }
                builds++; if (wins == 0) failedBuilds++;
                GD.Print($"BALANCE CSV: {archId},{gender},{skills[a].Id}+{skills[b].Id},100,{wins},{wispTurns/100.0:F2},{guardianTurns/100.0:F2},{energy/100.0:F2},{potions/100.0:F2},{misses/100.0:F2},{retreats}");
            }
        }
        GD.Print($"BALANCE CHECKS: {builds} builds observed; {failedBuilds} without a completion");
        GetTree().Quit(failedBuilds == 0 ? 0 : 1);
    }
}
