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
    private static void Prefix(string __0, string __1, LogType __2)
    {
        if (!LoadIntegrityGuard.IsLoadInProgress
            || !LoadIntegritySignalRules.IsDeserializationFailure(__0,
                __2 is LogType.Error or LogType.Exception or LogType.Assert)) return;

        var details = string.IsNullOrWhiteSpace(__1) ? __0 : __0 + Environment.NewLine + __1;
        LoadIntegrityGuard.ReportFailure("native-deserialization-log",
            new InvalidOperationException("Native load diagnostic (" + __2 + "): " + details));
    }
}
