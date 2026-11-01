using HarmonyLib;

namespace DungeonSettlersDelvers.Core;

// The hooks for the patches Core puts on by hand (LatePatchGate, trap list row 10): the end of the boot (MainFlow.InitCore,
// Core is ready before the first scene), the start of a load (CampaignDataContainer.Deserialize, LoadIntegrityGuard) and of a
// campaign (MainFlow.OnCampaignEnter). All three come before the first hit; each patch is put on at most once per session.
internal static class LatePatches
{
    internal static void TryAll(string at)
    {
        FalconCritInstall.TryInstall(at);
        TargetSortGuard.TryInstall(at);
        BanditCampFootprintInstall.TryInstall(at);
        SummonLimit.TryInstall(at);
    }
}

[HarmonyPatch(typeof(MainFlow), nameof(MainFlow.InitCore))]
internal static class LatePatchesAfterBootPatch
{
    private static void Postfix() => LatePatches.TryAll("boot");
}

[HarmonyPatch(typeof(MainFlow), nameof(MainFlow.OnCampaignEnter))]
internal static class LatePatchesCampaignEnterPatch
{
    private static void Postfix() => LatePatches.TryAll("campaign");
}
