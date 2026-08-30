<#
.SYNOPSIS
Cuts a full release: Release build (bumps ModVersion), packages the deployed mod
folder into a zip, commits + tags the bump, then pushes and publishes a GitHub release.

.PARAMETER Notes
Release notes body. If omitted, gh generates notes from commits since the last tag.

.PARAMETER DryRun
Builds and packages the zip but stops before committing/tagging/pushing, and
reverts the manifest.xml version bump afterward.

.PARAMETER AllowDirty
Skip the clean-working-tree check.
#>
param(
    [string]$Notes,
    [switch]$DryRun,
    [switch]$AllowDirty
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Error "GitHub CLI ('gh') not found on PATH. Install it from https://cli.github.com/ and run 'gh auth login'."
    exit 1
}

$status = git status --porcelain
if ($status -and -not $AllowDirty) {
    Write-Error "Working tree is not clean - commit or stash your changes first (or pass -AllowDirty):`n$status"
    exit 1
}

Write-Host "Building Release..."
dotnet build -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed."
    exit 1
}

$manifestContent = Get-Content -Path (Join-Path $root "manifest.xml") -Raw
$match = [regex]::Match($manifestContent, '<ModVersion>(\d+\.\d+)</ModVersion>')
if (-not $match.Success) {
    Write-Error "Could not read ModVersion from manifest.xml."
    exit 1
}
$version = $match.Groups[1].Value
$tag = "v$version"

if (git tag -l $tag) {
    Write-Error "Tag $tag already exists - was this version already released?"
    exit 1
}

Write-Host "Releasing $tag"

$modOutputDir = Join-Path $root "..\..\Mods\MurkysManySaves"
$distDir = Join-Path $root "dist"
New-Item -ItemType Directory -Force -Path $distDir | Out-Null
$zipPath = Join-Path $distDir "MurkysManySaves-$tag.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path $modOutputDir -DestinationPath $zipPath
Write-Host "Packaged $zipPath"

if ($DryRun) {
    Write-Host "Dry run - reverting the manifest.xml version bump, skipping commit/tag/push."
    git checkout -- manifest.xml
    exit 0
}

git add manifest.xml
git commit -m "Release $tag"
git tag $tag

$branch = git rev-parse --abbrev-ref HEAD
Write-Host ""
Write-Host "About to push branch '$branch' and tag '$tag' to origin, and publish a GitHub release with '$zipPath' attached."
$confirm = Read-Host "Proceed? [y/N]"
if ($confirm -notmatch '^[Yy]') {
    Write-Host "Aborted before pushing. Local commit and tag were created - to roll back:"
    Write-Host "  git tag -d $tag; git reset --soft HEAD~1"
    exit 0
}

git push origin $branch
git push origin $tag

$releaseArgs = @($tag, $zipPath, "--title", $tag)
if ($Notes) {
    $releaseArgs += @("--notes", $Notes)
} else {
    $releaseArgs += "--generate-notes"
}
gh release create @releaseArgs

Write-Host "Published $tag."
