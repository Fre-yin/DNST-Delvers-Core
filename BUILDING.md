# Dungeon Settlers Delvers: Core bauen

[English](BUILDING.en.md)

Paketversion 0.5.0 und Core-API 1.11.0 sind unterschiedliche Versionsnummern. Dieses Repository enthält nur Core. Ab 0.5.0 gibt es Delvers nur noch für BepInEx, MelonLoader wird nicht mehr unterstützt. Das optionale Frieren-Paket hat eine eigene Quellfassung.

Benötigt werden PowerShell 7, ein .NET SDK mit .NET-6-Targeting-Pack und eine gestartete Dungeon-Settlers-Kopie mit BepInEx 6 Unity IL2CPP x64, die ihre Interop-Assemblies bereits erzeugt hat. Spiel, Loader und lokale Pfade sind nicht Bestandteil dieses Repositorys.

```powershell
$BepInExGameDir = Read-Host 'Pfad zur BepInEx-Spielkopie'
pwsh -File .\Build-Release.ps1 -ValidateOnly -BepInExGameDir $BepInExGameDir
```

Das Skript restauriert offline mit `NuGet.Config`, baut Core und den BepInEx-Adapter, startet die Core-Regeltests und prüft die exakte Dateiliste des Pakets, die Maße aller Bilder von Core eingeschlossen. Beim letzten Lauf am Stand 0.5.0 bestanden 547 Core-Prüfungen; die Builds hatten null Fehler. `-ValidateOnly` erstellt kein ZIP, installiert nichts und veröffentlicht nichts. Der Build allein bestätigt keine Ingame-Kompatibilität.

Die Ausgaben liegen unter `bin/<Loader>/Release/net6.0/`; Zwischenstände unter `obj/<Loader>/`. Beide Ordner gehören nicht ins Git-Repository. Spiel- und Loader-Referenzen haben `Private=false` und gelangen nicht ins Paket.

Das Paket enthält beide DLLs unter `BepInEx/plugins/DungeonSettlersDelvers/`, die 32 Bilder von Core im Unterordner `Core/` und die Installationsnotiz. Core enthält keine Frieren-Dateien.

Die öffentliche API für Addons steht in [CORE-ADDON-LEITFADEN.md](CORE-ADDON-LEITFADEN.md).
