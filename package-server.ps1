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
$stage = Join-Path $artifacts 'KellysAIRLIFT-0.14.0-SERVER'
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
Copy-Item -LiteralPath (Join-Path $projectRoot 'config\kelly.nuclearoption.airlift.server.cfg') `
    -Destination (Join-Path $configTarget 'kelly.nuclearoption.airlift.cfg')
Copy-Item -LiteralPath (Join-Path $projectRoot 'config\kelly.nuclearoption.airlift.zones.tsv') `
    -Destination (Join-Path $configTarget 'kelly.nuclearoption.airlift.zones.tsv')
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination $stage
Copy-Item -LiteralPath (Join-Path $projectRoot 'INSTALL-CHECKLIST.md') -Destination $stage
Copy-Item -LiteralPath (Join-Path $projectRoot 'PUBLIC-RELEASE-CHECKLIST.md') -Destination $stage
Copy-Item -LiteralPath (Join-Path $projectRoot 'CHANGELOG.md') -Destination $stage
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\ARCHITECTURE.md') -Destination $docsTarget
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\CHIMERA-034-RESEARCH.md') -Destination $docsTarget

$serverReadme = @(
    "KELLY'S AIRLIFT 0.14.0 - CLEAN DEDICATED-SERVER PACKAGE"
    ""
    "WARNING: this package contains active production config and zone files."
    "Back up and compare an existing BepInEx/config installation before extracting."
    "Automatic airport-capture airlifts are enabled; listen-server triggers are disabled."
    "Public RAPID purchases are enabled at `$400m with a five-minute faction cooldown."
    "The buyer selects a currently enemy-held airport; the server revalidates it before charging."
    "RAPID transports carry AGM-68 x3 and suppress tracked hostile runway ground units."
    "Install the separate PUBLIC-UI package on player clients that need the Donate-menu button."
    "Recorded Terrain1 zones are preferred; validated dynamic fallback covers other live airports."
    "The compatible Aryx MC-260 Chimera addon remains a separate prerequisite."
)
[System.IO.File]::WriteAllLines(
    (Join-Path $stage 'SERVER-INSTALL.txt'),
    $serverReadme,
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

$zip = Join-Path $artifacts 'KellysAIRLIFT-0.14.0-SERVER-NO034.zip'
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
Write-Host "Server release: $zip"
Write-Host "SHA256: $((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash)"
