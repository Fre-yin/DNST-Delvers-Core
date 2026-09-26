# Changelog

## 0.2.0 — 26.09.2026

Erste öffentliche Ausgabe von Dungeon Settlers Delvers: Core.

- Lowell, Liana und Kragas erscheinen über ihre originalen Story-Presets in „Eigene Expedition“ und im Gildenpool; der Gildenpreis ist auf 500 Gold gedeckelt.
- Founder-Würfe verwenden einen eigenen Zufallsgenerator und verändern den Zufallszustand des Spiels nicht.
- Einheitliche Anzeige, wenn das Neu-Rekrutieren eines einzigartigen Kandidaten durch Sperren blockiert ist: originaler Konflikt-Hinweis, deaktivierter Button.
- Sperr-Regeln: Bei Lowell, Liana und Kragas blockieren gesperrte primäre Traits sofort, ein gesperrtes Bild allein nicht; bei Charakterpaketen mit einzigartigem Trait blockiert schon eines von beiden.
- Öffentliche Core-API 1.1.0: `RegisterUniqueCandidatePredicate` und `RegisterRecruitmentIntegration` mit referenzgezählten Leases und Fehlerisolation.
- Adapter für MelonLoader 0.7.3 und BepInEx 6 Unity IL2CPP (6.0.0-be.788) aus denselben Quellen.
- Geprüfte Spielsignatur: DS_B.0.4.23 (Steam-Builds 25269660 und 25284551).
