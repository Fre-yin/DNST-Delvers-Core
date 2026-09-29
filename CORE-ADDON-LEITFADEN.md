# Delvers Core: öffentliche API für Charakterpakete

Stand: Core 0.4.0, API 1.4.0. Dieser Leitfaden beschreibt die vorhandenen Schnittstellen. Addons für eigene Items, Gebäude oder neue Skill-Definitionen brauchen zusätzliche Forschung und besitzen derzeit keine allgemeine Core-API.

Ein Charakterpaket benötigt das Core-Paket für denselben Loader. Registriere es erst, wenn `DelversCoreRuntime.IsReady` wahr ist und `SupportsApi` für die kleinste API-Version erfolgreich war, die dein Paket braucht: `"1.3.0"` für Lade-Callbacks, `"1.4.0"` für umbenannte Keys und `DelversNativeList`. Gib jede erhaltene `IDisposable`-Registrierung beim Shutdown frei. Pack-IDs bestehen aus Kleinbuchstaben, Ziffern und Bindestrichen.

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
