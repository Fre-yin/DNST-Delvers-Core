using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Vignette = UnityEngine.Rendering.Universal.Vignette;
using Volume = UnityEngine.Rendering.Volume;
using VolumeProfile = UnityEngine.Rendering.VolumeProfile;

namespace DungeonSettlersDelvers.Core;

// Probe for the display of Apocalypse's slow motion (sketch B with the rim of B2).
// It reads only, as CORE_SLOWUI_* lines, also returned as a short summary (SlowUiProbeForTest):
// - the clock panel's tree (SubUI_CampaignClockUI): rects in screen pixels from the top left, anchors,
//   sprites with their borders, colours, texts, layouts and masks, Core's own parts included;
// - the canvas and its scaler (what a reference pixel is on this screen);
// - which children the panel's fields point to (day bar, speed buttons, time, weather icon) and the
//   speed buttons' sprites;
// - the game's vignettes (PostProcessingManager.volumeVignette) and Core's copy of one (RimName).
internal static class ApocalypseSlowProbe
{
    // Core's copy of the game's vignette, a sibling of the game's own (ApocalypseSlowDisplay).
    internal const string RimName = "DelversCore_SlowRim";

    private const int NodeCap = 120;
    private const int DepthCap = 8;

    internal static string Run()
    {
        var summary = new StringBuilder();
        try
        {
            var ui = UnityEngine.Object.FindObjectOfType<SubUI_CampaignClockUI>();
            var canvas = ui != null ? ui.GetComponentInParent<Canvas>() : null;
            var root = canvas != null ? canvas.rootCanvas : null;
            var scaler = root != null ? root.GetComponent<CanvasScaler>() : null;
            var line = string.Format(CultureInfo.InvariantCulture,
                "CORE_SLOWUI_PROBE screen={0}x{1} clock={2} canvas={3} scale={4:0.000} scaler={5} ref={6} match={7:0.00} applies={8} slow={9} charge={10:0.00}",
                Screen.width, Screen.height, ui != null ? Clean(ui.name) : "none", root != null ? root.renderMode.ToString() : "-",
                root != null ? root.scaleFactor : 0f, scaler != null ? scaler.uiScaleMode.ToString() : "-",
                scaler != null
                    ? scaler.referenceResolution.x.ToString("0", CultureInfo.InvariantCulture) + "x" + scaler.referenceResolution.y.ToString("0", CultureInfo.InvariantCulture)
                    : "-",
                scaler != null ? scaler.matchWidthOrHeight : 0f, ApocalypseTime.Applies ? 1 : 0, ApocalypseTime.Slow ? 1 : 0, ApocalypseTime.Charge);
            DelversHost.Info(line);
            summary.Append(line.Substring("CORE_SLOWUI_PROBE ".Length));
            if (ui != null)
            {
                var camera = root != null && root.renderMode != RenderMode.ScreenSpaceOverlay ? root.worldCamera : null;
                var count = 0;
                Node(ui.transform, "", 0, camera, ref count);
                summary.Append(" nodes=").Append(count);
                DelversHost.Info(string.Format(CultureInfo.InvariantCulture,
                    "CORE_SLOWUI_PROBE_FIELDS timerBar={0} pause={1} normal={2} fast={3} veryFast={4} pauseImage={5} time={6} weather={7} date={8} default={9} selected={10}",
                    PathOf(ui._timerBarIcon?.transform, ui.transform), PathOf(ui._pauseButton?.transform, ui.transform),
                    PathOf(ui._normalSpeedButton?.transform, ui.transform), PathOf(ui._fastSpeedButton?.transform, ui.transform),
                    PathOf(ui._veryFastSpeedButton?.transform, ui.transform), PathOf(ui._pauseButtonImage?.transform, ui.transform),
                    PathOf(ui._timeText?.transform, ui.transform), PathOf(ui._weatherIcon?.transform, ui.transform),
                    PathOf(ui._dateText?.transform, ui.transform), SpriteText(ui._defaultSpeedButtonIcon), SpriteText(ui._selectedSpeedButtonIcon)));
            }
            var manager = UnityEngine.Object.FindObjectOfType<PostProcessingManager>();
            var list = manager?.volumeVignette;
            summary.Append(" vignettes=").Append(list?.Count ?? -1);
            Transform rimParent = null;
            for (var i = 0; list != null && i < list.Count; i++)
            {
                ProbeVolume("native" + i, list[i]);
                if (rimParent == null && list[i] != null) rimParent = list[i].transform.parent;
            }
            var rim = rimParent != null ? rimParent.Find(RimName) : null;
            if (rim != null) ProbeVolume("core", rim.gameObject);
            summary.Append(" rim=").Append(rim != null ? 1 : 0);
        }
        catch (Exception ex)
        {
            DelversHost.Warning("CORE_SLOWUI_PROBE_FAILED: " + ex.GetType().Name + ": " + ex.Message);
            summary.Append(" error=").Append(ex.GetType().Name);
        }
        return summary.ToString();
    }

    // The first vignette component of a volume profile, null when it has none.
    internal static Vignette VignetteOf(VolumeProfile profile)
    {
        var components = profile?.components;
        for (var i = 0; components != null && i < components.Count; i++)
        {
            var vignette = components[i]?.TryCast<Vignette>();
            if (vignette != null) return vignette;
        }
        return null;
    }

    private static void Node(Transform t, string parentPath, int depth, Camera camera, ref int count)
    {
        if (t == null || count >= NodeCap || depth > DepthCap) return;
        count++;
        var path = parentPath.Length == 0 ? Clean(t.name) : parentPath + "/" + Clean(t.name);
        var line = new StringBuilder();
        line.Append("CORE_SLOWUI_NODE path=").Append(path).Append(" active=").Append(t.gameObject.activeInHierarchy ? 1 : 0);
        var rect = t.TryCast<RectTransform>();
        if (rect != null)
        {
            var r = rect.rect;
            var a = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(new Vector3(r.xMin, r.yMax, 0f)));
            var b = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(new Vector3(r.xMax, r.yMin, 0f)));
            line.Append(string.Format(CultureInfo.InvariantCulture,
                " screen={0:0.0},{1:0.0},{2:0.0},{3:0.0} anchors={4:0.##},{5:0.##}-{6:0.##},{7:0.##} pivot={8:0.##},{9:0.##} pos={10:0.0},{11:0.0} size={12:0.0}x{13:0.0} scale={14:0.###}",
                a.x, Screen.height - a.y, b.x - a.x, a.y - b.y, rect.anchorMin.x, rect.anchorMin.y, rect.anchorMax.x, rect.anchorMax.y,
                rect.pivot.x, rect.pivot.y, rect.anchoredPosition.x, rect.anchoredPosition.y, rect.sizeDelta.x, rect.sizeDelta.y, t.lossyScale.y));
        }
        var image = t.GetComponent<Image>();
        if (image != null)
            line.Append(string.Format(CultureInfo.InvariantCulture, " image={0} type={1} fill={2}:{3:0.00} colour={4} mat={5}",
                SpriteText(image.sprite), image.type, image.fillMethod, image.fillAmount, Hex(image.color),
                image.material != null ? Clean(image.material.name) : "-"));
        var raw = t.GetComponent<RawImage>();
        if (raw != null) line.Append(" raw=").Append(raw.texture != null ? Clean(raw.texture.name) : "-").Append(" colour=").Append(Hex(raw.color));
        var text = t.GetComponent<TMP_Text>();
        if (text != null)
            line.Append(string.Format(CultureInfo.InvariantCulture, " text={0} size={1:0.0} colour={2}", Clean(text.text), text.fontSize, Hex(text.color)));
        var layout = t.GetComponent<LayoutGroup>();
        if (layout != null) line.Append(" layout=").Append(layout.GetIl2CppType().Name);
        if (t.GetComponent<LayoutElement>() != null) line.Append(" layoutElement=1");
        if (t.GetComponent<Mask>() != null || t.GetComponent<RectMask2D>() != null) line.Append(" mask=1");
        DelversHost.Info(line.ToString());
        for (var i = 0; i < t.childCount; i++) Node(t.GetChild(i), path, depth + 1, camera, ref count);
    }

    private static void ProbeVolume(string which, GameObject go)
    {
        if (go == null)
        {
            DelversHost.Info("CORE_SLOWUI_RIM which=" + which + " object=none");
            return;
        }
        var volume = go.GetComponent<Volume>();
        var vignette = VignetteOf(volume != null ? volume.sharedProfile : null);
        DelversHost.Info(string.Format(CultureInfo.InvariantCulture,
            "CORE_SLOWUI_RIM which={0} name={1} active={2} volume={3} global={4} priority={5:0.0} weight={6:0.00} profile={7} vignette={8} intensity={9:0.00}:{10} smoothness={11:0.00}:{12} colour={13}:{14} centre={15:0.00},{16:0.00} rounded={17}",
            which, Clean(go.name), go.activeInHierarchy ? 1 : 0, volume != null ? 1 : 0, volume != null && volume.isGlobal ? 1 : 0,
            volume != null ? volume.priority : 0f, volume != null ? volume.weight : 0f,
            volume != null && volume.sharedProfile != null ? Clean(volume.sharedProfile.name) : "-", vignette != null ? 1 : 0,
            vignette != null ? vignette.intensity.value : 0f, vignette != null && vignette.intensity.overrideState ? 1 : 0,
            vignette != null ? vignette.smoothness.value : 0f, vignette != null && vignette.smoothness.overrideState ? 1 : 0,
            vignette != null ? Hex(vignette.color.value) : "-", vignette != null && vignette.color.overrideState ? 1 : 0,
            vignette != null ? vignette.center.value.x : 0f, vignette != null ? vignette.center.value.y : 0f,
            vignette != null && vignette.rounded.value ? 1 : 0));
    }

    private static string PathOf(Transform t, Transform root)
    {
        if (t == null) return "-";
        var parts = new List<string>();
        for (var at = t; at != null && at.Pointer != root.Pointer; at = at.parent) parts.Insert(0, Clean(at.name));
        return parts.Count == 0 ? "." : string.Join("/", parts);
    }

    private static string SpriteText(Sprite sprite)
        => sprite == null ? "-" : string.Format(CultureInfo.InvariantCulture, "{0}:{1:0}x{2:0}:border={3:0},{4:0},{5:0},{6:0}:ppu={7:0.#}",
            Clean(sprite.name), sprite.rect.width, sprite.rect.height, sprite.border.x, sprite.border.y, sprite.border.z, sprite.border.w, sprite.pixelsPerUnit);

    internal static string Hex(Color c)
        => string.Format(CultureInfo.InvariantCulture, "{0:X2}{1:X2}{2:X2}{3:X2}",
            (int)Math.Round(Mathf.Clamp01(c.r) * 255f), (int)Math.Round(Mathf.Clamp01(c.g) * 255f),
            (int)Math.Round(Mathf.Clamp01(c.b) * 255f), (int)Math.Round(Mathf.Clamp01(c.a) * 255f));

    // Log values without blanks, so a reader can split the line at spaces.
    private static string Clean(string text)
    {
        if (string.IsNullOrEmpty(text)) return "-";
        var chars = text.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
            if (char.IsWhiteSpace(chars[i])) chars[i] = '_';
        return new string(chars);
    }
}
