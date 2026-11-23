<#
.SYNOPSIS
  Run the test projects with minimal output: a pass/fail summary line per run plus the failing tests' messages.
  Build warnings (hundreds of nullable/obsolete warnings) are hidden, which keeps the output small for humans and agents.

.EXAMPLES
  tools\test.ps1                                   # HandsLiftedApp.Tests then HandsLiftedApp.UiTests
  tools\test.ps1 -Project UiTests -Filter ThemePicker
  tools\test.ps1 -Project UiTests -NoBuild -Repeat 10    # flake hunting: build once, run 10 times
#>
param(
    [ValidateSet('Tests', 'UiTests', 'All')][string]$Project = 'All',
    [string]$Filter,
    [int]$Repeat = 1,
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$env:PATH = "$env:USERPROFILE\.dotnet;$env:PATH"
$projects = switch ($Project) { 'Tests' { @('HandsLiftedApp.Tests') } 'UiTests' { @('HandsLiftedApp.UiTests') } default { @('HandsLiftedApp.Tests', 'HandsLiftedApp.UiTests') } }

$failed = $false
foreach ($p in $projects) {
    foreach ($i in 1..$Repeat) {
        $args = @('test', "$repo\$p", '--nologo', '-v', 'q', '-clp:ErrorsOnly', '--logger', 'console;verbosity=minimal')
        if ($NoBuild -or $i -gt 1) { $args += '--no-build' }
        if ($Filter) { $args += @('--filter', "FullyQualifiedName~$Filter") }

        $out = & dotnet @args 2>&1 | ForEach-Object { "$_" }
        $exit = $LASTEXITCODE
        $label = if ($Repeat -gt 1) { "$p [run $i/$Repeat]" } else { $p }

        $summary = $out | Where-Object { $_ -match '^(Passed|Failed)!' }
        if ($summary) { Write-Host "$label : $summary" } else { Write-Host "$label : no test summary (no matching tests, or a build error)" }

        if ($exit -ne 0) {
            $failed = $true
            # Failing tests with their message, and any compiler errors; everything else is dropped.
            for ($n = 0; $n -lt $out.Count; $n++) {
                if ($out[$n] -match '^\s+Failed ' -or $out[$n] -match ': error ') {
                    $out[$n..([Math]::Min($n + 6, $out.Count - 1))] | ForEach-Object { Write-Host $_ }
                }
            }
        }
    }
}
if ($failed) { exit 1 }
