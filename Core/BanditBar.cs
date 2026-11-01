using UnityEngine;
using UnityEngine.Rendering;
#if BEPINEX
using UnitEntityHUD = global::Refactor.View.UnitEntityHUD;
using VFXView = global::Refactor.FX.VFXView;
#else
using UnitEntityHUD = Il2CppRefactor.View.UnitEntityHUD;
using VFXView = Il2CppRefactor.FX.VFXView;
#endif

namespace DungeonSettlersDelvers.Core;

// The bandit buildings' health bar in game (BanditBarRules). BanditTint hands over each bandit seed and sprayer it tracks
// and each one that is gone.
// - The look is the enemy unit's own (a thin line was not):
//   the panel of a living bandit's HUD (Panel_Health: frame and background, with its bars) is copied once into a hidden
//   template; each building gets a copy of it with only the body row shown (the head rows and the shield are switched off),
//   its own material copies, and the unit HUD's SortingGroup (HUD layer; its id may be negative, a flag says it was read). The
//   frame is shortened to that one row (TrimToOneRow).
// - The place: right above the building's visible shape, the top of its sprite's outline (the 64x64 frame is mostly empty;
//   D107BA17: two cells too high), with BanditBarRules.TopOffset in between, centred on it.
// - Every frame (after the animations, with BanditTint) the body bar follows Durability / MaxDurability.
// CORE_BANDIT_BUILDING_BAR at=source|made|gone|state, capped.
internal static class BanditBar
{
    private sealed class Bar
    {
        internal IEntity Building;
        internal Transform Top;
        internal Transform Shape;
        internal GameObject Object;
        internal SpriteRenderer Panel;
        internal SpriteRenderer Fill;
        internal SpriteRenderer Back;
        internal float Made;
        internal bool StateLogged;
    }

    private static readonly Dictionary<string, Bar> bars = new(StringComparer.Ordinal);
    private static readonly List<(string Id, IEntity Building, Transform Top)> waiting = new();
    private static GameObject template;
    private static string fillName, backName;
    private static readonly List<string> hidden = new();
    private static Vector3 panelScale = Vector3.one;
    private static bool hasGroup;
    private static int groupLayer, groupOrder;
    private static string groupName = "-";
    private static bool show = true;
    private static int lines;

    internal static void Reset()
    {
        foreach (var bar in bars.Values) Destroy(bar);
        bars.Clear();
        waiting.Clear();
        if (template != null) UnityEngine.Object.Destroy(template);
        template = null;
        fillName = backName = null;
        hidden.Clear();
        shapeTops.Clear();
        hasGroup = false;
        groupLayer = groupOrder = 0;
        groupName = "-";
        show = BanditBarRules.Show(CoreSettings.Number(BanditBarRules.ShowKey));
        lines = 0;
    }

    internal static void OnTracked(string id, IEntity building, Transform top)
    {
        if (!show || building == null || top == null || bars.ContainsKey(id)) return;
        if (template == null) waiting.Add((id, building, top));
        else Make(id, building, top);
    }

    internal static void OnGone(string id)
    {
        waiting.RemoveAll(w => w.Id == id);
        if (!bars.TryGetValue(id, out var bar)) return;
        Destroy(bar);
        bars.Remove(id);
        Note("gone", id, float.NaN, float.NaN);
    }

    internal static void LateFrame(VFXView view)
    {
        if (!show) return;
        if (template == null && waiting.Count > 0)
        {
            if (!FindTemplate()) return;
            foreach (var (id, building, top) in waiting) if (top != null && !bars.ContainsKey(id)) Make(id, building, top);
            waiting.Clear();
        }
        List<string> dead = null;
        foreach (var pair in bars)
        {
            var bar = pair.Value;
            if (bar.Object == null || bar.Top == null)
            {
                (dead ??= new List<string>()).Add(pair.Key);
                continue;
            }
            Place(bar);
            var status = CoreGame.Component<StatusComponent>(bar.Building);
            if (status == null) continue;
            var reader = new StatusReader(status);
            var clip = BanditBarRules.Clip(reader.GetValue(StatType.Durability), reader.GetValue(StatType.MaxDurability));
            if (clip is not float c) continue;
            if (bar.Fill != null) bar.Fill.material.SetFloat(BanditBarRules.ClipProperty, c);
            if (bar.Back != null) bar.Back.material.SetFloat(BanditBarRules.ClipProperty, c);
            if (!bar.StateLogged && Time.unscaledTime - bar.Made > 1f)
            {
                bar.StateLogged = true;
                LogState(pair.Key, bar);
            }
        }
        if (dead != null) foreach (var id in dead) bars.Remove(id);
    }

    // Centred over the building's shape, the panel's lower edge TopOffset above its top.
    private static void Place(Bar bar)
    {
        var shape = ShapeTop(bar);
        var root = bar.Object.transform;
        root.position = new Vector3(shape.x, shape.y, bar.Top.position.z);
        if (bar.Panel == null) return;
        var b = bar.Panel.bounds;
        root.position += new Vector3(shape.x - b.center.x, shape.y + BanditBarRules.TopOffset - b.min.y, 0f);
    }

    // The top centre of what is drawn: the outline of the opaque pixels of the sprite shown now (its physics shape, the game's
    // collider follows the same), else the sprite's bounds. Per sprite the outline's top is read once.
    private static readonly Dictionary<IntPtr, float> shapeTops = new();

    private static Vector2 ShapeTop(Bar bar)
    {
        var renderer = bar.Shape != null ? bar.Shape.GetComponent<SpriteRenderer>() : null;
        if (renderer == null) return bar.Top.position;
        var b = renderer.bounds;
        var sprite = renderer.sprite;
        if (sprite == null) return new Vector2(b.center.x, b.max.y);
        if (!shapeTops.TryGetValue(sprite.Pointer, out var localTop))
        {
            localTop = float.NaN;
            try
            {
                var points = new Il2CppSystem.Collections.Generic.List<Vector2>();
                for (var s = 0; s < sprite.GetPhysicsShapeCount(); s++)
                {
                    points.Clear();
                    sprite.GetPhysicsShape(s, points);
                    for (var i = 0; i < points.Count; i++) localTop = float.IsNaN(localTop) ? points[i].y : Math.Max(localTop, points[i].y);
                }
            }
            catch (Exception) { localTop = float.NaN; }
            if (shapeTops.Count >= 256) shapeTops.Clear();
            shapeTops[sprite.Pointer] = localTop;
        }
        if (float.IsNaN(localTop)) return new Vector2(b.center.x, b.max.y);
        var top = renderer.transform.TransformPoint(new Vector3(0f, localTop, 0f));
        return new Vector2(b.center.x, top.y);
    }

    // The panel of a living bandit's HUD, copied once into a hidden template.
    private static bool FindTemplate()
    {
        var alive = CoreGame.InMap?.GetAliveUnitsOnMap();
        for (var i = 0; alive != null && i < alive.Count; i++)
        {
            var unit = alive[i];
            if (unit == null || !CoreUnitRules.IsCoreUnit(unit.Key)) continue;
            var hud = BanditTint.ObjectOf(unit.Guid)?.GetComponentInChildren<UnitEntityHUD>(true);
            var body = hud?._bodyHealthBar;
            var panel = body != null ? PanelOf(body.transform, hud.transform) : null;
            if (panel == null) continue;
            var group = body.GetComponentInParent<SortingGroup>();
            if (group != null)
            {
                hasGroup = true;
                groupLayer = group.sortingLayerID;
                groupOrder = group.sortingOrder;
                groupName = group.sortingLayerName + ":" + group.sortingOrder + "#" + group.sortingLayerID;
            }
            else
            {
                // The bandits carry the boss marker and their HUD has no group then (group=-, the bar
                // unseen): the game's HUD layer by name.
                var layer = SortingLayer.NameToID("HUD");
                if (SortingLayer.IsValid(layer))
                {
                    hasGroup = true;
                    groupLayer = layer;
                    groupOrder = 0;
                    groupName = "HUD:0#" + layer + "(fixed)";
                }
            }
            fillName = body.name;
            backName = hud._bodyHealthBarBackground != null ? hud._bodyHealthBarBackground.name : null;
            hidden.Clear();
            foreach (var r in new[] { hud._headHealthBar, hud._headHealthBarBackground, hud._shieldBar })
                if (r != null) hidden.Add(r.name);
            panelScale = panel.lossyScale;
            template = UnityEngine.Object.Instantiate(panel.gameObject);
            template.name = "DelversBanditBarTemplate";
            template.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(template);
            var panelRenderer = panel.GetComponent<SpriteRenderer>();
            Note("source", unit.Key, float.NaN, float.NaN, "panel=" + panel.name + ":" + (panelRenderer != null && panelRenderer.sprite != null ? panelRenderer.sprite.name : "-")
                + " fill=" + fillName + " back=" + (backName ?? "-") + " hidden=" + string.Join("+", hidden)
                + " scale=" + panelScale.x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " group=" + groupName);
            return true;
        }
        return false;
    }

    // The bar's panel: the parent of the body bar up to the HUD that carries a sprite of its own (Panel_Health in the probe).
    private static Transform PanelOf(Transform bar, Transform hud)
    {
        for (var at = bar.parent; at != null && at != hud; at = at.parent)
            if (at.GetComponent<SpriteRenderer>() != null) return at;
        return null;
    }

    private static void Make(string id, IEntity building, Transform top)
    {
        var shape = top.Find("Sprite");
        var root = new GameObject("DelversBanditBar");
        root.transform.SetParent(top, false);
        if (hasGroup)
        {
            var sorting = root.AddComponent<SortingGroup>();
            sorting.sortingLayerID = groupLayer;
            sorting.sortingOrder = groupOrder;
        }
        var panel = UnityEngine.Object.Instantiate(template, root.transform, false);
        panel.name = "Panel";
        panel.SetActive(true);
        var parent = root.transform.lossyScale;
        panel.transform.localScale = new Vector3(Div(panelScale.x, parent.x), Div(panelScale.y, parent.y), 1f);
        var bar = new Bar { Building = building, Top = top, Shape = shape, Object = root, Panel = panel.GetComponent<SpriteRenderer>(), Made = Time.unscaledTime };
        var trimmed = TrimToOneRow(panel.transform, bar.Panel);
        foreach (var r in panel.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (r == null || r == bar.Panel) continue;
            if (hidden.Contains(r.name))
            {
                r.gameObject.SetActive(false);
                continue;
            }
            // Own material copies: the template's renderers still point at the source unit's runtime materials.
            if (r.sharedMaterial != null) r.material = new Material(r.sharedMaterial);
            if (r.name == fillName) bar.Fill = r;
            else if (r.name == backName) bar.Back = r;
        }
        bars[id] = bar;
        var status = CoreGame.Component<StatusComponent>(building);
        var reader = status != null ? new StatusReader(status) : null;
        var at = ShapeTop(bar);
        Note("made", id, reader?.GetValue(StatType.Durability) ?? float.NaN, reader?.GetValue(StatType.MaxDurability) ?? float.NaN,
            "shape=" + (shape != null && shape.GetComponent<SpriteRenderer>()?.sprite != null && !float.IsNaN(shapeTops.TryGetValue(shape.GetComponent<SpriteRenderer>().sprite.Pointer, out var t) ? t : float.NaN) ? "outline" : "sprite") + " top="
            + at.y.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " fill=" + (bar.Fill != null ? 1 : 0) + " back=" + (bar.Back != null ? 1 : 0)
            + " trimmed=" + trimmed.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
    }

    // One row for the durability. The panel (9-sliced) loses the distance between the
    // head and the body row in height; the rows move along so the body row keeps its place inside the frame. Returns the height
    // taken off (0 when the panel is not sliced or the rows are not found).
    private static float TrimToOneRow(Transform panel, SpriteRenderer frame)
    {
        if (frame == null || frame.drawMode == SpriteDrawMode.Simple || hidden.Count == 0) return 0f;
        SpriteRenderer head = null, body = null;
        foreach (var r in panel.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (r == null) continue;
            if (r.name == hidden[0]) head = r;
            else if (r.name == fillName) body = r;
        }
        if (head == null || body == null) return 0f;
        var rows = head.bounds.center.y - body.bounds.center.y;
        if (!(rows > 0f) || !(rows < frame.size.y)) return 0f;
        var scale = Math.Abs(panel.lossyScale.y) < 1e-6f ? 1f : panel.lossyScale.y;
        frame.size = new Vector2(frame.size.x, frame.size.y - rows / scale);
        // The frame shrinks around its centre: down by half the rows, the children up by the same, so the body row stays put.
        panel.position -= new Vector3(0f, rows / 2f, 0f);
        for (var i = 0; i < panel.childCount; i++) panel.GetChild(i).position += new Vector3(0f, rows / 2f, 0f);
        return rows;
    }

    // Probe (bars made, none seen): the bar's real state a second after it was made, once per bar. It asks the
    // group OneShot first (CoreProbes) and writes nothing while it is off; the bar itself does not depend on it.
    private static void LogState(string id, Bar bar)
    {
        if (!CoreProbes.OneShot) return;
        if (lines++ >= BanditBarRules.LogCap) return;
        var group = bar.Object.GetComponent<SortingGroup>();
        var camera = Camera.main;
        var text = "CORE_BANDIT_BUILDING_BAR at=state building=" + id + " root=" + (bar.Object.activeInHierarchy ? 1 : 0) + " layer=" + bar.Object.layer
            + " group=" + (group == null ? "-" : group.sortingLayerName + ":" + group.sortingOrder + (group.enabled ? "" : "(off)"))
            + " panel=" + RendererState(bar.Panel, camera) + " fill=" + RendererState(bar.Fill, camera) + " back=" + RendererState(bar.Back, camera)
            + " shapeTop=" + ShapeTop(bar).y.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        DelversHost.Info(text);
    }

    private static string RendererState(SpriteRenderer r, Camera camera)
    {
        if (r == null) return "-";
        var b = r.bounds;
        var screen = camera != null ? camera.WorldToScreenPoint(b.center) : Vector3.zero;
        var m = r.sharedMaterial;
        var clip = m != null && m.HasProperty(BanditBarRules.ClipProperty) ? m.GetFloat(BanditBarRules.ClipProperty) : float.NaN;
        return string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "[on={0} visible={1} sort={2}:{3} center={4:0.00},{5:0.00},{6:0.00} size={7:0.000}x{8:0.000} screen={9:0},{10:0} color={11:0.00},{12:0.00},{13:0.00},{14:0.00} sprite={15} mode={16} material={17}/{18} clip={19:0.00} scale={20:0.###}]",
            r.enabled && r.gameObject.activeInHierarchy ? 1 : 0, r.isVisible ? 1 : 0, r.sortingLayerName, r.sortingOrder, b.center.x, b.center.y, b.center.z,
            b.size.x, b.size.y, screen.x, screen.y, r.color.r, r.color.g, r.color.b, r.color.a, r.sprite != null ? r.sprite.name : "null", r.drawMode,
            m != null ? m.name : "-", m != null && m.shader != null ? m.shader.name : "-", clip, r.transform.lossyScale.x);
    }

    private static float Div(float a, float b) => Math.Abs(b) < 1e-6f ? a : a / b;

    private static void Destroy(Bar bar)
    {
        if (bar?.Object != null) UnityEngine.Object.Destroy(bar.Object);
    }

    private static void Note(string what, string id, float value, float max, string source = null)
    {
        if (lines++ < BanditBarRules.LogCap) DelversHost.Info(BanditBarRules.Line(what, id, value, max, source));
    }
}
