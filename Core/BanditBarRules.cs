using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// A health bar for the bandits' Strange Seed and Mucus Sprayer ("always" visible, only the body bar). The game's
// building HUD has no bar (BuildingEntityHUD); Core draws the enemy units' body bar on the building (BanditBar): its sprites,
// colours and sorting read from a living bandit's UnitEntityHUD, its fill the game's way (UnitEntityHUD.SetBar: the bar
// material's "_ClipUvRight" = 1 - value / max, clamped). The value is the building's Durability of MaxDurability (seed 150,
// sprayer 200 in the game's table). Players' own buildings get none. ShowKey 0 turns it off. Rules without the game.
internal static class BanditBarRules
{
    internal const string ShowKey = "bandits.buildingbar";
    internal const string ClipProperty = "_ClipUvRight";
    // World units between the building's sprite top and the bar.
    internal const float TopOffset = 0.15f;
    internal const int LogCap = 200;

    internal static bool Show(double? setting) => setting is not double v || double.IsNaN(v) || v != 0.0;

    // The material value for a durability; null when it cannot be read (the bar keeps its last value).
    internal static float? Clip(float value, float max)
    {
        if (float.IsNaN(value) || float.IsNaN(max) || !(max > 0f)) return null;
        return Math.Clamp(1f - value / max, 0f, 1f);
    }

    internal static string Line(string what, string building, float value, float max, string source = null)
        => string.Format(CultureInfo.InvariantCulture, "CORE_BANDIT_BUILDING_BAR at={0} building={1} durability={2:0.#}/{3:0.#}{4}",
            what, building, value, max, source == null ? "" : " source=" + source);
}
