# Bauen und Validieren

[English](BUILDING.en.md)

## Voraussetzungen

- .NET SDK mit dem .NET-6-Targeting-Pack (geprüft mit SDK 10.0.302).
- PowerShell 7.
- Eine lokale MelonLoader-Spielkopie mit Referenzen unter `MelonLoader/net6` und `MelonLoader/Il2CppAssemblies`.
- Eine lokale BepInEx-6-Spielkopie (Unity IL2CPP x64) mit Referenzen unter `BepInEx/core` und `BepInEx/interop`.
- Beide Kopien müssen einmal gestartet worden sein, damit der Loader die Interop-Assemblies erzeugt hat.

Das Repository enthält keine Spiel- oder Loader-Dateien und keinen lokalen Pfad. `GameReferences.props` liest Spiel-, Unity-, Harmony-, IL2CPP- und Loader-Assemblies aus der übergebenen Kopie; alle Referenzen haben `Private=false` und landen nie in einem Paket.

## Beide Loader bauen und prüfen

```powershell
$MelonGameDir = Read-Host 'Pfad zur MelonLoader-Spielkopie'
$BepInExGameDir = Read-Host 'Pfad zur BepInEx-Spielkopie'
pwsh -File .\Build-Release.ps1 -ValidateOnly `
  -MelonGameDir $MelonGameDir `
  -BepInExGameDir $BepInExGameDir
```

Das Skript liest die Version aus `Directory.Build.props`, stellt offline über die leere `NuGet.Config` wieder her und baut je Loader Core und Adapter. Danach laufen die deterministischen Offline-Prüfungen aus `Tests/Core`. Zum Schluss wird je Loader die exakte Paketliste in einem temporären Ordner aufgebaut und geprüft: genaue Pfade, keine Spiel- oder Loader-Dateien, keine lokalen Pfade in den DLLs. Das Skript erstellt kein ZIP, installiert nichts und veröffentlicht nichts.

Ausgaben: `bin/Melon/Release/net6.0/` und `bin/BepInEx/Release/net6.0/`. Die Builds sind deterministisch.

## Paketaufbau

| Paket | MelonLoader | BepInEx |
| --- | --- | --- |
| Core | Adapter `DungeonSettlersDelvers.Core.MelonLoader.dll` in `Mods/`, `DungeonSettlersDelvers.Core.dll` in `UserLibs/` | beide DLLs in `BepInEx/plugins/DungeonSettlersDelvers/` |

Die Paketversion ist `0.2.0`, die loaderneutrale Core-API `1.1.0`. Der MelonLoader-Adapter heißt `DungeonSettlersDelvers.Core.MelonLoader`; Charakterpakete deklarieren ihn über `MelonAdditionalDependencies`. Das BepInEx-Plugin hat die ID `fre-yin.dungeonsettlers.delvers.core`.
