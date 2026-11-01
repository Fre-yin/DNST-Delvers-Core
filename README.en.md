# Dungeon Settlers Delvers: Core

[Deutsch](README.md) | **English**

Shared core for Dungeon Settlers character mods. Core brings the story characters **Lowell, Liana and Kragas** into recruitment and gives character packs a small, stable API.

**Version 0.5.0 for BepInEx. Core API 1.11.0.**

## Downloads

The mod package `DelversCore-0.5.0-BepInEx.zip` is on the [Releases](../../releases) page. From 0.5.0 on, Delvers is available for BepInEx only; MelonLoader is no longer supported. The MelonLoader versions up to 0.4.1 are being archived.

The automatically generated source-code archives are not installation packages.

## Features

- **Story characters as candidates:** Lowell, Liana and Kragas can appear through their original game presets in "Custom Expedition" and in the guild pool.
- **Guild price cap:** These three cost at most 500 gold in the guild.
- **Unique candidates when rerolling:** When a reroll is blocked by locked details, the button consistently shows the game's own message "The selected options conflict with the current locks."
  - Lowell, Liana, Kragas: locking only the portrait still lets you reroll the other starting details. As soon as the primary traits are locked, the reroll is blocked right away, because their story background can never be rolled randomly.
  - Characters from character packs with a unique trait: the portrait or the primary traits alone already block the reroll, so the same character cannot be generated several times.
- **Load protection:** Core detects certain failures while loading modded campaigns. After a detected failure it blocks further saves for the game session and shows an in-game notice. Restart before saving again. This protection cannot repair an already damaged save or detect every possible failure.
- **Delvers menu:** In the game's options, the last tab "Delvers" opens the settings of Core and the installed Delvers packs. During a running campaign it is locked and only available from the title menu. There you can turn Lowell, Liana and Kragas off as founders one by one, and switch Apocalypse, the pause rule, the revive scroll, the Apocalypse options for other difficulties (the forest bandits and the smaller bandit groups among them), the hints, the question before a burial, the group AI, the shield band and the tactical formation on or off.
- **Apocalypse difficulty:** one step above Downfall, separately for combat and management, available when you create a campaign once Downfall is unlocked. For now the game's enemies use Downfall's values. In combat Apocalypse, enemies attack at once, unaware enemies take no extra damage, and the pause key only slows time to half speed, for up to 5 seconds, and recharges. Without Core, such a campaign loads as Downfall.
- **Revival:** With the revive scroll, a living character brings back a dead party member, standing up next to the body after 20 seconds. How often this works depends on the combat difficulty: 2 times on Apocalypse and Downfall, 4 times on Very Hard, unlimited below. Afterwards health and energy are capped for a while. The guild merchant always sells the scroll, dungeon merchants sometimes. Without Core it stays a return scroll.
- **Bleeding and first aid:** in combat Apocalypse, bleeding no longer stops on its own. It keeps draining in four stages until a bandage, the aid kit, the portal or a revive stops it. Bandages are sold by merchants and made at the workbench and the alchemy table.
- **Enemy abilities:** in combat Apocalypse, some enemies cause bleeding with their hits, some casters silence their target, and spore enemies put it to sleep. Damage wakes a sleeping character.
- **Forest bandits:** in combat Apocalypse, an enemy group of its own with its own group control and its own values. In the Vile Forest you may come across their camp and smaller bandit groups, and their boss party drops special items. In the Delvers menu you can also switch them on for other difficulties, where they are weaker.
- **Strip equipment:** Alt and right click on the body of a party member or a bandit offers "Strip Equipment"; the body then drops everything it wears.
- **Equipment stays on the body:** fallen party members keep their equipped gear. Core asks before a dressed fallen member is buried.
- **Shield band:** a honeycomb band over the bars shows a character's shield relative to their health, up to 200%.
- **Group AI (experimental):** optional target choice, spacing, dodging and bandages for characters left on auto attack; up to Hard, off by default.
- **Tactical formation (experimental):** each character gets a place in a lineup that follows a leader through the dungeon, plus five places for skills the character casts first. Can be turned off in the Delvers menu.
- **Guide:** at the bottom of the game's guide, a Delvers section explains the rules and switches of Delvers, with its own pictures.
- **Own data in the save:** Core stores Apocalypse and revival data in its own "DelversCore" field in the save, which the game skips without Core, and also in one file per campaign under `DungeonSettlersDelvers\Core\campaigns`.
- **Logs without account names:** Core replaces your user folder with `<user>` in its log lines.
- **Fixed traits:** Character packs can register traits that Core restores from a surviving character profile when those traits are missing. Other saved traits remain intact.
- **API for character packs:** Packs register unique candidates, recruitment integrations, fixed traits, load callbacks, and renamed keys. See the [addon guide](CORE-ADDON-LEITFADEN.md) for details.

## Requirements

- Windows x64 and Dungeon Settlers **DS_B.0.4.29** (Steam build 25846389). On another game version Core stays inactive and says so in the log.
- A separately installed **BepInEx 6 Unity IL2CPP x64** (tested with 6.0.0-be.788). BepInEx 5, Mono and MelonLoader are not supported in 0.5.0.

## Installation

**BepInEx:** extract the ZIP into the game folder. Both DLLs then sit in `BepInEx/plugins/DungeonSettlersDelvers/` and Core's images in its `Core/` folder. Without that folder, the icons show the game's own.

**Moving from MelonLoader:** remove `DungeonSettlersDelvers.Core.MelonLoader.dll` from `Mods/` and `DungeonSettlersDelvers.Core.dll` from `UserLibs/`, then install BepInEx 6 and this package. The MelonLoader versions up to 0.4.1 are being archived.

Back up your saves before first use. Core runs on its own; character packs need Core.

## Tested

For 0.5.0, 1435 Core checks and 129 Frieren checks passed offline with no build warnings. In game, Core ran under BepInEx `6.0.0-be.788` on DS_B.0.4.29 together with Frieren 0.5.0 and Extended Hotbar 1.0.2.

For 0.4.1, an in-game self-test ran on DS_B.0.4.29 under MelonLoader and BepInEx on 10 October 2026, together with Frieren 0.4.1 and Extended Hotbar 1.0.2. Two test campaigns, one with keys to rename and one with missing fixed traits, were loaded, saved and loaded again. All nine monitored load methods carried Core's protection, and each of these loads was trusted. A save with an unknown effect was detected: Core blocked saving, showed its notice and refused a manual save. The offline matrix passed 168 Core checks per loader with no build warnings or errors.

For 0.4.0, a save with keys to rename and a save with missing fixed traits were loaded and saved in game under MelonLoader and BepInEx. Afterwards the saved files contained only the new keys and all fixed traits. The offline matrix passed 168 Core checks per loader with no build warnings or errors.

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

The Core API has three parts: the pack API and the tool API stay stable within major version 1, and the test API for test campaigns has no such promise. All members of the three parts are listed in [`Tests/Core/PublicApi.txt`](Tests/Core/PublicApi.txt).

Building and validation: see [BUILDING.en.md](BUILDING.en.md). The [addon guide](CORE-ADDON-LEITFADEN.md) describes further interfaces and safety rules.

## License

Original code and instructions: MIT, see [LICENSE](LICENSE). Game content, characters and trademarks are excluded, see [LICENSING.txt](LICENSING.txt) and [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). Unofficial community project, not made by CanOpener.
