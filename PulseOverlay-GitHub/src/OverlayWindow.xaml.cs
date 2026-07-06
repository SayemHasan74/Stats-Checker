using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Threading;
using PulseOverlay.Models;

namespace PulseOverlay;

public partial class OverlayWindow : Window
{
    private AppSettings _settings = new();
    private MetricSnapshot _snapshot = new();
    private nint _handle;
    private readonly DispatcherTimer _topmostTimer;

    public OverlayWindow()
    {
        InitializeComponent();
        SourceInitialized += MakeOverlayWindow;
        _topmostTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _topmostTimer.Tick += (_, _) => ForceTopMost();
        _topmostTimer.Start();
    }
    public void Apply(AppSettings settings) { _settings = settings; Render(); }
    public void Update(MetricSnapshot snapshot) { _snapshot = snapshot; Render(); }

    private void Render()
    {
        if (!IsLoaded) return;
        Metrics.Children.Clear();
        Add(_settings.ShowFps, "FPS", Number(_snapshot.Fps, "0"), 3.4);
        Add(_settings.ShowCpuUsage, "CPU", Percent(_snapshot.CpuUsage), 4);
        Add(_settings.ShowCpuTemp, "CPU TEMP", Temp(_snapshot.CpuTemperature), 5);
        Add(_settings.ShowGpuUsage, "GPU", Percent(_snapshot.GpuUsage), 4);
        Add(_settings.ShowGpuTemp, "GPU TEMP", Temp(_snapshot.GpuTemperature), 5);
        Add(_settings.ShowRam, "RAM", Pair(_snapshot.RamUsedGb, _snapshot.RamTotalGb, "GB"), 12);
        Add(_settings.ShowVram, "VRAM", Pair(_snapshot.VramUsedGb, _snapshot.VramTotalGb, "GB"), 11);
        Add(_settings.ShowDisk, "DISK", _snapshot.DiskActivity is null ? "N/A" : $"{_snapshot.DiskActivity:0}% {_snapshot.DiskMbps:0.0}MB/s", 11);
        Add(_settings.ShowNetwork, "NET", Network(_snapshot.NetworkDownloadMbps, _snapshot.NetworkUploadMbps), 13);
        Add(_settings.ShowCpuClock, "CPU CLK", Number(_snapshot.CpuClockMhz, "0", "MHz"), 7);
        Add(_settings.ShowGpuClock, "GPU CLK", Number(_snapshot.GpuClockMhz, "0", "MHz"), 7);
        if (_settings.ShowPower) Add(true, "PWR", $"C {Number(_snapshot.CpuPowerWatts, "0", "W")} G {Number(_snapshot.GpuPowerWatts, "0", "W")}", 12);
        Add(_settings.ShowClock, "TIME", DateTime.Now.ToString("h:mm"), 5);
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
        ForceTopMost();
    }

    private void Add(bool show, string label, string value, double valueChars, double fontSizeMultiplier = 1.0)
    {
        if (!show) return;
        if (Metrics.Children.Count > 0)
        {
            Metrics.Children.Add(new TextBlock
            {
                Text = "|",
                Margin = new Thickness(_settings.FontSize * .28, 0, _settings.FontSize * .28, 0),
                Opacity = .38,
                FontSize = _settings.FontSize,
                TextAlignment = TextAlignment.Center
            });
        }

        var item = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        item.Children.Add(new TextBlock
        {
            Text = label + " ",
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI Semibold"),
            FontSize = _settings.FontSize * fontSizeMultiplier,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(160, 180, 190, 205))
        });
        item.Children.Add(new TextBlock
        {
            Text = value,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize = _settings.FontSize * fontSizeMultiplier,
            Width = _settings.FontSize * valueChars * .56,
            TextAlignment = TextAlignment.Left,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.None
        });
        Metrics.Children.Add(item);
    }

    private void Position()
    {
        UpdateLayout(); var area = SystemParameters.VirtualScreenWidth > 0 ? GetVirtualScreen() : SystemParameters.WorkArea; const double gap = 0;
        Left = _settings.Position.Contains("Right") ? area.Right - ActualWidth - gap : area.Left + gap;
        Top = _settings.Position.StartsWith("Bottom") ? area.Bottom - ActualHeight - gap : area.Top + gap;
    }

    private static string Number(double? v, string format, string suffix = "") => v is null ? "N/A" : v.Value.ToString(format) + suffix;
    private static string Percent(double? v) => Number(v, "0", "%");
    private static string Temp(double? v) => Number(v, "0", "°C");
    private static string Pair(double? used, double? total, string suffix) => used is null || total is null ? "N/A" : $"{used:0.0}/{total:0.0} {suffix}";
    private static string Network(double? down, double? up) =>
        down is null || up is null ? "N/A" : $"↓{down:0.0} ↑{up:0.0}Mbps";
    private static Rect GetVirtualScreen() => new(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);

    private void MakeOverlayWindow(object? sender, EventArgs e)
    {
        _handle = new WindowInteropHelper(this).Handle;
        int style = GetWindowLong(_handle, -20);
        SetWindowLong(_handle, -20, style | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_LAYERED);
        ForceTopMost();
    }

    private void ForceTopMost()
    {
        if (_handle == 0 || Visibility != Visibility.Visible) return;
        SetWindowPos(_handle, HWND_TOPMOST, (int)Math.Round(Left), (int)Math.Round(Top), 0, 0,
            SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }

    private static readonly nint HWND_TOPMOST = new(-1);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const int WS_EX_TRANSPARENT = 0x20;
    private const int WS_EX_TOOLWINDOW = 0x80;
    private const int WS_EX_LAYERED = 0x80000;
    private const int WS_EX_NOACTIVATE = 0x8000000;

    [DllImport("user32.dll")] private static extern int GetWindowLong(nint hWnd, int index);
    [DllImport("user32.dll")] private static extern int SetWindowLong(nint hWnd, int index, int value);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
}
