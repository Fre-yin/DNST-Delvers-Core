# Dungeon Settlers Delvers: Core

**Deutsch** | [English](README.en.md)

Gemeinsamer Kern für Charakter-Mods in Dungeon Settlers. Core bringt die Story-Figuren **Lowell, Liana und Kragas** in die Rekrutierung und stellt Charakterpaketen eine kleine, stabile API bereit.

**Version 0.5.0 für BepInEx. Core-API 1.11.0.**

## Downloads

Das Mod-Paket `DelversCore-0.5.0-BepInEx.zip` liegt unter [Releases](../../releases). Die automatisch erzeugten Quellcode-Archive sind keine Installationspakete. Ab 0.5.0 gibt es Delvers nur noch für BepInEx, MelonLoader wird nicht mehr unterstützt. Die MelonLoader-Fassungen bis 0.4.1 werden archiviert.

## Funktionen

- **Story-Figuren als Kandidaten:** Lowell, Liana und Kragas können über ihre originalen Spiel-Presets in „Eigene Expedition“ und im Gildenpool erscheinen.
- **Gildenpreis-Deckel:** Diese drei kosten in der Gilde höchstens 500 Gold.
- **Einzigartige Kandidaten beim Neu-Rekrutieren:** Ist das Neu-Rekrutieren wegen gesperrter Merkmale blockiert, zeigt der Button einheitlich den originalen Hinweis „Die gewählten Optionen stehen mit den aktuellen Sperren in Konflikt.“
  - Lowell, Liana, Kragas: Nur das Bild zu sperren erlaubt weiterhin, die übrigen Start-Merkmale neu zu würfeln. Sobald die primären Traits gesperrt sind, ist Neu-Rekrutieren sofort blockiert, denn ihr Story-Hintergrund kann nicht zufällig erzeugt werden.
  - Charaktere aus Charakterpaketen mit einem einzigartigen Trait: Schon das Bild oder die primären Traits allein blockieren das Neu-Rekrutieren. So lässt sich derselbe Charakter nicht mehrfach erzeugen.
- **Schutz beim Laden:** Core erkennt bestimmte Fehler beim Laden modifizierter Kampagnen. Bei einem erkannten Fehler sperrt es weitere Speicherversuche für diese Spielsitzung und zeigt einen Hinweis im Spiel. Ein Neustart ist nötig, bevor wieder gespeichert werden kann. Der Schutz repariert keine beschädigten Spielstände und erfasst nicht jeden Fehler.
- **Delvers-Menü:** In den Optionen des Spiels öffnet der letzte Reiter „Delvers“ die Einstellungen von Core und der installierten Delvers-Pakete. Während einer laufenden Kampagne ist er gesperrt und nur im Titelmenü nutzbar. Dort lassen sich Lowell, Liana und Kragas einzeln als Gründer abschalten und Apocalypse, die Pausenregel, die Wiederbelebungsrolle, die Apocalypse Optionen für andere Schwierigkeitsgrade (darunter die Waldbanditen und die kleineren Banditengruppen), die Hinweise, die Frage vor dem Begraben, die KI für die Gruppe, das Schildband und die taktische Formation einschalten oder abschalten.
- **Schwierigkeitsgrad Apocalypse:** eine Stufe über Downfall, für Kampf und Verwaltung getrennt, wählbar beim Anlegen einer Kampagne, sobald Downfall freigeschaltet ist. Vorerst gelten für die Gegner des Spiels die Werte von Downfall. Bei Kampf-Apocalypse greifen Gegner sofort an, unbemerkte Gegner nehmen keinen Zusatzschaden, und die Pausentaste verlangsamt die Zeit nur noch auf halbe Geschwindigkeit, bis zu 5 Sekunden, mit Aufladung. Ohne Core lädt eine solche Kampagne als Downfall.
- **Wiederbelebung:** Mit der Wiederbelebungsrolle holt eine lebende Figur eine tote Figur der Gruppe zurück, nach 20 Sekunden neben der Leiche. Wie oft das geht, hängt von der Kampfstufe ab: Apocalypse und Downfall 2-mal, Sehr schwer 4-mal, darunter unbegrenzt. Danach sind Leben und Energie eine Zeit lang begrenzt. Der Gildenhändler führt die Rolle immer, Dungeon-Händler gelegentlich. Ohne Core bleibt sie eine Rückkehrrolle.
- **Blutung und Erste Hilfe:** Bei Kampf-Apocalypse hören Blutungen nicht mehr von selbst auf. Sie bluten in vier Stufen nach, bis ein Verband, der Verbandskasten, das Portal oder eine Wiederbelebung sie stillt. Verbände gibt es bei Händlern, an der Werkbank und am Alchemietisch.
- **Gegnerfähigkeiten:** Bei Kampf-Apocalypse verursachen einige Gegner mit ihren Treffern Blutungen, manche Zauberer belegen ihr Ziel mit Schweigen, und Sporengegner schläfern ein. Schaden weckt eine schlafende Figur.
- **Waldbanditen:** Bei Apocalypse im Kampf eine eigene Gegnergruppe mit eigener Gruppensteuerung und eigenen Werten. Im Vile Forest kann man auf ihr Lager und auf kleinere Banditengruppen stoßen, und ihre Boss Party lässt besondere Gegenstände fallen. Im Delvers Menü lassen sie sich auch für andere Schwierigkeitsgrade einschalten, dort sind sie schwächer.
- **Ausrüstung ausziehen:** Alt und Rechtsklick auf die Leiche einer Spielfigur oder eines Banditen bietet „Ausrüstung ausziehen“; die Leiche lässt dann alles Angezogene fallen.
- **Ausrüstung bleibt am Körper:** Gefallene behalten ihre angelegte Ausrüstung. Vor dem Begraben einer angezogenen Gefallenen fragt Core nach.
- **Schildband:** Ein Wabenband über den Leisten zeigt den Schild einer Figur im Verhältnis zu ihrem Leben, bis 200 %.
- **KI für die Gruppe (experimentell):** auf Wunsch Zielwahl, Abstand, Ausweichen und Verbände für Figuren auf automatischem Angriff; bis Schwer, standardmäßig aus.
- **Taktische Formation (experimentell):** Je Figur ein Platz in einer Aufstellung, der die Gruppe im Dungeon hinter einem Anführer folgt, dazu fünf Plätze für Fähigkeiten, die eine Figur zuerst wirkt. Im Delvers Menü abschaltbar.
- **Spielhilfe:** Ganz unten in der Spielhilfe des Spiels erklärt der Bereich „Delvers“ die Regeln und Schalter von Delvers, mit eigenen Bildern.
- **Eigene Daten im Spielstand:** Apocalypse und die Daten der Wiederbelebung speichert Core in einem eigenen Feld „DelversCore“ im Spielstand, das das Spiel ohne Core überliest, und zusätzlich in einer Datei je Kampagne unter `DungeonSettlersDelvers\Core\campaigns`.
- **Logs ohne Kontonamen:** Core ersetzt den Benutzerordner in seinen Logzeilen durch `<user>`.
- **Feste Traits:** Charakterpakete können die Traits einer Figur registrieren, die Core beim Laden anhand ihres erhaltenen Profils ergänzt, wenn sie fehlen. Andere gespeicherte Traits bleiben erhalten.
- **API für Charakterpakete:** Pakete melden eigene einzigartige Kandidaten, Rekrutierungsintegrationen, feste Traits, Lade-Callbacks und umbenannte Keys an. Weitere Einzelheiten stehen im [Addon-Leitfaden](CORE-ADDON-LEITFADEN.md).

## Voraussetzungen

- Windows x64 und Dungeon Settlers **DS_B.0.4.29** (Steam-Build 25846389). Bei einer anderen Spielversion bleibt Core inaktiv und meldet das im Log.
- Separat installiertes **BepInEx 6 Unity IL2CPP x64** (getestet mit 6.0.0-be.788). BepInEx 5, Mono und MelonLoader werden in 0.5.0 nicht unterstützt.

## Installation

**BepInEx:** Entpacke das ZIP in den Spielordner. Beide DLLs liegen dann in `BepInEx/plugins/DungeonSettlersDelvers/`, die Bilder von Core im Unterordner `Core/`. Ohne diesen Ordner zeigen die Symbole die des Spiels.

**Umstieg von MelonLoader:** Entferne `DungeonSettlersDelvers.Core.MelonLoader.dll` aus `Mods/` und `DungeonSettlersDelvers.Core.dll` aus `UserLibs/`, dann installiere BepInEx 6 und dieses Paket. Die MelonLoader-Fassungen bis 0.4.1 werden archiviert.

Vor der ersten Nutzung Spielstände sichern. Core läuft allein; Charakterpakete benötigen Core.

## Getestet

Für 0.5.0 bestanden offline 1435 Prüfungen für Core und 129 für Frieren, ohne Buildwarnungen. Im Spiel lief Core unter BepInEx `6.0.0-be.788` auf DS_B.0.4.29 zusammen mit Frieren 0.5.0 und Extended Hotbar 1.0.2.

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

Die Core-API hat drei Teile: Pack-API und Werkzeug-API bleiben in Hauptversion 1 stabil, die Test-API für Testkampagnen hat keine Zusage. Alle Member der drei Teile stehen in [`Tests/Core/PublicApi.txt`](Tests/Core/PublicApi.txt).

Bauen und Validieren: siehe [BUILDING.md](BUILDING.md). Weitere Schnittstellen und Sicherheitsregeln erklärt der [Addon-Leitfaden](CORE-ADDON-LEITFADEN.md).

## Lizenz

Eigener Code und eigene Anleitungen: MIT, siehe [LICENSE](LICENSE). Spielinhalte, Figuren und Marken sind ausgenommen, siehe [LICENSING.txt](LICENSING.txt) und [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). Inoffizielles Community-Projekt, nicht von CanOpener.
