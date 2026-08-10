[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path -LiteralPath $PSScriptRoot).Path
$artifacts = Join-Path $projectRoot 'artifacts'
$expectedDll = Join-Path $projectRoot 'bin\Release\net472\KellysAIRLIFT.dll'
$expectedPublicUiDll = Join-Path $projectRoot 'KellysAIRLIFTPublicUI\bin\Release\net472\KellysAIRLIFTPublicUI.dll'
$checks = 0
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Assert-Release([bool]$condition, [string]$message) {
    if (-not $condition) { throw $message }
    $script:checks++
}

function Get-ZipEntryText($entry) {
    $reader = [System.IO.StreamReader]::new($entry.Open())
    try { return $reader.ReadToEnd() } finally { $reader.Dispose() }
}

function Get-ZipEntryHash($entry) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    $stream = $entry.Open()
    try { return ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '') }
    finally { $stream.Dispose(); $sha.Dispose() }
}

function Test-AirliftZip([string]$fileName, [string[]]$requiredEntries,
        [bool]$expectActiveConfig, [bool]$expectZones, [bool]$expectProductionConfig) {
    $path = Join-Path $artifacts $fileName
    Assert-Release (Test-Path -LiteralPath $path -PathType Leaf) "Missing package: $fileName"
    $archive = [System.IO.Compression.ZipFile]::OpenRead($path)
    try {
        $entries = @{}
        foreach ($entry in $archive.Entries) {
            Assert-Release (-not $entry.FullName.Contains('\')) "Backslash ZIP path in $fileName"
            Assert-Release (-not $entry.FullName.StartsWith('/')) "Absolute ZIP path in $fileName"
            Assert-Release ($entry.FullName -notmatch '(^|/)\.\.(/|$)') "Traversal ZIP path in $fileName"
            $key = $entry.FullName.ToLowerInvariant()
            Assert-Release (-not $entries.ContainsKey($key)) "Duplicate ZIP path in ${fileName}: $($entry.FullName)"
            $entries[$key] = $entry
            $leaf = [System.IO.Path]::GetFileName($entry.FullName)
            Assert-Release ($leaf -notmatch '(?i)^(Assembly-CSharp|Mirage|UnityEngine.*|BepInEx|0Harmony)\.dll$') `
                "Proprietary/runtime dependency entered ${fileName}: $leaf"
            Assert-Release ($leaf -notmatch '(?i)\.(nobp|pdb)$') "Content/debug file entered ${fileName}: $leaf"
        }
        foreach ($required in $requiredEntries) {
            Assert-Release ($entries.ContainsKey($required.ToLowerInvariant())) `
                "Missing $required from $fileName"
        }
        $dllEntry = $entries['bepinex/plugins/kellysairlift/kellysairlift.dll']
        Assert-Release ((Get-ZipEntryHash $dllEntry) -eq
            (Get-FileHash -LiteralPath $expectedDll -Algorithm SHA256).Hash) `
            "Stale AIRLIFT DLL in $fileName"
        $hasActiveConfig = $entries.ContainsKey('bepinex/config/kelly.nuclearoption.airlift.cfg')
        Assert-Release ($hasActiveConfig -eq $expectActiveConfig) "Unexpected active config layout in $fileName"
        $hasActiveZones = $entries.ContainsKey('bepinex/config/kelly.nuclearoption.airlift.zones.tsv')
        Assert-Release ($hasActiveZones -eq ($expectActiveConfig -and $expectZones)) `
            "Unexpected active zone layout in $fileName"
        if ($entries.ContainsKey('examples/kelly.nuclearoption.airlift.default.cfg')) {
            $defaultConfig = Get-ZipEntryText $entries['examples/kelly.nuclearoption.airlift.default.cfg']
            Assert-Release ($defaultConfig -match '(?m)^OwnerSteamId\s*=\s*0\s*$') `
                "Generic public config grants a manual-command identity in $fileName"
        }
        if ($expectActiveConfig) {
            $config = Get-ZipEntryText $entries['bepinex/config/kelly.nuclearoption.airlift.cfg']
            Assert-Release ($config -match '(?m)^OwnerSteamId\s*=\s*0\s*$') `
                "Public server/test config grants a manual-command identity in $fileName"
        }
        if ($expectProductionConfig) {
            Assert-Release ($config -match '(?m)^EnableAirportCaptureAirlifts\s*=\s*true\s*$') `
                "Server automatic mode is not enabled in $fileName"
            Assert-Release ($config -match '(?m)^AllowListenServerCaptureTesting\s*=\s*false\s*$') `
                "Listen-server capture mode is unsafe in $fileName"
            Assert-Release ($config -match '(?m)^AllowValidatedDynamicZoneFallback\s*=\s*true\s*$') `
                "Validated dynamic airport fallback is not enabled in $fileName"
            Assert-Release ($config -match '(?m)^DropRadarAltitude\s*=\s*450\s*$') `
                "Server drop altitude is not 450 metres in $fileName"
            Assert-Release ($config -match '(?m)^MaximumSlopeDegrees\s*=\s*7\s*$') `
                "Server slope ceiling is not seven degrees in $fileName"
            Assert-Release ($config -match '(?m)^AnnounceMilestonesInGame\s*=\s*true\s*$') `
                "Production AIRLIFT milestone announcements are not enabled in $fileName"
            Assert-Release ($config -notmatch '(?m)^AnnounceOperationsInGame\s*=') `
                "Obsolete all-or-nothing AIRLIFT messaging setting remains in $fileName"
            Assert-Release ($config -match '(?m)^VerboseLogging\s*=\s*false\s*$') `
                "Production verbose logging is enabled in $fileName"
            Assert-Release ($config -match '(?m)^Behavior\s*=\s*Return\s*$') `
                "Production off-map extraction is not enabled in $fileName"
            Assert-Release ($config -match '(?m)^EnableCombatDropPurchases\s*=\s*true\s*$') `
                "Public combat-drop purchases are not enabled in $fileName"
            Assert-Release ($config -match '(?m)^CombatDropCost\s*=\s*400\s*$') `
                "Public combat-drop price is not 400 million in $fileName"
            Assert-Release ($config -match '(?m)^CombatDropCooldownSeconds\s*=\s*300\s*$') `
                "Public combat-drop cooldown is not five minutes in $fileName"
            Assert-Release ($config -match '(?m)^SuppressionMount\s*=\s*AGM-68\s*$') `
                "Combat AGM-68 suppression mount is not enabled in $fileName"
            Assert-Release ($config -match '(?m)^SuppressionRange\s*=\s*8000\s*$') `
                "Combat runway suppression range is not 8000 metres in $fileName"
            Assert-Release ($config -match '(?m)^SuppressionRetrySeconds\s*=\s*8\s*$') `
                "Combat runway target deconfliction interval is not eight seconds in $fileName"
        }
    }
    finally { $archive.Dispose() }
}

$common = @(
    'BepInEx/plugins/KellysAIRLIFT/KellysAIRLIFT.dll',
    'README.md',
    'INSTALL-CHECKLIST.md',
    'SHA256SUMS.txt'
)
Test-AirliftZip 'KellysAIRLIFT-0.14.0-NO034.zip' `
    ($common + @(
        'examples/kelly.nuclearoption.airlift.default.cfg',
        'examples/kelly.nuclearoption.airlift.server.cfg',
        'examples/kelly.nuclearoption.airlift.zones.tsv',
        'PUBLIC-RELEASE-CHECKLIST.md',
        'CHANGELOG.md'
    )) $false $false $false
Test-AirliftZip 'KellysAIRLIFT-0.14.0-SERVER-NO034.zip' `
    ($common + @(
        'BepInEx/config/kelly.nuclearoption.airlift.cfg',
        'BepInEx/config/kelly.nuclearoption.airlift.zones.tsv',
        'SERVER-INSTALL.txt'
    )) $true $true $true
Test-AirliftZip 'KellysAIRLIFT-0.14.0-LOCAL-TEST-NO034.zip' `
    ($common + @(
        'BepInEx/config/kelly.nuclearoption.airlift.cfg',
        'BepInEx/config/kelly.nuclearoption.airlift.zones.tsv',
        'LOCAL-TEST.txt'
    )) $true $true $false

function Test-PublicUiZip([string]$fileName) {
    $path = Join-Path $artifacts $fileName
    Assert-Release (Test-Path -LiteralPath $path -PathType Leaf) "Missing package: $fileName"
    $archive = [System.IO.Compression.ZipFile]::OpenRead($path)
    try {
        $entries = @{}
        foreach ($entry in $archive.Entries) {
            Assert-Release (-not $entry.FullName.Contains('\')) "Backslash ZIP path in $fileName"
            Assert-Release (-not $entry.FullName.StartsWith('/')) "Absolute ZIP path in $fileName"
            Assert-Release ($entry.FullName -notmatch '(^|/)\.\.(/|$)') "Traversal ZIP path in $fileName"
            $key = $entry.FullName.ToLowerInvariant()
            Assert-Release (-not $entries.ContainsKey($key)) "Duplicate ZIP path in ${fileName}: $($entry.FullName)"
            $entries[$key] = $entry
            $leaf = [System.IO.Path]::GetFileName($entry.FullName)
            Assert-Release ($leaf -notmatch '(?i)^(Assembly-CSharp|Mirage|UnityEngine.*|BepInEx|0Harmony)\.dll$') `
                "Proprietary/runtime dependency entered ${fileName}: $leaf"
            Assert-Release ($leaf -notmatch '(?i)\.(nobp|pdb)$') "Content/debug file entered ${fileName}: $leaf"
        }
        foreach ($required in @(
            'BepInEx/plugins/KellysAIRLIFTPublicUI/KellysAIRLIFTPublicUI.dll',
            'README.md', 'PUBLIC-UI-INSTALL.txt', 'SHA256SUMS.txt')) {
            Assert-Release ($entries.ContainsKey($required.ToLowerInvariant())) `
                "Missing $required from $fileName"
        }
        $publicUiDll = $entries['bepinex/plugins/kellysairliftpublicui/kellysairliftpublicui.dll']
        Assert-Release ((Get-ZipEntryHash $publicUiDll) -eq
            (Get-FileHash -LiteralPath $expectedPublicUiDll -Algorithm SHA256).Hash) `
            "Stale public-purchase UI DLL in $fileName"
        Assert-Release (-not $entries.ContainsKey('bepinex/plugins/kellysairlift/kellysairlift.dll')) `
            "Server AIRLIFT DLL entered public client-UI package $fileName"
    }
    finally { $archive.Dispose() }
}

Test-PublicUiZip 'KellysAIRLIFT-0.14.0-PUBLIC-UI-NO034.zip'

function Test-SourceZip([string]$fileName) {
    $path = Join-Path $artifacts $fileName
    Assert-Release (Test-Path -LiteralPath $path -PathType Leaf) "Missing package: $fileName"
    $archive = [System.IO.Compression.ZipFile]::OpenRead($path)
    try {
        $entries = @{}
        foreach ($entry in $archive.Entries) {
            Assert-Release (-not $entry.FullName.Contains('\')) "Backslash ZIP path in $fileName"
            Assert-Release (-not $entry.FullName.StartsWith('/')) "Absolute ZIP path in $fileName"
            Assert-Release ($entry.FullName -notmatch '(^|/)\.\.(/|$)') "Traversal ZIP path in $fileName"
            $key = $entry.FullName.ToLowerInvariant()
            Assert-Release (-not $entries.ContainsKey($key)) "Duplicate ZIP path in ${fileName}: $($entry.FullName)"
            $entries[$key] = $entry
            Assert-Release ($entry.FullName -notmatch '(?i)(^|/)(bin|obj|artifacts|backups|research)(/|$)') `
                "Private/build directory entered ${fileName}: $($entry.FullName)"
            Assert-Release ($entry.FullName -notmatch '(?i)kellysairliftowner') `
                "Retired owner-client source entered ${fileName}: $($entry.FullName)"
            Assert-Release ($entry.FullName -notmatch '(?i)\.(dll|pdb|nobp|zip|log)$') `
                "Binary/content/log file entered ${fileName}: $($entry.FullName)"
        }
        foreach ($required in @(
            'Plugin.cs', 'Models.cs', 'AirliftPolicy.cs', 'AirliftTransportState.cs',
            'NativeDefensiveCountermeasures.cs', 'KellysAIRLIFT.csproj',
            'KellysAIRLIFT.Tests/Program.cs',
            'KellysAIRLIFTPublicUI/Plugin.cs',
            '.github/workflows/policy-tests.yml',
            '.github/ISSUE_TEMPLATE/bug_report.yml',
            'docs/RELEASE-0.14.0.md', 'README.md', 'SECURITY.md',
            'CONTRIBUTING.md', 'SHA256SUMS.txt')) {
            Assert-Release ($entries.ContainsKey($required.ToLowerInvariant())) `
                "Missing $required from $fileName"
        }
        foreach ($configName in @(
            'config/kelly.nuclearoption.airlift.cfg',
            'config/kelly.nuclearoption.airlift.server.cfg',
            'config/kelly.nuclearoption.airlift.local-test.cfg')) {
            $configText = Get-ZipEntryText $entries[$configName]
            Assert-Release ($configText -match '(?m)^OwnerSteamId\s*=\s*0\s*$') `
                "Public source config grants a manual-command identity: $configName"
        }
    }
    finally { $archive.Dispose() }
}

Test-SourceZip 'KellysAIRLIFT-0.14.0-SOURCE.zip'

$zoneLines = Get-Content -LiteralPath (Join-Path $projectRoot 'config\kelly.nuclearoption.airlift.zones.tsv') |
    Where-Object { $_ -and -not $_.StartsWith('#') }
Assert-Release ($zoneLines.Count -eq 18) 'Expected exactly 18 prevalidated Terrain1 zones.'
$zoneKeys = @{}
foreach ($line in $zoneLines) {
    $parts = $line.Split("`t")
    Assert-Release ($parts.Count -eq 10) "Malformed zone line: $line"
    Assert-Release ($parts[1] -eq 'Terrain1') "Unexpected public zone map: $($parts[1])"
    Assert-Release ($parts[3] -in @('BDF', 'PALA')) "Unexpected public zone faction: $($parts[3])"
    Assert-Release ($parts[4] -eq 'GLOBAL' -and $parts[5] -eq 'APPROACH_HEADING') `
        "Legacy coordinate convention in public zone: $($parts[0])"
    Assert-Release (-not $zoneKeys.ContainsKey($parts[0])) "Duplicate public zone: $($parts[0])"
    $zoneKeys[$parts[0]] = $true
}

Write-Host "PASS: $checks AIRLIFT archive, dependency, config, freshness, and zone checks."
