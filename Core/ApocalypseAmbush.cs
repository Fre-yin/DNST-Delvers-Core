using HarmonyLib;

namespace DungeonSettlersDelvers.Core;

// Apocalypse takes away the ambush bonus: enemies that have not noticed
// anyone (AFFECTER_Unawared) or are still surprised (AFFECTER_Surprised) normally take +30 %
// damage; on a combat Apocalypse campaign, or with the option "instant-combat" (ApocalypseRules),
// they take none. The affecters and their icon stay, only
// their DamageTakenBonus changes, through the game's own per-campaign override
// (AffecterSheet.ApplyRuntimeAdjustStatOverride); the game's restore brings 30 back.
// The value must not change while units hold the affecter, or adding and removing it would
// differ: it is set before a load rebuilds the units and before a new campaign starts, and again
// after every native restore.
internal static class ApocalypseAmbush
{
    private static readonly string[] Keys = { "AFFECTER_Unawared", "AFFECTER_Surprised" };
    private static bool? logged;

    // True while the bonus is switched off; read by the self-test.
    internal static bool BonusOff { get; private set; }

    internal static void ResetLifecycle()
    {
        BonusOff = false;
        logged = null;
    }

    internal static void Sync()
    {
        var sheet = DataSheetManager.Instance?._affecter;
        if (sheet == null) return;
        var off = ApocalypseRules.InstantCombat;
        var native = float.NaN;
        foreach (var key in Keys)
        {
            // Always write, also the native value: that clears an earlier campaign's 0.
            if (!sheet.TryGetBaseAdjustStat(key, StatType.DamageTakenBonus, out var value)) continue;
            Write(sheet, key, off ? 0f : value);
            native = value;
        }
        BonusOff = off;
        if (logged == off) return;
        logged = off;
        DelversHost.Info(FormattableString.Invariant(
            $"CORE_APOCALYPSE_AMBUSH bonus={(off ? "off" : "native")} DamageTakenBonus={(off ? 0f : native):0.#} native={native:0.#}"));
    }

    // Self-test: 0 and the native value through the same path as Sync, each read back from the
    // table. The override struct carries a string; lesson 12 of the addon guide is why both
    // loaders must prove it. Sync restores the campaign's value at the end.
    internal static void Audit()
    {
        var sheet = DataSheetManager.Instance?._affecter ?? throw new InvalidOperationException("affecter sheet missing");
        const string key = "AFFECTER_Unawared";
        if (!sheet.TryGetBaseAdjustStat(key, StatType.DamageTakenBonus, out var native) || !(native > 0f))
            throw new InvalidOperationException("no native DamageTakenBonus on " + key);
        try
        {
            Write(sheet, key, 0f);
            var zero = Read(sheet, key);
            if (zero != 0f) throw new InvalidOperationException($"override 0 reads back {zero}");
            Write(sheet, key, native);
            var back = Read(sheet, key);
            if (back != native) throw new InvalidOperationException($"override {native} reads back {back}");
        }
        finally { Sync(); }
    }

    private static void Write(AffecterSheet sheet, string key, float value)
        => sheet.ApplyRuntimeAdjustStatOverride(new AffecterStatOverride(key, StatType.DamageTakenBonus, value, false));

    private static float Read(AffecterSheet sheet, string key)
    {
        var stats = sheet._affecterTable[key]?.AdjustStats ?? throw new InvalidOperationException("no stats on " + key);
        return stats.TryGetValue(StatType.DamageTakenBonus, out var value) ? value : float.NaN;
    }
}

// Every native restore of the per-campaign overrides: ApplyRuntimeAdjustStatOverrides calls it
// first, an invalid balance snapshot calls it alone. Without parameters and not hot. Neither
// ApplyRuntimeAdjustStatOverrides (a generic list of a game struct) nor
// CampaignBalanceRuntime.ApplySnapshot (a game struct) is hooked: after the TryLoad crash Core passes no game structs
// through the Il2CppInterop trampoline it can avoid.
[HarmonyPatch(typeof(AffecterSheet), nameof(AffecterSheet.RestoreRuntimeAdjustStatOverrides))]
internal static class ApocalypseAmbushRestorePatch
{
    private static void Postfix() => ApocalypseHooks.Run("ambush-restore", ApocalypseAmbush.Sync);
}
