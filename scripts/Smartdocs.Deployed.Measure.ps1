<#
.SYNOPSIS
Measures a deployed SmartDocs instance from outside.

.DESCRIPTION
Records the figures M1-deployed-baseline asks for and that can be read without
acting on the instance: time to the first byte and the total time of a page, a
navigation level and a rendered page, the bytes each sends, and whether a repeat
request revalidates. It samples each endpoint and reports p50 and p95.

It measures; it changes nothing. A restart, a configuration change, or a publish is
the caller's to perform between two runs of this script.

.PARAMETER BaseUrl
Base address of the deployed instance.

.PARAMETER ArticleRoute
Route of an article used for the page probes.

.PARAMETER Samples
Requests per endpoint.

.PARAMETER Label
Free text recorded with the result, naming what the run is measuring.

.EXAMPLE
.\scripts\Smartdocs.Deployed.Measure.ps1 -BaseUrl 'https://example.azurewebsites.net' -Label 'waves 1-3, always on'
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BaseUrl,
    [string]$ArticleRoute = '',
    [ValidateRange(1, 200)][int]$Samples = 10,
    [string]$Label = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$BaseUrl = $BaseUrl.TrimEnd('/')

function Get-Percentile {
    param([double[]]$Values, [double]$Percentile)

    if ($Values.Count -eq 0) { return $null }
    $sorted = $Values | Sort-Object
    $index = [math]::Ceiling(($Percentile / 100) * $sorted.Count) - 1
    $index = [math]::Max(0, [math]::Min($sorted.Count - 1, $index))
    [math]::Round($sorted[$index], 1)
}

function Measure-Endpoint {
    param([string]$Name, [string]$RequestUri, [int]$Count)

    $times = @()
    $bytes = $null
    $ok = 0
    $failed = 0
    $lastError = $null
    $etag = $null

    for ($i = 0; $i -lt $Count; $i++) {
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        try {
            $response = Invoke-WebRequest -Uri $RequestUri -UseBasicParsing -TimeoutSec 180 `
                -Headers @{ 'Accept-Encoding' = 'gzip, br' }
            $stopwatch.Stop()
            $times += $stopwatch.Elapsed.TotalMilliseconds
            $bytes = $response.RawContentLength
            $ok++
            if ($response.Headers.ContainsKey('ETag')) { $etag = [string]$response.Headers['ETag'] }
        }
        catch {
            $stopwatch.Stop()
            $failed++
            $lastError = $_.Exception.Message
        }
    }

    # One conditional request, to establish whether a repeat view revalidates.
    $revalidated = $null
    if ($etag) {
        try {
            $conditional = Invoke-WebRequest -Uri $RequestUri -UseBasicParsing -TimeoutSec 180 `
                -Headers @{ 'If-None-Match' = $etag } -SkipHttpErrorCheck
            $revalidated = ($conditional.StatusCode -eq 304)
        }
        catch { $revalidated = $false }
    }

    if ($failed -gt 0) {
        Write-Warning "$Name : $failed of $Count failed. Last error: $lastError"
    }

    [pscustomobject]@{
        Endpoint    = $Name
        Ok          = $ok
        Failed      = $failed
        Bytes       = $bytes
        P50Ms       = Get-Percentile -Values $times -Percentile 50
        P95Ms       = Get-Percentile -Values $times -Percentile 95
        HasETag     = [bool]$etag
        Revalidates = $revalidated
    }
}

Write-Host "Measuring $BaseUrl ($Samples samples per endpoint)"
if ($Label) { Write-Host "Label: $Label" }

$results = @(
    Measure-Endpoint -Name 'home page' -RequestUri "$BaseUrl/" -Count $Samples
    Measure-Endpoint -Name '/_nav/children (root)' -RequestUri "$BaseUrl/_nav/children?prefix=" -Count $Samples
    Measure-Endpoint -Name '/_nav/folder (root)' -RequestUri "$BaseUrl/_nav/folder?prefix=" -Count $Samples
)

if ($ArticleRoute) {
    $route = $ArticleRoute.TrimStart('/')
    $results += Measure-Endpoint -Name 'article page' -RequestUri "$BaseUrl/$route" -Count $Samples
    $results += Measure-Endpoint -Name '/_page' -RequestUri "$BaseUrl/_page/$route" -Count $Samples
}

$results | Format-Table -AutoSize
$results
