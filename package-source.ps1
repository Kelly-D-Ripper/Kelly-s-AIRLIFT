[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path -LiteralPath $PSScriptRoot).Path
$artifacts = Join-Path $projectRoot 'artifacts'
$stage = Join-Path $artifacts 'KellysAIRLIFT-0.14.0-SOURCE'
$resolvedStage = [System.IO.Path]::GetFullPath($stage)
if (-not $resolvedStage.StartsWith($projectRoot + [System.IO.Path]::DirectorySeparatorChar,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to stage outside project: $resolvedStage"
}
if (Test-Path -LiteralPath $stage) {
    Remove-Item -LiteralPath $stage -Recurse -Force
}
New-Item -ItemType Directory -Path $stage -Force | Out-Null

$files = @(
    '.gitignore',
    'AirliftPolicy.cs',
    'AirliftTransportState.cs',
    'CHANGELOG.md',
    'CONTRIBUTING.md',
    'INSTALL-CHECKLIST.md',
    'KellysAIRLIFT.csproj',
    'Models.cs',
    'NativeDefensiveCountermeasures.cs',
    'Plugin.cs',
    'PUBLIC-RELEASE-CHECKLIST.md',
    'README.md',
    'SECURITY.md',
    'package-local-test.ps1',
    'package-public-ui.ps1',
    'package-release.ps1',
    'package-server.ps1',
    'package-source.ps1',
    'verify-release.ps1'
)
$directories = @(
    '.github',
    'config',
    'docs',
    'KellysAIRLIFT.Tests',
    'KellysAIRLIFTPublicUI'
)
foreach ($relative in $files) {
    $source = Join-Path $projectRoot $relative
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
        throw "Missing public source file: $relative"
    }
    $destination = Join-Path $stage $relative
    $parent = Split-Path -Parent $destination
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $destination
}
foreach ($relative in $directories) {
    $source = Join-Path $projectRoot $relative
    if (-not (Test-Path -LiteralPath $source -PathType Container)) {
        throw "Missing public source directory: $relative"
    }
    Copy-Item -LiteralPath $source -Destination (Join-Path $stage $relative) -Recurse
}

Get-ChildItem -LiteralPath $stage -Recurse -Directory |
    Where-Object { $_.Name -in @('bin', 'obj', 'artifacts', 'backups') } |
    Sort-Object FullName -Descending |
    Remove-Item -Recurse -Force
Get-ChildItem -LiteralPath $stage -Recurse -File |
    Where-Object { $_.Extension -in @('.dll', '.pdb', '.nobp', '.zip', '.log') } |
    Remove-Item -Force

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

$zip = Join-Path $artifacts 'KellysAIRLIFT-0.14.0-SOURCE.zip'
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
Write-Host "Source release: $zip"
Write-Host "SHA256: $((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash)"
