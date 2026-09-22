using PulseOverlay.Models;

namespace PulseOverlay.Services;

// Immutable copy: the sampling worker never reads mutable WPF-bound settings.
public sealed record MonitoringOptions(bool Active, bool Fps, bool Cpu, bool Ram,
    bool Disk, bool Network, bool CpuHardware, bool GpuHardware)
{
    public static MonitoringOptions From(AppSettings s, bool preview) => new(
        s.ShowOverlay || preview, s.ShowFps, s.ShowCpuUsage, s.ShowRam,
        s.ShowDisk, s.ShowNetwork, s.ShowCpuTemp || s.ShowCpuClock || s.ShowPower,
        s.ShowGpuTemp || s.ShowGpuUsage || s.ShowGpuClock || s.ShowVram || s.ShowPower);
}

