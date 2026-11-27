using System.Collections;
using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DungeonSettlersDelvers.Core;

// The Steam guard's game side (SteamGuardRules in ModLoaderRules.cs): patches only the Delvers menu's tab, registers Core's page
// with the short line and shows the game's dialog once in the title.
internal static class SteamGuard
{
    private const float NoticeDelaySeconds = 1f;
    private const float NoticeWaitSeconds = 180f;
    private const string TitleScene = "1_Title";
    // The Delvers menu's tab in the game's options, and its Escape key: nothing else of Core is patched in.
    private static readonly Type[] MenuPatchClasses =
    {
        typeof(DelversMenuOptionsTabInitPatch), typeof(DelversMenuOptionsTabRefreshPatch), typeof(DelversMenuEscapePatch)
    };
    private static IDisposable page;
    private static Il2CppSystem.Action close;

    internal static bool Active { get; private set; }

    // True when the guard took over: Core then stays not ready.
    internal static bool Engage(Harmony harmony)
    {
        if (!SteamGuardRules.IsOff(Environment.GetEnvironmentVariable(ModLoaderRules.SteamVariable))) return false;
        Active = true;
        DelversHost.Warning(SteamGuardRules.Line(ModLoader.Release()));
        foreach (var type in MenuPatchClasses) harmony.CreateClassProcessor(type).Patch();
        page ??= DelversCoreRuntime.MenuPages.Register(DelversCoreMenuPage.PackId, DelversCoreMenuPage.Title, Rows);
        DelversHost.StartCoroutine(ShowOnce());
        return true;
    }

    private static IEnumerable<IReadOnlyDictionary<string, object>> Rows()
    {
        yield return DelversMenuRows.Section(DelversCoreMenuPage.Title);
        yield return DelversMenuRows.Text(() => MenuText.Pick(SteamGuardText.MenuLine));
    }

    // Once in the title: wait for the title scene and the game's view dispatcher, then the game's confirm dialog with the notice.
    private static IEnumerator ShowOnce()
    {
        var waited = 0f;
        MainFlow flow = null;
        while (waited < NoticeWaitSeconds)
        {
            if (SceneManager.GetActiveScene().name == TitleScene && DataSheetManager.Instance?._text?._textTable != null)
            {
                flow = UnityEngine.Object.FindObjectOfType<MainFlow>();
                if (flow?._viewDispatcher != null) break;
            }
            waited += Time.unscaledDeltaTime;
            yield return null;
        }
        if (flow?._viewDispatcher == null)
        {
            DelversHost.Info(SteamGuardRules.NoticeLine(false, "no-title"));
            yield break;
        }
        var delay = 0f;
        while (delay < NoticeDelaySeconds)
        {
            delay += Time.unscaledDeltaTime;
            yield return null;
        }
        try
        {
            UniqueCandidateLocalization.EnsureRow(SteamGuardText.NoticeKey);
            close ??= DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(new Action(() => { }));
            flow._viewDispatcher.DispatchViewUpdateImmediate(new ViewCallbackData(UpdateType.CommonNotice_ShowConfirm,
                SteamGuardText.NoticeKey, close, close).Cast<IViewUpdateData>());
            DelversHost.Info(SteamGuardRules.NoticeLine(true, "title"));
        }
        catch (Exception ex) { DelversHost.Warning("CORE_STEAM_GUARD_NOTICE_FAILED: " + ex.Message); }
    }
}
