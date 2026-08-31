namespace HexaDock.Linux.Services;

public static class StartupService
{
    private static string EntryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "autostart", "hexadock.desktop");
    public static bool IsEnabled => File.Exists(EntryPath);

    public static void Set(bool enabled)
    {
        if (!enabled) { if (File.Exists(EntryPath)) File.Delete(EntryPath); return; }
        Directory.CreateDirectory(Path.GetDirectoryName(EntryPath)!);
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Executable path unavailable.");
        File.WriteAllText(EntryPath, $"[Desktop Entry]\nType=Application\nName=HexaDock\nExec=\"{executable}\"\nTerminal=false\nX-GNOME-Autostart-enabled=true\n");
    }
}
