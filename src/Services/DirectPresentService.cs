using System.Diagnostics;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;

namespace PulseOverlay.Services;

// Some current DXGI builds emit the public Present call events without the
// analytic events used by PresentMon. Count successful calls on that path.
public sealed class DirectPresentService : IDisposable
{
    private static readonly Guid Dxgi = new("CA11C036-0102-4A2D-A6AD-F03CFED5D3C9");
    private readonly object _gate = new();
    private readonly Dictionary<int, ulong> _pending = new();
    private readonly Dictionary<ulong, Frames> _chains = new();
    private readonly byte[] _payload = new byte[16];
    private TraceEventSession? _session;
    private Task? _reader;
    private uint _pid;
    private long _lastReceived, _retryAfter;
    private double? _lastFps;
    public string Status { get; private set; } = "FPS: waiting for a foreground application";

    private sealed class Frames
    {
        public double First, Last;
        public int Count;
    }

    public double? Sample(uint pid)
    {
        if (_pid != pid) { Stop(); _pid = pid; _retryAfter = 0; }
        if (pid == 0) return null;
        if (_session is null && Stopwatch.GetTimestamp() >= _retryAfter) Start(pid);
        if (_session is null) return null;
        if (_reader?.IsCompleted == true)
        {
            Stop();
            Status = "FPS: DirectX capture stopped";
            return null;
        }
        _session.Flush();
        lock (_gate)
        {
            int bestCount = 0;
            foreach (var chain in _chains.Values)
            {
                if (chain.Count > bestCount && chain.Last > chain.First)
                {
                    bestCount = chain.Count;
                    _lastFps = 1000 * chain.Count / (chain.Last - chain.First);
                }
                chain.First = chain.Last; chain.Count = 0;
            }
            if (Stopwatch.GetElapsedTime(_lastReceived).TotalSeconds > 3) _lastFps = null;
            Status = _lastFps is null ? "FPS: waiting for DirectX presents" : "FPS: DirectX present capture";
            return _lastFps;
        }
    }

    private void Start(uint pid)
    {
        _retryAfter = Stopwatch.GetTimestamp() + 10 * Stopwatch.Frequency;
        try
        {
            _session = new TraceEventSession($"PulseOverlay-Direct-{Environment.ProcessId}") { BufferSizeMB = 4 };
            // Filter in ETW: no GPU workload, input, stack or unrelated-process tracing.
            _session.EnableProvider(Dxgi, TraceEventLevel.Verbose, ulong.MaxValue,
                new TraceEventProviderOptions { ProcessIDFilter = [(int)pid], EventIDsToEnable = [178, 179] });
            var source = _session.Source;
            source.AllEvents += OnEvent;
            _reader = Task.Factory.StartNew(() =>
            {
                try { source.Process(); }
                catch (Exception ex) { Status = "FPS capture could not continue: " + ex.Message; }
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }
        catch (Exception ex)
        {
            Stop();
            Status = "FPS capture could not start: " + ex.Message;
        }
    }

    private void OnEvent(TraceEvent data)
    {
        if ((uint)data.ProcessID != _pid || data.ProviderGuid != Dxgi) return;
        int id = (int)data.ID;
        if (id == 178 && data.EventDataLength >= 16)
        {
            data.EventData(_payload, 0, 0, 16);
            lock (_gate)
            {
                _pending.Remove(data.ThreadID);
                // DXGI_PRESENT_TEST probes availability; it does not render a frame.
                if ((BitConverter.ToUInt32(_payload, 12) & 1) == 0 && _pending.Count < 128)
                    _pending[data.ThreadID] = BitConverter.ToUInt64(_payload, 0);
            }
        }
        else if (id == 179 && data.EventDataLength >= 4)
        {
            data.EventData(_payload, 0, 0, 4);
            lock (_gate)
            {
                if (!_pending.Remove(data.ThreadID, out ulong swapchain) || BitConverter.ToInt32(_payload, 0) != 0) return;
                double now = data.TimeStampRelativeMSec;
                if (!_chains.TryGetValue(swapchain, out var chain))
                {
                    if (_chains.Count >= 16) return;
                    _chains.Add(swapchain, new Frames { First = now, Last = now });
                }
                else if (now > chain.Last) { chain.Last = now; chain.Count++; }
                _lastReceived = Stopwatch.GetTimestamp();
            }
        }
    }

    private void Stop()
    {
        _session?.Dispose(); _session = null;
        _reader?.GetAwaiter().GetResult(); _reader = null;
        lock (_gate) { _pending.Clear(); _chains.Clear(); _lastFps = null; _lastReceived = 0; }
    }
    public void Dispose() => Stop();
}

