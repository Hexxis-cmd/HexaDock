using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using HexaDock.Linux.Models;

namespace HexaDock.Linux;

public sealed partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly ObservableCollection<string> _sources;

    public SettingsWindow() : this(new AppSettings()) { }

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        _sources = new ObservableCollection<string>(settings.Sources);
        SourcesList.ItemsSource = _sources;
        StartupToggle.IsChecked = settings.StartWithDesktop;
    }

    private async void AddSource_Click(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Choose a folder to index", AllowMultiple = false });
        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (path is not null && !_sources.Contains(path, StringComparer.Ordinal)) _sources.Add(path);
    }

    private void RemoveSource_Click(object? sender, RoutedEventArgs e)
    {
        if (SourcesList.SelectedItem is string selected) _sources.Remove(selected);
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        _settings.StartWithDesktop = StartupToggle.IsChecked == true;
        _settings.Sources = [.. _sources];
        Close(true);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);
}
