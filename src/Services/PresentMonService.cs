using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;

namespace PulseOverlay.Services;

public sealed class PresentMonService : IDisposable
{
    private readonly ConcurrentDictionary<uint, ConcurrentQueue<long>> _frames = new();
    private Process? _process;

    public void Start()
    {
        string exe = Path.Combine(AppContext.BaseDirectory, "Tools", "PresentMon.exe");
        if (!File.Exists(exe)) return;
        try
        {
            _process = new Process { StartInfo = new ProcessStartInfo(exe,
                "--output_stdout --v1_metrics --no_console_stats --exclude PulseOverlay.exe --stop_existing_session --session_name PulseOverlay")
            { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true } };
            _process.OutputDataReceived += OnLine;
            _process.Start(); _process.BeginOutputReadLine();
        }
        catch { _process = null; }
    }

    private void OnLine(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Data) || e.Data.StartsWith("Application,")) return;
        var fields = ParseCsv(e.Data);
        if (fields.Count < 2 || !uint.TryParse(fields[1], out uint pid)) return;
        long now = Stopwatch.GetTimestamp();
        var q = _frames.GetOrAdd(pid, _ => new()); q.Enqueue(now);
        long cutoff = now - 2 * Stopwatch.Frequency;
        while (q.TryPeek(out long old) && old < cutoff) q.TryDequeue(out _);
    }

    public double? GetForegroundFps()
    {
        GetWindowThreadProcessId(GetForegroundWindow(), out uint pid);
        if (!_frames.TryGetValue(pid, out var q)) return null;
        long now = Stopwatch.GetTimestamp(), cutoff = now - Stopwatch.Frequency;
        while (q.TryPeek(out long old) && old < cutoff) q.TryDequeue(out _);
        return q.Count == 0 ? null : q.Count;
    }

    private static List<string> ParseCsv(string line)
    {
        var result = new List<string>(); var current = new System.Text.StringBuilder(); bool quoted = false;
        foreach (char c in line)
        {
            if (c == '"') quoted = !quoted;
            else if (c == ',' && !quoted) { result.Add(current.ToString()); current.Clear(); }
            else current.Append(c);
        }
        result.Add(current.ToString()); return result;
    }

    public void Dispose()
    {
        try { if (_process is { HasExited: false }) _process.Kill(true); } catch { }
        _process?.Dispose();
    }
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
}
