using System.Text.Json;
using HexaDock.Linux.Models;

namespace HexaDock.Linux.Services;

public static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public static string PathName => Path.Combine(LinuxPaths.ConfigRoot, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            var settings = File.Exists(PathName)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(PathName)) ?? new AppSettings()
                : new AppSettings();
            settings.Sources ??= [];
            settings.Favorites ??= [];
            settings.Recent = new Dictionary<string, DateTime>(settings.Recent ?? [], StringComparer.OrdinalIgnoreCase);
            settings.Vault ??= [];
            return settings;
        }
        catch { return new AppSettings(); }
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(LinuxPaths.ConfigRoot);
        var temporary = Path.Combine(LinuxPaths.ConfigRoot, $"settings-{Guid.NewGuid():N}.tmp");
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(temporary, PathName, true);
        TryPrivatePermissions(PathName);
    }

    public static void TryPrivatePermissions(string path)
    {
        if (!OperatingSystem.IsLinux()) return;
        try { File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite); }
        catch { }
    }
}
