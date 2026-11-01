using System.Collections;
using HarmonyLib;
using UnityEngine;

namespace DungeonSettlersDelvers.Core;

// Apocalypse takes the pause away. On a combat Apocalypse campaign the
// player's pause (Space, the HUD button, '[' down from normal speed) is refused and toggles a
// slow motion instead: half speed, fed by up to 5 seconds that refill at 1 second per 4 seconds
// while the game runs. Values are a first step, to be tuned later. System pauses (loading, map
// changes, event windows, popups) stay: they use AbsolutePause or their own pause reasons.
// The Delvers menu can switch the rule off, and outside Apocalypse switch it on as an option of its own
// (ApocalypseRules).
internal static class ApocalypseTime
{
    internal const float MaxCharge = 5f;
    internal const float RefillPerSecond = 0.25f;
    internal const float SlowMultiplier = 0.5f;
    internal const float MinChargeToStart = 0.5f;

    private static GameSpeedSetting setting;
    private static bool routineRunning;

    internal static float Charge { get; private set; } = MaxCharge;
    internal static bool Slow { get; private set; }

    internal static bool Applies => ApocalypseRules.NoPause;

    // A campaign was loaded or created: full charge, normal speed. The game rebuilds the views
    // itself after a load, so nothing is sent to them here.
    internal static void ResetCampaign()
    {
        Slow = false;
        Charge = MaxCharge;
        setting = null;
        ApocalypseSlowDisplay.ResetCampaign();
    }

    // Prefix on GameSpeedSetting.SetSpeed. True when the request is the player's pause and
    // Apocalypse refuses it; the native method then does not run. Fails open: on any error the
    // game pauses as usual.
    internal static bool RefusePause(GameSpeedSetting instance, GameSpeed speed, SwitchPauseState state)
    {
        if (speed != GameSpeed.Pause || state != SwitchPauseState.RollBack || instance == null) return false;
        try
        {
            if (!Applies) return false;
            // Locked (loading, a time-locked popup): the game refuses on its own.
            if (instance._gameSpeedLockCount > 0) return false;
            // The player's pause is already set: this request lifts it, which stays allowed.
            var reasons = instance._pauseReasons;
            if (reasons != null && reasons.ContainsKey(GamePauseReason.User)) return false;
            setting = instance;
            Toggle();
            // The first refused pause explains itself once (FirstHints).
            FirstHints.Request(FirstHintText.NoPause);
            return true;
        }
        catch (Exception ex)
        {
            ApocalypseHooks.Report("pause", ex);
            return false;
        }
    }

    private static void Toggle()
    {
        if (Slow) SetSlow(false, "toggle");
        else if (Charge >= MinChargeToStart) SetSlow(true, "toggle");
        else
        {
            DelversHost.Info("CORE_APOCALYPSE_SLOW refused charge=" + Charge.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
            ApocalypseSlowDisplay.Denied();
        }
    }

    private static void SetSlow(bool on, string reason)
    {
        if (Slow == on) return;
        Slow = on;
        RefreshViews();
        DelversHost.Info($"CORE_APOCALYPSE_SLOW on={on.ToString().ToLowerInvariant()} reason={reason} charge="
            + Charge.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
        if (on) StartRoutine();
        ApocalypseSlowDisplay.Wake();
    }

    private static void StartRoutine()
    {
        if (routineRunning) return;
        routineRunning = true;
        try { DelversHost.StartCoroutine(Run()); }
        catch
        {
            routineRunning = false;
            Slow = false;
            throw;
        }
    }

    // For tests (DelversCoreRuntime.ApocalypseSlowForTest), only while the rule applies: "press" sends the
    // game's own pause request (the player's way: refused and turned into slow motion or a denial),
    // "charge=<seconds>" sets the charge (it refills from there), "status" only reads.
    internal static string TestCommand(string command)
    {
        if (!Applies) return "error=rule-off";
        command = (command ?? "").Trim().ToLowerInvariant();
        if (command == "press")
        {
            var speed = CoreGame.Flow?._campaignSetting?.GameSpeedSetting;
            if (speed == null) return "error=no-speed-setting";
            speed.SetSpeed(GameSpeed.Pause, SwitchPauseState.RollBack);
        }
        else if (command.StartsWith("charge=", StringComparison.Ordinal)
                 && float.TryParse(command.Substring("charge=".Length), System.Globalization.NumberStyles.Float,
                     System.Globalization.CultureInfo.InvariantCulture, out var seconds))
        {
            Charge = Math.Clamp(seconds, 0f, MaxCharge);
            setting ??= CoreGame.Flow?._campaignSetting?.GameSpeedSetting;
            if (Charge < MaxCharge) StartRoutine();
            ApocalypseSlowDisplay.Wake();
        }
        else if (command != "status") return "error=unknown-command";
        return string.Format(System.Globalization.CultureInfo.InvariantCulture, "slow={0} charge={1:0.00} display={2}",
            Slow ? "true" : "false", Charge, ApocalypseSlowDisplay.ModeText);
    }

    // Animators, effects and tilemaps read the multiplier again on this view message.
    private static void RefreshViews()
    {
        var flow = UnityEngine.Object.FindObjectOfType<MainFlow>();
        var current = setting != null ? setting._gameSpeed : GameSpeed.Normal;
        flow?._viewDispatcher?.AddViewUpdateCall(
            new ViewIntData(UpdateType.OnGameSpeedChange, (int)current).Cast<IViewUpdateData>());
    }

    // Runs only while slowing or refilling; at full charge it ends, so normal play costs nothing.
    private static IEnumerator Run()
    {
        try
        {
            while (true)
            {
                yield return null;
                if (!Step(Time.unscaledDeltaTime)) yield break;
            }
        }
        finally { routineRunning = false; }
    }

    private static bool Step(float seconds)
    {
        try
        {
            if (!Applies || setting == null)
            {
                Slow = false;
                Charge = MaxCharge;
                return false;
            }
            // Paused by the game (popup, loading): neither drain nor refill.
            if (setting._gameSpeed == GameSpeed.Pause) return true;
            if (Slow)
            {
                Charge = Math.Max(0f, Charge - seconds);
                if (Charge <= 0f) SetSlow(false, "empty");
                return true;
            }
            Charge = Math.Min(MaxCharge, Charge + RefillPerSecond * seconds);
            return Charge < MaxCharge;
        }
        catch (Exception ex)
        {
            ApocalypseHooks.Report("slow-step", ex);
            Slow = false;
            Charge = MaxCharge;
            return false;
        }
    }
}

[HarmonyPatch(typeof(GameSpeedSetting), nameof(GameSpeedSetting.SetSpeed))]
internal static class ApocalypsePausePatch
{
    private static bool Prefix(GameSpeedSetting __instance, GameSpeed speed, SwitchPauseState pauseState, ref bool __result)
    {
        if (!ApocalypseTime.RefusePause(__instance, speed, pauseState)) return true;
        __result = false;
        return false;
    }
}

// Called twice per frame by TimeHandler.Update: a flag check only, nothing allocated.
[HarmonyPatch(typeof(GameSpeedSetting), nameof(GameSpeedSetting.GetMultiplier))]
internal static class ApocalypseSlowMultiplierPatch
{
    private static void Postfix(GameSpeed speed, ref float __result)
    {
        if (ApocalypseTime.Slow && speed != GameSpeed.Pause) __result = ApocalypseTime.SlowMultiplier;
    }
}
