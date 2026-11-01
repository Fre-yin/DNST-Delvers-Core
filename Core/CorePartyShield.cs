using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonSettlersDelvers.Core;

// (yes to the party's shield): the boss bar's honeycomb shield on the party's own health bars, in the
// portraits at the top (SubUI_UnitShortCutParts) and the status window (EntitySummaryUnitStatusUI). Neither shows a shield
// in the game (no shield image among their fields; only the bar over the head has one). Over the health,
// Core's wound band (a child of the bar) and the sickness: the band lies under the bars' common parent, right after them,
// measured on their visible outer rects, the texts it overlaps lifted above it. Share as at the boss bar, StatType.Barrier /
// MaxHealthTotal, layered above 100 %. Settings ui.shield.honeycomb (0 off), ui.shield.opacity.fill.boost.wave.
internal static class CorePartyShield
{
    private sealed class Entry
    {
        internal CoreShieldBand Body;
        internal CoreShieldBand Head;
        internal Vector3 Position;
        internal Vector2 Size;
        internal byte CellAlpha;
    }

    private static readonly Dictionary<IntPtr, Entry> entries = new();
    private static int errors;
    // Lines per place (portrait, status), so the status window is not crowded out by the portraits (portrait 4, status
    // 4; the boss bar keeps its one line per build). Counted anew per campaign.
    private static readonly Dictionary<string, int> logged = new(StringComparer.Ordinal);

    // Every new or loaded campaign (the campaign rules' start): the line counts start over; bands of the old scene are gone.
    // Each band is taken down first (Destroy), so no old band or copy stays and no original stays off.
    internal static void ResetCampaign()
    {
        logged.Clear();
        foreach (var entry in entries.Values)
        {
            try
            {
                entry.Body?.Destroy();
                entry.Head?.Destroy();
            }
            catch (Exception ex) { Failed("reset", ex); }
        }
        entries.Clear();
    }

    // The share shown instead of the real one, only in a test campaign (ShieldRules.PreviewKey).
    internal static float? Preview()
    {
        var data = CoreSaveStore.Current;
        return ShieldRules.ShieldPreview(CoreSettings.Number(ShieldRules.PreviewKey),
            data != null && CampaignPresence.IsTestCampaign(data.Campaign));
    }

    // After the game set the bars of one view: its band, made at the bars' place and made anew when they moved.
    internal static void Show(IntPtr view, Image body, Image head, UnitEntity unit, string where, Image bodyBack = null, Image headBack = null)
    {
        try
        {
            if (body == null) return;
            // The comfort option shield band (QolText) off shows no band, the same way as the honeycomb setting at 0.
            var on = BanditBossBarRules.ViewOn(CoreSettings.Number(ShieldRules.HoneycombKey)) && CoreSettings.ShieldBandShown;
            entries.TryGetValue(view, out var entry);
            var rect = body.rectTransform;
            var lines = ShieldRules.PartyShieldOpacity(CoreSettings.Number(ShieldRules.PartyShieldKey));
            var fill = ShieldRules.PartyFillOpacity(CoreSettings.Number(ShieldRules.PartyFillKey));
            var cellAlpha = ShieldRules.CellAlpha(fill, lines);
            // A changed cell alpha (fill or lines set anew) builds the band anew too.
            var moved = entry != null && (entry.Position != rect.position || entry.Size != rect.rect.size || entry.CellAlpha != cellAlpha);
            if (entry != null && (moved || !entry.Body.Alive))
            {
                entry.Body.Destroy();
                entry.Head?.Destroy();
                entries.Remove(view);
                entry = null;
            }
            if (!on && entry == null) return;
            if (entry == null)
            {
                Prune();
                // Measured on the bars' visible outer rects (their frames or backgrounds), so the honeycomb meets the outer frame flush.
                var headRect = head != null ? head.rectTransform : null;
                var stop = CoreShieldLayout.CommonParent(rect, headRect);
                var bodyOuter = CoreShieldLayout.OuterOf(rect, bodyBack != null ? bodyBack.rectTransform : null, stop);
                var headOuter = headRect != null ? CoreShieldLayout.OuterOf(headRect, headBack != null ? headBack.rectTransform : null, stop) : null;
                var (bodyBand, headBand, line) = CoreShieldBand.Over(rect, headRect, "DelversPartyShield_" + where,
                    cellAlpha, frameFollows: true, bodyOuter, headOuter);
                if (bodyBand == null) return;
                // The texts that would lie under the band come above it (CoreShieldBand.Lift).
                var raised = bodyBand.Lift() + (headBand?.Lift() ?? 0);
                line += " raised=" + raised;
                entry = new Entry { Body = bodyBand, Head = headBand, Position = rect.position, Size = rect.rect.size, CellAlpha = cellAlpha };
                entries[view] = entry;
                logged.TryGetValue(where, out var count);
                logged[where] = count + 1;
                if (count < 4)
                {
                    DelversHost.Info("CORE_PARTY_SHIELD at=" + where + " " + line + " " + MaxLine(unit));
                }
            }
            var (share, blood) = Shares(on, unit);
            entry.Body.SyncLifted();
            entry.Head?.SyncLifted();
            entry.Body.Show(share, lines, blood);
            entry.Head?.Show(share, lines, blood);
        }
        catch (Exception ex) { Failed(where, ex); }
    }

    // The other shields' share and the Blood Warden's own (BossEffectRules.BloodGuardKey), each of MaxHealthTotal. The game's
    // Barrier status adds up every shield (Status_Barrier: all of category Barrier, 801); the blood shield is counted apart, so it
    // shows as its own part (CoreShieldBand.Show) and the others as before.
    private static (float Others, float Blood) Shares(bool on, UnitEntity unit)
    {
        if (!on || unit == null || unit.IsDead) return (0f, 0f);
        if (Preview() is float preview) return (preview, 0f);
        var max = CoreGame.Stat(unit, StatType.MaxHealthTotal);
        var affecters = CoreGame.Affecters(unit);
        var blood = affecters != null && affecters.Has(BossEffectRules.BloodGuardKey) ? affecters.GetStack(BossEffectRules.BloodGuardKey) : 0;
        var others = Math.Max(0f, BossEffects.ShieldStacks(unit) - blood);
        return (ShieldRules.ShieldShare(others, max, false), ShieldRules.ShieldShare(blood, max, false));
    }

    // Three lines, then one note that further failures are not logged (they went silent).
    private static void Failed(string where, Exception ex)
    {
        errors++;
        if (errors <= 3) DelversHost.Warning("CORE_PARTY_SHIELD_FAILED where=" + where + ": " + ex.GetType().Name + ": " + ex.Message);
        else if (errors == 4) DelversHost.Warning("CORE_PARTY_SHIELD_FAILED where=" + where + ": further failures not logged");
    }

    // The share's measure next to head plus body, so a difference shows in the log.
    internal static string MaxLine(UnitEntity unit)
    {
        if (unit == null) return "maxTotal=- maxHeadBody=-";
        var total = CoreGame.Stat(unit, StatType.MaxHealthTotal);
        var parts = CoreGame.Stat(unit, StatType.MaxHealthHead) + CoreGame.Stat(unit, StatType.MaxHealthBody);
        return FormattableString.Invariant($"maxTotal={total:0.#} maxHeadBody={parts:0.#} barrier={CoreGame.Stat(unit, StatType.Barrier):0.#}");
    }

    // Views Unity destroyed (a scene change) leave their entry behind; dropped before a new one is made.
    private static void Prune()
    {
        List<IntPtr> gone = null;
        foreach (var pair in entries)
            if (!pair.Value.Body.Alive) (gone ??= new List<IntPtr>()).Add(pair.Key);
        if (gone != null) foreach (var key in gone) entries.Remove(key);
    }
}

[HarmonyPatch(typeof(SubUI_UnitShortCutParts), nameof(SubUI_UnitShortCutParts.RefreshUnitStatus))]
internal static class CorePartyShieldShortcutPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(SubUI_UnitShortCutParts __instance, UnitEntity unitEntity)
    {
        if (__instance == null) return;
        CorePartyShield.Show(__instance.Pointer, __instance._bodyHealthBar, __instance._headHealthBar, unitEntity, "portrait",
            __instance._bodyHealthBarBackground, __instance._headHealthBarBackground);
    }
}

[HarmonyPatch(typeof(EntitySummaryUIView), nameof(EntitySummaryUIView.UpdateUnitStatusUI))]
internal static class CorePartyShieldSummaryPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(EntitySummaryUIView __instance, UnitEntity unitEntity)
    {
        var status = __instance != null ? __instance._playerStatusUI : null;
        if (status == null) return;
        CorePartyShield.Show(status.Pointer, status._imgTotalBodyHealth, status._imgTotalHeadHealth, unitEntity, "status");
    }
}
