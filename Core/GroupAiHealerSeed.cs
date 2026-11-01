using HarmonyLib;
using UnityEngine;
#if BEPINEX
using DamageProperty = global::Refactor.Combat.DamageProperty;
using IExecutionPublisher = global::Refactor.Combat.IExecutionPublisher;
using Execution = global::Refactor.Combat.Execution;
using HealExecutionData = global::Refactor.Combat.HealExecutionData;
using LifeBurstRoutine = global::Refactor.Combat.SkillRelated.SKILL_LifeBurst.Passive_LifeBurst._ActivateLifeBurst_d__5;
using LifeBurstPassive = global::Refactor.Combat.SkillRelated.SKILL_LifeBurst.Passive_LifeBurst;
using PassiveActionContainer = global::Refactor.Tick.PassiveActionContainer;
using SeedAura = global::Refactor.Combat.AffecterRelated.AFFECTER_StrangeSeedAura.Passive_StrangeSeedHeal;
using SprayerDamageRoutine = global::Refactor.Combat.AffecterRelated.AFFECTER_MucusSprayer.Passive_MucusSprayerAttack._DamageRoutine_d__11;
using SummonedBuildingResolver = global::Refactor.Combat.AffecterRelated.SummonedBuildingResolver;
using VFXID = global::Refactor.FX.VFXID;
using VFXView = global::Refactor.FX.VFXView;
#else
using DamageProperty = Il2CppRefactor.Combat.DamageProperty;
using IExecutionPublisher = Il2CppRefactor.Combat.IExecutionPublisher;
using Execution = Il2CppRefactor.Combat.Execution;
using HealExecutionData = Il2CppRefactor.Combat.HealExecutionData;
using LifeBurstRoutine = Il2CppRefactor.Combat.SkillRelated.SKILL_LifeBurst.Passive_LifeBurst._ActivateLifeBurst_d__5;
using LifeBurstPassive = Il2CppRefactor.Combat.SkillRelated.SKILL_LifeBurst.Passive_LifeBurst;
using PassiveActionContainer = Il2CppRefactor.Tick.PassiveActionContainer;
using SeedAura = Il2CppRefactor.Combat.AffecterRelated.AFFECTER_StrangeSeedAura.Passive_StrangeSeedHeal;
using SprayerDamageRoutine = Il2CppRefactor.Combat.AffecterRelated.AFFECTER_MucusSprayer.Passive_MucusSprayerAttack._DamageRoutine_d__11;
using SummonedBuildingResolver = Il2CppRefactor.Combat.AffecterRelated.SummonedBuildingResolver;
using VFXID = Il2CppRefactor.FX.VFXID;
using VFXView = Il2CppRefactor.FX.VFXView;
#endif

namespace DungeonSettlersDelvers.Core;

// / (way B: the game's skills stay, Core acts only for a Core summoner or caster, so
// Frieren's seed and the players' Life Burst are untouched):
// - the seed's aura (Passive_StrangeSeedHeal, its target predicate <HealRoutine>b__9_0: alive and not hostile to the seed
//   building) heals, for a Core summoner, the units not hostile to the summoner. CORE_SEED_AURA_FILTER once per summoner:
//   changed=1 the first time the filter turned the game's answer, changed=0 after FilterQuietChecks answers without.
// - the Life Burst (Passive_LifeBurst._ActivateLifeBurst_d__5) of a Core owner heals bandits.lifeburst.share (12 %) of the
//   healed comrade's max health instead of the game's 200 % magic attack: the heal the routine yields is replaced.
// - the mucus sprayer's hits (Passive_MucusSprayerAttack._DamageRoutine_d__11, its context's target) are logged for a
//   Core summoner (CORE_MUCUS_SPRAYER_HIT).
// - probe (group Seeds), once per seed and capped: when the game draws a seed's area (VFXView.CreateVFX, StrangeSeedArea or
//   StrangeSeedEvolving), the seed's summoner and side and the trees of the seed building and of the area's VFX.
internal static class GroupAiHealerSeed
{
    private const int FilterQuietChecks = 200;
    private const int TreeNodes = 12;
    private static double lifeBurstShare = GroupAiHealerSeedRules.LifeBurstShareDefault;
    private static readonly Dictionary<string, int> filterChecks = new(StringComparer.Ordinal);
    private static readonly HashSet<string> filterLogged = new(StringComparer.Ordinal);
    private static readonly HashSet<string> probed = new(StringComparer.Ordinal);
    private static int sprayerLines, burstLines;

    internal static void Reset()
    {
        lifeBurstShare = GroupAiHealerSeedRules.LifeBurstShare(CoreSettings.Number(GroupAiHealerSeedRules.LifeBurstShareKey));
        filterChecks.Clear();
        filterLogged.Clear();
        probed.Clear();
        auras.Clear();
        lastCounter.Clear();
        skips.Clear();
        areaLogged.Clear();
        comparedAreas = 0;
        healLines = 0;
        burstChecked.Clear();
        GroupAiHealerMagicProbe.Reset();
        bursted.Clear();
        burstTries.Clear();
        sprayerLines = burstLines = tryLines = 0;
        BanditTint.Reset();
    }

    // Fix B: each seed's aura passive, seen at its Regen, for the seed's remaining life (GroupAiHealerSeedRules.Left).
    private const int AuraCap = 64;
    private static readonly Dictionary<string, SeedAura> auras = new(StringComparer.Ordinal);

    // Plan (b) step 3 (the game offers the aura only player units, a bandit never
    // reaches its predicate): for a Core summoner the aura heals nobody natively (OnAuraTarget answers false) and Core heals
    // instead, once per step of the aura's counter: every living Core unit on the aura's area that is no foe of the summoner,
    // by the game's own formula (AFFECTER_StrangeSeedAura._Heal_d__2: the summoner's magic attack x the aura's FloatValues[1]
    // x 0.01), published as the game's heal with the aura as its source, as Lifebloom does. CORE_SEED_HEAL per heal, capped.
    private static readonly Dictionary<string, int> lastCounter = new(StringComparer.Ordinal);
    private static IExecutionPublisher auraPublisher;
    private static float auraValue = -1f;
    private static int healLines;
    private static bool healWarned;

    private static void HealStep(SeedAura aura, IEntity seed)
    {
        var summoner = CoreSummoner(seed);
        if (summoner == null) return;
        var key = seed.Guid.ToString();
        int counter = aura._timeCounter;
        var known = lastCounter.TryGetValue(key, out var last);
        if (known && last == counter) return;
        if (lastCounter.Count >= AuraCap * 4) lastCounter.Clear();
        lastCounter[key] = counter;
        if (!known) return;
        var inMap = CoreGame.InMap;
        // (no-map:dead=1): the game's seed heals on while its summoner can be found, dead or not (Regen only
        // needs GetSummonerUnit and its status); a dead herbalist has no map of her own, so the shown map stands in.
        var map = CoreGame.CurrentMap;
        var onMap = CoreGame.TryMapOfUnit(summoner, out var found);
        if (onMap) map = found;
        if (inMap == null || (!onMap && !summoner.IsDead))
        {
            Skip(key, "no-map:dead=" + (summoner.IsDead ? 1 : 0));
            return;
        }
        // The aura's cells; without any (no CORE_SEED_HEAL at all), the seed's cell and its eight neighbours.
        var cells = new HashSet<(int, int)>();
        var area = aura._area;
        for (var i = 0; area != null && i < area.Count; i++) cells.Add((area[i].x, area[i].y));
        var fallback = cells.Count == 0;
        if (fallback)
        {
            var at = aura._position;
            foreach (var cell in GroupAiHealerSeedRules.AroundCells(at.x, at.y)) cells.Add(cell);
        }
        var magic = CoreGame.Stat(summoner, StatType.MagicAttackPower);
        var value = HealValue(aura);
        var amount = GroupAiHealerSeedRules.SeedHeal(magic, value);
        if (amount <= 0f)
        {
            Skip(key, GroupAiHealerSeedRules.AmountSkip(magic, value));
            return;
        }
        var alive = inMap.GetAliveUnitsOnMap();
        int core = 0, healed = 0;
        for (var i = 0; alive != null && i < alive.Count; i++)
        {
            var unit = alive[i];
            if (unit == null || unit.IsDead || !CoreFaction.IsBandit(unit)) continue;
            core++;
            var at = inMap.GetLogicalPosition(unit.Guid);
            // (CORE_SEED_AREA): the game counts every bandit as a foe of the herbalist (IsHostile true for all,
            // one stood on the area and was skipped). The bandits are her comrades, their summons with them (CoreFaction).
            if (!cells.Contains((at.x, at.y))) continue;
            var done = PublishHeal(map, unit, summoner, amount);
            healed++;
            if (healLines++ < GroupAiBandits.LogCap)
                DelversHost.Info(GroupAiHealerSeedRules.SeedHealLine(key, summoner.Guid.ToString(), unit.Guid.ToString(), amount, done, Tick()));
        }
        if (healed == 0)
        {
            Skip(key, GroupAiHealerSeedRules.TargetSkip(cells.Count, core, fallback));
            AreaOnce(key, true, cells, inMap, summoner);
        }
    }

    // Once per seed (Core seeds whose step healed nobody, and Frieren's first CompareSeeds for comparison): the area's cells
    // and the Core units' cells with whether they count as foes of the summoner. CORE_SEED_AREA, capped.
    private static readonly HashSet<string> areaLogged = new(StringComparer.Ordinal);
    private static int comparedAreas;

    private static void AreaOnce(string seed, bool core, HashSet<(int X, int Y)> cells, IInMapDataSupplier inMap, UnitEntity summoner)
    {
        if (areaLogged.Count >= 32 || !areaLogged.Add(seed)) return;
        var units = new List<string>();
        var alive = inMap?.GetAliveUnitsOnMap();
        for (var i = 0; alive != null && i < alive.Count && units.Count < 8; i++)
        {
            var unit = alive[i];
            if (unit == null || !CoreFaction.IsBandit(unit)) continue;
            var at = inMap.GetLogicalPosition(unit.Guid);
            var hostile = summoner != null && EntityExtensions.IsHostile(unit.Cast<IEntity>(), summoner.Cast<IEntity>()); // trap-ok: log line only, the game's own answer
            units.Add(at.x + "," + at.y + (hostile ? ":foe" : ""));
        }
        DelversHost.Info(GroupAiHealerSeedRules.AreaLine(seed, core, cells, units));
    }

    // The heal value: the aura passive's own Values[1] (its Values[2] is the life Fix B reads), else the affecter data's.
    private static float HealValue(SeedAura aura)
    {
        var values = aura?.Values;
        if (values != null && values.Length > GroupAiHealerSeedRules.HealValueIndex && values[GroupAiHealerSeedRules.HealValueIndex] > 0f)
            return values[GroupAiHealerSeedRules.HealValueIndex];
        return AuraValue();
    }

    // Why a step of a Core seed healed nobody, once per seed and reason.
    private static readonly HashSet<string> skips = new(StringComparer.Ordinal);

    private static void Skip(string seed, string reason)
    {
        var head = reason.Split(':')[0];
        if (skips.Count >= AuraCap * 4 || !skips.Add(seed + "|" + head)) return;
        DelversHost.Info(GroupAiHealerSeedRules.HealSkipLine(seed, reason, Tick()));
    }

    // The aura's FloatValues[1], read once from the game's affecter data; 0 when it is not there.
    private static float AuraValue()
    {
        if (auraValue >= 0f) return auraValue;
        var values = CombatDataManager.Instance?.GetAffecterData(GroupAiHealerSeedRules.AuraKey)?.FloatValues;
        auraValue = values != null && values.Length > GroupAiHealerSeedRules.HealValueIndex ? values[GroupAiHealerSeedRules.HealValueIndex] : 0f;
        return auraValue;
    }

    private static bool PublishHeal(MapType map, UnitEntity target, UnitEntity summoner, float amount)
    {
        try
        {
            var system = CoreGame.TickHandler(map)?._executionSystem;
            auraPublisher ??= CombatDataManager.Instance?.GetAffecterData(GroupAiHealerSeedRules.AuraKey)?.Cast<IExecutionPublisher>();
            if (system == null || auraPublisher == null) throw new InvalidOperationException("execution system or publisher missing");
            var builder = system.GetBuilder();
            builder.CreateExecution(auraPublisher, summoner.Cast<IEntity>(), target.Cast<IEntity>(), null, null, -1);
            builder.Add(Execution.Heal(target.Cast<IEntity>(), DataValue.FixedValue(amount), (DamageProperty)0));
            system.Publish(builder.Build());
            return true;
        }
        catch (Exception ex)
        {
            if (!healWarned)
            {
                healWarned = true;
                DelversHost.Warning("CORE_SEED_HEAL_FAILED: " + ex.Message);
            }
            return false;
        }
    }

    internal static void OnRegen(SeedAura aura)
    {
        var seed = aura?.Owner;
        if (seed == null) return;
        HealStep(aura, seed);
        CompareAreaWithGame(aura, seed);
        var key = seed.Guid.ToString();
        if (auras.ContainsKey(key)) return;
        if (auras.Count >= AuraCap) auras.Clear();
        auras[key] = aura;
    }

    // Probe, group Seeds: the area of a seed that is not a Core summoner's (Frieren's, the first CompareSeeds of them), logged once
    // for comparison (AreaOnce). Runs from Regen, so the flag is the first thing it asks.
    private static void CompareAreaWithGame(SeedAura aura, IEntity seed)
    {
        if (!CoreProbes.Seeds) return;
        if (CoreSummoner(seed) != null || comparedAreas >= GroupAiHealerSeedRules.CompareSeeds || areaLogged.Contains(seed.Guid.ToString())) return;
        comparedAreas++;
        var native = new HashSet<(int X, int Y)>();
        var own = aura._area;
        for (var i = 0; own != null && i < own.Count; i++) native.Add((own[i].x, own[i].y));
        AreaOnce(seed.Guid.ToString(), false, native, CoreGame.InMap, null);
    }

    // The cells of a seed's aura (SeedStay); before its aura was seen, or without cells, the seed's cell and its eight neighbours.
    internal static List<(int X, int Y)> AuraCells(string seed, int x, int y)
    {
        var cells = new List<(int X, int Y)>();
        if (seed != null && auras.TryGetValue(seed, out var aura) && aura != null && aura.Pointer != IntPtr.Zero)
        {
            var area = aura._area;
            for (var i = 0; area != null && i < area.Count; i++) cells.Add((area[i].x, area[i].y));
        }
        if (cells.Count == 0) cells.AddRange(GroupAiHealerSeedRules.AroundCells(x, y));
        return cells;
    }

    // The seconds the seed has left, with the raw life and counter; 0 while its aura was not seen yet.
    internal static float SecondsLeft(string seed, out float life, out float counter)
    {
        life = counter = -1f;
        if (seed == null || !auras.TryGetValue(seed, out var aura) || aura == null || aura.Pointer == IntPtr.Zero) return 0f;
        var values = aura.Values;
        if (values == null || values.Length <= GroupAiHealerSeedRules.LifeValueIndex) return 0f;
        life = values[GroupAiHealerSeedRules.LifeValueIndex];
        counter = aura._timeCounter;
        return GroupAiHealerSeedRules.Left(life, counter);
    }

    // Fix C: once per Core unit carrying the Life Burst, whether the game lists and registered it as her passive. Registering the
    // passive is work and runs whatever the group says; the lines about it (CORE_LIFEBURST_PROBE, CORE_LIFEBURST_REGISTER) are probe
    // lines of the group Seeds. The warning that the check itself failed stays.
    private static readonly HashSet<string> burstChecked = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, int> burstTries = new(StringComparer.Ordinal);
    private const int BurstTries = 3;
    private static int tryLines;

    internal static void CheckLifeBurst(UnitEntity unit, MapType map)
    {
        if (unit == null || burstChecked.Contains(unit.Guid.ToString())) return;
        GroupAiHealerMagicProbe.Run(unit);
        var entity = unit.Cast<IEntity>();
        // Walking GetAllPassiveSkills (a lazy IL2CPP sequence) at the start of a fight threw "Collection was
        // modified" and the probe never logged. SkillComponent.HasSkill asks the same without walking anything; a failed
        // check is tried again at the next decision, up to BurstTries times.
        bool listed;
        string registered = null, passive = null, result = "none";
        try
        {
            listed = CoreGame.Component<SkillComponent>(entity)?.HasSkill(GroupAiHealerSeedRules.LifeBurstSkill) ?? false;
            var container = CoreGame.TickHandler(map)?._passiveActions;
            if (container != null)
            {
                var isRegistered = container.IsEntityRegistered(entity);
                registered = isRegistered ? "1" : "0";
                var has = HasPassive(container, entity);
                passive = has ? "1" : "0";
                // C(c): the passive missing on a registered unit is registered once, with the game's
                // own Register (it warns and adds nothing when the key is there); never RegisterEntity, which makes the unit's
                // passive table anew. The game drops it with the unit (CleanupEntityBeforeDispose); after a load the check
                // runs again for the campaign.
                result = GroupAiHealerSeedRules.RegisterDecision(isRegistered, has);
                if (result == "register")
                {
                    container.Register(entity, GroupAiHealerSeedRules.LifeBurstSkill, true);
                    var after = HasPassive(container, entity);
                    if (CoreProbes.Seeds) DelversHost.Info(GroupAiHealerSeedRules.RegisterLine(unit.Guid.ToString(), false, after, after ? "done" : "failed"));
                }
                else if (result != "skip:present" && CoreProbes.Seeds)
                    DelversHost.Info(GroupAiHealerSeedRules.RegisterLine(unit.Guid.ToString(), has, has, result));
            }
        }
        catch (Exception ex)
        {
            var key = unit.Guid.ToString();
            burstTries.TryGetValue(key, out var tries);
            burstTries[key] = ++tries;
            if (tries >= BurstTries) burstChecked.Add(key);
            DelversHost.Warning("CORE_LIFEBURST_PROBE_FAILED unit=" + key + " try=" + tries + ": " + ex.Message);
            return;
        }
        burstChecked.Add(unit.Guid.ToString());
        if (CoreProbes.Seeds) DelversHost.Info(GroupAiHealerSeedRules.LifeBurstProbeLine(unit.Guid.ToString(), listed, registered, passive, result));
    }

    // Whether the unit's own passive table (PassiveActionContainer._passiveActions[guid]) holds the Life Burst.
    private static bool HasPassive(PassiveActionContainer container, IEntity entity)
    {
        var table = container._passiveActions;
        if (table == null || !table.ContainsKey(entity.Guid)) return false;
        var own = table[entity.Guid];
        return own != null && own.ContainsKey(GroupAiHealerSeedRules.LifeBurstSkill);
    }

    // Fix C: the game asks the Life Burst whether a death starts it (TryActivateLifeBurst); logged for a Core owner.
    // True when the game may go on; false for a dead Core owner: the herbalist of run 1 still trying in
    // runs 2 and 3. Her corpse stays (CorpseKeep), so the game never disposes the unit and never drops her passives
    // (CleanupEntityBeforeDispose); Core unregisters the Life Burst itself, once, and the try ends.
    internal static bool OnLifeBurstTry(LifeBurstPassive passive, IEntity target)
    {
        var owner = passive?.Owner?.TryCast<UnitEntity>();
        if (owner == null || !CoreUnitRules.IsCoreUnit(owner.Key)) return true;
        if (owner.IsDead)
        {
            try
            {
                var container = CoreGame.TickHandler(CoreGame.CurrentMap)?._passiveActions;
                container?.Unregister(owner.Cast<IEntity>(), GroupAiHealerSeedRules.LifeBurstSkill, true);
                DelversHost.Info(GroupAiHealerSeedRules.RegisterLine(owner.Guid.ToString(), true, false, container != null ? "unregistered:dead" : "failed:no-container"));
            }
            catch (Exception ex) { DelversHost.Warning("CORE_LIFEBURST_REGISTER_FAILED unit=" + owner.Guid + ": " + ex.Message); }
            return false;
        }
        if (tryLines < GroupAiHealerSeedRules.ProbeCap * 4)
        {
            tryLines++;
            DelversHost.Info(GroupAiHealerSeedRules.LifeBurstTryLine(owner.Guid.ToString(), target?.Guid.ToString() ?? "-", target?.Key, Tick()));
        }
        // The game's neighbours come from the seed's side and were players every time (10 x dropped). For
        // her own seed Core heals instead and the game's burst does not run: every living Core unit on the seed's cell and
        // its eight neighbours, bandits.lifeburst.share of its max health, once per seed.
        if (target == null || target.Key != GroupAiHealerSeedRules.SeedBuilding || CoreGame.SummonerGuid(target) != owner.Guid.ToString()) return true;
        OwnLifeBurst(owner, target);
        return false;
    }

    private static readonly HashSet<string> bursted = new(StringComparer.Ordinal);

    private static void OwnLifeBurst(UnitEntity owner, IEntity seed)
    {
        var key = seed.Guid.ToString();
        if (bursted.Count >= AuraCap * 4) bursted.Clear();
        if (!bursted.Add(key)) return;
        var inMap = CoreGame.InMap;
        var map = CoreGame.CurrentMap;
        if (CoreGame.TryMapOfUnit(owner, out var ownerMap)) map = ownerMap;
        if (inMap == null) return;
        Vector2Int at;
        try { at = inMap.GetLogicalPosition(seed.Guid); }
        catch (Exception)
        {
            if (!auras.TryGetValue(key, out var aura) || aura == null) return;
            at = aura._position;
        }
        var cells = new HashSet<(int, int)>(GroupAiHealerSeedRules.AroundCells(at.x, at.y));
        var alive = inMap.GetAliveUnitsOnMap();
        var healed = 0;
        for (var i = 0; alive != null && i < alive.Count; i++)
        {
            var unit = alive[i];
            if (unit == null || unit.IsDead || !CoreFaction.IsBandit(unit)) continue;
            var pos = inMap.GetLogicalPosition(unit.Guid);
            if (!cells.Contains((pos.x, pos.y))) continue;
            var max = CoreGame.Stat(unit, StatType.MaxHealthHead) + CoreGame.Stat(unit, StatType.MaxHealthBody);
            var amount = GroupAiHealerSeedRules.LifeBurstHeal(max, lifeBurstShare);
            if (amount <= 0f) continue;
            PublishHeal(map, unit, owner, amount);
            healed++;
            if (burstLines++ < GroupAiBandits.LogCap)
                DelversHost.Info(GroupAiHealerSeedRules.LifeBurstLine(owner.Guid.ToString(), unit.Guid.ToString(), max, lifeBurstShare, amount, Tick()));
        }
        if (healed == 0 && burstLines++ < GroupAiBandits.LogCap) DelversHost.Info(GroupAiHealerSeedRules.LifeBurstNoneLine(owner.Guid.ToString(), key, Tick()));
    }

    // The unit that summoned a building, when it is one of Core's.
    private static UnitEntity CoreSummoner(IEntity building)
    {
        if (building == null) return null;
        var inMap = CoreGame.InMap;
        if (inMap == null) return null;
        var summoner = SummonedBuildingResolver.GetSummonerUnit(inMap, building);
        return summoner != null && CoreUnitRules.IsCoreUnit(summoner.Key) ? summoner : null;
    }

    internal static void OnAuraTarget(SeedAura aura, UnitEntity unit, ref bool result)
    {
        if (aura == null || unit == null) return;
        var summoner = CoreSummoner(aura.Owner);
        if (summoner == null)
        {
            GroupAiHealerSeedProbe.OnAllow(aura.Owner, null, result);
            return;
        }
        var native = result;
        // Plan (b) step 3: a Core summoner's seed heals through Core (HealStep), the game's heal of this aura stays off.
        var filtered = false;
        result = filtered;
        var key = summoner.Guid.ToString();
        if (filterLogged.Contains(key)) return;
        if (native != filtered)
        {
            filterLogged.Add(key);
            DelversHost.Info(GroupAiHealerSeedRules.AuraFilterLine(key, true, unit.Guid.ToString(), native, filtered));
            return;
        }
        filterChecks.TryGetValue(key, out var checks);
        filterChecks[key] = ++checks;
        if (checks < FilterQuietChecks) return;
        filterLogged.Add(key);
        DelversHost.Info(GroupAiHealerSeedRules.AuraFilterLine(key, false, unit.Guid.ToString(), native, filtered));
    }

    internal static void OnLifeBurstStep(LifeBurstRoutine routine)
    {
        var owner = routine?.__4__this?.Owner?.TryCast<UnitEntity>();
        if (owner == null || !CoreUnitRules.IsCoreUnit(owner.Key)) return;
        var heal = routine.__2__current?.TryCast<HealExecutionData>();
        var target = heal?.Target?.TryCast<UnitEntity>();
        if (target == null) return;
        // The burst healed the player her seed sat on (the game picks the neighbours from the seed's side,
        // which is the players'). For a Core owner a heal on a unit not of Core's yields nothing (Execution.None).
        if (!GroupAiHealerSeedRules.LifeBurstKeeps(true, CoreFaction.IsBandit(target)))
        {
            routine.__2__current = Execution.None();
            if (burstLines++ < GroupAiBandits.LogCap)
                DelversHost.Info(GroupAiHealerSeedRules.LifeBurstDropLine(owner.Guid.ToString(), target.Guid.ToString(), Tick()));
            return;
        }
        var max = CoreGame.Stat(target, StatType.MaxHealthHead) + CoreGame.Stat(target, StatType.MaxHealthBody);
        var amount = GroupAiHealerSeedRules.LifeBurstHeal(max, lifeBurstShare);
        routine.__2__current = Execution.Heal(target.Cast<IEntity>(), DataValue.FixedValue(amount), (DamageProperty)0);
        if (burstLines++ < GroupAiBandits.LogCap)
            DelversHost.Info(GroupAiHealerSeedRules.LifeBurstLine(owner.Guid.ToString(), target.Guid.ToString(), max, lifeBurstShare, amount, Tick()));
    }

    internal static void OnSprayerDamage(SprayerDamageRoutine routine)
    {
        if (routine == null || routine.__1__state != 0) return;
        var summoner = CoreSummoner(routine.__4__this?.Owner);
        if (summoner == null) return;
        var target = routine.context?.TargetEntity?.TryCast<UnitEntity>();
        // The hit's effect (VFX 129) at this unit is the bandits' (BanditTint).
        BanditTint.NoteHit(target);
        if (sprayerLines >= GroupAiBandits.LogCap) return;
        var side = target == null ? "-" : CoreFactionRules.SideText(CoreFaction.IsBandit(target));
        sprayerLines++;
        DelversHost.Info(GroupAiHealerSeedRules.SprayerHitLine(summoner.Guid.ToString(), target?.Guid.ToString() ?? "-", side, Tick()));
    }

    internal static void OnSeedArea(VFXView view, Il2CppSystem.Guid fx, VFXID id, Vector2 position)
    {
        if (id != VFXID.StrangeSeedArea && id != VFXID.StrangeSeedEvolving) return;
        if (probed.Count >= GroupAiHealerSeedRules.ProbeCap) return;
        var inMap = CoreGame.InMap;
        var buildings = inMap?.GetCombatableBuildingsOnMap();
        IEntity seed = null;
        var best = float.MaxValue;
        for (var i = 0; buildings != null && i < buildings.Count; i++)
        {
            var building = buildings[i];
            if (building == null || building.IsDead || building.Key != GroupAiHealerSeedRules.SeedBuilding) continue;
            var at = inMap.GetWorldPosition(building.Guid);
            var d = (at - position).sqrMagnitude;
            if (d < best) { best = d; seed = building.Cast<IEntity>(); }
        }
        if (seed == null || best > 1f || !probed.Add(seed.Guid.ToString())) return;
        var seedId = seed.Guid.ToString();
        var summoner = SummonedBuildingResolver.GetSummonerUnit(inMap, seed);
        var core = summoner != null && CoreUnitRules.IsCoreUnit(summoner.Key);
        string toPlayer = null, toBandit = null;
        foreach (var unit in CoreGame.PlayerUnits(false))
        {
            toPlayer = EntityExtensions.IsHostile(seed, unit.Cast<IEntity>()) ? "1" : "0"; // trap-ok: log line only, the game's own answer
            break;
        }
        if (core) toBandit = EntityExtensions.IsHostile(seed, summoner.Cast<IEntity>()) ? "1" : "0"; // trap-ok: log line only, the game's own answer
        DelversHost.Info(GroupAiHealerSeedRules.SideLine(seedId, summoner?.Guid.ToString(), core, toPlayer, toBandit));
        Tree(seedId, "building", CoreGame.EntityObjectOf(view?._objectContainer, seed.Guid)?.Transform);
        Tree(seedId, "area:" + id, view?._vfxContainer?.GetVFX(fx)?.transform);
    }

    private static void Tree(string seed, string root, Transform top)
    {
        if (top == null)
        {
            DelversHost.Info(GroupAiHealerSeedRules.FxLine(seed, root, null, null, null, null, -1));
            return;
        }
        var pooled = top.parent != null && top.parent.name.IndexOf("pool", StringComparison.OrdinalIgnoreCase) >= 0 ? 1 : 0;
        var nodes = 0;
        var stack = new Stack<(Transform T, string Path)>();
        stack.Push((top, root + "/" + top.name));
        while (stack.Count > 0 && nodes++ < TreeNodes)
        {
            var (t, path) = stack.Pop();
            var names = new List<string>();
            foreach (var component in t.GetComponents<Component>())
                if (component != null) names.Add(component.GetIl2CppType().Name);
            var sprite = t.GetComponent<SpriteRenderer>();
            var renderer = t.GetComponent<Renderer>();
            var color = sprite != null ? ColorText(sprite.color) : null;
            DelversHost.Info(GroupAiHealerSeedRules.FxLine(seed, path, string.Join("+", names), renderer != null ? renderer.GetIl2CppType().Name : null,
                renderer != null && renderer.sharedMaterial != null ? renderer.sharedMaterial.name : null, color, pooled));
            for (var i = t.childCount - 1; i >= 0; i--)
            {
                var child = t.GetChild(i);
                stack.Push((child, path + "/" + child.name));
            }
        }
    }

    private static string ColorText(Color c)
        => string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.00},{1:0.00},{2:0.00},{3:0.00}", c.r, c.g, c.b, c.a);

    private static long Tick() => CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0;
}

[HarmonyPatch(typeof(SeedAura), "_HealRoutine_b__9_0")]
internal static class GroupAiHealerSeedAuraPatch
{
    private static void Postfix(SeedAura __instance, UnitEntity unit, ref bool __result)
    {
        try { GroupAiHealerSeed.OnAuraTarget(__instance, unit, ref __result); }
        catch (Exception ex) { DelversHost.Warning("CORE_SEED_AURA_FILTER_FAILED: " + ex.Message); }
    }
}

[HarmonyPatch(typeof(LifeBurstRoutine), "MoveNext")]
internal static class GroupAiHealerLifeBurstPatch
{
    private static void Postfix(LifeBurstRoutine __instance, bool __result)
    {
        if (!__result) return;
        try { GroupAiHealerSeed.OnLifeBurstStep(__instance); }
        catch (Exception ex) { DelversHost.Warning("CORE_LIFEBURST_HEAL_FAILED: " + ex.Message); }
    }
}

[HarmonyPatch(typeof(SprayerDamageRoutine), "MoveNext")]
internal static class GroupAiHealerSprayerPatch
{
    private static void Prefix(SprayerDamageRoutine __instance)
    {
        try { GroupAiHealerSeed.OnSprayerDamage(__instance); }
        catch (Exception ex) { DelversHost.Warning("CORE_MUCUS_SPRAYER_HIT_FAILED: " + ex.Message); }
    }
}

// The seed probe's entry into the game's VFX creation: it carries OnSeedArea only, so it is a probe class of the group Seeds.
[ProbeGroup(CoreProbeRules.Seeds)]
[HarmonyPatch(typeof(VFXView), nameof(VFXView.CreateVFX))]
internal static class GroupAiHealerSeedProbePatch
{
    private static void Postfix(VFXView __instance, Il2CppSystem.Guid guid, VFXID id, Vector2 position)
    {
        try { GroupAiHealerSeed.OnSeedArea(__instance, guid, id, position); }
        catch (Exception ex) { DelversHost.Warning("CORE_SEED_FX_PROBE_FAILED: " + ex.Message); }
    }
}

[HarmonyPatch(typeof(SeedAura), "Regen")]
internal static class GroupAiHealerSeedRegenPatch
{
    private static void Postfix(SeedAura __instance)
    {
        try { GroupAiHealerSeed.OnRegen(__instance); }
        catch (Exception ex) { DelversHost.Warning("CORE_SEED_REGEN_FAILED: " + ex.Message); }
    }
}

[HarmonyPatch(typeof(LifeBurstPassive), "TryActivateLifeBurst")]
internal static class GroupAiHealerLifeBurstTryPatch
{
    private static bool Prefix(LifeBurstPassive __instance, IEntity target)
    {
        try { return GroupAiHealerSeed.OnLifeBurstTry(__instance, target); }
        catch (Exception ex) { DelversHost.Warning("CORE_LIFEBURST_PROBE_FAILED try: " + ex.Message); return true; }
    }
}
