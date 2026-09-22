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
    private readonly Computer _computer;
    private bool _opened;
    private int _reads;
    private readonly List<ISensor> _cpuTemp = [], _cpuFallback = [], _cpuPower = [], _powerFallback = [];
    private ISensor? _cpuClock, _gpuTemp, _gpuLoad, _gpuClock, _gpuPower, _vramUsed, _vramTotal;
    private string _status = "Initializing sensors...";

    public HardwareMonitorService(bool cpu, bool gpu)
    {
        _computer = new Computer
        {
            IsCpuEnabled = cpu, IsGpuEnabled = gpu,
            IsMotherboardEnabled = cpu, IsControllerEnabled = cpu
        };
    }

    public void Open()
    {
        try { _computer.Open(); _opened = true; }
        catch { _computer.Close(); }
    }

    public HardwareReadings Read()
    {
        if (!_opened) return new() { Status = "Hardware sensor driver could not open" };
        foreach (var hardware in _computer.Hardware) UpdateTree(hardware);
        // Sensor names and selection are cached; periodically rediscover delayed sensors.
        if (_reads++ % 30 == 0) Discover();
        return new HardwareReadings
        {
            CpuTemperature = Hottest(_cpuTemp) ?? Hottest(_cpuFallback),
            CpuPowerWatts = FirstPower(_cpuPower) ?? FirstPower(_powerFallback),
            CpuClockMhz = Value(_cpuClock), GpuTemperature = Value(_gpuTemp),
            GpuUsage = Value(_gpuLoad), GpuClockMhz = Value(_gpuClock), GpuPowerWatts = Value(_gpuPower),
            VramUsedGb = Value(_vramUsed) / 1024, VramTotalGb = Value(_vramTotal) / 1024,
            Status = _status
        };
    }

    private void Discover()
    {
        _cpuTemp.Clear(); _cpuFallback.Clear(); _cpuPower.Clear(); _powerFallback.Clear();
        _cpuClock = _gpuTemp = _gpuLoad = _gpuClock = _gpuPower = _vramUsed = _vramTotal = null;
        var all = _computer.Hardware.SelectMany(Tree).ToArray();
        // Keep all GPU metrics on one adapter; prefer a discrete GPU over integrated graphics.
        var gpu = all.FirstOrDefault(h => h.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd)
            ?? all.FirstOrDefault(h => h.HardwareType == HardwareType.GpuIntel);
        foreach (var h in all)
        {
            bool cpu = h.HardwareType == HardwareType.Cpu;
            bool isGpu = h.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel;
            foreach (var sensor in h.Sensors)
            {
                string n = sensor.Name.ToLowerInvariant();
                bool cpuNamed = cpu || (!isGpu && (n.Contains("cpu") || n.Contains("processor") ||
                    n.Contains("package") || n.Contains("tdie") || n.Contains("tctl") || n.Contains("ccd") || n.Contains("peci") || n.Contains("core")));
                if (cpuNamed && sensor.SensorType == SensorType.Temperature)
                {
                    if (n.Contains("package") || n.Contains("tdie") || n.Contains("tctl") || n.Contains("cpu socket") || n.Contains("peci") || n == "core max")
                        _cpuTemp.Add(sensor);
                    else if (!n.Contains("distance") && !n.Contains("throttle") &&
                        (n.StartsWith("core #") || n.Contains("cpu core") || n.Contains("ccd") || n == "cpu" || n == "processor"))
                        _cpuFallback.Add(sensor);
                }
                if (cpuNamed && sensor.SensorType == SensorType.Power)
                {
                    if (n.Contains("package") || n.Contains("cpu total") || n.Contains("ppt") || n == "cpu") _cpuPower.Insert(0, sensor);
                    else if (n.Contains("cores")) _cpuPower.Add(sensor);
                    else if (cpu) _powerFallback.Add(sensor);
                }
                if (cpu && sensor.SensorType == SensorType.Clock && (n.Contains("core #1") || n.Contains("core average"))) _cpuClock ??= sensor;
                if (h != gpu) continue;
                if (sensor.SensorType == SensorType.Temperature && n.Contains("core")) _gpuTemp ??= sensor;
                if (sensor.SensorType == SensorType.Load && (n == "gpu core" || n.Contains("d3d 3d"))) _gpuLoad ??= sensor;
                if (sensor.SensorType == SensorType.Clock && n.Contains("core")) _gpuClock ??= sensor;
                if (sensor.SensorType == SensorType.Power && (n.Contains("package") || n.Contains("gpu power"))) _gpuPower ??= sensor;
                if (sensor.SensorType == SensorType.SmallData && n.Contains("memory used")) _vramUsed ??= sensor;
                if (sensor.SensorType == SensorType.SmallData && n.Contains("memory total")) _vramTotal ??= sensor;
            }
        }
        var names = new List<string>();
        foreach (var s in _cpuTemp.Concat(_cpuFallback)) names.Add("CPU: " + s.Hardware.Name + " / " + s.Name);
        foreach (var s in _cpuPower.Concat(_powerFallback)) names.Add("CPU PWR: " + s.Hardware.Name + " / " + s.Name);
        if (_gpuTemp is not null) names.Add("GPU: " + _gpuTemp.Hardware.Name + " / " + _gpuTemp.Name);
        _status = names.Count == 0 ? "No temperature or power sensors selected or available" : string.Join(" | ", names.Distinct());
    }

    private static IEnumerable<IHardware> Tree(IHardware h)
    {
        yield return h;
        foreach (var child in h.SubHardware)
            foreach (var item in Tree(child)) yield return item;
    }
    private static void UpdateTree(IHardware h) { h.Update(); foreach (var child in h.SubHardware) UpdateTree(child); }
    private static double? Value(ISensor? s) => s?.Value is float v && float.IsFinite(v) ? v : null;
    private static double? Hottest(List<ISensor> sensors)
    {
        double? result = null;
        foreach (var s in sensors) if (Value(s) is double v && v > 0 && v < 125) result = result is null ? v : Math.Max(result.Value, v);
        return result;
    }
    private static double? FirstPower(List<ISensor> sensors)
    {
        foreach (var s in sensors) if (Value(s) is double v && v > 0 && v < 500) return v;
        return null;
    }
    public void Dispose() { if (_opened) { _computer.Close(); _opened = false; } }
}
