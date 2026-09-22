using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Net.NetworkInformation;

namespace PulseOverlay.Services;

public sealed class SystemReadings
{
    public double? CpuUsage, RamUsedGb, RamTotalGb;
    public double? DiskActivity, DiskMbps, NetworkDownloadMbps, NetworkUploadMbps;
}

public sealed class SystemMonitorService : IDisposable
{
    private ulong _lastIdle, _lastKernel, _lastUser;
    private bool _cpuPrimed, _diskEnabled, _networkEnabled;
    private PerformanceCounter? _diskActivity, _diskBytes;
    private readonly Dictionary<string, (long Down, long Up)> _networkPrevious = new();
    private long _networkTime;
    private NetworkInterface[] _adapters = [];
    private int _networkReads;
    private readonly MemoryStatusEx _memory = new();

    public void Configure(MonitoringOptions o)
    {
        bool disk = o.Active && o.Disk, network = o.Active && o.Network;
        if (!o.Active || !o.Cpu) _cpuPrimed = false;
        if (disk != _diskEnabled)
        {
            _diskEnabled = disk;
            _diskActivity?.Dispose(); _diskBytes?.Dispose();
            _diskActivity = _diskBytes = null;
            if (disk)
            {
                try
                {
                    _diskActivity = new("PhysicalDisk", "% Disk Time", "_Total");
                    _diskBytes = new("PhysicalDisk", "Disk Bytes/sec", "_Total");
                    _diskActivity.NextValue(); _diskBytes.NextValue();
                }
                catch { _diskActivity?.Dispose(); _diskBytes?.Dispose(); _diskActivity = _diskBytes = null; }
            }
        }
        if (network != _networkEnabled)
        {
            _networkEnabled = network;
            _networkPrevious.Clear(); _adapters = []; _networkReads = 0;
            _networkTime = Stopwatch.GetTimestamp();
        }
    }

    public SystemReadings Read(MonitoringOptions o)
    {
        var r = new SystemReadings();
        if (o.Cpu && GetSystemTimes(out var idle, out var kernel, out var user))
        {
            ulong idleDelta = idle.Value - _lastIdle, total = kernel.Value - _lastKernel + user.Value - _lastUser;
            if (_cpuPrimed && total > 0) r.CpuUsage = Math.Clamp(100.0 * (1 - idleDelta / (double)total), 0, 100);
            _lastIdle = idle.Value; _lastKernel = kernel.Value; _lastUser = user.Value; _cpuPrimed = true;
        }
        if (o.Ram && GlobalMemoryStatusEx(_memory))
        {
            r.RamTotalGb = _memory.TotalPhysical / 1073741824.0;
            r.RamUsedGb = (_memory.TotalPhysical - _memory.AvailablePhysical) / 1073741824.0;
        }
        try
        {
            if (_diskActivity is not null && _diskBytes is not null)
            {
                r.DiskActivity = Math.Clamp(_diskActivity.NextValue(), 0, 100);
                r.DiskMbps = _diskBytes.NextValue() / 1048576.0;
            }
        }
        catch { }
        if (_networkEnabled)
        {
            try
            {
                if (_networkReads++ % 30 == 0) _adapters = NetworkInterface.GetAllNetworkInterfaces();
                long now = Stopwatch.GetTimestamp();
                double seconds = (now - _networkTime) / (double)Stopwatch.Frequency;
                long down = 0, up = 0; bool sampled = false;
                foreach (var adapter in _adapters)
                {
                    if (adapter.OperationalStatus != OperationalStatus.Up || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    var stats = adapter.GetIPv4Statistics();
                    if (_networkPrevious.TryGetValue(adapter.Id, out var previous))
                    {
                        down += Math.Max(0, stats.BytesReceived - previous.Down);
                        up += Math.Max(0, stats.BytesSent - previous.Up);
                        sampled = true;
                    }
                    _networkPrevious[adapter.Id] = (stats.BytesReceived, stats.BytesSent);
                }
                _networkTime = now;
                if (sampled && seconds > 0)
                {
                    r.NetworkDownloadMbps = down * 8 / seconds / 1_000_000;
                    r.NetworkUploadMbps = up * 8 / seconds / 1_000_000;
                }
            }
            catch { }
        }
        return r;
    }

    public void Dispose() { _diskActivity?.Dispose(); _diskBytes?.Dispose(); }
    [StructLayout(LayoutKind.Sequential)] private struct FileTime { public uint Low, High; public readonly ulong Value => ((ulong)High << 32) | Low; }
    [DllImport("kernel32.dll")] private static extern bool GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)] private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx status);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)] private sealed class MemoryStatusEx
    {
        public uint Length = (uint)Marshal.SizeOf<MemoryStatusEx>(); public uint Load;
        public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile, TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
    }
}
