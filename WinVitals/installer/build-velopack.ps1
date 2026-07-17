param(
    [string]$Version = "0.9.0",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$AppProj = Join-Path $Root "src\WinVitals.App\WinVitals.App.csproj"
$PublishDir = Join-Path $PSScriptRoot "publish"
$Output = Join-Path $PSScriptRoot "releases"

if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }

Write-Host "Publishing app..." -ForegroundColor Cyan
dotnet publish $AppProj `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:Version=$Version `
    -o $PublishDir

Write-Host "Building Velopack package..." -ForegroundColor Cyan
vpk pack `
    --packId WinVitals `
    --packVersion $Version `
    --packDir $PublishDir `
    --mainExe WinVitals.exe `
    --outputDir $Output `
    --icon "$Root\assets\icon.ico"

Write-Host "`nDone. Upload contents of $Output to GitHub release v$Version" -ForegroundColor Green
