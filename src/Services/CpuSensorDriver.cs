using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace PulseOverlay.Services;

internal static class CpuSensorDriver
{
    // Installation is checked once, when CPU hardware monitoring is first enabled.
    // No installer or registry work runs in the sampling loop.
    private static readonly Lazy<string?> Installation = new(InstallIfMissing);
    public static string? EnsureInstalled() => Installation.Value;

    private static string? InstallIfMissing()
    {
        try
        {
            using var existing = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\PawnIO");
            if (existing is not null) return null;
            string installer = Path.Combine(AppContext.BaseDirectory, "Tools", "PawnIO_setup.exe");
            if (!File.Exists(installer)) return "CPU temperature unavailable: PawnIO driver installer is missing";
            using var process = Process.Start(new ProcessStartInfo(installer, "-install")
                { UseShellExecute = false, CreateNoWindow = true });
            if (process is null) return "CPU temperature unavailable: PawnIO installation could not start";
            process.WaitForExit();
            using var installed = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\PawnIO");
            return installed is not null ? null : $"CPU temperature unavailable: PawnIO installation failed ({process.ExitCode})";
        }
        catch (Exception ex) { return "CPU temperature unavailable: " + ex.Message; }
    }
}
