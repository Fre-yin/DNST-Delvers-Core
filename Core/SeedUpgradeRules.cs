using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// The game's check of both skills (BuildingTransformTargetCondition.CheckCondition, only these two use it)
// asks for a building of the seed's key at the target cell and never for its summoner; the skill then summons the new building
// for the caster (Execution.Summon). Core refuses the target where caster and seed stand on different sides, one of Core's
// bandits against anybody else; between the game's own units nothing changes. The solar bloom shares the check and so the
// block: interpretation, not confirmed. The game in SeedUpgradeSide. No game types here.
internal static class SeedUpgradeRules
{
    internal const int LogCap = 40;

    internal static bool Allows(bool casterIsCore, bool seedIsCore) => casterIsCore == seedIsCore;

    internal static string RefusedLine(string caster, string seed, string key, bool casterIsCore, bool seedIsCore)
        => string.Format(CultureInfo.InvariantCulture, "CORE_SEED_UPGRADE_SIDE caster={0} seed={1} key={2} caster-side={3} seed-side={4} result=refused",
            caster ?? "-", seed ?? "-", key ?? "-", Side(casterIsCore), Side(seedIsCore));

    private static string Side(bool isCore) => isCore ? "core" : "party";
}
