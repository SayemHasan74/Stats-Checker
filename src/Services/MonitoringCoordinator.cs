using PulseOverlay.Models;

namespace PulseOverlay.Services;

public sealed class MonitoringCoordinator : IDisposable
{
    private readonly HardwareMonitorService _hardware = new();
    private readonly SystemMonitorService _system = new();
    private readonly PresentMonService _fps = new();
    private readonly CancellationTokenSource _cancel = new();
    public event Action<MetricSnapshot>? Updated;

    public void Start()
    {
        _hardware.Open(); _fps.Start();
        _ = Task.Run(Loop);
    }

    private async Task Loop()
    {
        while (!_cancel.IsCancellationRequested)
        {
            try
            {
                var s = _system.Read(); var h = _hardware.Read();
                Updated?.Invoke(new MetricSnapshot(_fps.GetForegroundFps(), s.CpuUsage, h.CpuTemperature,
                    h.GpuUsage, h.GpuTemperature, s.RamUsedGb, s.RamTotalGb, h.VramUsedGb, h.VramTotalGb,
                    s.DiskActivity, s.DiskMbps, s.NetworkDownloadMbps, s.NetworkUploadMbps,
                    h.CpuClockMhz, h.GpuClockMhz,
                    h.CpuPowerWatts, h.GpuPowerWatts, h.Status));
            }
            catch { }
            try { await Task.Delay(1000, _cancel.Token); } catch (OperationCanceledException) { }
        }
    }

    public void Dispose() { _cancel.Cancel(); _fps.Dispose(); _hardware.Dispose(); _system.Dispose(); _cancel.Dispose(); }
}
