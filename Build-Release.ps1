#Requires -Version 7.0
[CmdletBinding()]
param(
    [string]$Version = '0.4.0',
    [string]$MelonGameDir = '',
    [string]$BepInExGameDir = '',
    [switch]$ValidateOnly
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
if ([string]::IsNullOrWhiteSpace($MelonGameDir) -or [string]::IsNullOrWhiteSpace($BepInExGameDir)) {
    throw 'Pass both -MelonGameDir and -BepInExGameDir using initialized local game copies. Steam is never modified.'
}

$nuget = Join-Path $root 'NuGet.Config'
$coreProject = Join-Path $root 'Core\DungeonSettlersDelvers.Core.csproj'
$frierenProject = Join-Path $root 'Frieren\DungeonSettlersDelvers.Frieren.csproj'
# The public Core repository ships without the optional Frieren pack; validate whatever is present.
$hasFrieren = Test-Path -LiteralPath $frierenProject -PathType Leaf
$packages = if ($hasFrieren) { @('Core','Frieren') } else { @('Core') }
$testsProjects = @(Join-Path $root 'Tests\Core\DungeonSettlersDelvers.Core.Tests.csproj')
if ($hasFrieren) { $testsProjects += Join-Path $root 'Tests\Frieren\FrierenPortrait.Tests.csproj' }
$loaderProjects = @{
    Melon = @{
        GameDir = [IO.Path]::GetFullPath($MelonGameDir)
        CoreAdapter = Join-Path $root 'Adapters\Melon\Core\DungeonSettlersDelvers.Core.MelonLoader.csproj'
        FrierenAdapter = Join-Path $root 'Adapters\Melon\Frieren\DungeonSettlersDelvers.Frieren.MelonLoader.csproj'
    }
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

function Assert-NoForbiddenPackageEntry([string]$EntryName) {
    $normalized = $EntryName.Replace('\', '/')
    $forbidden = '(?i)(^|/)(GameAssembly\.dll|global-metadata\.dat|Assembly-CSharp\.dll|Il2Cppmscorlib\.dll|Il2CppSystem\.Core\.dll|Il2CppNewtonsoft\.Json\.dll|Newtonsoft\.Json\.dll|MelonLoader\.dll|BepInEx[^/]*\.dll|0Harmony\.dll|Il2CppInterop\.Runtime\.dll|UnityEngine[^/]*\.dll|Unity\.TextMeshPro\.dll|SemanticVersioning\.dll|DungeonSettlersFrierenPortrait\.dll|[^/]+\.pdb|[^/]+\.deps\.json)$|(^|/)(Steam|steamapps|userdata|UserData|Saves|SaveGames)(/|$)|\.(sav|save|savegame|log|dmp|crash)$'
    if ($normalized -match $forbidden) { throw "Forbidden game, loader, legacy, or user-data package entry: $normalized" }
}

function Assert-PngDimensions([string]$Path, [int]$Size) {
    $bytes = [IO.File]::ReadAllBytes($Path)
    $signature = [byte[]](137,80,78,71,13,10,26,10)
    if ($bytes.Length -lt 24) { throw "Runtime PNG is too short: $Path" }
    $hasSignature = $true
    for ($index = 0; $index -lt $signature.Length; $index++) {
        if ($bytes[$index] -ne $signature[$index]) { $hasSignature = $false; break }
    }
    $width = [Net.IPAddress]::NetworkToHostOrder([BitConverter]::ToInt32($bytes,16))
    $height = [Net.IPAddress]::NetworkToHostOrder([BitConverter]::ToInt32($bytes,20))
    if (-not $hasSignature -or $width -ne $Size -or $height -ne $Size) {
        throw "Runtime PNG is invalid or has unexpected dimensions ($Size x $Size): $Path"
    }
}

function New-PackagePlan([string]$Loader, [string]$Package) {
    if ($Package -notin @('Core','Frieren')) { throw "Unknown package plan: $Package" }
    $bin = Join-Path $root "bin\$Loader\Release\net6.0"
    $files = @(
        [pscustomobject]@{ Source = Join-Path $root 'BITTE ZUERST LESEN.txt'; Entry = 'BITTE ZUERST LESEN.txt' }
    )
    if ($Loader -eq 'Melon') {
        if ($Package -eq 'Core') {
            $files += @(
                [pscustomobject]@{ Source = Join-Path $bin 'DungeonSettlersDelvers.Core.MelonLoader.dll'; Entry = 'Mods/DungeonSettlersDelvers.Core.MelonLoader.dll' }
                [pscustomobject]@{ Source = Join-Path $bin 'DungeonSettlersDelvers.Core.dll'; Entry = 'UserLibs/DungeonSettlersDelvers.Core.dll' }
            )
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
        foreach ($name in @('Frieren_Normal.png','Frieren_Stress.png','Elfische_Erzmagierin.png','Booklover.png')) {
            $files += [pscustomobject]@{ Source = Join-Path $root "Frieren\Assets\$name"; Entry = "$assetRoot/$name" }
        }
    }
    return ,$files
}

function Assert-PackagePlan([object[]]$Plan, [string]$Loader, [string]$Package, [switch]$Staged, [string]$BaseDirectory) {
    $expectedEntries = @('BITTE ZUERST LESEN.txt')
    if ($Loader -eq 'Melon' -and $Package -eq 'Core') {
        $expectedEntries += @('Mods/DungeonSettlersDelvers.Core.MelonLoader.dll','UserLibs/DungeonSettlersDelvers.Core.dll')
    }
    elseif ($Loader -eq 'Melon') {
        $expectedEntries += @('Mods/DungeonSettlersDelvers.Frieren.MelonLoader.dll','UserLibs/DungeonSettlersDelvers.Frieren.dll',
            'Mods/DungeonSettlersDelvers/Frieren/Frieren_Normal.png','Mods/DungeonSettlersDelvers/Frieren/Frieren_Stress.png',
            'Mods/DungeonSettlersDelvers/Frieren/Elfische_Erzmagierin.png','Mods/DungeonSettlersDelvers/Frieren/Booklover.png')
    }
    elseif ($Package -eq 'Core') {
        $expectedEntries += @('BepInEx/plugins/DungeonSettlersDelvers/DungeonSettlersDelvers.Core.BepInEx.dll',
            'BepInEx/plugins/DungeonSettlersDelvers/DungeonSettlersDelvers.Core.dll')
    }
    else {
        $expectedEntries += @('BepInEx/plugins/DungeonSettlersDelvers/DungeonSettlersDelvers.Frieren.BepInEx.dll',
            'BepInEx/plugins/DungeonSettlersDelvers/DungeonSettlersDelvers.Frieren.dll',
            'BepInEx/plugins/DungeonSettlersDelvers/Frieren/Frieren_Normal.png',
            'BepInEx/plugins/DungeonSettlersDelvers/Frieren/Frieren_Stress.png',
            'BepInEx/plugins/DungeonSettlersDelvers/Frieren/Elfische_Erzmagierin.png',
            'BepInEx/plugins/DungeonSettlersDelvers/Frieren/Booklover.png')
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
        if ($file.Entry -like '*.png') {
            $size = if ($file.Entry.EndsWith('Elfische_Erzmagierin.png') -or $file.Entry.EndsWith('Booklover.png')) { 24 } else { 48 }
            Assert-PngDimensions $path $size
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
    foreach ($entry in @('Mods/GameAssembly.dll','BepInEx/plugins/BepInEx.Core.dll','Mods/DungeonSettlersFrierenPortrait.dll')) {
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
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('Delvers-loader-matrix-' + [guid]::NewGuid().ToString('N'))
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
    foreach ($loader in @('Melon','BepInEx')) {
        $settings = $loaderProjects[$loader]
        $game = $settings.GameDir
        $buildProjects = @($coreProject,$settings.CoreAdapter)
        if ($hasFrieren) { $buildProjects += @($frierenProject,$settings.FrierenAdapter) }
        foreach ($project in @($buildProjects + $testsProjects)) {
            Invoke-Dotnet "$loader restore $([IO.Path]::GetFileName($project))" @(
                'restore', $project, '--configfile', $nuget,
                "-p:RestoreConfigFile=$nuget", '-p:NuGetAudit=false', "-p:LoaderProfile=$loader", "-p:GameDir=$game"
            )
        }
        foreach ($project in $buildProjects) {
            Invoke-Dotnet "$loader Release build $([IO.Path]::GetFileName($project))" @(
                'build', $project, '-c', 'Release', '--no-restore', '-t:Rebuild',
                "-p:RestoreConfigFile=$nuget", "-p:LoaderProfile=$loader", "-p:GameDir=$game"
            )
        }
        $outputDirectory = Join-Path $root "bin\$loader\Release\net6.0"
        $pdbFiles = @(Get-ChildItem -LiteralPath $outputDirectory -Filter '*.pdb' -File -Recurse -ErrorAction SilentlyContinue)
        if ($pdbFiles.Count -gt 0) { throw "$loader Release output unexpectedly contains PDB files: $($pdbFiles.Name -join ', ')" }
        foreach ($testsProject in $testsProjects) {
            Invoke-Dotnet "$loader offline rule tests $([IO.Path]::GetFileName($testsProject))" @(
                'run', '--project', $testsProject, '-c', 'Release', '--no-restore',
                "-p:RestoreConfigFile=$nuget", "-p:LoaderProfile=$loader"
            )
        }

        foreach ($package in $packages) {
            $plan = New-PackagePlan $loader $package
            Assert-PackagePlan $plan $loader $package
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
    "PASS: all $(2 * $packages.Count) loader/package plans validated; no archive was created and nothing was installed or published."
}
finally {
    foreach ($name in $previousEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name], 'Process')
    }
    $resolvedTemp = [IO.Path]::GetFullPath($tempRoot)
    $allowedTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if (!$resolvedTemp.StartsWith($allowedTemp, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Matrix temp directory resolved outside the system temp root.'
    }
    if (Test-Path -LiteralPath $resolvedTemp) { [IO.Directory]::Delete($resolvedTemp, $true) }
}
