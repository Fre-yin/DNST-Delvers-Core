# Dungeon Settlers Delvers: Core bauen

[English](BUILDING.en.md)

Paketversion 0.3.0 und Core-API 1.3.0 sind unterschiedliche Versionsnummern. Dieses Repository enthält nur Core. Das optionale Frieren-Paket hat eine eigene Quellfassung.

Benötigt werden PowerShell 7, ein .NET SDK mit .NET-6-Targeting-Pack und je eine gestartete Dungeon-Settlers-Kopie mit MelonLoader 0.7.3 beziehungsweise BepInEx 6 Unity IL2CPP x64. Die Loader müssen ihre Interop-Assemblies bereits erzeugt haben. Spiel, Loader und lokale Pfade sind nicht Bestandteil dieses Repositorys.

```powershell
$MelonGameDir = Read-Host 'Pfad zur MelonLoader-Spielkopie'
$BepInExGameDir = Read-Host 'Pfad zur BepInEx-Spielkopie'
pwsh -File .\Build-Release.ps1 -ValidateOnly `
  -MelonGameDir $MelonGameDir `
  -BepInExGameDir $BepInExGameDir
```

Das Skript restauriert offline mit `NuGet.Config`, baut Core und die beiden Adapter, startet die Core-Regeltests und prüft die exakten Dateilisten beider Loader-Pakete. Beim letzten vollständigen 0.3.0-Lauf bestanden 156 von 156 Core-Prüfungen je Loader; die Builds hatten null Warnungen und Fehler. `-ValidateOnly` erstellt kein ZIP, installiert nichts und veröffentlicht nichts. Der Build allein bestätigt keine Ingame-Kompatibilität.

Die Ausgaben liegen unter `bin/<Loader>/Release/net6.0/`; Zwischenstände unter `obj/<Loader>/`. Beide Ordner gehören nicht ins Git-Repository. Spiel- und Loader-Referenzen haben `Private=false` und gelangen nicht ins Paket.

Für MelonLoader enthält das Paket die Adapter-DLL unter `Mods/` und `DungeonSettlersDelvers.Core.dll` unter `UserLibs/`. Für BepInEx liegen beide DLLs unter `BepInEx/plugins/DungeonSettlersDelvers/`. In beiden Fällen kommt die Installationsnotiz hinzu. Core enthält keine Frieren-Dateien.

Die öffentliche API für Addons steht in [CORE-ADDON-LEITFADEN.md](CORE-ADDON-LEITFADEN.md).
