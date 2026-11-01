using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
#if BEPINEX
using AffecterApplier = global::Refactor.Tick.DataApply.AffecterApplier;
using StatusApplier = global::Refactor.Tick.DataApply.StatusApplier;
#else
using AffecterApplier = Il2CppRefactor.Tick.DataApply.AffecterApplier;
using StatusApplier = Il2CppRefactor.Tick.DataApply.StatusApplier;
#endif

namespace DungeonSettlersDelvers.Core;

// The game's hooks of the bleeding wound (Wounds). Every hook catches its own failures, so the
// game's apply runs on if Core fails (stage wound-*). Arguments bound by name.

// Bleeding stacks on a party member: the stack before and after the game's own apply. At the
// game's cap (99) the count no longer rises; then the stacks of the hit count as they came. The
// attacker (data.User) decides the elite chances of the roll (Wounds.Hit).
[HarmonyPatch(typeof(AffecterApplier), "Apply")]
internal static class WoundHitPatch
{
    private static void Prefix(AffecterApplyData data, out int __state)
    {
        __state = -1;
        try
        {
            if (data == null || data.Key != Wounds.BleedingKey || data.Stack <= 0 || !ApocalypseRules.Wounds) return;
            var unit = data.Target?.TryCast<UnitEntity>();
            var affecters = CoreGame.Affecters(unit);
            if (affecters != null) __state = affecters.Has(Wounds.BleedingKey) ? affecters.GetStack(Wounds.BleedingKey) : 0;
        }
        catch (Exception ex) { ApocalypseHooks.Report("wound-hit", ex); }
    }

    private static void Postfix(AffecterApplyData data, int __state)
    {
        if (__state < 0) return;
        try
        {
            var unit = data.Target?.TryCast<UnitEntity>();
            var affecters = CoreGame.Affecters(unit);
            if (affecters == null) return;
            var after = affecters.Has(Wounds.BleedingKey) ? affecters.GetStack(Wounds.BleedingKey) : 0;
            var added = WoundRules.BleedingAdded(__state, after, data.Stack);
            if (added > 0) Wounds.Hit(unit, added, data.User);
        }
        catch (Exception ex) { ApocalypseHooks.Report("wound-hit", ex); }
    }
}

// Heals on a wounded body: regeneration stops, the portal's aura and heals with a source take the
// buffer. The amount lives in the data object, so changing its field changes what the game applies.
[HarmonyPatch(typeof(StatusApplier), "Apply")]
internal static class WoundHealPatch
{
    private static void Prefix(StatusApplyData data)
    {
        if (!Wounds.AnyOpen || data == null) return;
        try
        {
            if (!(data.Amount > 0f) || (data.StatType != StatType.HealthBody && data.StatType != StatType.HealthHead)) return;
            var unit = data.Target?.TryCast<UnitEntity>();
            if (unit == null || !Wounds.IsOpen(CoreGame.GuidOf(unit))) return;
            // The game's heal execution carries no source; HealSources marked its changes.
            var execution = HealSources.Take(data);
            if (data.StatType == StatType.HealthHead)
            {
                // The game's refill of an empty head on going down: a lethal wound grows along (option C).
                Wounds.OnFallRefill(unit, data.Amount, data.From != null || execution);
                if (data.From == null && !execution && Wounds.BlocksHeadRegeneration(unit))
                {
                    Wounds.LogHeadBlocked(unit, data.Amount);
                    data._Amount_k__BackingField = 0f;
                }
                return;
            }
            // A heal of the staff's bearer eases the wound's reduction (Overgrowth, BossEffects).
            var amount = Wounds.Heal(unit, data.Amount, data.From != null, execution, BossEffects.TakeStaffHeal(data));
            if (amount != data.Amount) data._Amount_k__BackingField = amount;
        }
        catch (Exception ex) { ApocalypseHooks.Report("wound-heal", ex); }
    }
}

// The health bar's tooltip (Tooltip_Status with STATUS_HealthHead or STATUS_HealthBody) names the
// stage, the buffer, the loss per time and the weaker heals. The tooltip knows no unit; the bars
// that open it belong to the unit in the selection panel. WoundTooltip keeps the game's own text
// and writes the wound line below it, also while the tooltip stays open (every clock tick);
// any other key on the same tooltip ends that.
[HarmonyPatch(typeof(Tooltip_Status), "Show")]
internal static class WoundStatusTooltipPatch
{
    private static void Postfix(Tooltip_Status __instance, string key)
    {
        try
        {
            if (key != "STATUS_HealthBody" && key != "STATUS_HealthHead")
            {
                WoundTooltip.Forget(__instance);
                return;
            }
            var text = __instance._descText;
            if (text == null) return;
            WoundTooltip.Opened(__instance, text.text, text.gameObject.activeSelf);
            WoundTooltip.Refresh();
        }
        catch (Exception ex) { ApocalypseHooks.Report("wound-tooltip", ex); }
    }
}

// The band on the health bars: the part of the body's health that drains without treatment, at the
// right end of the current health (variant B, the game's red; lethal with a dark zigzag).
internal static class WoundBars
{
    private const string OverlayName = "DelversCore_Wound";
    private const string HudBandName = "DelversCore_WoundBand";
    private const string HudHeadBandName = "DelversCore_WoundBandHead";
    private static bool shownInScene;
    private static int errors;
    private static bool hudOrderLogged;

    internal static UnitEntity PanelUnit { get; set; }

    internal static bool Needed => Wounds.AnyOpen || shownInScene;

    internal static void ResetLifecycle()
    {
        sprites.Clear();
        shownInScene = false;
        PanelUnit = null;
        WoundTooltip.ResetLifecycle();
    }

    // The share of the bar's fill that the buffer covers, from its right end (WoundRules.BandShare):
    // on the body bar the whole buffer, on the head bar the part that reaches the head.
    internal static (float From, float To)? Band(UnitEntity unit, float fill, bool head)
    {
        if (unit == null || unit.IsDead) return null;
        var guid = CoreGame.GuidOf(unit);
        if (!Wounds.IsOpen(guid)) return null;
        return WoundRules.BandShare(head, Wounds.Stage(guid), Wounds.Buffer(guid),
            CoreGame.Stat(unit, StatType.HealthBody), CoreGame.Stat(unit, StatType.HealthHead), fill);
    }

    internal static void Show(Image bar, UnitEntity unit, bool head)
    {
        if (bar == null) return;
        var existing = bar.transform.Find(OverlayName);
        var band = Band(unit, bar.fillAmount, head);
        if (band is not { } b || b.To - b.From < 0.001f)
        {
            if (existing != null) existing.gameObject.SetActive(false);
            return;
        }
        var stage = Wounds.Stage(CoreGame.GuidOf(unit));
        var overlay = existing != null ? existing.gameObject : Create(bar.transform);
        // (one column of green right of the band, only where the bar sits at a broken pixel position on the
        // screen): the band is drawn as the game draws the fill, a filled image, from its start to the bar's right end; its fill
        // ends where the bar's fill ends, both measured on the rectangles the canvas draws (GetPixelAdjustedRect).
        var rect = overlay.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(b.From, 0f);
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var image = overlay.GetComponent<Image>();
        var parent = bar.rectTransform.rect;
        var height = Math.Max(1f, parent.height);
        image.sprite = SpriteOf(stage, parent.width * (1f - b.From), height);
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = (int)Image.OriginHorizontal.Left;
        // Both rectangles in the bar's own space: the band's is relative to its pivot, which sits at localPosition in the bar.
        var drawn = bar.GetPixelAdjustedRect();
        var own = image.GetPixelAdjustedRect();
        image.fillAmount = WoundRules.FillTo(drawn.xMin, drawn.width, own.xMin + rect.localPosition.x, own.width, b.To);
        overlay.SetActive(true);
        shownInScene = true;
    }

    // Over the head the bars are sprites clipped by their material (_ClipUvRight = 1 - share). A
    // clone of the body bar in the band colour sits behind the bar, clipped at the current health;
    // the bar itself is clipped at the health that stays. The head bar the same way, with its own band.
    internal static void ShowHud(UnitEntityHUD hud, bool head)
    {
        var front = head ? hud._headHealthBar : hud._bodyHealthBar;
        var material = head ? hud._headHealthBarMaterial : hud._bodyHealthBarMaterial;
        if (front == null || material == null) return;
        var name = head ? HudHeadBandName : HudBandName;
        var parent = front.transform.parent;
        var existing = parent != null ? parent.Find(name) : null;
        var unit = hud._entity?.TryCast<UnitEntity>();
        var share = 1f - (head ? hud._headFrontClip : hud._bodyFrontClip);
        var band = Band(unit, share, head);
        if (band is not { } b || b.To - b.From < 0.001f)
        {
            if (existing != null) existing.gameObject.SetActive(false);
            return;
        }
        var renderer = existing != null ? existing.GetComponent<SpriteRenderer>()
            : CreateHud(front, material, head ? hud._headHealthBarBackground : hud._bodyHealthBarBackground, name);
        if (renderer == null) return;
        // Over the head the band only says that the unit bleeds: one red for every stage.
        var colour = ColorUtility.TryParseHtmlString(WoundText.HudColour, out var red) ? red : Color.red;
        renderer.color = new Color(colour.r, colour.g, colour.b, front.color.a);
        renderer.sharedMaterial.SetFloat("_ClipUvRight", 1f - b.To); // trap-ok: own copy from CreateHud
        material.SetFloat("_ClipUvRight", 1f - b.From);
        renderer.gameObject.SetActive(true);
        shownInScene = true;
    }

    internal static void Report(string where, Exception ex)
    {
        if (++errors <= 3) DelversHost.Warning($"CORE_WOUND_BARS_FAILED where={where}: {ex.GetType().Name}: {ex.Message}");
    }

    private static SpriteRenderer CreateHud(SpriteRenderer front, Material material, SpriteRenderer background, string name)
    {
        var clone = UnityEngine.Object.Instantiate(front.gameObject, front.transform.parent);
        clone.name = name;
        var renderer = clone.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            UnityEngine.Object.Destroy(clone);
            return null;
        }
        renderer.sharedMaterial = new Material(material);
        // Behind the bar, above its background: one sorting step below the bar when there is room.
        var below = background != null ? background.sortingOrder : int.MinValue;
        renderer.sortingOrder = below < front.sortingOrder - 1 ? front.sortingOrder - 1 : front.sortingOrder;
        if (!hudOrderLogged)
        {
            hudOrderLogged = true;
            DelversHost.Info($"CORE_WOUND_HUD_BAND bar={front.sortingOrder} background={below} band={renderer.sortingOrder}");
        }
        return renderer;
    }

    private static Color ColourOf(WoundRules.Stage stage)
        => ColorUtility.TryParseHtmlString(WoundText.Colour(stage), out var colour) ? colour : Color.red;

    private static GameObject Create(Transform parent)
    {
        var overlay = new GameObject(OverlayName);
        overlay.transform.SetParent(parent, false);
        overlay.AddComponent<RectTransform>();
        var image = overlay.AddComponent<Image>();
        image.raycastTarget = false;
        return overlay;
    }

    // The band's sprite: one pixel of the stage's red; for lethal the zigzag at the band's own size, a 4x2 tile repeated so each
    // of its texels is half the bar's height wide and tall, as before. One per stage and width in texels.
    private static readonly Dictionary<(WoundRules.Stage, int), Sprite> sprites = new();

    private static Sprite SpriteOf(WoundRules.Stage stage, float width, float height)
    {
        var lethal = stage == WoundRules.Stage.Fatal;
        var texels = lethal ? Math.Max(4, Mathf.RoundToInt(2f * width / height)) : 1;
        if (sprites.TryGetValue((stage, texels), out var cached) && cached != null) return cached;
        var texture = Texture(stage, texels);
        var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        sprites[(stage, texels)] = sprite;
        return sprite;
    }

    // Light, deep and heavy: one colour. Lethal (moved from heavy): the sickness's 4x2 zigzag in red and dark red.
    private static Texture2D Texture(WoundRules.Stage stage, int width)
    {
        var lethal = stage == WoundRules.Stage.Fatal;
        var texture = new Texture2D(lethal ? width : 1, lethal ? 2 : 1, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        var colour = ColourOf(stage);
        if (lethal)
        {
            var dark = ColorUtility.TryParseHtmlString(WoundText.LethalDarkColour, out var d) ? d : Color.black;
            string[] rows = { "PDDP", "DDPP" };
            for (var y = 0; y < 2; y++)
                for (var x = 0; x < width; x++)
                    texture.SetPixel(x, y, rows[y][x % 4] == 'D' ? dark : colour);
        }
        else texture.SetPixel(0, 0, colour);
        texture.Apply();
        return texture;
    }
}

// After the game and after the sickness's own postfix, so the fill is final.
[HarmonyPatch(typeof(SubUI_UnitShortCutParts), nameof(SubUI_UnitShortCutParts.RefreshUnitStatus))]
internal static class WoundShortcutPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(SubUI_UnitShortCutParts __instance, UnitEntity unitEntity)
    {
        // A party member staying in a seed leaves once healed up (GroupAiSeedStay); nothing while none stays.
        if (GroupAiSeedStay.AnyOpen)
        {
            try { GroupAiSeedStay.OnHealth(unitEntity); }
            catch (Exception ex) { ApocalypseHooks.Report("seed-stay-portrait", ex); }
        }
        if (!WoundBars.Needed) return;
        try
        {
            WoundBars.Show(__instance._bodyHealthBar, unitEntity, head: false);
            WoundBars.Show(__instance._headHealthBar, unitEntity, head: true);
        }
        catch (Exception ex) { WoundBars.Report("shortcut", ex); }
    }
}

[HarmonyPatch(typeof(EntitySummaryUIView), nameof(EntitySummaryUIView.UpdateUnitStatusUI))]
internal static class WoundSummaryPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(EntitySummaryUIView __instance, UnitEntity unitEntity)
    {
        WoundBars.PanelUnit = unitEntity;
        if (!WoundBars.Needed) return;
        try
        {
            WoundBars.Show(__instance._playerStatusUI?._imgTotalBodyHealth, unitEntity, head: false);
            WoundBars.Show(__instance._playerStatusUI?._imgTotalHeadHealth, unitEntity, head: true);
        }
        catch (Exception ex) { WoundBars.Report("summary", ex); }
    }
}

// The game's death of a unit, any faction (EntityLifecycleHelper.DeadUnit, called from the DeadUnitRequested event): the
// bleeding on it ends (Wounds.OnDeath).
[HarmonyPatch(typeof(EntityLifecycleHelper), "DeadUnit")]
internal static class WoundDeathPatch
{
    private static void Postfix(MapType map, UnitEntity entity)
    {
        try { Wounds.OnDeath(map, entity, "death"); }
        catch (Exception ex) { ApocalypseHooks.Report("wound-death", ex); }
    }
}

[HarmonyPatch(typeof(UnitEntityHUD), nameof(UnitEntityHUD.RefreshStatus))]
internal static class WoundHudPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(UnitEntityHUD __instance)
    {
        if (!WoundBars.Needed) return;
        try
        {
            WoundBars.ShowHud(__instance, head: false);
            WoundBars.ShowHud(__instance, head: true);
        }
        catch (Exception ex) { WoundBars.Report("hud", ex); }
    }
}
