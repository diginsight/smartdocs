<#
.SYNOPSIS
Stops all local Diginsight SmartDocs web application instances.

.DESCRIPTION
Stops the SmartDocs web host executable and dotnet run/watch processes that
reference the Diginsight.SmartDocs.Web project or assembly. Other .NET
processes are not affected.

.EXAMPLE
.\scripts\Smartdocs.Instances.Kill.ps1

.EXAMPLE
.\scripts\Smartdocs.Instances.Kill.ps1 -WhatIf
#>

[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'Medium')]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$applicationName = 'Diginsight.SmartDocs.Web'

function Get-SmartDocsProcess {
    Get-CimInstance -ClassName Win32_Process |
        Where-Object {
            if ($_.ProcessId -eq $PID) {
                return $false
            }

            $processName = [System.IO.Path]::GetFileNameWithoutExtension($_.Name)
            if ($processName -ieq $applicationName) {
                return $true
            }

            if ($_.Name -ine 'dotnet.exe' -or [string]::IsNullOrWhiteSpace($_.CommandLine)) {
                return $false
            }

            $commandLine = $_.CommandLine
            $isRunOrWatchHost = $commandLine -match '(?i)(?:^|\s)(?:run|watch)(?:\s|$)'
            $isManagedApplicationHost = $commandLine -match "(?i)$([regex]::Escape($applicationName))\.dll(?:`"|'\s|$)"

            return ($isRunOrWatchHost -or $isManagedApplicationHost) -and
                $commandLine.Contains($applicationName, [System.StringComparison]::OrdinalIgnoreCase)
        }
}

$processes = @(Get-SmartDocsProcess | Sort-Object @{
        Expression = { if ($_.Name -ieq 'dotnet.exe') { 0 } else { 1 } }
    }, ProcessId)

if ($processes.Count -eq 0) {
    Write-Host 'No local SmartDocs application instances are running.'
    return
}

$stoppedProcessIds = [System.Collections.Generic.List[uint32]]::new()

foreach ($process in $processes) {
    $target = "$($process.Name) (PID $($process.ProcessId))"
    if (-not $PSCmdlet.ShouldProcess($target, 'Stop local SmartDocs application instance')) {
        continue
    }

    try {
        Stop-Process -Id $process.ProcessId -Force -ErrorAction Stop
        $stoppedProcessIds.Add($process.ProcessId)
        Write-Host "Stopped $target."
    }
    catch {
        $stillRunning = Get-Process -Id $process.ProcessId -ErrorAction SilentlyContinue
        if ($null -ne $stillRunning) {
            throw
        }

        Write-Host "$target had already stopped."
    }
}

if ($WhatIfPreference) {
    return
}

Start-Sleep -Milliseconds 250
$remainingProcesses = @(Get-SmartDocsProcess)

if ($remainingProcesses.Count -gt 0) {
    $remaining = $remainingProcesses |
        ForEach-Object { "$($_.Name) (PID $($_.ProcessId))" }
    throw "Failed to stop all local SmartDocs application instances: $($remaining -join ', ')."
}

Write-Host "Stopped $($stoppedProcessIds.Count) local SmartDocs application instance(s)."
