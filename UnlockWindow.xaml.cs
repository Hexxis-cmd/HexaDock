using System.Windows;
using System.Windows.Input;
using HexaDock.Models;
using HexaDock.Services;

namespace HexaDock;

public partial class UnlockWindow : Window
{
    private readonly AppSettings _settings;

    public UnlockWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        QuestionText.Text = settings.Pin?.Question ?? "";
        Loaded += (_, _) => PinBox.Focus();
    }

    private void Action_Click(object sender, RoutedEventArgs e)
    {
        if (_settings.Pin is null)
        {
            DialogResult = true;
            return;
        }

        if (RecoveryPanel.Visibility == Visibility.Visible)
        {
            if (!PinService.VerifyAnswer(_settings.Pin, AnswerBox.Password))
            {
                ShowProblem("That recovery answer does not match.");
                return;
            }
            _settings.Pin = null;
            SettingsStore.Save(_settings);
            MessageBox.Show(this, "PIN protection has been removed. You can create a new PIN in Settings.", "HexaDock", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            return;
        }

        if (!PinService.VerifyPin(_settings.Pin, PinBox.Password))
        {
            ShowProblem("Incorrect PIN.");
            PinBox.Clear();
            PinBox.Focus();
            return;
        }
        DialogResult = true;
    }

    private void Recovery_Click(object sender, RoutedEventArgs e)
    {
        PinPanel.Visibility = Visibility.Collapsed;
        RecoveryPanel.Visibility = Visibility.Visible;
        ActionButton.Content = "RECOVER";
        AnswerBox.Focus();
    }

    private void BackToPin_Click(object sender, RoutedEventArgs e)
    {
        RecoveryPanel.Visibility = Visibility.Collapsed;
        PinPanel.Visibility = Visibility.Visible;
        ActionButton.Content = "UNLOCK";
        PinBox.Focus();
    }

    private void PinBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) Action_Click(sender, e); }
    private void ShowProblem(string message) => MessageBox.Show(this, message, "HexaDock", MessageBoxButton.OK, MessageBoxImage.Information);
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
