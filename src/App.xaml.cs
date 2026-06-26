using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using PulseOverlay.Models;
using PulseOverlay.Services;
using Forms = System.Windows.Forms;

namespace PulseOverlay;

public partial class App : System.Windows.Application
{
    private AppSettings _settings = null!;
    private MainWindow _settingsWindow = null!;
    private OverlayWindow _overlay = null!;
    private MonitoringCoordinator _monitor = null!;
    private Forms.NotifyIcon _tray = null!;
    private HwndSource? _hotkeyWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _settings = SettingsService.Load();
        _settingsWindow = new MainWindow(_settings);
        _overlay = new OverlayWindow(); _overlay.Show(); _overlay.Apply(_settings);
        CreateTray(); RegisterOverlayHotkey();
        _monitor = new MonitoringCoordinator();
        _monitor.Updated += snapshot => Dispatcher.BeginInvoke(() =>
        {
            _overlay.Update(snapshot); _settingsWindow.SetSensorStatus(snapshot.SensorStatus);
        });
        _monitor.Start();
        if (!e.Args.Contains("--minimized", StringComparer.OrdinalIgnoreCase)) _settingsWindow.Show();
    }

    public void ApplySettings() => _overlay.Apply(_settings);

    private void CreateTray()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open settings", null, (_, _) => Dispatcher.Invoke(OpenSettings));
        menu.Items.Add("Toggle overlay", null, (_, _) => Dispatcher.Invoke(ToggleOverlay));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Dispatcher.Invoke(ExitApp));
        _tray = new Forms.NotifyIcon { Text = "Pulse Overlay", Icon = SystemIcons.Application, Visible = true, ContextMenuStrip = menu };
        _tray.DoubleClick += (_, _) => Dispatcher.Invoke(OpenSettings);
    }

    private void OpenSettings() { _settingsWindow.Show(); _settingsWindow.WindowState = WindowState.Normal; _settingsWindow.Activate(); }
    private void ToggleOverlay() { _settings.ShowOverlay = !_settings.ShowOverlay; ApplySettings(); }

    private void RegisterOverlayHotkey()
    {
        var parameters = new HwndSourceParameters("PulseOverlayHotkey") { Width = 0, Height = 0, WindowStyle = unchecked((int)0x80000000) };
        _hotkeyWindow = new HwndSource(parameters); _hotkeyWindow.AddHook(HotkeyHook);
        RegisterHotKey(_hotkeyWindow.Handle, 1, 0x0002 | 0x0004, 0x4F);
    }

    private nint HotkeyHook(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == 0x0312 && wParam == 1) { ToggleOverlay(); handled = true; }
        return 0;
    }

    private void ExitApp()
    {
        SettingsService.Save(_settings); _monitor.Dispose(); _tray.Visible = false; _tray.Dispose();
        if (_hotkeyWindow is not null) { UnregisterHotKey(_hotkeyWindow.Handle, 1); _hotkeyWindow.Dispose(); }
        _settingsWindow.Exit(); _overlay.Close(); Shutdown();
    }

    protected override void OnExit(ExitEventArgs e) { try { _monitor?.Dispose(); } catch { } base.OnExit(e); }
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool RegisterHotKey(nint hWnd, int id, uint modifiers, uint key);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool UnregisterHotKey(nint hWnd, int id);
}
