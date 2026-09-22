using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using PulseOverlay.Models;

namespace PulseOverlay;

public partial class OverlayWindow : Window
{
    private AppSettings _settings = new();
    private nint _handle;
    private readonly DispatcherTimer _topmostTimer;

    public OverlayWindow()
    {
        InitializeComponent();
        SourceInitialized += MakeOverlayWindow;
        SizeChanged += (_, _) => Position();
        Loaded += (_, _) => Apply(_settings);
        _topmostTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _topmostTimer.Tick += (_, _) => ForceTopMost();
        IsVisibleChanged += (_, _) => _topmostTimer.IsEnabled = IsVisible;
        Closed += (_, _) => _topmostTimer.Stop();
    }

    public void Apply(AppSettings settings)
    {
        _settings = settings;
        Strip.Apply(settings);
        Fit.MaxWidth = SystemParameters.PrimaryScreenWidth;
        Visibility = settings.ShowOverlay && Strip.HasMetrics ? Visibility.Visible : Visibility.Hidden;
        if (IsLoaded) Position();
    }
    public void Update(MetricSnapshot snapshot) { if (IsVisible) Strip.Update(snapshot); }

    private void Position()
    {
        double gap = _settings.EdgeSpacing == "Flush to corner" ? 0 : 10;
        // WPF positions are device-independent; let WPF handle per-monitor DPI conversion.
        Left = _settings.Position.Contains("Right") ? SystemParameters.PrimaryScreenWidth - ActualWidth - gap : gap;
        Top = _settings.Position.StartsWith("Bottom") ? SystemParameters.PrimaryScreenHeight - ActualHeight - gap : gap;
    }

    private void MakeOverlayWindow(object? sender, EventArgs e)
    {
        _handle = new WindowInteropHelper(this).Handle;
        int style = GetWindowLong(_handle, -20);
        SetWindowLong(_handle, -20, style | 0x20 | 0x80 | 0x8000000 | 0x80000);
        ForceTopMost();
    }

    private void ForceTopMost()
    {
        if (_handle != 0 && IsVisible)
            SetWindowPos(_handle, new nint(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
    }

    [DllImport("user32.dll")] private static extern int GetWindowLong(nint hWnd, int index);
    [DllImport("user32.dll")] private static extern int SetWindowLong(nint hWnd, int index, int value);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
}
