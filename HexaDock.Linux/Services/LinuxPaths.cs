namespace HexaDock.Linux.Services;

public static class LinuxPaths
{
    public static string ConfigRoot => Root("XDG_CONFIG_HOME", ".config", "hexadock");
    public static string DataRoot => Root("XDG_DATA_HOME", ".local/share", "hexadock");

    public static string Desktop
    {
        get
        {
            var overridePath = Environment.GetEnvironmentVariable("HEXADOCK_DESKTOP");
            if (!string.IsNullOrWhiteSpace(overridePath) && Directory.Exists(overridePath)) return overridePath;
            var configured = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "user-dirs.dirs");
            if (File.Exists(configured))
            {
                var line = File.ReadLines(configured).FirstOrDefault(value => value.StartsWith("XDG_DESKTOP_DIR=", StringComparison.Ordinal));
                if (line is not null)
                {
                    var value = line[(line.IndexOf('=') + 1)..].Trim().Trim('"').Replace("$HOME", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
                    if (Directory.Exists(value)) return value;
                }
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop");
        }
    }

    private static string Root(string variable, string fallback, string app)
    {
        var root = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(root)) root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), fallback);
        return Path.Combine(root, app);
    }
}
