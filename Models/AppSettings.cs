namespace HexaDock.Models;

public sealed class AppSettings
{
    public double Left { get; set; } = double.NaN;
    public double Top { get; set; } = double.NaN;
    public bool HideDesktopIcons { get; set; } = true;
    public List<string> Sources { get; set; } = [];
    public List<string> Favorites { get; set; } = [];
    public Dictionary<string, DateTime> Recent { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, WindowPosition> MonitorPositions { get; set; } = [];
    public string LastMonitor { get; set; } = "";
    public List<VaultItem> Vault { get; set; } = [];
    public PinSettings? Pin { get; set; }
}

public sealed class WindowPosition
{
    public double Left { get; set; }
    public double Top { get; set; }
}

public sealed class VaultItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public long Size { get; set; }
    public DateTime Added { get; set; }
}

public sealed class PinSettings
{
    public string PinSalt { get; set; } = "";
    public string PinHash { get; set; } = "";
    public string Question { get; set; } = "";
    public string AnswerSalt { get; set; } = "";
    public string AnswerHash { get; set; } = "";
}
