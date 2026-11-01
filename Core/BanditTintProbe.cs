using HarmonyLib;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;
#if BEPINEX
using IExecutionData = global::Refactor.Combat.IExecutionData;
using PassiveAction = global::Refactor.Combat.AffecterRelated.PassiveAction;
using UnitEntityHUD = global::Refactor.View.UnitEntityHUD;
using SequenceType = global::Refactor.Combat.ExecutionProcess.SequenceType;
#else
using IExecutionData = Il2CppRefactor.Combat.IExecutionData;
using PassiveAction = Il2CppRefactor.Combat.AffecterRelated.PassiveAction;
using UnitEntityHUD = Il2CppRefactor.View.UnitEntityHUD;
using SequenceType = Il2CppRefactor.Combat.ExecutionProcess.SequenceType;
#endif

namespace DungeonSettlersDelvers.Core;

// DIAGNOSTIC and the building bar (third attempt: prove before fixing). Observing only,
// capped, nothing changed:
// - CORE_SEED_FX_SOURCE (called from BanditTintSource's patches): at the game's CreateVFX step of the seed's area (AffecterFX_StrangeSeedAura.<OnFXCall>, state 1) and
//   of the sprayer's spray (AffecterFX_MucusSprayer.OnVFX and its <OnVFX>, state 1): the effect's Guid and the execution's user
//   and target, so the owner the effect belongs to is read from the game itself.
// - CORE_BAR_PROBE: once for a seed or sprayer and once for a unit's HUD body bar: world bounds, screen point, sorting layer and
//   order, the SortingGroup above it and the object layer.
// - CORE_HUD_PROBE: once per Core unit kit, every SpriteRenderer of its HUD with its sprite (or "null"), to find a square
//   drawn without a picture.
// All three log only while the diagnostics group probes.tint (CoreProbeRules.Tint) is on, off by default.
internal static class BanditTintProbe
{
    private const int SourceCap = 60;
    private static int sourceLines;
    private static bool buildingProbed, hudBarProbed;
    private static readonly HashSet<string> hudProbed = new(StringComparer.Ordinal);

    internal static void Reset()
    {
        sourceLines = 0;
        buildingProbed = hudBarProbed = false;
        hudProbed.Clear();
    }

    internal static void Source(string kind, string fx, IExecutionData data)
    {
        if (!CoreProbes.Tint || sourceLines >= SourceCap) return;
        sourceLines++;
        IEntity user = null, target = null;
        try { user = data?.User; } catch (Exception) { }
        try { target = data?.Target; } catch (Exception) { }
        DelversHost.Info("CORE_SEED_FX_SOURCE kind=" + kind + " fx=" + (fx ?? "-") + " user=" + Describe(user) + " target=" + Describe(target)
            + " data=" + (data == null ? "-" : TypeName(data)) + " publisher=" + Publisher(data));
    }

    // The execution's publisher (the seed's aura passive?) and its owner.
    private static string Publisher(IExecutionData data)
    {
        try
        {
            var context = data?.BaseExecution?.Context;
            var publisher = context?.Publisher;
            var type = publisher == null ? "-" : publisher.TryCast<Il2CppSystem.Object>()?.GetIl2CppType().Name ?? "?";
            var passive = publisher?.TryCast<PassiveAction>();
            return type + (passive != null ? "/owner=" + Describe(passive.Owner) : "")
                + " ctxUser=" + Describe(context?.User) + " ctxTarget=" + Describe(context?.TargetEntity) + " ctxDelegator=" + Describe(context?.Delegator);
        }
        catch (Exception ex) { return "?" + ex.GetType().Name; }
    }

    private static string TypeName(IExecutionData data)
    {
        try { return data.TryCast<Il2CppSystem.Object>()?.GetIl2CppType().Name ?? "?"; }
        catch (Exception) { return "?"; }
    }

    private static string Describe(IEntity entity)
    {
        if (entity == null) return "-";
        string key, guid;
        try { key = entity.Key ?? "NULL"; } catch (Exception) { key = "?"; }
        try { guid = entity.Guid.ToString(); } catch (Exception) { guid = "?"; }
        return guid + "/" + key;
    }

    // The building's main sprite and its object, once.
    internal static void Building(string id, Transform top, SpriteRenderer body)
    {
        if (!CoreProbes.Tint || buildingProbed || top == null || body == null) return;
        buildingProbed = true;
        DelversHost.Info("CORE_BAR_PROBE what=building id=" + id + " " + Placement(body) + " objectLayer=" + top.gameObject.layer
            + " rootGroup=" + Group(top.GetComponent<SortingGroup>()));
    }

    // A unit's HUD (its body bar once for all, and once per Core kit every renderer).
    internal static void Hud(UnitEntity unit, UnitEntityHUD hud)
    {
        if (!CoreProbes.Tint || unit == null || hud == null) return;
        var body = hud._bodyHealthBar;
        if (!hudBarProbed && body != null)
        {
            hudBarProbed = true;
            DelversHost.Info("CORE_BAR_PROBE what=hudbar unit=" + unit.Key + " " + Placement(body) + " objectLayer=" + body.gameObject.layer
                + " group=" + Group(body.GetComponentInParent<SortingGroup>()));
        }
        if (!CoreUnitRules.IsCoreUnit(unit.Key) || !hudProbed.Add(unit.Key)) return;
        var renderers = hud.GetComponentsInChildren<SpriteRenderer>(true);
        var parts = new List<string>();
        for (var i = 0; renderers != null && i < renderers.Length && parts.Count < 40; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            parts.Add(r.name + ":" + (r.sprite != null ? r.sprite.name : "null") + (r.gameObject.activeInHierarchy && r.enabled ? "" : "(off)"));
        }
        DelversHost.Info("CORE_HUD_PROBE unit=" + unit.Key + " renderers=" + string.Join(",", parts));
    }

    private static string Placement(SpriteRenderer r)
    {
        var b = r.bounds;
        var camera = Camera.main;
        var screen = camera != null ? camera.WorldToScreenPoint(new Vector3(b.center.x, b.max.y, b.center.z)) : Vector3.zero;
        return string.Format(CultureInfo.InvariantCulture,
            "center={0:0.00},{1:0.00} max={2:0.00} size={3:0.00}x{4:0.00} screen={5:0},{6:0} sortLayer={7}:{8} camera={9}",
            b.center.x, b.center.y, b.max.y, b.size.x, b.size.y, screen.x, screen.y, r.sortingLayerName, r.sortingOrder, camera != null ? camera.name : "-");
    }

    private static string Group(SortingGroup group)
        => group == null ? "-" : group.sortingLayerName + ":" + group.sortingOrder + "#" + group.sortingLayerID;
}

// DIAGNOSTIC (Core's Lifebloom icon over the herbalist's head drawn dark, the plant barely visible): how the
// overhead skill display of a Core unit runs. CORE_HUD_SKILL_FLOW: SetSkill, the last progress (sequence, value) and UnsetSkill
// with the seconds between, and the colours of icon and fill overlay at that time; once per unit kit and skill, capped.
// The HUD probes (CORE_HUD_SKILL, CORE_HUD_SKILL_FLOW, CORE_HUD_ACTIVE: this class, BanditHudSkillProbe and the four patch classes
// below) are the diagnostics group probes.hud (CoreProbeRules.Hud), off by default: they found the dark square (the game's own cast
// display of the basic attack) and stay in the code for diagnosis. The patch classes are patched only while the group is on; the
// checks of the scan read the flag first.
internal static class BanditHudFlowProbe
{
    private static readonly Dictionary<IntPtr, (string Skill, float At, string Progress)> running = new();
    private static readonly HashSet<string> logged = new(StringComparer.Ordinal);

    internal static void Set(UnitEntityHUD hud, string skill)
    {
        if (!CoreProbes.Hud || hud == null || !CoreUnitRules.IsCoreUnit(hud._entity?.Key)) return;
        running[hud.Pointer] = (skill, Time.unscaledTime, "-");
    }

    internal static void Progress(UnitEntityHUD hud, string sequence, float value)
    {
        if (!CoreProbes.Hud || hud == null || !running.TryGetValue(hud.Pointer, out var r)) return;
        running[hud.Pointer] = (r.Skill, r.At, sequence + ":" + value.ToString("0.00", CultureInfo.InvariantCulture));
    }

    internal static void Unset(UnitEntityHUD hud)
    {
        if (!CoreProbes.Hud || hud == null || !running.TryGetValue(hud.Pointer, out var r)) return;
        running.Remove(hud.Pointer);
        var key = hud._entity?.Key + "|" + r.Skill;
        if (logged.Count >= 40 || !logged.Add(key)) return;
        DelversHost.Info(string.Format(CultureInfo.InvariantCulture, "CORE_HUD_SKILL_FLOW unit={0} skill={1} seconds={2:0.00} lastProgress={3} icon={4} fill={5}",
            hud._entity?.Key, r.Skill, Time.unscaledTime - r.At, r.Progress, Colour(hud._skillIconRenderer), Colour(hud._skillFillRenderer)));
    }

    // Every change of the set of active renderers of a Core unit's HUD (checked by the scan), with sprite and colour: shows which
    // renderer is on while the dark square is seen (neither the skill display nor the booked one). Capped.
    private static readonly Dictionary<IntPtr, string> lastActive = new();
    private static int activeLines;

    internal static void CheckActive(UnitEntityHUD hud)
    {
        if (!CoreProbes.Hud || hud == null || activeLines >= 200 || !CoreUnitRules.IsCoreUnit(hud._entity?.Key)) return;
        var parts = new List<string>();
        foreach (var r in hud.GetComponentsInChildren<SpriteRenderer>(false))
        {
            if (r == null || !r.enabled) continue;
            parts.Add(string.Format(CultureInfo.InvariantCulture, "{0}:{1}:{2:0.00},{3:0.00},{4:0.00},{5:0.00}", r.name, r.sprite != null ? r.sprite.name : "null",
                r.color.r, r.color.g, r.color.b, r.color.a));
        }
        var now = string.Join(" ", parts);
        if (lastActive.TryGetValue(hud.Pointer, out var before) && before == now) return;
        lastActive[hud.Pointer] = now;
        activeLines++;
        DelversHost.Info("CORE_HUD_ACTIVE unit=" + hud._entity?.Key + " t=" + Time.unscaledTime.ToString("0.0", CultureInfo.InvariantCulture) + " " + now);
    }

    // A display still on after StuckSeconds (checked by the scan): logged once per unit kit and skill.
    internal const float StuckSeconds = 4f;

    internal static void CheckStuck(UnitEntityHUD hud)
    {
        if (!CoreProbes.Hud || hud == null || !running.TryGetValue(hud.Pointer, out var r) || Time.unscaledTime - r.At < StuckSeconds) return;
        var key = "stuck|" + hud._entity?.Key + "|" + r.Skill;
        if (logged.Count >= 40 || !logged.Add(key)) return;
        DelversHost.Info(string.Format(CultureInfo.InvariantCulture, "CORE_HUD_SKILL_FLOW unit={0} skill={1} stuck={2:0.0} lastProgress={3} object={4} icon={5} fill={6}",
            hud._entity?.Key, r.Skill, Time.unscaledTime - r.At, r.Progress, hud._skillObject != null && hud._skillObject.activeInHierarchy ? 1 : 0,
            Colour(hud._skillIconRenderer), Colour(hud._skillFillRenderer)));
    }

    private static string Colour(SpriteRenderer r)
        => r == null ? "-" : string.Format(CultureInfo.InvariantCulture, "{0}:{1:0.00},{2:0.00},{3:0.00},{4:0.00}/scale={5:0.##}x{6:0.##}/on={7}",
            r.sprite != null ? r.sprite.name : "null", r.color.r, r.color.g, r.color.b, r.color.a, r.transform.localScale.x, r.transform.localScale.y,
            r.gameObject.activeInHierarchy && r.enabled ? 1 : 0);
}

[ProbeGroup(CoreProbeRules.Hud)]
[HarmonyPatch(typeof(UnitEntityHUD), nameof(UnitEntityHUD.SetSkillProgress))]
internal static class BanditHudProgressPatch
{
    private static void Postfix(UnitEntityHUD __instance, SequenceType currentSequence, float currentProgress)
    {
        try { BanditHudFlowProbe.Progress(__instance, currentSequence.ToString(), currentProgress); }
        catch (Exception) { }
    }
}

[ProbeGroup(CoreProbeRules.Hud)]
[HarmonyPatch(typeof(UnitEntityHUD), nameof(UnitEntityHUD.UnsetSkill))]
internal static class BanditHudUnsetPatch
{
    private static void Prefix(UnitEntityHUD __instance)
    {
        try { BanditHudFlowProbe.Unset(__instance); }
        catch (Exception) { }
    }
}

// DIAGNOSTIC (a black square at the herbalist's HUD after her Lifebloom charges ran out): the skill icon the
// unit HUD shows for a Core unit, after the game set it (UnitEntityHUD.SetSkill: _skillIconRenderer; SetSkillReserve:
// _bookedSkill, both through ResourceLoader.LoadSkillIcon). CORE_HUD_SKILL once per unit kit, method and skill, capped.
internal static class BanditHudSkillProbe
{
    private static readonly HashSet<string> seen = new(StringComparer.Ordinal);

    internal static void Note(UnitEntityHUD hud, string method, string skill, SpriteRenderer renderer)
    {
        if (!CoreProbes.Hud || hud == null || seen.Count >= 80) return;
        var key = hud._entity?.Key;
        // Core's Lifebloom icon is drawn black over a head; every unit's first icon per skill, to compare
        // Core's sprite with the game's (any unit, the self-test's Frieren casts Lifebloom too).
        if (!seen.Add(method + "|" + skill)) return;
        DelversHost.Info("CORE_HUD_SKILL unit=" + (key ?? "-") + " method=" + method + " skill=" + (skill ?? "-")
            + " on=" + (renderer != null && renderer.gameObject.activeInHierarchy && renderer.enabled ? 1 : 0) + " " + SpriteDetails(renderer));
    }

    private static string SpriteDetails(SpriteRenderer r)
    {
        if (r == null) return "renderer=-";
        var s = r.sprite;
        var m = r.sharedMaterial;
        if (s == null) return "sprite=null material=" + (m != null ? m.name : "-");
        var t = s.texture;
        return string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "sprite={0} texture={1} format={2} size={3}x{4} rect={5:0},{6:0},{7:0}x{8:0} ppu={9:0.#} pivot={10:0.##},{11:0.##} packed={12} secondary={13} filter={14} material={15}/{16} color={17:0.00},{18:0.00},{19:0.00},{20:0.00} scale={21:0.###} drawMode={22}",
            s.name, t != null ? t.name : "-", t != null ? t.format.ToString() : "-", t != null ? t.width : 0, t != null ? t.height : 0,
            s.rect.x, s.rect.y, s.rect.width, s.rect.height, s.pixelsPerUnit, s.pivot.x / Math.Max(1f, s.rect.width), s.pivot.y / Math.Max(1f, s.rect.height),
            s.packed ? 1 : 0, s.GetSecondaryTextureCount(), t != null ? t.filterMode.ToString() : "-", m != null ? m.name : "-", m != null && m.shader != null ? m.shader.name : "-",
            r.color.r, r.color.g, r.color.b, r.color.a, r.transform.localScale.x, r.drawMode);
    }
}

[ProbeGroup(CoreProbeRules.Hud)]
[HarmonyPatch(typeof(UnitEntityHUD), nameof(UnitEntityHUD.SetSkill))]
internal static class BanditHudSkillPatch
{
    private static void Postfix(UnitEntityHUD __instance, string skill)
    {
        try
        {
            BanditHudSkillProbe.Note(__instance, "skill", skill, __instance._skillIconRenderer);
            BanditHudFlowProbe.Set(__instance, skill);
        }
        catch (Exception ex) { DelversHost.Warning("CORE_HUD_SKILL_FAILED skill: " + ex.Message); }
    }
}

[ProbeGroup(CoreProbeRules.Hud)]
[HarmonyPatch(typeof(UnitEntityHUD), nameof(UnitEntityHUD.SetSkillReserve))]
internal static class BanditHudReservePatch
{
    private static void Postfix(UnitEntityHUD __instance, string skill)
    {
        try { BanditHudSkillProbe.Note(__instance, "reserve", skill, __instance._bookedSkill); }
        catch (Exception ex) { DelversHost.Warning("CORE_HUD_SKILL_FAILED reserve: " + ex.Message); }
    }
}
