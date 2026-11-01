using HarmonyLib;
#if BEPINEX
using CombatStateApplier = global::Refactor.Tick.DataApply.CombatStateApplier;
#else
using CombatStateApplier = Il2CppRefactor.Tick.DataApply.CombatStateApplier;
#endif

namespace DungeonSettlersDelvers.Core;

// Apocalypse: enemies give no time to prepare. Noticing a hostile
// normally puts an enemy into Awaring with AFFECTER_Surprised for SystemVar.Combat
// .SurprisedDuration (4 s); it fights only once that runs out. On a combat Apocalypse campaign
// the switch to Awaring becomes InCombat at once; the game then removes Unawared and Surprised,
// adds Awared and pulls the whole room itself (DungeonBehaviorSimulator.OnCombatStart).
// The rule also runs on other campaigns as the option "instant-combat" (ApocalypseRules); Core's
// own enemies (CoreUnits) always skip the window, on every campaign (never surprised, so nobody grabs the
// wagon and runs before the normal aggro would come).
internal static class ApocalypseAggro
{
    private static bool reported;
    private static bool unitReported;

    // Enemies that skipped the window since the campaign was bound; read by the self-test.
    internal static int Sharpened { get; private set; }

    internal static void ResetCampaign()
    {
        Sharpened = 0;
        reported = false;
        unitReported = false;
    }

    internal static void Sharpen(CombatStateApplyData data)
    {
        // State changes are rare compared to ticks, but the cheap flag check comes first.
        var instant = ApocalypseRules.InstantCombat;
        if ((!instant && !CoreUnits.Active) || data == null) return;
        try
        {
            if (data.State != CombatState.Awaring) return;
            var unit = data.Target?.TryCast<UnitEntity>();
            if (unit == null || unit.Faction != FactionType.Enemy) return;
            var coreUnit = CoreUnitRules.IsCoreUnit(unit.Key);
            if (!instant && !coreUnit) return;
            // The property's setter is private and shares its body with other setters, so the
            // field is written directly (never hook either of them).
            data._State_k__BackingField = CombatState.InCombat;
            Sharpened++;
            if (coreUnit && !unitReported)
            {
                unitReported = true;
                DelversHost.Info("CORE_UNIT_AGGRO first Core unit skipped the surprise window state=Awaring->InCombat unit="
                    + unit.Key + " rule=" + (instant ? "1" : "0"));
            }
            if (instant && !reported)
            {
                reported = true;
                FirstHints.Request(FirstHintText.InstantCombat);
                DelversHost.Info("CORE_APOCALYPSE_AGGRO first enemy skipped the surprise window state=Awaring->InCombat unit=" + unit.Key);
            }
        }
        catch (Exception ex) { ApocalypseHooks.Report("aggro", ex); }
    }
}

// Protected in the game; patched by name. Exceptions inside a prefix only reach the loader log
// (Il2CppInterop trampoline), so Sharpen catches everything itself.
[HarmonyPatch(typeof(CombatStateApplier), "Apply",
    new[] { typeof(CombatStateApplyData), typeof(Il2CppSystem.Collections.Generic.List<IApplyData>) })]
internal static class ApocalypseAggroPatch
{
    private static void Prefix(CombatStateApplyData data) => ApocalypseAggro.Sharpen(data);
}
