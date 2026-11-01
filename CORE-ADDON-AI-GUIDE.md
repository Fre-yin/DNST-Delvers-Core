# Building addons with Delvers Core

For addon developers and the AI assistants working with them. New to modding? Start with the quickstart: [CORE-ADDON-SCHNELLSTART.en.md](CORE-ADDON-SCHNELLSTART.en.md).

Delvers Core is the shared foundation for independent Dungeon Settlers addons. You build and maintain your own addon. Core provides shared integration points, and we extend those interfaces when an addon needs a system that Core does not expose yet.

You do not need to make a character pack or depend on Frieren to use Core. A weapon, skill tree or other gameplay addon can use Core directly. This does not mean that every required interface is already available.

Shared integration also lets us handle changes from game updates centrally in Core. An addon using a compatible public API may then continue without changes to that integration. This is not a guarantee that an addon will never need an update: its own direct game hooks and gameplay behavior still need verification, and Core must support the new game build first.

## Check the version first

Source review: 31 October 2026.

| Source | Package version | Core API version | Status |
| --- | --- | --- | --- |
| Release reviewed for this guide | 0.5.0 | 1.11.0 | Current release |
| Previous release | 0.4.1 | 1.4.0 | Baseline for users who have not updated |

Package versions and API versions are different. Inspect the exact release used by the addon. Do not assume that an interface is available in the user's installed release because a newer one has it. Use `SupportsApi` for the compatibility decision, with the matching `MinimumApiVersionFor...` constant where one exists. `ApiVersion` is a readonly field from 0.5.0 on; in 0.4.1 it was a constant that the compiler copies into your addon, so it cannot identify the installed runtime there.

## Instructions for AI assistants

1. Read this guide and the README, build instructions and public API of the selected Core version before implementing integration.
2. Treat Core as a separate dependency. Do not copy its source, loader adapter, registries or save patches into the addon.
3. Verify each proposed API against that version's public source or assembly. A class marked `internal` is an implementation detail, even when its code looks useful.
4. If a required hook or API is missing, state plainly: **The required hook is not available in this version of Delvers Core.** Identify the missing capability and give the developer a copyable request for the Core maintainer. Use the format below, or the issue template "Request a Core hook" in the Core repository.
5. Do not invent a registration method, silently access Core internals through reflection, or copy or patch Core to bypass the missing interface. Do not patch native save and load methods as a substitute for a missing Core integration point. Describe the gap and continue only the work that does not depend on it.
6. Explain what the addon should own and what shared capability it needs from Core. The maintainer will check whether an existing internal system can be exposed or whether new support is needed. Do not assume that a missing public API means the underlying system does not exist.
7. Keep proposed API names clearly marked as proposals. Do not present them as callable methods until they exist in the selected Core version.
8. Core 0.5.0 supports BepInEx 6 for Unity IL2CPP x64 only. MelonLoader was supported up to 0.4.1. If an addon also targets 0.4.1, keep the loader profiles separate and confirm dependency identities, references and initialization order for each.
9. Make content registration repeatable without duplicate entries. Keep registration leases and release them during proper shutdown. Roll back partial initialization and remove only patches owned by the addon.
10. Report source checks, builds, offline checks and actual game tests separately. A successful build does not prove that a new weapon, skill tree or saved skill works in the game.
11. Members whose name ends in `ForTest` are test hooks for development tools. They act only in a campaign marked as a test campaign and carry no stability promise. Do not build addon features on them.

### Copyable request when an API is missing

Fill this in with the actual findings. Remove unused fields. Write it in the developer's preferred language. Include no private paths, saves or personal log data.

```text
Hi, I am building [addon name] on Delvers Core [package version], API [API version], using [loader and version].

I need the addon to [specific player action and expected result].

The required hook is not available in this version of Delvers Core. I checked [public type or source file] and found [existing support and the specific gap].

Could Core expose support for [capability]? The addon needs to provide [definitions or data] and receive [event, callback or result] when [exact stage or trigger].

The addon would keep responsibility for [its mechanics, assets and balancing]. Core would handle [the shared integration needed]. If you already use this system internally, exposing a supported interface may be enough.

Persistence requirements: [what must survive saving and loading, and what should happen if the addon is missing].
Relevant compatibility: [equipment, native hotbar, Extended Hotbar or other affected systems].

This is an API request, not a claim that these methods already exist.
```

For example, a Sceptre addon might request registration of a weapon and its basic attack, a primary Sceptre tree and a secondary Devout tree. The request should describe equipment restrictions, skill learning and save behavior. It should not assume that a method named `RegisterWeapon` already exists.

## Public integration in 0.5.0

Namespace: `DungeonSettlersDelvers.Core`. The minimum API version of each group is available as a constant on `DelversCoreRuntime`, for example `MinimumApiVersionForMenuPages`.

| Public member | Purpose | From API |
| --- | --- | --- |
| `DelversCoreRuntime.IsReady` | Check that Core initialized successfully. | 1.0.0 |
| `DelversCoreRuntime.LoaderProfile` | Check the active profile; `BepInEx` in 0.5.0. | 1.0.0 |
| `DelversCoreRuntime.SupportsApi(minimumVersion)` | Require the same API major version and at least the requested version. | 1.0.0 |
| `RegisterRecruitmentIntegration(packId, onCampaignRefreshed, suppressNativeFounderRolls)` | Participate in guild candidate integration. | 1.0.0 |
| `RegisterUniqueCandidatePredicate(packId, predicate)` | Identify unique recruitment candidates. | 1.1.0 |
| `RegisterFixedTraits(packId, profileKey, orderedTraitKeys)` | Declare a profile's fixed traits for restoration. This does not register the traits themselves. | 1.2.0 |
| `RegisterLoadIntegration(packId, ...)` | Callbacks around component deserialization, unit profile deserialization and completed campaign loading. | 1.3.0 |
| `RegisterSaveKeyRenames(packId, renames)` | Migrate renamed persistent IDs. | 1.4.0 |
| `DelversNativeList.AddValue(list, item)` | Add supported value types to native lists safely. | 1.4.0 |
| `RegisterMenuPage(packId, title, rows)` with `DelversMenuRows.Section`, `Text`, `Toggle`, `Choice`, `Button` | Add the addon's own tab to the Delvers menu in the game's options. `OpenMenu`, `CloseMenu` and `IsMenuOpen` control and read the menu. | 1.5.0 |
| `IsTestCampaign`, `MarkTestCampaign`, `ForgetTestCampaign` | Test campaigns, identified by campaign GUID, never by name. For development tools. | 1.6.0 |
| `CanRevive`, `StartRevive`, `LastReviveResult`, `RevivesLeft`, `RevivalSickness...` readers | Core's revive of a dead party member and its after effects, read only for addons. | 1.7.0 |
| `WoundStatus(unit)` | Read Core's bleeding wound of a party member. | 1.8.0 |
| `CurrentCampaignGuid()`, `IsCurrentCampaignTest()` | The loaded campaign's GUID (format "D", `null` outside a campaign) and whether it is a test campaign. | 1.9.0 |
| `SettingsFilePath()` | The settings file of the running game copy. How the copy's identity follows from the game folder is described in `CORE-ADDON-LEITFADEN.md`, so a tool can find the file before the game starts. | 1.10.0 |
| `IsSaveAllowed`, `SaveBlockReason` | Inspect Core's save guard. | 1.0.0 |
| `DelversSummons.SummonerGuid(entity)` | The summoner of a summoned unit or building, read safely. | 0.5.0 |
| `DelversLogPrivacy.Mask(text)` | Mask user folder paths before writing them to a shared log. | 0.5.0 |
| `DelversHost.Info`, `Warning`, `Error` | Use the active Core logging service. Prefix messages with the addon's identity. | 1.0.0 |
| `DelversHost.StartCoroutine`, `StopCoroutine` | Use the active Core coroutine service. Keep and stop the addon's own handles. | 1.0.0 |

Registration methods return `IDisposable` leases. Keep them for the lifetime of the registration and dispose them on shutdown. Logging and coroutine helpers do not register addon content.

`DelversHost.PackageDirectory` belongs to Core's bound host. Do not assume it is your addon's asset directory. Resolve addon assets from the addon's own installation location.

Other public members of `DelversCoreRuntime` (status readers such as `GroupAiStatus` or `ApocalypseStatus`, and `CampaignPresence`) serve Core's own tools and packs. Check the selected version before relying on them; they are not part of the integration surface listed above.

## Reference Core without bundling it

Build against the gameplay assembly `DungeonSettlersDelvers.Core.dll` from the matching Core build. Keep it as an external dependency. A reference fragment can use a caller supplied path:

```xml
<ItemGroup>
  <Reference Include="DungeonSettlersDelvers.Core">
    <HintPath>$(DelversCoreAssembly)</HintPath>
    <Private>false</Private>
  </Reference>
</ItemGroup>
```

This is only the Core reference fragment, not a complete project. Configure the target framework, loader references, game interop references and build properties from the selected version's build instructions, or follow the quickstart for a complete BepInEx project; its project file and code build without warnings. The reviewed projects target `net6.0`. Use separate interop references and outputs for each loader.

Declare Core as a loader dependency so that it starts before the addon. The loader identity is a constant on `DelversCoreRuntime`:

| Loader | Dependency identity |
| --- | --- |
| BepInEx 6 | Plugin ID `DelversCoreRuntime.BepInExPluginId` (`fre-yin.dungeonsettlers.delvers.core`) |
| MelonLoader | Not supported in 0.5.0 (up to 0.4.1: adapter assembly `DelversCoreRuntime.MelonAssemblyName`) |

Under BepInEx 6 the version in `BepInDependency` is a range. Give a minimum, for example `">=0.5.0"`, not an exact version; an exact `"0.5.0"` stops the addon from loading with the next Core release. The gameplay assembly name, loader assembly name, plugin ID and Harmony owner are different things. A BepInEx version constraint does not replace the API check.

Before applying addon patches or registering content, check:

```csharp
using DungeonSettlersDelvers.Core;

// For Core 0.5.0 expectedLoader is "BepInEx".
static bool CanStart(string expectedLoader)
{
    return DelversCoreRuntime.IsReady
        && DelversCoreRuntime.LoaderProfile == expectedLoader
        && DelversCoreRuntime.SupportsApi(DelversCoreRuntime.MinimumApiVersionForMenuPages);
}
```

Use the minimum API your addon actually needs. If this check fails, report why through the addon's loader logger and leave the addon inactive. Missing Core must be handled through loader dependency resolution; calling this method cannot resolve a missing assembly.

Core's adapter owns Core initialization and host binding. An addon must not call `DelversCoreRuntime.Initialize`, `Shutdown`, `DelversHost.Bind` or `Unbind` to manage Core.

## Content interfaces that still need work

Core 0.5.0 exposes no general public registration API for custom skills, skill trees, weapons, basic attacks, items, icons or localization. Existing addons register some content directly in native tables. That is version specific addon code, not a general Core content API.

There are useful internal examples:

| Example | What it demonstrates | Limitation |
| --- | --- | --- |
| Frieren's Zoltraak and channel skills | A custom skill built using native skill infrastructure | Internal addon code, not a public skill or tree API |
| Core's Lifebloom | Another custom skill implementation | Internal Core code |
| Core's bandages and revive scroll | Custom item implementations | Internal Core code |
| Core's skill and effect save handling | Persistence handling for specific Core content | Not a general third party content registry |

These examples can help the Core maintainer extract shared interfaces. They are not a promise that another addon can call them or that every combat behavior is already supported.

For a weapon and skill tree addon, agree on these integration needs before implementing dependent code:

1. Weapon registration, equipment restrictions and basic attack selection.
2. Primary and secondary skill tree registration, learning rules, costs and progression.
3. Skill definitions, targeting, cast behavior and hotbar integration.
4. Effect definitions, icons and localized text.
5. Ownership of persistent IDs, restoration and safe behavior when content is missing.

A shared Core interface should handle common registration and lifecycle needs. The addon should retain its own damage rules, skill effects, art, text and balance. Propose the smallest interface needed by the actual addon.

## Saving, loading and compatibility

Give the addon its own stable pack ID, content prefix and Harmony owner. Do not reuse Frieren or Core IDs. Pack IDs consist of lowercase letters, digits and hyphens. Once content IDs reach a save, keep them stable or provide an explicit migration.

Register migrations through `RegisterLoadIntegration` and `RegisterSaveKeyRenames`. A failed load callback can block saving until restart because the loaded state may be incomplete. Do not swallow a data integrity failure or disable the guard to make the game appear to work. Handle retryable runtime work separately.

Core's guard is not a general guarantee that removing any addon is safe. Define and test what happens to each saved weapon, item, skill, effect and tree when its addon is missing. Do not claim hot unload support for saved content without a proven lifecycle.

Use the game's normal skill and quickslot model where possible. Test with the native hotbar and with Extended Hotbar. Core integration alone does not establish hotbar compatibility.

Before a release, test each supported loader with a fresh campaign and an existing save. Include learning a skill, equipping and removing the weapon, casting, save and reload, unmet dependencies and the defined missing addon behavior. Verify the actual released archives rather than relying only on a development checkout.

## What to send with an integration question

Provide the Core package and API versions, loader version, relevant public types, a short reproduction and the desired result. Attach only the necessary sanitized log excerpt (`DelversLogPrivacy.Mask` helps). State whether the issue is a missing Core hook, an addon implementation problem or an unverified hypothesis. For a missing hook, use the issue template "Request a Core hook".

This guide was checked against the source of Core 0.5.0. It does not certify a third party addon or replace its build and game tests.
