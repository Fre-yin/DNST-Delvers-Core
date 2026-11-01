using HarmonyLib;
using UnityEngine;

namespace DungeonSettlersDelvers.Core;

// Observation-only finalizers cover native load boundaries that Core does not
// otherwise change. This includes Extended Hotbar's QuickSlot patches. Every
// exception is returned unchanged, preserving the game's and other mods'
// behavior while the guard prevents a partial load from being saved.
[HarmonyPatch(typeof(EntityLoadHelper), nameof(EntityLoadHelper.LoadUnit),
    new[] { typeof(UnitEntity), typeof(Vector2Int), typeof(MapType) })]
internal static class DelversLoadedUnitIntegrityPatch
{
    private static Exception Finalizer(Exception __exception)
        => LoadIntegrityGuard.ObserveFinalizer(__exception, "EntityLoadHelper.LoadUnit");
}

[HarmonyPatch(typeof(PlayerUnitDataContainer), nameof(PlayerUnitDataContainer.Deserialize),
    new[] { typeof(PlayerUnitsSaveData) })]
internal static class DelversPlayerUnitDataIntegrityPatch
{
    private static Exception Finalizer(Exception __exception)
        => LoadIntegrityGuard.ObserveFinalizer(__exception, "PlayerUnitDataContainer.Deserialize");
}

[HarmonyPatch(typeof(ClanDataContainer), nameof(ClanDataContainer.Deserialize),
    new[] { typeof(ClanSaveData) })]
internal static class DelversClanDataIntegrityPatch
{
    private static Exception Finalizer(Exception __exception)
        => LoadIntegrityGuard.ObserveFinalizer(__exception, "ClanDataContainer.Deserialize");
}

[HarmonyPatch(typeof(UnitQuickSlotContainer), nameof(UnitQuickSlotContainer.Deserialize))]
internal static class DelversQuickSlotContainerIntegrityPatch
{
    private static Exception Finalizer(Exception __exception)
        => LoadIntegrityGuard.ObserveFinalizer(__exception, "UnitQuickSlotContainer.Deserialize");
}

[HarmonyPatch(typeof(QuickSlotData), nameof(QuickSlotData.Deserialize),
    new[] { typeof(QuickSlotSaveData) })]
internal static class DelversQuickSlotDataIntegrityPatch
{
    private static Exception Finalizer(Exception __exception)
        => LoadIntegrityGuard.ObserveFinalizer(__exception, "QuickSlotData.Deserialize");
}

// FileLogger receives the same Unity errors that the game persists to
// ErrorLog.txt. This catches the native component and section failures that the
// game deliberately handles instead of rethrowing through a Harmony boundary.
[HarmonyPatch(typeof(GameFileLogger), "OnLogReceived",
    new[] { typeof(string), typeof(string), typeof(LogType) })]
internal static class DelversNativeLoadErrorSignalPatch
{
    private static bool checkFailureLogged;

    // Never throws: an exception here would drop the game's own ErrorLog line, and logging it
    // through Unity could re-enter this hook. If an error line during a load cannot be
    // classified, it counts as a load failure. A restart is recoverable, a partly loaded
    // campaign that gets saved is not (review).
    private static void Prefix(string condition, string stackTrace, LogType type)
    {
        var isError = type is LogType.Error or LogType.Exception or LogType.Assert;
        try
        {
            if (!LoadIntegrityGuard.IsLoadInProgress
                || !LoadIntegritySignalRules.IsDeserializationFailure(condition, isError)) return;

            var details = string.IsNullOrWhiteSpace(stackTrace) ? condition : condition + Environment.NewLine + stackTrace;
            LoadIntegrityGuard.ReportFailure("native-deserialization-log",
                new InvalidOperationException("Native load diagnostic (" + type + "): " + details));
        }
        catch (Exception ex)
        {
            try
            {
                if (!checkFailureLogged)
                {
                    checkFailureLogged = true;
                    DelversHost.Error("CORE_LOAD_SIGNAL_CHECK_FAILED: " + ex);
                }
                if (isError && LoadIntegrityGuard.IsLoadInProgress)
                    LoadIntegrityGuard.ReportFailure("native-log-check-failed", ex);
            }
            catch { /* The native logger must keep running. */ }
        }
    }
}
