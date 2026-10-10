# Dungeon Settlers Delvers: Core

**Deutsch** | [English](README.en.md)

Gemeinsamer Kern für Charakter-Mods in Dungeon Settlers. Core bringt die Story-Figuren **Lowell, Liana und Kragas** in die Rekrutierung und stellt Charakterpaketen eine kleine, stabile API bereit.

**Version 0.4.1 für MelonLoader und BepInEx. Core-API 1.4.0.**

## Downloads

Die Mod-Pakete liegen unter [Releases](../../releases). Lade nur das Paket für deinen installierten Loader herunter:

| Loader | Mod-Paket |
| --- | --- |
| MelonLoader | `DelversCore-0.4.1-MelonLoader.zip` |
| BepInEx | `DelversCore-0.4.1-BepInEx.zip` |

Die automatisch erzeugten Quellcode-Archive sind keine Installationspakete.

## Funktionen

- **Story-Figuren als Kandidaten:** Lowell, Liana und Kragas können über ihre originalen Spiel-Presets in „Eigene Expedition“ und im Gildenpool erscheinen. Ihre Grafiken und Daten kommen aus deinem installierten Spiel; Core kopiert nichts davon.
- **Gildenpreis-Deckel:** Diese drei kosten in der Gilde höchstens 500 Gold.
- **Einzigartige Kandidaten beim Neu-Rekrutieren:** Ist das Neu-Rekrutieren wegen gesperrter Merkmale blockiert, zeigt der Button einheitlich den originalen Hinweis „Die gewählten Optionen stehen mit den aktuellen Sperren in Konflikt.“
  - Lowell, Liana, Kragas: Nur das Bild zu sperren erlaubt weiterhin, die übrigen Start-Merkmale neu zu würfeln. Sobald die primären Traits gesperrt sind, ist Neu-Rekrutieren sofort blockiert, denn ihr Story-Hintergrund kann nicht zufällig erzeugt werden.
  - Charaktere aus Charakterpaketen mit einem einzigartigen Trait: Schon das Bild oder die primären Traits allein blockieren das Neu-Rekrutieren. So lässt sich derselbe Charakter nicht mehrfach erzeugen.
- **Schutz beim Laden:** Core erkennt bestimmte Fehler beim Laden modifizierter Kampagnen. Bei einem erkannten Fehler sperrt es weitere Speicherversuche für diese Spielsitzung und zeigt einen Hinweis im Spiel. Ein Neustart ist nötig, bevor wieder gespeichert werden kann. Der Schutz repariert keine beschädigten Spielstände und erfasst nicht jeden Fehler.
- **Feste Traits:** Charakterpakete können die Traits einer Figur registrieren, die Core beim Laden anhand ihres erhaltenen Profils ergänzt, wenn sie fehlen. Andere gespeicherte Traits bleiben erhalten.
- **API für Charakterpakete:** Pakete melden eigene einzigartige Kandidaten, Rekrutierungsintegrationen, feste Traits, Lade-Callbacks und umbenannte Keys an. Weitere Einzelheiten stehen im [Addon-Leitfaden](CORE-ADDON-LEITFADEN.md).

## Voraussetzungen

- Windows x64 und Dungeon Settlers **DS_B.0.4.29** (Steam-Build 25846389). Bei einer anderen Spielversion bleibt Core inaktiv und meldet das im Log.
- Separat installiertes **MelonLoader 0.7.3** oder **BepInEx 6 Unity IL2CPP x64** (getestet mit 6.0.0-be.788). BepInEx 5 und Mono werden nicht unterstützt.
- MelonLoader und BepInEx nicht in derselben Spielinstallation mischen.

## Installation

**MelonLoader:** `DungeonSettlersDelvers.Core.MelonLoader.dll` nach `Mods/`, `DungeonSettlersDelvers.Core.dll` nach `UserLibs/`.

**BepInEx:** Beide DLLs nach `BepInEx/plugins/DungeonSettlersDelvers/`.

Vor der ersten Nutzung Spielstände sichern. Core läuft allein; Charakterpakete benötigen das Core-Paket desselben Loaders.

## Getestet

Für 0.4.1 lief am 10.10.2026 auf DS_B.0.4.29 unter MelonLoader und BepInEx ein Selbsttest im Spiel, zusammen mit Frieren 0.4.1 und Extended Hotbar 1.0.2. Zwei Testkampagnen, eine mit umzubenennenden Keys und eine mit fehlenden festen Traits, wurden geladen, gespeichert und erneut geladen. Alle neun überwachten Lade-Methoden trugen den Schutz von Core, und jeder dieser Ladevorgänge galt als vertrauenswürdig. Ein Spielstand mit einem unbekannten Effekt wurde erkannt: Core sperrte das Speichern, zeigte den Hinweis und lehnte ein manuelles Speichern ab. Die Offline-Matrix bestand mit 168 Core-Prüfungen je Loader, ohne Buildwarnungen oder Fehler.

Für 0.4.0 wurden unter MelonLoader und BepInEx ein Spielstand mit umzubenennenden Keys und einer mit fehlenden festen Traits im Spiel geladen und gespeichert. Die gespeicherten Dateien enthielten danach nur noch die neuen Keys und alle festen Traits. Die Offline-Matrix bestand mit 168 Core-Prüfungen je Loader, ohne Buildwarnungen oder Fehler.

Die Funktionen von 0.2.0 wurden am 26.09.2026 mit Core allein unter beiden Loadern im Spiel geprüft. Für 0.3.0 bestanden normale Kampagnen-Loads mit einem Charakterpaket und Extended Hotbar 1.0.1 unter MelonLoader und BepInEx. Unter MelonLoader bestanden außerdem ein erkannter Komponentenfehler mit Speichersperre und der Spielerhinweis. Die Offline-Matrix bestand mit 156 Core-Prüfungen je Loader, ohne Buildwarnungen oder Fehler. Quickload und weitere Fehlerpfade von 0.3.0 sind noch nicht vollständig im Spiel abgenommen.

Langzeitkampagnen, alle Auflösungen und beliebige Kombinationen mit anderen Mods können nicht pauschal garantiert werden. Bei Fehlerberichten bitte Spielbuild, Loader-Version und das bereinigte Log angeben.

## Für Entwickler von Charakterpaketen

```csharp
// Nach der Core-Initialisierung registrieren, beim eigenen Shutdown freigeben.
if (!DelversCoreRuntime.IsReady || !DelversCoreRuntime.SupportsApi("1.3.0")) return;

IDisposable uniqueLease = DelversCoreRuntime.RegisterUniqueCandidatePredicate(
    "my-pack", candidate => IsMyUniqueCharacter(candidate));

IDisposable integrationLease = DelversCoreRuntime.RegisterRecruitmentIntegration(
    "my-pack", onCampaignRefreshed: helper => { /* eigene Kampagnenlogik */ },
    suppressNativeFounderRolls: () => false);

uniqueLease.Dispose();
integrationLease.Dispose();
```

Pack-IDs bestehen aus Kleinbuchstaben, Ziffern und Bindestrichen. Wiederholte Registrierung mit demselben Prädikat liefert eine weitere referenzgezählte Lease; ein anderes Prädikat unter einer aktiven ID wird abgelehnt. Fehler in Prädikaten werden einmal mit der Pack-ID protokolliert und blockieren weder die Oberfläche noch andere Pakete.

Bauen und Validieren: siehe [BUILDING.md](BUILDING.md). Weitere Schnittstellen und Sicherheitsregeln erklärt der [Addon-Leitfaden](CORE-ADDON-LEITFADEN.md).

## Lizenz

Eigener Code und eigene Anleitungen: MIT, siehe [LICENSE](LICENSE). Spielinhalte, Figuren und Marken sind ausgenommen, siehe [LICENSING.txt](LICENSING.txt) und [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). Inoffizielles Community-Projekt, nicht von CanOpener.
