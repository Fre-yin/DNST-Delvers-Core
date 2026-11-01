using HarmonyLib;
#if BEPINEX
using AidKitRoutine = global::Refactor.Combat.SkillRelated.SKILL_AidKit._SkillRoutine_d__0;
using BlackSerumRoutine = global::Refactor.Combat.SkillRelated.SKILL_BlackSerum._SkillRoutine_d__1;
#else
using AidKitRoutine = Il2CppRefactor.Combat.SkillRelated.SKILL_AidKit._SkillRoutine_d__0;
using BlackSerumRoutine = Il2CppRefactor.Combat.SkillRelated.SKILL_BlackSerum._SkillRoutine_d__1;
#endif

namespace DungeonSettlersDelvers.Core;

// The game's first aid kit and black serum close every wound of the unit they treat: once per use, when the skill's
// routine starts (state 0), before the game's own effect.
// Wounds.ClearAll also takes away the game's bleeding stacks. The game's routine runs on unchanged;
// every hook catches its own failures (stage wound-aidkit, wound-serum).

[HarmonyPatch(typeof(AidKitRoutine), "MoveNext")]
internal static class WoundAidKitPatch
{
    private static void Prefix(AidKitRoutine __instance)
    {
        try
        {
            if (__instance != null && __instance.__1__state == 0)
                Wounds.ClearAll(__instance.context?.TargetEntity?.TryCast<UnitEntity>(), "firstaid");
        }
        catch (Exception ex) { ApocalypseHooks.Report("wound-aidkit", ex); }
    }
}

[HarmonyPatch(typeof(BlackSerumRoutine), "MoveNext")]
internal static class WoundSerumPatch
{
    private static void Prefix(BlackSerumRoutine __instance)
    {
        try
        {
            if (__instance != null && __instance.__1__state == 0)
                Wounds.ClearAll(__instance.context?.TargetEntity?.TryCast<UnitEntity>(), "serum");
        }
        catch (Exception ex) { ApocalypseHooks.Report("wound-serum", ex); }
    }
}
