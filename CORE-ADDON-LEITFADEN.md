# Delvers Core: öffentliche API für Charakterpakete

Stand: Core 0.5.0, API 1.11.0. Dieser Leitfaden beschreibt die Schnittstellen bis API 1.4.0, die Kampagnen-GUID aus API 1.9.0 (Abschnitt „Testkampagne erkennen“) und die Einstellungsdatei je Spielkopie aus API 1.10.0 (Abschnitt „Einstellungen je Spielkopie“). Die Erweiterungen 1.5.0 bis 1.8.0 (Seiten im Delvers-Menü, Testkampagnen, Wiederbelebung, Blutung) und die Test-Aufrufe der Formation aus 1.11.0 nennt das Changelog; in diesem Leitfaden stehen sie noch nicht. Addons für eigene Items, Gebäude oder neue Skill-Definitionen brauchen zusätzliche Forschung und besitzen derzeit keine allgemeine Core-API.

Ein Charakterpaket benötigt das Core-Paket für denselben Loader. Registriere es erst, wenn `DelversCoreRuntime.IsReady` wahr ist und `SupportsApi` für die kleinste API-Version erfolgreich war, die dein Paket braucht: `"1.3.0"` für Lade-Callbacks, `"1.4.0"` für umbenannte Keys und `DelversNativeList`. Gib jede erhaltene `IDisposable`-Registrierung beim Shutdown frei. Pack-IDs bestehen aus Kleinbuchstaben, Ziffern und Bindestrichen.

Unter BepInEx 6 ist die Versionsangabe in `BepInDependency` ein Bereich. `"0.4.0"` verlangt genau Core 0.4.0, und schon Core 0.4.1 verhindert dann das Laden deines Pakets. Gib deshalb eine Mindestversion an, etwa `[BepInDependency(DelversCoreRuntime.BepInExPluginId, ">=0.4.1")]`. Die eigentliche Prüfung bleibt `SupportsApi`.

| Schnittstelle | Zweck |
| --- | --- |
| `RegisterRecruitmentIntegration` | Reaktion auf aktualisierte Kampagnenkandidaten und optionales Unterdrücken nativer Founder-Würfe. |
| `RegisterUniqueCandidatePredicate` | Eigene einzigartige Kandidaten vor unzulässigem Neu-Auswürfeln schützen. |
| `RegisterFixedTraits` | Geordnete Liste der permanenten Traits eines Profils anmelden, damit fehlende Einträge beim Laden ergänzt werden können. |
| `RegisterLoadIntegration` | Migrationen vor der Komponenten-Deserialisierung, vor und nach der Profil-Deserialisierung oder nach dem Kampagnenladen einhängen. |
| `RegisterSaveKeyRenames` | Ab API 1.4.0: umbenannte gespeicherte Keys anmelden. Core ersetzt die alten Keys, bevor das Spiel den Spielstand liest. |
| `DelversNativeList.AddValue` | Ab API 1.4.0: Strukturen des Spiels, etwa `AffecterHolder`, in native Listen einfügen. |
| `IsSaveAllowed`, `SaveBlockReason` | Aktuellen Zustand des Core-Ladeschutzes lesen. |

Beispiel für die festen Traits eines eigenen Profils:

```csharp
if (!DelversCoreRuntime.IsReady || !DelversCoreRuntime.SupportsApi("1.3.0")) return;

IDisposable fixedTraits = DelversCoreRuntime.RegisterFixedTraits(
    "my-pack", "UNITVISUAL_MyCharacterProfile",
    new[] { "AFFECTER_Elf", "AFFECTER_MyBackground", "AFFECTER_MyTrait" });

// Beim Beenden des Addons:
fixedTraits.Dispose();
```

Ein Profil darf nur einem aktiven Pack mit einer geordneten Trait-Liste gehören. Core ergänzt registrierte feste Traits, wenn das Profil im Save erhalten blieb; vorübergehende Effekte werden nicht rekonstruiert. Bewahre die Keys dauerhaft stabil. Muss ein gespeicherter Key doch umbenannt werden, melde die Paare aus altem und neuem Key mit `RegisterSaveKeyRenames` an. Core ersetzt dann nur vollständige Keys. Ketten und doppelte alte Keys lehnt Core ab.

Core besitzt die Harmony-Hooks an `ComponentList.Deserialize` und `UnitProfileComponent.Deserialize`. Addons sollten ihre Save-Migrationen über `RegisterLoadIntegration` einbinden. Ein Fehler in einem solchen Callback kann den Load als nicht vertrauenswürdig markieren und das Speichern bis zum Neustart sperren. Behandle wiederholbare oder nicht kritische Arbeit im Addon selbst; wirf nur, wenn die Datenintegrität tatsächlich gefährdet ist.

Der Ladeschutz erkennt bestimmte Komponenten- und Abschnittsfehler. Er schützt nicht jede Phase des Ladevorgangs und repariert keine bereits beschädigten Saves. Teste ein neues Addon auch mit einem Spielstand, der ohne das Addon geladen wurde, sowie unter beiden Loadern. Eine erfolgreiche Offline-Matrix ersetzt den Ingame-Test nicht.

Unter BepInEx gibt `List<T>.Add` Strukturen des Spiels, etwa `AffecterHolder`, nicht korrekt an die native Liste weiter. Nutze dafür `DelversNativeList.AddValue`; unter MelonLoader verhält es sich gleich.

Die genauen Delegattypen und Signaturen stehen in [`Core/DelversCoreRuntime.cs`](Core/DelversCoreRuntime.cs) und [`Core/PackLoadIntegrationRegistry.cs`](Core/PackLoadIntegrationRegistry.cs). Zum Bauen siehe [BUILDING.md](BUILDING.md).

## Testkampagne erkennen

Ab API 1.9.0 liefert `DelversCoreRuntime.CurrentCampaignGuid()` die GUID der Kampagne, an die Core gerade gebunden ist, als Text im Format „D“. Im Hauptmenü und bevor eine Kampagne geladen oder neu begonnen wurde, ist der Wert `null`. Lies ihn frühestens in den Lade-Callbacks (`RegisterLoadIntegration`) oder danach.

`DelversCoreRuntime.IsCurrentCampaignTest()` fasst beides zusammen: Es ist `true`, wenn die gebundene Kampagne eine Testkampagne ist, sonst `false`, auch ohne Kampagne. Damit prüft ein Addon, bevor es Inhalte nur für Testkampagnen freigibt. Die GUID nicht aus Core-Logzeilen lesen. Prüfe vorher `ApiVersion` gegen `MinimumApiVersionForCampaignGuid` (1.9.0).

## Drei Teile der Core-API

Die **Pack-API** nutzen Charakterpakete mit einem Verweis auf Core; sie bleibt in Hauptversion 1 stabil. Die **Werkzeug-API** rufen Werkzeuge über Reflection auf, etwa `SettingsFilePath()` und `WoundStatus`; sie bleibt ebenfalls stabil. Die **Test-API** dient nur Testläufen in markierten Testkampagnen und hat keine Stabilitätszusage. Zur Test-API gehören alle Member, die auf `ForTest` enden, dazu `MarkTestCampaign`, `ForgetTestCampaign`, `SetRecruitmentFree`, `SetFounderOffersForced`, `MarkNextNewCampaignAsTest`, `IsNextNewCampaignMarkedAsTest`, `ApocalypseStatus`, `CoreOption`, `EnemyAbilityStatus`, `HintsStatus`, `GroupAiStatus`, `GroupAiBanditStatus`, `GroupAiResetWindow`, `LifebloomStatus`, `BanditsActive` und `AuditFixedTraitRestore`. Zwölf weitere öffentliche Member gehören zu keinem der drei Teile und sind nicht für Pakete gedacht: der Lebenszyklus der Adapter (`Initialize`, `Shutdown`, `DelversHost.Bind` und `Unbind` sowie die sechs Member von `IDelversHostServices`), `AuditNativeFounders` und `MelonAssemblyName`. Sie werden mit API 2.0 intern oder fallen weg. Die vollständige Tabelle aller Member mit Teil und Mindestversion folgt in einer späteren Fassung des Leitfadens.

## Einstellungen je Spielkopie

Ab API 1.10.0 hat jede Installation des Spiels ihre eigene Einstellungsdatei von Core: `<persistentDataPath>\DungeonSettlersDelvers\Core\copies\<Kennung>\settings.json`. Daneben liegt `copy.txt` mit dem Spielordner. Im Spiel liefert `DelversCoreRuntime.SettingsFilePath()` den vollen Pfad (die Datei muss noch nicht bestehen). Prüfe vorher `ApiVersion` gegen `MinimumApiVersionForSettingsCopy` (1.10.0).

Ein Werkzeug, das Einstellungen vor dem Spielstart schreibt, rechnet die Kennung so: Spielordner (der Ordner über `DungeonSettlers_Data`) als voller Pfad, `/` als `\`, ohne `\` am Ende, in Kleinbuchstaben; davon SHA256 über die UTF-8-Bytes; die ersten 8 Hexziffern in Großbuchstaben. Fehlt die Datei der Kopie beim Start, übernimmt Core einmal die Werte aus der früheren gemeinsamen `DungeonSettlersDelvers\Core\settings.json`. Diese Datei liest Core danach nicht mehr und schreibt sie nie.
