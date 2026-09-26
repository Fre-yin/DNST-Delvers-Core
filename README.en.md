# Dungeon Settlers Delvers: Core

[Deutsch](README.md) | **English**

Shared core for Dungeon Settlers character mods. Core brings the story characters **Lowell, Liana and Kragas** into recruitment and gives character packs a small, stable API.

**Version 0.2.0 for MelonLoader and BepInEx. Core API 1.1.0.**

## Downloads

The mod packages are on the [Releases](../../releases) page. Download only the package for your installed loader:

| Loader | Mod package |
| --- | --- |
| MelonLoader | `Dungeon-Settlers-Delvers-Core-0.2.0-MelonLoader.zip` |
| BepInEx | `Dungeon-Settlers-Delvers-Core-0.2.0-BepInEx.zip` |

The automatically generated source-code archives are not installation packages.

## Features

- **Story characters as candidates:** Lowell, Liana and Kragas can appear through their original game presets in "Custom Expedition" and in the guild pool. Their art and data come from your installed game; Core copies none of it.
- **Guild price cap:** These three cost at most 500 gold in the guild.
- **Unique candidates when rerolling:** When a reroll is blocked by locked details, the button consistently shows the game's own message "The selected options conflict with the current locks."
  - Lowell, Liana, Kragas: locking only the portrait still lets you reroll the other starting details. As soon as the primary traits are locked, the reroll is blocked right away, because their story background can never be rolled randomly.
  - Characters from character packs with a unique trait: the portrait or the primary traits alone already block the reroll, so the same character cannot be generated several times.
- **API for character packs:** Packs register their own unique candidates and campaign observers (see below).

## Requirements

- Windows x64 and Dungeon Settlers **DS_B.0.4.23** (Steam builds 25269660 and 25284551 share the same verified signature). On another game version Core stays inactive and says so in the log.
- A separately installed **MelonLoader 0.7.3** or **BepInEx 6 Unity IL2CPP x64** (tested with 6.0.0-be.788). BepInEx 5 and Mono are not supported.
- Do not mix MelonLoader and BepInEx in the same game installation.

## Installation

**MelonLoader:** `DungeonSettlersDelvers.Core.MelonLoader.dll` into `Mods/`, `DungeonSettlersDelvers.Core.dll` into `UserLibs/`.

**BepInEx:** both DLLs into `BepInEx/plugins/DungeonSettlersDelvers/`.

Back up your saves before first use. Core runs on its own; character packs need the Core package of the same loader.

## Tested

Checked in game on 26 Sep 2026 with Core alone on both loaders: startup and signature check, Lowell and Liana in "Custom Expedition", Liana in the guild pool at 500 gold, reroll lock behavior. Additionally with MelonLoader together with a character pack. Every build runs 86 deterministic offline checks per loader.

Long campaigns, every resolution and arbitrary combinations with other mods cannot be guaranteed. When reporting a problem, please include the game build, loader version and a cleaned log.

## For character pack developers

```csharp
// Register after Core has initialized; dispose during your own shutdown.
if (!DelversCoreRuntime.IsReady || !DelversCoreRuntime.SupportsApi("1.1.0")) return;

IDisposable uniqueLease = DelversCoreRuntime.RegisterUniqueCandidatePredicate(
    "my-pack", candidate => IsMyUniqueCharacter(candidate));

IDisposable integrationLease = DelversCoreRuntime.RegisterRecruitmentIntegration(
    "my-pack", onCampaignRefreshed: helper => { /* your campaign logic */ },
    suppressNativeFounderRolls: () => false);

uniqueLease.Dispose();
integrationLease.Dispose();
```

Pack IDs use lowercase letters, digits and hyphens. Registering the same predicate again returns another reference-counted lease; a different predicate under an active ID is rejected. Predicate failures are logged once with the pack ID and block neither the UI nor other packs.

Building and validation: see [BUILDING.en.md](BUILDING.en.md).

## License

Original code and instructions: MIT, see [LICENSE](LICENSE). Game content, characters and trademarks are excluded, see [LICENSING.txt](LICENSING.txt) and [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). Unofficial community project, not made by CanOpener.
