# Changelog

## 0.5.0: 01.11.2026

- Nur noch BepInEx, MelonLoader wird nicht mehr unterstützt.
- Neu: das Delvers Menü als letzter Reiter in den Optionen, mit allen Schaltern von Core.
- Neu: der Schwierigkeitsgrad Apocalypse über Downfall, für Kampf und Management. Ohne Core lädt eine solche Kampagne als Downfall.
- Neu bei Apocalypse: Blutungen in Stufen, Gegnerfähigkeiten, sofort angreifende Gegner und Zeitlupe statt Pause.
- Neu bei Apocalypse: die Waldbanditen, mit eigenem Lager, kleineren Gruppen und einer Boss Party mit besonderer Beute.
- Die Regeln von Apocalypse lassen sich im Delvers Menü einzeln auch für andere Schwierigkeitsgrade einschalten.
- Neu: Erste Hilfe mit Verbänden, eigener Forschung und dem Verarzten anderer Figuren.
- Neu: die Wiederbelebungsrolle, mit Nachwirkungen.
- Neu: Gefallene behalten ihre Ausrüstung, Leichen lassen sich mit Alt und Rechtsklick ausziehen.
- Neu: ein Schildband an den Lebensleisten.
- Neu, experimentell: die taktische Formation und eine KI für die Gruppe.
- Neu: ein eigener Bereich Delvers in der Spielhilfe.
- Gründer lassen sich einzeln abschalten, Logs zeigen keinen Kontonamen mehr, Speichern ist robuster.
- Behoben: Frieren und die Gründer konnten nach einem Absturz beim Rekrutieren verschwinden. Ein Fehler des Spiels in der Zielauswahl konnte im Kampf die Spielzeit anhalten.
- Für Paketautoren: Core API 1.11.0, veröffentlicht war 1.4.0. Neu sind Menüseiten, Testkampagnen, Wiederbelebung und Blutung. Pack API und Werkzeug API bleiben in Hauptversion 1 stabil, die Test API nicht. Alle Member stehen in `Tests/Core/PublicApi.txt`, die Erklärung im Leitfaden.
- Geprüft: 1435 Prüfungen für Core und 129 für Frieren ohne Buildwarnungen. Im Spiel unter BepInEx auf DS_B.0.4.29, zusammen mit Frieren 0.5.0 und Extended Hotbar 1.0.2.

## 0.4.1: 10.10.2026

- Angepasst an das Spielupdate DS_B.0.4.29 (Steam-Build 25846389). Core erkennt die neue Spielsignatur und startet wieder. Auf älteren Spielversionen bleibt 0.4.1 inaktiv. Sonst ändert sich nichts, die Core-API bleibt 1.4.0.
- Frieren: Zusammen mit Core 0.4.1 bitte Frieren 0.4.1 installieren. Unter BepInEx lädt Frieren 0.4.0 nur mit genau Core 0.4.0.
- Für Paketautoren: `SupportedBuilds` ist keine Konstante mehr. Pakete lesen den Text zur Laufzeit und nennen im Log so immer die Spielversion des installierten Core. Unter BepInEx 6 verlangt eine feste Version in `BepInDependency` genau diese Version, besser ist eine Mindestversion wie `">=0.4.1"` (siehe Leitfaden).
- Geprüft: 168 Core-Prüfungen je Loader ohne Buildwarnungen. Im Spiel auf DS_B.0.4.29 unter MelonLoader und BepInEx, mit Frieren 0.4.1 und Extended Hotbar 1.0.2: Testkampagnen mit umzubenennenden Keys und mit fehlenden festen Traits wurden geladen, gespeichert und erneut geladen. Ein Spielstand mit unbekanntem Effekt wurde erkannt, das Speichern gesperrt und der Hinweis gezeigt.

## 0.4.0: 29.09.2026

- Core-API 1.4.0: Charakterpakete können mit `RegisterSaveKeyRenames` umbenannte Keys anmelden. Core ersetzt die alten Keys im Spielstand, bevor das Spiel ihn liest. Ersetzt werden nur vollständige Keys, und das Log nennt nur das Paket und die Anzahl.
- Core-API 1.4.0: `DelversNativeList.AddValue` fügt Strukturen des Spiels, etwa Trait-Einträge, zuverlässig in native Listen ein.
- Behoben: Unter BepInEx konnte das Wiederherstellen fehlender fester Traits beim Laden fehlerhafte Einträge schreiben und das Spiel zum Absturz bringen. MelonLoader war nicht betroffen.
- Extended Hotbar 1.0.2 wird im Lade-Patch-Audit wie 1.0.1 als kompatibel erkannt.
- Geprüft: 168 Core-Prüfungen je Loader ohne Buildwarnungen. Unter MelonLoader und BepInEx wurden im Spiel ein Spielstand mit umzubenennenden Keys und einer mit fehlenden festen Traits geladen und gespeichert. Danach wurde die gespeicherte Datei kontrolliert.

## 0.3.0: 28.09.2026

- Core-API 1.3.0 ergänzt Lade-Callbacks für Charakterpakete. Core besitzt die gemeinsamen Harmony-Lade-Hooks; Packs können Migrationen über registrierte Callbacks einbinden.
- Der Lade-Wächter sperrt Speicherversuche während eines aktiven Loads und nach bestimmten erkannten Ladefehlern. Ein Hinweis erscheint im Spiel.
- Feste Traits lassen sich je Charakterprofil registrieren und bei fehlenden Einträgen wiederherstellen.
- Das Lade-Patch-Audit erfasst neun kritische Methoden. Zwei exakt bekannte Patches von Extended Hotbar 1.0.1 werden als kompatibel klassifiziert; die Überwachung bleibt aktiv.
- Die Funktionen für Lowell, Liana und Kragas bleiben erhalten. Die Offline-Matrix bestand 156 Core-Prüfungen je Loader und beide Core-Paketlisten. Normale Loads mit Extended Hotbar 1.0.1 bestanden unter MelonLoader und BepInEx.

## 0.2.0: 26.09.2026

Erste öffentliche Ausgabe. Lowell, Liana und Kragas über ihre nativen Story-Presets; Gildenpreis höchstens 500 Gold; Regeln für einzigartige Kandidaten; Core-API 1.1.0; MelonLoader- und BepInEx-Adapter.
