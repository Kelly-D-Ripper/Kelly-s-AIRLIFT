[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path -LiteralPath $PSScriptRoot).Path
$workspaceRoot = (Resolve-Path -LiteralPath (Join-Path $projectRoot '..')).Path
$localDotnet = Join-Path $workspaceRoot 'build-tools\dotnet-sdk\dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $localDotnet -PathType Leaf) {
    $localDotnet
} else {
    (Get-Command dotnet -ErrorAction Stop).Source
}
$gameRoot = if ($env:NUCLEAR_OPTION_DIR) {
    $env:NUCLEAR_OPTION_DIR
} else {
    'C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option'
}
if (-not (Test-Path -LiteralPath (Join-Path $gameRoot 'NuclearOption_Data\Managed\Assembly-CSharp.dll') -PathType Leaf)) {
    throw "Set NUCLEAR_OPTION_DIR to a Nuclear Option 0.34 installation."
}

$env:NUCLEAR_OPTION_DIR = $gameRoot
& $dotnet build (Join-Path $projectRoot 'KellysAIRLIFT.csproj') -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Plugin build failed.' }
& $dotnet run --project (Join-Path $projectRoot 'KellysAIRLIFT.Tests\KellysAIRLIFT.Tests.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Policy tests failed.' }

$artifacts = Join-Path $projectRoot 'artifacts'
$stage = Join-Path $artifacts 'KellysAIRLIFT-0.14.0-LOCAL-TEST'
$resolvedProject = [System.IO.Path]::GetFullPath($projectRoot)
$resolvedStage = [System.IO.Path]::GetFullPath($stage)
if (-not $resolvedStage.StartsWith($resolvedProject + [System.IO.Path]::DirectorySeparatorChar,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to stage outside project: $resolvedStage"
}
if (Test-Path -LiteralPath $stage) {
    Remove-Item -LiteralPath $stage -Recurse -Force
}

$pluginTarget = Join-Path $stage 'BepInEx\plugins\KellysAIRLIFT'
$configTarget = Join-Path $stage 'BepInEx\config'
$docsTarget = Join-Path $stage 'docs'
New-Item -ItemType Directory -Path $pluginTarget, $configTarget, $docsTarget -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'bin\Release\net472\KellysAIRLIFT.dll') -Destination $pluginTarget
Copy-Item -LiteralPath (Join-Path $projectRoot 'config\kelly.nuclearoption.airlift.local-test.cfg') `
    -Destination (Join-Path $configTarget 'kelly.nuclearoption.airlift.cfg')
Copy-Item -LiteralPath (Join-Path $projectRoot 'config\kelly.nuclearoption.airlift.zones.tsv') `
    -Destination (Join-Path $configTarget 'kelly.nuclearoption.airlift.zones.tsv')
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination $stage
Copy-Item -LiteralPath (Join-Path $projectRoot 'INSTALL-CHECKLIST.md') -Destination $stage
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\ARCHITECTURE.md') -Destination $docsTarget
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\CHIMERA-034-RESEARCH.md') -Destination $docsTarget

$localReadme = @(
    "KELLY'S AIRLIFT 0.14.0 - LOCAL HOST TEST"
    ""
    "This package enables AllowListenServerTesting."
    "Use it only while your local game is hosting the mission."
    "It does not grant client-side spawn authority on another server."
    ""
    "Start a local mission, then use chat:"
    "  /airlift validate"
    "  /airlift start"
    "  /airlift purchase rapid <enemy-airport>"
    "Public RAPID drops cost `$400m and have a five-minute faction cooldown."
)
[System.IO.File]::WriteAllLines(
    (Join-Path $stage 'LOCAL-TEST.txt'),
    $localReadme,
    [System.Text.UTF8Encoding]::new($false))

$manifestPath = Join-Path $stage 'SHA256SUMS.txt'
$manifestLines = Get-ChildItem -LiteralPath $stage -File -Recurse |
    Where-Object { $_.FullName -ne $manifestPath } |
    Sort-Object FullName |
    ForEach-Object {
        $relative = $_.FullName.Substring($stage.Length + 1).Replace('\', '/')
        '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash, $relative
    }
[System.IO.File]::WriteAllLines($manifestPath, $manifestLines, [System.Text.UTF8Encoding]::new($false))

$zip = Join-Path $artifacts 'KellysAIRLIFT-0.14.0-LOCAL-TEST-NO034.zip'
if (Test-Path -LiteralPath $zip) {
    Remove-Item -LiteralPath $zip -Force
}
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
Write-Host "Local test release: $zip"
Write-Host "SHA256: $((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash)"
