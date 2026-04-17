# init.ps1 — Initialize MAXsCursor project
# Run this once from the project root after copying SPEC.md and CLAUDE.md into place.
#
# Usage:
#   cd D:\Projects\MAXsCursor
#   .\init.ps1

$ErrorActionPreference = "Stop"

Write-Host "Checking .NET 8 SDK..." -ForegroundColor Cyan
$dotnetVersion = & dotnet --version 2>$null
if (-not $dotnetVersion -or -not ($dotnetVersion -match "^8\.")) {
    Write-Host "WARNING: .NET 8 SDK not found or not the active version." -ForegroundColor Yellow
    Write-Host "Install with: winget install Microsoft.DotNet.SDK.8"
    Write-Host "Current: $dotnetVersion"
    $go = Read-Host "Continue anyway? (y/N)"
    if ($go -ne "y") { exit 1 }
}

Write-Host "Creating solution..." -ForegroundColor Cyan
dotnet new sln -n MAXsCursor

Write-Host "Creating WPF project..." -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path "src\MAXsCursor" | Out-Null
Push-Location "src\MAXsCursor"
dotnet new wpf -n MAXsCursor --force
Pop-Location

Write-Host "Wiring solution..." -ForegroundColor Cyan
dotnet sln add src/MAXsCursor/MAXsCursor.csproj

Write-Host "Creating folder layout..." -ForegroundColor Cyan
$folders = @(
    "src\MAXsCursor\Core",
    "src\MAXsCursor\Overlay",
    "src\MAXsCursor\Settings",
    "src\MAXsCursor\Tray",
    "src\MAXsCursor\Interop"
)
foreach ($f in $folders) {
    New-Item -ItemType Directory -Force -Path $f | Out-Null
}

Write-Host "Writing .gitignore..." -ForegroundColor Cyan
@'
bin/
obj/
.vs/
.idea/
*.user
*.suo
*.log
[Dd]ebug/
[Rr]elease/
x64/
x86/
[Aa][Rr][Mm]/
[Aa][Rr][Mm]64/
artifacts/
*.pdb
TestResults/
'@ | Out-File -FilePath ".gitignore" -Encoding utf8 -Force

Write-Host "Writing app.manifest (DPI awareness)..." -ForegroundColor Cyan
@'
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
      <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true/pm</dpiAware>
    </windowsSettings>
    <compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1">
      <application>
        <supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}" />
        <supportedOS Id="{1f676c76-80e1-4239-95bb-83d0f6d0da78}" />
      </application>
    </compatibility>
  </application>
</assembly>
'@ | Out-File -FilePath "src\MAXsCursor\app.manifest" -Encoding utf8 -Force

Write-Host "Updating .csproj with manifest reference..." -ForegroundColor Cyan
$csprojPath = "src\MAXsCursor\MAXsCursor.csproj"
$csproj = Get-Content $csprojPath -Raw
if ($csproj -notmatch "ApplicationManifest") {
    $csproj = $csproj -replace "(<OutputType>WinExe</OutputType>)", "`$1`n    <ApplicationManifest>app.manifest</ApplicationManifest>`n    <Nullable>enable</Nullable>`n    <LangVersion>12</LangVersion>"
    $csproj | Out-File -FilePath $csprojPath -Encoding utf8 -Force
}

Write-Host "Verifying build..." -ForegroundColor Cyan
dotnet build -c Debug --nologo --verbosity quiet

Write-Host ""
Write-Host "Done." -ForegroundColor Green
Write-Host ""
Write-Host "Next step:"
Write-Host "  1. Open this folder in Claude Code"
Write-Host "  2. Paste the content of FIRST_PROMPT.md as your first message"
Write-Host "  3. Let Claude Code build step 1 of the work order"
