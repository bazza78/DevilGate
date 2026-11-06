using Godot;
namespace DevilGate.Scripts.Systems;

/// <summary>Device preferences are separate from the crystal-funded adventure slot.</summary>
public static class ComfortSettings
{
    public static string ConfigPath { get; set; } = "user://comfort.cfg";
    public static float Master { get; set; } = .8f;
    public static float Effects { get; set; } = .7f;
    public static float Ambience { get; set; } = .4f;
    public static bool FastBattle { get; set; }
    public static bool ReducedMotion { get; set; }
    public static bool ReducedFlash { get; set; }
    public static float TextScale { get; set; } = 1;
    public static double BattleTime => ReducedMotion ? .15 : FastBattle ? .35 : 1;
    public static event Action? Changed;
    public static void Load()
    {
        using var c = new ConfigFile();
        if (c.Load(ConfigPath) != Error.Ok) return;
        Master = Mathf.Clamp((float)c.GetValue("audio", "master", .8f), 0, 1);
        Effects = Mathf.Clamp((float)c.GetValue("audio", "effects", .7f), 0, 1);
        Ambience = Mathf.Clamp((float)c.GetValue("audio", "ambience", .4f), 0, 1);
        FastBattle = (bool)c.GetValue("display", "fast", false);
        ReducedMotion = (bool)c.GetValue("display", "motion", false);
        ReducedFlash = (bool)c.GetValue("display", "flash", false);
        TextScale = Mathf.Clamp((float)c.GetValue("display", "text", 1f), 1, 1.2f);
    }
    public static Error Save()
    {
        using var c = new ConfigFile();
        c.SetValue("audio", "master", Master); c.SetValue("audio", "effects", Effects); c.SetValue("audio", "ambience", Ambience);
        c.SetValue("display", "fast", FastBattle); c.SetValue("display", "motion", ReducedMotion); c.SetValue("display", "flash", ReducedFlash); c.SetValue("display", "text", TextScale);
        Changed?.Invoke();
        return c.Save(ConfigPath);
    }
}
