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
    throw 'Set NUCLEAR_OPTION_DIR to a Nuclear Option 0.34 installation.'
}

$env:NUCLEAR_OPTION_DIR = $gameRoot
$uiProject = Join-Path $projectRoot 'KellysAIRLIFTPublicUI\KellysAIRLIFTPublicUI.csproj'
& $dotnet build $uiProject -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Public purchase UI build failed.' }

$artifacts = Join-Path $projectRoot 'artifacts'
$stage = Join-Path $artifacts 'KellysAIRLIFT-0.14.0-PUBLIC-UI'
$resolvedProject = [System.IO.Path]::GetFullPath($projectRoot)
$resolvedStage = [System.IO.Path]::GetFullPath($stage)
if (-not $resolvedStage.StartsWith($resolvedProject + [System.IO.Path]::DirectorySeparatorChar,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to stage outside project: $resolvedStage"
}
if (Test-Path -LiteralPath $stage) {
    Remove-Item -LiteralPath $stage -Recurse -Force
}

$pluginTarget = Join-Path $stage 'BepInEx\plugins\KellysAIRLIFTPublicUI'
New-Item -ItemType Directory -Path $pluginTarget -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'KellysAIRLIFTPublicUI\bin\Release\net472\KellysAIRLIFTPublicUI.dll') `
    -Destination $pluginTarget
Copy-Item -LiteralPath (Join-Path $projectRoot 'KellysAIRLIFTPublicUI\README.md') `
    -Destination (Join-Path $stage 'README.md')

$install = @(
    "KELLY'S AIRLIFT 0.14.0 - RAPID PUBLIC PURCHASE UI"
    ""
    "Install this optional package on every player client that should see"
    "RAPID Combat Drop under Donate > Vehicles. Do not install it on the server."
    "Choose an enemy-held destination from the dropdown before purchasing."
    "The server must run Kelly's AIRLIFT 0.14.0 and remains authoritative."
    "A client cannot charge allocation or spawn aircraft with this addon."
)
[System.IO.File]::WriteAllLines(
    (Join-Path $stage 'PUBLIC-UI-INSTALL.txt'),
    $install,
    [System.Text.UTF8Encoding]::new($false))

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

$zip = Join-Path $artifacts 'KellysAIRLIFT-0.14.0-PUBLIC-UI-NO034.zip'
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
Write-Host "Public UI release: $zip"
Write-Host "SHA256: $((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash)"
