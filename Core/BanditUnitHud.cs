using HarmonyLib;
using UnityEngine;
#if BEPINEX
using UnitEntityHUD = global::Refactor.View.UnitEntityHUD;
#else
using UnitEntityHUD = Il2CppRefactor.View.UnitEntityHUD;
#endif

namespace DungeonSettlersDelvers.Core;

// The game hides the bar and name above a unit
// whose table traits hold AFFECTER_BossMonster (UnitEntityHUD.Init: _upperPivot.SetActive(false)),
// and Core gives the forest bandits that marker for the boss HUD (CoreUnitRules.BossTrait). After Init, Core takes the game's
// other branch for its own units only: _upperPivot active, so the game's own head and body bars and name show and fill as for any
// mob. The boss HUD keeps working, it reads the running effect (CombatStateApplier.IsBossUnit).
// The boss branch also loads SelectionMarker_Huge into _groundHalo and lowers _lowerPivot by
// 0.170625 to -0.53 (the ring; the lowering and the height -0.359375); the normal branch sets
// no sprite, so the prefab's own ring stays, and _lowerPivot's height is -0.359375. Core keeps the ring the HUD had before Init and
// puts the pivot at the normal height.
[HarmonyPatch(typeof(UnitEntityHUD), nameof(UnitEntityHUD.Init))]
internal static class BanditUnitHudInitPatch
{
    private static bool logged;

    private static void Prefix(UnitEntityHUD __instance, IEntity entity, out Sprite __state)
    {
        __state = null;
        try
        {
            if (__instance == null || entity == null || !CoreUnitRules.IsCoreUnit(entity.Key)) return;
            var ring = __instance._groundHalo != null ? __instance._groundHalo.sprite : null;
            // A pooled HUD that held a boss before still carries the large ring; then there is no normal one to keep.
            if (ring != null && ring.name != BanditBossBarRules.HugeRing) __state = ring;
        }
        catch (Exception ex) { ApocalypseHooks.Report("bandit-hud", ex); }
    }

    private static void Postfix(UnitEntityHUD __instance, IEntity entity, Sprite __state)
    {
        try
        {
            if (__instance == null || entity == null || !CoreUnitRules.IsCoreUnit(entity.Key)) return;
            var pivot = __instance._upperPivot;
            if (pivot != null && !pivot.gameObject.activeSelf) pivot.gameObject.SetActive(true);
            var halo = __instance._groundHalo;
            var ring = "-";
            if (halo != null && __state != null && halo.sprite != __state)
            {
                halo.sprite = __state;
                ring = __state.name;
            }
            var lower = __instance._lowerPivot;
            if (lower != null)
            {
                var at = lower.localPosition;
                lower.localPosition = new Vector3(at.x, BanditBossBarRules.NormalLowerY, at.z);
            }
            if (!logged)
            {
                logged = true;
                DelversHost.Info("CORE_BANDIT_HUD at=init unit=" + entity.Key + " upper=on ring=" + ring + (__state == null ? " kept=none" : ""));
            }
        }
        catch (Exception ex) { ApocalypseHooks.Report("bandit-hud", ex); }
    }
}
