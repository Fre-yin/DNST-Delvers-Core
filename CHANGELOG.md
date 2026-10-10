# Changelog

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
