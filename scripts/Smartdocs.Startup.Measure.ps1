<#
.SYNOPSIS
Measures startup and navigation cost of the local SmartDocs host against one content set.

.DESCRIPTION
Runs the host in a visible console window, then records the figures the acceptance
criteria of
src/docs/90.00-issues/202610/20261001.01-perfanalysis/01-startup-and-navigation-optimization.analysis.md
are stated in: time to listening, time to the first page, bytes of the first page,
the p50 and p95 of /_nav/children and /_page, and the server's working set once the
run has settled.

It measures; it does not judge. The finding under test is growth with the corpus, so
run it against at least two tree sizes and compare — a single run says nothing about
whether a cost is proportional to the corpus.

The host runs in a visible window on purpose, so the run can be watched and stopped.

.PARAMETER EnvironmentName
Value of AppsettingsEnvironmentName, which selects the overlay that declares the
space. 'scale' serves the generated tree; 'Development' serves this repository's docs.

.PARAMETER Url
Base address of the host under measurement.

.PARAMETER Samples
Requests per endpoint in the warm phase.

.PARAMETER SettleSeconds
Seconds to wait after the first page before sampling the warm phase, so background
discovery has a bounded chance to run.

.PARAMETER TimeoutSeconds
How long to wait for the host to start answering before giving up.

.PARAMETER SkipBuild
Measure the existing build output instead of rebuilding first.

.PARAMETER KeepRunning
Leave the host running after the measurement.

.EXAMPLE
.\scripts\Smartdocs.Startup.Measure.ps1 -EnvironmentName scale

.EXAMPLE
.\scripts\Smartdocs.Startup.Measure.ps1 -EnvironmentName Development -Samples 50
#>

[CmdletBinding()]
param(
    [string]$EnvironmentName = 'scale',
    [string]$Url = 'http://localhost:5280',
    [ValidateRange(1, 500)][int]$Samples = 20,
    [ValidateRange(0, 600)][int]$SettleSeconds = 20,
    [ValidateRange(10, 3600)][int]$TimeoutSeconds = 600,
    [switch]$SkipBuild,
    [switch]$KeepRunning
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectDirectory = Join-Path $repositoryRoot 'src\Diginsight.SmartDocs.Web'
$projectPath = Join-Path $projectDirectory 'Diginsight.SmartDocs.Web.csproj'
$assemblyPath = Join-Path $projectDirectory 'bin\Release\net10.0\Diginsight.SmartDocs.Web.dll'

if (-not $SkipBuild) {
    Write-Host 'Building the host (Release)...'
    & dotnet build $projectPath -c Release -v q --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE."
    }
}

if (-not (Test-Path -LiteralPath $assemblyPath)) {
    throw "'$assemblyPath' not found. Run without -SkipBuild."
}

function Measure-Request {
    param([string]$RequestUri)

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $response = Invoke-WebRequest -Uri $RequestUri -UseBasicParsing -TimeoutSec 300
    $stopwatch.Stop()

    [pscustomobject]@{
        Milliseconds = $stopwatch.Elapsed.TotalMilliseconds
        Bytes        = $response.RawContentLength
        StatusCode   = $response.StatusCode
    }
}

function Get-Percentile {
    param([double[]]$Values, [double]$Percentile)

    if ($Values.Count -eq 0) { return $null }
    $sorted = $Values | Sort-Object
    $index = [math]::Ceiling(($Percentile / 100) * $sorted.Count) - 1
    $index = [math]::Max(0, [math]::Min($sorted.Count - 1, $index))
    [math]::Round($sorted[$index], 1)
}

$environmentAssignments = @(
    "ASPNETCORE_ENVIRONMENT=Development"
    "AppsettingsEnvironmentName=$EnvironmentName"
    "ASPNETCORE_URLS=$Url"
)

# A visible console window, so the run can be watched and interrupted. The working directory is
# the project folder, because a space's RootPath is relative and resolves against it — the same
# directory launchSettings.json runs the host from.
$innerCommand = ($environmentAssignments | ForEach-Object { "`$env:$($_.Split('=')[0])='$($_.Split('=',2)[1])';" }) -join ' '
$innerCommand += " Set-Location '$projectDirectory';"
$innerCommand += " Write-Host 'SmartDocs host under measurement ($EnvironmentName). Ctrl+C to stop.'; dotnet `"$assemblyPath`""

Write-Host "Starting the host: environment '$EnvironmentName' at $Url"
$startedAt = Get-Date
$process = Start-Process -FilePath 'powershell.exe' `
    -ArgumentList '-NoExit', '-NoProfile', '-Command', $innerCommand `
    -WorkingDirectory $projectDirectory -WindowStyle Normal -PassThru

try {
    Write-Host 'Waiting for the host to answer...'
    $listeningMs = $null
    $deadline = $startedAt.AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $probe = Invoke-WebRequest -Uri "$Url/_nav/version" -UseBasicParsing -TimeoutSec 10
            if ($probe.StatusCode -ge 200) {
                $listeningMs = ((Get-Date) - $startedAt).TotalMilliseconds
                break
            }
        }
        catch {
            Start-Sleep -Milliseconds 250
        }
    }

    if ($null -eq $listeningMs) {
        throw "The host did not answer within $TimeoutSeconds seconds."
    }

    Write-Host ("Listening after {0:N0} ms" -f $listeningMs)

    $firstPage = Measure-Request -RequestUri "$Url/"
    Write-Host ("First page: {0:N0} ms, {1:N0} bytes" -f $firstPage.Milliseconds, $firstPage.Bytes)

    if ($SettleSeconds -gt 0) {
        Write-Host "Settling for $SettleSeconds s..."
        Start-Sleep -Seconds $SettleSeconds
    }

    Write-Host "Sampling $Samples requests per endpoint..."
    $rootLevel = @()
    $pageTimes = @()
    for ($i = 0; $i -lt $Samples; $i++) {
        $rootLevel += (Measure-Request -RequestUri "$Url/_nav/children?prefix=").Milliseconds
        $pageTimes += (Measure-Request -RequestUri "$Url/").Milliseconds
    }

    # The host runs as a child of the visible console, so the working set belongs to the
    # dotnet process that console started, not to the console itself.
    $hostProcess = Get-CimInstance -ClassName Win32_Process -Filter "ParentProcessId = $($process.Id)" |
        Where-Object { $_.Name -ieq 'dotnet.exe' } |
        Select-Object -First 1
    $workingSetMb = if ($hostProcess) {
        [math]::Round((Get-Process -Id $hostProcess.ProcessId).WorkingSet64 / 1MB, 1)
    }
    else { $null }

    $result = [pscustomobject]@{
        Environment        = $EnvironmentName
        ListeningMs        = [math]::Round($listeningMs, 0)
        FirstPageMs        = [math]::Round($firstPage.Milliseconds, 0)
        FirstPageBytes     = $firstPage.Bytes
        NavChildrenP50Ms   = Get-Percentile -Values $rootLevel -Percentile 50
        NavChildrenP95Ms   = Get-Percentile -Values $rootLevel -Percentile 95
        PageP50Ms          = Get-Percentile -Values $pageTimes -Percentile 50
        PageP95Ms          = Get-Percentile -Values $pageTimes -Percentile 95
        WorkingSetMb       = $workingSetMb
        SampledAt          = (Get-Date).ToString('o')
    }

    Write-Host ''
    $result | Format-List
    $result
}
finally {
    if (-not $KeepRunning) {
        $children = Get-CimInstance -ClassName Win32_Process -Filter "ParentProcessId = $($process.Id)"
        foreach ($child in $children) {
            Stop-Process -Id $child.ProcessId -Force -ErrorAction SilentlyContinue
        }
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        Write-Host 'Host stopped.'
    }
    else {
        Write-Host "Host left running in its console window (PID $($process.Id))."
    }
}
