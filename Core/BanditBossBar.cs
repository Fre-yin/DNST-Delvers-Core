using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.UI;
#if BEPINEX
using UnitEntityHUD = global::Refactor.View.UnitEntityHUD;
using AffecterHolder = global::Refactor.Component.AffecterHolder;
using BossHUDPresenter = global::Refactor.BossHUDPresenter;
using BossHUDView = global::Refactor.BossHUDView;
using DASText = global::Refactor.UI.DASText;
using EntitySummaryAffecterUI = global::Refactor.UI.EntitySummaryAffecterUI;
using IViewUpdateData = global::Refactor.View.IViewUpdateData;
using ViewEntityData = global::Refactor.View.ViewEntityData;
#else
using UnitEntityHUD = Il2CppRefactor.View.UnitEntityHUD;
using AffecterHolder = Il2CppRefactor.Component.AffecterHolder;
using BossHUDPresenter = Il2CppRefactor.BossHUDPresenter;
using BossHUDView = Il2CppRefactor.BossHUDView;
using DASText = Il2CppRefactor.UI.DASText;
using EntitySummaryAffecterUI = Il2CppRefactor.UI.EntitySummaryAffecterUI;
using IViewUpdateData = Il2CppRefactor.View.IViewUpdateData;
using ViewEntityData = Il2CppRefactor.View.ViewEntityData;
#endif

namespace DungeonSettlersDelvers.Core;

// In game (BanditBossBarRules). The forest bandits carry the game's boss marker (CoreUnitRules.BossTrait), so the game opens
// its boss HUD itself when the first of them enters combat (CombatStateApplier, BossCombatStart; BossHUDPresenter.Init starts the
// boss music). Core then turns the HUD into its own view:
// - the head bar, the body bar and the effect row of the one bound unit are hidden; one segment per bandit present stands in
//   their place in the slayer-first order: a copy of the game's body bar and, where the game has one, of its head bar, the
//   bandit's name above them (bandits.bossbar.names), its shield band, and two copies of the effect row under it;
// - every frame (instead of BossHUDPresenter.RefreshOnFrame, which Core skips while its bar is open) each segment shows its
//   bandit's health, shield (CoreShieldBand) and effects; a fallen one is empty and dark and raises SegmentFell once, also
//   across a rebuild (a bandit already fallen starts as fallen);
// - a further BossCombatStart for a bandit of a role the bar does not show yet rebuilds the bar, at most once per game tick (the
//   game's start runs again, the music may restart); one of a role it shows, or of the bar, is skipped;
//   BossCombatEnd (a bandit's death or leaving) is skipped while one of them lives; then the game's end runs and Core puts the
//   HUD back.
// CORE_BANDIT_BOSSBAR at=start|start-done|rebuild|skip-start|stale|other-boss|skip-end|end|segment|overflow|layout|labels|shield.
internal static class BanditBossBar
{
    private sealed class Segment
    {
        internal int Index;
        internal string Role;
        internal UnitEntity Unit;
        internal GameObject Root;
        internal Image Fill;
        internal Image Frame;
        internal GameObject HeadRoot;
        internal Image HeadFill;
        internal Image HeadFrame;
        internal GameObject Label;
        internal CoreShieldBand Shield;
        // The head bar's own band when the shield does not run over the gap (ShieldRules.ShieldGapKey 0).
        internal CoreShieldBand ShieldHead;
        // The tiled shield's full width (the fill's), 0 for a filled one.
        internal EntitySummaryAffecterUI First;
        internal EntitySummaryAffecterUI Second;
        internal string Shown;
        internal bool Fallen;
    }

    // Raised once per bandit when its segment falls: index, role, unit, reason ("dead"). For effects to come.
    internal static event Action<int, string, Guid, string> SegmentFell;

    private static readonly List<Segment> segments = new();
    private static readonly List<GameObject> hiddenOriginals = new();
    private static BossHUDView view;
    private static bool active, skipEndLogged, overflowLogged, startDone;
    private static int lines, segmentCount = BanditBossBarRules.Segments;
    private static float drop;
    private static float width = BanditBossBarRules.BarWidth;
    private static long lastRebuildTick = -1;
    private static UnitEntity[] keptUnits;

    internal static bool Active => active;

    internal static void Reset()
    {
        Restore("reset");
        lines = 0;
        keptUnits = null;
        lastRebuildTick = -1;
    }

    // A boss start for a bandit while Core's bar is open and one of its bandits lives on the map: skipped, unless its role is not
    // in the bar yet (a late bandit of the first group), then the bar is rebuilt, at most once per game tick. A bandit of a role the
    // bar shows (a second group) stays out. A start while none of them lives (a fight
    // that ended without the game's end): the bar starts anew. Another boss: Core's bar gives way.
    internal static bool OnPresenterInit(IViewUpdateData data)
    {
        var unit = data?.TryCast<ViewEntityData>()?.Entity?.TryCast<UnitEntity>();
        var bandit = unit != null && CoreUnitRules.IsCoreUnit(unit.Key);
        var newMember = active && bandit && !InSegments(unit) && !RoleShown(unit);
        var tick = CoreGame.Flow?._campaignData?.TimeData?.Tick ?? -1;
        var action = BanditBossBarRules.OnStart(active, bandit, active && bandit ? AliveInSegments() : 0, newMember, tick >= 0 && tick == lastRebuildTick);
        if (action == "run") return true;
        if (action == "rebuild")
        {
            lastRebuildTick = tick;
            keptUnits = SlotsOfSegments();
            Log(BanditBossBarRules.Line("rebuild", "unit=" + unit.Guid + " segments=" + segments.Count));
        }
        if (action == "skip")
        {
            Log(BanditBossBarRules.Line("skip-start", "unit=" + unit.Guid));
            return false;
        }
        Restore(action);
        return true;
    }

    // The view of Core's bar (setting bandits.bossbar.view, 0 turns it off: the game's own boss HUD stays as it is).
    internal const string ViewKey = "bandits.bossbar.view";
    private static bool ViewOn => BanditBossBarRules.ViewOn(CoreSettings.Number(ViewKey));

    // The bandits' names above their segments (setting bandits.bossbar.names, 0 turns them off; BanditBossBarRules.LabelScale).
    internal const string NamesKey = "bandits.bossbar.names";
    private static bool NamesOn => BanditBossBarRules.ViewOn(CoreSettings.Number(NamesKey));

    // A copy of the game's boss name text above a segment, with the bandit's own name key, a little above the bar.
    private static GameObject Label(BossHUDView hud, GameObject segmentRoot, GameObject headRoot, UnitEntity unit, int index)
    {
        var title = hud._bossNameText;
        var copy = UnityEngine.Object.Instantiate(title.gameObject, title.transform.parent, false);
        copy.name = "DelversBanditName_" + index;
        copy.transform.localScale = title.transform.localScale * BanditBossBarRules.LabelScale;
        var rect = copy.transform.TryCast<RectTransform>();
        if (rect != null) rect.sizeDelta = new Vector2(BanditBossBarRules.LabelWidth(segmentCount, width), rect.sizeDelta.y);
        // Above the higher of the segment's two bars.
        var top = Math.Max(TopOf(segmentRoot), headRoot != null ? TopOf(headRoot) : float.MinValue);
        var labelHeight = rect != null ? rect.rect.height * rect.lossyScale.y : 0f;
        copy.transform.position = new Vector3(segmentRoot.transform.position.x, BanditBossBarRules.LabelCenterY(top, labelHeight), segmentRoot.transform.position.z);
        var text = copy.GetComponent<DASText>();
        if (text != null && unit != null)
        {
            text.SetUseTextKey(true);
            text.SetTextKey(CoreUnitRules.NameKey(unit.Key));
        }
        copy.SetActive(true);
        if (index == 0) Log(BanditBossBarRules.Line("labels", "scale=" + BanditBossBarRules.LabelScale.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
            + " key=" + (unit != null ? CoreUnitRules.NameKey(unit.Key) : "-")));
        return copy;
    }

    // The world height of a bar's top edge.
    private static float TopOf(GameObject bar)
    {
        var rect = bar.transform.TryCast<RectTransform>();
        return rect == null ? bar.transform.position.y : bar.transform.position.y + rect.rect.yMax * rect.lossyScale.y;
    }

    // Whether the bar already shows this bandit (by GUID).
    private static bool InSegments(UnitEntity unit)
    {
        var guid = unit.Guid.ToString();
        foreach (var segment in segments) if (segment.Unit != null && segment.Unit.Guid.ToString() == guid) return true;
        return false;
    }

    // Whether the bar already has a segment for this bandit's role (alive or fallen).
    private static bool RoleShown(UnitEntity unit)
    {
        if (!CoreUnitRules.TryGet(unit.Key, out var definition)) return false;
        foreach (var segment in segments) if (segment.Role == definition.Role) return true;
        return false;
    }

    // The bar's bandits by slot, the fallen ones included, for a rebuild.
    private static UnitEntity[] SlotsOfSegments()
    {
        var slots = new UnitEntity[BanditBossBarRules.Segments];
        foreach (var segment in segments)
        {
            var slot = Array.IndexOf(BanditBossBarRules.RoleOrder, segment.Role);
            if (slot >= 0 && segment.Unit != null) slots[slot] = segment.Unit;
        }
        return slots;
    }

    // The segments' bandits alive on the shown map now.
    private static int AliveInSegments()
    {
        var alive = 0;
        foreach (var segment in segments) if (segment.Unit != null && !segment.Unit.IsDead && OnMap(segment.Unit)) alive++;
        return alive;
    }

    private static bool OnMap(UnitEntity unit)
    {
        var alive = CoreGame.InMap?.GetAliveUnitsOnMap();
        for (var i = 0; alive != null && i < alive.Count; i++) if (alive[i] != null && alive[i].Pointer == unit.Pointer) return true;
        return false;
    }

    // After the game bound its boss HUD to a unit: a bandit of the boss party turns it into one segment per bandit there now.
    internal static void OnViewInit(BossHUDView hud, UnitEntity unit)
    {
        if (active || hud == null || unit == null || !CoreUnitRules.IsCoreUnit(unit.Key)) return;
        // Bandits kept by a rebuild belong to that bar only; with the view off they would wait for the next.
        if (!ViewOn)
        {
            keptUnits = null;
            return;
        }
        var body = hud._bodyHP != null ? hud._bodyHP.transform.parent : null;
        var head = hud._headHP != null ? hud._headHP.transform.parent : null;
        var effects = hud._affecterUI;
        if (body == null || effects == null) return;
        view = hud;
        active = true;
        skipEndLogged = overflowLogged = startDone = false;
        drop = BanditBossBarRules.Drop(CoreSettings.Number(BanditBossBarRules.DropKey));
        width = BanditBossBarRules.Width(CoreSettings.Number(BanditBossBarRules.WidthKey));
        var units = BanditBossBarRules.KeepSlots(keptUnits, PartyUnits());
        keptUnits = null;
        var filled = new bool[units.Length];
        for (var i = 0; i < units.Length; i++) filled[i] = units[i] != null;
        var slots = BanditBossBarRules.PresentSlots(filled);
        if (slots.Length == 0) slots = new[] { Math.Max(0, CoreUnitRules.TryGet(unit.Key, out var bound) ? BanditBossBarRules.SlotOf(bound.Role) : 0) };
        segmentCount = slots.Length;
        for (var i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            // A bandit kept fallen by a rebuild starts as fallen, so its segment line and SegmentFell do not come again.
            var segment = new Segment { Index = i, Role = BanditBossBarRules.RoleOrder[slot], Unit = units[slot] ?? unit, Fallen = units[slot]?.IsDead ?? false };
            segment.Root = UnityEngine.Object.Instantiate(body.gameObject, body.parent, false);
            segment.Root.name = "DelversBanditSegment_" + i;
            Lay(segment.Root.transform, body, i);
            segment.Frame = segment.Root.GetComponent<Image>();
            segment.Fill = segment.Root.transform.Find("Fill")?.GetComponent<Image>();
            // A copy of the game's head bar per segment too.
            if (head != null)
            {
                segment.HeadRoot = UnityEngine.Object.Instantiate(head.gameObject, head.parent, false);
                segment.HeadRoot.name = "DelversBanditHead_" + i;
                LayHead(segment.HeadRoot.transform, head, body, i);
                segment.HeadFrame = segment.HeadRoot.GetComponent<Image>();
                segment.HeadFill = HeadFillOf(segment.HeadRoot, hud);
            }
            if (NamesOn && hud._bossNameText != null) segment.Label = Label(hud, segment.Root, segment.HeadRoot, segment.Unit, i);
            Shield(segment, i);
            segment.First = Row(effects, i, 0);
            segment.Second = Row(effects, i, 1);
            segments.Add(segment);
        }
        // Over every bar: the later segments' bars were made after the earlier shields.
        foreach (var segment in segments)
        {
            segment.Shield?.ToFront();
            segment.ShieldHead?.ToFront();
        }
        Hide(body.gameObject);
        if (head != null) Hide(head.gameObject);
        Hide(effects.gameObject);
        var name = hud._bossNameText;
        if (name != null)
        {
            name.SetUseTextKey(true);
            name.SetTextKey(BanditBossBarRules.NameKey);
        }
        int found = 0, fallen = 0;
        foreach (var u in units)
        {
            if (u == null) continue;
            found++;
            if (u.IsDead) fallen++;
        }
        // No update here: this runs inside the game's start of the HUD; the first comes with the next frame (Frame).
        Log(BanditBossBarRules.Line("start", "bound=" + unit.Key + " bandits=" + found + " fallen=" + fallen + " segments=" + segmentCount + " iconsPerRow=" + BanditBossBarRules.IconsPerRow(segmentCount, width)));
    }

    // Instead of BossHUDPresenter.RefreshOnFrame while Core's bar is open (the game sets its bars there, not in
    // BossHUDView.UpdateUI; the segments stayed full). False when the bar has to close: no bandit of it lives on
    // the map any more, though the game sent no end (a wipe, a fight left).
    internal static bool Frame()
    {
        if (!active) return true;
        if (!startDone)
        {
            startDone = true;
            Log(BanditBossBarRules.Line("start-done", "segments=" + segments.Count));
        }
        if (AliveInSegments() == 0) return false;
        Update();
        return true;
    }

    // The bar's place for a segment: its share of the body bar, its fill and deco moved along.
    private static void Lay(Transform segment, Transform body, int index)
    {
        var rect = segment.TryCast<RectTransform>();
        var source = body.TryCast<RectTransform>();
        if (rect == null || source == null) return;
        var segmentWidth = BanditBossBarRules.SegmentWidth(segmentCount, width);
        rect.sizeDelta = new Vector2(segmentWidth, source.sizeDelta.y);
        rect.anchoredPosition = new Vector2(source.anchoredPosition.x + BanditBossBarRules.SegmentCenter(index, segmentCount, width), source.anchoredPosition.y - drop);
        for (var c = 0; c < segment.childCount; c++)
        {
            var child = segment.GetChild(c).TryCast<RectTransform>();
            if (child == null) continue;
            if (child.name == "Fill") child.sizeDelta = new Vector2(segmentWidth - BanditBossBarRules.FillInset, child.sizeDelta.y);
            else child.anchoredPosition = new Vector2(-segmentWidth / 2f + BanditBossBarRules.FillInset / 2f, child.anchoredPosition.y);
        }
    }

    // The shield over a segment's bars (CoreShieldBand): over the head and the body bar or one band per bar, flush with their
    // outer edges, framed white, layered above 100 %.
    private static void Shield(Segment segment, int index)
    {
        if (segment.Root == null || segment.Unit == null) return;
        try
        {
            var (body, head, line) = CoreShieldBand.Over(segment.Root.transform.TryCast<RectTransform>(),
                segment.HeadRoot != null ? segment.HeadRoot.transform.TryCast<RectTransform>() : null, "DelversBanditShield_" + index);
            segment.Shield = body;
            segment.ShieldHead = head;
            if (index == 0) Log(BanditBossBarRules.Line("shield", "source=honeycomb " + line + " border=" + ShieldRules.ShieldBorder.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " layers=" + ShieldRules.ShieldLayerCount + " " + CorePartyShield.MaxLine(segment.Unit)));
        }
        catch (Exception ex) { if (index == 0) Log(BanditBossBarRules.Line("shield", "failed=" + ex.GetType().Name)); }
    }

    // The head bar's copy for a segment: the game's head bar scaled to the segment's share, at the same offset from the body bar.
    private static void LayHead(Transform copy, Transform head, Transform body, int index)
    {
        var rect = copy.TryCast<RectTransform>();
        var source = head.TryCast<RectTransform>();
        var bodyRect = body.TryCast<RectTransform>();
        if (rect == null || source == null || bodyRect == null) return;
        var scale = bodyRect.sizeDelta.x > 0f ? BanditBossBarRules.SegmentWidth(segmentCount, width) / bodyRect.sizeDelta.x : 1f;
        var headWidth = BanditBossBarRules.HeadWidth(source.sizeDelta.x, bodyRect.sizeDelta.x, segmentCount, width);
        rect.sizeDelta = new Vector2(headWidth, source.sizeDelta.y);
        var offsetX = (source.anchoredPosition.x - bodyRect.anchoredPosition.x) * scale;
        rect.anchoredPosition = new Vector2(bodyRect.anchoredPosition.x + BanditBossBarRules.SegmentCenter(index, segmentCount, width) + offsetX, source.anchoredPosition.y - drop);
        for (var c = 0; c < copy.childCount; c++)
        {
            var child = copy.GetChild(c).TryCast<RectTransform>();
            if (child == null) continue;
            if (child.name == "Fill") child.sizeDelta = new Vector2(headWidth - BanditBossBarRules.FillInset, child.sizeDelta.y);
            else child.anchoredPosition = new Vector2(-headWidth / 2f + BanditBossBarRules.FillInset / 2f, child.anchoredPosition.y);
        }
        if (index == 0)
            Log(BanditBossBarRules.Line("layout", FormattableString.Invariant(
                $"head={source.sizeDelta.x:0.#}x{source.sizeDelta.y:0.#}@{source.anchoredPosition.x:0.#},{source.anchoredPosition.y:0.#} body={bodyRect.sizeDelta.x:0.#}x{bodyRect.sizeDelta.y:0.#}@{bodyRect.anchoredPosition.x:0.#},{bodyRect.anchoredPosition.y:0.#} headSegment={headWidth:0.#} drop={drop:0.#} width={width:0.#}")));
    }

    // The copy's own fill image: the child that matches the game's _headHP by name, else one called Fill.
    private static Image HeadFillOf(GameObject copy, BossHUDView hud)
    {
        var name = hud._headHP != null ? hud._headHP.name : "Fill";
        var found = copy.transform.Find(name) ?? copy.transform.Find("Fill");
        return found != null ? found.GetComponent<Image>() : null;
    }

    // A copy of the game's effect row under a segment: the first row at the game's height, the second one icon lower.
    private static EntitySummaryAffecterUI Row(EntitySummaryAffecterUI source, int index, int row)
    {
        var copy = UnityEngine.Object.Instantiate(source.gameObject, source.transform.parent, false);
        copy.name = "DelversBanditEffects_" + index + "_" + row;
        var rect = copy.transform.TryCast<RectTransform>();
        var origin = source.transform.TryCast<RectTransform>();
        var body = view._bodyHP.transform.parent.TryCast<RectTransform>();
        if (rect != null && origin != null)
            rect.anchoredPosition = new Vector2((body != null ? body.anchoredPosition.x : 0f) + BanditBossBarRules.SegmentLeft(index, segmentCount, width),
                origin.anchoredPosition.y - drop - row * BanditBossBarRules.IconWidth);
        copy.SetActive(true);
        return copy.GetComponent<EntitySummaryAffecterUI>();
    }

    private static void Hide(GameObject original)
    {
        if (original == null || !original.activeSelf) return;
        original.SetActive(false);
        hiddenOriginals.Add(original);
    }

    // The party's bandits on the map, one per role in the bar's order (null where none is).
    private static UnitEntity[] PartyUnits()
    {
        var units = new UnitEntity[BanditBossBarRules.Segments];
        var alive = CoreGame.InMap?.GetAliveUnitsOnMap();
        for (var i = 0; alive != null && i < alive.Count; i++)
        {
            var unit = alive[i];
            if (unit == null || unit.IsDead || !CoreUnitRules.TryGet(unit.Key, out var definition) || !CoreUnitRules.IsBossParty(definition)) continue;
            var slot = BanditBossBarRules.SlotOf(definition.Role);
            if (slot >= 0 && units[slot] == null) units[slot] = unit;
        }
        return units;
    }

    // Instead of BossHUDPresenter.RefreshOnFrame, which Core skips while its bar is open.
    internal static void Update()
    {
        if (!active) return;
        foreach (var segment in segments)
        {
            var unit = segment.Unit;
            var dead = unit == null || unit.IsDead || !OnMap(unit);
            // As the game's boss HUD: the body bar the body's health, the head bar the head's (with no head bar copy the body bar
            // keeps both together, as before).
            if (segment.Fill != null)
                segment.Fill.fillAmount = dead ? 0f : segment.HeadFill != null
                    ? BanditBossBarRules.BarFill(CoreGame.Stat(unit, StatType.HealthBody), CoreGame.Stat(unit, StatType.MaxHealthBody), false)
                    : BanditBossBarRules.Fill(CoreGame.Stat(unit, StatType.HealthHead), CoreGame.Stat(unit, StatType.HealthBody),
                        CoreGame.Stat(unit, StatType.MaxHealthHead), CoreGame.Stat(unit, StatType.MaxHealthBody), false);
            if (segment.HeadFill != null)
                segment.HeadFill.fillAmount = dead ? 0f : BanditBossBarRules.BarFill(CoreGame.Stat(unit, StatType.HealthHead), CoreGame.Stat(unit, StatType.MaxHealthHead), false);
            if (segment.Shield != null)
            {
                var share = CorePartyShield.Preview() ?? ShieldRules.ShieldShare(CoreGame.Stat(unit, StatType.Barrier),
                    CoreGame.Stat(unit, StatType.MaxHealthTotal), dead);
                // The comfort option shield band (QolText) off shows the boss bar's band empty too.
                if (dead || !CoreSettings.ShieldBandShown) share = 0f;
                var alpha = BanditBossBarRules.ShieldOpacity(CoreSettings.Number(BanditBossBarRules.ShieldKey));
                segment.Shield.Show(share, alpha);
                segment.ShieldHead?.Show(share, alpha);
            }
            if (dead)
            {
                if (segment.Frame != null) segment.Frame.color = new Color(0.35f, 0.35f, 0.35f, 1f);
                if (segment.HeadFrame != null) segment.HeadFrame.color = new Color(0.35f, 0.35f, 0.35f, 1f);
                segment.First?.OffUI();
                segment.Second?.OffUI();
                if (!segment.Fallen)
                {
                    segment.Fallen = true;
                    var guid = CoreGame.GuidOf(unit);
                    Log(BanditBossBarRules.SegmentLine(segment.Index, segment.Role, unit != null ? guid.ToString("D") : null, "dead"));
                    try { SegmentFell?.Invoke(segment.Index, segment.Role, guid, "dead"); }
                    catch (Exception ex) { DelversHost.Warning("CORE_BANDIT_BOSSBAR_FAILED event: " + ex.Message); }
                }
                continue;
            }
            try { Effects(segment, unit); }
            catch (Exception ex) { DelversHost.Warning("CORE_BANDIT_BOSSBAR_FAILED effects: " + ex.Message); }
        }
    }

    private static void Effects(Segment segment, UnitEntity unit)
    {
        var all = CoreGame.Affecters(unit)?.GetExposedList();
        var count = all?.Count ?? 0;
        var (first, second, left) = BanditBossBarRules.Rows(count, segmentCount, width);
        var signature = count + "|";
        for (var i = 0; i < count; i++) signature += all[i]?.Key + ",";
        var firstList = new Il2CppSystem.Collections.Generic.List<AffecterHolder>();
        var secondList = new Il2CppSystem.Collections.Generic.List<AffecterHolder>();
        // AffecterHolder is a struct: List<T>.Add under BepInEx hands over the boxed object, and the game read a garbage key
        // from it (AccessViolationException in SetUI). DelversNativeList.AddValue passes the struct data.
        for (var i = 0; i < first; i++) if (all[i] != null) DelversNativeList.AddValue(firstList, all[i]);
        for (var i = first; i < first + second; i++) if (all[i] != null) DelversNativeList.AddValue(secondList, all[i]);
        if (signature != segment.Shown)
        {
            segment.Shown = signature;
            segment.First?.SetUI(firstList);
            segment.Second?.SetUI(secondList);
            if (left > 0 && !overflowLogged)
            {
                overflowLogged = true;
                Log(BanditBossBarRules.Line("overflow", "role=" + segment.Role + " effects=" + count + " shown=" + (first + second)));
            }
        }
        segment.First?.RefreshOnFrame(firstList);
        segment.Second?.RefreshOnFrame(secondList);
    }

    // The game's boss end while a bandit of the bar still lives on the map: skipped (counted on the map now; a
    // bar left open from a wiped fight ended at the first death of the next one).
    internal static bool OnCombatEnd()
    {
        if (!active) return true;
        var alive = AliveInSegments();
        if (BanditBossBarRules.KeepsOpen(active, alive) && CoreGame.InMap != null)
        {
            if (!skipEndLogged)
            {
                skipEndLogged = true;
                Log(BanditBossBarRules.Line("skip-end", "alive=" + alive));
            }
            return false;
        }
        return true;
    }

    internal static void AfterCombatEnd()
    {
        if (active) Restore("end");
    }

    private static void Restore(string why)
    {
        if (!active && segments.Count == 0) return;
        foreach (var segment in segments)
        {
            if (segment.Root != null) UnityEngine.Object.Destroy(segment.Root);
            if (segment.HeadRoot != null) UnityEngine.Object.Destroy(segment.HeadRoot);
            // The shield hangs under the bars' parent, not under the segment.
            segment.Shield?.Destroy();
            segment.ShieldHead?.Destroy();
            if (segment.Label != null) UnityEngine.Object.Destroy(segment.Label);
            if (segment.First != null) UnityEngine.Object.Destroy(segment.First.gameObject);
            if (segment.Second != null) UnityEngine.Object.Destroy(segment.Second.gameObject);
        }
        segments.Clear();
        drop = 0f;
        width = BanditBossBarRules.BarWidth;
        // The bandits kept for a rebuild wait only for the start right after it.
        if (why != "rebuild") keptUnits = null;
        foreach (var original in hiddenOriginals) if (original != null) original.SetActive(true);
        hiddenOriginals.Clear();
        if (view?._bossNameText != null) view._bossNameText.SetUseTextKey(false);
        if (active) Log(BanditBossBarRules.Line("end", "reason=" + why));
        active = false;
        view = null;
    }

    private static void Log(string line)
    {
        if (lines++ < 200) DelversHost.Info(line);
    }
}

[HarmonyPatch(typeof(BossHUDPresenter), "Init")]
internal static class BanditBossBarPresenterInitPatch
{
    private static bool Prefix(IViewUpdateData viewData)
    {
        try { return BanditBossBar.OnPresenterInit(viewData); }
        catch (Exception ex)
        {
            DelversHost.Warning("CORE_BANDIT_BOSSBAR_FAILED init: " + ex.Message);
            return true;
        }
    }
}

[HarmonyPatch(typeof(BossHUDView), nameof(BossHUDView.Init))]
internal static class BanditBossBarViewInitPatch
{
    private static void Postfix(BossHUDView __instance, UnitEntity unit)
    {
        try { BanditBossBar.OnViewInit(__instance, unit); }
        catch (Exception ex) { DelversHost.Warning("CORE_BANDIT_BOSSBAR_FAILED view: " + ex.Message); }
    }
}

[HarmonyPatch(typeof(BossHUDPresenter), "RefreshOnFrame")]
internal static class BanditBossBarFramePatch
{
    private static bool Prefix(BossHUDPresenter __instance, IViewUpdateData viewData)
    {
        if (!BanditBossBar.Active) return true;
        try
        {
            // No bandit left on the map: the game's own end closes the HUD and its music (and Core's view with it).
            if (!BanditBossBar.Frame()) __instance.CombatEnd(viewData);
        }
        catch (Exception ex) { DelversHost.Warning("CORE_BANDIT_BOSSBAR_FAILED frame: " + ex.Message); }
        return false;
    }
}

[HarmonyPatch(typeof(BossHUDPresenter), "CombatEnd")]
internal static class BanditBossBarEndPatch
{
    private static bool Prefix()
    {
        try { return BanditBossBar.OnCombatEnd(); }
        catch (Exception ex)
        {
            DelversHost.Warning("CORE_BANDIT_BOSSBAR_FAILED end: " + ex.Message);
            return true;
        }
    }

    private static void Postfix(bool __runOriginal)
    {
        if (!__runOriginal) return;
        try { BanditBossBar.AfterCombatEnd(); }
        catch (Exception ex) { DelversHost.Warning("CORE_BANDIT_BOSSBAR_FAILED restore: " + ex.Message); }
    }
}
