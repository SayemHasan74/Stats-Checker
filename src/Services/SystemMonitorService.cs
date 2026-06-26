using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PulseOverlay.Services;

public sealed class SystemReadings
{
    public double CpuUsage, RamUsedGb, RamTotalGb;
    public double? DiskActivity, DiskMbps, NetworkDownloadMbps, NetworkUploadMbps;
}

public sealed class SystemMonitorService : IDisposable
{
    private ulong _lastIdle, _lastKernel, _lastUser;
    private readonly PerformanceCounter? _diskActivity, _diskBytes;
    private readonly List<PerformanceCounter> _networkDown = [];
    private readonly List<PerformanceCounter> _networkUp = [];

    public SystemMonitorService()
    {
        GetSystemTimes(out var idle, out var kernel, out var user);
        _lastIdle = idle.Value; _lastKernel = kernel.Value; _lastUser = user.Value;
        try
        {
            _diskActivity = new("PhysicalDisk", "% Disk Time", "_Total");
            _diskBytes = new("PhysicalDisk", "Disk Bytes/sec", "_Total");
            _diskActivity.NextValue(); _diskBytes.NextValue();
            foreach (string instance in new PerformanceCounterCategory("Network Interface").GetInstanceNames())
            {
                var down = new PerformanceCounter("Network Interface", "Bytes Received/sec", instance);
                var up = new PerformanceCounter("Network Interface", "Bytes Sent/sec", instance);
                down.NextValue(); up.NextValue();
                _networkDown.Add(down); _networkUp.Add(up);
            }
        }
        catch { }
    }

    public SystemReadings Read()
    {
        var r = new SystemReadings();
        if (GetSystemTimes(out var idle, out var kernel, out var user))
        {
            ulong idleDelta = idle.Value - _lastIdle, total = kernel.Value - _lastKernel + user.Value - _lastUser;
            r.CpuUsage = total == 0 ? 0 : Math.Clamp(100.0 * (total - idleDelta) / total, 0, 100);
            _lastIdle = idle.Value; _lastKernel = kernel.Value; _lastUser = user.Value;
        }
        var memory = new MemoryStatusEx();
        if (GlobalMemoryStatusEx(memory))
        {
            r.RamTotalGb = memory.TotalPhysical / 1073741824.0;
            r.RamUsedGb = (memory.TotalPhysical - memory.AvailablePhysical) / 1073741824.0;
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
        try
        {
            if (_networkDown.Count > 0)
            {
                r.NetworkDownloadMbps = _networkDown.Sum(c => Math.Max(0, c.NextValue())) * 8 / 1_000_000.0;
                r.NetworkUploadMbps = _networkUp.Sum(c => Math.Max(0, c.NextValue())) * 8 / 1_000_000.0;
            }
        }
        catch { }
        return r;
    }

    public void Dispose()
    {
        _diskActivity?.Dispose(); _diskBytes?.Dispose();
        foreach (var counter in _networkDown) counter.Dispose();
        foreach (var counter in _networkUp) counter.Dispose();
    }

    [StructLayout(LayoutKind.Sequential)] private struct FileTime { public uint Low, High; public readonly ulong Value => ((ulong)High << 32) | Low; }
    [DllImport("kernel32.dll")] private static extern bool GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)] private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx status);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)] private sealed class MemoryStatusEx
    {
        public uint Length = (uint)Marshal.SizeOf<MemoryStatusEx>(); public uint Load;
        public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile, TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
    }
}
