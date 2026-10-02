<#
.SYNOPSIS
Generates a synthetic content tree for scale measurement.

.DESCRIPTION
Writes a navigable content set of the shape SmartDocs expects — area, group, leaf
section, articles — with a metadata.yml at every folder and renderer frontmatter on
every article. It exists to answer the acceptance criterion of
src/docs/90.00-issues/202610/20261001.01-perfanalysis/01-startup-and-navigation-optimization.analysis.md:
a tree of about 10,000 sections and 100,000 articles must leave startup time,
first-page reads, and server memory unchanged within 10%.

The defaults produce exactly that tree. Pass smaller fan-outs to measure a point on
the curve: the measurement is only meaningful when several sizes are compared, because
the finding under test is growth with the corpus, not any single figure.

The tree is written outside the repository by default, because it is neither content
nor test data: it is measurement apparatus, regenerated on demand and never committed.

.PARAMETER RootPath
Folder that receives the tree. Defaults to a sibling of the repository,
..\smartdocs.scale-tree, which is the path appsettings.scale.json declares.

.PARAMETER Areas
Top-level sections. Each becomes NN.00-area-NN.

.PARAMETER GroupsPerArea
Second-level folders per area, named as year-months.

.PARAMETER SectionsPerGroup
Leaf sections per group, named as dated work items.

.PARAMETER ArticlesPerSection
Articles per leaf section. One of them is the section's overview.md.

.PARAMETER BodyKilobytes
Approximate size of each article body. The Learning Hub averages about 10 KB; the
default of 2 KB keeps a 100,000-article tree near 200 MB instead of 1 GB.

.PARAMETER Force
Overwrite an existing tree at RootPath.

.EXAMPLE
.\scripts\Smartdocs.ContentTree.Generate.ps1
Generates the full 10,000-section, 100,000-article tree.

.EXAMPLE
.\scripts\Smartdocs.ContentTree.Generate.ps1 -Areas 4 -GroupsPerArea 5 -SectionsPerGroup 5 -ArticlesPerSection 10
Generates a 100-section, 1,000-article tree — a fast point on the curve.
#>

[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'Medium')]
param(
    [string]$RootPath,
    [ValidateRange(1, 200)][int]$Areas = 20,
    [ValidateRange(1, 200)][int]$GroupsPerArea = 25,
    [ValidateRange(1, 500)][int]$SectionsPerGroup = 20,
    [ValidateRange(1, 500)][int]$ArticlesPerSection = 10,
    [ValidateRange(1, 64)][int]$BodyKilobytes = 2,
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
if (-not $RootPath) {
    $RootPath = Join-Path (Split-Path -Parent $repositoryRoot) 'smartdocs.scale-tree'
}

$sectionCount = $Areas * $GroupsPerArea * $SectionsPerGroup
$articleCount = $sectionCount * $ArticlesPerSection

Write-Host "Target: $Areas areas x $GroupsPerArea groups x $SectionsPerGroup sections x $ArticlesPerSection articles"
Write-Host "        = $('{0:N0}' -f $sectionCount) sections, $('{0:N0}' -f $articleCount) articles at ~$BodyKilobytes KB each"
Write-Host "Root:   $RootPath"

if (Test-Path -LiteralPath $RootPath) {
    if (-not $Force) {
        throw "'$RootPath' already exists. Pass -Force to replace it."
    }

    if ($PSCmdlet.ShouldProcess($RootPath, 'Remove the existing tree')) {
        Remove-Item -LiteralPath $RootPath -Recurse -Force
    }
}

if (-not $PSCmdlet.ShouldProcess($RootPath, "Generate $('{0:N0}' -f $articleCount) articles")) {
    return
}

# One body, reused by every article. The measurement is about the number of files the
# navigation walks and the bytes the cache holds, not about distinct prose.
$paragraph = 'Synthetic body text generated for scale measurement. It carries no meaning and is ' +
             'reused across every article so that the measured cost reflects the size of the corpus ' +
             'rather than the variety of its content. '
$bodyBuilder = [System.Text.StringBuilder]::new()
$targetBytes = $BodyKilobytes * 1024
while ($bodyBuilder.Length -lt $targetBytes) {
    [void]$bodyBuilder.AppendLine($paragraph)
    if ($bodyBuilder.Length % 512 -lt $paragraph.Length) {
        [void]$bodyBuilder.AppendLine()
    }
}
$body = $bodyBuilder.ToString()

$utf8 = [System.Text.UTF8Encoding]::new($false)
$baseDate = [datetime]::new(2024, 1, 1)
$written = 0
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

function Write-FolderMetadata {
    param([string]$Folder, [string]$Label, [string]$Icon, [int]$Order)

    $content = @"
label: "$Label"
icon: "$Icon"
order: $Order
"@
    [System.IO.File]::WriteAllText((Join-Path $Folder 'metadata.yml'), $content, $utf8)
}

[void][System.IO.Directory]::CreateDirectory($RootPath)
Write-FolderMetadata -Folder $RootPath -Label 'Scale tree' -Icon 'bi-diagram-3' -Order 1

for ($area = 1; $area -le $Areas; $area++) {
    $areaName = '{0:D2}.00-area-{0:D2}' -f $area
    $areaPath = Join-Path $RootPath $areaName
    [void][System.IO.Directory]::CreateDirectory($areaPath)
    Write-FolderMetadata -Folder $areaPath -Label "Area $area" -Icon 'bi-folder' -Order $area

    for ($group = 1; $group -le $GroupsPerArea; $group++) {
        $groupDate = $baseDate.AddMonths((($area - 1) * $GroupsPerArea) + $group - 1)
        $groupName = $groupDate.ToString('yyyyMM')
        $groupPath = Join-Path $areaPath $groupName
        [void][System.IO.Directory]::CreateDirectory($groupPath)
        Write-FolderMetadata -Folder $groupPath -Label $groupDate.ToString('MMMM yyyy') -Icon 'bi-calendar3' -Order $group

        for ($section = 1; $section -le $SectionsPerGroup; $section++) {
            $sectionDate = $groupDate.AddDays(($section - 1) % 28)
            $sectionName = '{0}.{1:D2}-item-{1:D2}' -f $sectionDate.ToString('yyyyMMdd'), $section
            $sectionPath = Join-Path $groupPath $sectionName
            [void][System.IO.Directory]::CreateDirectory($sectionPath)
            Write-FolderMetadata -Folder $sectionPath -Label "Item $section" -Icon 'bi-file-earmark-text' -Order $section

            for ($article = 1; $article -le $ArticlesPerSection; $article++) {
                $articleDate = $sectionDate.AddDays($article - 1)
                $fileName = if ($article -eq 1) { 'overview.md' } else { '{0:D2}-article-{0:D2}.md' -f $article }
                $title = if ($article -eq 1) { "Item $section overview" } else { "Item $section, article $article" }

                $content = @"
---
title: "$title"
author: "Scale Harness"
date: "$($articleDate.ToString('yyyy-MM-dd'))"
categories: [scale, synthetic]
description: "Synthetic article $article of item $section, generated for scale measurement."
publish: false
---

# $title

$body
"@
                [System.IO.File]::WriteAllText((Join-Path $sectionPath $fileName), $content, $utf8)
                $written++
            }
        }
    }

    $percent = [int](($area / $Areas) * 100)
    Write-Progress -Activity 'Generating content tree' -Status "$('{0:N0}' -f $written) articles" -PercentComplete $percent
}

Write-Progress -Activity 'Generating content tree' -Completed
$stopwatch.Stop()

$bytes = (Get-ChildItem -LiteralPath $RootPath -Recurse -File | Measure-Object -Property Length -Sum).Sum
Write-Host ''
Write-Host "Generated $('{0:N0}' -f $written) articles in $('{0:N0}' -f $sectionCount) sections"
Write-Host "Size on disk: $('{0:N1}' -f ($bytes / 1MB)) MB"
Write-Host "Elapsed: $('{0:N1}' -f $stopwatch.Elapsed.TotalSeconds) s"
Write-Host ''
Write-Host "Serve it with: AppsettingsEnvironmentName=scale (see appsettings.scale.json)"

[pscustomobject]@{
    RootPath   = $RootPath
    Sections   = $sectionCount
    Articles   = $written
    Megabytes  = [math]::Round($bytes / 1MB, 1)
    ElapsedSec = [math]::Round($stopwatch.Elapsed.TotalSeconds, 1)
}
