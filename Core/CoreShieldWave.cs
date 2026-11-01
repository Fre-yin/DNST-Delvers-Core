using UnityEngine;

namespace DungeonSettlersDelvers.Core;

// The wave over the honeycomb (ShieldRules.WaveFactor): every band alive steps each frame from
// Core's own frame call (DelversCoreRuntime.LateFrame), in game time, its strips at the chosen opacity times the wave. After
// ShieldRules.WaveFailureLimit failures the wave goes off for the session and the bands stay calm.
internal static class CoreShieldWave
{
    // Every band alive, for the wave's step each frame.
    private static readonly List<CoreShieldBand> live = new();

    internal static void Register(CoreShieldBand band) => live.Add(band);

    internal static void Unregister(CoreShieldBand band) => live.Remove(band);

    // The wave's time: game time in seconds (60 ticks a second), so it stands still in a pause and follows the game's speed.
    internal static float Time()
    {
        var tick = CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0L;
        return tick / 60f;
    }

    // Failures of the wave's frame step; at ShieldRules.WaveFailureLimit the wave goes off for the session.
    private static int waveFailures;
    private static bool waveOff;

    // Every strip of a layer at its colour, the wave's factor at its centre applied to the opacity.
    internal static void Paint(CoreShieldBand band, CoreShieldBand.Layer item, float t)
    {
        if (item?.Strips == null || item.Clip == null || !item.Clip.gameObject.activeSelf) return;
        var boss = !band.IsParty;
        var (on, trough, length, period) = waveOff ? (false, 1f, 1f, 1f) : ShieldRules.WaveSettings(boss,
            CoreSettings.Number(boss ? ShieldRules.BossWaveKey : ShieldRules.PartyWaveKey),
            CoreSettings.Number((boss ? ShieldRules.BossWaveKey : ShieldRules.PartyWaveKey) + ".trough"),
            CoreSettings.Number((boss ? ShieldRules.BossWaveKey : ShieldRules.PartyWaveKey) + ".length"),
            CoreSettings.Number((boss ? ShieldRules.BossWaveKey : ShieldRules.PartyWaveKey) + ".period"));
        var c = item.Colour;
        for (var i = 0; i < item.Strips.Length; i++)
        {
            if (item.Strips[i] == null) continue;
            var factor = on ? ShieldRules.WaveFactor(item.Centres[i], t, length, period, trough) : 1f;
            item.Strips[i].color = new Color(c.r, c.g, c.b, c.a * factor);
        }
    }

    // Each frame: the wave moves on every band alive (DelversCoreRuntime.LateFrame).
    internal static void AnimateAll()
    {
        if (live.Count == 0) return;
        var t = Time();
        for (var i = live.Count - 1; i >= 0; i--)
        {
            var band = live[i];
            if (band == null || !band.Alive)
            {
                live.RemoveAt(i);
                continue;
            }
            // Per band, so one failing band does not stop the others.
            try
            {
                Paint(band, band.Under, t);
                Paint(band, band.Top, t);
                Paint(band, band.Blood, t);
            }
            catch (Exception ex) { WaveFailed(band, ex); }
        }
    }

    private static void WaveFailed(CoreShieldBand band, Exception ex)
    {
        waveFailures++;
        if (waveFailures <= ShieldRules.WaveFailureLimit)
            DelversHost.Warning("CORE_SHIELD_WAVE_FAILED n=" + waveFailures + ": " + ex.GetType().Name + ": " + ex.Message);
        if (waveFailures < ShieldRules.WaveFailureLimit || waveOff) return;
        waveOff = true;
        DelversHost.Warning("CORE_SHIELD_WAVE_OFF after=" + waveFailures + " (bands stay calm at their opacity; further failures not logged)");
        try
        {
            Paint(band, band.Under, 0f);
            Paint(band, band.Top, 0f);
            Paint(band, band.Blood, 0f);
        }
        catch (Exception) { }
    }
}
