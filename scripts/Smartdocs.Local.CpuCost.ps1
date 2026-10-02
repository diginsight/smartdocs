<#
.SYNOPSIS
Measures the marginal CPU a request costs the local host, under a named configuration.

.DESCRIPTION
Starts the host in a visible console, waits until the startup warm-up has gone quiet,
then reads the server process's own CPU time across an idle window and across a load
window. The marginal cost of a request is the difference, divided by the number of
requests — not the loaded window's CPU divided by requests, which attributes
background work to the request path.

This exists because the same comparison on a deployed App Service instance could not
separate the two: on a shared B1 core the startup walk was still burning 45-51
CPU-seconds per minute during a window that served three requests, so any
CPU-per-request figure taken there measured the walk, not the request.

Process CPU comes from Process.TotalProcessorTime, which counts only this process and
has no neighbours on the machine competing for attribution.

.PARAMETER EnvironmentName
Value of AppsettingsEnvironmentName, selecting the overlay that declares the space.

.PARAMETER Url
Address to bind. Use a port no other instance holds.

.PARAMETER EnvironmentOverrides
Extra environment variables for the host, as 'KEY=VALUE' strings — the local
equivalent of App Service app settings.

.PARAMETER Label
Name of the configuration being measured.

.PARAMETER QuietSeconds
How long the process CPU must stay below QuietCpuRatio before the warm-up counts as
finished.

.PARAMETER QuietCpuRatio
Fraction of one core below which the process counts as quiet.

.PARAMETER Requests
Requests to issue in the load window.

.EXAMPLE
.\scripts\Smartdocs.Local.CpuCost.ps1 -Label 'activities on' -EnvironmentOverrides @()
#>

[CmdletBinding()]
param(
    [string]$EnvironmentName = 'Development',
    [string]$Url = 'http://localhost:5292',
    [string[]]$EnvironmentOverrides = @(),
    [Parameter(Mandatory)][string]$Label,
    [ValidateRange(5, 300)][int]$QuietSeconds = 20,
    [ValidateRange(0.01, 1.0)][double]$QuietCpuRatio = 0.05,
    [ValidateRange(10, 5000)][int]$Requests = 400,
    [ValidateRange(10, 600)][int]$IdleSeconds = 30,
    [ValidateRange(10, 1200)][int]$MaxWarmupSeconds = 600,
    [string]$ArticleRoute = '03.00-architecture'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectDirectory = Join-Path $repositoryRoot 'src\Diginsight.SmartDocs.Web'
$assemblyPath = Join-Path $projectDirectory 'bin\Release\net10.0\Diginsight.SmartDocs.Web.dll'

if (-not (Test-Path -LiteralPath $assemblyPath)) {
    throw "'$assemblyPath' not found. Build the host in Release first."
}

Write-Host "=== $Label ===" -ForegroundColor Cyan

$assignments = @(
    "ASPNETCORE_ENVIRONMENT=Development"
    "AppsettingsEnvironmentName=$EnvironmentName"
    "ASPNETCORE_URLS=$Url"
) + $EnvironmentOverrides

$prelude = ($assignments | ForEach-Object {
        $name = $_.Split('=', 2)[0]
        $value = $_.Split('=', 2)[1]
        # SetEnvironmentVariable, not `$env:X`, because these names contain dots and asterisks.
        "[Environment]::SetEnvironmentVariable('$name','$value');"
    }) -join ' '
$inner = "$prelude Set-Location '$projectDirectory'; Write-Host 'CPU cost run: $Label. Ctrl+C to stop.'; dotnet `"$assemblyPath`""

$console = Start-Process -FilePath 'powershell.exe' `
    -ArgumentList '-NoExit', '-NoProfile', '-Command', $inner `
    -WorkingDirectory $projectDirectory -WindowStyle Normal -PassThru

function Get-HostProcess {
    $child = Get-CimInstance -ClassName Win32_Process -Filter "ParentProcessId = $($console.Id)" |
        Where-Object { $_.Name -ieq 'dotnet.exe' } | Select-Object -First 1
    if ($child) { Get-Process -Id $child.ProcessId -ErrorAction SilentlyContinue } else { $null }
}

try {
    $deadline = (Get-Date).AddSeconds($MaxWarmupSeconds)
    $server = $null
    while ((Get-Date) -lt $deadline -and -not $server) {
        try { Invoke-WebRequest "$Url/_nav/version" -UseBasicParsing -TimeoutSec 10 | Out-Null; $server = Get-HostProcess }
        catch { Start-Sleep -Milliseconds 500 }
    }
    if (-not $server) { throw 'The host did not start.' }

    # One request, so the warm-up starts, then wait for the process to go quiet.
    Invoke-WebRequest "$Url/" -UseBasicParsing -TimeoutSec 300 | Out-Null

    Write-Host 'Waiting for the warm-up to go quiet...'
    $quietSince = $null
    while ((Get-Date) -lt $deadline) {
        $server.Refresh()
        $before = $server.TotalProcessorTime
        Start-Sleep -Seconds 5
        $server.Refresh()
        $ratio = ($server.TotalProcessorTime - $before).TotalSeconds / 5.0
        if ($ratio -lt $QuietCpuRatio) {
            if (-not $quietSince) { $quietSince = Get-Date }
            if (((Get-Date) - $quietSince).TotalSeconds -ge $QuietSeconds) { break }
        }
        else { $quietSince = $null }
    }
    if (-not $quietSince) { Write-Warning 'The warm-up never went quiet; figures include background work.' }

    # Idle window: no requests at all.
    $server.Refresh()
    $idleCpuBefore = $server.TotalProcessorTime
    Start-Sleep -Seconds $IdleSeconds
    $server.Refresh()
    $idleCpu = ($server.TotalProcessorTime - $idleCpuBefore).TotalSeconds

    # Load window: a fixed number of requests, as fast as they complete.
    $targets = @("$Url/_nav/children?prefix=", "$Url/_nav/folder?prefix=", "$Url/_page/$ArticleRoute", "$Url/$ArticleRoute")
    $server.Refresh()
    $loadCpuBefore = $server.TotalProcessorTime
    $wall = [System.Diagnostics.Stopwatch]::StartNew()
    $times = [System.Collections.Generic.List[double]]::new()
    $failed = 0
    for ($i = 0; $i -lt $Requests; $i++) {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        try { Invoke-WebRequest $targets[$i % $targets.Count] -UseBasicParsing -TimeoutSec 180 | Out-Null; $sw.Stop(); $times.Add($sw.Elapsed.TotalMilliseconds) }
        catch { $sw.Stop(); $failed++ }
    }
    $wall.Stop()
    $server.Refresh()
    $loadCpu = ($server.TotalProcessorTime - $loadCpuBefore).TotalSeconds

    $idlePerSecond = $idleCpu / $IdleSeconds
    $backgroundDuringLoad = $idlePerSecond * $wall.Elapsed.TotalSeconds
    $marginal = ($loadCpu - $backgroundDuringLoad) / [math]::Max(1, $Requests - $failed)
    $sorted = $times | Sort-Object

    [pscustomobject]@{
        Label             = $Label
        IdleCpuPerSec     = [math]::Round($idlePerSecond, 4)
        LoadWallSec       = [math]::Round($wall.Elapsed.TotalSeconds, 1)
        LoadCpuSec        = [math]::Round($loadCpu, 2)
        BackgroundCpuSec  = [math]::Round($backgroundDuringLoad, 2)
        Requests          = $Requests - $failed
        Failed            = $failed
        CpuPerRequestMs   = [math]::Round($marginal * 1000, 2)
        P50Ms             = if ($sorted.Count) { [math]::Round($sorted[[int]($sorted.Count * 0.5)], 1) } else { $null }
        P95Ms             = if ($sorted.Count) { [math]::Round($sorted[[math]::Min($sorted.Count - 1, [int]($sorted.Count * 0.95))], 1) } else { $null }
    }
}
finally {
    foreach ($c in (Get-CimInstance -ClassName Win32_Process -Filter "ParentProcessId = $($console.Id)")) {
        Stop-Process -Id $c.ProcessId -Force -ErrorAction SilentlyContinue
    }
    Stop-Process -Id $console.Id -Force -ErrorAction SilentlyContinue
}
