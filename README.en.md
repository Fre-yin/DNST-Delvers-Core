# Dungeon Settlers Delvers: Core

[Deutsch](README.md) | **English**

Shared core for Dungeon Settlers character mods. Core brings the story characters **Lowell, Liana and Kragas** into recruitment and gives character packs a small, stable API.

**Version 0.3.0 for MelonLoader and BepInEx. Core API 1.3.0.**

## Downloads

The mod packages are on the [Releases](../../releases) page. Download only the package for your installed loader:

| Loader | Mod package |
| --- | --- |
| MelonLoader | `Dungeon-Settlers-Delvers-Core-0.3.0-MelonLoader.zip` |
| BepInEx | `Dungeon-Settlers-Delvers-Core-0.3.0-BepInEx.zip` |

The automatically generated source-code archives are not installation packages.

## Features

- **Story characters as candidates:** Lowell, Liana and Kragas can appear through their original game presets in "Custom Expedition" and in the guild pool. Their art and data come from your installed game; Core copies none of it.
- **Guild price cap:** These three cost at most 500 gold in the guild.
- **Unique candidates when rerolling:** When a reroll is blocked by locked details, the button consistently shows the game's own message "The selected options conflict with the current locks."
  - Lowell, Liana, Kragas: locking only the portrait still lets you reroll the other starting details. As soon as the primary traits are locked, the reroll is blocked right away, because their story background can never be rolled randomly.
  - Characters from character packs with a unique trait: the portrait or the primary traits alone already block the reroll, so the same character cannot be generated several times.
- **Load protection:** Core detects certain failures while loading modded campaigns. After a detected failure it blocks further saves for the game session and shows an in-game notice. Restart before saving again. This protection cannot repair an already damaged save or detect every possible failure.
- **Fixed traits:** Character packs can register traits that Core restores from a surviving character profile when those traits are missing. Other saved traits remain intact.
- **API for character packs:** Packs register unique candidates, recruitment integrations, fixed traits, and load callbacks. See the [addon guide](CORE-ADDON-LEITFADEN.md) for details.

## Requirements

- Windows x64 and Dungeon Settlers **DS_B.0.4.23** (Steam builds 25269660 and 25284551 share the same verified signature). On another game version Core stays inactive and says so in the log.
- A separately installed **MelonLoader 0.7.3** or **BepInEx 6 Unity IL2CPP x64** (tested with 6.0.0-be.788). BepInEx 5 and Mono are not supported.
- Do not mix MelonLoader and BepInEx in the same game installation.

## Installation

**MelonLoader:** `DungeonSettlersDelvers.Core.MelonLoader.dll` into `Mods/`, `DungeonSettlersDelvers.Core.dll` into `UserLibs/`.

**BepInEx:** both DLLs into `BepInEx/plugins/DungeonSettlersDelvers/`.

Back up your saves before first use. Core runs on its own; character packs need the Core package of the same loader.

## Tested

The 0.2.0 features were checked in game with Core alone on both loaders on 26 September 2026. For 0.3.0, normal campaign loads with a character pack and Extended Hotbar 1.0.1 passed under MelonLoader and BepInEx. A detected component failure with save blocking and the player notice also passed under MelonLoader. The offline matrix passed 156 Core checks per loader with no build warnings or errors. Quickload and further 0.3.0 failure paths have not yet received full in-game acceptance.

Long campaigns, every resolution and arbitrary combinations with other mods cannot be guaranteed. When reporting a problem, please include the game build, loader version and a cleaned log.

## For character pack developers

```csharp
// Register after Core has initialized; dispose during your own shutdown.
if (!DelversCoreRuntime.IsReady || !DelversCoreRuntime.SupportsApi("1.3.0")) return;

IDisposable uniqueLease = DelversCoreRuntime.RegisterUniqueCandidatePredicate(
    "my-pack", candidate => IsMyUniqueCharacter(candidate));

IDisposable integrationLease = DelversCoreRuntime.RegisterRecruitmentIntegration(
    "my-pack", onCampaignRefreshed: helper => { /* your campaign logic */ },
    suppressNativeFounderRolls: () => false);

uniqueLease.Dispose();
integrationLease.Dispose();
```

Pack IDs use lowercase letters, digits and hyphens. Registering the same predicate again returns another reference-counted lease; a different predicate under an active ID is rejected. Predicate failures are logged once with the pack ID and block neither the UI nor other packs.

Building and validation: see [BUILDING.en.md](BUILDING.en.md). The [addon guide](CORE-ADDON-LEITFADEN.md) describes further interfaces and safety rules.

## License

Original code and instructions: MIT, see [LICENSE](LICENSE). Game content, characters and trademarks are excluded, see [LICENSING.txt](LICENSING.txt) and [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). Unofficial community project, not made by CanOpener.
