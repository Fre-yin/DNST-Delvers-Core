# Building Dungeon Settlers Delvers: Core

[Deutsch](BUILDING.md)

Package version 0.4.0 and Core API 1.4.0 are separate version numbers. This repository contains Core only. The optional Frieren pack has its own source distribution.

You need PowerShell 7, a .NET SDK with the .NET 6 targeting pack, and initialized local Dungeon Settlers copies with MelonLoader 0.7.3 and BepInEx 6 Unity IL2CPP x64. Both loaders must have generated their interop assemblies. The game, loaders, and local paths are not part of this repository.

```powershell
$MelonGameDir = Read-Host 'Path to the MelonLoader game copy'
$BepInExGameDir = Read-Host 'Path to the BepInEx game copy'
pwsh -File .\Build-Release.ps1 -ValidateOnly `
  -MelonGameDir $MelonGameDir `
  -BepInExGameDir $BepInExGameDir
```

The script restores offline using `NuGet.Config`, builds Core and both adapters, runs the Core rule tests, and validates the exact file list for each loader package. In the last full 0.4.0 run, 168 of 168 Core checks passed per loader; the builds had zero warnings and errors. `-ValidateOnly` creates no ZIP, installs nothing, and publishes nothing. A build alone does not establish in-game compatibility.

Outputs go to `bin/<Loader>/Release/net6.0/` and intermediates to `obj/<Loader>/`. Neither directory belongs in Git. Game and loader references have `Private=false` and are not packaged.

The MelonLoader package contains its adapter DLL under `Mods/` and `DungeonSettlersDelvers.Core.dll` under `UserLibs/`. The BepInEx package contains both DLLs under `BepInEx/plugins/DungeonSettlersDelvers/`. Both include the installation note. Core contains no Frieren files.

See [CORE-ADDON-LEITFADEN.md](CORE-ADDON-LEITFADEN.md) for the public addon API.
