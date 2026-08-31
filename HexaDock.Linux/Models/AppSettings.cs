namespace HexaDock.Linux.Models;

public sealed class AppSettings
{
    public int Left { get; set; } = -1;
    public int Top { get; set; } = -1;
    public bool StartWithDesktop { get; set; }
    public List<string> Sources { get; set; } = [];
    public List<string> Favorites { get; set; } = [];
    public Dictionary<string, DateTime> Recent { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<VaultItem> Vault { get; set; } = [];
}

public sealed class VaultItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public long Size { get; set; }
    public DateTime Added { get; set; }
}
