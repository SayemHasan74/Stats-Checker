using LibreHardwareMonitor.Hardware;

namespace PulseOverlay.Services;

public sealed class HardwareReadings
{
    public double? CpuTemperature, GpuTemperature, GpuUsage, VramUsedGb, VramTotalGb,
        CpuClockMhz, GpuClockMhz, CpuPowerWatts, GpuPowerWatts;
    public string Status = "No supported temperature sensors detected";
}

public sealed class HardwareMonitorService : IDisposable
{
    private readonly Computer _computer = new()
    {
        IsCpuEnabled = true, IsGpuEnabled = true, IsMemoryEnabled = true,
        IsMotherboardEnabled = true, IsControllerEnabled = true
    };
    private bool _opened;

    public void Open()
    {
        try { _computer.Open(); _opened = true; }
        catch { _opened = false; }
    }

    public HardwareReadings Read()
    {
        var result = new HardwareReadings();
        if (!_opened) { result.Status = "Hardware sensor driver could not open"; return result; }
        var detected = new List<string>();
        foreach (var hardware in _computer.Hardware)
        {
            UpdateTree(hardware);
            ReadTree(hardware, result, detected);
        }
        result.Status = detected.Count == 0 ? "No supported temperature sensors detected" : string.Join(" | ", detected.Distinct());
        return result;
    }

    private static void UpdateTree(IHardware hardware)
    {
        hardware.Update();
        foreach (var child in hardware.SubHardware) UpdateTree(child);
    }

    private static void ReadTree(IHardware hardware, HardwareReadings r, List<string> detected)
    {
        bool cpu = hardware.HardwareType == HardwareType.Cpu;
        bool gpu = hardware.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel;
        foreach (var sensor in hardware.Sensors)
        {
            if (sensor.Value is not float raw || float.IsNaN(raw)) continue;
            double value = raw;
            string name = sensor.Name.ToLowerInvariant();
            if (cpu && sensor.SensorType == SensorType.Temperature && IsCpuPackage(name))
            {
                r.CpuTemperature = Max(r.CpuTemperature, value);
                detected.Add($"CPU: {hardware.Name} / {sensor.Name}");
            }
            else if (cpu && sensor.SensorType == SensorType.Temperature && IsUsableCpuTemperature(name, value))
            {
                r.CpuTemperature = Max(r.CpuTemperature, value);
                detected.Add($"CPU: {hardware.Name} / {sensor.Name}");
            }
            else if (cpu && sensor.SensorType == SensorType.Clock && (name.Contains("core #1") || name.Contains("core average"))) r.CpuClockMhz ??= value;
            else if (cpu && sensor.SensorType == SensorType.Power && IsPreferredCpuPower(name)) r.CpuPowerWatts = value;
            else if (cpu && sensor.SensorType == SensorType.Power && value > 0) r.CpuPowerWatts ??= value;
            else if (gpu && sensor.SensorType == SensorType.Temperature && name.Contains("core"))
            {
                r.GpuTemperature = Max(r.GpuTemperature, value);
                detected.Add($"GPU: {hardware.Name} / {sensor.Name}");
            }
            else if (gpu && sensor.SensorType == SensorType.Load && (name == "gpu core" || name.Contains("d3d 3d"))) r.GpuUsage = Max(r.GpuUsage, value);
            else if (gpu && sensor.SensorType == SensorType.Clock && name.Contains("core")) r.GpuClockMhz = value;
            else if (gpu && sensor.SensorType == SensorType.Power && (name.Contains("package") || name.Contains("gpu power"))) r.GpuPowerWatts = value;
            else if (gpu && sensor.SensorType == SensorType.SmallData && name.Contains("memory used")) r.VramUsedGb = value / 1024.0;
            else if (gpu && sensor.SensorType == SensorType.SmallData && name.Contains("memory total")) r.VramTotalGb = value / 1024.0;
        }
        foreach (var child in hardware.SubHardware) ReadTree(child, r, detected);
    }

    private static bool IsCpuPackage(string name) => name.Contains("package") || name.Contains("tdie") || name.Contains("tctl") || name == "core max";
    private static bool IsUsableCpuTemperature(string name, double value) =>
        value is > 0 and < 125 &&
        (name.StartsWith("core #") || name.Contains("core max") || name.Contains("cpu core") || name.Contains("ccd"));
    private static bool IsPreferredCpuPower(string name) =>
        name.Contains("package") || name.Contains("cpu total") || name.Contains("cores") || name.Contains("ppt");
    private static double Max(double? current, double value) => current is null ? value : Math.Max(current.Value, value);
    public void Dispose() { if (_opened) _computer.Close(); }
}
