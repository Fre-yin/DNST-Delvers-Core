namespace DungeonSettlersDelvers.Core;

// GroupAiBandits, the bandit log: every line goes through Log, capped per fight (LogCap) and per session (SessionLogCap);
// Warn has its own cap, Log2 lets GroupAiBanditMoves share this log's cap, and LogSkillValues writes the game's data of
// every bandit skill once per session.
internal static partial class GroupAiBandits
{
    internal const int LogCap = 2000;

    private static int logLines;
    private static int warnings;

    // Once per session: the game's data of every bandit skill (BanditAreaRules.SkillValuesLine), for entering the factors.
    private static bool skillValuesLogged;

    private static void LogSkillValues()
    {
        BanditSerrated.ProbeStatic();
        BanditStatsProbe.Static();
        if (skillValuesLogged) return;
        var sheet = DataSheetManager.Instance;
        if (sheet == null) return;
        skillValuesLogged = true;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var keys = new List<string>();
        foreach (var unit in CoreUnitRules.All) keys.AddRange(unit.Skills);
        keys.AddRange(FollowUpSkills);
        foreach (var key in keys)
        {
            if (!seen.Add(key)) continue;
            var data = sheet.GetSkillData(key);
            if (data == null) continue;
            var values = data.FloatValues;
            var parts = new List<string>();
            for (var i = 0; values != null && i < values.Length; i++) parts.Add(values[i].ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
            Log(BanditAreaRules.SkillValuesLine(key, string.Join("/", parts), data.DevComment_SkillFloatValues, data.AreaType.ToString(),
                data.AreaRadius, data.Range));
        }
        // The plants' and summons' own hits are affecters: their FloatValues the same way, with the affecter's key.
        foreach (var key in SummonAffecters)
        {
            var data = sheet.GetAffecterData(key);
            if (data == null) continue;
            var values = data.FloatValues;
            var parts = new List<string>();
            for (var i = 0; values != null && i < values.Length; i++) parts.Add(values[i].ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
            Log(BanditAreaRules.SkillValuesLine(key, string.Join("/", parts), "-", "-", 0, 0));
        }
    }

    // Toolkit's skill damage table: the hits of a bandit that are no skill of its row, a follow up its skill sets off or a
    // summon's attack, and the affecters its plants and summons hit with.
    private static readonly string[] FollowUpSkills =
        { "SKILL_RapidFireAttack", "SKILL_FireMissileAttack", "SKILL_TwinThrustAttack", "SKILL_BubbleTackle", "SKILL_TongueThrust" };

    private static readonly string[] SummonAffecters = { "AFFECTER_MucusSprayer", "AFFECTER_SolarBloom", "AFFECTER_SunHeartBloom", "AFFECTER_BubbleSplash" };

    // (a 167 s fight ran into the cap at 3/4): LogCap lines per fight, at most SessionLogCap a session; a fight
    // begins with a bandit's combat decision after FightGapSeconds without one. CORE_AI_BANDIT_LOG_CAPPED once per fight.
    internal const int SessionLogCap = 20000;
    internal const double FightGapSeconds = 10.0;
    private static int sessionLogLines;
    private static int fight;
    private static bool cappedLogged;
    private static double lastCombatDecision = double.NegativeInfinity;

    private static void NoteFightDecision(bool inCombat)
    {
        if (!inCombat) return;
        var now = GameClock.Now;
        if (GroupAiRules.NewFight(lastCombatDecision, now, FightGapSeconds))
        {
            fight++;
            logLines = 0;
            cappedLogged = false;
        }
        lastCombatDecision = now;
    }

    private static void Log(string line)
    {
        if (sessionLogLines >= SessionLogCap) return;
        if (logLines++ < LogCap)
        {
            sessionLogLines++;
            DelversHost.Info(line);
            return;
        }
        if (cappedLogged) return;
        cappedLogged = true;
        sessionLogLines++;
        DelversHost.Info("CORE_AI_BANDIT_LOG_CAPPED fight=" + fight + " lines=" + LogCap);
    }

    // For GroupAiBanditMoves, sharing this log's cap.
    internal static void Log2(string line) => Log(line);

    private static void Warn(string line)
    {
        if (warnings++ < GroupAiRules.WarningCap) DelversHost.Warning(line);
    }
}
