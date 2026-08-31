using System.IO;
using Microsoft.Win32;

namespace HexaDock.Services;

public static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "HexaDock";
    private static string ExecutablePath => Environment.ProcessPath ?? throw new InvalidOperationException("Executable path is unavailable.");
    private static string LegacyShortcut => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "HexaDock.lnk");

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        var value = key?.GetValue(ValueName)?.ToString()?.Trim('"');
        return string.Equals(value, ExecutablePath, StringComparison.OrdinalIgnoreCase) || File.Exists(LegacyShortcut);
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, true);
        if (enabled) key.SetValue(ValueName, $"\"{ExecutablePath}\"", RegistryValueKind.String);
        else key.DeleteValue(ValueName, false);
        if (File.Exists(LegacyShortcut)) File.Delete(LegacyShortcut);
    }
}
