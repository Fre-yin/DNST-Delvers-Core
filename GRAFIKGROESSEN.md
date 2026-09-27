# Dungeon Settlers: Grafikgrößen für Aseprite

Zusammenfassung früherer Messungen an lokalen Spielgrafiken und Unity-Sprites. Die extrahierte Dateiliste und Spieldateien sind nicht Teil dieses Projekts.

| Grafiktyp | Arbeitsfläche für neue Grafik | Originale und Ausnahmen |
| --- | ---: | --- |
| **Talentsymbol** (Attributentwicklung: Dummkopf, Mäßig, Genie usw.) | **10 × 10 px** | Die sechs Stufen- und Statussymbole sowie das Kreuzsymbol sind 10 × 10. Separate kleine Tooltip-Bilder sind meist 5 × 5; `Unknown_Tooltip` ist 6 × 7 und `Moderate_Tooltip` 10 × 10. |
| **Skill- oder Ability-Icon** (aktive und passive Skills) | **32 × 32 px** | 207 von 210 Originalen sind 32 × 32. Zwei sind 32 × 31 und eines 31 × 32 zugeschnitten. Der normale Skill-Rahmen ist ebenfalls 32 × 32, aber eine eigene UI-Grafik. |
| **Charakter-Trait-Icon** (z. B. Elfe, Magier, Slow Learner, Frieren) | **24 × 24 px** | Diese vier konkreten Trait-Icons sind 24 × 24. Die größere Affecter-Sammlung enthält auch Zustands- und Effektsymbole: 336 Dateien mit 16 × 16, 285 mit 24 × 24, acht mit 32 × 32 sowie je eine Ausnahme mit 24 × 28 und 28 × 28. |
| **Charakterporträt** | **48 × 48 px** | Beide Frieren-Zustände und die normalen Elfenporträts sind 48 × 48. Von 161 Portrait-Dateien sind 129 genau 48 × 48; andere Originale wurden an transparenten Rändern zugeschnitten. Für Frieren Normal und Stress je eine eigene 48 × 48-Datei verwenden. |

Die Maße gelten für die eigentliche Pixelgrafik, nicht für die vergrößerte Anzeige im Spiel. Charakter-Trait-Icons werden auf einer Arbeitsfläche von **24 × 24 px** erstellt. Seltenheitsrahmen und UI-Hintergrund gehören nicht in die eigentliche Icondatei.
