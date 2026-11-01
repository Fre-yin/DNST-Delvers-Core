using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// The bandits' herbalist with the Strange Seed, its mucus sprayer and the passive Life
// Burst. The game's skills stay as they are (way B: row, icon, look, sound); Core only filters the aura's targets and
// sets the Life Burst's heal for a Core summoner (GroupAiHealerSeed). (bandits.lifeburst.share, the player side
// unchanged). The seed only when a comrade below
// SeedShare stands next to the foe it is thrown at; the upgrade to the sprayer once the seed has UpgradeSeconds left.
internal static class GroupAiHealerSeedRules
{
    internal const string SeedSkill = "SKILL_StrangeSeed";
    internal const string SprayerSkill = "SKILL_MucusSprayer";
    internal const string LifeBurstSkill = "SKILL_LifeBurst";
    internal const string SeedBuilding = "BUILDING_StrangeSeed";
    internal const string AuraKey = "AFFECTER_StrangeSeedAura";
    internal const float SeedShare = 0.9f;
    internal const float UpgradeSeconds = 5f;
    internal const string LifeBurstShareKey = "bandits.lifeburst.share";
    internal const double LifeBurstShareDefault = 0.12;
    internal const int ProbeCap = 8;

    // Test tool (a bandit seed for the comparison picture, own against bandit): a herbalist's next
    // decision casts her Strange Seed at a chosen unit's cell (or the nearest hostile), or with upgrade grows her seed into the
    // sprayer at once. Her normal skill path, so the seed has her as summoner. CORE_TEST_BANDIT_SEED result=queued|nohealer|
    // cast|noskill|cooldown|range|notarget|noseed.
    internal static string TestSeedLine(string unit, string target, bool upgrade, string result)
        => "CORE_TEST_BANDIT_SEED unit=" + (unit ?? "-") + " target=" + (target ?? "-") + " upgrade=" + (upgrade ? 1 : 0) + " result=" + result;

    internal static string TestSeedResult(bool hasSlot, bool slotFree, bool hasTarget, bool inRange)
        => !hasSlot ? "noskill" : !slotFree ? "cooldown" : !hasTarget ? "notarget" : !inRange ? "range" : "cast";

    internal static double LifeBurstShare(double? setting)
        => setting is double value && !double.IsNaN(value) ? Math.Clamp(value, 0.0, 1.0) : LifeBurstShareDefault;

    internal static float LifeBurstHeal(float maxHealth, double share) => maxHealth > 0f ? (float)(maxHealth * share) : 0f;

    // The foe to throw the seed at: the one with the most comrades below SeedShare right next to it (the herbalist
    // herself counts), the nearer one on a tie; null when no foe has one. Foes as (index, distance to the herbalist,
    // comrades below next to it).
    internal static int PickAnchor(IReadOnlyList<(int Index, int Distance, int Below)> foes)
    {
        var best = -1;
        int bestBelow = 0, bestDistance = int.MaxValue;
        foreach (var foe in foes ?? Array.Empty<(int, int, int)>())
        {
            if (foe.Below <= 0) continue;
            if (foe.Below > bestBelow || (foe.Below == bestBelow && foe.Distance < bestDistance))
            {
                best = foe.Index;
                bestBelow = foe.Below;
                bestDistance = foe.Distance;
            }
        }
        return best;
    }

    internal static bool Below(float share) => share < SeedShare;

    // The upgrade when the own seed has at most UpgradeSeconds left; an unknown time (0 or less) never upgrades.
    internal static bool UpgradeDue(float secondsLeft) => secondsLeft > 0f && secondsLeft <= UpgradeSeconds;

    // Fix B (left=0.0 always): the seed dies when its aura passive's _timeCounter reaches its Values[2]
    // (HealRoutine); the aura itself carries no duration. Left is the difference, 0 when unknown.
    internal const int LifeValueIndex = 2;

    // The first check came before the seed's first Regen (life=-1) and later waits were not logged again.
    // Each distinct result logs once per seed: range (she is too far), wait:unknown (no life read yet), wait, upgrade.
    internal static string UpgradeResult(bool inRange, bool lifeKnown, bool due)
        => !inRange ? "range" : !lifeKnown ? "wait:unknown" : due ? "upgrade" : "wait";

    internal static float Left(float life, float counter) => life > 0f && counter >= 0f ? Math.Max(0f, life - counter) : 0f;

    // Fix C: whether the herbalist's Life Burst is a passive of hers and registered with the game.
    internal static string LifeBurstProbeLine(string unit, bool listed, string registered, string passive, string action)
        => string.Format(CultureInfo.InvariantCulture, "CORE_LIFEBURST_PROBE unit={0} listed={1} registered={2} passive={3} action={4}",
            unit, listed ? 1 : 0, registered ?? "-", passive ?? "-", action);

    // C(c): register the Life Burst only on a registered unit that lacks it; never make the unit's table anew.
    internal static string RegisterDecision(bool unitRegistered, bool hasPassive)
        => hasPassive ? "skip:present" : unitRegistered ? "register" : "skip:not-registered";

    internal static string RegisterLine(string unit, bool before, bool after, string result)
        => string.Format(CultureInfo.InvariantCulture, "CORE_LIFEBURST_REGISTER unit={0} before={1} after={2} result={3}",
            unit, before ? 1 : 0, after ? 1 : 0, result);

    internal static string LifeBurstTryLine(string owner, string target, string key, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_LIFEBURST_PROBE kind=try owner={0} target={1} key={2} tick={3}", owner, target, key ?? "-", tick);


    internal static string SeedLine(string unit, string target, int below, string rule, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_BANDIT_SEED unit={0} target={1} below={2} rule={3} tick={4}", unit, target, below, rule, tick);

    internal static string UpgradeLine(string unit, string seed, float secondsLeft, float life, float counter, string result, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_BANDIT_SEED_UPGRADE unit={0} seed={1} left={2:0.0} life={3:0.##} counter={4:0.##} result={5} tick={6}",
            unit, seed, secondsLeft, life, counter, result, tick);

    internal static string AuraFilterLine(string summoner, bool changed, string unit, bool native, bool filtered)
        => string.Format(CultureInfo.InvariantCulture, "CORE_SEED_AURA_FILTER summoner={0} changed={1} unit={2} native={3} filtered={4}",
            summoner, changed ? 1 : 0, unit, native ? 1 : 0, filtered ? 1 : 0);

    internal static string LifeBurstLine(string caster, string target, float maxHealth, double share, float heal, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_LIFEBURST_HEAL caster={0} target={1} max={2:0} share={3:0.00} heal={4:0.0} tick={5}",
            caster, target, maxHealth, share, heal, tick);

    internal static string SprayerHitLine(string summoner, string target, string side, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_MUCUS_SPRAYER_HIT summoner={0} target={1} side={2} tick={3}", summoner, target, side, tick);

    // Plan (b): where a Core summoner's seed loses its heals. Per seed: allow (comrades the aura
    // predicate lets through), yield (heals AFFECTER_StrangeSeedAura._Heal_d__2 makes), applied (heals that arrived, and
    // their sum); Frieren's first seeds the same for comparison. A line when the counts changed, at most every LineSeconds.
    internal const float HealProbeSeconds = 5f;
    internal const int HealProbeCap = 200;
    internal const int CompareSeeds = 2;

    internal static string HealProbeLine(string seed, string summoner, bool core, int allow, int yields, int applied, float amount, long tick)
        => string.Format(CultureInfo.InvariantCulture,
            "CORE_SEED_HEAL_PROBE seed={0} summoner={1} core={2} allow={3} yield={4} applied={5} amount={6:0.0} tick={7}",
            seed, summoner ?? "-", core ? 1 : 0, allow, yields, applied, amount, tick);

    internal static string PushLine(string seed, string unit, string side, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_SEED_PUSH seed={0} unit={1} side={2} tick={3}", seed, unit, side, tick);

    // Plan (b) step 3: the game's seed heal (AFFECTER_StrangeSeedAura's heal routine): the summoner's
    // magic attack x the aura's FloatValues[1] x 0.01, rounded down, per step of the aura's counter.
    internal const int HealValueIndex = 1;
    internal const float HealScale = 0.01f;

    internal static float SeedHeal(float magicAttack, float auraValue)
        => magicAttack > 0f && auraValue > 0f ? MathF.Floor(magicAttack * auraValue * HealScale) : 0f;

    // The cell and its eight neighbours (the seed's area without the aura's own, and the Life Burst's neighbours).
    internal static IEnumerable<(int X, int Y)> AroundCells(int x, int y)
    {
        for (var dx = -1; dx <= 1; dx++)
            for (var dy = -1; dy <= 1; dy++)
                yield return (x + dx, y + dy);
    }

    internal static string AreaLine(string seed, bool core, IEnumerable<(int X, int Y)> cells, IEnumerable<string> units)
        => string.Format(CultureInfo.InvariantCulture, "CORE_SEED_AREA seed={0} core={1} cells={2} units={3}", seed, core ? 1 : 0,
            string.Join("|", cells.Select(c => c.X + "," + c.Y)), string.Join("|", units));

    internal static string AmountSkip(float magic, float value)
        => string.Format(CultureInfo.InvariantCulture, "amount:matk={0:0.#},value={1:0.##}", magic, value);

    internal static string TargetSkip(int cells, int coreUnits, bool fallback)
        => string.Format(CultureInfo.InvariantCulture, "no-targets:area={0},core={1},fallback={2}", cells, coreUnits, fallback ? 1 : 0);

    internal static string HealSkipLine(string seed, string reason, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_SEED_HEAL_SKIP seed={0} reason={1} tick={2}", seed, reason, tick);

    internal static string SeedHealLine(string seed, string summoner, string target, float amount, bool published, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_SEED_HEAL seed={0} summoner={1} target={2} amount={3:0.0} result={4} tick={5}",
            seed, summoner, target, amount, published ? "published" : "failed", tick);

    // A herbalist's Life Burst healed the player her seed sat on; for a Core owner a heal on a unit that is
    // not one of Core's is dropped.
    internal static bool LifeBurstKeeps(bool coreOwner, bool coreTarget) => !coreOwner || coreTarget;

    // The game's monster difficulty affecter for a combat level (Tooltip_Difficulty.GetMonsterDifficultyAffecterKey,
    // "AFFECTER_MonsterDifficulty{0}"; classes exist for Easy, Normal, VeryHard and Downfall, Hard is the base). Core's
    // Apocalypse plays as Downfall.
    internal static string MonsterDifficultyKey(int level) => level switch
    {
        0 => "AFFECTER_MonsterDifficultyEasy",
        1 => "AFFECTER_MonsterDifficultyNormal",
        3 => "AFFECTER_MonsterDifficultyVeryHard",
        4 or 5 => "AFFECTER_MonsterDifficultyDownfall",
        _ => null
    };

    internal static readonly string[] MonsterDifficultyKeys =
    {
        "AFFECTER_MonsterDifficultyEasy", "AFFECTER_MonsterDifficultyNormal", "AFFECTER_MonsterDifficultyVeryHard", "AFFECTER_MonsterDifficultyDownfall"
    };

    internal static string MagicSetLine(float wanted, float? normalPerModifier, float row)
        => string.Format(CultureInfo.InvariantCulture, "CORE_UNIT_MAGIC set={0:0.##} normalPerModifier={1} row={2:0.##}",
            wanted, normalPerModifier?.ToString("0.###", CultureInfo.InvariantCulture) ?? "unknown", row);

    internal static string MagicAffecterLine(string key, bool found, float? magicAdjust, float? physicalAdjust)
        => string.Format(CultureInfo.InvariantCulture, "CORE_UNIT_MAGIC_PROBE affecter={0} found={1} magicAdjust={2} physicalAdjust={3}",
            key, found ? 1 : 0, magicAdjust?.ToString("0.###", CultureInfo.InvariantCulture) ?? "-",
            physicalAdjust?.ToString("0.###", CultureInfo.InvariantCulture) ?? "-");

    // The adjustments are the PerModifier stats (83 magic, 81 physical) the game multiplies the attack with; ownPerModifier is
    // the herbalist's stat 83 in the fight.
    internal static string MagicProbeLine(string unit, int level, string key, bool carries, float? magicAdjust, float? physicalAdjust, float row, float actual,
        float ownPerModifier)
        => string.Format(CultureInfo.InvariantCulture,
            "CORE_UNIT_MAGIC_PROBE unit={0} level={1} affecter={2} carries={3} magicAdjust={4} physicalAdjust={5} row={6:0.##} actual={7:0.##} ownPerModifier={8:0.###}",
            unit, level, key ?? "-", carries ? 1 : 0, magicAdjust?.ToString("0.###", CultureInfo.InvariantCulture) ?? "-",
            physicalAdjust?.ToString("0.###", CultureInfo.InvariantCulture) ?? "-", row, actual, ownPerModifier);

    internal static string LifeBurstNoneLine(string caster, string seed, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_LIFEBURST_HEAL caster={0} seed={1} result=none tick={2}", caster, seed, tick);

    internal static string LifeBurstDropLine(string caster, string target, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_LIFEBURST_HEAL caster={0} target={1} dropped=player tick={2}", caster, target, tick);

    internal static string SideLine(string seed, string summoner, bool coreSummoner, string hostileToPlayer, string hostileToBandit)
        => string.Format(CultureInfo.InvariantCulture, "CORE_SEED_SIDE_PROBE seed={0} summoner={1} core={2} hostileToPlayer={3} hostileToBandit={4}",
            seed, summoner ?? "-", coreSummoner ? 1 : 0, hostileToPlayer ?? "-", hostileToBandit ?? "-");

    internal static string FxLine(string seed, string path, string components, string renderer, string material, string color, int pooled)
        => string.Format(CultureInfo.InvariantCulture, "CORE_SEED_FX_PROBE seed={0} path={1} components={2} renderer={3} material={4} color={5} pooled={6}",
            seed, path, components ?? "-", renderer ?? "-", material ?? "-", color ?? "-", pooled);
}
