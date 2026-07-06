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
        var cpuCandidates = new List<string>();
        foreach (var hardware in _computer.Hardware)
        {
            UpdateTree(hardware);
            ReadTree(hardware, result, detected, cpuCandidates);
        }
        result.Status = BuildStatus(result, detected, cpuCandidates);
        return result;
    }

    private static void UpdateTree(IHardware hardware)
    {
        hardware.Update();
        foreach (var child in hardware.SubHardware) UpdateTree(child);
    }

    private static void ReadTree(IHardware hardware, HardwareReadings r, List<string> detected, List<string> cpuCandidates)
    {
        bool cpu = hardware.HardwareType == HardwareType.Cpu;
        bool gpu = hardware.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel;
        foreach (var sensor in hardware.Sensors)
        {
            if (sensor.Value is not float raw || float.IsNaN(raw)) continue;
            double value = raw;
            string name = sensor.Name.ToLowerInvariant();
            bool cpuNamedSensor = IsCpuNamedSensor(name);
            bool cpuSensor = cpu || (!gpu && cpuNamedSensor);
            if (cpuSensor && sensor.SensorType is SensorType.Temperature or SensorType.Power)
                cpuCandidates.Add($"{hardware.Name} / {sensor.Name} {value:0.#}");

            if (cpuSensor && sensor.SensorType == SensorType.Temperature && IsPreferredCpuTemperature(name, value))
            {
                r.CpuTemperature = Max(r.CpuTemperature, value);
                detected.Add($"CPU: {hardware.Name} / {sensor.Name}");
            }
            else if (cpuSensor && sensor.SensorType == SensorType.Temperature && IsFallbackCpuTemperature(name, value))
            {
                r.CpuTemperature = Max(r.CpuTemperature, value);
                detected.Add($"CPU: {hardware.Name} / {sensor.Name}");
            }
            else if (cpu && sensor.SensorType == SensorType.Clock && (name.Contains("core #1") || name.Contains("core average"))) r.CpuClockMhz ??= value;
            else if (cpuSensor && sensor.SensorType == SensorType.Power && IsPreferredCpuPower(name, value))
            {
                r.CpuPowerWatts = value;
                detected.Add($"CPU PWR: {hardware.Name} / {sensor.Name}");
            }
            else if (cpu && sensor.SensorType == SensorType.Power && IsFallbackPower(value))
            {
                r.CpuPowerWatts ??= value;
                detected.Add($"CPU PWR: {hardware.Name} / {sensor.Name}");
            }
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
        foreach (var child in hardware.SubHardware) ReadTree(child, r, detected, cpuCandidates);
    }

    private static string BuildStatus(HardwareReadings readings, List<string> detected, List<string> cpuCandidates)
    {
        var parts = detected.Distinct().ToList();
        if (readings.CpuTemperature is null) parts.Add("CPU temp N/A");
        if (readings.CpuPowerWatts is null) parts.Add("CPU power N/A");
        if (cpuCandidates.Count > 0)
            parts.Add("CPU-like sensors seen: " + string.Join(", ", cpuCandidates.Distinct().Take(8)));
        return parts.Count == 0 ? "No supported temperature sensors detected" : string.Join(" | ", parts);
    }

    private static bool IsCpuNamedSensor(string name) =>
        name.Contains("cpu") || name.Contains("processor") || name.Contains("package") ||
        name.Contains("tdie") || name.Contains("tctl") || name.Contains("ccd") ||
        name.Contains("peci") || name.Contains("core");

    private static bool IsPreferredCpuTemperature(string name, double value) =>
        IsSaneTemperature(value) &&
        (name.Contains("package") || name.Contains("tdie") || name.Contains("tctl") ||
         name.Contains("cpu package") || name.Contains("cpu socket") || name.Contains("peci") ||
         name == "core max");

    private static bool IsFallbackCpuTemperature(string name, double value) =>
        IsSaneTemperature(value) &&
        !name.Contains("distance") &&
        !name.Contains("throttle") &&
        (name.StartsWith("core #") || name.Contains("core max") || name.Contains("cpu core") ||
         name.Contains("ccd") || name == "cpu" || name == "processor");

    private static bool IsPreferredCpuPower(string name, double value) =>
        IsFallbackPower(value) &&
        (name.Contains("package") || name.Contains("cpu total") || name.Contains("cpu package") ||
         name.Contains("cores") || name.Contains("ppt") || name == "cpu");

    private static bool IsSaneTemperature(double value) => value is > 0 and < 125;
    private static bool IsFallbackPower(double value) => value is > 0 and < 500;
    private static double Max(double? current, double value) => current is null ? value : Math.Max(current.Value, value);
    public void Dispose() { if (_opened) _computer.Close(); }
}
