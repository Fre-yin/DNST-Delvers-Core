using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine;
#if BEPINEX
using ActionOrder = global::Refactor.Control.ActionOrder;
using Interaction = global::Refactor.Control.Interaction;
using RunCheck = global::Refactor.Control.RunCheck;
#else
using ActionOrder = Il2CppRefactor.Control.ActionOrder;
using Interaction = Il2CppRefactor.Control.Interaction;
using RunCheck = Il2CppRefactor.Control.RunCheck;
#endif

namespace DungeonSettlersDelvers.Core;

// CorpseRules.BurialWarns in the game. A burial order reaches the game two ways, and both end in DirectTaskHelper.OrderPersonal
// or OrderSharedTask: an interaction through ActionOrder.Execute (right click), and the event DirectTaskRequested
// (EntityEventHandler.OnEvent; the action menu, since the ClearDeadUnit entry is a DirectTask input).
// For ClearDeadUnit / ClearDeadBody while the user carries a dead party member that still wears equipment, Core holds the
// order and asks with the game's yes/no dialog (CommonNotice_ShowYesNo: CommonNoticePresenter.ShowNotice_YesNo opens
// CommonNoticeView.Show_Select with both callbacks and yesNo true; CommonNotice_ShowConfirm opens Show_Confirm, which has one
// button and drops the cancel callback): yes runs the same order again when it still holds
// (CorpseRules.BurialStillValid), no drops it. A held order reports true to the game's runner, which otherwise falls back to
// another entry (at the base portal: "Portal"); an error in this check holds the order the same way. Only one question at a
// time: a second order while one is open is dropped (result=busy). Every line names its way (execute or task). With the setting off, or
// without the campaign's UI, the order runs as before.
internal static class CorpseBurialWarning
{
    private static bool replaying;
    private static bool questionOpen;
    private static double questionAt;
    private static string questionWay;

    private static string CampaignOf() => CoreGame.Flow?._campaignData?.ClanDataContainer?.CampaignGuid.ToString();

    private static bool OnMap(IEntity target)
        => target != null && CoreGame.InMap?.GetEntity(target.Guid) != null;

    internal static bool Replaying => replaying;

    // Every new or loaded campaign (CorpseKeep.ResetCampaign from the campaign rules): a question left open by a title trip or a
    // load has no answer coming (burials stayed blocked for 124 s after title and load, 11x busy), so it is dropped.
    internal static void ResetCampaign()
    {
        if (questionOpen) DelversHost.Info(CorpseRules.BurialLine("dropped", "-", 0, questionWay ?? "-") + " reason=campaign");
        questionOpen = false;
        questionAt = 0;
    }

    // The unit the user carries now, or null.
    private static UnitEntity Carried(UnitEntity user)
    {
        var carry = CoreGame.Component<CarryComponent>(user.Cast<IEntity>());
        return carry != null && Guid.TryParse(carry.GetCarriedGuid().ToString(), out var guid) && guid != Guid.Empty ? CoreGame.FindUnit(guid) : null;
    }

    // True when the order may run now; false when Core holds it for the dialog. Right click: ActionOrder.Execute.
    internal static bool Allow(ActionOrder order, Interaction data, UnitEntity user, RunCheck check)
    {
        if (replaying || order == null || user == null) return true;
        // The cheap tests first: the switch and the interaction, only then the search for the carried unit.
        if (!CoreSettings.BurialWarning || (int)data.InteractionType != CorpseRules.ClearDeadUnitType) return true;
        var target = data.TargetEntity;
        return Ask(user, () => OnMap(target), () => order.Execute(data, user, check), "execute");
    }

    // The same for the action menu: EntityEventHandler.OnEvent(DirectTaskRequested) with TaskType.ClearDeadBody.
    internal static bool AllowTask(EntityEventHandler handler, DirectTaskRequested msg)
    {
        if (replaying || handler == null || msg == null) return true;
        if (!CoreSettings.BurialWarning || (int)msg.TaskType != CorpseRules.ClearDeadBodyTask) return true;
        var user = CoreGame.FindUnit(Guid.Parse(msg.UserGuid.ToString()));
        if (user == null) return true;
        var map = msg.Map;
        var targetGuid = msg.TargetGuid;
        return Ask(user, () => OnMap(map, targetGuid), () => handler.OnEvent(msg), "task");
    }

    private static bool OnMap(MapType map, Il2CppSystem.Guid guid)
    {
        var maps = CoreGame.Flow?._inMapDatas;
        var inMap = maps != null && maps.ContainsKey(map) ? maps[map] : null;
        return inMap?.GetEntity(guid) != null;
    }

    // The question for a dressed party corpse the user carries; runAgain gives the same order to the game again on yes.
    private static bool Ask(UnitEntity user, Func<bool> targetThere, Action runAgain, string way)
    {
        var carried = Carried(user);
        var party = carried != null && carried.IsDead && carried.Faction == FactionType.Player;
        var equipped = party ? CorpseKeep.EquippedKeys(carried.Cast<IEntity>()).Count : 0;
        if (!CorpseRules.BurialWarns(true, CorpseRules.ClearDeadUnitType, party, equipped)) return true;
        var dispatcher = UnityEngine.Object.FindObjectOfType<MainFlow>()?._viewDispatcher;
        var corpse = carried.Guid.ToString();
        if (dispatcher == null)
        {
            DelversHost.Info(CorpseRules.BurialLine("no-ui", corpse, equipped, way));
            return true;
        }
        if (CorpseRules.BurialBusy(questionOpen, questionAt, UnityEngine.Time.unscaledTime))
        {
            DelversHost.Info(CorpseRules.BurialLine("busy", corpse, equipped, way));
            return false;
        }
        UniqueCandidateLocalization.EnsureCurrent();
        var corpseGuid = carried.Guid;
        var campaign = CampaignOf();
        var confirm = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(new Action(() =>
        {
            questionOpen = false;
            try { Confirmed(); }
            catch (Exception ex) { DelversHost.Warning(CorpseRules.BurialLine("error", corpse, equipped, way) + " answer=yes: " + ex.Message); }
        }));
        void Confirmed()
        {
            // The answer may come later: the order runs only while the same user still carries the same dressed
            // corpse; otherwise it is dropped and the player gives it again.
            var now = user.IsDead ? null : Carried(user);
            var still = CorpseRules.BurialStillValid(campaign != null && CampaignOf() == campaign, targetThere(),
                now != null && now.Guid == corpseGuid, now != null && now.IsDead, now != null ? CorpseKeep.EquippedKeys(now.Cast<IEntity>()).Count : 0);
            if (!still)
            {
                DelversHost.Info(CorpseRules.BurialLine("stale", corpse, equipped, way));
                return;
            }
            DelversHost.Info(CorpseRules.BurialLine("confirmed", corpse, equipped, way));
            replaying = true;
            try { runAgain(); }
            catch (Exception ex) { DelversHost.Warning("CORE_CORPSE_BURIAL_WARNING_FAILED replay way=" + way + ": " + ex.Message); }
            finally { replaying = false; }
        }
        var cancel = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(new Action(() =>
        {
            questionOpen = false;
            try { DelversHost.Info(CorpseRules.BurialLine("cancelled", corpse, equipped, way)); }
            catch (Exception ex) { DelversHost.Warning("CORE_CORPSE_BURIAL_WARNING_FAILED answer=no way=" + way + ": " + ex.Message); }
        }));
        dispatcher.DispatchViewUpdateImmediate(new ViewCallbackData(UpdateType.CommonNotice_ShowYesNo,
            ReviveText.BurialNoticeKey, confirm, cancel).Cast<IViewUpdateData>());
        questionOpen = true;
        questionAt = UnityEngine.Time.unscaledTime;
        questionWay = way;
        DelversHost.Info(CorpseRules.BurialLine("shown", corpse, equipped, way));
        return false;
    }
}

[HarmonyPatch(typeof(ActionOrder), nameof(ActionOrder.Execute))]
internal static class CorpseBurialWarningPatch
{
    private static bool Prefix(ActionOrder __instance, Interaction data, UnitEntity unit, RunCheck check, ref bool __result)
    {
        try
        {
            if (CorpseBurialWarning.Allow(__instance, data, unit, check)) return true;
            // Held for the question: the order counts as taken, so the runner does not fall back to another entry.
            __result = true;
            return false;
        }
        catch (Exception ex)
        {
            ApocalypseHooks.Report("burial-warning", ex);
            // Never a burial without the question: an error holds a ClearDeadUnit (the player gives it again), and the runner
            // hears true as for a held order, so it does not fall back to another entry.
            var clear = false;
            try { clear = !CorpseBurialWarning.Replaying && CoreSettings.BurialWarning && (int)data.InteractionType == CorpseRules.ClearDeadUnitType; } catch { }
            if (!clear) return true;
            DelversHost.Warning(CorpseRules.BurialLine("error", "-", 0, "execute") + ": " + ex.Message);
            __result = true;
            return false;
        }
    }
}

// The action menu's way: the ClearDeadBody task as an event (the entry's input is DirectTask, which does not
// pass ActionOrder.Execute).
[HarmonyPatch(typeof(EntityEventHandler), nameof(EntityEventHandler.OnEvent), new[] { typeof(DirectTaskRequested) })]
internal static class CorpseBurialWarningTaskPatch
{
    private static bool Prefix(EntityEventHandler __instance, DirectTaskRequested msg)
    {
        try { return CorpseBurialWarning.AllowTask(__instance, msg); }
        catch (Exception ex)
        {
            ApocalypseHooks.Report("burial-warning-task", ex);
            var clear = false;
            try { clear = !CorpseBurialWarning.Replaying && CoreSettings.BurialWarning && msg != null && (int)msg.TaskType == CorpseRules.ClearDeadBodyTask; } catch { }
            if (!clear) return true;
            DelversHost.Warning(CorpseRules.BurialLine("error", "-", 0, "task") + ": " + ex.Message);
            return false;
        }
    }
}
