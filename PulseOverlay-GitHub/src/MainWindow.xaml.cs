using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using PulseOverlay.Models;
using PulseOverlay.Services;
using Forms = System.Windows.Forms;

namespace PulseOverlay;

public partial class MainWindow : Window
{
    private readonly AppSettings _settings;
    private bool _allowClose;

    public MainWindow(AppSettings settings)
    {
        InitializeComponent(); _settings = settings; DataContext = settings;
        settings.PropertyChanged += SettingsChanged;
        RefreshColor();
    }

    public void SetSensorStatus(string status)
    {
        SensorText.Text = status;
        StatusText.Text = status.StartsWith("No ") || status.Contains("could not") ? "Sensor attention" : "Sensors online";
    }

    private void SettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppSettings.Color)) RefreshColor();
        ((App)System.Windows.Application.Current).ApplySettings();
    }

    private void RefreshColor()
    {
        try { ColorPreview.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_settings.Color)); } catch { }
    }

    private void ChooseColor_Click(object sender, RoutedEventArgs e)
    {
        using var picker = new Forms.ColorDialog { FullOpen = true };
        if (picker.ShowDialog() == Forms.DialogResult.OK)
            _settings.Color = $"#{picker.Color.R:X2}{picker.Color.G:X2}{picker.Color.B:X2}";
    }

    private void Save_Click(object sender, RoutedEventArgs e) { SettingsService.Save(_settings); StatusText.Text = "Changes saved"; }
    private void Hide_Click(object sender, RoutedEventArgs e) => Hide();
    protected override void OnClosing(CancelEventArgs e) { if (!_allowClose) { e.Cancel = true; Hide(); } base.OnClosing(e); }
    public void Exit() { _allowClose = true; Close(); }
}
