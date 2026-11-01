namespace DungeonSettlersDelvers.Core;

// The permanent traits a death removes (race, background, personality, inscription, physical,
// soul; the game takes them with -999 stacks), kept per party member in Core's save data so a
// revive can give them back. Taken:
// - after every load, and before every save the game writes (autosaves run at every map change,
//   so every dungeon floor gets one);
// - when near-death (AFFECTER_Deadly) first shows on a unit: the traits are still complete then.
// A list taken while a unit is already in near-death never replaces one that exists
// (ReviveRules.ShouldReplaceSnapshot); dead units are never read. After the toolkit's UnitRevive.
internal static class ReviveSnapshots
{
    internal const string DeadlyKey = "AFFECTER_Deadly";

    private static readonly Dictionary<string, bool> permanent = new(StringComparer.Ordinal);
    private static readonly HashSet<Guid> nearDeath = new();

    internal static void ResetLifecycle()
    {
        permanent.Clear();
        nearDeath.Clear();
    }

    internal static void ResetCampaign() => nearDeath.Clear();

    // Every living party member on every loaded map; returns how many snapshots changed.
    internal static int TakeAll(string reason)
    {
        var data = CoreSaveStore.Current;
        var maps = CoreGame.Flow?._inMapDatas;
        if (data == null || maps == null) return 0;
        int seen = 0, changed = 0;
        foreach (var pair in maps)
        {
            var units = pair.Value?.GetAllPlayerUnits(false);
            for (var i = 0; units != null && i < units.Count; i++)
            {
                var unit = units[i];
                if (unit == null || unit.IsDead) continue;
                seen++;
                if (Take(data, unit, firstNearDeath: false)) changed++;
            }
        }
        DelversHost.Info($"CORE_REVIVE_SNAPSHOTS reason={reason} units={seen} changed={changed}");
        return changed;
    }

    // Once a second while a campaign runs: a unit that has just come into near-death gets its
    // snapshot now, before its death takes the traits.
    internal static void CheckNearDeath()
    {
        var data = CoreSaveStore.Current;
        var maps = CoreGame.Flow?._inMapDatas;
        if (data == null || maps == null) return;
        var still = new HashSet<Guid>();
        foreach (var pair in maps)
        {
            var units = pair.Value?.GetAllPlayerUnits(false);
            for (var i = 0; units != null && i < units.Count; i++)
            {
                var unit = units[i];
                if (unit == null || unit.IsDead) continue;
                var affecters = CoreGame.Affecters(unit);
                if (affecters == null || !affecters.Has(DeadlyKey)) continue;
                var guid = CoreGame.GuidOf(unit);
                still.Add(guid);
                if (nearDeath.Contains(guid)) continue;
                Take(data, unit, firstNearDeath: true);
                DelversHost.Info("CORE_REVIVE_SNAPSHOT_NEAR_DEATH unit=" + guid.ToString("D"));
            }
        }
        nearDeath.Clear();
        nearDeath.UnionWith(still);
    }

    private static bool Take(CoreSaveData data, UnitEntity unit, bool firstNearDeath)
    {
        var guid = CoreGame.GuidOf(unit);
        var affecters = CoreGame.Affecters(unit);
        var keys = affecters?.GetAll();
        if (guid == Guid.Empty || keys == null) return false;
        var list = new List<TraitStack>();
        for (var k = 0; k < keys.Count; k++)
        {
            var key = keys[k];
            if (string.IsNullOrWhiteSpace(key) || !IsPermanent(key)) continue;
            list.Add(new TraitStack(key, Math.Max(1, affecters.GetStack(key))));
        }
        list.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
        var old = data.Snapshot(guid);
        var dying = !firstNearDeath && affecters.Has(DeadlyKey);
        if (!ReviveRules.ShouldReplaceSnapshot(old.Count > 0, list.Count, dying)) return false;
        if (old.Count == list.Count && old.SequenceEqual(list)) return false;
        data.SetSnapshot(guid, list);
        return true;
    }

    // The native trait check: traits without a duration are permanent.
    private static bool IsPermanent(string key)
    {
        if (permanent.TryGetValue(key, out var known)) return known;
        bool result;
        try
        {
            var definition = Affecter.Create(key);
            result = definition != null && definition.IsTrait && !definition.HasDuration;
        }
        catch (Exception) { result = false; }
        permanent[key] = result;
        return result;
    }
}
