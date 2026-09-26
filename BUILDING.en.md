# Build and validation

[Deutsch](BUILDING.md)

## Requirements

- .NET SDK with the .NET 6 targeting pack (checked with SDK 10.0.302).
- PowerShell 7.
- A local MelonLoader game copy with references under `MelonLoader/net6` and `MelonLoader/Il2CppAssemblies`.
- A local BepInEx 6 game copy (Unity IL2CPP x64) with references under `BepInEx/core` and `BepInEx/interop`.
- Both copies must have been started once so the loader has generated its interop assemblies.

The repository contains no game or loader files and no local path. `GameReferences.props` reads game, Unity, Harmony, IL2CPP and loader assemblies from the copy you pass in; every reference has `Private=false` and never ends up in a package.

## Build and check both loaders

```powershell
$MelonGameDir = Read-Host 'Path to the MelonLoader game copy'
$BepInExGameDir = Read-Host 'Path to the BepInEx game copy'
pwsh -File .\Build-Release.ps1 -ValidateOnly `
  -MelonGameDir $MelonGameDir `
  -BepInExGameDir $BepInExGameDir
```

The script reads the version from `Directory.Build.props`, restores offline through the empty-source `NuGet.Config`, and builds Core and its adapter for each loader. It then runs the deterministic offline checks in `Tests/Core`. Finally it stages the exact package file list for each loader in a temporary folder and checks it: exact paths, no game or loader files, no local paths inside the DLLs. The script creates no ZIP, installs nothing and publishes nothing.

Outputs: `bin/Melon/Release/net6.0/` and `bin/BepInEx/Release/net6.0/`. Builds are deterministic.

## Package layout

| Package | MelonLoader | BepInEx |
| --- | --- | --- |
| Core | adapter `DungeonSettlersDelvers.Core.MelonLoader.dll` in `Mods/`, `DungeonSettlersDelvers.Core.dll` in `UserLibs/` | both DLLs in `BepInEx/plugins/DungeonSettlersDelvers/` |

The package version is `0.2.0`; the loader-neutral Core API is `1.1.0`. The MelonLoader adapter assembly is `DungeonSettlersDelvers.Core.MelonLoader`; character packs declare it through `MelonAdditionalDependencies`. The BepInEx plugin ID is `fre-yin.dungeonsettlers.delvers.core`.
