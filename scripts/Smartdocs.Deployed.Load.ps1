<#
.SYNOPSIS
Applies a steady, repeatable load to a deployed SmartDocs instance.

.DESCRIPTION
Drives a fixed mix of navigation and page requests at a fixed rate for a fixed
duration, and reports the window it covered in UTC so platform metrics can be read
for exactly that window. Two runs either side of a configuration change make a
comparison; one run on its own measures nothing.

The rate is deliberately modest: the instance under measurement shares its plan.

.PARAMETER BaseUrl
Base address of the deployed instance.

.PARAMETER ArticleRoute
Route of an article used for the page requests.

.PARAMETER DurationMinutes
How long to sustain the load.

.PARAMETER RequestsPerSecond
Target rate across all endpoints.

.PARAMETER Label
Free text recorded with the result, naming what the run is measuring.

.EXAMPLE
.\scripts\Smartdocs.Deployed.Load.ps1 -BaseUrl 'https://example.azurewebsites.net' -Label 'sources listened to'
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BaseUrl,
    [string]$ArticleRoute = '00.00-getting-started',
    [ValidateRange(1, 30)][int]$DurationMinutes = 3,
    [ValidateRange(1, 20)][int]$RequestsPerSecond = 4,
    [string]$Label = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$BaseUrl = $BaseUrl.TrimEnd('/')
$route = $ArticleRoute.TrimStart('/')

# A mix that exercises the paths the analysis names: a level read, a folder record,
# a rendered page, and a prerendered page.
$targets = @(
    "$BaseUrl/_nav/children?prefix="
    "$BaseUrl/_nav/folder?prefix="
    "$BaseUrl/_page/$route"
    "$BaseUrl/$route"
)

$delayMs = [int](1000 / $RequestsPerSecond)
$startUtc = (Get-Date).ToUniversalTime()
$deadline = (Get-Date).AddMinutes($DurationMinutes)

Write-Host "Load: $RequestsPerSecond req/s for $DurationMinutes min against $BaseUrl"
if ($Label) { Write-Host "Label: $Label" }
Write-Host "Started (UTC): $($startUtc.ToString('yyyy-MM-ddTHH:mm:ssZ'))"

$ok = 0
$failed = 0
$times = [System.Collections.Generic.List[double]]::new()
$index = 0

while ((Get-Date) -lt $deadline) {
    $uri = $targets[$index % $targets.Count]
    $index++

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        Invoke-WebRequest -Uri $uri -UseBasicParsing -TimeoutSec 120 `
            -Headers @{ 'Accept-Encoding' = 'gzip, br' } | Out-Null
        $stopwatch.Stop()
        $times.Add($stopwatch.Elapsed.TotalMilliseconds)
        $ok++
    }
    catch {
        $stopwatch.Stop()
        $failed++
    }

    $remaining = $delayMs - $stopwatch.Elapsed.TotalMilliseconds
    if ($remaining -gt 0) { Start-Sleep -Milliseconds ([int]$remaining) }
}

$endUtc = (Get-Date).ToUniversalTime()
$sorted = $times | Sort-Object
$p50 = if ($sorted.Count) { [math]::Round($sorted[[int]($sorted.Count * 0.5)], 1) } else { $null }
$p95 = if ($sorted.Count) { [math]::Round($sorted[[math]::Min($sorted.Count - 1, [int]($sorted.Count * 0.95))], 1) } else { $null }

[pscustomobject]@{
    Label      = $Label
    StartUtc   = $startUtc.ToString('yyyy-MM-ddTHH:mm:ssZ')
    EndUtc     = $endUtc.ToString('yyyy-MM-ddTHH:mm:ssZ')
    Ok         = $ok
    Failed     = $failed
    P50Ms      = $p50
    P95Ms      = $p95
}
