using System.Windows;
using HexaDock.Models;
using HexaDock.Services;

namespace HexaDock;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly List<string> _sources;

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        _sources = [.. settings.Sources];
        SourcesList.ItemsSource = _sources;
        StartupToggle.IsChecked = StartupService.IsEnabled();
        HideIconsToggle.IsChecked = settings.HideDesktopIcons;
        PinToggle.IsChecked = settings.Pin is not null;
        UpdatePinFields();
    }

    private void PinToggle_Changed(object sender, RoutedEventArgs e) => UpdatePinFields();

    private void UpdatePinFields()
    {
        if (PinFields is null) return;
        PinFields.IsEnabled = PinToggle.IsChecked == true;
        PinFields.Opacity = PinFields.IsEnabled ? 1 : 0.45;
        PinStateText.Text = _settings.Pin is null ? "" : "A PIN is active. Leave the fields blank to keep it.";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        PinSettings? nextPin = _settings.Pin;
        if (PinToggle.IsChecked != true)
        {
            nextPin = null;
        }
        else
        {
            var changingPin = PinBox.Password.Length > 0 || ConfirmPinBox.Password.Length > 0 ||
                              QuestionBox.Text.Trim().Length > 0 || AnswerBox.Password.Length > 0;
            if (_settings.Pin is null || changingPin)
            {
                var pin = PinBox.Password.Trim();
                if (pin.Length is < 4 or > 8 || !pin.All(char.IsDigit))
                {
                    ShowProblem("Use a 4–8 digit PIN.");
                    return;
                }
                if (pin != ConfirmPinBox.Password.Trim())
                {
                    ShowProblem("The PINs do not match.");
                    return;
                }
                if (QuestionBox.Text.Trim().Length < 4 || AnswerBox.Password.Trim().Length < 2)
                {
                    ShowProblem("Add a recovery question and answer you will remember.");
                    return;
                }
                nextPin = PinService.Create(pin, QuestionBox.Text, AnswerBox.Password);
            }
        }

        try
        {
            StartupService.SetEnabled(StartupToggle.IsChecked == true);
            _settings.HideDesktopIcons = HideIconsToggle.IsChecked == true;
            _settings.Sources = [.. _sources];
            _settings.Pin = nextPin;
            DialogResult = true;
        }
        catch (Exception exception)
        {
            ShowProblem($"The settings could not be saved: {exception.Message}");
        }
    }

    private void ShowProblem(string message) => MessageBox.Show(this, message, "HexaDock", MessageBoxButton.OK, MessageBoxImage.Information);
    private void AddSource_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Microsoft.Win32.OpenFolderDialog { Title = "Add a folder to HexaDock", Multiselect = false };
        if (picker.ShowDialog(this) != true || _sources.Contains(picker.FolderName, StringComparer.OrdinalIgnoreCase)) return;
        _sources.Add(picker.FolderName);
        SourcesList.Items.Refresh();
    }
    private void RemoveSource_Click(object sender, RoutedEventArgs e)
    {
        if (SourcesList.SelectedItem is not string selected) return;
        _sources.Remove(selected);
        SourcesList.Items.Refresh();
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
