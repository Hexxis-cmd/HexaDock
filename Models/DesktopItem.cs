using System.Windows.Media;

namespace HexaDock.Models;

public sealed class DesktopItem
{
    public required string Name { get; init; }
    public required string Category { get; init; }
    public required string Type { get; init; }
    public required DateTime Modified { get; init; }
    public required string FullPath { get; init; }
    public required string Source { get; init; }
    public long Size { get; init; }
    public ImageSource? Icon { get; init; }
    public bool IsVault { get; init; }
    public string? VaultId { get; init; }
}
