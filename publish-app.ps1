#!/usr/bin/env pwsh
# Publish HandsLiftedApp.Desktop (self-contained single-file win-x64) together with
# HandsLiftedApp.Importer.PowerPointInteropHost (self-contained, multi-file — the helper
# exe needs its companion .dll/.deps.json/.runtimeconfig.json and NetOffice/protobuf-net
# dependencies sitting next to it) into one deployable output folder.
#
# Usage: ./publish-app.ps1 [-Configuration Release] [-Rid win-x64]

param(
    [string]$Configuration = "Release",
    [string]$Rid = "win-x64"
)

$ErrorActionPreference = "Stop"

$desktopProj = "HandsLiftedApp.Desktop/HandsLiftedApp.Desktop.csproj"
$interopProj = "HandsLiftedApp.Importer.PowerPointInteropHost/HandsLiftedApp.Importer.PowerPointInteropHost.csproj"

$desktopOut = "HandsLiftedApp.Desktop/bin/$Configuration/net8.0/publish"
$interopOut = "$desktopOut/PowerPointInteropHost"

if (Test-Path $desktopOut) {
    Write-Host "Cleaning previous publish output: $desktopOut"
    Remove-Item -Recurse -Force $desktopOut
}

Write-Host "Publishing PowerPointInteropHost ($Rid, self-contained)..."
dotnet publish $interopProj `
    -c $Configuration `
    -r $Rid `
    --self-contained true `
    -p:PublishSingleFile=false `
    -o $interopOut
if ($LASTEXITCODE -ne 0) { throw "Publish failed: PowerPointInteropHost" }

Write-Host "Publishing HandsLiftedApp.Desktop ($Rid, self-contained, single-file)..."
dotnet publish $desktopProj `
    -c $Configuration `
    -r $Rid `
    --self-contained true `
    -p:PublishSingleFile=true `
    -o $desktopOut
if ($LASTEXITCODE -ne 0) { throw "Publish failed: HandsLiftedApp.Desktop" }

Write-Host ""
Write-Host "Done. Deployable app folder: $desktopOut"
Write-Host "  Main app:   $desktopOut/HandsLiftedApp.Desktop.exe"
Write-Host "  PPT helper: $interopOut/HandsLiftedApp.Importer.PowerPointInteropHost.exe"
