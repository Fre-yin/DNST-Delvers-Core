using System.Globalization;
using UnityEngine;

namespace DungeonSettlersDelvers.Core;

// The bleeding wound of combat Apocalypse in game (rules: WoundRules). Also on other campaigns as the option "wounds"
// (ApocalypseRules); without the rule a
// load closes the wounds still in the data.
// - Every stack of the game's bleeding on a party member adds to the wound (WoundHitPatch); the
//   buffer lives in Core's save data (CoreSaveData), so a load without Core loses nothing.
// - Core's clock drains the buffer in game time (ticks, so pause and speed count): the body first,
//   the head once the body is empty (WoundRules.DrainsHead).
// - The drain goes past the shield, as the game's bleeding does: the game's bleeding hits with OnlyHealth, and a hit wears shields down
//   only without OnlyHealth; the wound takes health straight from HealthBody and HealthHead (DrainOne).
// - Heals on the body, and the head's regeneration while the wound bleeds there, are changed in
//   StatusApplier.Apply (WoundHealPatch).
// - The effect in the bar shows the stage with the stacks as its count; it never reaches the game's
//   part of a save (CoreSaveEffects) and comes back after loading.
// - A revive with the scroll (ReviveService), a rescue from near-death (CheckRescues) and the game's
//   first aid kit or black serum (WoundAidPatches) close the wound and take the game's bleeding with
//   it (ClearAll; a rescue did not before).
// - The wound shows on the body: the game's looping bleeding at the unit, a sparse blood trail for a
//   lethal wound (WoundFx).
internal static class Wounds
{
    internal const string BleedingKey = "AFFECTER_Bleeding";
    internal const string PortalHealKey = "AFFECTER_PortalHeal";
    internal const string BedSleepKey = "AFFECTER_BedSleep";
    internal const string BossKey = "AFFECTER_BossMonster";
    internal const string EliteKey = "AFFECTER_EliteMonster";

    private const float DrainLogSeconds = 30f;

    private static readonly HashSet<Guid> open = new();
    private static readonly Dictionary<Guid, double> carry = new();
    // Health the wound has drained since it opened, all and from the head; read by the tests.
    private static readonly Dictionary<Guid, double> drainedTotal = new();
    private static readonly Dictionary<Guid, double> drainedHead = new();
    private static readonly HashSet<Guid> dirty = new();
    private static readonly Dictionary<Guid, float> lastDrainLog = new();
    private static readonly Dictionary<Guid, long> lastJumpTick = new();
    private static readonly Dictionary<Guid, double> forcedRoll = new();
    private static readonly System.Random dice = new();
    // The open wounds that drained in this tick, for WoundFx.EndTick.
    private static readonly HashSet<Guid> ticked = new();
    private static long lastTick = -1;

    // Units at near-death in the last tick; one that left it alive was rescued (CheckRescues).
    private const string SevereDamageKey = "AFFECTER_SevereDamage";
    private static readonly HashSet<Guid> nearDeath = new();
    private static readonly HashSet<Guid> nearDeathNow = new();

    // The weaker heals of a wound, summed per unit and tick into one log line (FlushHealCuts), at
    // most HealCutLogCap lines a campaign.
    private const int HealCutLogCap = 5000;
    private static readonly Dictionary<Guid, HealCut> healCuts = new();
    private static int healCutLines;

    private sealed class HealCut
    {
        internal WoundRules.Stage Stage;
        // The last heal's factor in the tick (variant B: it falls with the buffer on a lethal wound).
        internal double Factor;
        internal double Before;
        internal double After;
        internal double Buffer;
        internal int Heals;
    }

    internal static bool AnyOpen => open.Count > 0;
    internal static IReadOnlyCollection<Guid> OpenUnits => open;
    internal static bool IsOpen(Guid unit) => unit != Guid.Empty && open.Contains(unit);

    internal static void ResetCampaign()
    {
        open.Clear();
        carry.Clear();
        drainedTotal.Clear();
        drainedHead.Clear();
        dirty.Clear();
        lastDrainLog.Clear();
        lastJumpTick.Clear();
        forcedRoll.Clear();
        healCuts.Clear();
        healCutLines = 0;
        healLines = 0;
        soon.Clear();
        waitLogged.Clear();
        regenLogged.Clear();
        nearDeath.Clear();
        nearDeathNow.Clear();
        lastTick = -1;
        WoundFx.ResetCampaign();
    }

    // A save was applied or a campaign started: the open wounds come from Core's data, and their
    // effects are put back with the next tick. Without the rule they close at once.
    internal static void OnLoaded()
    {
        ResetCampaign();
        var data = CoreSaveStore.Current;
        if (data == null) return;
        foreach (var unit in data.WoundedUnits())
        {
            open.Add(unit);
            dirty.Add(unit);
        }
        EndOnCorpses();
        if (open.Count == 0) return;
        DelversHost.Info("CORE_WOUND_RESTORED units=" + open.Count);
        if (ApocalypseRules.Wounds) return;
        foreach (var unit in open.ToList()) Close(unit, "option");
    }

    // A save from before the death hook can hold a dead party member with a wound or the game's bleeding: it ends now.
    private static void EndOnCorpses()
    {
        var maps = CoreGame.Flow?._inMapDatas;
        if (maps == null) return;
        foreach (var pair in maps)
        {
            var units = pair.Value?.GetAllPlayerUnits(true);
            for (var i = 0; units != null && i < units.Count; i++)
            {
                if (units[i] == null || !units[i].IsDead) continue;
                try { OnDeath(pair.Key, units[i], "load"); }
                catch (Exception ex) { ApocalypseHooks.Report("wound-death", ex); }
            }
        }
    }

    internal static double Buffer(Guid unit) => CoreSaveStore.Current?.WoundBuffer(unit) ?? 0;

    internal static WoundRules.Stage Stage(Guid unit) => StageAt(unit, Buffer(unit));

    // The stage a buffer has for this unit (set C: against its whole health, remembered from the last time Core saw it).
    internal static WoundRules.Stage StageAt(Guid unit, double buffer)
        => WoundRules.StageOf(buffer, MaxLife(unit), CoreSaveStore.Current?.IsWoundFatal(unit) == true);

    internal static bool IsFatal(Guid unit) => Stage(unit) == WoundRules.Stage.Fatal;

    // The unit's whole health (body + head), remembered per unit; 0 before Core saw it (WoundRules.DefaultMaxLife then).
    private static readonly Dictionary<Guid, double> maxLife = new();

    internal static double MaxLife(Guid unit) => maxLife.TryGetValue(unit, out var max) ? max : 0;

    internal static double SeeMaxLife(UnitEntity unit, Guid guid)
    {
        var max = CoreGame.Stat(unit, StatType.MaxHealthBody) + CoreGame.Stat(unit, StatType.MaxHealthHead);
        if (max > 0 && !float.IsNaN(max) && !float.IsInfinity(max)) maxLife[guid] = max;
        return MaxLife(guid);
    }

    // ---- changes ----

    // Bleeding stacks landed on a party member (WoundHitPatch); only where wounds exist. The
    // game's stacks count first, then the roll (WoundRules.RollOf); the attacker decides the elite
    // chances.
    internal static void Hit(UnitEntity unit, int stacks, IEntity attacker)
    {
        if (stacks <= 0 || unit == null || unit.IsDead || !ApocalypseRules.Wounds || !IsPlayer(unit)) return;
        var guid = CoreGame.GuidOf(unit);
        var max = SeeMaxLife(unit, guid);
        var landed = WoundRules.Hit(Buffer(guid), stacks);
        var tick = CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0;
        var since = lastJumpTick.TryGetValue(guid, out var last) ? (tick - last) / WoundRules.TicksPerSecond : double.PositiveInfinity;
        var rolled = WoundRules.RollOf(NextRollValue(guid), IsEliteOrBoss(attacker));
        var roll = WoundRules.Limit(rolled, since);
        if (roll == WoundRules.Roll.Jump) lastJumpTick[guid] = tick;
        var after = WoundRules.AfterRoll(landed, roll, max);
        Set(guid, after, "hit");
        // The lethal check and the taking-in of the game's stacks at the next game tick, not a second later.
        soon.Add(guid);
        if (roll == WoundRules.Roll.None) return;
        DelversHost.Info($"CORE_WOUND_ROLL unit={guid:D} roll={WoundRules.Describe(roll)} stacks={WoundRules.Stacks(landed)}->{WoundRules.Stacks(after)}"
            + " source=" + (attacker?.Key ?? "none") + (rolled != roll ? " cooldown=yes" : ""));
    }

    private static double NextRollValue(Guid unit)
    {
        if (!forcedRoll.TryGetValue(unit, out var value)) return dice.NextDouble();
        forcedRoll.Remove(unit);
        return value;
    }

    private static bool IsEliteOrBoss(IEntity attacker)
    {
        var affecters = CoreGame.Affecters(attacker?.TryCast<UnitEntity>());
        return affecters != null && (affecters.Has(BossKey) || affecters.Has(EliteKey));
    }

    // Test hook (DelversCoreRuntime.SetWoundRollForTest): the unit's next roll uses this value;
    // null clears it and lifts the unit's jump cooldown.
    internal static void ForceRoll(Guid unit, double? value)
    {
        if (value is { } v) forcedRoll[unit] = v;
        else
        {
            forcedRoll.Remove(unit);
            lastJumpTick.Remove(unit);
        }
    }

    internal static void Set(Guid unit, double buffer, string reason)
    {
        var data = CoreSaveStore.Current;
        if (data == null || unit == Guid.Empty) return;
        var before = data.WoundBuffer(unit);
        data.SetWoundBuffer(unit, buffer);
        var after = data.WoundBuffer(unit);
        if (before <= 0 && after > 0)
        {
            drainedTotal[unit] = 0;
            drainedHead[unit] = 0;
        }
        // The first wound from a hit explains itself once (FirstHints).
        if (before <= 0 && after > 0 && reason == "hit") FirstHints.Request(FirstHintText.Wounds);
        if (after > 0) open.Add(unit);
        else
        {
            open.Remove(unit);
            carry.Remove(unit);
            lastDrainLog.Remove(unit);
            WoundFx.Forget(unit);
            if (before > 0) WoundTooltip.Refresh();
        }
        if (WoundRules.Stacks(before) != WoundRules.Stacks(after) || StageAt(unit, before) != StageAt(unit, after))
        {
            dirty.Add(unit);
            soon.Add(unit);
        }
        if (after <= 0) waitLogged.Remove(unit);
        if (reason == "drain" && after > 0 && StageAt(unit, before) == StageAt(unit, after)
            && lastDrainLog.TryGetValue(unit, out var last) && Time.unscaledTime - last < DrainLogSeconds) return;
        if (reason == "drain") lastDrainLog[unit] = Time.unscaledTime;
        Log(unit, after, after > 0 ? reason : "closed", after > 0 ? null : reason);
    }

    internal static void Close(Guid unit, string reason)
    {
        if (Buffer(unit) <= 0) return;
        Set(unit, 0, reason);
    }

    // A party member is saved (source: "scroll", "rescue", "firstaid" or "serum"): the wound closes
    // and the game's bleeding goes with it, the effect at once.
    internal static void ClearAll(UnitEntity unit, string source)
    {
        if (unit == null || !(ApocalypseRules.Wounds || IsOpen(CoreGame.GuidOf(unit))) || !CoreGame.TryMapOfPartyMember(unit, out var map)) return;
        var guid = CoreGame.GuidOf(unit);
        var buffer = Buffer(guid);
        var affecters = CoreGame.Affecters(unit);
        var bleeding = affecters != null && affecters.Has(BleedingKey) ? affecters.GetStack(BleedingKey) : 0;
        if (bleeding > 0)
            CoreGame.Apply(map, new AffecterApplyData(unit.Cast<IEntity>(), BleedingKey, -bleeding, 0f, false, null).Cast<IApplyData>());
        Close(guid, source);
        SyncNow(unit);
        DelversHost.Info($"CORE_WOUND_CLEARED source={source} unit={guid:D} stacks={WoundRules.Stacks(buffer)}->0 bleeding={bleeding}->0");
    }

    // A unit died (WoundDeathPatch) or lies dead in a loaded save (OnLoaded): its bleeding ends, so the corpse shows none.
    // The game's bleeding stacks go on every unit, Core puts them on foes too (BossEffects,
    // EnemyAbilities); on a party member also Core's buffer with its lethal mark and the stage effect in the bar. A revive
    // starts without a wound, as the scroll's ClearAll did before; nothing of the wound is needed to revive.
    internal static void OnDeath(MapType map, UnitEntity unit, string source)
    {
        var affecters = CoreGame.Affecters(unit);
        if (affecters == null) return;
        var guid = CoreGame.GuidOf(unit);
        var party = IsPlayer(unit);
        var buffer = party ? Buffer(guid) : 0;
        var bleeding = affecters.Has(BleedingKey) ? affecters.GetStack(BleedingKey) : 0;
        var effects = new List<(string Key, int Stacks)>();
        foreach (var key in new[] { WoundText.LightKey, WoundText.DeepKey, WoundText.HeavyKey, WoundText.FatalKey })
            if (affecters.Has(key) && affecters.GetStack(key) > 0) effects.Add((key, affecters.GetStack(key)));
        var effectStacks = effects.Sum(effect => effect.Stacks);
        if (!WoundRules.EndsAtDeath(buffer, bleeding, effectStacks)) return;
        if (bleeding > 0) RemoveBleeding(map, unit, bleeding);
        foreach (var (key, stacks) in effects)
            CoreGame.Apply(map, new AffecterApplyData(unit.Cast<IEntity>(), key, -stacks, 0f, false, null).Cast<IApplyData>());
        if (party)
        {
            Close(guid, source);
            dirty.Remove(guid);
            soon.Remove(guid);
            WoundFx.Forget(guid);
        }
        DelversHost.Info(WoundRules.DeathLine(guid.ToString("D"), source, party, buffer, bleeding, effectStacks));
    }

    // A heal on a wounded body (WoundHealPatch): the amount the game should apply. The buffer part
    // is taken right away.
    internal static float Heal(UnitEntity unit, float amount, bool from, bool execution, bool staffEased = false)
    {
        var guid = CoreGame.GuidOf(unit);
        SeeMaxLife(unit, guid);
        var buffer = Buffer(guid);
        var stage = StageAt(guid, buffer);
        if (stage == WoundRules.Stage.None) return amount;
        var kind = WoundRules.KindOf(from, execution, CoreGame.Affecters(unit)?.Has(PortalHealKey) == true);
        var current = CoreGame.Stat(unit, StatType.HealthBody);
        var max = CoreGame.Stat(unit, StatType.MaxHealthBody);
        // Variant B: on a lethal wound the factor falls with the buffer over the whole health (WoundRules.FatalFactor).
        var wound = WoundRules.HealFactor(stage, buffer, MaxLife(guid));
        var factor = BossEffectRules.EasedHealFactor(wound, staffEased && kind == WoundRules.HealKind.Sourced);
        if (factor != wound)
            BossEffects.Line("overgrowth-ease", unit.Cast<IEntity>(), FormattableString.Invariant($"factor={wound:0.###}->{factor:0.###}"));
        var (heal, taken) = WoundRules.Heal(stage, kind, amount, float.IsNaN(current) ? 0 : current, float.IsNaN(max) ? 0 : max, factor);
        if (taken > 0) Set(guid, buffer - taken, kind == WoundRules.HealKind.Portal ? "portal" : "heal");
        if (kind == WoundRules.HealKind.Sourced) NoteHealCut(guid, stage, amount, heal, taken, factor);
        LogHeal(guid, "body", SourceOf(from, execution), kind, stage, amount, heal, max - current, taken,
            kind == WoundRules.HealKind.Sourced ? factor : double.NaN);
        return (float)heal;
    }

    // At most this many CORE_WOUND_HEAL lines per campaign; regeneration once per unit every RegenLogSeconds.
    private const int HealLogCap = 3000;
    private const float RegenLogSeconds = 10f;
    private static int healLines;
    private static readonly Dictionary<Guid, float> regenLogged = new();

    // The head of a wounded unit stops regenerating while its body drains (WoundHealPatch).
    internal static void LogHeadBlocked(UnitEntity unit, float amount)
    {
        var guid = CoreGame.GuidOf(unit);
        LogHeal(guid, "head", "none", WoundRules.HealKind.Regeneration, Stage(guid), amount, 0,
            CoreGame.Stat(unit, StatType.MaxHealthHead) - CoreGame.Stat(unit, StatType.HealthHead), 0);
    }

    // What a heal with a source gives beyond full health (the game capped it, or Lifebloom at full
    // health): it takes the buffer, like the overheal of WoundRules.Heal.
    internal static void Overheal(UnitEntity unit, double amount, string source)
    {
        var guid = CoreGame.GuidOf(unit);
        if (!IsOpen(guid) || !(amount > 0)) return;
        var buffer = Buffer(guid);
        var stage = StageAt(guid, buffer);
        if (stage == WoundRules.Stage.None) return;
        var max = CoreGame.Stat(unit, StatType.MaxHealthBody);
        if (float.IsNaN(max)) max = 0;
        var factor = WoundRules.HealFactor(stage, buffer, MaxLife(guid));
        var (_, taken) = WoundRules.Heal(stage, WoundRules.HealKind.Sourced, amount, max, max, factor);
        if (taken > 0) Set(guid, buffer - taken, "overheal");
        LogHeal(guid, "over", source, WoundRules.HealKind.Sourced, stage, amount, 0, max - CoreGame.Stat(unit, StatType.HealthBody), taken, factor);
    }

    private static string SourceOf(bool from, bool execution) => execution ? "exec" : from ? "from" : "none";

    private static void LogHeal(Guid guid, string stat, string source, WoundRules.HealKind kind, WoundRules.Stage stage,
        double amountIn, double amountOut, double room, double taken, double factor = double.NaN)
    {
        if (kind == WoundRules.HealKind.Regeneration)
        {
            var now = Time.realtimeSinceStartup;
            if (regenLogged.TryGetValue(guid, out var at) && now - at < RegenLogSeconds) return;
            regenLogged[guid] = now;
        }
        if (healLines++ >= HealLogCap) return;
        DelversHost.Info(WoundRules.HealLine(guid.ToString("D"), stat, source, kind, stage, amountIn, amountOut, room, taken,
            CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0, factor));
    }

    private static void NoteHealCut(Guid guid, WoundRules.Stage stage, double before, double after, double buffer, double factor)
    {
        if (!healCuts.TryGetValue(guid, out var cut)) healCuts[guid] = cut = new HealCut();
        cut.Stage = stage;
        cut.Factor = factor;
        cut.Before += before;
        cut.After += after;
        cut.Buffer += buffer;
        cut.Heals++;
    }

    // One line per healed unit and tick: the heals, what the wound let through and what went into
    // the buffer.
    private static void FlushHealCuts()
    {
        if (healCuts.Count == 0) return;
        foreach (var pair in healCuts)
        {
            if (healCutLines++ >= HealCutLogCap) break;
            var cut = pair.Value;
            DelversHost.Info($"CORE_WOUND_HEAL_CUT unit={pair.Key:D} stage={cut.Stage} factor={cut.Factor.ToString("0.00", CultureInfo.InvariantCulture)}"
                + $" heals={cut.Heals} before={cut.Before.ToString("0.0", CultureInfo.InvariantCulture)} after={cut.After.ToString("0.0", CultureInfo.InvariantCulture)}"
                + $" buffer={cut.Buffer.ToString("0.0", CultureInfo.InvariantCulture)}");
        }
        healCuts.Clear();
    }

    // The game's refill of the head when a unit goes down (WoundHealPatch, before it is applied): a lethal wound grows by it
    // (WoundRules.FatalRecovery). CORE_WOUND_FATAL_RECOVERY unit= added= buffer= life= (life after the refill).
    internal static void OnFallRefill(UnitEntity unit, float amount, bool sourced)
    {
        var guid = CoreGame.GuidOf(unit);
        var head = CoreGame.Stat(unit, StatType.HealthHead);
        var added = WoundRules.FatalRecovery(IsFatal(guid), head, sourced, amount);
        if (!(added > 0)) return;
        var buffer = Buffer(guid) + added;
        Set(guid, buffer, "fall");
        var life = Math.Max(0, head) + added + Math.Max(0, CoreGame.Stat(unit, StatType.HealthBody));
        DelversHost.Info(FormattableString.Invariant($"CORE_WOUND_FATAL_RECOVERY unit={guid:D} added={added:0.0} buffer={buffer:0.0} life={life:0.0}"));
    }

    // A heal without a source on the head (WoundHealPatch): its regeneration stops while the wound
    // bleeds at the head (WoundRules.BlocksHeadRegeneration).
    internal static bool BlocksHeadRegeneration(UnitEntity unit)
        => WoundRules.BlocksHeadRegeneration(Stage(CoreGame.GuidOf(unit)), CoreGame.Stat(unit, StatType.HealthBody),
            CoreGame.Stat(unit, StatType.HealthHead), CoreGame.Affecters(unit)?.Has(PortalHealKey) == true);

    // ---- clock ----

    // Once a second from Core's clock: drain in game time, keep the effects in step and look for
    // rescues.
    internal static void Tick()
    {
        var data = CoreSaveStore.Current;
        var maps = CoreGame.Flow?._inMapDatas;
        var time = CoreGame.Flow?._campaignData?.TimeData;
        if (data == null || maps == null || time == null) return;
        var tick = time.Tick;
        var seconds = lastTick >= 0 && tick >= lastTick ? (tick - lastTick) / WoundRules.TicksPerSecond : 0;
        lastTick = tick;
        ticked.Clear();
        FlushHealCuts();
        if (open.Count > 0 || dirty.Count > 0)
        {
            // A big jump means a load or a long pause of Core's clock, not bleeding time.
            if (seconds > 60) seconds = 0;
            foreach (var pair in maps)
            {
                var units = pair.Value?.GetAllPlayerUnits(false);
                for (var i = 0; units != null && i < units.Count; i++)
                {
                    var unit = units[i];
                    if (unit == null || unit.IsDead) continue;
                    var guid = CoreGame.GuidOf(unit);
                    if (open.Contains(guid))
                    {
                        // A unit Core sees for the first time after a load: its effect follows the stage of its own health.
                        if (MaxLife(guid) == 0) dirty.Add(guid);
                        SeeMaxLife(unit, guid);
                        var drained = DrainOne(pair.Key, unit, guid, seconds);
                        CheckFatal(pair.Key, unit, guid);
                        if (open.Contains(guid)) ticked.Add(guid);
                        // The effects are view only; a failure must not stop the drain.
                        try
                        {
                            // Lying is every unconscious unit (the game's category,
                            // as the bandits see the fallen, GroupAiBandits.IsDowned) or one with AFFECTER_Deadly; before only Deadly, so a
                            // fallen unit kept the drawn body blood, which tipped over with it, and got no pools on the ground.
                            var lying = WoundRules.IsLying(CoreGame.Affecters(unit)?.Has(ReviveSnapshots.DeadlyKey) == true, GroupAiBandits.IsDowned(unit));
                            WoundFx.OnTick(pair.Key, pair.Value, unit, guid, Stage(guid), lying, seconds, drained, tick);
                        }
                        catch (Exception ex) { ApocalypseHooks.Report("wound-fx", ex); }
                    }
                    if (dirty.Remove(guid)) SyncEffect(pair.Key, unit, guid);
                }
            }
        }
        if (ApocalypseRules.Wounds) CheckRescues();
        try { WoundFx.EndTick(ticked, tick); }
        catch (Exception ex) { ApocalypseHooks.Report("wound-fx", ex); }
    }

    // A party member that was at near-death (severe damage or deadly) in the last tick and is no
    // longer, alive, was rescued: its wound and bleeding end (ClearAll).
    private static void CheckRescues()
    {
        var maps = CoreGame.Flow?._inMapDatas;
        if (maps == null) return;
        nearDeathNow.Clear();
        foreach (var pair in maps)
        {
            var units = pair.Value?.GetAllPlayerUnits(false);
            for (var i = 0; units != null && i < units.Count; i++)
            {
                var unit = units[i];
                if (unit == null || unit.IsDead) continue;
                var affecters = CoreGame.Affecters(unit);
                if (affecters == null) continue;
                var guid = CoreGame.GuidOf(unit);
                if (affecters.Has(SevereDamageKey) || affecters.Has(ReviveSnapshots.DeadlyKey)) nearDeathNow.Add(guid);
                else if (nearDeath.Contains(guid))
                {
                    var bleeding = affecters.Has(BleedingKey) ? affecters.GetStack(BleedingKey) : 0;
                    if (Buffer(guid) > 0 || bleeding > 0) ClearAll(unit, "rescue");
                }
            }
        }
        nearDeath.Clear();
        foreach (var guid in nearDeathNow) nearDeath.Add(guid);
    }

    // Units hit or whose wound changed since the last game tick (GroupAiBanditTickPatch): the lethal check and the effect's
    // count at once (x15 shown while Core counted 13), outside the game's own apply, inside which Hit and
    // Heal run. Core's clock still checks every second.
    private static readonly HashSet<Guid> soon = new();

    internal static void OnGameTick()
    {
        if (soon.Count == 0) return;
        var maps = CoreGame.Flow?._inMapDatas;
        if (maps == null)
        {
            soon.Clear();
            return;
        }
        foreach (var pair in maps)
        {
            var units = pair.Value?.GetAllPlayerUnits(false);
            for (var i = 0; units != null && i < units.Count && soon.Count > 0; i++)
            {
                var unit = units[i];
                if (unit == null || unit.IsDead) continue;
                var guid = CoreGame.GuidOf(unit);
                if (!soon.Remove(guid)) continue;
                if (open.Contains(guid))
                {
                    SeeMaxLife(unit, guid);
                    CheckFatal(pair.Key, unit, guid);
                }
                if (dirty.Remove(guid)) SyncEffect(pair.Key, unit, guid);
            }
        }
        soon.Clear();
    }

    // Once per wound: heavy, the buffer at least the body's health, but not the whole health with the head
    // (CORE_WOUND_FATAL event=wait body= head=), so a test sees why it is not lethal yet.
    private static readonly HashSet<Guid> waitLogged = new();

    // The lethal wound (WoundRules.BecomesFatal): it begins when the buffer covers the health left (by default only from
    // heavy on), takes the game's bleeding stacks into the buffer at 10 health each and stays until the wound closes.
    // While it lasts, new bleeding stacks are counted by Hit and taken off the game's bleeding here. CORE_WOUND_FATAL
    // event=enter|absorb.
    // After a bandage: a lethal wound whose buffer no longer covers the health left ends, and the stage follows the thresholds
    // again (WoundRules.LeavesFatal). CORE_WOUND_FATAL event=leave.
    internal static void LeaveFatal(UnitEntity unit, Guid guid)
    {
        var data = CoreSaveStore.Current;
        if (data == null || unit == null || !data.IsWoundFatal(guid)) return;
        var body = CoreGame.Stat(unit, StatType.HealthBody);
        var head = CoreGame.Stat(unit, StatType.HealthHead);
        var life = Math.Max(0f, body) + Math.Max(0f, head);
        var buffer = Buffer(guid);
        if (!WoundRules.LeavesFatal(buffer, life)) return;
        data.SetWoundFatal(guid, false);
        dirty.Add(guid);
        DelversHost.Info(FormattableString.Invariant(
            $"CORE_WOUND_FATAL unit={guid:D} event=leave to={WoundRules.Describe(Stage(guid))} life={life:0} buffer={buffer:0.0}"));
    }

    private static void CheckFatal(MapType map, UnitEntity unit, Guid guid)
    {
        var data = CoreSaveStore.Current;
        var buffer = Buffer(guid);
        // An open wound decides, not the option alone: a test campaign without the rule has wounds set by the test hook,
        // and they drain the same (woundrise on a campaign with wounds=false, buffer 136 over life 105.6, no).
        if (data == null || !(buffer > 0)) return;
        var affecters = CoreGame.Affecters(unit);
        var bleeding = affecters != null && affecters.Has(BleedingKey) ? affecters.GetStack(BleedingKey) : 0;
        if (data.IsWoundFatal(guid))
        {
            if (bleeding <= 0) return;
            RemoveBleeding(map, unit, bleeding);
            DelversHost.Info($"CORE_WOUND_FATAL unit={guid:D} event=absorb bleeding={bleeding}->0 buffer={buffer.ToString("0.0", CultureInfo.InvariantCulture)}");
            return;
        }
        var body = CoreGame.Stat(unit, StatType.HealthBody);
        var head = CoreGame.Stat(unit, StatType.HealthHead);
        if (float.IsNaN(body) || float.IsNaN(head)) return;
        var life = Math.Max(0f, body) + Math.Max(0f, head);
        if (!WoundRules.BecomesFatal(buffer, MaxLife(guid), life, CoreSettings.FatalFromHeavyOnly))
        {
            if (buffer >= Math.Max(0f, body) && StageAt(guid, buffer) == WoundRules.Stage.Heavy && waitLogged.Add(guid))
                DelversHost.Info($"CORE_WOUND_FATAL unit={guid:D} event=wait buffer={buffer.ToString("0.0", CultureInfo.InvariantCulture)}"
                    + $" body={body.ToString("0.0", CultureInfo.InvariantCulture)} head={head.ToString("0.0", CultureInfo.InvariantCulture)}"
                    + $" max={MaxLife(guid).ToString("0", CultureInfo.InvariantCulture)}");
            return;
        }
        var from = StageAt(guid, buffer);
        data.SetWoundFatal(guid, true);
        if (bleeding > 0) RemoveBleeding(map, unit, bleeding);
        var after = WoundRules.TakeOver(buffer, bleeding);
        dirty.Add(guid);
        Set(guid, after, "fatal");
        DelversHost.Info($"CORE_WOUND_FATAL unit={guid:D} event=enter from={WoundRules.Describe(from)} life={life.ToString("0", CultureInfo.InvariantCulture)}"
            + $" max={MaxLife(guid).ToString("0", CultureInfo.InvariantCulture)} bleeding={bleeding}->0"
            + $" buffer={buffer.ToString("0.0", CultureInfo.InvariantCulture)}->{after.ToString("0.0", CultureInfo.InvariantCulture)}");
    }

    private static void RemoveBleeding(MapType map, UnitEntity unit, int stacks)
        => CoreGame.Apply(map, new AffecterApplyData(unit.Cast<IEntity>(), BleedingKey, -stacks, 0f, false, null).Cast<IApplyData>());

    // The effect in the bar at once instead of with the next tick (a bandage, ClearAll).
    internal static void SyncNow(UnitEntity unit)
    {
        var guid = CoreGame.GuidOf(unit);
        if (!dirty.Contains(guid) || !CoreGame.TryMapOfPartyMember(unit, out var map)) return;
        dirty.Remove(guid);
        SyncEffect(map, unit, guid);
    }

    // The drained points of this call (0 until the next point is due).
    private static int DrainOne(MapType map, UnitEntity unit, Guid guid, double seconds)
    {
        var buffer = Buffer(guid);
        var stage = StageAt(guid, buffer);
        var affecters = CoreGame.Affecters(unit);
        carry.TryGetValue(guid, out var rest);
        var points = WoundRules.Drain(stage, affecters?.Has(BedSleepKey) == true, seconds, ref rest);
        carry[guid] = rest;
        if (points <= 0) return 0;
        points = (int)Math.Min(points, Math.Ceiling(buffer));
        var stat = WoundRules.DrainsHead(CoreGame.Stat(unit, StatType.HealthBody)) ? StatType.HealthHead : StatType.HealthBody;
        CoreGame.Apply(map, new StatusApplyData(unit.Cast<IEntity>(), stat, -points, (StatusApplyPersistence)0).Cast<IApplyData>());
        drainedTotal[guid] = (drainedTotal.TryGetValue(guid, out var total) ? total : 0) + points;
        if (stat == StatType.HealthHead) drainedHead[guid] = (drainedHead.TryGetValue(guid, out var head) ? head : 0) + points;
        Set(guid, buffer - points, "drain");
        return points;
    }

    // The effect of the stage, with the stacks as its count; the other stages removed.
    private static void SyncEffect(MapType map, UnitEntity unit, Guid guid)
    {
        var affecters = CoreGame.Affecters(unit);
        if (affecters == null) return;
        ReviveEffects.Ensure();
        var stacks = WoundRules.Stacks(Buffer(guid));
        var stage = Stage(guid);
        foreach (var key in new[] { WoundText.LightKey, WoundText.DeepKey, WoundText.HeavyKey, WoundText.FatalKey })
        {
            var want = key == EffectKey(stage) ? WoundRules.EffectStacks(stage, stacks) : 0;
            var have = affecters.Has(key) ? affecters.GetStack(key) : 0;
            if (want != have)
                CoreGame.Apply(map, new AffecterApplyData(unit.Cast<IEntity>(), key, want - have, 0f, false, null).Cast<IApplyData>());
        }
    }

    internal static string EffectKey(WoundRules.Stage stage) => stage switch
    {
        WoundRules.Stage.Light => WoundText.LightKey,
        WoundRules.Stage.Deep => WoundText.DeepKey,
        WoundRules.Stage.Heavy => WoundText.HeavyKey,
        WoundRules.Stage.Fatal => WoundText.FatalKey,
        _ => null
    };

    internal static bool IsWoundEffect(string key)
        => key == WoundText.LightKey || key == WoundText.DeepKey || key == WoundText.HeavyKey || key == WoundText.FatalKey;

    // ---- reading ----

    // For tests: "stage=deep stacks=15 buffer=150.0 drained=40 head=0" or "none".
    internal static string Status(Guid unit)
    {
        var buffer = Buffer(unit);
        if (!(buffer > 0)) return "none";
        var stacks = WoundRules.Stacks(buffer);
        return "stage=" + WoundRules.Describe(StageAt(unit, buffer)) + " stacks=" + stacks
            + " buffer=" + buffer.ToString("0.0", CultureInfo.InvariantCulture)
            + " drained=" + DrainedTotal(unit).ToString("0", CultureInfo.InvariantCulture)
            + " head=" + (drainedHead.TryGetValue(unit, out var head) ? head : 0).ToString("0", CultureInfo.InvariantCulture);
    }

    internal static double DrainedTotal(Guid unit) => drainedTotal.TryGetValue(unit, out var total) ? total : 0;

    // The line under the health bar's tooltip; null without a wound.
    internal static string StatusLine(UnitEntity unit)
    {
        if (unit == null || !AnyOpen) return null;
        var guid = CoreGame.GuidOf(unit);
        var buffer = Buffer(guid);
        var stage = StageAt(guid, buffer);
        if (stage == WoundRules.Stage.None) return null;
        var name = "<color=" + WoundText.Colour(stage) + ">" + MenuText.Pick(WoundText.Name(stage)) + "</color>";
        return string.Format(CultureInfo.InvariantCulture, MenuText.Pick(WoundText.StatusLine), name,
            Math.Ceiling(buffer).ToString("0", CultureInfo.InvariantCulture));
    }

    internal static bool IsPlayer(UnitEntity unit) => CoreGame.TryMapOfPartyMember(unit, out _);

    // A closing wound names the health it drained since it opened, all and from the head
    // ("reason=closed drained=<HP> head=<HP>").
    private static void Log(Guid unit, double buffer, string reason, string cause)
    {
        var stacks = WoundRules.Stacks(buffer);
        DelversHost.Info($"CORE_WOUND unit={unit:D} stack={stacks} stage={WoundRules.Describe(StageAt(unit, buffer))}"
            + " buffer=" + buffer.ToString("0.0", CultureInfo.InvariantCulture) + " reason=" + reason
            + (cause != null ? " by=" + cause : "")
            + (reason == "closed" ? " drained=" + DrainedTotal(unit).ToString("0", CultureInfo.InvariantCulture)
                + " head=" + (drainedHead.TryGetValue(unit, out var head) ? head : 0).ToString("0", CultureInfo.InvariantCulture) : ""));
    }
}
