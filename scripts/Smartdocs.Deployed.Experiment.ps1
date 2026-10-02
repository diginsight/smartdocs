<#
.SYNOPSIS
Runs one configuration of the instrumentation-cost experiment against a deployed instance.

.DESCRIPTION
Applies a named set of app settings, waits for the restart and for the startup
warm-up to settle, measures an idle CPU window with no load, then applies a steady
load and reports both windows in UTC.

The idle window exists because the startup warm-up competes for the same core and
runs longer when instrumented: without subtracting it, background CPU is attributed
to requests, which overstates the cost per request.

Each configuration must be run through this script rather than by hand, so that the
settle, the idle window and the load are identical across configurations.

.PARAMETER AppName
Name of the App Service web app.

.PARAMETER ResourceGroup
Resource group holding the web app.

.PARAMETER BaseUrl
Base address of the deployed instance.

.PARAMETER Settings
App settings to apply, as 'KEY=VALUE' strings. Pass an empty array for the base
configuration.

.PARAMETER RemoveSettings
App setting names to remove before applying Settings, so each run starts from a
known state.

.PARAMETER Label
Name of the configuration being measured.

.PARAMETER SettleMinutes
Minutes to wait after the restart before measuring, so the warm-up can finish.

.PARAMETER LoadMinutes
Minutes of steady load.

.EXAMPLE
.\scripts\Smartdocs.Deployed.Experiment.ps1 -AppName app -ResourceGroup rg -BaseUrl https://host -Label 'base' -RemoveSettings @('X') -Settings @()
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$AppName,
    [Parameter(Mandatory)][string]$ResourceGroup,
    [Parameter(Mandatory)][string]$BaseUrl,
    [Parameter(Mandatory)][string]$Label,
    [string[]]$Settings = @(),
    [string[]]$RemoveSettings = @(),
    [ValidateRange(1, 20)][int]$SettleMinutes = 4,
    [ValidateRange(1, 20)][int]$LoadMinutes = 3,
    [ValidateRange(1, 20)][int]$RequestsPerSecond = 4,
    [string]$ArticleRoute = '00.00-getting-started'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$BaseUrl = $BaseUrl.TrimEnd('/')
Write-Host "=== $Label ===" -ForegroundColor Cyan

if ($RemoveSettings.Count -gt 0) {
    Write-Host "Removing: $($RemoveSettings -join ', ')"
    az webapp config appsettings delete --name $AppName --resource-group $ResourceGroup `
        --setting-names @RemoveSettings -o none 2>$null
}

if ($Settings.Count -gt 0) {
    Write-Host "Applying: $($Settings -join ', ')"
    az webapp config appsettings set --name $AppName --resource-group $ResourceGroup `
        --settings @Settings -o none 2>$null
}
else {
    # No settings to apply: restart anyway, so every configuration starts from a restart.
    Write-Host 'No settings to apply; restarting to match the other runs.'
    az webapp restart --name $AppName --resource-group $ResourceGroup -o none 2>$null
}

Write-Host "Settling for $SettleMinutes min (restart + warm-up)..."
Start-Sleep -Seconds 30
# One request to force the app to load, then leave it alone so the warm-up can finish.
for ($i = 0; $i -lt 20; $i++) {
    try { Invoke-WebRequest "$BaseUrl/" -UseBasicParsing -TimeoutSec 180 | Out-Null; break }
    catch { Start-Sleep -Seconds 5 }
}
Start-Sleep -Seconds ($SettleMinutes * 60)

$idleStart = (Get-Date).ToUniversalTime()
Write-Host "Idle window: 2 min with no requests from here..."
Start-Sleep -Seconds 120
$idleEnd = (Get-Date).ToUniversalTime()

Write-Host "Load: $RequestsPerSecond req/s for $LoadMinutes min..."
$load = & "$PSScriptRoot\Smartdocs.Deployed.Load.ps1" -BaseUrl $BaseUrl `
    -ArticleRoute $ArticleRoute -DurationMinutes $LoadMinutes `
    -RequestsPerSecond $RequestsPerSecond -Label $Label

[pscustomobject]@{
    Label        = $Label
    IdleStartUtc = $idleStart.ToString('yyyy-MM-ddTHH:mm:ssZ')
    IdleEndUtc   = $idleEnd.ToString('yyyy-MM-ddTHH:mm:ssZ')
    LoadStartUtc = $load.StartUtc
    LoadEndUtc   = $load.EndUtc
    Ok           = $load.Ok
    Failed       = $load.Failed
    P50Ms        = $load.P50Ms
    P95Ms        = $load.P95Ms
}
