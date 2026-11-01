using HarmonyLib;
using UnityEngine;
#if BEPINEX
using UIInputStream = global::Refactor.Main.InputModule.UIInputStream;
#else
using UIInputStream = Il2CppRefactor.Main.InputModule.UIInputStream;
#endif

namespace DungeonSettlersDelvers.Core;

// The research screen's content has the size its prefab gives, just below the game's last row, so the Delvers tree beneath it
// could not be scrolled to (First Aid on row 21 wholly below the view at the lowest scroll). Once the game
// has set up its nodes (SubUI_ResearchPanel.InitResearchNodes, each time the screen is built), the content's bottom edge goes to
// the lowest node plus half a node (DelversResearchTree.ContentGrowth); the nodes keep their places. A second build finds nothing
// to add. CORE_RESEARCH_CONTENT when it grows.
internal static class DelversResearchTreeView
{
    private static int lines;

    internal static void FitContent(SubUI_ResearchPanel panel)
    {
        var content = panel?._contentParent;
        var nodes = panel?._researchNodes;
        if (content == null || nodes == null || nodes.Count == 0) return;
        var lowest = float.MaxValue;
        var height = 0f;
        foreach (var pair in nodes)
        {
            var node = pair.Value;
            if (node == null) continue;
            var corners = CoreShieldLayout.Corners(node.transform, content);
            var bottom = Math.Min(corners[0].y, corners[3].y);
            if (bottom >= lowest) continue;
            lowest = bottom;
            height = Math.Abs(corners[1].y - corners[0].y);
        }
        if (lowest == float.MaxValue) return;
        var before = content.rect;
        var growth = DelversResearchTree.ContentGrowth(before.yMin, lowest, height * 0.5f);
        if (growth <= 0f) return;
        var size = content.sizeDelta;
        content.sizeDelta = new Vector2(size.x, size.y + DelversResearchTree.SizeFor(growth, content.pivot.y));
        if (lines++ < 4)
            DelversHost.Info(FormattableString.Invariant(
                $"CORE_RESEARCH_CONTENT bottom={before.yMin:0.#}->{content.rect.yMin:0.#} lowestNode={lowest:0.#} height={before.height:0.#}->{content.rect.height:0.#} pivot={content.pivot.y:0.##}"));
    }
}

[HarmonyPatch(typeof(SubUI_ResearchPanel), nameof(SubUI_ResearchPanel.InitResearchNodes),
    new[] { typeof(Il2CppSystem.Collections.Generic.Dictionary<string, ResearchProgress>), typeof(Il2CppSystem.Collections.Generic.Queue<string>), typeof(UIInputStream) })]
internal static class DelversResearchTreeViewPatch
{
    private static void Postfix(SubUI_ResearchPanel __instance)
    {
        try { DelversResearchTreeView.FitContent(__instance); }
        catch (Exception ex) { ApocalypseHooks.Report("research-content", ex); }
    }
}
