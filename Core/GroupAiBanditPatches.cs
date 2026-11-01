using HarmonyLib;
#if BEPINEX
using IExecutionData = global::Refactor.Combat.IExecutionData;
#else
using IExecutionData = Il2CppRefactor.Combat.IExecutionData;
#endif

namespace DungeonSettlersDelvers.Core;

// After the game processed an execution: a landed hit on the bandits' healer is kept for her
// protection (GroupAiBandits.OnDamage). Arguments bound by name.
[HarmonyPatch(typeof(TickEvent), "CallOnAfterProcess")]
internal static class GroupAiBanditDamagePatch
{
    private static void Postfix(IExecutionData data)
    {
        try { GroupAiBandits.OnDamage(data); }
        catch (Exception ex) { ApocalypseHooks.Report("bandit-healer-damage", ex); }
        // B: hits on party units, for the vines' threat (GroupAiPartyHits).
        try { GroupAiPartyHits.OnDamage(data); }
        catch (Exception ex) { ApocalypseHooks.Report("party-hits", ex); }
    }
}

// Every game tick: the bandits' taunts start and end on time (GroupAiBandits.OnGameTick), and the group AI's move
// orders are followed to their end (GroupAiMoveWatch.OnGameTick).
// TickFlowHandler.Tick has its own address and one caller (TickFlow.Tick); without bandits that decided
// and without open move orders both return at once.
[HarmonyPatch(typeof(TickFlowHandler), nameof(TickFlowHandler.Tick))]
internal static class GroupAiBanditTickPatch
{
    private static int failures;

    private static void Postfix()
    {
        try { GroupAiBandits.OnGameTick(); }
        catch (Exception ex)
        {
            if (++failures <= 3) DelversHost.Warning("CORE_AI_BANDIT_TICK_FAILED: " + ex.Message);
        }
        try { GroupAiMoveWatch.OnGameTick(); }
        catch (Exception ex)
        {
            if (++failures <= 3) DelversHost.Warning("CORE_AI_MOVE_WATCH_FAILED: " + ex.Message);
        }
        // The tactical formation in the dungeon (GroupAiFormation); without a saved formation it returns at once.
        try { GroupAiFormation.OnGameTick(); }
        catch (Exception ex)
        {
            if (++failures <= 3) DelversHost.Warning("CORE_AI_FORMATION_FAILED: " + ex.Message);
        }
        // The party's aftercare once the fight is over (GroupAiAftercare); without an open wound it returns at once.
        try { GroupAiAftercare.OnGameTick(); }
        catch (Exception ex)
        {
            if (++failures <= 3) DelversHost.Warning("CORE_AI_AFTERCARE_FAILED: " + ex.Message);
        }
        // A unit that walked back into an enemy area it dodged goes out again (GroupAiZones.WatchReturns).
        try { GroupAiAreaDodge.OnGameTick(); }
        catch (Exception ex)
        {
            if (++failures <= 3) DelversHost.Warning("CORE_AI_ZONE_WATCH_FAILED: " + ex.Message);
        }
        // Lifebloom: a charge that came back shows in the skill slot.
        try { LifebloomSlotIcon.OnGameTick(); }
        catch (Exception ex)
        {
            if (++failures <= 3) DelversHost.Warning("CORE_LIFEBLOOM_ICON_TICK_FAILED: " + ex.Message);
        }
        // Lifebloom: a buff whose time is up blooms on this tick, not with the next second.
        try { Lifebloom.OnGameTick(); }
        catch (Exception ex)
        {
            if (++failures <= 3) DelversHost.Warning("CORE_LIFEBLOOM_BLOOM_TICK_FAILED: " + ex.Message);
        }
        // The countdown to the next Lifebloom charge in the skill slot.
        try { LifebloomSlotCooldown.OnGameTick(); }
        catch (Exception ex)
        {
            if (++failures <= 3) DelversHost.Warning("CORE_LIFEBLOOM_CD_TICK_FAILED: " + ex.Message);
        }
        // Equipment departures due after a full tick are settled (EquipmentMarker catches its own failures).
        EquipmentMarker.OnGameTick();
        // The bandit room's waterfall and bridge: drawn once per shown map (BanditRoomFeatureView catches its own failures).
        BanditRoomFeatureView.OnGameTick();
        // The bandit camp of a floor just built: furnished once after the arrival (BanditCampFurnish).
        try
        {
            BanditCampFurnish.OnGameTick();
            BanditCampWake.OnGameTick();
            BanditGroupWake.FlushDelayed();
            CampGuardianStones.OnGameTick();
        }
        catch (Exception ex)
        {
            if (++failures <= 3) DelversHost.Warning("CORE_BANDITS_CAMP_FURNISH_FAILED: " + ex.Message);
        }
        // The author's body blood: the frame of each overlay.
        try { WoundBodyArt.OnGameTick(); }
        catch (Exception ex)
        {
            if (++failures <= 3) DelversHost.Warning("CORE_WOUND_FX_BODYART_TICK_FAILED: " + ex.Message);
        }
        // The boss parts' Blood Warden: shields given and followed to their end (BossEffects.OnGameTick).
        try { BossEffects.OnGameTick(); }
        catch (Exception ex)
        {
            if (++failures <= 3) DelversHost.Warning("CORE_BOSS_EFFECT_TICK_FAILED: " + ex.Message);
        }
        // The wounds hit or changed since the last tick: lethal check and effect count (Wounds.OnGameTick).
        try { Wounds.OnGameTick(); }
        catch (Exception ex)
        {
            if (++failures <= 3) DelversHost.Warning("CORE_WOUND_GAME_TICK_FAILED: " + ex.Message);
        }
        // C: a bandage order of Core's that put no bandage on is logged as lost.
        try { GroupAiBandageUse.OnGameTick(); }
        catch (Exception ex)
        {
            if (++failures <= 3) DelversHost.Warning("CORE_AI_BANDAGE_TICK_FAILED: " + ex.Message);
        }
        // A player's order on the party healer starts her quiet time.
        try { GroupAiStance.OnGameTick(); }
        catch (Exception ex)
        {
            if (++failures <= 3) DelversHost.Warning("CORE_AI_STANCE_TICK_FAILED: " + ex.Message);
        }
    }
}