using DevilGate.Scripts.Systems;

namespace DevilGate.Scripts.Checks;

/// <summary>
/// Redirects SaveService at a temp directory so checks never touch the
/// player's real save slot.
/// </summary>
public static class SaveServiceTestDirectory
{
    private static string? _dir;

    public static void UseTempDirectory()
    {
        _dir = Path.Combine(Path.GetTempPath(), "devilgate_checks_" + Guid.NewGuid().ToString("N"));
        SaveService.DirectoryOverride = _dir;
        SaveService.DeleteSave();
    }

    /// <summary>Point saves at a path that cannot be created, forcing write failures.</summary>
    public static void BreakWrites()
    {
        var blocker = Path.Combine(Path.GetTempPath(), "devilgate_blocker_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.GetTempPath());
        File.WriteAllText(blocker, "not a directory");
        SaveService.DirectoryOverride = blocker;
    }

    /// <summary>Restore the temp directory after a simulated failure.</summary>
    public static void Restore()
    {
        if (_dir != null)
        {
            SaveService.DirectoryOverride = _dir;
        }
    }

    public static void Cleanup()
    {
        SaveService.DirectoryOverride = null;
        if (_dir != null)
        {
            try
            {
                if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
            }
            catch
            {
                // best effort; temp dirs are reclaimed by the OS
            }
            _dir = null;
        }
    }
}
