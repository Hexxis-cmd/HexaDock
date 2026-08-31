using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using HexaDock.Linux.Models;
using HexaDock.Linux.Services;

namespace HexaDock.Linux;

public sealed partial class MainWindow : Window
{
    private const int CollapsedSize = 74;
    private const int ExpandedWidth = 930;
    private const int ExpandedHeight = 620;
    private readonly AppSettings _settings = SettingsStore.Load();
    private readonly ObservableCollection<DesktopItem> _visible = [];
    private LinuxHotkeyService? _hotkey;
    private List<DesktopItem> _items = [];
    private bool _expanded;
    private bool _descending;
    private bool _indexing;
    private bool _refreshPending;
    private double _dragDistance;

    public MainWindow()
    {
        InitializeComponent();
        CategoryList.ItemsSource = new[] { "All", "Favorites", "Recent", "This Week", "Large Files", "Vault", "Folders", "Projects", "Apps", "Photos", "Music", "Videos", "Documents", "Archives", "Other" };
        CategoryList.SelectedIndex = 0;
        SortBox.ItemsSource = new[] { "Relevance", "Name", "Type", "Date modified" };
        SortBox.SelectedIndex = 0;
        ItemsGrid.ItemsSource = _visible;
        Opened += (_, _) => { RestorePosition(); _hotkey ??= new LinuxHotkeyService(ToggleExpanded); RefreshIndex(); };
        Closing += (_, _) => { SavePosition(); SettingsStore.Save(_settings); };
    }

    private async void RefreshIndex()
    {
        if (_indexing) { _refreshPending = true; return; }
        _indexing = true;
        StatusText.Text = "Indexing…";
        try
        {
            var sources = _settings.Sources.ToArray();
            var indexed = await Task.Run(() => DesktopIndex.Scan(LinuxPaths.Desktop, sources).ToList());
            indexed.AddRange(_settings.Vault.Where(item => File.Exists(VaultService.ItemPath(item.Id))).Select(item => new DesktopItem
            {
                Name = item.Name, Category = "Vault", Type = Path.GetExtension(item.Name).TrimStart('.').ToUpperInvariant() is { Length: > 0 } type ? type : "File",
                Modified = item.Added, FullPath = VaultService.ItemPath(item.Id), Source = "Encrypted Vault", Size = item.Size,
                IconGlyph = "⬢", IsVault = true, VaultId = item.Id
            }));
            _items = indexed;
            ApplyFilter();
        }
        catch { StatusText.Text = "Index unavailable  •  select refresh to retry"; }
        finally
        {
            _indexing = false;
            if (_refreshPending) { _refreshPending = false; RefreshIndex(); }
        }
    }

    private void ApplyFilter()
    {
        var category = CategoryList.SelectedItem as string ?? "All";
        var query = SearchBox.Text?.Trim() ?? "";
        IEnumerable<DesktopItem> results = _items.Where(item => MatchesCategory(category, item));
        if (query.Length > 0) results = results.Where(item => SearchScore(query, item) > 0);
        results = (SortBox.SelectedItem as string) switch
        {
            "Name" => _descending ? results.OrderByDescending(item => item.Name) : results.OrderBy(item => item.Name),
            "Type" => _descending ? results.OrderByDescending(item => item.Type) : results.OrderBy(item => item.Type),
            "Date modified" => _descending ? results.OrderByDescending(item => item.Modified) : results.OrderBy(item => item.Modified),
            _ => results.OrderByDescending(item => SearchScore(query, item))
        };
        _visible.Clear();
        foreach (var item in results) _visible.Add(item);
        StatusText.Text = $"{_visible.Count} shown  •  {_items.Count} indexed";
    }

    private bool MatchesCategory(string category, DesktopItem item) => category switch
    {
        "All" => true,
        "Favorites" => _settings.Favorites.Contains(item.FullPath, StringComparer.Ordinal),
        "Recent" => _settings.Recent.ContainsKey(item.FullPath),
        "This Week" => item.Modified >= DateTime.Now.AddDays(-7),
        "Large Files" => item.Size >= 100L * 1024 * 1024,
        "Vault" => item.IsVault,
        _ => item.Category == category
    };

    private int SearchScore(string query, DesktopItem item)
    {
        var score = Math.Max(FuzzySearch.Score(query, item.Name) * 4,
            Math.Max(FuzzySearch.Score(query, item.Type), FuzzySearch.Score(query, item.Source)));
        if (_settings.Favorites.Contains(item.FullPath, StringComparer.Ordinal)) score += 300;
        if (_settings.Recent.TryGetValue(item.FullPath, out var opened)) score += Math.Max(0, 200 - (int)(DateTime.Now - opened).TotalDays);
        return score;
    }

    private void Expand()
    {
        Width = ExpandedWidth; Height = ExpandedHeight; Panel.IsVisible = true; _expanded = true;
        ConstrainPosition(); Activate(); SearchBox.Focus();
    }

    public void ExpandForTest() => Expand();

    private void Collapse()
    {
        Panel.IsVisible = false; Width = CollapsedSize; Height = CollapsedSize; _expanded = false; SavePosition();
    }

    private void ToggleExpanded() { if (_expanded) Collapse(); else Expand(); }

    private async void OpenSettings()
    {
        var settings = new SettingsWindow(_settings);
        if (await settings.ShowDialog<bool>(this))
        {
            SettingsStore.Save(_settings);
            StartupService.Set(_settings.StartWithDesktop);
            RefreshIndex();
        }
    }

    private void OpenSelected()
    {
        if (ItemsGrid.SelectedItem is not DesktopItem item) return;
        if (item.IsVault) { ExportVault(item); return; }
        if (!File.Exists(item.FullPath) && !Directory.Exists(item.FullPath)) return;
        _settings.Recent[item.FullPath] = DateTime.Now;
        foreach (var old in _settings.Recent.OrderByDescending(pair => pair.Value).Skip(40).Select(pair => pair.Key).ToList()) _settings.Recent.Remove(old);
        SettingsStore.Save(_settings);
        DesktopIndex.Open(item.FullPath);
    }

    private async void ImportVault()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Encrypt a copy in HexaDock", AllowMultiple = false });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path is null) return;
        var item = await Task.Run(() => VaultService.Import(path));
        _settings.Vault.Add(item); SettingsStore.Save(_settings); RefreshIndex();
    }

    private async void ExportVault(DesktopItem item)
    {
        var vault = _settings.Vault.FirstOrDefault(value => value.Id == item.VaultId);
        if (vault is null) return;
        var destination = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Export decrypted copy", SuggestedFileName = vault.Name });
        var path = destination?.TryGetLocalPath();
        if (path is not null) await Task.Run(() => VaultService.Export(vault, path));
    }

    private void DeleteVault()
    {
        if (ItemsGrid.SelectedItem is not DesktopItem { IsVault: true } item) return;
        var vault = _settings.Vault.FirstOrDefault(value => value.Id == item.VaultId);
        if (vault is null) return;
        VaultService.Delete(vault); _settings.Vault.Remove(vault); SettingsStore.Save(_settings); RefreshIndex();
    }

    private void RestorePosition()
    {
        if (_settings.Left >= 0 && _settings.Top >= 0) Position = new PixelPoint(_settings.Left, _settings.Top);
        else ResetPosition();
        ConstrainPosition();
    }

    private void SavePosition() { _settings.Left = Position.X; _settings.Top = Position.Y; }

    private void ConstrainPosition()
    {
        var screen = Screens.ScreenFromPoint(Position) ?? Screens.Primary;
        if (screen is null) return;
        var area = screen.WorkingArea;
        var width = (int)Math.Ceiling(Width); var height = (int)Math.Ceiling(Height);
        Position = new PixelPoint(Math.Clamp(Position.X, area.X, Math.Max(area.X, area.Right - width)), Math.Clamp(Position.Y, area.Y, Math.Max(area.Y, area.Bottom - height)));
    }

    private void ResetPosition()
    {
        var area = Screens.Primary?.WorkingArea ?? new PixelRect(0, 0, 1280, 720);
        Position = new PixelPoint(area.Right - CollapsedSize - 20, area.Bottom - CollapsedSize - 20);
        SavePosition();
    }

    private void Logo_DragStarted(object? sender, VectorEventArgs e) => _dragDistance = 0;

    private void Logo_PointerPressed(object? sender, PointerPressedEventArgs e) => _dragDistance = 0;

    private void Logo_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragDistance < 4) ToggleExpanded();
    }

    private void Logo_DragDelta(object? sender, VectorEventArgs e)
    {
        _dragDistance += Math.Abs(e.Vector.X) + Math.Abs(e.Vector.Y);
        Position = new PixelPoint(Position.X + (int)Math.Round(e.Vector.X), Position.Y + (int)Math.Round(e.Vector.Y));
    }

    private void Logo_DragCompleted(object? sender, VectorEventArgs e)
    {
        ConstrainPosition();
        SavePosition();
    }

    private void Filter_Changed(object? sender, SelectionChangedEventArgs e) => ApplyFilter();
    private void Filter_Changed(object? sender, TextChangedEventArgs e) => ApplyFilter();
    private void Sort_Changed(object? sender, SelectionChangedEventArgs e) => ApplyFilter();
    private void Direction_Click(object? sender, RoutedEventArgs e) { _descending = !_descending; DirectionButton.Content = _descending ? "Z → A" : "A → Z"; ApplyFilter(); }
    private void Refresh_Click(object? sender, RoutedEventArgs e) => RefreshIndex();
    private void Collapse_Click(object? sender, RoutedEventArgs e) => Collapse();
    private void OpenMenu_Click(object? sender, RoutedEventArgs e) => Expand();
    private void Settings_Click(object? sender, RoutedEventArgs e) => OpenSettings();
    private void VaultImport_Click(object? sender, RoutedEventArgs e) => ImportVault();
    private void Open_Click(object? sender, RoutedEventArgs e) => OpenSelected();
    private void ItemsGrid_DoubleTapped(object? sender, TappedEventArgs e) => OpenSelected();
    private void Favorite_Click(object? sender, RoutedEventArgs e)
    {
        if (ItemsGrid.SelectedItem is not DesktopItem item) return;
        if (!_settings.Favorites.Remove(item.FullPath)) _settings.Favorites.Add(item.FullPath);
        SettingsStore.Save(_settings); ApplyFilter();
    }
    private void VaultExport_Click(object? sender, RoutedEventArgs e) { if (ItemsGrid.SelectedItem is DesktopItem { IsVault: true } item) ExportVault(item); }
    private void VaultDelete_Click(object? sender, RoutedEventArgs e) => DeleteVault();
    private void Reveal_Click(object? sender, RoutedEventArgs e) { if (ItemsGrid.SelectedItem is DesktopItem { IsVault: false } item) DesktopIndex.Reveal(item.FullPath); }
    private void ResetPosition_Click(object? sender, RoutedEventArgs e) => ResetPosition();
    private void Exit_Click(object? sender, RoutedEventArgs e) => Close();
}
