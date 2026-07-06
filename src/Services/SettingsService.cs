using System.Text.Json;
using System.IO;
using Microsoft.Win32;
using PulseOverlay.Models;

namespace PulseOverlay.Services;

public static class SettingsService
{
    private static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PulseOverlay");
    private static readonly string FilePath = Path.Combine(Folder, "settings.json");

    public static AppSettings Load()
    {
        try { return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new(); }
        catch { return new(); }
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        using var run = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
        if (settings.StartWithWindows)
            run?.SetValue("PulseOverlay", $"\"{Environment.ProcessPath}\" --minimized");
        else run?.DeleteValue("PulseOverlay", false);
    }
}
