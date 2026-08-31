using System.Diagnostics;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using HexaDock.Models;
using HexaDock.Services;

namespace HexaDock;

public partial class MainWindow : Window
{
    private const double CollapsedSize = 70;
    private const double ExpandedWidth = 930;
    private const double ExpandedHeight = 620;
    private readonly string _desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    private readonly ObservableCollection<DesktopItem> _items = [];
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly DispatcherTimer _refreshDelay;
    private readonly DesktopIconService _desktopIcons = new();
    private readonly HotkeyService _hotkey;
    private readonly AppSettings _settings;
    private ICollectionView _view = null!;
    private double _dragTotal;
    private double _dragLeft;
    private double _dragTop;
    private bool _expanded;
    private bool _indexing;
    private bool _refreshPending;
    private bool _descending;
    private bool _sessionUnlocked;

    public MainWindow()
    {
        InitializeComponent();
        _settings = SettingsStore.Load();
        _hotkey = new HotkeyService(this, ToggleExpanded);
        _sessionUnlocked = _settings.Pin is null;
        SearchBox.Text = "Search Desktop…";
        SearchBox.GotFocus += (_, _) => { if (SearchBox.Text == "Search Desktop…") SearchBox.Clear(); };
        SearchBox.LostFocus += (_, _) => { if (string.IsNullOrWhiteSpace(SearchBox.Text)) SearchBox.Text = "Search Desktop…"; };
        CategoryList.ItemsSource = new[] { "All", "Favorites", "Recent", "This Week", "Large Files", "Installers", "Vault", "Folders", "Projects", "Apps", "Games", "Photos", "Music", "Videos", "Documents", "Archives", "Other" };
        CategoryList.SelectedIndex = 0;
        SortBox.ItemsSource = new[] { "Relevance", "Name", "Type", "Date modified" };
        SortBox.SelectedIndex = 0;
        _view = CollectionViewSource.GetDefaultView(_items);
        _view.Filter = MatchesFilter;
        ItemsGrid.ItemsSource = _view;
        _refreshDelay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        _refreshDelay.Tick += (_, _) => { _refreshDelay.Stop(); RefreshIndex(); };
        Loaded += Window_Loaded;
        Closing += Window_Closing;
        SystemParameters.StaticPropertyChanged += SystemParameters_Changed;
        PreviewKeyDown += Window_PreviewKeyDown;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        RestorePosition();
        RefreshIndex();
        StartWatching();
        if (_settings.HideDesktopIcons) _desktopIcons.Apply(true);
    }

    private async void RefreshIndex()
    {
        if (_indexing) { _refreshPending = true; return; }
        _indexing = true;
        StatusText.Text = "Indexing…";
        var selectedPath = (ItemsGrid.SelectedItem as DesktopItem)?.FullPath;
        List<DesktopItem> scanned;
        try
        {
            var sources = _settings.Sources.ToArray();
            scanned = await Task.Run(() => DesktopIndex.Scan(_desktop, sources).ToList());
        }
        catch
        {
            StatusText.Text = "Index unavailable  •  select refresh to retry";
            _indexing = false;
            return;
        }
        var vaultIcon = Environment.ProcessPath is null ? null : IconService.Load(Environment.ProcessPath, false);
        scanned.AddRange(_settings.Vault.Where(item => File.Exists(VaultService.ItemPath(item.Id))).Select(item => new DesktopItem
        {
            Name = item.Name,
            Category = "Vault",
            Type = Path.GetExtension(item.Name).TrimStart('.').ToUpperInvariant() is { Length: > 0 } type ? type : "File",
            Modified = item.Added,
            FullPath = VaultService.ItemPath(item.Id),
            Source = "Encrypted Vault",
            Size = item.Size,
            Icon = vaultIcon,
            IsVault = true,
            VaultId = item.Id
        }));
        _items.Clear();
        foreach (var item in scanned) _items.Add(item);
        ApplySort();
        if (selectedPath is not null)
            ItemsGrid.SelectedItem = _items.FirstOrDefault(item => item.FullPath.Equals(selectedPath, StringComparison.OrdinalIgnoreCase));
        UpdateStatus();
        _indexing = false;
        if (_refreshPending) { _refreshPending = false; RefreshIndex(); }
    }

    private bool MatchesFilter(object value)
    {
        if (value is not DesktopItem item) return false;
        var category = CategoryList.SelectedItem as string ?? "All";
        if (!MatchesCategory(category, item)) return false;
        var query = SearchBox.Text.Trim();
        if (query.Length == 0 || query == "Search Desktop…") return true;
        return SearchScore(query, item) > 0;
    }

    private bool MatchesCategory(string category, DesktopItem item) => category switch
    {
        "All" => true,
        "Favorites" => _settings.Favorites.Contains(item.FullPath, StringComparer.OrdinalIgnoreCase),
        "Recent" => _settings.Recent.ContainsKey(item.FullPath),
        "This Week" => item.Modified >= DateTime.Now.AddDays(-7),
        "Large Files" => item.Size >= 100L * 1024 * 1024,
        "Installers" => new[] { ".exe", ".msi", ".msix", ".appx", ".appxbundle" }.Contains(Path.GetExtension(item.Name), StringComparer.OrdinalIgnoreCase),
        "Vault" => item.IsVault,
        _ => item.Category == category
    };

    private int SearchScore(string query, DesktopItem item)
    {
        var score = Math.Max(FuzzySearch.Score(query, item.Name) * 4,
                    Math.Max(FuzzySearch.Score(query, item.Type), FuzzySearch.Score(query, item.Source)));
        if (_settings.Favorites.Contains(item.FullPath, StringComparer.OrdinalIgnoreCase)) score += 300;
        if (_settings.Recent.TryGetValue(item.FullPath, out var opened)) score += Math.Max(0, 200 - (int)(DateTime.Now - opened).TotalDays);
        return score;
    }

    private void ApplySort()
    {
        if (_view is null) return;
        if (_view is ListCollectionView list) list.CustomSort = null;
        _view.SortDescriptions.Clear();
        var query = SearchBox.Text.Trim();
        if ((SortBox.SelectedItem as string) == "Relevance" || (query.Length > 0 && query != "Search Desktop…"))
        {
            if (_view is ListCollectionView ranked)
                ranked.CustomSort = Comparer<object>.Create((left, right) => CompareRelevance(left as DesktopItem, right as DesktopItem, query));
            _view.Refresh();
            UpdateStatus();
            return;
        }
        var property = (SortBox.SelectedItem as string) switch
        {
            "Type" => nameof(DesktopItem.Type),
            "Date modified" => nameof(DesktopItem.Modified),
            _ => nameof(DesktopItem.Name)
        };
        _view.SortDescriptions.Add(new SortDescription(property, _descending ? ListSortDirection.Descending : ListSortDirection.Ascending));
        _view.Refresh();
        UpdateStatus();
    }

    private int CompareRelevance(DesktopItem? left, DesktopItem? right, string query)
    {
        if (left is null || right is null) return 0;
        var leftScore = query.Length == 0 || query == "Search Desktop…" ? 0 : SearchScore(query, left);
        var rightScore = query.Length == 0 || query == "Search Desktop…" ? 0 : SearchScore(query, right);
        if (_settings.Favorites.Contains(left.FullPath, StringComparer.OrdinalIgnoreCase)) leftScore += 300;
        if (_settings.Favorites.Contains(right.FullPath, StringComparer.OrdinalIgnoreCase)) rightScore += 300;
        var result = rightScore.CompareTo(leftScore);
        return result != 0 ? result : string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateStatus()
    {
        if (StatusText is not null) StatusText.Text = $"{_view.Cast<object>().Count()} shown  •  {_items.Count} indexed";
    }

    private bool UnlockIfNeeded()
    {
        if (_sessionUnlocked || _settings.Pin is null) return true;
        var unlock = new UnlockWindow(_settings) { Owner = this };
        if (unlock.ShowDialog() != true) return false;
        _sessionUnlocked = true;
        return true;
    }

    private void Expand()
    {
        if (!UnlockIfNeeded()) return;
        Width = ExpandedWidth;
        Height = ExpandedHeight;
        ConstrainPosition();
        Panel.Visibility = Visibility.Visible;
        _expanded = true;
        Activate();
        SearchBox.Focus();
    }

    private void Collapse()
    {
        Panel.Visibility = Visibility.Collapsed;
        Width = CollapsedSize;
        Height = CollapsedSize;
        _expanded = false;
        _sessionUnlocked = _settings.Pin is null;
        SavePosition();
    }

    private void ToggleExpanded()
    {
        if (_expanded) Collapse(); else Expand();
    }

    private void OpenSettings()
    {
        if (!UnlockIfNeeded()) return;
        var settings = new SettingsWindow(_settings) { Owner = this };
        if (settings.ShowDialog() != true) return;
        SettingsStore.Save(_settings);
        if (_settings.HideDesktopIcons) _desktopIcons.Apply(true); else _desktopIcons.Apply(false);
        foreach (var watcher in _watchers) watcher.Dispose();
        _watchers.Clear();
        StartWatching();
        RefreshIndex();
        _sessionUnlocked = _settings.Pin is null || _sessionUnlocked;
    }

    private void StartWatching()
    {
        AddWatcher(_desktop);
        foreach (var source in _settings.Sources) AddWatcher(source);
    }

    private void AddWatcher(string path)
    {
        if (!Directory.Exists(path)) return;
        var watcher = new FileSystemWatcher(path)
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
        };
        FileSystemEventHandler changed = (_, _) => Dispatcher.Invoke(RestartRefreshDelay);
        RenamedEventHandler renamed = (_, _) => Dispatcher.Invoke(RestartRefreshDelay);
        watcher.Created += changed;
        watcher.Deleted += changed;
        watcher.Changed += changed;
        watcher.Renamed += renamed;
        watcher.EnableRaisingEvents = true;
        _watchers.Add(watcher);
    }

    private void RestartRefreshDelay()
    {
        _refreshDelay.Stop();
        _refreshDelay.Start();
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

    private void RestorePosition()
    {
        var current = MonitorService.At(double.IsFinite(_settings.Left) ? _settings.Left : 0, double.IsFinite(_settings.Top) ? _settings.Top : 0);
        var saved = _settings.MonitorPositions.GetValueOrDefault(_settings.LastMonitor);
        Left = saved?.Left ?? current.Right - 100;
        Top = saved?.Top ?? current.Top + 100;
        ConstrainPosition();
    }

    private void SavePosition()
    {
        ConstrainPosition();
        _settings.Left = Left;
        _settings.Top = Top;
        var monitor = MonitorService.At(Left + Width / 2, Top + Height / 2);
        _settings.LastMonitor = monitor.Name;
        _settings.MonitorPositions[monitor.Name] = new WindowPosition { Left = Left, Top = Top };
        try { SettingsStore.Save(_settings); } catch { }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        SystemParameters.StaticPropertyChanged -= SystemParameters_Changed;
        SavePosition();
        _desktopIcons.Restore();
        _hotkey.Dispose();
        foreach (var watcher in _watchers) watcher.Dispose();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _expanded) { Collapse(); e.Handled = true; }
        else if (e.Key == Key.Down && SearchBox.IsKeyboardFocusWithin && ItemsGrid.Items.Count > 0)
        {
            ItemsGrid.SelectedIndex = 0;
            ItemsGrid.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.F && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
        }
    }

    private void ToggleFavorite()
    {
        if (ItemsGrid.SelectedItem is not DesktopItem item || item.IsVault) return;
        var existing = _settings.Favorites.FirstOrDefault(path => path.Equals(item.FullPath, StringComparison.OrdinalIgnoreCase));
        if (existing is null) _settings.Favorites.Add(item.FullPath); else _settings.Favorites.Remove(existing);
        SettingsStore.Save(_settings);
        ApplySort();
    }

    private void ImportVault()
    {
        if (_settings.Pin is null)
        {
            MessageBox.Show(this, "Create a PIN in Settings before using the encrypted vault.", "HexaDock", MessageBoxButton.OK, MessageBoxImage.Information);
            OpenSettings();
            return;
        }
        var picker = new Microsoft.Win32.OpenFileDialog { Title = "Import encrypted copies", Multiselect = true };
        if (picker.ShowDialog(this) != true) return;
        try
        {
            foreach (var file in picker.FileNames) _settings.Vault.Add(VaultService.Import(file));
            SettingsStore.Save(_settings);
            RefreshIndex();
            CategoryList.SelectedItem = "Vault";
            MessageBox.Show(this, "Encrypted copies were added. The original files were not changed or deleted.", "HexaDock", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, $"The vault import failed safely: {exception.Message}", "HexaDock", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportVault(DesktopItem item)
    {
        var vaultItem = _settings.Vault.FirstOrDefault(value => value.Id == item.VaultId);
        if (vaultItem is null) return;
        var picker = new Microsoft.Win32.SaveFileDialog { Title = "Export decrypted copy", FileName = vaultItem.Name, OverwritePrompt = true };
        if (picker.ShowDialog(this) != true) return;
        try
        {
            VaultService.Export(vaultItem, picker.FileName);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, $"The export failed: {exception.Message}", "HexaDock", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DeleteVault(DesktopItem item)
    {
        var vaultItem = _settings.Vault.FirstOrDefault(value => value.Id == item.VaultId);
        if (vaultItem is null) return;
        if (MessageBox.Show(this, $"Remove the encrypted copy of {vaultItem.Name}? The original file is not affected.", "HexaDock", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        VaultService.Delete(vaultItem);
        _settings.Vault.Remove(vaultItem);
        SettingsStore.Save(_settings);
        RefreshIndex();
    }

    private void Hex_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
    {
        _dragTotal = 0;
        _dragLeft = Left;
        _dragTop = Top;
    }
    private void Hex_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        _dragLeft += e.HorizontalChange;
        _dragTop += e.VerticalChange;
        var constrained = MonitorService.Constrain(_dragLeft, _dragTop, Width, Height);
        Left = constrained.Left;
        Top = constrained.Top;
        _dragTotal += Math.Abs(e.HorizontalChange) + Math.Abs(e.VerticalChange);
    }

    private void ConstrainPosition()
    {
        var constrained = MonitorService.Constrain(Left, Top, Width, Height);
        Left = constrained.Left;
        Top = constrained.Top;
    }

    private void ResetPosition()
    {
        var primary = MonitorService.At(0, 0);
        Left = primary.Right - Width - 30;
        Top = primary.Top + 80;
        ConstrainPosition();
        SavePosition();
    }

    private void SystemParameters_Changed(object? sender, PropertyChangedEventArgs e) => Dispatcher.BeginInvoke(ConstrainPosition);
    private void Hex_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        if (_dragTotal < 4) ToggleExpanded(); else SavePosition();
    }

    private void Filter_Changed(object sender, RoutedEventArgs e) { if (_view is not null) ApplySort(); }
    private void Sort_Changed(object sender, SelectionChangedEventArgs e) => ApplySort();
    private void Direction_Click(object sender, RoutedEventArgs e) { _descending = !_descending; DirectionButton.Content = _descending ? "Z → A" : "A → Z"; ApplySort(); }
    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshIndex();
    private void ResetPosition_Click(object sender, RoutedEventArgs e) => ResetPosition();
    private void Collapse_Click(object sender, RoutedEventArgs e) => Collapse();
    private void OpenMenu_Click(object sender, RoutedEventArgs e) { if (!_expanded) Expand(); }
    private void Settings_Click(object sender, RoutedEventArgs e) => OpenSettings();
    private void RecycleBin_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("explorer.exe", "shell:RecycleBinFolder") { UseShellExecute = true });
    private void VaultImport_Click(object sender, RoutedEventArgs e) => ImportVault();
    private void VaultExport_Click(object sender, RoutedEventArgs e) { if (ItemsGrid.SelectedItem is DesktopItem { IsVault: true } item) ExportVault(item); }
    private void VaultDelete_Click(object sender, RoutedEventArgs e) { if (ItemsGrid.SelectedItem is DesktopItem { IsVault: true } item) DeleteVault(item); }
    private void Favorite_Click(object sender, RoutedEventArgs e) => ToggleFavorite();
    private void Exit_Click(object sender, RoutedEventArgs e) => Close();
    private void ItemsGrid_DoubleClick(object sender, MouseButtonEventArgs e) => OpenSelected();
    private void ItemsGrid_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) OpenSelected(); }
    private void Open_Click(object sender, RoutedEventArgs e) => OpenSelected();
    private void Reveal_Click(object sender, RoutedEventArgs e) { if (ItemsGrid.SelectedItem is DesktopItem { IsVault: false } item) DesktopIndex.Reveal(item.FullPath); }
}
