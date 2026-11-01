using HarmonyLib;
using UnityEngine;

namespace DungeonSettlersDelvers.Core;

// The game's hooks of First Aid (FirstAid). Arguments bound by name.

// The research First Aid shows the game's icon of basic alchemy, chosen on purpose.
[HarmonyPatch(typeof(ResourceLoader), nameof(ResourceLoader.LoadResearchIcon))]
internal static class FirstAidIconPatch
{
    private static bool Prefix(ResourceLoader __instance, string key, ref Sprite __result)
    {
        if (key != FirstAid.ResearchKey) return true;
        try
        {
            __result = __instance.LoadResearchIcon(FirstAid.AlchemyKey);
            return __result == null;
        }
        catch (Exception) { return true; }
    }
}

// Core's recipes join the table before the game parses it: first aid, and the boss parts' repair recipes.
[HarmonyPatch(typeof(RecipeSheet), nameof(RecipeSheet.Parse))]
internal static class FirstAidRecipePatch
{
    private static void Prefix(RecipeTable table)
    {
        try { FirstAid.RegisterRecipes(table); }
        catch (Exception ex) { DelversHost.Error("CORE_FIRSTAID_RECIPES_FAILED: " + ex); }
        try { BossItems.RegisterRecipes(table?.tableDatas); }
        catch (Exception ex) { DelversHost.Error("CORE_BOSS_ITEM_RECIPES_FAILED: " + ex); }
    }

    private static void Postfix(RecipeSheet __instance)
    {
        try { BossItems.MapRecipeOutputs(__instance); }
        catch (Exception ex) { DelversHost.Error("CORE_BOSS_ITEM_RECIPES_FAILED: " + ex); }
    }
}

// The research First Aid and the heavy bandage of weaving join the table before the game parses it.
[HarmonyPatch(typeof(ResearchSheet), nameof(ResearchSheet.Parse))]
internal static class FirstAidResearchPatch
{
    private static void Prefix(ResearchTable table)
    {
        try { FirstAid.RegisterResearch(table); }
        catch (Exception ex) { DelversHost.Error("CORE_FIRSTAID_RESEARCH_FAILED: " + ex); }
    }
}
