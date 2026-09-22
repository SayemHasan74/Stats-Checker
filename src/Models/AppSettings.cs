using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PulseOverlay.Models;

public sealed class AppSettings : INotifyPropertyChanged
{
    private bool _showFps = true, _showCpuUsage = true, _showCpuTemp = true, _showGpuUsage = true,
        _showGpuTemp = true, _showRam = true, _showVram, _showDisk = true, _showNetwork = true,
        _showCpuClock, _showGpuClock, _showPower, _showClock, _showOverlay = true, _startWithWindows;
    private string _position = "Top Right", _edgeSpacing = "Flush to corner", _color = "#FFFFFF";
    private double _fontSize = 13, _opacity = 0.88;

    public bool ShowFps { get => _showFps; set => Set(ref _showFps, value); }
    public bool ShowCpuUsage { get => _showCpuUsage; set => Set(ref _showCpuUsage, value); }
    public bool ShowCpuTemp { get => _showCpuTemp; set => Set(ref _showCpuTemp, value); }
    public bool ShowGpuUsage { get => _showGpuUsage; set => Set(ref _showGpuUsage, value); }
    public bool ShowGpuTemp { get => _showGpuTemp; set => Set(ref _showGpuTemp, value); }
    public bool ShowRam { get => _showRam; set => Set(ref _showRam, value); }
    public bool ShowVram { get => _showVram; set => Set(ref _showVram, value); }
    public bool ShowDisk { get => _showDisk; set => Set(ref _showDisk, value); }
    public bool ShowNetwork { get => _showNetwork; set => Set(ref _showNetwork, value); }
    public bool ShowCpuClock { get => _showCpuClock; set => Set(ref _showCpuClock, value); }
    public bool ShowGpuClock { get => _showGpuClock; set => Set(ref _showGpuClock, value); }
    public bool ShowPower { get => _showPower; set => Set(ref _showPower, value); }
    public bool ShowClock { get => _showClock; set => Set(ref _showClock, value); }
    public bool ShowOverlay { get => _showOverlay; set => Set(ref _showOverlay, value); }
    public bool StartWithWindows { get => _startWithWindows; set => Set(ref _startWithWindows, value); }
    public string Position { get => _position; set => Set(ref _position, value); }
    public string EdgeSpacing { get => _edgeSpacing; set => Set(ref _edgeSpacing, value); }
    public string Color { get => _color; set => Set(ref _color, value); }
    public double FontSize { get => _fontSize; set => Set(ref _fontSize, value); }
    public double Opacity { get => _opacity; set => Set(ref _opacity, value); }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
