# Changelog

## 0.5.2: 27.11.2026

- Taktische Formation: Ein Gruppenbefehl mit Rechtsklick verschiebt jetzt die ganze Formation ans Ziel, statt sie aufzulösen. Der Anführer steht auf dem Klickfeld, die anderen auf ihren Plätzen um ihn.
- Ohne den Anführer stellen sich die ausgewählten Figuren am Ziel auf, als stünde er dort, und bleiben, bis er nachkommt. Ein Befehl nur an den Anführer lässt die anderen folgen, auch Figuren mit Autoskill aus. Nur Position halten hält eine Figur an ihrem Platz.
- Die Formation hält in die Richtung, aus der der Anführer gekommen ist, auch hinter einer Ecke. Wegpunkte mit Shift laufen in Formation.
- Ist ein Platz durch Gelände belegt, nimmt die Figur zuerst den gespiegelten Platz auf der anderen Seite. Die Formation gibt zwei Figuren nie dasselbe Feld.
- Im Kampf bricht eine einzeln weggeschickte Figur die Formation nur für sich. Sie kehrt nach dem Kampf oder mit dem nächsten Gruppenbefehl zurück.
- Ein Angriffsbefehl gilt, bis das Ziel fällt, ein neuer Befehl kommt oder der Kampf endet.
- Neu im Formationsfenster: Rollen für jedes Mitglied (Tank, Schaden, Support, neutral), je Formation, noch experimentell, und der Schalter In Formation bewegen: Die Gruppe läuft außerhalb des Kampfs im Tempo ihres langsamsten Mitglieds.
- Ebenfalls neu: feste Plätze für Beschwörungen und beliebig viele Mitglieder mit Scrollleiste. Gefallene behalten ihren Platz.
- Das Formationsfenster ist neu gestaltet und breiter. Jede Figur steht im Rahmen der Porträts, mit ihrer Rolle als Symbol und Name und ihren Prioritätsplätzen als nummerierte Felder. Das Raster steht rechts, darüber Vorne über die ganze Breite.
- Im Raster ist der Anführer golden gerahmt, Beschwörungen blau. In der Liste stehen Beschwörungen eingerückt unter ihrer Beschwörerin. Gefallene und Abwesende sind grau.
- Hinzufügen öffnet eine Auswahl mit Bildern. Beim Ziehen zeigt das Fenster das Ziel und eine Vorschau, die Seiten der Formationen zeigen Punkte statt einer Zahl.
- Das Formationsfenster lässt sich an der Titelzeile verschieben und bleibt dort, bis du ins Hauptmenü gehst. Beschwörungen wie Bubbleling und Blasenkröte lassen sich schon einplanen, sobald eine Figur sie beschwören kann.
- Der Tooltip des Formationsknopfs zeigt, ob die Formation an ist, und was Linksklick und Rechtsklick tun. In Formation ist die Wegpunktlinie des Anführers kräftiger, die der Begleiter blass gestrichelt.
- Gruppen KI: Die Gruppe geht gemeinsam in den Kampf, und jede Rolle handelt danach. Heilungen gehen an den am schwersten Verletzten in Reichweite, Beschwörungen halten ihre Plätze.
- Waldbanditen: Jeder Bandit kämpft auf seine eigene Art, und sie arbeiten besser zusammen. Kleine Gruppen ziehen auf den ersten Ebenen durch den Wald.
- Im Delvers Menü neu: der Anteil der Banditenpatrouillen und eine Wartezeit für das Zusammenspiel der Banditen (0 schaltet es aus).
- Mit der Mod Hell steht Apocalypse in der Auswahl zwischen Downfall und Hell.
- Dungeon Settlers Modded Loader: Core und Frieren laufen in eigenen Ordnern des Loaders und übernehmen einmal die Einstellungen des normalen Spiels. Mit älteren Fassungen des Loaders, die die Besitzprüfung des Spiels abschalten, bleibt Core aus und sagt im Spiel, warum.
- Mit den Kampfwunden zeigt Blut am Körper einer Figur der Gruppe nur noch das Körperblut von Delvers. Läuft die Blutung des Spiels nach dem Schließen der Wunde weiter, ist sie wieder zu sehen.
- Behoben: Figuren wichen im Pfeilregen nicht mehr aus, wenn sie erst später unter halbes Leben fielen.
- Behoben: Eine fast verheilte Wunde konnte die nächste Wunde sofort tödlich machen.
- Behoben: Im zweiten Gefecht auf einer Karte verhielten sich Gruppe und Banditen nicht mehr wie im ersten.
- Behoben: Nach einer neuen Kampagne in derselben Sitzung setzte die Nachwirkung der Wiederbelebung verspätet ein.
- Behoben: Der Verbandskasten wurde über Auf andere Figur anwenden an einer Gefallenen verbraucht, ohne sie zu retten. Gefallene rettet er per Klick auf sie.
- Behoben: Ein Verbandskasten für eine andere Figur ging verloren, wenn das Anlegen nicht zustande kam. Er kommt jetzt zurück in die Tasche, bei voller Tasche liegt er vor der Figur.
- Behoben: Die Formation holte eine Figur zurück, bevor sie fertig gefüttert, verbunden oder gezaubert hatte. Was eine Figur auf deinen Befehl tut, macht sie jetzt zu Ende.
- Behoben: Rote Striche an Brust oder Gesicht konnten nach dem Verbinden oder einer Rettung an der Figur hängen bleiben.
- Behoben: Eine getragene Gefallene blieb manchmal zurück. Lebensblüte mit der Einstellung 0 heilte trotzdem. Die Farben der Schriftrolle der Wiederbelebung stimmen wieder. Weitere Fehler bei Formation, In Formation bewegen, Gruppen KI und den Waldbanditen.
- Extended Hotbar 1.0.4 wird als verträglich erkannt.
- Für Paketautoren: Core API 1.12.0, neu sind Testaufrufe für die Formation.
- Geprüft: 1776 Prüfungen für Core und 133 für Frieren ohne Buildwarnungen. Im Spiel unter BepInEx auf DS_B.0.4.29, zusammen mit Frieren 0.5.1 und Extended Hotbar 1.0.4.

## 0.5.1: 02.11.2026

- Extended Hotbar 1.0.3 wird als verträglich erkannt.
- Behoben: Verbände von Delvers auf den Zusatzplätzen von Extended Hotbar bleiben nach dem Laden erhalten.

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
