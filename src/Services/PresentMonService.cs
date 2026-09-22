using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.IO;

namespace PulseOverlay.Services;

public sealed class PresentMonService : IDisposable
{
    private Process? _process;
    private uint _target;
    private long _lastSample;
    private long _retryAfter;
    private int _frames;

    public double? Sample(bool enabled)
    {
        GetWindowThreadProcessId(GetForegroundWindow(), out uint pid);
        if (!enabled || pid == 0 || pid == Environment.ProcessId) { Stop(); return null; }
        long now = Stopwatch.GetTimestamp();
        if (_target != pid || _process is null || _process.HasExited)
        {
            if (_target == pid && now < _retryAfter) return null;
            Stop(); _target = pid;
            _retryAfter = now + 10 * Stopwatch.Frequency;
            Start(pid); _lastSample = now;
            return null;
        }
        int frames = Interlocked.Exchange(ref _frames, 0);
        double seconds = (now - _lastSample) / (double)Stopwatch.Frequency;
        _lastSample = now;
        return frames == 0 || seconds <= 0 ? null : frames / seconds;
    }

    private void Start(uint pid)
    {
        string exe = Path.Combine(AppContext.BaseDirectory, "Tools", "PresentMon.exe");
        if (!File.Exists(exe)) return;
        try
        {
            var process = new Process { StartInfo = new ProcessStartInfo(exe,
                $"--process_id {pid} --output_stdout --v1_metrics --no_console_stats --no_track_gpu --no_track_input --no_track_display --terminate_on_proc_exit --stop_existing_session --session_name PulseOverlay-{Environment.ProcessId}")
            { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true } };
            _process = process;
            process.OutputDataReceived += OnLine;
            process.ErrorDataReceived += (_, _) => { };
            process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
        }
        catch { StopProcess(); }
    }

    private void OnLine(object sender, DataReceivedEventArgs e)
    {
        if (!ReferenceEquals(sender, _process) || string.IsNullOrEmpty(e.Data)) return;
        // Parse only the PID column, without per-frame lists, strings or queue nodes.
        ReadOnlySpan<char> row = e.Data.AsSpan();
        bool quoted = false;
        for (int i = 0; i < row.Length; i++)
        {
            if (row[i] == '"') quoted = !quoted;
            else if (row[i] == ',' && !quoted)
            {
                var rest = row[(i + 1)..];
                int end = rest.IndexOf(',');
                if (end > 0 && uint.TryParse(rest[..end], NumberStyles.Integer, CultureInfo.InvariantCulture, out uint pid) && pid == _target)
                    Interlocked.Increment(ref _frames);
                break;
            }
        }
    }

    private void StopProcess()
    {
        var process = _process; _process = null;
        if (process is null) return;
        process.OutputDataReceived -= OnLine;
        try
        {
            if (!process.HasExited)
            {
                using var stop = Process.Start(new ProcessStartInfo(process.StartInfo.FileName,
                    $"--terminate_existing_session --session_name PulseOverlay-{Environment.ProcessId}")
                    { UseShellExecute = false, CreateNoWindow = true });
                if (stop is not null && !stop.WaitForExit(2000)) stop.Kill();
                if (!process.WaitForExit(2000)) process.Kill(true);
                process.WaitForExit();
            }
        }
        catch { try { if (!process.HasExited) process.Kill(true); } catch { } }
        process.Dispose();
    }
    private void Stop() { StopProcess(); _target = 0; Interlocked.Exchange(ref _frames, 0); }
    public void Dispose() => Stop();
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
}
