#Requires -Version 7.0
[CmdletBinding()]
param(
    [string]$Version = '0.5.0',
    [string]$MelonGameDir = '',
    [string]$BepInExGameDir = '',
    [switch]$ValidateOnly,
    # 0.5.0 for BepInEx only: MelonLoader is built and checked only on this explicit switch.
    [switch]$Melon
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$versionProps = [xml](Get-Content -LiteralPath (Join-Path $root 'Directory.Build.props') -Raw)
$versionNodes = $versionProps.SelectNodes('/Project/PropertyGroup/Version')
$projectVersion = if ($versionNodes.Count -eq 1) { $versionNodes[0].InnerText.Trim() } else { $null }
if ([string]::IsNullOrWhiteSpace($projectVersion)) { throw 'Could not resolve one release version from Directory.Build.props.' }
if ($Version -ne $projectVersion) { throw "Version $Version does not match Directory.Build.props ($projectVersion)." }
if (-not $ValidateOnly) {
    throw 'This script only builds and validates. Release archives are assembled separately after the in-game regression pass. Use -ValidateOnly.'
}
if ([string]::IsNullOrWhiteSpace($BepInExGameDir)) {
    throw 'Pass -BepInExGameDir with an initialized local game copy. Steam is never modified.'
}
if ($Melon -and [string]::IsNullOrWhiteSpace($MelonGameDir)) {
    throw 'With -Melon also pass -MelonGameDir with an initialized local MelonLoader game copy.'
}
if (-not $Melon -and -not [string]::IsNullOrWhiteSpace($MelonGameDir)) {
    throw '-MelonGameDir is only used with -Melon (0.5.0 is BepInEx only).'
}
$loaders = if ($Melon) { @('Melon', 'BepInEx') } else { @('BepInEx') }

$nuget = Join-Path $root 'NuGet.Config'
$coreProject = Join-Path $root 'Core\DungeonSettlersDelvers.Core.csproj'
$frierenProject = Join-Path $root 'Frieren\DungeonSettlersDelvers.Frieren.csproj'
# The public Core repository ships without the optional Frieren pack; validate whatever is present.
$hasFrieren = Test-Path -LiteralPath $frierenProject -PathType Leaf
$packages = if ($hasFrieren) { @('Core','Frieren') } else { @('Core') }
$testsProjects = @(Join-Path $root 'Tests\Core\DungeonSettlersDelvers.Core.Tests.csproj')
if ($hasFrieren) { $testsProjects += Join-Path $root 'Tests\Frieren\FrierenPortrait.Tests.csproj' }
$loaderProjects = @{}
if ($Melon) {
    $loaderProjects.Melon = @{
        GameDir = [IO.Path]::GetFullPath($MelonGameDir)
        CoreAdapter = Join-Path $root 'Adapters\Melon\Core\DungeonSettlersDelvers.Core.MelonLoader.csproj'
        FrierenAdapter = Join-Path $root 'Adapters\Melon\Frieren\DungeonSettlersDelvers.Frieren.MelonLoader.csproj'
    }
}
$loaderProjects += @{
    BepInEx = @{
        GameDir = [IO.Path]::GetFullPath($BepInExGameDir)
        CoreAdapter = Join-Path $root 'Adapters\BepInEx\Core\DungeonSettlersDelvers.Core.BepInEx.csproj'
        FrierenAdapter = Join-Path $root 'Adapters\BepInEx\Frieren\DungeonSettlersDelvers.Frieren.BepInEx.csproj'
    }
}

foreach ($loader in $loaderProjects.Keys) {
    if (!(Test-Path -LiteralPath $loaderProjects[$loader].GameDir -PathType Container)) {
        throw "$loader GameDir is not a directory: $($loaderProjects[$loader].GameDir)"
    }
}
if (!(Test-Path -LiteralPath $nuget -PathType Leaf)) { throw "Offline NuGet config missing: $nuget" }

function Invoke-Dotnet([string]$Label, [string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Label failed with exit code $LASTEXITCODE." }
}

function Restore-Project([string]$Loader, [string]$Game, [string]$Project) {
    Invoke-Dotnet "$Loader restore $([IO.Path]::GetFileName($Project))" @(
        'restore', $Project, '--configfile', $nuget,
        "-p:RestoreConfigFile=$nuget", '-p:NuGetAudit=false', "-p:LoaderProfile=$Loader", "-p:GameDir=$Game"
    )
}

function Assert-NoPrivateBuildPath([byte[]]$Bytes, [string]$Artifact) {
    foreach ($encoding in @([Text.Encoding]::Latin1, [Text.Encoding]::Unicode, [Text.Encoding]::BigEndianUnicode)) {
        $text = $encoding.GetString($Bytes)
        foreach ($marker in @(('C:' + '\' + 'Users' + '\'), 'Game Copies')) {
            if ($text.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                throw "Local user or game-copy path found in package file: $Artifact"
            }
        }
    }
}

# Private release checks, not part of the public sources: they check every staged file and the public tree, and test
# themselves. The canonical project (it holds Export-Public.ps1) never builds without them; a public copy builds without.
$privateChecks = Join-Path $PSScriptRoot 'Private-Checks.ps1'
$hasPrivateChecks = Test-Path -LiteralPath $privateChecks
if (-not $hasPrivateChecks -and (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'Export-Public.ps1'))) {
    throw 'Private-Checks.ps1 is missing in the canonical project; the release build stops.'
}
if ($hasPrivateChecks) {
    . $privateChecks
    $publicEntries = Test-PrivateExportTree $PSScriptRoot
    Write-Host "PASS: public tree checked ($publicEntries entries)"
}

function Assert-NoForbiddenPackageEntry([string]$EntryName) {
    $normalized = $EntryName.Replace('\', '/')
    $forbidden = '(?i)(^|/)(GameAssembly\.dll|global-metadata\.dat|Assembly-CSharp\.dll|Il2Cppmscorlib\.dll|Il2CppSystem\.Core\.dll|Il2CppNewtonsoft\.Json\.dll|Newtonsoft\.Json\.dll|MelonLoader\.dll|BepInEx[^/]*\.dll|0Harmony\.dll|Il2CppInterop\.Runtime\.dll|UnityEngine[^/]*\.dll|Unity\.TextMeshPro\.dll|SemanticVersioning\.dll|DungeonSettlersFrierenPortrait\.dll|[^/]+\.pdb|[^/]+\.deps\.json|[^/]+\.dev\.cfg)$|(^|/)(Steam|steamapps|userdata|UserData|Saves|SaveGames)(/|$)|\.(sav|save|savegame|log|dmp|crash)$'
    if ($normalized -match $forbidden) { throw "Forbidden game, loader, legacy, or user-data package entry: $normalized" }
}

# Every PNG of the Frieren package with its exact width and height. Plan, allowlist and size check all
# read this one table (Delvers Frieren, the six Zoltraak images were missing from the ZIP).
$FrierenAssets = [ordered]@{
    'Frieren_Normal.png' = @(48, 48)
    'Frieren_Stress.png' = @(48, 48)
    'Elfische_Erzmagierin.png' = @(24, 24)
    'Booklover.png' = @(24, 24)
    'Zoltraak_Icon.png' = @(32, 32)
    'Zoltraak_Bruch_Symbol_16x16.png' = @(16, 16)
    'Zoltraak_Zauberkreis_32x32_5Bilder.png' = @(160, 32)
    'Zoltraak_Castimpuls_48x48_8Bilder.png' = @(384, 48)
    'Zoltraak_Strahlsegment_32x12_8Bilder.png' = @(256, 12)
    'Zoltraak_Lichtpunkt_17x17_4Bilder.png' = @(68, 17)
    'Zoltraak_Endring_8x20_4Bilder.png' = @(32, 20)
    'Zoltraak_Einschlag_64x64_4Bilder.png' = @(256, 64)
}

# Zoltraak's sounds in <Frieren>/Sounds, all PCM 16 bit, stereo, 48 kHz. The release plays exactly
# these; without them Zoltraak stays silent (Delvers Frieren).
$FrierenSounds = @('Zoltraak_Wirken.wav', 'Zoltraak_Abschuss.wav', 'Zoltraak_Dauerton.wav')

# Core's own pictures in <package>/Core (CoreAssets), the same way: every PNG in Core\Assets with its size. The body
# blood frames are WoundBodyArtRules.Width x Height (64x40).
$CoreAssets = [ordered]@{
    'Aderlass_Effektsymbol_32x32.png' = @(32, 32)
    'Blutwaechter_Effektsymbol_32x32.png' = @(32, 32)
    'Falkenaugen_Effektsymbol_32x32.png' = @(32, 32)
    'Wildwuchs_Effektsymbol_32x32.png' = @(32, 32)
    'Lifebloom_Zaubersymbol_32x32.png' = @(32, 32)
    'Lifebloom_Effektsymbol_16x16.png' = @(16, 16)
    'Lifebloom_Zaubersymbol_32x32_0.png' = @(32, 32)
    'Lifebloom_Zaubersymbol_32x32_1.png' = @(32, 32)
    'Lifebloom_Zaubersymbol_32x32_2.png' = @(32, 32)
    'Verband_leicht_16x16.png' = @(16, 16)
    'Verband_schwer_16x16.png' = @(16, 16)
    'Verband_Kraeuter_16x16.png' = @(16, 16)
    'Verband_Salbe_16x16.png' = @(16, 16)
    'Lifebloom_Skillbuch_16x16.png' = @(16, 16)
    'Banditen_Kettenzug_32x32.png' = @(32, 32)
    'SerratedBlade_32x32.png' = @(32, 32)
    'Koerperblut_leicht_f01.png' = @(64, 40)
    'Koerperblut_leicht_f02.png' = @(64, 40)
    'Koerperblut_leicht_f03.png' = @(64, 40)
    'Koerperblut_leicht_f04.png' = @(64, 40)
    'Koerperblut_leicht_f05.png' = @(64, 40)
    'Koerperblut_tief_f01.png' = @(64, 40)
    'Koerperblut_tief_f02.png' = @(64, 40)
    'Koerperblut_tief_f03.png' = @(64, 40)
    'Koerperblut_tief_f04.png' = @(64, 40)
    'Koerperblut_tief_f05.png' = @(64, 40)
    'Koerperblut_toedlich_f01.png' = @(64, 40)
    'Koerperblut_toedlich_f02.png' = @(64, 40)
    'Koerperblut_toedlich_f03.png' = @(64, 40)
    'Koerperblut_toedlich_f04.png' = @(64, 40)
    'Koerperblut_toedlich_f05.png' = @(64, 40)
    'Wasserfall_Sheet_384x80.png' = @(384, 80)
    'Bruecke_unter_Figuren_64x32.png' = @(64, 32)
    'Bruecke_ueber_Figuren_64x32.png' = @(64, 32)
    'Formation_Knopf_Aus_20x23.png' = @(20, 23)
    'Formation_Knopf_An_20x23.png' = @(20, 23)
    'Formation_Krone_7x5.png' = @(7, 5)
    # The guide topics' own pictures (GuideTopicText.ImageFile), screenshots of the game at the guide picture's size.
    'Guide_9001.png' = @(796, 284)
    'Guide_9004.png' = @(796, 284)
    'Guide_9008.png' = @(796, 284)
    'Guide_9009.png' = @(796, 284)
}

# Core's own sounds in <package>/Core/Sounds (CoreSound; Lifebloom's, LifebloomSoundRules), checked like Frieren's.
$CoreSounds = @('Lifebloom_Wirken.wav', 'Lifebloom_Aufbluehen.wav')

function Get-PackageAssetEntries([string]$Package, [string]$AssetRoot) {
    if ($Package -eq 'Core') { return @($CoreAssets.Keys | ForEach-Object { "$AssetRoot/$_" }) + @($CoreSounds | ForEach-Object { "$AssetRoot/Sounds/$_" }) }
    return @($FrierenAssets.Keys | ForEach-Object { "$AssetRoot/$_" }) + @($FrierenSounds | ForEach-Object { "$AssetRoot/Sounds/$_" })
}

# The Core image plan and Core\Assets name the same PNGs, so no picture is left out of a package.
function Assert-CoreAssetsListed {
    $onDisk = @(Get-ChildItem -LiteralPath (Join-Path $root 'Core\Assets') -Filter '*.png' -File | ForEach-Object Name)
    $unlisted = @($onDisk | Where-Object { -not $CoreAssets.Contains($_) })
    $missing = @($CoreAssets.Keys | Where-Object { $_ -notin $onDisk })
    if ($unlisted.Count -gt 0 -or $missing.Count -gt 0) {
        throw "Core image plan differs from Core\Assets. Not in the plan: $($unlisted -join ', '); no file: $($missing -join ', ')"
    }
}

function Assert-PngDimensions([string]$Path, [int]$Width, [int]$Height) {
    $bytes = [IO.File]::ReadAllBytes($Path)
    $signature = [byte[]](137,80,78,71,13,10,26,10)
    if ($bytes.Length -lt 24) { throw "Runtime PNG is too short: $Path" }
    $hasSignature = $true
    for ($index = 0; $index -lt $signature.Length; $index++) {
        if ($bytes[$index] -ne $signature[$index]) { $hasSignature = $false; break }
    }
    # PowerShell variables ignore case: the file's size must not be called $width or $height, they would overwrite the parameters.
    $actualWidth = [Net.IPAddress]::NetworkToHostOrder([BitConverter]::ToInt32($bytes,16))
    $actualHeight = [Net.IPAddress]::NetworkToHostOrder([BitConverter]::ToInt32($bytes,20))
    if (-not $hasSignature -or $actualWidth -ne $Width -or $actualHeight -ne $Height) {
        throw "Runtime PNG is invalid or has unexpected dimensions (expected $Width x $Height, found $actualWidth x $actualHeight): $Path"
    }
}

# Self-probe, once per run: one picture of the Core plan is checked against a wrong size and the check has to throw.
# If it does not, the size comparison is dead and the build stops before it trusts the package check.
function Test-PngDimensionCheck {
    $name = @($CoreAssets.Keys)[0]
    $path = Join-Path $root "Core\Assets\$name"
    $threw = $false
    try { Assert-PngDimensions $path ($CoreAssets[$name][0] + 1) $CoreAssets[$name][1] }
    catch { $threw = $true }
    if (-not $threw) { throw "PNG size check is not effective: $name passed against a wrong expected size." }
}

function Assert-WavFormat([string]$Path) {
    $bytes = [IO.File]::ReadAllBytes($Path)
    $ascii = [Text.Encoding]::ASCII
    if ($bytes.Length -lt 44 -or $ascii.GetString($bytes, 0, 4) -ne 'RIFF' -or $ascii.GetString($bytes, 8, 4) -ne 'WAVE') {
        throw "Runtime WAV is not a RIFF WAVE file: $Path"
    }
    $offset = 12
    while ($offset + 8 -le $bytes.Length) {
        $id = $ascii.GetString($bytes, $offset, 4)
        $size = [BitConverter]::ToInt32($bytes, $offset + 4)
        if ($id -eq 'fmt ') {
            $format = [BitConverter]::ToUInt16($bytes, $offset + 8)
            $channels = [BitConverter]::ToUInt16($bytes, $offset + 10)
            $rate = [BitConverter]::ToInt32($bytes, $offset + 12)
            $bits = [BitConverter]::ToUInt16($bytes, $offset + 22)
            if ($format -ne 1 -or $channels -ne 2 -or $rate -ne 48000 -or $bits -ne 16) {
                throw "Runtime WAV must be PCM 16 bit, stereo, 48 kHz (format $format, $channels ch, $rate Hz, $bits bit): $Path"
            }
            return
        }
        if ($size -lt 0) { break }
        $offset += 8 + $size + ($size % 2)
    }
    throw "Runtime WAV has no fmt chunk: $Path"
}

# The plan of one loader and package; -Tree names the tree it reads (the project, or a mirror of the public tree for the
# private check Assert-PublicPackageSources).
function New-PackagePlan([string]$Loader, [string]$Package, [string]$Tree = $PSScriptRoot) {
    if ($Package -notin @('Core','Frieren')) { throw "Unknown package plan: $Package" }
    $bin = Join-Path $Tree "bin\$Loader\Release\net6.0"
    # The package's own readme: in this project under Public\<Package>, in the public tree at the top (the export copies
    # Public\<Package>\ there).
    $readme = Join-Path $Tree "Public\$Package\BITTE ZUERST LESEN.txt"
    if (!(Test-Path -LiteralPath $readme -PathType Leaf)) { $readme = Join-Path $Tree 'BITTE ZUERST LESEN.txt' }
    $files = @(
        [pscustomobject]@{ Source = $readme; Entry = 'BITTE ZUERST LESEN.txt' }
    )
    if ($Loader -eq 'Melon') {
        if ($Package -eq 'Core') {
            $files += @(
                [pscustomobject]@{ Source = Join-Path $bin 'DungeonSettlersDelvers.Core.MelonLoader.dll'; Entry = 'Mods/DungeonSettlersDelvers.Core.MelonLoader.dll' }
                [pscustomobject]@{ Source = Join-Path $bin 'DungeonSettlersDelvers.Core.dll'; Entry = 'UserLibs/DungeonSettlersDelvers.Core.dll' }
            )
            $assetRoot = 'Mods/DungeonSettlersDelvers/Core'
        }
        else {
            $files += @(
                [pscustomobject]@{ Source = Join-Path $bin 'DungeonSettlersDelvers.Frieren.MelonLoader.dll'; Entry = 'Mods/DungeonSettlersDelvers.Frieren.MelonLoader.dll' }
                [pscustomobject]@{ Source = Join-Path $bin 'DungeonSettlersDelvers.Frieren.dll'; Entry = 'UserLibs/DungeonSettlersDelvers.Frieren.dll' }
            )
            $assetRoot = 'Mods/DungeonSettlersDelvers/Frieren'
        }
    }
    else {
        $pluginRoot = 'BepInEx/plugins/DungeonSettlersDelvers'
        if ($Package -eq 'Core') {
            $files += @(
                [pscustomobject]@{ Source = Join-Path $bin 'DungeonSettlersDelvers.Core.BepInEx.dll'; Entry = "$pluginRoot/DungeonSettlersDelvers.Core.BepInEx.dll" }
                [pscustomobject]@{ Source = Join-Path $bin 'DungeonSettlersDelvers.Core.dll'; Entry = "$pluginRoot/DungeonSettlersDelvers.Core.dll" }
            )
            $assetRoot = "$pluginRoot/Core"
        }
        else {
            $files += @(
                [pscustomobject]@{ Source = Join-Path $bin 'DungeonSettlersDelvers.Frieren.BepInEx.dll'; Entry = "$pluginRoot/DungeonSettlersDelvers.Frieren.BepInEx.dll" }
                [pscustomobject]@{ Source = Join-Path $bin 'DungeonSettlersDelvers.Frieren.dll'; Entry = "$pluginRoot/DungeonSettlersDelvers.Frieren.dll" }
            )
            $assetRoot = "$pluginRoot/Frieren"
        }
    }
    if ($Package -eq 'Frieren') {
        foreach ($name in $FrierenAssets.Keys) {
            $files += [pscustomobject]@{ Source = Join-Path $Tree "Frieren\Assets\$name"; Entry = "$assetRoot/$name" }
        }
        foreach ($name in $FrierenSounds) {
            $files += [pscustomobject]@{ Source = Join-Path $Tree "Frieren\Assets\Sounds\$name"; Entry = "$assetRoot/Sounds/$name" }
        }
    }
    else {
        foreach ($name in $CoreAssets.Keys) {
            $files += [pscustomobject]@{ Source = Join-Path $Tree "Core\Assets\$name"; Entry = "$assetRoot/$name" }
        }
        foreach ($name in $CoreSounds) {
            $files += [pscustomobject]@{ Source = Join-Path $Tree "Core\Assets\Sounds\$name"; Entry = "$assetRoot/Sounds/$name" }
        }
    }
    return ,$files
}

function Assert-PackagePlan([object[]]$Plan, [string]$Loader, [string]$Package, [switch]$Staged, [string]$BaseDirectory) {
    $expectedEntries = @('BITTE ZUERST LESEN.txt')
    if ($Loader -eq 'Melon' -and $Package -eq 'Core') {
        $expectedEntries += @('Mods/DungeonSettlersDelvers.Core.MelonLoader.dll','UserLibs/DungeonSettlersDelvers.Core.dll')
        $expectedEntries += Get-PackageAssetEntries 'Core' 'Mods/DungeonSettlersDelvers/Core'
    }
    elseif ($Loader -eq 'Melon') {
        $expectedEntries += @('Mods/DungeonSettlersDelvers.Frieren.MelonLoader.dll','UserLibs/DungeonSettlersDelvers.Frieren.dll')
        $expectedEntries += Get-PackageAssetEntries 'Frieren' 'Mods/DungeonSettlersDelvers/Frieren'
    }
    elseif ($Package -eq 'Core') {
        $expectedEntries += @('BepInEx/plugins/DungeonSettlersDelvers/DungeonSettlersDelvers.Core.BepInEx.dll',
            'BepInEx/plugins/DungeonSettlersDelvers/DungeonSettlersDelvers.Core.dll')
        $expectedEntries += Get-PackageAssetEntries 'Core' 'BepInEx/plugins/DungeonSettlersDelvers/Core'
    }
    else {
        $expectedEntries += @('BepInEx/plugins/DungeonSettlersDelvers/DungeonSettlersDelvers.Frieren.BepInEx.dll',
            'BepInEx/plugins/DungeonSettlersDelvers/DungeonSettlersDelvers.Frieren.dll')
        $expectedEntries += Get-PackageAssetEntries 'Frieren' 'BepInEx/plugins/DungeonSettlersDelvers/Frieren'
    }
    if ($Plan.Count -ne $expectedEntries.Count) { throw "$Loader $Package package allowlist must contain exactly $($expectedEntries.Count) files." }
    $entries = @($Plan | ForEach-Object { $_.Entry })
    if (@($entries | Select-Object -Unique).Count -ne $Plan.Count) { throw "$Loader package allowlist contains duplicate entries." }
    $unexpectedEntries = @($entries | Where-Object { $_ -notin $expectedEntries })
    $missingEntries = @($expectedEntries | Where-Object { $_ -notin $entries })
    if ($unexpectedEntries.Count -gt 0 -or $missingEntries.Count -gt 0) {
        throw "$Loader $Package package contents differ from the exact allowlist."
    }

    foreach ($file in $Plan) {
        Assert-NoForbiddenPackageEntry $file.Entry
        $path = if ($Staged) { Join-Path $BaseDirectory $file.Entry.Replace('/', '\') } else { $file.Source }
        if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "$Loader $Package package file missing: $path" }
        Assert-NoPrivateBuildPath ([IO.File]::ReadAllBytes($path)) $file.Entry
        if ($hasPrivateChecks) { Assert-PrivateReleaseChecks $path $file.Entry $file.Source }
        if ($file.Entry -like '*.png') {
            $name = $file.Entry.Substring($file.Entry.LastIndexOf('/') + 1)
            $sizes = if ($Package -eq 'Core') { $CoreAssets } else { $FrierenAssets }
            if (-not $sizes.Contains($name)) { throw "$Loader $Package package has a PNG without a known size: $($file.Entry)" }
            Assert-PngDimensions $path $sizes[$name][0] $sizes[$name][1]
        }
        elseif ($file.Entry -like '*.wav') {
            Assert-WavFormat $path
        }
    }

    if ($Staged) {
        $actual = @(Get-ChildItem -LiteralPath $BaseDirectory -Recurse -File | ForEach-Object {
            $_.FullName.Substring($BaseDirectory.Length + 1).Replace('\', '/')
        })
        if ($actual.Count -ne $Plan.Count -or @($actual | Where-Object { $_ -notin $entries }).Count -gt 0) {
            throw "$Loader $Package package staging contains files outside its exact allowlist."
        }
    }
}

function Test-PackageValidator {
    $rejected = $false
    try { Assert-NoPrivateBuildPath ([Text.Encoding]::ASCII.GetBytes(('C:' + '\' + 'Users' + '\local\artifact.dll'))) 'synthetic path test' }
    catch { if ($_.Exception.Message -like 'Local user or game-copy path*') { $rejected = $true } else { throw } }
    if (!$rejected) { throw 'Private path scanner accepted a known user path.' }
    # The private release checks test themselves when they are present.
    if ($hasPrivateChecks) { Test-PrivateReleaseChecks }
    # Developer switches (Frieren.dev.cfg) never ship.
    foreach ($entry in @('Mods/GameAssembly.dll','BepInEx/plugins/BepInEx.Core.dll','Mods/DungeonSettlersFrierenPortrait.dll','BepInEx/plugins/DungeonSettlersDelvers/Frieren/Frieren.dev.cfg')) {
        $rejected = $false
        try { Assert-NoForbiddenPackageEntry $entry }
        catch { if ($_.Exception.Message -like 'Forbidden game, loader, legacy*') { $rejected = $true } else { throw } }
        if (!$rejected) { throw "Package scanner accepted forbidden entry: $entry" }
    }
}

$previousEnvironment = @{
    APPDATA = $env:APPDATA
    DOTNET_CLI_HOME = $env:DOTNET_CLI_HOME
    NUGET_PACKAGES = $env:NUGET_PACKAGES
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE = $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE
    DOTNET_CLI_TELEMETRY_OPTOUT = $env:DOTNET_CLI_TELEMETRY_OPTOUT
}
# In the full project the temp folder lies in the folder that is never deleted by a script (Private-Checks loads the helper);
# the public script keeps its own temp folder under the system temp and removes it at the end.
$tempRoot = if ($hasPrivateChecks) { Get-DeleteFolder 'build-release' } else { Join-Path ([IO.Path]::GetTempPath()) ('Delvers-loader-matrix-' + [guid]::NewGuid().ToString('N')) }
New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null
try {
    $env:APPDATA = Join-Path $tempRoot 'appdata'
    $env:DOTNET_CLI_HOME = Join-Path $tempRoot 'dotnet-home'
    if ([string]::IsNullOrWhiteSpace($env:NUGET_PACKAGES)) {
        $env:NUGET_PACKAGES = Join-Path $env:USERPROFILE '.nuget\packages'
    }
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    New-Item -ItemType Directory -Path $env:APPDATA,$env:DOTNET_CLI_HOME -Force | Out-Null

    Test-PackageValidator
    Assert-CoreAssetsListed
    Test-PngDimensionCheck
    "PASS: Core image plan matches Core\Assets ($($CoreAssets.Count) images)"
    foreach ($loader in $loaders) {
        $settings = $loaderProjects[$loader]
        $game = $settings.GameDir
        $buildProjects = @($coreProject,$settings.CoreAdapter)
        if ($hasFrieren) { $buildProjects += @($frierenProject,$settings.FrierenAdapter) }
        # The offline tests are the gate: they run first, and a failing check (exit code 1) stops the script before any
        # game project is restored or built, so nothing lands under bin\<Loader>\Release. They read no game folder.
        foreach ($testsProject in $testsProjects) { Restore-Project $loader $game $testsProject }
        foreach ($testsProject in $testsProjects) {
            Invoke-Dotnet "$loader offline rule tests $([IO.Path]::GetFileName($testsProject))" @(
                'run', '--project', $testsProject, '-c', 'Release', '--no-restore',
                "-p:RestoreConfigFile=$nuget", "-p:LoaderProfile=$loader"
            )
        }
        foreach ($project in $buildProjects) { Restore-Project $loader $game $project }
        foreach ($project in $buildProjects) {
            Invoke-Dotnet "$loader Release build $([IO.Path]::GetFileName($project))" @(
                'build', $project, '-c', 'Release', '--no-restore', '-t:Rebuild',
                "-p:RestoreConfigFile=$nuget", "-p:LoaderProfile=$loader", "-p:GameDir=$game"
            )
        }
        $outputDirectory = Join-Path $root "bin\$loader\Release\net6.0"
        $pdbFiles = @(Get-ChildItem -LiteralPath $outputDirectory -Filter '*.pdb' -File -Recurse -ErrorAction SilentlyContinue)
        if ($pdbFiles.Count -gt 0) { throw "$loader Release output unexpectedly contains PDB files: $($pdbFiles.Name -join ', ')" }

        foreach ($package in $packages) {
            $plan = New-PackagePlan $loader $package
            Assert-PackagePlan $plan $loader $package
            if ($hasPrivateChecks) {
                $publicPlan = Assert-PublicPackageSources $root $loader $package $tempRoot
                "PASS: $loader $package package plan finds its $publicPlan source files in the public tree"
            }
            $stage = Join-Path $tempRoot "staged-$loader-$package"
            New-Item -ItemType Directory -Path $stage -Force | Out-Null
            foreach ($file in $plan) {
                $destination = Join-Path $stage $file.Entry.Replace('/', '\')
                New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
                Copy-Item -LiteralPath $file.Source -Destination $destination
            }
            Assert-PackagePlan $plan $loader $package -Staged -BaseDirectory $stage
            "PASS: $loader $package package allowlist, asset paths, dependency split, private-path scan"
        }
        "PASS: $loader builds and offline rules; package plans validated: $($packages -join ', ')"
    }
    "PASS: all $($loaders.Count * $packages.Count) loader/package plans validated ($($loaders -join ', ')); no archive was created and nothing was installed or published."
}
finally {
    foreach ($name in $previousEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name], 'Process')
    }
    if (-not $hasPrivateChecks) {
        $resolvedTemp = [IO.Path]::GetFullPath($tempRoot)
        $allowedTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
        if (!$resolvedTemp.StartsWith($allowedTemp, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Matrix temp directory resolved outside the system temp root.'
        }
        if (Test-Path -LiteralPath $resolvedTemp) { [IO.Directory]::Delete($resolvedTemp, $true) }
    }
}
