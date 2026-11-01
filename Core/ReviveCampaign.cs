using System.Collections;
using UnityEngine;

namespace DungeonSettlersDelvers.Core;

// Ties the revive parts to the campaign's life: reset when a campaign is bound, snapshots after
// every load and before every save, and a once-a-second clock while a campaign runs (near-death
// snapshots). The clock waits for the campaign to start and ends with it; nothing runs on the
// title screen.
internal static class ReviveCampaign
{
    private const float StartTimeoutSeconds = 300f;
    private const float StopAfterSeconds = 60f;
    private static bool clockRunning;
    private static bool reported;

    internal static void ResetLifecycle()
    {
        ReviveService.ResetLifecycle();
        ReviveSnapshots.ResetLifecycle();
        ReviveSicknessBars.ResetLifecycle();
        ReviveSicknessHudPatch.ResetScene();
        WoundBars.ResetLifecycle();
        CoreGame.ResetLifecycle();
        reported = false;
    }

    // A load or a new campaign binds Core's save data.
    internal static void OnCampaignBound()
    {
        // The old campaign's open equipment departures are settled and summed before anything starts over.
        EquipmentMarker.OnCampaignBound();
        BanditRoomFeatureView.ResetCampaign();
        KitDrops.ResetCampaign();
        BossEffects.ResetCampaign();
        ReviveService.ResetCampaign();
        ReviveSnapshots.ResetCampaign();
        ReviveSickness.ResetCampaign();
        ReviveSicknessBars.ResetCampaign();
        ReviveSicknessHudPatch.ResetScene();
        Wounds.ResetCampaign();
    }

    // The game has finished loading a save: the units exist. The sickness comes back first, so
    // the snapshot afterwards already sees it.
    internal static void OnCampaignLoaded()
    {
        Guard("restore", ReviveSickness.Restore);
        ApocalypseHooks.Run("wound-load", Wounds.OnLoaded);
        Guard("lifebloom-load", Lifebloom.OnLoaded);
        Guard("boss-effects", BossItems.RestoreEffects);
        Guard("bandit-difficulty", BanditDifficulty.RestoreLoaded);
        Guard("formation-sleep", TacticalFormationSleep.OnLoaded);
        Guard("loaded", () => { ReviveSnapshots.TakeAll("load"); });
        Guard("price", ReviveScroll.ApplyPrice);
        ApocalypseHooks.Run("wound-bandage-merchants", BandageContent.ApplyMerchantChances);
        StartClock();
    }

    internal static void OnNewCampaign()
    {
        Guard("price", ReviveScroll.ApplyPrice);
        ApocalypseHooks.Run("wound-bandage-merchants", BandageContent.ApplyMerchantChances);
        StartClock();
    }

    // Inside the game's own save, just before Core's block is written. Autosaves run at every map
    // change, so this also makes sure the clock runs after one.
    internal static void BeforeSave()
    {
        Guard("save", () => { ReviveSnapshots.TakeAll("save"); });
        Guard("sickness-save", ReviveSickness.SyncToSave);
        Guard("lifebloom-save", Lifebloom.SyncToSave);
        StartClock();
    }

    private static void StartClock()
    {
        if (clockRunning) return;
        clockRunning = true;
        try { DelversHost.StartCoroutine(Clock()); }
        catch (Exception ex)
        {
            clockRunning = false;
            Report("clock-start", ex);
        }
    }

    // Checks once a second. A map change has short moments without a running campaign (MainFlow
    // missing or IsCampaignStarted false); the clock outlasts them and ends only after a minute
    // without a campaign (it ended at the first trip into the dungeon).
    private static IEnumerator Clock()
    {
        var idleSince = Time.unscaledTime;
        var started = false;
        var next = 0f;
        try
        {
            while (true)
            {
                yield return null;
                if (Time.unscaledTime < next) continue;
                next = Time.unscaledTime + 1f;
                var flow = CoreGame.Flow;
                if (flow == null || !flow.IsCampaignStarted)
                {
                    var limit = started ? StopAfterSeconds : StartTimeoutSeconds;
                    if (Time.unscaledTime - idleSince > limit) yield break;
                    continue;
                }
                started = true;
                idleSince = Time.unscaledTime;
                if (!CoreGame.CampaignReady) continue;
                Guard("near-death", ReviveSnapshots.CheckNearDeath);
                Guard("sickness", ReviveSickness.Tick);
                ApocalypseHooks.Run("wound-tick", Wounds.Tick);
                Guard("wound-tooltip", WoundTooltip.Refresh);
                ApocalypseHooks.Run("lifebloom-tick", Lifebloom.Tick);
                Guard("hints", FirstHints.Tick);
                Guard("wound-probe", WoundProbe.Clock);
            }
        }
        finally { clockRunning = false; }
    }

    private static void Guard(string stage, Action action)
    {
        try { action(); }
        catch (Exception ex) { Report(stage, ex); }
    }

    private static void Report(string stage, Exception ex)
    {
        if (reported) return;
        reported = true;
        DelversHost.Error("CORE_REVIVE_CAMPAIGN_FAILED stage=" + stage + ": " + ex);
    }
}
