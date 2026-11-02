#Requires -Version 7.2
<#
.SYNOPSIS
    Publishes a local content folder to the Blob Storage container of one SmartDocs space.

.DESCRIPTION
    Mirrors a local Markdown content folder (for example, the internal Learning Hub synced from
    SharePoint/OneDrive) into the Blob container that a deployed SmartDocs host reads for one space.

    The destination is never configured in this script. It is read from the space's own
    Source/Blob settings in appsettings.<Environment>.json, looked up first in the private
    smartdocs.internal peer and then in this repository. Publishing therefore can't target a
    container the site doesn't read, and no account, container or App Service name lives in this
    public repository.

    The run is incremental and safe to repeat:
      1. Stage: walk the source, applying the host's navigation exclusions plus OS, Office and
         tooling clutter. Files above -MaxFileSizeMB are skipped and reported, because the host
         buffers a whole blob in memory to serve it.
      2. Compare: list the container and upload only new or changed files (Content-MD5).
      3. Upload: the root access.metadata.yml goes first, so new content is never published under
         an older access policy. Uploads are authenticated with your Azure CLI sign-in (Entra ID);
         no storage keys or SAS tokens are used.
      4. Prune: only after every upload succeeded, delete blobs that no longer exist locally, then
         empty directories. A failed prune leaves the site complete rather than gapped.
      5. Invalidate: POST /_nav/invalidate so readers see the change immediately.

    Use -WhatIf to see the plan without writing anything.

.PARAMETER Environment
    Deployment environment whose overlay names the destination, for example Testmc. Resolves
    appsettings.<Environment>.json.

.PARAMETER SpaceId
    Site:Spaces[].Id to publish. Must be a Blob-sourced space in the environment overlay.

.PARAMETER SourcePath
    Local content root. When omitted, it is the space's FileSystem:RootPath in the
    -SourceEnvironment overlays (public half first, then the private half, as configuration
    binds them).

.PARAMETER SourceEnvironment
    Local profile used to resolve -SourcePath when it isn't given. Defaults to devlearn.

.PARAMETER InternalFolder
    Root of the private smartdocs.internal checkout. Defaults to $env:SMARTDOCS_INTERNAL_FOLDER,
    then ..\smartdocs.internal next to this repository.

.PARAMETER MaxFileSizeMB
    Files larger than this are not uploaded and are reported. 0 disables the limit.

.PARAMETER ExcludePath
    Extra content-relative paths to skip, with forward slashes and -like wildcards, for example
    'Shared Documents' or '00.00-draft/*'. A folder name excludes the whole folder.

.PARAMETER SkipPrune
    Upload only; leave blobs that no longer exist locally.

.PARAMETER Force
    Allow a prune that would delete more than half of the container's files.

.PARAMETER SkipInvalidate
    Don't call the host's navigation invalidation endpoint.

.PARAMETER SiteUrl
    Base URL of the host to invalidate. Defaults to the App Service named by Deployment:WebAppName.

.PARAMETER InvalidateKey
    X-Invalidate-Key value. Defaults to $env:SMARTDOCS_INVALIDATE_KEY, then the App Service's
    Site__InvalidateApiKey setting. Never printed.

.PARAMETER TenantId
    Entra tenant to request the storage token from, when your Azure CLI default tenant differs.

.PARAMETER ThrottleLimit
    Parallel uploads.

.EXAMPLE
    .\devops\scripts\Publish-SpaceContent.ps1 -Environment Testmc -WhatIf

    Shows what would be uploaded to and pruned from the internal Learning Hub container.

.EXAMPLE
    .\devops\scripts\Publish-SpaceContent.ps1 -Environment Testmc

    Publishes the internal Learning Hub folder configured for devlearn to the Testmc container.

.EXAMPLE
    .\devops\scripts\Publish-SpaceContent.ps1 -Environment Testmc -SpaceId learn -SourcePath C:\src\Learn

    Publishes another space from an explicit folder.

.NOTES
    Prerequisites: Azure CLI signed in (az login) with Storage Blob Data Contributor on the
    storage account or container, and network reach to its blob endpoint (for a private endpoint,
    the VPN or private DNS that resolves it).
#>
[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'Medium')]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]*$')]
    [string]$Environment,

    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]*$')]
    [string]$SpaceId = 'learn-internal',

    [string]$SourcePath,

    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]*$')]
    [string]$SourceEnvironment = 'devlearn',

    [string]$InternalFolder,

    [ValidateRange(0, 4096)]
    [int]$MaxFileSizeMB = 100,

    [string[]]$ExcludePath = @(),

    [switch]$SkipPrune,

    [switch]$Force,

    [switch]$SkipInvalidate,

    [string]$SiteUrl,

    [string]$InvalidateKey,

    [string]$TenantId,

    [ValidateRange(1, 32)]
    [int]$ThrottleLimit = 8
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
# The Azure CLI's bundled Python prints a harmless cryptography warning on every call.
$env:PYTHONWARNINGS = 'ignore'

$StorageApiVersion = '2023-11-03'
$AccessFileName = 'access.metadata.yml'
$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$WebProjectRelative = 'src\Diginsight.SmartDocs.Web'

# Mirrors DynamicNavBuilder.RootInfra / BuildOutput and NavRules.IsExcludedName: what the host never
# shows is never uploaded.
$RootInfraFolders = @('src', 'deploy', 'docs', 'scripts', 'readme_files', 'bin', 'obj', 'node_modules', '99.00-temp')
$BuildOutputFolders = @('bin', 'obj', 'node_modules')
# Office lock files, OS metadata, editor state and local databases are never content.
$ExcludedFilePatterns = @(
    '~$*', '~*.tmp', '*.tmp', '*.bak', '*.lnk', 'desktop.ini', 'Thumbs.db', '.DS_Store',
    '*.sqlite', '*.sqlite-*', '*.db', '*.db-wal', '*.db-shm', '*.vsidx', '*.wsuo', '*.suo'
)

$ContentTypes = @{
    '.md' = 'text/markdown; charset=utf-8'; '.qmd' = 'text/markdown; charset=utf-8'
    '.markdown' = 'text/markdown; charset=utf-8'; '.txt' = 'text/plain; charset=utf-8'
    '.yml' = 'application/yaml; charset=utf-8'; '.yaml' = 'application/yaml; charset=utf-8'
    '.json' = 'application/json; charset=utf-8'; '.jsonl' = 'application/jsonl; charset=utf-8'
    '.xml' = 'application/xml; charset=utf-8'; '.csv' = 'text/csv; charset=utf-8'
    '.html' = 'text/html; charset=utf-8'; '.htm' = 'text/html; charset=utf-8'
    '.css' = 'text/css; charset=utf-8'; '.js' = 'text/javascript; charset=utf-8'
    '.vtt' = 'text/vtt; charset=utf-8'; '.ps1' = 'text/plain; charset=utf-8'
    '.py' = 'text/plain; charset=utf-8'; '.cs' = 'text/plain; charset=utf-8'
    '.png' = 'image/png'; '.jpg' = 'image/jpeg'; '.jpeg' = 'image/jpeg'; '.gif' = 'image/gif'
    '.svg' = 'image/svg+xml'; '.webp' = 'image/webp'; '.ico' = 'image/x-icon'; '.bmp' = 'image/bmp'
    '.pdf' = 'application/pdf'; '.zip' = 'application/zip'
    '.mp4' = 'video/mp4'; '.webm' = 'video/webm'; '.mp3' = 'audio/mpeg'; '.wav' = 'audio/wav'
    '.docx' = 'application/vnd.openxmlformats-officedocument.wordprocessingml.document'
    '.xlsx' = 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
    '.pptx' = 'application/vnd.openxmlformats-officedocument.presentationml.presentation'
}

function Read-JsonFile([string]$Path) {
    # PowerShell 7's ConvertFrom-Json accepts the // comments the overlays use.
    Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -AsHashtable
}

function Get-SpacesNode($Map) {
    # The leading comma keeps a one-element Spaces array from being unrolled into its element.
    $site = Get-Value $Map @('Site')
    if ($site -is [System.Collections.IDictionary] -and $site.Contains('Spaces')) { return , $site['Spaces'] }
}

function Get-HttpStatus([System.Management.Automation.ErrorRecord]$ErrorRecord) {
    $response = $ErrorRecord.Exception.PSObject.Properties['Response']
    if ($response -and $response.Value) { return [int]$response.Value.StatusCode }
    0
}

function Get-SpaceElement($Spaces, [int]$Index) {
    # Spaces is a JSON array in full overlays and an index-keyed object ("1": {...}) in partial ones.
    if ($null -eq $Spaces) { return $null }
    if ($Spaces -is [System.Collections.IList]) {
        if ($Index -lt $Spaces.Count) { return $Spaces[$Index] }
        return $null
    }
    if ($Spaces.ContainsKey("$Index")) { return $Spaces["$Index"] }
    return $null
}

function Get-Value($Map, [string[]]$Path) {
    $current = $Map
    foreach ($segment in $Path) {
        if ($null -eq $current -or $current -isnot [System.Collections.IDictionary] -or -not $current.Contains($segment)) {
            return $null
        }
        $current = $current[$segment]
    }
    $current
}

function Resolve-InternalFolder {
    $candidate = if ($InternalFolder) { $InternalFolder }
                 elseif ($env:SMARTDOCS_INTERNAL_FOLDER) { $env:SMARTDOCS_INTERNAL_FOLDER }
                 else { Join-Path (Split-Path $RepositoryRoot -Parent) 'smartdocs.internal' }
    if (Test-Path -LiteralPath $candidate -PathType Container) { return (Resolve-Path -LiteralPath $candidate).Path }
    return $null
}

function Find-Overlay([string]$Name, [string]$PrivateRoot) {
    $file = "appsettings.$Name.json"
    $found = @()
    if ($PrivateRoot) {
        $private = Join-Path $PrivateRoot (Join-Path $WebProjectRelative $file)
        if (Test-Path -LiteralPath $private) { $found += $private }
    }
    $public = Join-Path $RepositoryRoot (Join-Path $WebProjectRelative $file)
    if (Test-Path -LiteralPath $public) { $found += $public }
    $found
}

function Invoke-Az([string[]]$Arguments, [string]$Failure) {
    $output = & az @Arguments --only-show-errors 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "$Failure`n$(($output | Out-String).Trim())"
    }
    ($output | Where-Object { $_ -isnot [System.Management.Automation.ErrorRecord] }) -join "`n"
}

function Get-StorageToken {
    $arguments = @('account', 'get-access-token', '--resource', 'https://storage.azure.com/', '--query', 'accessToken', '-o', 'tsv')
    if ($TenantId) { $arguments += @('--tenant', $TenantId) }
    $token = Invoke-Az $arguments 'Could not get an Azure Storage token. Sign in with "az login" (add --tenant when the storage account lives in another tenant).'
    if ([string]::IsNullOrWhiteSpace($token)) { throw 'The Azure CLI returned an empty storage token.' }
    $token.Trim()
}

function Get-StorageHeaders([string]$Token) {
    @{
        Authorization = "Bearer $Token"
        'x-ms-date'   = [DateTime]::UtcNow.ToString('R')
        'x-ms-version' = $StorageApiVersion
    }
}

function Get-BlobUri([string]$Name) {
    $encoded = ($Name -split '/' | ForEach-Object { [Uri]::EscapeDataString($_) }) -join '/'
    "$ContainerUri/$encoded"
}

function Get-StorageFailureHint([System.Management.Automation.ErrorRecord]$ErrorRecord) {
    $text = "$($ErrorRecord.Exception.Message) $($ErrorRecord.ErrorDetails)"
    if ($text -match 'AuthorizationPermissionMismatch') {
        return 'Your identity lacks a data-plane role. Grant "Storage Blob Data Contributor" on the account or container (role changes can take a few minutes).'
    }
    if ($text -match 'AuthorizationFailure|not authorized to perform this operation') {
        return 'The storage firewall rejected this network. Connect the VPN or private network that reaches the account''s private endpoint, or add a network rule.'
    }
    if ($text -match 'No such host|name.*resolv|actively refused|timed out|unreachable') {
        return 'The blob endpoint is unreachable from this machine. For a private endpoint, check the VPN and that the host name resolves to a private address.'
    }
    if ($text -match 'InvalidAuthenticationInfo|401') {
        return 'The storage token was rejected. Run "az login" again, with --tenant for the storage account''s tenant.'
    }
    return $null
}

function Get-RemoteBlobs([string]$Token) {
    # REST rather than "az storage blob list": the CLI's console encoding mangles non-ASCII names,
    # and the comparison needs exact names, sizes and Content-MD5.
    $files = [System.Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
    $directories = [System.Collections.Generic.List[string]]::new()
    $marker = $null
    do {
        $uri = "${ContainerUri}?restype=container&comp=list&maxresults=5000"
        if ($marker) { $uri += "&marker=$([Uri]::EscapeDataString($marker))" }
        try {
            $response = Invoke-WebRequest -Uri $uri -Headers (Get-StorageHeaders $Token) -Method Get -TimeoutSec 60
        }
        catch {
            $status = Get-HttpStatus $_
            if ($status -eq 404 -and "$($_.ErrorDetails)" -match 'ContainerNotFound') { return $null }
            $hint = Get-StorageFailureHint $_
            throw "Listing the container failed: $($_.Exception.Message)$(if ($hint) { "`n$hint" })"
        }
        $document = [System.Xml.XmlDocument]::new()
        $document.LoadXml(([string]$response.Content).TrimStart([char]0xFEFF))
        foreach ($blob in $document.SelectNodes('/EnumerationResults/Blobs/Blob')) {
            $name = $blob.SelectSingleNode('Name').InnerText
            $resourceType = $blob.SelectSingleNode('Properties/ResourceType')
            if ($resourceType -and $resourceType.InnerText -eq 'directory') {
                $directories.Add($name)
                continue
            }
            $md5 = $blob.SelectSingleNode('Properties/Content-MD5')
            $files[$name] = [pscustomobject]@{
                Length = [long]$blob.SelectSingleNode('Properties/Content-Length').InnerText
                Md5    = if ($md5) { $md5.InnerText } else { '' }
            }
        }
        $next = $document.SelectSingleNode('/EnumerationResults/NextMarker')
        $marker = if ($next) { $next.InnerText } else { $null }
    } while (-not [string]::IsNullOrEmpty($marker))

    [pscustomobject]@{ Files = $files; Directories = $directories }
}

function Get-LocalMd5([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    try { [Convert]::ToBase64String([Security.Cryptography.MD5]::HashData($stream)) }
    finally { $stream.Dispose() }
}

function Test-Excluded([string]$RelativePath) {
    foreach ($pattern in $ExcludePath) {
        $p = $pattern.Replace('\', '/').Trim('/')
        if ($RelativePath -like $p -or $RelativePath -like "$p/*") { return $true }
    }
    $false
}

function Get-ContentFiles([string]$Root) {
    $included = [System.Collections.Generic.List[object]]::new()
    $skipped = [ordered]@{ Excluded = 0; Clutter = 0; Hidden = 0; Oversize = [System.Collections.Generic.List[object]]::new() }
    $maxBytes = [long]$MaxFileSizeMB * 1MB
    $pending = [System.Collections.Generic.Stack[object]]::new()
    $pending.Push([pscustomobject]@{ Path = $Root; Relative = '' })

    while ($pending.Count -gt 0) {
        $folder = $pending.Pop()
        foreach ($entry in Get-ChildItem -LiteralPath $folder.Path -Force) {
            $relative = if ($folder.Relative) { "$($folder.Relative)/$($entry.Name)" } else { $entry.Name }
            $atRoot = -not $folder.Relative
            $hidden = ($entry.Attributes -band [IO.FileAttributes]::Hidden) -or ($entry.Attributes -band [IO.FileAttributes]::System)

            if ($entry.PSIsContainer) {
                if ($entry.Name.StartsWith('.') -or $hidden -or
                    ($atRoot -and ($entry.Name.StartsWith('_') -or $RootInfraFolders -contains $entry.Name)) -or
                    ($BuildOutputFolders -contains $entry.Name) -or (Test-Excluded $relative)) {
                    $skipped.Excluded++
                    continue
                }
                $pending.Push([pscustomobject]@{ Path = $entry.FullName; Relative = $relative })
                continue
            }

            if ($entry.Name.StartsWith('.') -or $hidden) { $skipped.Hidden++; continue }
            if (@($ExcludedFilePatterns | Where-Object { $entry.Name -like $_ }).Count -gt 0) { $skipped.Clutter++; continue }
            if (($atRoot -and $entry.Name.StartsWith('_')) -or (Test-Excluded $relative)) { $skipped.Excluded++; continue }
            if ($maxBytes -gt 0 -and $entry.Length -gt $maxBytes) {
                $skipped.Oversize.Add([pscustomobject]@{ Name = $relative; MB = [math]::Round($entry.Length / 1MB, 1) })
                continue
            }
            $included.Add([pscustomobject]@{ Name = $relative; FullName = $entry.FullName; Length = $entry.Length })
        }
    }

    [pscustomobject]@{ Files = $included; Skipped = $skipped }
}

function Get-TotalMB($Items) {
    # Measure-Object omits Sum for empty input, which StrictMode turns into an error.
    $bytes = 0L
    foreach ($item in $Items) { $bytes += $item.Length }
    $bytes / 1MB
}

function Send-Blobs($Items) {
    # Each item: Name, FullName, Length, Md5, ContentType, Uri. Returns the failures.
    $token = Get-StorageToken
    $client = $script:HttpClient
    $apiVersion = $StorageApiVersion
    $Items | ForEach-Object -ThrottleLimit $ThrottleLimit -Parallel {
        $item = $_
        $http = $using:client
        $bearer = "Bearer $using:token"
        $version = $using:apiVersion
        $ok = $false
        $failure = $null
        for ($attempt = 1; $attempt -le 3 -and -not $ok; $attempt++) {
            $stream = $null
            try {
                $stream = [IO.File]::OpenRead($item.FullName)
                $request = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Put, $item.Uri)
                [void]$request.Headers.TryAddWithoutValidation('Authorization', $bearer)
                [void]$request.Headers.TryAddWithoutValidation('x-ms-date', [DateTime]::UtcNow.ToString('R'))
                [void]$request.Headers.TryAddWithoutValidation('x-ms-version', $version)
                [void]$request.Headers.TryAddWithoutValidation('x-ms-blob-type', 'BlockBlob')
                [void]$request.Headers.TryAddWithoutValidation('x-ms-blob-content-type', $item.ContentType)
                [void]$request.Headers.TryAddWithoutValidation('x-ms-blob-content-md5', $item.Md5)
                $request.Content = [Net.Http.StreamContent]::new($stream)
                $request.Content.Headers.ContentLength = $item.Length
                # Transactional MD5: storage rejects a body corrupted in transit.
                $request.Content.Headers.ContentMD5 = [Convert]::FromBase64String($item.Md5)
                $response = $http.Send($request)
                if ($response.IsSuccessStatusCode) {
                    $ok = $true
                }
                else {
                    $status = [int]$response.StatusCode
                    $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                    $code = if ($body -match '<Code>([^<]+)</Code>') { $Matches[1] } else { $response.ReasonPhrase }
                    $failure = "$status $code"
                    if ($status -lt 500 -and $status -notin 408, 429) { break }
                }
            }
            catch {
                $failure = $_.Exception.GetBaseException().Message
            }
            finally {
                if ($stream) { $stream.Dispose() }
            }
            if (-not $ok -and $attempt -lt 3) { Start-Sleep -Seconds (2 * $attempt) }
        }
        if (-not $ok) { [pscustomobject]@{ Name = $item.Name; Error = $failure } }
    }
}

function Remove-Blob([string]$Name, [string]$Token) {
    try {
        Invoke-WebRequest -Uri (Get-BlobUri $Name) -Headers (Get-StorageHeaders $Token) -Method Delete -TimeoutSec 60 | Out-Null
        $true
    }
    catch {
        Write-Warning "Could not delete '$Name': $($_.Exception.Message)"
        $false
    }
}

# --- Resolve the destination ------------------------------------------------------------------

$privateRoot = Resolve-InternalFolder
$destinationOverlays = @(Find-Overlay $Environment $privateRoot)
if ($destinationOverlays.Count -eq 0) {
    throw "No appsettings.$Environment.json found in the private peer ($(if ($privateRoot) { $privateRoot } else { 'not found: set SMARTDOCS_INTERNAL_FOLDER or -InternalFolder' })) or in this repository."
}

$space = $null
$deployment = $null
foreach ($overlayPath in $destinationOverlays) {
    $overlay = Read-JsonFile $overlayPath
    $spaces = Get-SpacesNode $overlay
    if ($spaces -isnot [System.Collections.IList]) { continue }
    foreach ($candidate in $spaces) {
        if ($candidate -is [System.Collections.IDictionary] -and $candidate['Id'] -eq $SpaceId) { $space = $candidate; break }
    }
    if ($space) {
        $deployment = Get-Value $overlay @('Deployment')
        Write-Host "Destination overlay: $overlayPath"
        break
    }
}
if (-not $space) {
    throw "No space with Id '$SpaceId' is declared in appsettings.$Environment.json ($($destinationOverlays -join ', '))."
}

# A FileSystem space is served from the host's own disk; uploading would succeed and change nothing.
if ($space['Source'] -ne 'Blob') {
    throw "Space '$SpaceId' in $Environment has Source '$($space['Source'])'; only Blob-sourced spaces are published by this script."
}
$accountUri = Get-Value $space @('Blob', 'AccountUri')
$containerName = Get-Value $space @('Blob', 'ContainerName')
if (-not $accountUri -or -not $containerName) {
    throw "Space '$SpaceId' in $Environment doesn't declare Blob:AccountUri and Blob:ContainerName."
}
$ContainerUri = "$($accountUri.TrimEnd('/'))/$containerName"
$accessFileRequired = $space['AccessFileRequired'] -eq $true
$accessFloor = "$($space['Access'])"

# --- Resolve the source -----------------------------------------------------------------------

if (-not $SourcePath) {
    $sourceOverlays = @(Find-Overlay $SourceEnvironment $privateRoot)
    $publicOverlay = $sourceOverlays | Where-Object { $_.StartsWith($RepositoryRoot, [StringComparison]::OrdinalIgnoreCase) } | Select-Object -First 1
    if (-not $publicOverlay) {
        throw "-SourcePath wasn't given and appsettings.$SourceEnvironment.json isn't in this repository to resolve it from."
    }
    $publicSpaces = Get-SpacesNode (Read-JsonFile $publicOverlay)
    $index = -1
    if ($publicSpaces -is [System.Collections.IList]) {
        for ($i = 0; $i -lt $publicSpaces.Count; $i++) {
            if ($publicSpaces[$i]['Id'] -eq $SpaceId) { $index = $i; break }
        }
    }
    if ($index -lt 0) { throw "Space '$SpaceId' isn't declared in $publicOverlay; pass -SourcePath." }

    # Site:Spaces binds by index: a later file's element overrides the earlier one's RootPath.
    $rootPath = Get-Value $publicSpaces[$index] @('FileSystem', 'RootPath')
    foreach ($overlayPath in $sourceOverlays | Where-Object { $_ -ne $publicOverlay }) {
        $element = Get-SpaceElement (Get-SpacesNode (Read-JsonFile $overlayPath)) $index
        $override = Get-Value $element @('FileSystem', 'RootPath')
        if ($override) { $rootPath = $override }
    }
    if (-not $rootPath) { throw "No FileSystem:RootPath for space '$SpaceId' in the $SourceEnvironment overlays; pass -SourcePath." }
    # Relative roots resolve against the web project folder, the host's content root.
    $SourcePath = if ([IO.Path]::IsPathRooted($rootPath)) { $rootPath } else { Join-Path (Join-Path $RepositoryRoot $WebProjectRelative) $rootPath }
}
if (-not (Test-Path -LiteralPath $SourcePath -PathType Container)) {
    throw "Source folder '$SourcePath' doesn't exist."
}
$SourcePath = (Resolve-Path -LiteralPath $SourcePath).Path.TrimEnd('\')

Write-Host "Space:       $SpaceId ($Environment)"
Write-Host "Source:      $SourcePath"
Write-Host "Destination: $ContainerUri"

# --- Stage ------------------------------------------------------------------------------------

$content = Get-ContentFiles $SourcePath
$local = $content.Files
$markdownCount = @($local | Where-Object { $_.Name -match '\.(md|qmd)$' }).Count
if ($markdownCount -eq 0) { throw "No Markdown found under '$SourcePath'; refusing to publish (and prune) an empty space." }

$accessFile = $local | Where-Object { $_.Name -ceq $AccessFileName } | Select-Object -First 1
if (-not $accessFile) {
    if ($accessFileRequired) {
        throw "Space '$SpaceId' requires $AccessFileName at its root (AccessFileRequired), and '$SourcePath' has none. Without it the host hides the space."
    }
    if ($accessFloor) {
        Write-Warning "Space '$SpaceId' has access floor '$accessFloor' but no $AccessFileName; only the floor will apply."
    }
}
elseif ($accessFile.Length -eq 0) {
    throw "$AccessFileName at '$SourcePath' is empty; the host would treat it as invalid."
}

$skipped = $content.Skipped
Write-Host ("Staged {0} files ({1} Markdown, {2:N1} MB); skipped {3} excluded, {4} hidden, {5} clutter, {6} oversize" -f
    $local.Count, $markdownCount, (Get-TotalMB $local),
    $skipped.Excluded, $skipped.Hidden, $skipped.Clutter, $skipped.Oversize.Count)
if ($skipped.Oversize.Count -gt 0) {
    Write-Warning "Not uploaded: $($skipped.Oversize.Count) file(s) above $MaxFileSizeMB MB (the host buffers whole blobs in memory). Links to them won't resolve. Raise -MaxFileSizeMB to include them."
    $skipped.Oversize | Sort-Object MB -Descending | ForEach-Object { Write-Host ("  {0,8:N1} MB  {1}" -f $_.MB, $_.Name) }
}

# --- Compare ----------------------------------------------------------------------------------

$remote = Get-RemoteBlobs (Get-StorageToken)
$containerExists = $null -ne $remote
$remoteFiles = if ($containerExists) { $remote.Files } else { [System.Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal) }

$localNames = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$uploads = [System.Collections.Generic.List[object]]::new()
$unchanged = 0
$hashed = 0
foreach ($file in $local) {
    [void]$localNames.Add($file.Name)
    $hashed++
    if ($hashed % 200 -eq 0) { Write-Progress -Activity 'Comparing with storage' -Status "$hashed / $($local.Count)" -PercentComplete (100 * $hashed / $local.Count) }
    $md5 = Get-LocalMd5 $file.FullName
    $existing = $null
    if ($remoteFiles.TryGetValue($file.Name, [ref]$existing) -and $existing.Length -eq $file.Length -and $existing.Md5 -eq $md5) {
        $unchanged++
        continue
    }
    $extension = [IO.Path]::GetExtension($file.Name).ToLowerInvariant()
    $uploads.Add([pscustomobject]@{
        Name        = $file.Name
        FullName    = $file.FullName
        Length      = $file.Length
        Md5         = $md5
        ContentType = if ($ContentTypes.ContainsKey($extension)) { $ContentTypes[$extension] } else { 'application/octet-stream' }
        Uri         = Get-BlobUri $file.Name
        IsNew       = -not $remoteFiles.ContainsKey($file.Name)
    })
}
Write-Progress -Activity 'Comparing with storage' -Completed

$stale = @($remoteFiles.Keys | Where-Object { -not $localNames.Contains($_) } | Sort-Object)
$newCount = @($uploads | Where-Object IsNew).Count
Write-Host ("Plan: {0} new, {1} changed ({2:N1} MB), {3} unchanged, {4} stale in storage{5}" -f
    $newCount, ($uploads.Count - $newCount), (Get-TotalMB $uploads), $unchanged, $stale.Count,
    $(if ($SkipPrune) { ' (prune skipped)' } else { '' }))
$uploads | ForEach-Object { Write-Verbose "upload $(if ($_.IsNew) { 'new    ' } else { 'changed' }) $($_.Name)" }
$stale | ForEach-Object { Write-Verbose "stale          $_" }

if (-not $SkipPrune -and -not $Force -and $remoteFiles.Count -ge 20 -and $stale.Count -gt ($remoteFiles.Count / 2)) {
    throw "The prune would delete $($stale.Count) of $($remoteFiles.Count) files in the container; is -SourcePath the right folder? Re-run with -Force to confirm, or -SkipPrune."
}

# --- Upload -----------------------------------------------------------------------------------

if (-not $containerExists -and $PSCmdlet.ShouldProcess($ContainerUri, 'Create private container')) {
    try {
        Invoke-WebRequest -Uri "${ContainerUri}?restype=container" -Headers (Get-StorageHeaders (Get-StorageToken)) -Method Put -TimeoutSec 60 | Out-Null
    }
    catch {
        $hint = Get-StorageFailureHint $_
        throw "Creating the container failed: $($_.Exception.Message)$(if ($hint) { "`n$hint" })"
    }
}

$failed = @()
if ($uploads.Count -gt 0 -and $PSCmdlet.ShouldProcess($ContainerUri, "Upload $($uploads.Count) file(s)")) {
    $script:HttpClient = [Net.Http.HttpClient]::new()
    $script:HttpClient.Timeout = [TimeSpan]::FromHours(2)
    try {
        # The access policy goes first so new content is never served under an older policy.
        $accessUpload = @($uploads | Where-Object { $_.Name -ceq $AccessFileName })
        $rest = @($uploads | Where-Object { $_.Name -cne $AccessFileName })
        $done = 0
        if ($accessUpload.Count -gt 0) {
            $accessFailure = @(Send-Blobs $accessUpload)
            if ($accessFailure.Count -gt 0) {
                throw "Uploading $AccessFileName failed ($($accessFailure[0].Error)); nothing else was uploaded."
            }
            $done = 1
        }
        # Batches refresh the storage token, which an hour-long first publish can outlive.
        for ($start = 0; $start -lt $rest.Count; $start += 200) {
            $batch = @($rest[$start..([math]::Min($start + 199, $rest.Count - 1))])
            $failed += @(Send-Blobs $batch)
            $done += $batch.Count
            Write-Progress -Activity 'Uploading' -Status "$done / $($uploads.Count)" -PercentComplete (100 * $done / $uploads.Count)
        }
    }
    finally {
        Write-Progress -Activity 'Uploading' -Completed
        $script:HttpClient.Dispose()
    }

    Write-Host "Uploaded $($uploads.Count - $failed.Count) of $($uploads.Count) file(s)"
    if ($failed.Count -gt 0) {
        $failed | Select-Object -First 20 | ForEach-Object { Write-Host "  FAILED $($_.Name): $($_.Error)" }
        $errorText = ($failed | ForEach-Object Error) -join ' '
        if ($errorText -match 'AuthorizationPermissionMismatch') { Write-Host '  Hint: grant "Storage Blob Data Contributor" on the account or container.' }
        elseif ($errorText -match 'AuthorizationFailure') { Write-Host '  Hint: the storage firewall rejected this network; connect the VPN that reaches the private endpoint.' }
    }
}

# --- Prune ------------------------------------------------------------------------------------

if ($failed.Count -gt 0) {
    Write-Warning 'Prune skipped because some uploads failed; re-run to retry them.'
}
elseif (-not $SkipPrune -and $containerExists -and $stale.Count -gt 0 -and
        $PSCmdlet.ShouldProcess($ContainerUri, "Delete $($stale.Count) stale file(s)")) {
    $token = Get-StorageToken
    $deleted = 0
    foreach ($name in $stale) {
        if ($name -ceq $AccessFileName -and $accessFileRequired) {
            Write-Warning "Keeping $AccessFileName in storage: the space requires it. Remove it there deliberately if intended."
            continue
        }
        if (Remove-Blob $name $token) { $deleted++ }
    }

    # Hierarchical-namespace accounts keep real directories; drop the ones nothing lives under any more.
    $localFolders = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($name in $localNames) {
        $slash = $name.LastIndexOf('/')
        while ($slash -gt 0) {
            if (-not $localFolders.Add($name.Substring(0, $slash))) { break }
            $slash = $name.LastIndexOf('/', $slash - 1)
        }
    }
    $emptyDirectories = @($remote.Directories | Where-Object { -not $localFolders.Contains($_) } |
        Sort-Object { $_.Split('/').Count } -Descending)
    $deletedDirectories = 0
    foreach ($directory in $emptyDirectories) {
        if (Remove-Blob $directory $token) { $deletedDirectories++ }
    }
    Write-Host "Pruned $deleted stale file(s) and $deletedDirectories empty folder(s)"
}

# --- Invalidate -------------------------------------------------------------------------------

$changed = ($uploads.Count - $failed.Count) -gt 0 -or (-not $SkipPrune -and $stale.Count -gt 0)
if ($SkipInvalidate -or $WhatIfPreference) {
    if ($changed) { Write-Host 'Navigation invalidation skipped.' }
}
elseif (-not $changed) {
    Write-Host 'Storage already matched the source; nothing to invalidate.'
}
else {
    $webAppName = Get-Value $deployment @('WebAppName')
    $resourceGroup = $null
    if (-not $SiteUrl -and $webAppName) {
        $app = Invoke-Az @('webapp', 'list', '--query', "[?name=='$webAppName'] | [0].{rg:resourceGroup, host:defaultHostName}", '-o', 'json') 'Could not look up the App Service.' | ConvertFrom-Json
        if ($app) { $SiteUrl = "https://$($app.host)"; $resourceGroup = $app.rg }
    }
    if (-not $SiteUrl) {
        Write-Warning "No -SiteUrl and no resolvable Deployment:WebAppName; the host picks the change up within its content-freshness window."
    }
    else {
        if (-not $InvalidateKey) { $InvalidateKey = $env:SMARTDOCS_INVALIDATE_KEY }
        if (-not $InvalidateKey -and $resourceGroup) {
            $InvalidateKey = Invoke-Az @('webapp', 'config', 'appsettings', 'list', '--name', $webAppName, '--resource-group', $resourceGroup,
                '--query', "[?name=='Site__InvalidateApiKey'].value | [0]", '-o', 'tsv') 'Could not read the invalidation key from the App Service.'
        }
        $headers = @{}
        if ($InvalidateKey) { $headers['X-Invalidate-Key'] = $InvalidateKey.Trim() }

        $invalidated = $false
        foreach ($delay in 0, 5, 15, 30) {
            Start-Sleep -Seconds $delay
            try {
                $response = Invoke-WebRequest -Uri "$($SiteUrl.TrimEnd('/'))/_nav/invalidate" -Method Post -Headers $headers -TimeoutSec 60
                $version = ($response.Content | ConvertFrom-Json).version
                if ($null -ne $version) {
                    Write-Host "Navigation invalidated (version $version)"
                    $invalidated = $true
                    break
                }
            }
            catch {
                if ((Get-HttpStatus $_) -eq 401) {
                    Write-Warning 'The host refused the invalidation key (401). Pass -InvalidateKey or set SMARTDOCS_INVALIDATE_KEY.'
                    break
                }
                Write-Verbose "Invalidation attempt failed: $($_.Exception.Message)"
            }
        }
        if (-not $invalidated) {
            Write-Warning 'Navigation wasn''t invalidated; the content is published and appears within the host''s content-freshness window.'
        }
    }
}

if ($failed.Count -gt 0) { exit 1 }
