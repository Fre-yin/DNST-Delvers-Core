using Il2CppInterop.Runtime;
using UnityEngine;

namespace DungeonSettlersDelvers.Core;

// Only on the switch from off to on in the
// menu, never on load. The game's own confirm dialog (as LoadIntegrityNotice), which stays until confirmed; without the
// campaign's UI (title screen) the one-line help notice cannot show either, so only the log says it was skipped.
internal static class GroupAiNotice
{
    private static Il2CppSystem.Action close;

    internal static bool Due(bool before, bool after) => !before && after;

    internal static void Show()
    {
        try
        {
            var dispatcher = UnityEngine.Object.FindObjectOfType<MainFlow>()?._viewDispatcher;
            if (dispatcher == null)
            {
                DelversHost.Info("CORE_AI_GROUP_NOTICE shown=0 reason=no-campaign-ui");
                return;
            }
            UniqueCandidateLocalization.EnsureCurrent();
            close ??= DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(new Action(() => { }));
            dispatcher.DispatchViewUpdateImmediate(new ViewCallbackData(UpdateType.CommonNotice_ShowConfirm,
                GroupAiText.NoticeKey, close, close).Cast<IViewUpdateData>());
            DelversHost.Info("CORE_AI_GROUP_NOTICE shown=1");
        }
        catch (Exception ex)
        {
            DelversHost.Warning("CORE_AI_GROUP_NOTICE shown=0 failed: " + ex.Message);
        }
    }
}
