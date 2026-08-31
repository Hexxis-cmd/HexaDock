using System.IO;
using System.Text.Json;
using HexaDock.Models;

namespace HexaDock.Services;

public static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public static string PathName => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HexaDock", "settings.json");

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
            settings.MonitorPositions ??= [];
            settings.Vault ??= [];
            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(PathName)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $"settings-{Guid.NewGuid():N}.tmp");
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(temporary, PathName, true);
    }
}
