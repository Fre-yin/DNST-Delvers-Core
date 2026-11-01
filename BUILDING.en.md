# Building Dungeon Settlers Delvers: Core

[Deutsch](BUILDING.md)

Package version 0.5.0 and Core API 1.11.0 are separate version numbers. This repository contains Core only. From 0.5.0 on, Delvers is available for BepInEx only; MelonLoader is no longer supported. The optional Frieren pack has its own source distribution.

You need PowerShell 7, a .NET SDK with the .NET 6 targeting pack, and an initialized local Dungeon Settlers copy with BepInEx 6 Unity IL2CPP x64 that has generated its interop assemblies. The game, the loader, and local paths are not part of this repository.

```powershell
$BepInExGameDir = Read-Host 'Path to the BepInEx game copy'
pwsh -File .\Build-Release.ps1 -ValidateOnly -BepInExGameDir $BepInExGameDir
```

The script restores offline using `NuGet.Config`, builds Core and the BepInEx adapter, runs the Core rule tests, and validates the package's exact file list, including the size of every Core image. In the last run on the 0.5.0 state, 547 Core checks passed; the builds had zero errors. `-ValidateOnly` creates no ZIP, installs nothing, and publishes nothing. A build alone does not establish in-game compatibility.

Outputs go to `bin/<Loader>/Release/net6.0/` and intermediates to `obj/<Loader>/`. Neither directory belongs in Git. Game and loader references have `Private=false` and are not packaged.

The package holds both DLLs in `BepInEx/plugins/DungeonSettlersDelvers/`, Core's 32 images in its `Core/` folder, and the installation note. Core contains no Frieren files.

See [CORE-ADDON-LEITFADEN.md](CORE-ADDON-LEITFADEN.md) for the public addon API.
