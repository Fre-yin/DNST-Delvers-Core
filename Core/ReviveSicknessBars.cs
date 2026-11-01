using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonSettlersDelvers.Core;

// The locked part of health and energy while a unit is capped (revival sickness or a down's
// after-effect), drawn like the author's sketch the bars show the real value against
// the full maximum, and the locked rest is purple with a dark zigzag. Taken over from the toolkit's
// SicknessDisplay (tested in test campaigns), now for every campaign. The game
// itself would draw the lowered maximum as a full bar.
// Core's share is read from its mirror stats 122 and 129 (ReviveSickness); the head bar's hook also asks whether
// the unit is at near-death (ReviveSnapshots.DeadlyKey).
// The hooks do nothing while nobody is capped and no purple part is left on screen.
internal static class ReviveSicknessBars
{
    private const string OverlayName = "DelversCore_Cursed";
    internal static readonly Color Purple = new(0.455f, 0.259f, 0.549f, 1f);
    private static readonly Color32 Dark = new(18, 10, 22, 255);
    private static Texture2D pattern;
    private static bool shownInScene;
    private static int errors;

    internal static bool Needed => ReviveSickness.AnyCapped || shownInScene;

    internal static void ResetCampaign() => shownInScene = false;

    internal static void ResetLifecycle()
    {
        shownInScene = false;
        pattern = null;
    }

    // The unlocked share of the maximum, null when Core has nothing locked.
    internal static float? Cap(UnitEntity unit)
    {
        var own = CoreGame.Stat(unit, StatType.MaxHealthTotalFinalMultiflier);
        // The total percent is read only for a unit that has a share locked (this runs in three UI postfixes per unit).
        return ReviveShareRules.HasShare(own)
            ? ReviveShareRules.LockedCap(own, CoreGame.Stat(unit, StatType.MaxHealthTotalPerModifier))
            : null;
    }

    internal static float Ratio(float value, float max) => max > 0f ? Mathf.Clamp01(value / max) : 0f;

    // cap: the unlocked share of the bar; null hides the overlay.
    internal static void Show(Image bar, float? cap)
    {
        if (bar == null) return;
        var existing = bar.transform.Find(OverlayName);
        if (cap is not { } c || ReviveShareRules.IsLifted(c))
        {
            if (existing != null) existing.gameObject.SetActive(false);
            return;
        }
        var overlay = existing != null ? existing.gameObject : Create(bar.transform);
        var rect = overlay.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(Mathf.Clamp01(c), 0f);
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var parent = bar.rectTransform.rect;
        var width = parent.width * (1f - Mathf.Clamp01(c));
        var height = Math.Max(1f, parent.height);
        overlay.GetComponent<RawImage>().uvRect = new Rect(0f, 0f, width / (2f * height), 1f);
        overlay.SetActive(true);
        shownInScene = true;
    }

    internal static void MarkShown() => shownInScene = true;

    internal static void Report(string where, Exception ex)
    {
        if (++errors <= 3) DelversHost.Warning($"CORE_SICKNESS_BARS_FAILED where={where}: {ex.GetType().Name}: {ex.Message}");
    }

    private static GameObject Create(Transform parent)
    {
        var overlay = new GameObject(OverlayName);
        overlay.transform.SetParent(parent, false);
        overlay.AddComponent<RectTransform>();
        var image = overlay.AddComponent<RawImage>();
        image.texture = Pattern();
        image.raycastTarget = false;
        return overlay;
    }

    // A 4x2 zigzag, repeated along the locked part; square texels at half the bar's height.
    private static Texture2D Pattern()
    {
        if (pattern != null) return pattern;
        pattern = new Texture2D(4, 2, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat,
            hideFlags = HideFlags.HideAndDontSave
        };
        // Bottom row, then top row: the dark steps climb and fall like the sketch's zigzag.
        string[] rows = { "PDDP", "DDPP" };
        for (var y = 0; y < 2; y++)
            for (var x = 0; x < 4; x++)
                pattern.SetPixel(x, y, rows[y][x] == 'D' ? (Color)Dark : Purple);
        pattern.Apply();
        return pattern;
    }
}

// Party bar at the top: fills against the full maximum plus the purple locked part.
[HarmonyPatch(typeof(SubUI_UnitShortCutParts), nameof(SubUI_UnitShortCutParts.RefreshUnitStatus))]
internal static class ReviveSicknessShortcutPatch
{
    private static void Postfix(SubUI_UnitShortCutParts __instance, UnitEntity unitEntity)
    {
        if (!ReviveSicknessBars.Needed) return;
        try
        {
            var cap = unitEntity is { IsDead: false } ? ReviveSicknessBars.Cap(unitEntity) : null;
            if (cap != null)
            {
                __instance._headHealthBar.fillAmount = ReviveSicknessBars.Ratio(CoreGame.Stat(unitEntity, StatType.HealthHead), ReviveSickness.FullMaxHealth(unitEntity, StatType.MaxHealthHead));
                __instance._bodyHealthBar.fillAmount = ReviveSicknessBars.Ratio(CoreGame.Stat(unitEntity, StatType.HealthBody), ReviveSickness.FullMaxHealth(unitEntity, StatType.MaxHealthBody));
                __instance._energyBar.fillAmount = ReviveSicknessBars.Ratio(CoreGame.Stat(unitEntity, StatType.Energy), ReviveSickness.FullMaxEnergy(unitEntity));
            }
            ReviveSicknessBars.Show(__instance._headHealthBar, cap);
            ReviveSicknessBars.Show(__instance._bodyHealthBar, cap);
            ReviveSicknessBars.Show(__instance._energyBar, cap);
        }
        catch (Exception ex) { ReviveSicknessBars.Report("shortcut", ex); }
    }
}

// Selection panel: numbers and fills against the full maximum (13 / 134), purple locked part.
[HarmonyPatch(typeof(EntitySummaryUIView), nameof(EntitySummaryUIView.UpdateUnitStatusUI))]
internal static class ReviveSicknessSummaryPatch
{
    private static void Postfix(EntitySummaryUIView __instance, UnitEntity unitEntity)
    {
        if (!ReviveSicknessBars.Needed) return;
        try
        {
            var status = __instance._playerStatusUI;
            if (status == null) return;
            var cap = unitEntity is { IsDead: false } ? ReviveSicknessBars.Cap(unitEntity) : null;
            if (cap != null)
            {
                status.SetHeadHealthUI(CoreGame.Stat(unitEntity, StatType.HealthHead), ReviveSickness.FullMaxHealth(unitEntity, StatType.MaxHealthHead));
                status.SetBodyHealthUI(CoreGame.Stat(unitEntity, StatType.HealthBody), ReviveSickness.FullMaxHealth(unitEntity, StatType.MaxHealthBody));
                status.SetEnergyUI(CoreGame.Stat(unitEntity, StatType.Energy), ReviveSickness.FullMaxEnergy(unitEntity));
            }
            ReviveSicknessBars.Show(status._imgTotalHeadHealth, cap);
            ReviveSicknessBars.Show(status._imgTotalBodyHealth, cap);
            ReviveSicknessBars.Show(status._imgEnergy, cap);
        }
        catch (Exception ex) { ReviveSicknessBars.Report("summary", ex); }
    }
}

// Bars over the head: filled against the full maximum and tinted purple while capped.
[HarmonyPatch(typeof(UnitEntityHUD), nameof(UnitEntityHUD.RefreshStatus))]
internal static class ReviveSicknessHudPatch
{
    private static readonly Dictionary<IntPtr, Color> original = new();

    internal static void ResetScene() => original.Clear();

    private static void Postfix(UnitEntityHUD __instance)
    {
        if (!ReviveSicknessBars.Needed) return;
        try
        {
            var unit = __instance._entity?.TryCast<UnitEntity>();
            var cap = unit is { IsDead: false } && CoreGame.Affecters(unit)?.Has(ReviveSnapshots.DeadlyKey) != true
                ? ReviveSicknessBars.Cap(unit) : null;
            if (cap != null)
            {
                __instance._headFrontClip = __instance.SetBar(__instance._headHealthBarMaterial, CoreGame.Stat(unit, StatType.HealthHead), ReviveSickness.FullMaxHealth(unit, StatType.MaxHealthHead));
                __instance._bodyFrontClip = __instance.SetBar(__instance._bodyHealthBarMaterial, CoreGame.Stat(unit, StatType.HealthBody), ReviveSickness.FullMaxHealth(unit, StatType.MaxHealthBody));
            }
            Tint(__instance._headHealthBar, cap != null);
            Tint(__instance._bodyHealthBar, cap != null);
        }
        catch (Exception ex) { ReviveSicknessBars.Report("hud", ex); }
    }

    private static void Tint(SpriteRenderer renderer, bool capped)
    {
        if (renderer == null) return;
        var key = renderer.Pointer;
        if (capped)
        {
            ReviveSicknessBars.MarkShown();
            if (!original.ContainsKey(key)) original[key] = renderer.color;
            renderer.color = new Color(ReviveSicknessBars.Purple.r, ReviveSicknessBars.Purple.g, ReviveSicknessBars.Purple.b, renderer.color.a);
        }
        else if (original.TryGetValue(key, out var color))
        {
            renderer.color = color;
            original.Remove(key);
        }
    }
}
