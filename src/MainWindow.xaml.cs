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
    private MetricSnapshot _snapshot = new();

    public MainWindow(AppSettings settings)
    {
        InitializeComponent(); _settings = settings; DataContext = settings;
        settings.PropertyChanged += SettingsChanged;
        RefreshColor();
        RefreshPreview();
        IsVisibleChanged += (_, _) =>
        {
            ((App)System.Windows.Application.Current).UpdateMonitoring();
            if (IsVisible) { RefreshPreview(); SetSensorStatus(_snapshot.SensorStatus); }
        };
    }

    public void UpdateSnapshot(MetricSnapshot snapshot)
    {
        _snapshot = snapshot;
        if (!IsVisible) return;
        PreviewStrip.Update(snapshot);
        SetSensorStatus(snapshot.SensorStatus);
    }

    private void RefreshPreview()
    {
        PreviewStrip.Apply(_settings);
        PreviewStrip.Update(_snapshot);
        PreviewBox.HorizontalAlignment = _settings.Position.Contains("Right") ? System.Windows.HorizontalAlignment.Right : System.Windows.HorizontalAlignment.Left;
        PreviewBox.VerticalAlignment = _settings.Position.StartsWith("Bottom") ? VerticalAlignment.Bottom : VerticalAlignment.Top;
    }

    public void SetSensorStatus(string status)
    {
        if (SensorText.Text != status) SensorText.Text = status;
        string state = status.Contains("paused") ? "SENSORS PAUSED" : status.StartsWith("No ") || status.Contains("could not") ? "SENSOR ATTENTION" : "SENSORS ONLINE";
        if (StatusText.Text != state) StatusText.Text = state;
    }

    private void SettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        SaveButton.Visibility = Visibility.Visible;
        if (e.PropertyName == nameof(AppSettings.Color)) RefreshColor();
        if (IsVisible) RefreshPreview();
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

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        SettingsService.Save(_settings);
        SaveButton.Visibility = Visibility.Hidden;
    }
    private void Hide_Click(object sender, RoutedEventArgs e) => Hide();
    protected override void OnClosing(CancelEventArgs e) { if (!_allowClose) { e.Cancel = true; Hide(); } base.OnClosing(e); }
    public void Exit() { _allowClose = true; Close(); }
}
