using DevilGate.Scripts.Data;
using DevilGate.Scripts.Definitions;
using DevilGate.Scripts.Systems;
using Godot;
namespace DevilGate.Scripts.Checks;
public partial class PolishChecks : Node
{
    private int _fail;
    private void Check(bool ok, string text) { GD.Print($"POLISH {(ok ? "PASS" : "FAIL")}: {text}"); if (!ok) _fail++; }
    private async Task Frames(int n = 4) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private static RunState Run()
    {
        var arch = GameCatalog.Instance.GetArchetype(Ids.ArchetypeMage);
        return RunFactory.CreateNewRun(new CharacterCreationResult { Name = "Polish", ArchetypeId = arch.Id, GenderId = Ids.GenderFemale, Attributes = arch.CreateSuggestedAttributes() }, GameCatalog.Instance);
    }
    public override async void _Ready()
    {
        var cfg = ComfortSettings.ConfigPath;
        try
        {
            var r = Run();
            for (int i = 0; i < 12; i++) { r.Backpack[i] = new ItemStack(Ids.ItemHealthPotion, 9); r.Chest.Inventory[i] = null; }
            r.Backpack[0]!.Quantity = 8;
            r.Chest.Inventory[0] = new ItemStack(Ids.ItemHealthPotion, 2);
            r.Chest.Inventory[1] = new ItemStack(Ids.ItemManaPotion, 1);
            r.Chest.Gold = 7;
            var result = InventoryService.TakeAll(r, GameCatalog.Instance);
            Check(r.Chest.Inventory[0]?.Quantity == 2 && r.Chest.Inventory[1]?.Quantity == 1 && r.Backpack[0]?.Quantity == 8 && r.CarriedGold == 7, "Take All preserves non-fitting whole stacks and collects gold once");
            r.Backpack[11] = null;
            InventoryService.TakeAll(r, GameCatalog.Instance);
            Check(r.Backpack.CountItem(Ids.ItemHealthPotion) == 100 && r.Chest.Inventory[0] == null && r.Chest.Inventory[1]?.Quantity == 1 && r.CarriedGold == 7, "Take All merges then spills; later non-fitting stacks remain; gold never duplicates");
            r.Equipment.Armour = new ItemStack(Ids.ItemBasicJacket, 1);
            Check(WoodlandBattle.BaseDefence(r) == 11 && WoodlandBattle.BasicDamage(r) == 4 && ItemCardController.EffectText(GameCatalog.Instance.GetItem(Ids.ItemBasicJacket)).Contains("+1 defence"), "equipment facts match resolver");
            r.LocationId = "woodland"; r.WoodlandBattle.Stage = 1; r.Character.Attributes.Might = 1; r.Character.Attributes.Willpower = 5;
            var strike = new BattleTurnFeedback(); WoodlandBattle.Act(r, "staff", new ScriptedDieSource(8, 1), strike);
            Check(strike.PlayerHit == false && r.WoodlandBattle.EnemyHp == 18, "free staff strike rolls Might, matching the corrected tooltip");
            r = Run(); GameSession.Instance!.SetCurrentRunForTesting(r);
            var hud = GD.Load<PackedScene>("res://Scenes/HUD.tscn").Instantiate<HUDController>(); AddChild(hud); await Frames();
            hud.BackpackScrollVertical = 60; await Frames();
            int down = hud.BackpackScrollVertical;
            hud.BackpackScrollVertical = 10; await Frames();
            int up = hud.BackpackScrollVertical; GameSession.Instance.EmitChanged(); await Frames();
            Check(down > up && hud.BackpackScrollVertical == up, "real laid-out scroll can move upward and remains there after refresh");
            r.Backpack[0] = new ItemStack(Ids.ItemApprenticeStaff, 1); GameSession.Instance.EmitChanged();
            hud.BackpackCards[0]._GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, DoubleClick = true });
            Check(r.Equipment.Weapon?.ItemId == Ids.ItemApprenticeStaff, "double-click equips through the visible action's service");
            Check(!JournalPanel.Summary(r).Contains(EchoMission.Recording), "undiscovered recording stays hidden");
            r.Echo.RecordingHeard = true;
            Check(JournalPanel.Summary(r).Contains(EchoMission.Recording), "discovered recording readable in journal");
            ComfortSettings.ConfigPath = Path.Combine(Path.GetTempPath(), "devilgate-comfort-check.cfg");
            ComfortSettings.FastBattle = true; ComfortSettings.ReducedMotion = true; ComfortSettings.ReducedFlash = true; ComfortSettings.Master = .25f; ComfortSettings.TextScale = 1.2f;
            Check(ComfortSettings.Save() == Error.Ok, "preferences save independently of crystals");
            ComfortSettings.FastBattle = false; ComfortSettings.Master = 1; ComfortSettings.Load();
            Check(ComfortSettings.FastBattle && ComfortSettings.ReducedMotion && ComfortSettings.ReducedFlash && ComfortSettings.Master == .25f && ComfortSettings.TextScale == 1.2f, "audio, accessibility, scale and speed preferences round-trip");
            System.IO.File.Delete(ComfortSettings.ConfigPath);
            hud.QueueFree(); await Frames();
            // Compare actual animated turns, including duplicate clicks, using identical dice.
            string? baseline = null;
            foreach (bool fast in new[] { false, true })
            {
                r = Run(); r.LocationId = "woodland"; r.InductionStage = 3; r.MagicAwakened = true; r.LearnedSpellIds = new[] { "ember_bolt", "arcane_ward" }; WoodlandBattle.Start(r);
                GameSession.Instance.SetCurrentRunForTesting(r); ComfortSettings.FastBattle = fast;
                var battle = new WoodlandBattlePanel { Dice = new ScriptedDieSource(20, 1) }; AddChild(battle);
                var fire = battle.FindChildren("*", "Button", true, false).OfType<Button>().Single(b => b.Text.StartsWith("Ember Bolt"));
                fire.EmitSignal(BaseButton.SignalName.Pressed); fire.EmitSignal(BaseButton.SignalName.Pressed);
                // Suspend a turn whose state is already resolved; no input or timer may resolve another turn.
                GetTree().Paused = true;
                int suspendedHp = r.Character.Hp, suspendedEnemy = r.WoodlandBattle.EnemyHp;
                await ToSignal(GetTree().CreateTimer(.12, true), SceneTreeTimer.SignalName.Timeout);
                Check(battle.IsAnimating && r.Character.Hp == suspendedHp && r.WoodlandBattle.EnemyHp == suspendedEnemy, "focus suspension leaves resolved turn intact");
                GetTree().Paused = false;
                while (battle.IsAnimating) await Frames(1);
                string state = $"{r.Character.Hp}/{r.Character.Mana}/{r.WoodlandBattle.EnemyHp}/{r.Character.Xp}/{r.CarriedGold}";
                if (baseline == null) baseline = state;
                else Check(state == baseline && r.WoodlandBattle.EnemyHp == 10, "fast/reduced presentation resolves exactly the same single turn");
                battle.QueueFree(); await Frames();
            }
        }
        catch (Exception ex) { GD.PrintErr(ex); _fail++; }
        finally { ComfortSettings.ConfigPath = cfg; ComfortSettings.Load(); }
        GD.Print($"POLISH CHECKS: {_fail} failures"); GetTree().Quit(_fail == 0 ? 0 : 1);
    }
}
