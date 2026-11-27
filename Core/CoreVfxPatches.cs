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
// body blood (WoundBodyArt.SeeView), a new effect is handed to the bandits' tint (BanditTint.OnVfx), and the game's two bleeding
// pictures are noted for the one source of blood on the body (WoundFx.NoteGameVfx). The first two stay in the order they had;
// the tint keeps its own catch and its log text.
[HarmonyPatch(typeof(VFXView), nameof(VFXView.CreateVFX))]
internal static class CoreVfxPatches
{
    private static int failures;

    private static void Postfix(VFXView __instance, Il2CppSystem.Guid guid, VFXID id, Vector2 position)
    {
        WoundBodyArt.SeeView(__instance);
        try { BanditTint.OnVfx(__instance, guid, (int)id, position); }
        catch (Exception ex) { DelversHost.Warning("CORE_SEED_TINT_FAILED vfx: " + ex.Message); }
        try { WoundFx.NoteGameVfx(guid, (int)id); }
        catch (Exception ex) { if (++failures <= 3) DelversHost.Warning("CORE_WOUND_FX_GAME_FAILED note: " + ex.Message); }
    }
}

// The game binds an effect to its owner (VFXView.SetVFXOwner): its bleeding pictures on a party member are hidden while Core's
// body blood shows (WoundFx.HideGameBlood).
[HarmonyPatch(typeof(VFXView), nameof(VFXView.SetVFXOwner))]
internal static class WoundGameBloodOwnerPatch
{
    private static int failures;

    private static void Postfix(VFXView __instance, Il2CppSystem.Guid guid, IEntity owner)
    {
        try { WoundFx.HideGameBlood(__instance, guid, owner); }
        catch (Exception ex) { if (++failures <= 3) DelversHost.Warning("CORE_WOUND_FX_GAME_FAILED owner: " + ex.Message); }
    }
}

// The game stops an effect (VFXView.StopVFX): a hidden bleeding picture gets its renderers back before it returns to the game's pool
// (WoundFx.OnGameStop).
[HarmonyPatch(typeof(VFXView), nameof(VFXView.StopVFX))]
internal static class WoundGameBloodStopPatch
{
    private static int failures;

    private static void Prefix(VFXView __instance, Il2CppSystem.Guid guid)
    {
        try { WoundFx.OnGameStop(__instance, guid); }
        catch (Exception ex) { if (++failures <= 3) DelversHost.Warning("CORE_WOUND_FX_GAME_FAILED stop: " + ex.Message); }
    }
}
