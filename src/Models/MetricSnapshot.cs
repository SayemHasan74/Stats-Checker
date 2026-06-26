namespace PulseOverlay.Models;

public sealed record MetricSnapshot(
    double? Fps = null, double? CpuUsage = null, double? CpuTemperature = null,
    double? GpuUsage = null, double? GpuTemperature = null, double? RamUsedGb = null,
    double? RamTotalGb = null, double? VramUsedGb = null, double? VramTotalGb = null,
    double? DiskActivity = null, double? DiskMbps = null, double? NetworkDownloadMbps = null,
    double? NetworkUploadMbps = null,
    double? CpuClockMhz = null, double? GpuClockMhz = null, double? CpuPowerWatts = null,
    double? GpuPowerWatts = null, string SensorStatus = "Initializing sensors...");
