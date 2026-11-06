using DevilGate.Scripts.Data;
using DevilGate.Scripts.Systems;
using Godot;
namespace DevilGate.Scripts;

/// <summary>Read-only view of existing quest state and already discovered story.</summary>
public partial class JournalPanel : PanelContainer, ICloseable
{
    public event Action? CloseRequested;
    public static string Summary(RunState r)
    {
        var lines = new List<string> { "CURRENT OBJECTIVE", OpeningGuidance.Next(r).Text, "", "COMPLETED STEPS" };
        if (r.CreationStage > 0) lines.Add("✓ Identity discovered");
        if (r.CreationStage > 1) lines.Add("✓ Training and starter kit collected");
        if (r.InductionStage >= 2) lines.Add("✓ Registered with Diane · Guild card and access drive");
        if (r.InductionStage >= 3) lines.Add("✓ Guild account activated · Induction allowance collected");
        if (r.LearnedSpellIds.Length == 2) lines.Add("✓ Two starter skills equipped");
        if (r.WoodlandBattle.Stage == 2) lines.Add("✓ First field test · 20 XP and 5 gold collected");
        if (r.Echo.Stage >= 2) lines.Add("✓ Grove relay inspected");
        if (r.Echo.Stage >= 3) lines.Add("✓ Guardian disabled · 15 XP collected");
        if (r.Echo.Stage >= 4) lines.Add("✓ Relay restored · Filter sample sealed");
        lines.Add("\nFIRST CONNECTION · " + (r.InductionStage == 3 ? "Completed" : r.InductionStage > 0 ? "Accepted" : "Available"));
        lines.Add(InductionMission.Description(r));
        lines.Add("\nECHO CONTRACT · " + (r.Echo.Stage == 5 ? "Completed" : r.Echo.Stage > 0 ? "Accepted" : EchoMission.Available(r) ? "Available" : "Locked"));
        lines.Add(EchoMission.Description(r));
        lines.Add("\nReward: 20 gold · 35 XP total (15 guardian, 20 report).");
        if (r.Echo.RecordingHeard) lines.Add("\nDISCOVERED RECORDING\n" + EchoMission.Recording);
        return string.Join("\n", lines);
    }
    public override void _Ready()
    {
        AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color("14212ff5"), BorderColor = new Color("47746f"), BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1, CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8, ContentMarginLeft = 18, ContentMarginRight = 18, ContentMarginTop = 14, ContentMarginBottom = 14 });
        CustomMinimumSize = new Vector2(650, 540);
        var v = new VBoxContainer(); AddChild(v);
        v.AddChild(new Label { Text = "Journal · Objectives and archive", HorizontalAlignment = HorizontalAlignment.Center });
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 440), SizeFlagsVertical = SizeFlags.ExpandFill }; v.AddChild(scroll);
        scroll.AddChild(new Label { Text = Summary(GameSession.Instance!.CurrentRun!), AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var close = new Button { Text = "Close journal" }; v.AddChild(close); close.Pressed += () => CloseRequested?.Invoke();
    }
}
