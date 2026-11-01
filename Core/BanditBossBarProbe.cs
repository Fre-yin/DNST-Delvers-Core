using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
#if BEPINEX
using BossHUDView = global::Refactor.BossHUDView;
using EntitySummaryAffecterUI = global::Refactor.UI.EntitySummaryAffecterUI;
#else
using BossHUDView = Il2CppRefactor.BossHUDView;
using EntitySummaryAffecterUI = Il2CppRefactor.UI.EntitySummaryAffecterUI;
#endif

namespace DungeonSettlersDelvers.Core;

// Probe (the bandits' boss bar in the game's own BossHUDView, four segments with the effects
// under each): once a session, the tree of the game's boss HUD (it lies inactive in the scene) with every node's RectTransform,
// its Image settings and layout components and the effect row's icon count, so the segments are built in the game's style, not
// guessed. CORE_BANDIT_BOSSBAR at=probe, capped. Observing only: the boss bar itself is BanditBossBar. The tint's scan calls Run
// every quarter second; it looks for the HUD only while the group BossBar (CoreProbes.BossBar) is on.
internal static class BanditBossBarProbe
{
    private const int MaxNodes = 60;
    private static bool done;

    internal static void Reset() => done = false;

    internal static void Run()
    {
        if (!CoreProbes.BossBar || done) return;
        var views = Resources.FindObjectsOfTypeAll<BossHUDView>();
        if (views == null || views.Length == 0) return;
        done = true;
        var view = views[0];
        DelversHost.Info("CORE_BANDIT_BOSSBAR at=probe views=" + views.Length + " active=" + (view.gameObject.activeInHierarchy ? 1 : 0)
            + " head=" + (view._headHP != null ? view._headHP.name : "-") + " body=" + (view._bodyHP != null ? view._bodyHP.name : "-")
            + " affecters=" + (view._affecterUI != null ? view._affecterUI.name + "/icons=" + (view._affecterUI._affecterIcons?.Length ?? -1) : "-"));
        var nodes = 0;
        var stack = new Stack<(Transform T, string Path)>();
        stack.Push((view.transform, view.name));
        while (stack.Count > 0 && nodes++ < MaxNodes)
        {
            var (t, path) = stack.Pop();
            DelversHost.Info("CORE_BANDIT_BOSSBAR at=probe node=" + path + " " + Describe(t));
            for (var i = t.childCount - 1; i >= 0; i--)
            {
                var child = t.GetChild(i);
                stack.Push((child, path + "/" + child.name));
            }
        }
    }

    private static string Describe(Transform t)
    {
        var text = "on=" + (t.gameObject.activeSelf ? 1 : 0);
        var rect = t.TryCast<RectTransform>();
        if (rect != null)
            text += string.Format(CultureInfo.InvariantCulture, " pos={0:0.#},{1:0.#} size={2:0.#}x{3:0.#} anchor={4:0.##},{5:0.##}-{6:0.##},{7:0.##} pivot={8:0.##},{9:0.##} scale={10:0.##}",
                rect.anchoredPosition.x, rect.anchoredPosition.y, rect.sizeDelta.x, rect.sizeDelta.y, rect.anchorMin.x, rect.anchorMin.y, rect.anchorMax.x, rect.anchorMax.y,
                rect.pivot.x, rect.pivot.y, rect.localScale.x);
        var image = t.GetComponent<Image>();
        if (image != null)
            text += string.Format(CultureInfo.InvariantCulture, " image={0}:{1}:{2}:{3}:fill={4:0.##} color={5:0.00},{6:0.00},{7:0.00},{8:0.00} material={9}",
                image.sprite != null ? image.sprite.name : "null", image.type, image.fillMethod, image.fillOrigin, image.fillAmount,
                image.color.r, image.color.g, image.color.b, image.color.a, image.material != null ? image.material.name : "-");
        var names = new List<string>();
        foreach (var component in t.GetComponents<Component>())
        {
            if (component == null) continue;
            var name = component.GetIl2CppType().Name;
            if (name is "RectTransform" or "Transform" or "Image" or "CanvasRenderer") continue;
            names.Add(name);
        }
        if (names.Count > 0) text += " components=" + string.Join("+", names);
        var horizontal = t.GetComponent<HorizontalLayoutGroup>();
        if (horizontal != null) text += string.Format(CultureInfo.InvariantCulture, " hlayout=spacing{0:0.#}", horizontal.spacing);
        var grid = t.GetComponent<GridLayoutGroup>();
        if (grid != null) text += string.Format(CultureInfo.InvariantCulture, " grid=cell{0:0.#}x{1:0.#}/spacing{2:0.#}x{3:0.#}/{4}:{5}",
            grid.cellSize.x, grid.cellSize.y, grid.spacing.x, grid.spacing.y, grid.constraint, grid.constraintCount);
        return text;
    }
}
