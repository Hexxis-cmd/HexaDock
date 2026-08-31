namespace HexaDock.Linux.Services;

public static class SelfTest
{
    public static void Run()
    {
        if (DesktopIndex.Classify("photo.webp", false) != "Photos" ||
            DesktopIndex.Classify("song.flac", false) != "Music" ||
            DesktopIndex.Classify("project", true) != "Folders" ||
            FuzzySearch.Score("hxd", "HexaDock") <= FuzzySearch.Score("hxd", "unrelated"))
            throw new InvalidOperationException("Linux classification/search self-test failed.");

        var root = Path.Combine(Path.GetTempPath(), $"HexaDock-index-{Guid.NewGuid():N}");
        try
        {
            var nested = Directory.CreateDirectory(Path.Combine(root, "Organized", "Music"));
            var song = Path.Combine(nested.FullName, "track.mp3");
            File.WriteAllBytes(song, [0]);
            var tooDeep = Path.Combine(Directory.CreateDirectory(Path.Combine(nested.FullName, "Artist")).FullName, "deep.mp3");
            File.WriteAllBytes(tooDeep, [0]);
            var items = DesktopIndex.Scan(root, []);
            if (!items.Any(item => item.FullPath == song) || items.Any(item => item.FullPath == tooDeep))
                throw new InvalidOperationException("Linux bounded indexing self-test failed.");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }

        VaultService.RunSelfTest();
    }
}
