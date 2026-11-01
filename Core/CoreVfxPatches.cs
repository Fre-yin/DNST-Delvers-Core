using HarmonyLib;
using UnityEngine;
#if BEPINEX
using VFXID = global::Refactor.FX.VFXID;
using VFXView = global::Refactor.FX.VFXView;
#else
using VFXID = Il2CppRefactor.FX.VFXID;
using VFXView = Il2CppRefactor.FX.VFXView;
#endif

namespace DungeonSettlersDelvers.Core;

// Core's own entries into the game's VFX creation (VFXView.CreateVFX), apart from the probes: the view is noted for the wounds'
// body blood (WoundBodyArt.SeeView), and a new effect is handed to the bandits' tint (BanditTint.OnVfx). Both stay in the order
// they had; the tint keeps its own catch and its log text.
[HarmonyPatch(typeof(VFXView), nameof(VFXView.CreateVFX))]
internal static class CoreVfxPatches
{
    private static void Postfix(VFXView __instance, Il2CppSystem.Guid guid, VFXID id, Vector2 position)
    {
        WoundBodyArt.SeeView(__instance);
        try { BanditTint.OnVfx(__instance, guid, (int)id, position); }
        catch (Exception ex) { DelversHost.Warning("CORE_SEED_TINT_FAILED vfx: " + ex.Message); }
    }
}
