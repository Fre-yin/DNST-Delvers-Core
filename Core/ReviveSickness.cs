using UnityEngine;

namespace DungeonSettlersDelvers.Core;

// What a revive and a down leave behind; after the toolkit's RevivalSickness,
// tested there (artifacts\recherche-krankheit-20261005).
// - Revive (full death): Core's effect "Wiederbelebungskrankheit" carries the duration
//   (ReviveRules.SicknessWeeks). Health and energy are capped at the cap, 10 % rising evenly to
//   100 %. In the dungeon the unit panics (Core's "Dungeonpanik", mood -25), removed on the base.
// - Down (rescued from near-death): the game's own after-effect (AFFECTER_DeadlyAfterEffect, mood
//   -5, the next down is deadly) carries it; health and energy start at 65 % and rise to full while
//   it runs. No panic, and a down does not count against the revive limit.
// The cap uses the game's own percent modifiers (stats 80 and 88, the channel the trait Prodigy
// uses); the game then caps every heal, regeneration and load itself. Core mirrors its share in
// stats 122 and 129, which the game reads at no fixed place (reads with a variable stat are not checked). A save does not keep the share; it is rebuilt after
// loading. The sickness's remaining length lives in Core's data (CoreSaveData), so it comes back
// after a load, also after the campaign was played without Core for a while.
internal static class ReviveSickness
{
    internal const string DownCarrierKey = "AFFECTER_DeadlyAfterEffect";
    private const float PanicDuration = 120f;
    private const float PanicRefreshBelow = 60f;
    private const float ShareStep = 0.5f;

    private static readonly HashSet<Guid> fresh = new();
    private static readonly HashSet<Guid> known = new();
    private static readonly HashSet<Guid> sickLastTick = new();
    private static int errors;
    private static float notBefore;
    // The cap goes to the log when its tenth changes or Cores own share moves by this much.
    private const float ReportOwnStep = 10f;
    private static readonly Dictionary<Guid, (int Step, float Own)> capSteps = new();

    internal static void ResetCampaign()
    {
        fresh.Clear();
        known.Clear();
        sickLastTick.Clear();
        capSteps.Clear();
        errors = 0;
        AnyCapped = false;
    }

    internal static double DurationOf(double days) => days * ReviveRules.DurationUnitsPerDay;

    // ReviveService.AfterRevive: the unit stands again. Only the carrier is given here; the shares
    // follow in the next tick, when the restored traits count in the percent values.
    internal static void Begin(UnitEntity unit)
    {
        var guid = CoreGame.GuidOf(unit);
        var data = CoreSaveStore.Current;
        if (data == null || guid == Guid.Empty) return;
        var revive = data.RevivesUsed(guid) + 1;   // counted once the revive has been read back
        var days = ReviveRules.SicknessDays(CoreGame.CombatLevel(), revive);
        var duration = (float)DurationOf(days);
        ReviveEffects.Ensure();
        CoreGame.Apply(MapOf(unit), new AffecterApplyData(unit.Cast<IEntity>(), ReviveEffects.SicknessKey, 1, duration, false, null).Cast<IApplyData>());
        data.SetSickness(guid, duration, duration);
        fresh.Add(guid);
        DelversHost.Info($"CORE_SICKNESS_BEGIN unit={guid:D} revive={revive} days={days.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
    }

    // After a load: units whose sickness Core knows but whose effect is missing (Core keeps it out
    // of the game's save) get it back with the remaining length.
    internal static void Restore()
    {
        // The game restores health and energy a moment after the map is ready; the caps wait.
        notBefore = Time.unscaledTime + 3f;
        var data = CoreSaveStore.Current;
        var maps = CoreGame.Flow?._inMapDatas;
        if (data == null || maps == null) return;
        ReviveEffects.Ensure();
        var restored = 0;
        foreach (var pair in maps)
        {
            var units = pair.Value?.GetAllPlayerUnits(false);
            for (var i = 0; units != null && i < units.Count; i++)
            {
                var unit = units[i];
                if (unit == null || unit.IsDead) continue;
                var guid = CoreGame.GuidOf(unit);
                if (!data.TryGetSickness(guid, out var remaining, out _)) continue;
                var affecters = CoreGame.Affecters(unit);
                if (affecters == null || affecters.Has(ReviveEffects.SicknessKey)) continue;
                CoreGame.Apply(pair.Key, new AffecterApplyData(unit.Cast<IEntity>(), ReviveEffects.SicknessKey, 1, (float)remaining, false, null).Cast<IApplyData>());
                restored++;
            }
        }
        if (restored > 0) DelversHost.Info("CORE_SICKNESS_RESTORED units=" + restored);
    }

    // Before every save: the remaining length of each running sickness into Core's data.
    internal static void SyncToSave()
    {
        var data = CoreSaveStore.Current;
        var maps = CoreGame.Flow?._inMapDatas;
        if (data == null || maps == null) return;
        foreach (var pair in maps)
        {
            var units = pair.Value?.GetAllPlayerUnits(false);
            for (var i = 0; units != null && i < units.Count; i++)
            {
                var unit = units[i];
                if (unit == null || unit.IsDead) continue;
                var affecters = CoreGame.Affecters(unit);
                if (affecters == null || !affecters.Has(ReviveEffects.SicknessKey)) continue;
                var guid = CoreGame.GuidOf(unit);
                var total = data.TryGetSickness(guid, out _, out var stored) ? stored : affecters.GetInitialDuration(ReviveEffects.SicknessKey);
                data.SetSickness(guid, affecters.GetDuration(ReviveEffects.SicknessKey), total);
            }
        }
    }

    // The cap for a living unit: 1 when neither sick nor after a down.
    internal static float Cap(UnitEntity unit) => Cap(CoreGame.GuidOf(unit), CoreGame.Affecters(unit));

    private static float Cap(Guid guid, AffecterComponent affecters)
    {
        if (affecters == null) return 1f;
        var cap = 1.0;
        if (affecters.Has(ReviveEffects.SicknessKey))
        {
            var left = affecters.GetDuration(ReviveEffects.SicknessKey);
            var total = CoreSaveStore.Current != null && CoreSaveStore.Current.TryGetSickness(guid, out _, out var stored)
                ? stored : affecters.GetInitialDuration(ReviveEffects.SicknessKey);
            cap = Math.Min(cap, ReviveRules.SicknessCap(left, total));
        }
        if (affecters.Has(DownCarrierKey))
            cap = Math.Min(cap, ReviveRules.DownCap(affecters.GetDuration(DownCarrierKey), affecters.GetInitialDuration(DownCarrierKey)));
        return (float)cap;
    }

    internal static double DaysLeft(Guid guid)
    {
        var unit = CoreGame.FindUnit(guid);
        var affecters = unit != null && !unit.IsDead ? CoreGame.Affecters(unit) : null;
        if (affecters != null && affecters.Has(ReviveEffects.SicknessKey))
            return affecters.GetDuration(ReviveEffects.SicknessKey) / ReviveRules.DurationUnitsPerDay;
        return CoreSaveStore.Current != null && CoreSaveStore.Current.TryGetSickness(guid, out var remaining, out _)
            ? remaining / ReviveRules.DurationUnitsPerDay : 0.0;
    }

    internal static double DaysTotal(Guid guid)
        => CoreSaveStore.Current != null && CoreSaveStore.Current.TryGetSickness(guid, out _, out var total)
            ? total / ReviveRules.DurationUnitsPerDay : 0.0;

    // Test hook: the sickness now lasts this many more days (0 ends it); the total stays.
    internal static bool SetDaysForTest(Guid guid, double days)
    {
        var unit = CoreGame.FindUnit(guid);
        var data = CoreSaveStore.Current;
        if (unit == null || unit.IsDead || data == null) return false;
        var map = MapOf(unit);
        var entity = unit.Cast<IEntity>();
        var affecters = CoreGame.Affecters(unit);
        var total = data.TryGetSickness(guid, out _, out var stored) ? stored : DurationOf(Math.Max(days, 0.0));
        // Re-applying only lengthens a running effect, so it is removed first.
        if (affecters != null && affecters.Has(ReviveEffects.SicknessKey))
            CoreGame.Apply(map, new AffecterApplyData(entity, ReviveEffects.SicknessKey, -1, 0f, false, null).Cast<IApplyData>());
        if (days <= 0)
        {
            data.ClearSickness(guid);
            return true;
        }
        var duration = DurationOf(days);
        CoreGame.Apply(map, new AffecterApplyData(entity, ReviveEffects.SicknessKey, 1, (float)duration, false, null).Cast<IApplyData>());
        data.SetSickness(guid, duration, Math.Max(total, duration));
        return true;
    }

    // Once a second while a campaign runs.
    internal static void Tick()
    {
        if (Time.unscaledTime < notBefore) return;
        var maps = CoreGame.Flow?._inMapDatas;
        if (maps == null) return;
        var capped = false;
        foreach (var pair in maps)
        {
            var units = pair.Value?.GetAllPlayerUnits(false);
            for (var i = 0; units != null && i < units.Count; i++)
            {
                var unit = units[i];
                if (unit == null || unit.IsDead) continue;
                try { capped |= Process(unit, pair.Key); }
                catch (Exception ex)
                {
                    if (++errors <= 3) DelversHost.Warning($"CORE_SICKNESS_FAILED unit={unit.Guid}: {ex.Message}");
                }
            }
        }
        AnyCapped = capped;
    }

    // Someone's health or energy is capped by Core; the bars (ReviveSicknessBars) only work then.
    internal static bool AnyCapped { get; private set; }

    // True while the unit stays capped.
    private static bool Process(UnitEntity unit, MapType map)
    {
        var guid = CoreGame.GuidOf(unit);
        var affecters = CoreGame.Affecters(unit);
        if (affecters == null) return false;
        var sick = affecters.Has(ReviveEffects.SicknessKey);

        if (sick) sickLastTick.Add(guid);
        else if (sickLastTick.Remove(guid))
        {
            // The sickness ran out: Core forgets it, the revive count stays.
            CoreSaveStore.Current?.ClearSickness(guid);
            DelversHost.Info("CORE_SICKNESS_END unit=" + guid.ToString("D"));
        }
        // Healthy, never capped by Core this session and no panic left: nothing to read.
        if (!sick && !known.Contains(guid) && !affecters.Has(DownCarrierKey) && !affecters.Has(ReviveEffects.PanicKey)) return false;

        var ownHealth = CoreGame.Stat(unit, StatType.MaxHealthTotalFinalMultiflier);
        var ownEnergy = CoreGame.Stat(unit, StatType.EnergyRegenerationFinalMultiflier);
        var c = Cap(guid, affecters);

        if (ReviveShareRules.IsLifted(c))
        {
            if (ReviveShareRules.HasShare(ownHealth) || ReviveShareRules.HasShare(ownEnergy))
            {
                // Over: the full maximum comes back, and the health rises with it.
                var share = ShareCap(unit, StatType.MaxHealthTotalPerModifier, ownHealth);
                if (share < 1f) RaiseHealth(unit, map, 1f - share);
                SetShare(unit, map, StatType.MaxHealthTotalPerModifier, StatType.MaxHealthTotalFinalMultiflier, -ownHealth);
                SetShare(unit, map, StatType.MaxEnergyPerModifier, StatType.EnergyRegenerationFinalMultiflier, -ownEnergy);
            }
            Panic(unit, map, affecters, false);
            known.Remove(guid);
            return false;
        }

        // A capped unit without a share that was not just revived comes from a save: the game
        // restored health and energy as a share of the full maximum, so scale them once.
        var loaded = Math.Abs(ownHealth) < ReviveShareRules.MinShare && !fresh.Contains(guid) && !known.Contains(guid);
        if (loaded)
        {
            Add(unit, map, StatType.HealthHead, CoreGame.Stat(unit, StatType.HealthHead) * (c - 1f));
            Add(unit, map, StatType.HealthBody, CoreGame.Stat(unit, StatType.HealthBody) * (c - 1f));
            Add(unit, map, StatType.Energy, CoreGame.Stat(unit, StatType.Energy) * (c - 1f));
        }

        var foreignHealth = ReviveShareRules.Foreign(CoreGame.Stat(unit, StatType.MaxHealthTotalPerModifier), ownHealth);
        var targetHealth = ReviveShareRules.ShareForCap(c, foreignHealth);
        if (Math.Abs(targetHealth - ownHealth) >= ShareStep)
        {
            var before = ShareCap(unit, StatType.MaxHealthTotalPerModifier, ownHealth);
            SetShare(unit, map, StatType.MaxHealthTotalPerModifier, StatType.MaxHealthTotalFinalMultiflier, targetHealth - ownHealth);
            // Health follows the cap; the game's own clamp stops it at the new maximum.
            if (!loaded && !fresh.Contains(guid) && c > before) RaiseHealth(unit, map, c - before);
        }
        var foreignEnergy = ReviveShareRules.Foreign(CoreGame.Stat(unit, StatType.MaxEnergyPerModifier), ownEnergy);
        var targetEnergy = ReviveShareRules.ShareForCap(c, foreignEnergy);
        if (Math.Abs(targetEnergy - ownEnergy) >= ShareStep)
            SetShare(unit, map, StatType.MaxEnergyPerModifier, StatType.EnergyRegenerationFinalMultiflier, targetEnergy - ownEnergy);

        if (fresh.Remove(guid))
        {
            // Just revived: energy starts full at the cap.
            Add(unit, map, StatType.Energy, c * NormalMax(unit, StatType.MaxEnergy, StatType.MaxEnergyPerModifier, StatType.EnergyRegenerationFinalMultiflier)
                - CoreGame.Stat(unit, StatType.Energy));
        }
        else
        {
            // The game clamps only on a change; a value restored before the lowered maximum took
            // effect (loading) would stay above it.
            Clamp(unit, map, StatType.HealthHead, StatType.MaxHealthHead);
            Clamp(unit, map, StatType.HealthBody, StatType.MaxHealthBody);
            Clamp(unit, map, StatType.Energy, StatType.MaxEnergy);
        }
        ReportCap(unit, guid, c, ownHealth, foreignHealth);
        known.Add(guid);
        // Panic only after a full death, and only in the dungeon.
        Panic(unit, map, affecters, sick && map == MapType.Dungeon);
        return true;
    }

    // One log line per step of the cap, with the maxima the player sees and the full ones, so a
    // report of a wrong cap can be read from the log.
    private static void ReportCap(UnitEntity unit, Guid guid, float cap, float ownHealth, float foreignHealth)
    {
        var step = (int)Math.Floor(cap * 10f);
        if (capSteps.TryGetValue(guid, out var last) && last.Step == step && Math.Abs(last.Own - ownHealth) < ReportOwnStep) return;
        capSteps[guid] = (step, ownHealth);
        DelversHost.Info(string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "CORE_SICKNESS_CAP unit={0:D} cap={1:0.000} maxHead={2:0.00} maxBody={3:0.00} fullHead={4:0.00} fullBody={5:0.00} own={6:0.00} foreign={7:0.00}",
            guid, cap, CoreGame.Stat(unit, StatType.MaxHealthHead), CoreGame.Stat(unit, StatType.MaxHealthBody),
            FullMaxHealth(unit, StatType.MaxHealthHead), FullMaxHealth(unit, StatType.MaxHealthBody), ownHealth, foreignHealth));
    }

    private static void Panic(UnitEntity unit, MapType map, AffecterComponent affecters, bool wanted)
    {
        var entity = unit.Cast<IEntity>();
        var has = affecters.Has(ReviveEffects.PanicKey);
        if (!wanted)
        {
            if (has) CoreGame.Apply(map, new AffecterApplyData(entity, ReviveEffects.PanicKey, -Math.Max(1, affecters.GetStack(ReviveEffects.PanicKey)), 0f, false, null).Cast<IApplyData>());
            return;
        }
        if (!has) CoreGame.Apply(map, new AffecterApplyData(entity, ReviveEffects.PanicKey, 1, PanicDuration, false, null).Cast<IApplyData>());
        else if (affecters.GetDuration(ReviveEffects.PanicKey) < PanicRefreshBelow)
            CoreGame.Apply(map, new AffecterApplyData(entity, ReviveEffects.PanicKey, 0, PanicDuration, false, null).Cast<IApplyData>());
    }

    // A unit's full health maximum without Core's share; a corpse can still carry the share of a
    // down (revive started at 6.5 % instead of 10 %).
    internal static float FullMaxHealth(UnitEntity unit, StatType max)
        => NormalMax(unit, max, StatType.MaxHealthTotalPerModifier, StatType.MaxHealthTotalFinalMultiflier);

    internal static float FullMaxEnergy(UnitEntity unit)
        => NormalMax(unit, StatType.MaxEnergy, StatType.MaxEnergyPerModifier, StatType.EnergyRegenerationFinalMultiflier);

    // Full maximum without Core's share (ReviveShareRules.FullMax).
    private static float NormalMax(UnitEntity unit, StatType max, StatType percent, StatType marker)
        => ReviveShareRules.FullMax(CoreGame.Stat(unit, max), CoreGame.Stat(unit, marker), CoreGame.Stat(unit, percent));

    // The cap the current share stands for (ReviveShareRules.CapOfShare).
    private static float ShareCap(UnitEntity unit, StatType percent, float own)
        => ReviveShareRules.CapOfShare(own, ReviveShareRules.Foreign(CoreGame.Stat(unit, percent), own));

    private static void RaiseHealth(UnitEntity unit, MapType map, float fraction)
    {
        Add(unit, map, StatType.HealthHead, fraction * NormalMax(unit, StatType.MaxHealthHead, StatType.MaxHealthTotalPerModifier, StatType.MaxHealthTotalFinalMultiflier));
        Add(unit, map, StatType.HealthBody, fraction * NormalMax(unit, StatType.MaxHealthBody, StatType.MaxHealthTotalPerModifier, StatType.MaxHealthTotalFinalMultiflier));
    }

    private static void SetShare(UnitEntity unit, MapType map, StatType percent, StatType marker, float delta)
    {
        if (Math.Abs(delta) < 0.001f) return;
        Add(unit, map, percent, delta);
        Add(unit, map, marker, delta);
    }

    private static void Clamp(UnitEntity unit, MapType map, StatType value, StatType max)
    {
        var over = CoreGame.Stat(unit, value) - CoreGame.Stat(unit, max);
        if (over > 0.5f) Add(unit, map, value, -over);
    }

    private static void Add(UnitEntity unit, MapType map, StatType stat, float delta)
    {
        if (Math.Abs(delta) < 0.0001f) return;
        CoreGame.Apply(map, new StatusApplyData(unit.Cast<IEntity>(), stat, delta, StatusApplyPersistence.Normal).Cast<IApplyData>());
    }

    private static MapType MapOf(UnitEntity unit)
    {
        var maps = CoreGame.Flow?._inMapDatas;
        var guid = CoreGame.GuidOf(unit);
        if (maps != null)
            foreach (var pair in maps)
            {
                var units = pair.Value?.GetAllPlayerUnits(true);
                for (var i = 0; units != null && i < units.Count; i++)
                    if (units[i] != null && CoreGame.GuidOf(units[i]) == guid) return pair.Key;
            }
        return CoreGame.CurrentMap;
    }
}
