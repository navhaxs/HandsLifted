<#
.SYNOPSIS
  Launch / drive the Debug app through its DIY devtools HTTP service (HandsLiftedApp.DevTools).

.EXAMPLES
  tools\devtools.ps1 start                 # build Debug, launch with an EMPTY throw-away profile, wait until ready
  tools\devtools.ps1 start -RealData       # same, but against your real %APPDATA%\HandsLifted (careful: edits are real)
  tools\devtools.ps1 get  /tree?depth=6
  tools\devtools.ps1 post "/click?text=New Playlist"
  tools\devtools.ps1 shot                  # screenshot -> prints the PNG path
  tools\devtools.ps1 stop
#>
param(
    [Parameter(Mandatory, Position = 0)][ValidateSet('start', 'stop', 'get', 'post', 'shot')] [string]$Command,
    [Parameter(Position = 1)][string]$Path,
    [switch]$NoBuild,
    [switch]$RealData,
    [int]$Port = 9333
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$base = "http://127.0.0.1:$Port"
$info = Join-Path $env:TEMP 'handslifted-devtools\devtools.json'

function Invoke-DevTools([string]$Method, [string]$PathAndQuery) {
    # Query values with spaces are fine; encode just the unsafe bits the way a browser would.
    $uri = $base + ([uri]::EscapeUriString($PathAndQuery))
    if ($Method -eq 'POST') {
        Invoke-RestMethod -Method Post -Uri $uri -Headers @{ 'X-DevTools' = '1' } -Body ''
    } else {
        Invoke-RestMethod -Method Get -Uri $uri
    }
}

switch ($Command) {
    'start' {
        $env:PATH = "$env:USERPROFILE\.dotnet;$env:PATH"
        if (-not $NoBuild) {
            dotnet build "$repo\HandsLiftedApp.Desktop\HandsLiftedApp.Desktop.csproj" -nologo -v q | Out-Host
            if ($LASTEXITCODE -ne 0) { throw 'build failed' }
        }
        $env:HANDSLIFTED_DEVTOOLS_PORT = "$Port"
        if ($RealData) {
            Remove-Item Env:HANDSLIFTED_DATA_DIR -ErrorAction SilentlyContinue
        } else {
            $data = Join-Path $env:TEMP 'handslifted-devtools\profile'
            New-Item -ItemType Directory -Force $data | Out-Null
            $env:HANDSLIFTED_DATA_DIR = $data
        }
        $exe = "$repo\HandsLiftedApp.Desktop\bin\Debug\net10.0\HandsLiftedApp.Desktop.exe"
        Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe)
        foreach ($i in 1..60) {
            try { Invoke-DevTools GET '/state' | Out-Null; Write-Host "devtools ready on $base (profile: $(if ($RealData) { 'REAL' } else { $env:HANDSLIFTED_DATA_DIR }))"; return }
            catch { Start-Sleep -Milliseconds 500 }
        }
        throw "devtools did not come up on $base within 30s"
    }
    'stop' {
        if (Test-Path $info) {
            $procId = (Get-Content $info | ConvertFrom-Json).pid
            Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue
            Write-Host "stopped pid $procId"
        } else { Write-Host 'no devtools.json: nothing to stop' }
    }
    'get'  { Invoke-DevTools GET $Path }
    'post' { Invoke-DevTools POST $Path }
    'shot' { (Invoke-DevTools GET '/screenshot').path }
}
