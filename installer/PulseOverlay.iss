#define MyAppName "Pulse Overlay"
#define MyAppVersion "1.1.2"
#define MyAppExeName "PulseOverlay.exe"

[Setup]
AppId={{F876B540-4164-4B21-913E-EB24D9BFA5D4}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=Pulse Overlay
DefaultDirName={autopf}\Pulse Overlay
DefaultGroupName=Pulse Overlay
OutputDir=..\Deliverables
OutputBaseFilename=PulseOverlay-Installer
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupLogging=yes

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\Deliverables\Installed Version\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Pulse Overlay"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall Pulse Overlay"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Pulse Overlay"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch Pulse Overlay"; Flags: nowait postinstall skipifsilent
