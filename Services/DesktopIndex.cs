using System.Diagnostics;
using System.IO;
using HexaDock.Models;

namespace HexaDock.Services;

public static class DesktopIndex
{
    private const int DesktopMaxDepth = 1;
    private const int AddedSourceMaxDepth = 2;
    private const int MaxIndexedItems = 2_000;
    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
        { ".git", ".svn", ".hg", ".vs", ".idea", "node_modules", "bin", "obj", "packages", ".venv", "venv", "__pycache__" };
    private static readonly HashSet<string> Photos = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tif", ".tiff", ".heic", ".svg" };
    private static readonly HashSet<string> Music = new(StringComparer.OrdinalIgnoreCase)
        { ".mp3", ".wav", ".flac", ".m4a", ".aac", ".ogg", ".wma" };
    private static readonly HashSet<string> Videos = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".mkv", ".mov", ".avi", ".webm", ".wmv", ".m4v" };
    private static readonly HashSet<string> Documents = new(StringComparer.OrdinalIgnoreCase)
        { ".pdf", ".doc", ".docx", ".txt", ".rtf", ".md", ".xls", ".xlsx", ".csv", ".ppt", ".pptx" };
    private static readonly HashSet<string> Archives = new(StringComparer.OrdinalIgnoreCase)
        { ".zip", ".7z", ".rar", ".tar", ".gz", ".iso" };
    private static readonly HashSet<string> Code = new(StringComparer.OrdinalIgnoreCase)
        { ".sln", ".csproj", ".py", ".js", ".ts", ".tsx", ".jsx", ".html", ".css", ".json", ".yaml", ".yml", ".ps1", ".bat", ".cmd" };

    public static IReadOnlyList<DesktopItem> Scan(string desktop, IEnumerable<string> additionalSources)
    {
        var items = new List<DesktopItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ScanDirectory(desktop, "Desktop", true, false, DesktopMaxDepth, items, seen);
        var publicDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
        if (!publicDesktop.Equals(desktop, StringComparison.OrdinalIgnoreCase))
            ScanDirectory(publicDesktop, "Public Desktop", true, false, DesktopMaxDepth, items, seen);
        foreach (var source in additionalSources.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
            if (!source.Equals(desktop, StringComparison.OrdinalIgnoreCase) && !source.Equals(publicDesktop, StringComparison.OrdinalIgnoreCase))
                ScanDirectory(source, new DirectoryInfo(source).Name, false, true, AddedSourceMaxDepth, items, seen);
        return items;
    }

    private static void ScanDirectory(string path, string source, bool useDesktopCategories, bool includeAllNestedFiles, int maxDepth, List<DesktopItem> items, HashSet<string> seen)
    {
        if (!Directory.Exists(path)) return;
        var pending = new Stack<(DirectoryInfo Directory, string? ForcedCategory, int Depth)>();
        pending.Push((new DirectoryInfo(path), null, 0));
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
                    var forcedCategory = current.ForcedCategory;
                    var organizer = current.Depth == 0 && useDesktopCategories && entry is DirectoryInfo &&
                        (entry.Name.Equals("Apps & Tools", StringComparison.OrdinalIgnoreCase) ||
                         entry.Name.Equals("Games - Installed", StringComparison.OrdinalIgnoreCase));
                    if (organizer)
                        forcedCategory = entry.Name.StartsWith("Games", StringComparison.OrdinalIgnoreCase) ? "Games" : "Apps";
                    else if (current.Depth == 0 || entry is not DirectoryInfo &&
                             (includeAllNestedFiles || forcedCategory is not null || IsUserFacingNestedFile(entry.FullName)))
                        items.Add(Create(entry, entry is DirectoryInfo ? null : forcedCategory, source));

                    if (entry is DirectoryInfo directory && current.Depth < maxDepth &&
                        !IgnoredDirectories.Contains(directory.Name) &&
                        (directory.Attributes & FileAttributes.ReparsePoint) == 0)
                        pending.Push((directory, forcedCategory, current.Depth + 1));
                }
                catch (UnauthorizedAccessException) { }
                catch (IOException) { }
            }
        }
    }

    public static string Classify(string path, bool isDirectory, string? forcedCategory = null)
    {
        if (forcedCategory is not null) return forcedCategory;
        if (isDirectory) return "Folders";
        var extension = Path.GetExtension(path);
        if (Photos.Contains(extension)) return "Photos";
        if (Music.Contains(extension)) return "Music";
        if (Videos.Contains(extension)) return "Videos";
        if (Documents.Contains(extension)) return "Documents";
        if (Archives.Contains(extension)) return "Archives";
        if (Code.Contains(extension)) return "Projects";
        if (extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase) || extension.Equals(".url", StringComparison.OrdinalIgnoreCase)) return "Apps";
        return "Other";
    }

    public static void Open(string path) => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });

    public static void Reveal(string path)
    {
        var arguments = Directory.Exists(path) ? $"\"{path}\"" : $"/select,\"{path}\"";
        Process.Start(new ProcessStartInfo("explorer.exe", arguments) { UseShellExecute = true });
    }

    public static void RunSelfTest()
    {
        if (Classify("photo.webp", false) != "Photos" ||
            Classify("song.flac", false) != "Music" ||
            Classify("project", true) != "Folders" ||
            Classify("game.lnk", false, "Games") != "Games")
            throw new InvalidOperationException("Desktop classification self-test failed.");

        var security = PinService.Create("1234", "First project?", "Alpha");
        if (!PinService.VerifyPin(security, "1234") || PinService.VerifyPin(security, "9999") ||
            !PinService.VerifyAnswer(security, " alpha "))
            throw new InvalidOperationException("Local PIN self-test failed.");

        if (FuzzySearch.Score("hxd", "HexaDock") <= FuzzySearch.Score("hxd", "unrelated"))
            throw new InvalidOperationException("Fuzzy search self-test failed.");

        var constrained = MonitorService.Constrain(1_000_000, -1_000_000, 70, 70);
        if (constrained.Left < constrained.Monitor.Left || constrained.Left + 70 > constrained.Monitor.Right ||
            constrained.Top < constrained.Monitor.Top || constrained.Top + 70 > constrained.Monitor.Bottom)
            throw new InvalidOperationException("Monitor boundary self-test failed.");

        VaultService.RunSelfTest();

        var testRoot = Path.Combine(Path.GetTempPath(), $"HexaDock-index-{Guid.NewGuid():N}");
        try
        {
            var organized = Directory.CreateDirectory(Path.Combine(testRoot, "Organized"));
            var song = Path.Combine(organized.FullName, "track.mp3");
            File.WriteAllBytes(song, [0]);
            var tooDeep = Path.Combine(Directory.CreateDirectory(Path.Combine(organized.FullName, "Artist")).FullName, "deep.mp3");
            File.WriteAllBytes(tooDeep, [0]);
            var generated = Directory.CreateDirectory(Path.Combine(testRoot, "Project", "node_modules"));
            var dependency = Path.Combine(generated.FullName, "dependency.js");
            File.WriteAllBytes(dependency, [0]);
            var items = new List<DesktopItem>();
            ScanDirectory(testRoot, "Desktop", true, false, DesktopMaxDepth, items, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            if (!items.Any(item => item.FullPath == song && item.Category == "Music") ||
                items.Any(item => item.FullPath == tooDeep || item.FullPath == dependency))
                throw new InvalidOperationException("Bounded Desktop indexing self-test failed.");
        }
        finally
        {
            if (Directory.Exists(testRoot)) Directory.Delete(testRoot, true);
        }
    }

    private static DesktopItem Create(FileSystemInfo item, string? category, string source)
    {
        var isDirectory = item is DirectoryInfo;
        var type = isDirectory ? "Folder" : Path.GetExtension(item.Name).TrimStart('.').ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(type)) type = "File";
        if (type == "LNK") type = "Shortcut";
        if (type == "URL") type = "Web shortcut";
        return new DesktopItem
        {
            Name = item.Name,
            Category = Classify(item.FullName, isDirectory, category),
            Type = type,
            Modified = item.LastWriteTime,
            FullPath = item.FullName,
            Source = source,
            Size = item is FileInfo file ? file.Length : 0,
            Icon = IconService.Load(item.FullName, isDirectory)
        };
    }

    private static bool ShouldSkip(FileSystemInfo item) =>
        item.Name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase) ||
        item.Name.Equals("HexaDock.lnk", StringComparison.OrdinalIgnoreCase) ||
        (item.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0;

    private static bool IsUserFacingNestedFile(string path)
    {
        var category = Classify(path, false);
        return category is "Photos" or "Music" or "Videos" or "Documents" or "Archives" or "Apps";
    }
}
