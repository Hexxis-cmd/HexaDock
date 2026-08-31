using System.Diagnostics;
using HexaDock.Linux.Models;

namespace HexaDock.Linux.Services;

public static class DesktopIndex
{
    private const int DesktopMaxDepth = 2;
    private const int AddedSourceMaxDepth = 3;
    private const int MaxIndexedItems = 2_000;
    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
        { ".git", ".svn", ".hg", ".idea", ".vscode", "node_modules", "bin", "obj", "packages", ".venv", "venv", "__pycache__" };
    private static readonly HashSet<string> Photos = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tif", ".tiff", ".heic", ".svg" };
    private static readonly HashSet<string> Music = new(StringComparer.OrdinalIgnoreCase)
        { ".mp3", ".wav", ".flac", ".m4a", ".aac", ".ogg", ".wma" };
    private static readonly HashSet<string> Videos = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".mkv", ".mov", ".avi", ".webm", ".wmv", ".m4v" };
    private static readonly HashSet<string> Documents = new(StringComparer.OrdinalIgnoreCase)
        { ".pdf", ".doc", ".docx", ".odt", ".txt", ".rtf", ".md", ".xls", ".xlsx", ".ods", ".csv", ".ppt", ".pptx", ".odp" };
    private static readonly HashSet<string> Archives = new(StringComparer.OrdinalIgnoreCase)
        { ".zip", ".7z", ".rar", ".tar", ".gz", ".bz2", ".xz", ".iso" };
    private static readonly HashSet<string> Code = new(StringComparer.OrdinalIgnoreCase)
        { ".sln", ".csproj", ".py", ".js", ".ts", ".tsx", ".jsx", ".html", ".css", ".json", ".yaml", ".yml", ".sh" };

    public static IReadOnlyList<DesktopItem> Scan(string desktop, IEnumerable<string> additionalSources)
    {
        var items = new List<DesktopItem>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        ScanDirectory(desktop, "Desktop", DesktopMaxDepth, items, seen);
        foreach (var source in additionalSources.Where(Directory.Exists).Distinct(StringComparer.Ordinal))
            if (!source.Equals(desktop, StringComparison.Ordinal))
                ScanDirectory(source, new DirectoryInfo(source).Name, AddedSourceMaxDepth, items, seen);
        return items;
    }

    private static void ScanDirectory(string path, string source, int maxDepth, List<DesktopItem> items, HashSet<string> seen)
    {
        if (!Directory.Exists(path)) return;
        var pending = new Stack<(DirectoryInfo Directory, int Depth)>();
        pending.Push((new DirectoryInfo(path), 0));
        while (pending.Count > 0 && items.Count < MaxIndexedItems)
        {
            var current = pending.Pop();
            FileSystemInfo[] entries;
            try { entries = current.Directory.GetFileSystemInfos(); }
            catch (UnauthorizedAccessException) { continue; }
            catch (IOException) { continue; }
            foreach (var entry in entries)
            {
                try
                {
                    if (ShouldSkip(entry) || !seen.Add(entry.FullName)) continue;
                    if (current.Depth == 0 || entry is not DirectoryInfo) items.Add(Create(entry, source));
                    if (entry is DirectoryInfo directory && current.Depth < maxDepth &&
                        !IgnoredDirectories.Contains(directory.Name) &&
                        (directory.Attributes & FileAttributes.ReparsePoint) == 0)
                        pending.Push((directory, current.Depth + 1));
                }
                catch (UnauthorizedAccessException) { }
                catch (IOException) { }
            }
        }
    }

    public static string Classify(string path, bool isDirectory)
    {
        if (isDirectory) return "Folders";
        var extension = Path.GetExtension(path);
        if (Photos.Contains(extension)) return "Photos";
        if (Music.Contains(extension)) return "Music";
        if (Videos.Contains(extension)) return "Videos";
        if (Documents.Contains(extension)) return "Documents";
        if (Archives.Contains(extension)) return "Archives";
        if (Code.Contains(extension)) return "Projects";
        if (extension.Equals(".desktop", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".appimage", StringComparison.OrdinalIgnoreCase)) return "Apps";
        return "Other";
    }

    public static void Open(string path) => Process.Start(new ProcessStartInfo("xdg-open", [path]) { UseShellExecute = false });
    public static void Reveal(string path)
    {
        var directory = Directory.Exists(path) ? path : Path.GetDirectoryName(path)!;
        Process.Start(new ProcessStartInfo("xdg-open", [directory]) { UseShellExecute = false });
    }

    private static DesktopItem Create(FileSystemInfo item, string source)
    {
        var isDirectory = item is DirectoryInfo;
        var category = Classify(item.FullName, isDirectory);
        var type = isDirectory ? "Folder" : Path.GetExtension(item.Name).TrimStart('.').ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(type)) type = "File";
        return new DesktopItem
        {
            Name = item.Name,
            Category = category,
            Type = type,
            Modified = item.LastWriteTime,
            FullPath = item.FullName,
            Source = source,
            Size = item is FileInfo file ? file.Length : 0,
            IconGlyph = category switch
            {
                "Folders" => "▰", "Photos" => "▧", "Music" => "♫", "Videos" => "▶",
                "Documents" => "▤", "Archives" => "◆", "Projects" => "⌘", "Apps" => "⬡", _ => "◇"
            }
        };
    }

    private static bool ShouldSkip(FileSystemInfo item) => item.Name.StartsWith('.') ||
        (item.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0;
}
