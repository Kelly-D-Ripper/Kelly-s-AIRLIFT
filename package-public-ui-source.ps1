[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path -LiteralPath $PSScriptRoot).Path
$artifacts = Join-Path $projectRoot 'artifacts'
$stage = Join-Path $artifacts 'KellysAIRLIFTPublicUI-0.17.1-SOURCE'
$resolvedStage = [System.IO.Path]::GetFullPath($stage)
if (-not $resolvedStage.StartsWith($projectRoot + [System.IO.Path]::DirectorySeparatorChar,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to stage outside project: $resolvedStage"
}
if (Test-Path -LiteralPath $stage) {
    Remove-Item -LiteralPath $stage -Recurse -Force
}
New-Item -ItemType Directory -Path (Join-Path $stage 'docs') -Force | Out-Null

$uiRoot = Join-Path $projectRoot 'KellysAIRLIFTPublicUI'
foreach ($name in @('.gitignore', 'BUILDING.md', 'KellysAIRLIFTPublicUI.csproj',
        'Plugin.cs', 'README.md')) {
    Copy-Item -LiteralPath (Join-Path $uiRoot $name) -Destination (Join-Path $stage $name)
}
Copy-Item -LiteralPath (Join-Path $projectRoot 'SECURITY.md') -Destination (Join-Path $stage 'SECURITY.md')
Copy-Item -LiteralPath (Join-Path $uiRoot 'RELEASE-0.17.1.md') `
    -Destination (Join-Path $stage 'docs\RELEASE-0.17.1.md')

$manifestPath = Join-Path $stage 'SHA256SUMS.txt'
$manifestLines = Get-ChildItem -LiteralPath $stage -File -Recurse |
    Where-Object { $_.FullName -ne $manifestPath } |
    Sort-Object FullName |
    ForEach-Object {
        $relative = $_.FullName.Substring($stage.Length + 1).Replace('\', '/')
        '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash, $relative
    }
[System.IO.File]::WriteAllLines($manifestPath, $manifestLines,
    [System.Text.UTF8Encoding]::new($false))

$zip = Join-Path $artifacts 'KellysAIRLIFTPublicUI-0.17.1-SOURCE.zip'
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::Open($zip,
    [System.IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem -LiteralPath $stage -File -Recurse | Sort-Object FullName | ForEach-Object {
        $relative = $_.FullName.Substring($stage.Length + 1).Replace('\', '/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive, $_.FullName, $relative,
            [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally { $archive.Dispose() }

Write-Host "Public UI source release: $zip"
Write-Host "SHA256: $((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash)"

