---
name: "Core-Hook anfragen / Request a Core hook"
about: "Eine Schnittstelle in Delvers: Core anfragen, die dein Paket braucht / Ask for an interface your addon needs"
title: "[Hook] "
labels: ""
assignees: ""
---

Deutsch oder Englisch ist willkommen. / German or English is welcome.

Bitte zuerst prüfen, ob es die Schnittstelle schon gibt: Leitfaden `CORE-ADDON-LEITFADEN.md`, KI-Leitfaden `CORE-ADDON-AI-GUIDE.md` und das Changelog.
Please check first whether the interface exists: `CORE-ADDON-LEITFADEN.md`, `CORE-ADDON-AI-GUIDE.md` and the changelog.

## Version / Version
Core-Paket und API-Version, Loader und Version (z. B. Core 0.5.0, API 1.11.0, BepInEx 6).
Core package and API version, loader and version (e.g. Core 0.5.0, API 1.11.0, BepInEx 6).

## Dein Paket / Your addon
Name und Paket-ID. Was soll der Spieler tun können, und was soll dann passieren?
Name and pack ID. What should the player be able to do, and what should happen then?

## Was fehlt / What is missing
Welche öffentliche Klasse oder Datei hast du geprüft? Was gibt es schon, und wo genau ist die Lücke?
Which public type or file did you check? What exists already, and where exactly is the gap?

## Gewünschte Schnittstelle / Requested interface
Was gibt dein Paket an Core (Definitionen, Daten), und was soll es zurückbekommen (Ereignis, Rückruf, Ergebnis), wann genau?
What does your addon give Core (definitions, data), and what should it receive back (event, callback, result), and when exactly?
Namen sind nur Vorschläge. / Names are proposals only.

## Aufteilung / Responsibility
Was bleibt bei deinem Paket (Mechanik, Grafik, Balance), und was soll Core gemeinsam regeln?
What stays with your addon (mechanics, art, balance), and what should Core handle as shared integration?

## Speichern und Laden / Saving and loading
Was muss Speichern und Laden überstehen? Was soll passieren, wenn dein Paket fehlt?
What must survive saving and loading? What should happen if your addon is missing?

## Verträglichkeit / Compatibility
Betroffene Systeme, z. B. Ausrüstung, Schnellleiste des Spiels, Extended Hotbar, andere Pakete.
Affected systems, e.g. equipment, the native hotbar, Extended Hotbar, other addons.

## Vor dem Absenden / Before submitting
- [ ] Ich habe geprüft, dass die Schnittstelle in meiner Core-Version fehlt. / I checked that the interface is missing in my Core version.
- [ ] Keine privaten Pfade, Spielstände oder persönlichen Logdaten. / No private paths, saves or personal log data.
- [ ] Mir ist bewusst, dass eine Umsetzung nicht zugesichert ist. / I understand that implementation is not guaranteed.
