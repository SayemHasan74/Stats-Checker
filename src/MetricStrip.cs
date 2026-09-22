using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PulseOverlay.Models;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using FontFamily = System.Windows.Media.FontFamily;

namespace PulseOverlay;

// Shared by the actual overlay and the settings preview. Controls survive sampling ticks.
public sealed class MetricStrip : Border
{
    private sealed record Definition(string Label, Func<AppSettings, bool> Enabled, Func<MetricSnapshot, string> Format, int Width);
    private static readonly Definition[] Definitions =
    [
        new("FPS", s => s.ShowFps, m => Number(m.Fps, "0"), 5),
        new("CPU", s => s.ShowCpuUsage, m => Number(m.CpuUsage, "0", "%"), 5),
        new("CPU TEMP", s => s.ShowCpuTemp, m => Number(m.CpuTemperature, "0", "°C"), 5),
        new("GPU", s => s.ShowGpuUsage, m => Number(m.GpuUsage, "0", "%"), 5),
        new("GPU TEMP", s => s.ShowGpuTemp, m => Number(m.GpuTemperature, "0", "°C"), 5),
        new("RAM", s => s.ShowRam, m => Pair(m.RamUsedGb, m.RamTotalGb), 14),
        new("VRAM", s => s.ShowVram, m => Pair(m.VramUsedGb, m.VramTotalGb), 14),
        new("DISK", s => s.ShowDisk, m => m.DiskActivity is null ? "N/A" : $"{m.DiskActivity:0}% {m.DiskMbps:0.0}MB/s", 16),
        new("NET", s => s.ShowNetwork, m => m.NetworkDownloadMbps is null || m.NetworkUploadMbps is null ? "N/A" : $"↓{m.NetworkDownloadMbps:0.0} ↑{m.NetworkUploadMbps:0.0}Mbps", 23),
        new("CPU CLK", s => s.ShowCpuClock, m => Number(m.CpuClockMhz, "0", "MHz"), 8),
        new("GPU CLK", s => s.ShowGpuClock, m => Number(m.GpuClockMhz, "0", "MHz"), 8),
        new("PWR", s => s.ShowPower, m => $"C {Number(m.CpuPowerWatts, "0", "W")} G {Number(m.GpuPowerWatts, "0", "W")}", 15),
        new("TIME", s => s.ShowClock, _ => DateTime.Now.ToString("HH:mm"), 5)
    ];
    private readonly StackPanel _panel = new() { Orientation = System.Windows.Controls.Orientation.Horizontal };
    private readonly List<(TextBlock Text, Definition Metric)> _values = [];
    private MetricSnapshot _snapshot = new();
    public bool HasMetrics => _values.Count > 0;

    public MetricStrip() { Child = _panel; SnapsToDevicePixels = true; }

    public void Apply(AppSettings settings)
    {
        _panel.Children.Clear(); _values.Clear();
        double size = Math.Clamp(settings.FontSize, 10, 32);
        Brush color;
        try { color = new SolidColorBrush((Color)ColorConverter.ConvertFromString(settings.Color)); color.Freeze(); }
        catch { color = Brushes.White; }
        Background = new SolidColorBrush(Color.FromArgb((byte)(Math.Clamp(settings.Opacity, 0, 1) * 255), 8, 9, 11));
        Padding = settings.EdgeSpacing == "Flush to corner" ? new Thickness(0) : new Thickness(10, 6, 10, 6);
        foreach (var metric in Definitions)
        {
            if (!metric.Enabled(settings)) continue;
            if (_values.Count > 0) _panel.Children.Add(new TextBlock { Text = "│", Foreground = Brushes.DimGray, FontSize = size, Margin = new Thickness(5, 0, 5, 0) });
            _panel.Children.Add(new TextBlock { Text = metric.Label + " ", FontFamily = new FontFamily("Consolas"), FontSize = size, Foreground = Brushes.DarkGray, VerticalAlignment = VerticalAlignment.Center });
            var value = new TextBlock { FontFamily = new FontFamily("Consolas"), FontSize = size, Foreground = color,
                Width = size * metric.Width * .61, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            _panel.Children.Add(value); _values.Add((value, metric));
        }
        Update(_snapshot);
        // Metric removal must invalidate this outer border immediately as well as
        // its child panel, so hosts do not reuse the previous strip width.
        InvalidateMeasure();
    }

    public void Update(MetricSnapshot snapshot)
    {
        _snapshot = snapshot;
        foreach (var (text, metric) in _values)
        {
            string value = metric.Format(snapshot);
            if (text.Text != value) text.Text = value;
        }
    }

    private static string Number(double? value, string format, string suffix = "") => value is null ? "N/A" : value.Value.ToString(format) + suffix;
    private static string Pair(double? used, double? total) => used is null || total is null ? "N/A" : $"{used:0.0}/{total:0.0}GB";
}

