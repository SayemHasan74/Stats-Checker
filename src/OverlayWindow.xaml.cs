using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Controls;
using PulseOverlay.Models;

namespace PulseOverlay;

public partial class OverlayWindow : Window
{
    private AppSettings _settings = new();
    private MetricSnapshot _snapshot = new();

    public OverlayWindow() { InitializeComponent(); SourceInitialized += MakeClickThrough; }
    public void Apply(AppSettings settings) { _settings = settings; Render(); }
    public void Update(MetricSnapshot snapshot) { _snapshot = snapshot; Render(); }

    private void Render()
    {
        if (!IsLoaded) return;
        Metrics.Children.Clear();
        Add(_settings.ShowFps, "FPS", Number(_snapshot.Fps, "0"));
        Add(_settings.ShowCpuUsage, "CPU", Percent(_snapshot.CpuUsage));
        Add(_settings.ShowCpuTemp, "CPU TEMP", Temp(_snapshot.CpuTemperature));
        Add(_settings.ShowGpuUsage, "GPU", Percent(_snapshot.GpuUsage));
        Add(_settings.ShowGpuTemp, "GPU TEMP", Temp(_snapshot.GpuTemperature));
        Add(_settings.ShowRam, "RAM", Pair(_snapshot.RamUsedGb, _snapshot.RamTotalGb, "GB"));
        Add(_settings.ShowVram, "VRAM", Pair(_snapshot.VramUsedGb, _snapshot.VramTotalGb, "GB"));
        Add(_settings.ShowDisk, "DISK", _snapshot.DiskActivity is null ? "N/A" : $"{_snapshot.DiskActivity:0}%  {_snapshot.DiskMbps:0.0} MB/s");
        Add(_settings.ShowNetwork, "NET", Network(_snapshot.NetworkDownloadMbps, _snapshot.NetworkUploadMbps));
        Add(_settings.ShowCpuClock, "CPU CLK", Number(_snapshot.CpuClockMhz, "0", " MHz"));
        Add(_settings.ShowGpuClock, "GPU CLK", Number(_snapshot.GpuClockMhz, "0", " MHz"));
        if (_settings.ShowPower) Add(true, "POWER", $"CPU {Number(_snapshot.CpuPowerWatts, "0", "W")}  GPU {Number(_snapshot.GpuPowerWatts, "0", "W")}");
        Add(_settings.ShowClock, "TIME", DateTime.Now.ToString("h:mm"));
        try
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_settings.Color);
            Foreground = new SolidColorBrush(color); Panel.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb((byte)(_settings.Opacity * 235), 8, 12, 21));
        }
        catch { Foreground = System.Windows.Media.Brushes.SpringGreen; }
        bool flush = _settings.EdgeSpacing == "Flush to corner";
        Panel.Padding = flush ? new Thickness(0) : new Thickness(12, 7, 12, 7);
        Panel.CornerRadius = flush ? new CornerRadius(0) : new CornerRadius(7);
        Position(); Visibility = _settings.ShowOverlay && Metrics.Children.Count > 0 ? Visibility.Visible : Visibility.Hidden;
    }

    private void Add(bool show, string label, string value)
    {
        if (!show) return;
        if (Metrics.Children.Count > 0) Metrics.Children.Add(new TextBlock { Text = "  |  ", Opacity = .38, FontSize = _settings.FontSize });
        var block = new TextBlock { FontFamily = new System.Windows.Media.FontFamily("Segoe UI Semibold"), FontSize = _settings.FontSize };
        block.Inlines.Add(new System.Windows.Documents.Run(label + " ") { Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(160, 180, 190, 205)) });
        block.Inlines.Add(new System.Windows.Documents.Run(value)); Metrics.Children.Add(block);
    }

    private void Position()
    {
        UpdateLayout(); var area = SystemParameters.WorkArea; const double gap = 0;
        Left = _settings.Position.Contains("Right") ? area.Right - ActualWidth - gap : area.Left + gap;
        Top = _settings.Position.StartsWith("Bottom") ? area.Bottom - ActualHeight - gap : area.Top + gap;
    }

    private static string Number(double? v, string format, string suffix = "") => v is null ? "N/A" : v.Value.ToString(format) + suffix;
    private static string Percent(double? v) => Number(v, "0", "%");
    private static string Temp(double? v) => Number(v, "0", "°C");
    private static string Pair(double? used, double? total, string suffix) => used is null || total is null ? "N/A" : $"{used:0.0}/{total:0.0} {suffix}";
    private static string Network(double? down, double? up) =>
        down is null || up is null ? "N/A" : $"DOWN {down:0.0}  UP {up:0.0} Mbps";
    private void MakeClickThrough(object? sender, EventArgs e)
    {
        nint handle = new WindowInteropHelper(this).Handle;
        int style = GetWindowLong(handle, -20); SetWindowLong(handle, -20, style | 0x20 | 0x80 | 0x8000000);
    }
    [DllImport("user32.dll")] private static extern int GetWindowLong(nint hWnd, int index);
    [DllImport("user32.dll")] private static extern int SetWindowLong(nint hWnd, int index, int value);
}
