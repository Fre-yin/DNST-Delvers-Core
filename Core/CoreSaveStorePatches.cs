using HarmonyLib;
using UnityEngine;

namespace DungeonSettlersDelvers.Core;

// The game side of CoreSaveStore. Every hook passes strings or reads a few fields; none has a
// ref or out parameter of the game (lesson 13 of the addon guide). Each catches its own failures.

internal static class CoreSavePaths
{
    // Core's own file per campaign: <persistentDataPath>\DungeonSettlersDelvers\Core\campaigns.
    internal static void EnsureArchive()
        => CoreSaveStore.UseArchive(Path.Combine(Application.persistentDataPath, "DungeonSettlersDelvers", "Core", "campaigns"));

    // The file the save data belongs to (null when its name is unknown).
    internal static string SavePathOf(CampaignSaveData data)
    {
        var saveName = data.CampaignSaveHeader?.SaveName;
        return string.IsNullOrEmpty(saveName) ? null : SaveLoadHelper.GetCampaignSavePath(saveName);
    }
}

// After DelversSaveKeyRenamePatch (Priority.First): renamed keys inside the block count too. The
// text may change: Core's own items get their key back before the game parses it. Like the
// rename patch this replaces a string argument; the game's method has no ref or out parameter.
[HarmonyPatch(typeof(SaveLoadHelper), nameof(SaveLoadHelper.DeserializeCampaignSaveDataSafely))]
internal static class CoreSaveDataReadPatch
{
    private static void Prefix(ref string json, string saveFilePath)
    {
        try { json = CoreSaveStore.Remember(json, saveFilePath); }
        catch (Exception ex) { CoreSaveStore.RememberFailed(saveFilePath, ex); }
    }
}

// After DelversCampaignDeserializeGuardPatch (Priority.First), which has begun the load, and before
// ApocalypseLoadPatch (same method, normal priority), which reads Core's data. Only the load
// routine calls this method, never a new campaign. A block that cannot be bound fails the load.
[HarmonyPatch(typeof(CampaignDataContainer), nameof(CampaignDataContainer.Deserialize),
    new[] { typeof(CampaignSaveData) })]
internal static class CoreSaveDataLoadPatch
{
    [HarmonyPriority(Priority.High)]
    private static void Prefix(CampaignSaveData data)
    {
        if (data == null) return;
        var error = TryBind(data);
        if (error != null)
        {
            CoreSaveStore.BindFailed(error);
            LoadIntegrityGuard.ReportFailure(LoadFailureSources.CoreBind, error);
            return;
        }
        try { ReviveCampaign.OnCampaignBound(); }
        catch (Exception ex) { CoreSaveStore.Report("load", ex); }
    }

    // null when the block is bound; else why not. An empty GUID is a failure too: the throwaway
    // campaign of the load's start would stay Current.
    private static Exception TryBind(CampaignSaveData data)
    {
        try
        {
            var campaign = Guid.TryParse(data.ClanSaveData.CampaignGuid.ToString(), out var guid) ? guid : Guid.Empty;
            if (campaign == Guid.Empty) return new InvalidOperationException("The save names no campaign GUID.");
            var path = CoreSavePaths.SavePathOf(data);
            CoreSavePaths.EnsureArchive();
            CoreSaveStore.BindLoaded(campaign, path);
            return null;
        }
        catch (Exception ex) { return ex; }
    }
}

// One level below WriteJsonFileSafely on purpose: if the original fails, the game's own read-back
// of the temporary file still sees it (research).
[HarmonyPatch(typeof(SaveLoadHelper), nameof(SaveLoadHelper.WriteUtf8NoBomText))]
internal static class CoreSaveDataWritePatch
{
    private static void Prefix(string path, ref string contents)
    {
        try { contents = CoreSaveStore.Decorate(path, contents); }
        catch (Exception ex) { CoreSaveStore.Report("write", ex); }
    }
}

// Marks the game's own campaign saves (manual, autosave, iron mode, quit); anything written
// outside one is a rewrite of a file on disk.
[HarmonyPatch(typeof(CampaignSaveLoader), nameof(CampaignSaveLoader.Save),
    new[] { typeof(string), typeof(bool), typeof(string) })]
internal static class CoreSaveDataScopePatch
{
    private static void Prefix()
    {
        CoreSaveStore.SaveStarted();
    }

    private static Exception Finalizer(Exception __exception)
    {
        CoreSaveStore.SaveEnded();
        return __exception;
    }
}
