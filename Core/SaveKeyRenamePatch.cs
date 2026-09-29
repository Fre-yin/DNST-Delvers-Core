using HarmonyLib;

namespace DungeonSettlersDelvers.Core;

// Every campaign save passes this method as text before the game parses it (loading and the
// title-screen preview). Renaming here covers all fields and units at once. Without a
// registered old key in the text the cost is a few string searches.
[HarmonyPatch(typeof(SaveLoadHelper), nameof(SaveLoadHelper.DeserializeCampaignSaveDataSafely))]
internal static class DelversSaveKeyRenamePatch
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(ref string __0)
    {
        try
        {
            var renamed = DelversCoreRuntime.SaveKeyRenames.Apply(__0, out var tokens, out var packs);
            if (tokens == 0) return;
            __0 = renamed;
            DelversHost.Info("CORE_SAVE_KEYS_RENAMED packs=" + string.Join(",", packs) + " tokens=" + tokens);
        }
        catch (Exception ex)
        {
            // Leave the text untouched: the load then runs as if the pack were missing and
            // Core's load guard blocks saving instead of losing data.
            DelversHost.Error("CORE_SAVE_KEY_RENAME_FAILED: " + ex.GetType().Name + ": " + ex.Message);
        }
    }
}
