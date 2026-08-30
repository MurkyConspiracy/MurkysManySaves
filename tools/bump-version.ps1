param(
    [Parameter(Mandatory = $true)][string]$ManifestPath
)

$fullPath = Resolve-Path -Path $ManifestPath
$content = [System.IO.File]::ReadAllText($fullPath)

$pattern = '<ModVersion>(\d+)\.(\d+)</ModVersion>'
$match = [regex]::Match($content, $pattern)
if (-not $match.Success) {
    throw "Could not find <ModVersion>Major.Minor</ModVersion> in $ManifestPath"
}

$major = [int]$match.Groups[1].Value
$minor = [int]$match.Groups[2].Value + 1
$oldVersion = "$($match.Groups[1].Value).$($match.Groups[2].Value)"
$newVersion = "$major.$minor"

$newContent = $content.Substring(0, $match.Index) + "<ModVersion>$newVersion</ModVersion>" + $content.Substring($match.Index + $match.Length)

# Write back without a BOM and without altering line endings, matching the original file exactly.
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($fullPath, $newContent, $utf8NoBom)

Write-Host "Bumped ModVersion: $oldVersion -> $newVersion"
