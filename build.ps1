param([switch]$SkipInstaller)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root 'src\PulseOverlay.csproj'
$publish = Join-Path $root 'Deliverables\Installed Version'
$dotnet = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet-sdk\dotnet.exe'
$iscc = Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'

if (-not (Test-Path $dotnet)) { $dotnet = (Get-Command dotnet -ErrorAction Stop).Source }
if (-not $SkipInstaller -and -not (Test-Path $iscc)) { $iscc = (Get-Command iscc -ErrorAction Stop).Source }

New-Item -ItemType Directory -Force $publish | Out-Null
& $dotnet publish $project -c Release -r win-x64 --self-contained true -o $publish /p:PublishReadyToRun=true
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }
Copy-Item (Join-Path $root 'README.md') (Join-Path $publish 'README.md') -Force
if (-not $SkipInstaller) {
    & $iscc (Join-Path $root 'installer\PulseOverlay.iss')
    if ($LASTEXITCODE -ne 0) { throw 'installer compilation failed' }
}
