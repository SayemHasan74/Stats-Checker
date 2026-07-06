# Pulse Overlay

Pulse Overlay is a lightweight, configurable Windows performance overlay. It renders one click-through horizontal line and lets you independently enable FPS, clock, CPU/GPU use and temperature, RAM/VRAM, disk, network `↓`/`↑` speed, clocks, and power.

## Ready-to-run files

- `Deliverables\Installed Version\PulseOverlay.exe`: portable/self-contained build. Open this EXE and approve the Windows administrator prompt.
- `Deliverables\PulseOverlay-Installer.exe`: installer for another Windows PC.

The installed version includes the .NET runtime, LibreHardwareMonitor dependencies, and Intel PresentMon. No separate runtime package is required.

## Usage

1. Launch `PulseOverlay.exe` and approve administrator access. Sensor-driver access and ETW FPS collection require it.
2. Enable only the metrics you want.
3. Choose a corner, edge spacing, font size, background opacity, and overlay color. **Flush to corner** removes the panel inset so the text itself reaches the selected screen edges; **Comfortable padding** keeps the original spacing.
4. Select **Save changes**. Closing the settings window leaves the overlay in the notification area.
5. Press `Ctrl+Shift+O` or use the tray menu to toggle the overlay.

FPS follows the foreground application. Borderless-windowed mode is recommended because exclusive-fullscreen and some anti-cheat systems can prevent independent overlays or ETW capture. Pulse Overlay does not inject DLLs into games.

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
Set-Location 'E:\Codes\PC Performance Overlay\PulseOverlay-GitHub'
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

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
