# Pulse Overlay

Pulse Overlay is a lightweight, configurable Windows performance overlay. It renders one click-through horizontal line and lets you independently enable FPS, clock, CPU/GPU use and temperature, RAM/VRAM, disk, network `↓`/`↑` speed, clocks, and power.

## Ready-to-run files

- `Deliverables\Installed Version\PulseOverlay.exe`: portable/self-contained build. Open this EXE and approve the Windows administrator prompt.
- `Deliverables\PulseOverlay-Installer.exe`: installer for another Windows PC.

The installed version includes the .NET runtime, LibreHardwareMonitor dependencies, and Intel PresentMon. No separate runtime package is required.

## Usage

Version 1.1 adds a monochrome settings UI and a live preview. The preview uses the same metric strip as the overlay and scales down to fit its panel. The overlay fits the primary screen; settings and saved metric selections carry over from previous versions.

Version 1.1.1 fixes strip sizing when removing metrics such as the clock and prevents duplicate app instances from drawing overlapping overlays. Reopening the app reveals the existing settings window.

1. Launch `PulseOverlay.exe` and approve administrator access. Sensor-driver access and ETW FPS collection require it.
2. Enable only the metrics you want.
3. Choose a corner, edge spacing, font size, background opacity, and overlay color. **Flush to corner** removes the panel inset so the text itself reaches the selected screen edges; **Comfortable padding** keeps the original spacing.
4. Select **Save changes** to save and hide settings. The overlay keeps running in the notification area; use its tray menu to reopen settings.
5. Press `Ctrl+Shift+O` or use the tray menu to toggle the overlay.

FPS follows the foreground application. Borderless-windowed mode is recommended because exclusive-fullscreen and some anti-cheat systems can prevent independent overlays or ETW capture. Pulse Overlay does not inject DLLs into games.

## Low-overhead operation

- Existing text controls are reused; sampling does not rebuild the overlay or force its layout.
- System readings update once per second on a background worker. Disk and network collection stop when disabled; CPU and GPU hardware monitoring stop when their respective metric groups are disabled.
- When both the overlay and settings are hidden, collection pauses. Opening settings resumes collection for the live preview.
- PresentMon runs only when FPS is selected and another application is foreground. Capture is filtered to that process, with GPU-duration, input, and display tracking disabled. FPS counts application presents per elapsed second; it is not a measurement of displayed or generated frames. Switching applications can briefly show N/A.
- FPS processing uses a counter instead of per-process frame queues. Sensor discovery is cached and refreshed every 30 samples. Network adapters are refreshed every 30 samples.
- On systems with multiple GPUs, readings come from the first detected discrete GPU, falling back to integrated graphics. All GPU fields use that same adapter.
- The UI uses native WPF controls with no animations or continuously running preview effects. Actual game performance impact depends on the hardware and selected metrics; no zero-overhead claim is made.

## Temperature accuracy

CPU temperature prefers CPU Package, Tdie/Tctl, CPU Socket/PECI, and Core Max sensors reported by LibreHardwareMonitor. If those are not exposed, it falls back to real CPU core/CCD temperature sensors and uses the hottest value. CPU power prefers package/CPU total/cores/PPT sensors and can also read CPU-named power sensors exposed by the motherboard controller. GPU temperature uses the GPU Core sensor. The settings page displays the exact detected sensor names. If hardware, firmware, permissions, or a laptop EC does not expose a supported sensor, the overlay displays `N/A`; it does not substitute an unrelated ACPI thermal-zone value.

## Supported system

- Windows 10 or Windows 11, 64-bit
- Administrator access at launch
- A hardware sensor supported by LibreHardwareMonitor for temperature fields

## Rebuilding from source

Build dependencies:

- .NET 9 SDK (x64)
- Inno Setup 6
- Internet access for the first NuGet restore

Install the tools with Windows Package Manager:

```powershell
winget install Microsoft.DotNet.SDK.9
winget install JRSoftware.InnoSetup
```

Then build both deliverables:

```powershell
Set-Location 'E:\Codes\PC Performance Overlay\Source Code'
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

For only the portable build, use `powershell -ExecutionPolicy Bypass -File .\build.ps1 -SkipInstaller`; Inno Setup is not needed for this option.

NuGet dependencies are declared in `src\PulseOverlay.csproj`; do not manually copy them. The bundled `src\Tools\PresentMon.exe` is the official Intel PresentMon 2.4.1 x64 release.

## Troubleshooting

- **CPU temperature is N/A:** update BIOS/chipset drivers, run as administrator, and check the sensor name shown in settings. Some laptops do not expose CPU temperature to third-party software.
- **FPS is N/A:** focus the game, use borderless mode, and verify that anti-cheat policy permits PresentMon/ETW tools.
- **Overlay is hidden:** press `Ctrl+Shift+O` and check **Show overlay**.
- **Settings reset:** ensure `%LOCALAPPDATA%\PulseOverlay` is writable. Settings are stored there as JSON.

## Third-party components

- LibreHardwareMonitorLib 0.9.6: Mozilla Public License 2.0
- Intel PresentMon 2.4.1: MIT License
- .NET 9 runtime: Microsoft distribution terms
