using System.Collections;
using System.Globalization;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Volume = UnityEngine.Rendering.Volume;

namespace DungeonSettlersDelvers.Core;

// Draws Apocalypse's slow motion (rules: ApocalypseSlowRules; sketch B with the
// rim of B2) on the game's clock panel (SubUI_CampaignClockUI) and through a copy of the game's vignette.
// - Only while the no-pause rule applies (ApocalypseTime.Applies) and apocalypse.slow.bar is on;
//   otherwise nothing is built, and what was built is hidden.
// - Built from the panel's own parts: the bar is two flat rects in the HUD's colours under the speed
//   buttons, the glow is the game's selected button sprite, the rim a copy of the game's vignette volume
//   with a copy of its profile. Nothing native is moved, scaled or replaced; sprites and colours it
//   touches are given back.
// - Its routine runs in real time while something moves and ends once all is calm, so a full charge
//   costs nothing per frame. The bar and rim settings are read once per campaign; the no pause rule is read at every step
//   (ApocalypseTime.Applies through ApocalypseRules.NoPause), so its menu switch works at once.
// - The game's SetSpeedChangeOnUI plays a click: Core never calls it and keeps the sprites itself.
// ApocalypseSlowProbe shows where the parts sit (SlowUiProbeForTest).
internal static class ApocalypseSlowDisplay
{
    private const string BarName = "DelversCore_SlowBar";
    private const string GlowName = "DelversCore_SlowGlow";
    private const string FrameName = "DelversCore_SlowFrame";
    private const int ShimmerTexels = 32;

    private static SubUI_CampaignClockUI clock;
    private static RectTransform bar;
    private static RectTransform fill;
    private static RectTransform shimmerRect;
    private static Image backImage;
    private static Image fillImage;
    private static RawImage shimmer;
    private static Texture2D shimmerTexture;
    private static float barX;
    private static float barY;
    private static float unitsPerRef = 1f;

    private static Image glow;
    private static readonly Image[] frameEdges = new Image[4];
    private static bool glowOn;
    private static readonly Sprite[] keptSprites = new Sprite[4];

    private static bool tintKept;
    private static Color timeColour;
    private static Color iconColour;

    private static Volume rim;
    private static bool rimTried;

    private static bool? barSetting;
    private static double? rimSetting;

    private static bool routineRunning;
    private static float sinceDenied = -1f;
    private static float sinceFull = -1f;
    private static float run;
    private static float fade;
    private static bool wasFull = true;
    private static bool wasSlow;
    private static ApocalypseSlowRules.Mode mode = ApocalypseSlowRules.Mode.Full;
    private static int errors;

    internal static string ModeText => mode.ToString().ToLowerInvariant();

    private static bool Showing => ApocalypseTime.Applies && (barSetting ??= CoreSettings.SlowBarEnabled);

    private static double RimStrength => rimSetting ??= CoreSettings.SlowRimStrength;

    internal static void ResetLifecycle()
    {
        try { Hide(); }
        catch (Exception ex) { Report("lifecycle", ex); }
        clock = null;
        Forget();
        ResetState();
        shimmerTexture = null;
        rim = null;
        rimTried = false;
        errors = 0;
    }

    // A campaign was loaded or created (ApocalypseTime.ResetCampaign): calm state, the game's looks
    // back, the settings read anew.
    internal static void ResetCampaign()
    {
        try
        {
            Hide();
            ResetState();
            Refresh();
        }
        catch (Exception ex) { Report("reset", ex); }
    }

    private static void ResetState()
    {
        sinceDenied = sinceFull = -1f;
        run = fade = 0f;
        wasFull = true;
        wasSlow = false;
        mode = ApocalypseSlowRules.Mode.Full;
        barSetting = null;
        rimSetting = null;
    }

    // Postfix of SubUI_CampaignClockUI.Initialize: the panel of this HUD.
    internal static void OnClock(SubUI_CampaignClockUI ui)
    {
        if (ui == null) return;
        if (clock == null || clock.Pointer != ui.Pointer)
        {
            clock = ui;
            Forget();
        }
        Refresh();
    }

    // Postfix of SubUI_CampaignClockUI.SetSpeedChangeOnUI: the game set its sprites again; while slow
    // motion runs, they are kept as the ones to give back and Core's look returns.
    internal static void OnSpeedUi(SubUI_CampaignClockUI ui)
    {
        if (!glowOn || ui == null || clock == null || ui.Pointer != clock.Pointer) return;
        glowOn = false;
        SetGlow(true, run);
    }

    // The player pressed with too little charge (ApocalypseTime).
    internal static void Denied()
    {
        sinceDenied = 0f;
        Wake();
    }

    // Slow motion started or ended, or the charge changed outside the routine (ApocalypseTime).
    internal static void Wake()
    {
        if (routineRunning || !Showing) return;
        routineRunning = true;
        try { DelversHost.StartCoroutine(Routine()); }
        catch (Exception ex)
        {
            routineRunning = false;
            Report("start", ex);
        }
    }

    // Draws the current state once and starts the routine if anything moves; hides all when the rule
    // or the setting is off.
    private static void Refresh()
    {
        if (!Showing)
        {
            Hide();
            return;
        }
        Draw(0f);
        if (!ApocalypseSlowRules.Calm(mode, fade)) Wake();
    }

    private static IEnumerator Routine()
    {
        try
        {
            while (true)
            {
                yield return null;
                if (!Step(Time.unscaledDeltaTime)) yield break;
            }
        }
        finally { routineRunning = false; }
    }

    private static bool Step(float seconds)
    {
        try
        {
            if (!Showing)
            {
                Hide();
                return false;
            }
            Draw(seconds);
            return !ApocalypseSlowRules.Calm(mode, fade);
        }
        catch (Exception ex)
        {
            Report("step", ex);
            Hide();
            return false;
        }
    }

    // Advances the clocks by seconds of real time and draws bar, glow, tint and rim.
    private static void Draw(float seconds)
    {
        var slow = ApocalypseTime.Slow;
        var charge = ApocalypseTime.Charge;
        if (sinceDenied >= 0f) sinceDenied = sinceDenied + seconds < ApocalypseSlowRules.BlinkSeconds ? sinceDenied + seconds : -1f;
        if (sinceFull >= 0f) sinceFull = sinceFull + seconds < ApocalypseSlowRules.FlashSeconds ? sinceFull + seconds : -1f;
        var full = charge >= ApocalypseTime.MaxCharge;
        if (full && !wasFull && !slow) sinceFull = 0f;
        wasFull = full;
        if (slow && !wasSlow) run = 0f;
        else if (slow) run += seconds;
        wasSlow = slow;
        fade = ApocalypseSlowRules.Approach(fade, slow ? 1f : 0f, seconds);
        mode = ApocalypseSlowRules.ModeOf(slow, charge, ApocalypseTime.MaxCharge, ApocalypseTime.MinChargeToStart, sinceDenied, sinceFull);
        DrawBar(ApocalypseSlowRules.FillShare(charge, ApocalypseTime.MaxCharge));
        SetGlow(mode == ApocalypseSlowRules.Mode.Running, run);
        Tint(fade);
        SetRim(fade, run);
    }

    // ---- the bar ----

    private static void DrawBar(float share)
    {
        if (!EnsureBar()) return;
        var t = mode switch
        {
            ApocalypseSlowRules.Mode.Running => run,
            ApocalypseSlowRules.Mode.Denied => sinceDenied,
            ApocalypseSlowRules.Mode.Flash => sinceFull,
            _ => 0f
        };
        var (fillColour, backColour) = ApocalypseSlowRules.BarColours(mode, t);
        fillImage.color = ColourOf(fillColour, 1f);
        backImage.color = ColourOf(backColour, 1f);
        fill.anchorMax = new Vector2(share, 1f);
        fill.offsetMax = Vector2.zero;
        // The light band, cut to the filled part.
        var filled = bar.rect.width * share / unitsPerRef;
        var showShimmer = false;
        if (mode == ApocalypseSlowRules.Mode.Running && filled > 0f)
        {
            var half = ApocalypseSlowRules.ShimmerHalfWidth;
            var centre = ApocalypseSlowRules.ShimmerCentre(run, filled);
            var left = Math.Max(0f, centre - half);
            var right = Math.Min(filled, centre + half);
            if (right > left)
            {
                shimmerRect.offsetMin = new Vector2(left * unitsPerRef, 0f);
                shimmerRect.offsetMax = new Vector2(right * unitsPerRef, 0f);
                shimmer.uvRect = new Rect((left - (centre - half)) / (2f * half), 0f, (right - left) / (2f * half), 1f);
                showShimmer = true;
            }
        }
        if (shimmer.gameObject.activeSelf != showShimmer) shimmer.gameObject.SetActive(showShimmer);
        var shake = mode == ApocalypseSlowRules.Mode.Denied ? ApocalypseSlowRules.Shake(sinceDenied) : 0f;
        bar.anchoredPosition = new Vector2(barX + shake * unitsPerRef, barY);
        if (!bar.gameObject.activeSelf) bar.gameObject.SetActive(true);
    }

    // The bar under the speed buttons: a child of their own panel (Panel_Speed, no layout group), stretched
    // over its width, BarGap reference pixels below it and BarHeight high. Positions
    // read at Initialize came before the clock's layout, and the bar sat under the date.
    private static bool EnsureBar()
    {
        if (bar != null) return true;
        var pause = clock != null ? clock._pauseButton : null;
        var parent = pause != null ? pause.transform.parent?.TryCast<RectTransform>() : null;
        if (parent == null) return false;
        unitsPerRef = UnitsPerRef(parent);
        var existing = parent.Find(BarName);
        if (existing != null) UnityEngine.Object.Destroy(existing.gameObject);
        var root = new GameObject(BarName);
        root.transform.SetParent(parent, false);
        bar = root.AddComponent<RectTransform>();
        root.AddComponent<LayoutElement>().ignoreLayout = true;
        bar.anchorMin = new Vector2(0f, 0f);
        bar.anchorMax = new Vector2(1f, 0f);
        bar.pivot = new Vector2(0.5f, 1f);
        barX = 0f;
        barY = -ApocalypseSlowRules.BarGap * unitsPerRef;
        bar.anchoredPosition = new Vector2(barX, barY);
        bar.sizeDelta = new Vector2(0f, ApocalypseSlowRules.BarHeight * unitsPerRef);
        var left = parent.rect.xMin;
        var right = parent.rect.xMax;
        var top = parent.rect.yMin + barY;
        backImage = Flat(bar, "Back", out _);
        fillImage = Flat(bar, "Fill", out fill);
        var band = new GameObject("Shimmer");
        band.transform.SetParent(bar, false);
        shimmerRect = band.AddComponent<RectTransform>();
        shimmerRect.anchorMin = Vector2.zero;
        shimmerRect.anchorMax = new Vector2(0f, 1f);
        shimmer = band.AddComponent<RawImage>();
        shimmer.texture = ShimmerTexture();
        shimmer.raycastTarget = false;
        band.SetActive(false);
        DelversHost.Info(string.Format(CultureInfo.InvariantCulture,
            "CORE_APOCALYPSE_SLOW_BAR built parent={0} left={1:0.0} right={2:0.0} top={3:0.0} height={4:0.0} unitsPerRef={5:0.000} screen={6}x{7}",
            parent.name.Replace(' ', '_'), left, right, top, bar.sizeDelta.y, unitsPerRef, Screen.width, Screen.height));
        return true;
    }

    private static Image Flat(Transform parent, string name, out RectTransform rect)
    {
        var part = new GameObject(name);
        part.transform.SetParent(parent, false);
        rect = part.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var image = part.AddComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    // White with the band's strength as alpha, across its full width.
    private static Texture2D ShimmerTexture()
    {
        if (shimmerTexture != null) return shimmerTexture;
        shimmerTexture = new Texture2D(ShimmerTexels, 1, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        var half = ApocalypseSlowRules.ShimmerHalfWidth;
        for (var i = 0; i < ShimmerTexels; i++)
        {
            var x = -half + (i + 0.5f) / ShimmerTexels * 2f * half;
            shimmerTexture.SetPixel(i, 0, new Color(1f, 1f, 1f, ApocalypseSlowRules.ShimmerAt(x, 0f)));
        }
        shimmerTexture.Apply();
        return shimmerTexture;
    }

    // ---- the pause button ----

    // While slow motion runs the pause button looks selected and glows in the beat, framed in gold, and
    // the speed buttons look unselected. Afterwards the game's sprites come back.
    private static void SetGlow(bool on, float t)
    {
        var pause = clock != null ? clock._pauseButtonImage : null;
        if (pause == null)
        {
            glowOn = false;
            return;
        }
        if (!on)
        {
            if (!glowOn) return;
            glowOn = false;
            var images = SpeedImages();
            for (var i = 0; i < images.Length; i++)
                if (images[i] != null && keptSprites[i] != null) images[i].sprite = keptSprites[i];
            if (glow != null) glow.gameObject.SetActive(false);
            foreach (var edge in frameEdges)
                if (edge != null) edge.gameObject.SetActive(false);
            return;
        }
        if (!glowOn)
        {
            glowOn = true;
            var images = SpeedImages();
            for (var i = 0; i < images.Length; i++)
            {
                keptSprites[i] = images[i] != null ? images[i].sprite : null;
                if (images[i] == null) continue;
                var sprite = i == 0 ? clock._selectedSpeedButtonIcon : clock._defaultSpeedButtonIcon;
                if (sprite != null) images[i].sprite = sprite;
            }
            EnsureGlow(pause);
        }
        if (glow != null)
        {
            glow.color = new Color(1f, 1f, 1f, ApocalypseSlowRules.Glow(t));
            if (!glow.gameObject.activeSelf) glow.gameObject.SetActive(true);
        }
        var frame = ColourOf(ApocalypseSlowRules.Gold, ApocalypseSlowRules.FrameAlpha(t));
        foreach (var edge in frameEdges)
        {
            if (edge == null) continue;
            edge.color = frame;
            if (!edge.gameObject.activeSelf) edge.gameObject.SetActive(true);
        }
    }

    // Pause, normal, fast, very fast.
    private static Image[] SpeedImages()
        => clock == null
            ? new Image[4]
            : new[] { clock._pauseButtonImage, clock._normalSpeedButtonImage, clock._fastSpeedButtonImage, clock._veryFastSpeedButtonImage };

    // The glow: the selected sprite once more over the button, white and see-through, below the button's
    // own children. The frame: four 1 px edges with one free pixel to the button, except below (the free
    // row there is the bar's gap).
    private static void EnsureGlow(Image pause)
    {
        if (glow == null)
        {
            var part = new GameObject(GlowName);
            part.transform.SetParent(pause.transform, false);
            part.transform.SetAsFirstSibling();
            var rect = part.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            glow = part.AddComponent<Image>();
            glow.sprite = clock._selectedSpeedButtonIcon != null ? clock._selectedSpeedButtonIcon : pause.sprite;
            glow.type = pause.type;
            glow.preserveAspect = pause.preserveAspect;
            glow.raycastTarget = false;
        }
        if (frameEdges[0] != null) return;
        var u = UnitsPerRef(pause.transform);
        // (anchorMin, anchorMax, offsetMin, offsetMax), offsets in reference pixels: top, bottom, left, right.
        var edges = new (Vector2 AnchorMin, Vector2 AnchorMax, Vector2 OffsetMin, Vector2 OffsetMax)[]
        {
            (new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-2f, 1f), new Vector2(2f, 2f)),
            (new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(-2f, -1f), new Vector2(2f, 0f)),
            (new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(-2f, -1f), new Vector2(-1f, 2f)),
            (new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, -1f), new Vector2(2f, 2f))
        };
        for (var i = 0; i < edges.Length; i++)
        {
            var image = Flat(pause.transform, FrameName + i, out var rect);
            rect.anchorMin = edges[i].AnchorMin;
            rect.anchorMax = edges[i].AnchorMax;
            rect.offsetMin = edges[i].OffsetMin * u;
            rect.offsetMax = edges[i].OffsetMax * u;
            image.transform.SetAsFirstSibling();
            frameEdges[i] = image;
        }
    }

    // ---- clock icon and time ----

    private static void Tint(float amount)
    {
        var text = clock != null ? clock._timeText : null;
        var icon = clock != null ? clock._weatherIcon : null;
        if (!(amount > 0f))
        {
            if (!tintKept) return;
            tintKept = false;
            if (text != null) text.color = timeColour;
            if (icon != null) icon.color = iconColour;
            return;
        }
        if (!tintKept)
        {
            tintKept = true;
            timeColour = text != null ? text.color : Color.white;
            iconColour = icon != null ? icon.color : Color.white;
        }
        var tan = ColourOf(ApocalypseSlowRules.Tan, 1f);
        if (text != null) text.color = Color.Lerp(timeColour, new Color(tan.r, tan.g, tan.b, timeColour.a), amount);
        if (icon != null) icon.color = iconColour * Color.Lerp(Color.white, tan, amount);
    }

    // ---- the rim ----

    private static void SetRim(float amount, float t)
    {
        var weight = ApocalypseSlowRules.RimWeight(amount, t);
        if (!(weight > 0f) || RimStrength <= 0.0)
        {
            if (rim != null && rim.gameObject.activeSelf) rim.gameObject.SetActive(false);
            return;
        }
        if (rim == null && !rimTried)
        {
            rimTried = true;
            rim = CreateRim();
        }
        if (rim == null) return;
        rim.weight = weight;
        if (!rim.gameObject.activeSelf) rim.gameObject.SetActive(true);
    }

    // A copy of the game's vignette object with a copy of its profile (the game's own stay as they are):
    // the vignette in the rim's colour and strength, its priority the first game vignette's plus 1. That
    // equals the game's second vignette (2.0), so which one wins while both are on is open (the author's check).
    private static Volume CreateRim()
    {
        try
        {
            var manager = UnityEngine.Object.FindObjectOfType<PostProcessingManager>();
            var list = manager?.volumeVignette;
            GameObject source = null;
            Volume native = null;
            for (var i = 0; list != null && i < list.Count && native == null; i++)
            {
                var go = list[i];
                var volume = go != null ? go.GetComponent<Volume>() : null;
                if (volume != null && volume.sharedProfile != null)
                {
                    source = go;
                    native = volume;
                }
            }
            if (native == null)
            {
                DelversHost.Warning("CORE_APOCALYPSE_SLOW_RIM missing=vignette list=" + (list?.Count ?? -1).ToString(CultureInfo.InvariantCulture));
                return null;
            }
            var copy = UnityEngine.Object.Instantiate(source, source.transform.parent);
            copy.name = ApocalypseSlowProbe.RimName;
            copy.SetActive(false);
            var own = copy.GetComponent<Volume>();
            var profile = UnityEngine.Object.Instantiate(native.sharedProfile);
            profile.name = ApocalypseSlowProbe.RimName;
            // Instantiate copies the profile, not its components: without copies of its own the rim's colour and
            // strength went into the game's vignette (09A8AB/0.88 became E8C27A/0.42).
            var components = profile.components;
            for (var i = 0; components != null && i < components.Count; i++)
                if (components[i] != null) components[i] = UnityEngine.Object.Instantiate(components[i]);
            own.sharedProfile = profile;
            own.priority = native.priority + 1f;
            own.weight = 0f;
            var vignette = ApocalypseSlowProbe.VignetteOf(profile);
            if (vignette == null)
            {
                UnityEngine.Object.Destroy(copy);
                DelversHost.Warning("CORE_APOCALYPSE_SLOW_RIM missing=vignette-component source=" + source.name.Replace(' ', '_'));
                return null;
            }
            var strength = (float)RimStrength;
            vignette.active = true;
            vignette.color.value = ColourOf(ApocalypseSlowRules.Rim, 1f);
            vignette.color.overrideState = true;
            vignette.intensity.value = strength;
            vignette.intensity.overrideState = true;
            DelversHost.Info(string.Format(CultureInfo.InvariantCulture,
                "CORE_APOCALYPSE_SLOW_RIM built source={0} global={1} priority={2:0.0} intensity={3:0.00} smoothness={4:0.00} colour={5}",
                source.name.Replace(' ', '_'), own.isGlobal ? 1 : 0, own.priority, strength, vignette.smoothness.value, ApocalypseSlowRules.Rim.Hex));
            return own;
        }
        catch (Exception ex)
        {
            Report("rim", ex);
            return null;
        }
    }

    // ---- helpers ----

    private static void Hide()
    {
        SetGlow(false, 0f);
        Tint(0f);
        if (rim != null && rim.gameObject.activeSelf) rim.gameObject.SetActive(false);
        if (bar != null && bar.gameObject.activeSelf) bar.gameObject.SetActive(false);
    }

    // References into a HUD that is gone or replaced.
    private static void Forget()
    {
        bar = fill = shimmerRect = null;
        backImage = fillImage = null;
        shimmer = null;
        glow = null;
        Array.Clear(frameEdges);
        Array.Clear(keptSprites);
        glowOn = false;
        tintKept = false;
    }

    private static Color ColourOf(ApocalypseSlowRules.Rgb rgb, float alpha) => new(rgb.R, rgb.G, rgb.B, alpha);

    // Local units of t per reference pixel of the root canvas.
    private static float UnitsPerRef(Transform t)
    {
        var canvas = clock != null ? clock.GetComponentInParent<Canvas>() : null;
        var root = canvas != null ? canvas.rootCanvas : null;
        if (root == null || t == null) return 1f;
        var own = t.lossyScale.y;
        return own > 0f ? root.transform.lossyScale.y / own : 1f;
    }

    private static void Report(string where, Exception ex)
    {
        if (++errors <= 3) DelversHost.Warning($"CORE_APOCALYPSE_SLOW_DISPLAY_FAILED where={where}: {ex.GetType().Name}: {ex.Message}");
    }
}

// The clock panel of the HUD that was just set up (own address, two callers).
[HarmonyPatch(typeof(SubUI_CampaignClockUI), nameof(SubUI_CampaignClockUI.Initialize))]
internal static class ApocalypseSlowClockPatch
{
    private static void Postfix(SubUI_CampaignClockUI __instance)
    {
        try { ApocalypseSlowDisplay.OnClock(__instance); }
        catch (Exception ex) { DelversHost.Warning("CORE_APOCALYPSE_SLOW_DISPLAY_FAILED where=clock: " + ex.Message); }
    }
}

// The game set the speed buttons' sprites (a speed change; own address, one caller): while slow motion
// runs, Core puts its look back.
[HarmonyPatch(typeof(SubUI_CampaignClockUI), nameof(SubUI_CampaignClockUI.SetSpeedChangeOnUI))]
internal static class ApocalypseSlowSpeedUiPatch
{
    private static void Postfix(SubUI_CampaignClockUI __instance)
    {
        try { ApocalypseSlowDisplay.OnSpeedUi(__instance); }
        catch (Exception ex) { DelversHost.Warning("CORE_APOCALYPSE_SLOW_DISPLAY_FAILED where=speed-ui: " + ex.Message); }
    }
}
