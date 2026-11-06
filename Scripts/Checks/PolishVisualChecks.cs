using DevilGate.Scripts.Data;
using DevilGate.Scripts.Systems;
using Godot;
namespace DevilGate.Scripts.Checks;
public partial class PolishVisualChecks : Node
{
    private async Task Frames(int n=12) { for (int i=0;i<n;i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    public override async void _Ready()
    {
        int failed = 0;
        try
        {
            var session = GameSession.Instance!; session.StartTemporaryRun();
            var run = session.CurrentRun!; run.InductionStage=3; run.MagicAwakened=true; run.LearnedSpellIds=new[]{"ember_bolt","arcane_ward"}; run.WakingIntroductionSeen=true;
            var house=GD.Load<PackedScene>("res://Scenes/Safehouse.tscn").Instantiate<SafehouseController>(); AddChild(house); await Frames(35);
            var interaction=house.GetChildren().OfType<InteractionController>().Single();
            foreach (var (size,scale) in new[]{(new Vector2I(1280,800),1f),(new Vector2I(960,600),1.2f)})
            {
                GetWindow().Size=size; ComfortSettings.TextScale=scale; await Frames(20);
                foreach (string panel in new[]{"settings","journal","chest","training","battle"})
                {
                    if(panel=="settings") interaction.ShowSettings();
                    if(panel=="journal") interaction.ShowJournal();
                    if(panel=="chest") interaction.ShowChestPanel();
                    if(panel=="training") interaction.ShowMagicTraining();
                    if(panel=="battle")
                    {
                        run.LocationId="woodland"; run.LearnedSpellIds=new[]{"shadow_veil","mage_light"};
                        run.WoodlandBattle.Stage=1; run.WoodlandBattle.EnemyHp=18; run.WoodlandBattle.Ward=1; run.WoodlandBattle.Veil=2; run.WoodlandBattle.Lit=true;
                        run.Character.Hp-=1; run.Backpack[1]=new ItemStack(Ids.ItemHealthPotion,1);
                        interaction.ShowWoodlandBattle();
                        var battle = interaction.TopPanel!;
                        battle.FindChildren("*", "CheckBox", true, false).OfType<CheckBox>().Single().ButtonPressed=true;
                        battle.FindChildren("BattleItemsButton", "Button", true, false).OfType<Button>().Single().EmitSignal(BaseButton.SignalName.Pressed);
                    }
                    await Frames(20);
                    var top=interaction.TopPanel!;
                    bool contained = true;
                    for (int tab=0; tab<25; tab++)
                    {
                        interaction._Input(new InputEventKey { Keycode=Key.Tab, Pressed=true });
                        var focus=GetViewport().GuiGetFocusOwner();
                        contained &= focus != null && top.IsAncestorOf(focus);
                    }
                    if (!contained) failed++;
                    GD.Print($"POLISH VISUAL {(contained?"PASS":"FAIL")}: keyboard focus stays within {panel}");
                    var rect=top.GetGlobalRect(); var viewport=GetViewport().GetVisibleRect();
                    bool fits=viewport.Encloses(rect);
                    GD.Print($"POLISH VISUAL {(fits?"PASS":"FAIL")}: {panel} {size} scale={scale} panel={rect} viewport={viewport}"); if(!fits) failed++;
                    if (DisplayServer.GetName()!="headless")
                    {
                        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                        GetViewport().GetTexture().GetImage().SavePng($"/tmp/devilgate-polish/{panel}-{size.X}.png");
                    }
                    if (panel=="battle") { run.WoodlandBattle.Stage=2; run.WoodlandBattle.EnemyHp=0; }
                    interaction.HandleEscape();
                    if (panel=="battle") { run.LocationId="safehouse"; run.WoodlandBattle=new(); }
                    await Frames();
                }
            }
            session._Notification((int)NotificationWMCloseRequest); await Frames();
            var confirmation=session.GetChildren().OfType<ConfirmationDialog>().Single();
            if(!confirmation.Visible) failed++;
            confirmation.EmitSignal(ConfirmationDialog.SignalName.Canceled);
            GD.Print("POLISH VISUAL: unsaved OS-close route requires confirmation and can be cancelled");
            ComfortSettings.TextScale=1;
            SaveServiceTestDirectory.UseTempDirectory();
            session.CurrentRun!.Backpack[0] = new ItemStack(Ids.ItemSaveCrystal, 1);
            session.SaveGame();
            if (!SaveService.TryLoadSave(out var saved, out _) || !DateTimeOffset.TryParse(saved?.SavedAtUtc, out _)) failed++;
            house.QueueFree(); await Frames();
            var title = GD.Load<PackedScene>("res://Scenes/Title.tscn").Instantiate<TitleController>(); AddChild(title); await Frames(20);
            if (DisplayServer.GetName() != "headless") { await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw); GetViewport().GetTexture().GetImage().SavePng("/tmp/devilgate-polish/continue-960.png"); }
            SaveServiceTestDirectory.Cleanup();
        }
        catch(Exception ex) { GD.PrintErr(ex); failed++; }
        GD.Print($"POLISH VISUAL CHECKS: {failed} failures"); GetTree().Quit(failed==0?0:1);
    }
}
