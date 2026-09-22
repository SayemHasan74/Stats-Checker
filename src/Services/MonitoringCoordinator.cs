using PulseOverlay.Models;

namespace PulseOverlay.Services;

public sealed class MonitoringCoordinator : IDisposable
{
    private readonly CancellationTokenSource _cancel = new();
    private volatile MonitoringOptions _options = new(false, false, false, false, false, false, false, false);
    private Task? _worker;
    private bool _disposed;
    public event Action<MetricSnapshot>? Updated;
    public void Configure(AppSettings settings, bool preview) => _options = MonitoringOptions.From(settings, preview);
    public void Start() => _worker ??= Task.Run(Loop);

    private async Task Loop()
    {
        using var system = new SystemMonitorService();
        using var fps = new PresentMonService();
        HardwareMonitorService? hardware = null;
        (bool Cpu, bool Gpu) enabled = default;
        try
        {
            while (!_cancel.IsCancellationRequested)
            {
                var o = _options;
                try
                {
                    var requested = (o.Active && o.CpuHardware, o.Active && o.GpuHardware);
                    if (requested != enabled)
                    {
                        hardware?.Dispose(); hardware = null;
                        enabled = requested;
                        if (requested.Item1 || requested.Item2)
                        {
                            hardware = new HardwareMonitorService(requested.Item1, requested.Item2);
                            hardware.Open();
                        }
                    }
                    system.Configure(o);
                    double? frameRate = fps.Sample(o.Active && o.Fps);
                    if (o.Active)
                    {
                        var s = system.Read(o);
                        var h = hardware?.Read() ?? new HardwareReadings { Status = "Hardware sensors paused — no hardware metrics selected" };
                        Updated?.Invoke(new MetricSnapshot(frameRate, s.CpuUsage, h.CpuTemperature,
                            h.GpuUsage, h.GpuTemperature, s.RamUsedGb, s.RamTotalGb, h.VramUsedGb, h.VramTotalGb,
                            s.DiskActivity, s.DiskMbps, s.NetworkDownloadMbps, s.NetworkUploadMbps,
                            h.CpuClockMhz, h.GpuClockMhz, h.CpuPowerWatts, h.GpuPowerWatts,
                            o.Fps ? h.Status + " | " + fps.Status : h.Status));
                    }
                }
                catch (Exception ex)
                {
                    Updated?.Invoke(new MetricSnapshot(SensorStatus: "Monitoring could not refresh: " + ex.Message));
                }
                await Task.Delay(1000, _cancel.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (_cancel.IsCancellationRequested) { }
        finally { hardware?.Dispose(); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cancel.Cancel();
        // The worker owns its resources; never close a driver during a sensor read.
        _worker?.GetAwaiter().GetResult();
        _cancel.Dispose();
    }
}
