using HarmonyLib;
using UnityEngine;
#if BEPINEX
using HealSimulator = global::Refactor.Combat.ExecutionProcess.HealSimulator;
#else
using HealSimulator = Il2CppRefactor.Combat.ExecutionProcess.HealSimulator;
#endif

namespace DungeonSettlersDelvers.Core;

// The game's heal execution (HealSimulator) builds its health changes without a source (From null),
// just like regeneration. For the Apocalypse wound (WoundHealPatch) they are heals with a source:
// skills, items, the Strange Seed and Lifebloom. The health changes made in
// CalculateApplyHealToUnit are marked here and taken by WoundHealPatch; what the game capped at full
// health is overheal and takes the wound's buffer (Wounds.Overheal). Only while a wound is open.
internal static class HealSources
{
    private const float KeepSeconds = 10f;
    private const int MaxMarks = 4096;

    private static readonly Dictionary<IntPtr, float> marks = new();

    internal static void ResetLifecycle() => marks.Clear();

    internal static void OnHealToUnit(float heal, UnitEntity to, Il2CppSystem.Collections.Generic.List<IApplyData> result)
    {
        if (!Wounds.AnyOpen || to == null || result == null || !Wounds.IsOpen(CoreGame.GuidOf(to))) return;
        var now = Time.realtimeSinceStartup;
        Prune(now);
        var applied = 0f;
        for (var i = 0; i < result.Count; i++)
        {
            var data = result[i]?.TryCast<StatusApplyData>();
            if (data == null || (data.StatType != StatType.HealthBody && data.StatType != StatType.HealthHead)) continue;
            marks[data.Pointer] = now;
            applied += Math.Max(0f, data.Amount);
        }
        if (heal - applied > WoundRules.OverhealEpsilon) Wounds.Overheal(to, heal - applied, "exec");
    }

    // True once for a health change the heal execution made (WoundHealPatch).
    internal static bool Take(StatusApplyData data) => data != null && marks.Count > 0 && marks.Remove(data.Pointer);

    // Marks of changes the game never applied (a target that died in between) do not pile up.
    private static void Prune(float now)
    {
        if (marks.Count > MaxMarks)
        {
            marks.Clear();
            return;
        }
        if (marks.Count < 64) return;
        foreach (var key in new List<IntPtr>(marks.Keys))
            if (now - marks[key] > KeepSeconds) marks.Remove(key);
    }
}

// After the game split a heal over head and body (both hurt: each part capped at what it misses; only one hurt: the whole heal
// goes to that part without a cap): its changes count as
// a heal with a source. Private in the game; patched by name, arguments bound by name.
[HarmonyPatch(typeof(HealSimulator), "CalculateApplyHealToUnit")]
internal static class HealSourcesPatch
{
    private static void Postfix(float heal, UnitEntity to, Il2CppSystem.Collections.Generic.List<IApplyData> __result)
    {
        try { HealSources.OnHealToUnit(heal, to, __result); }
        catch (Exception ex) { ApocalypseHooks.Report("heal-source", ex); }
    }
}
