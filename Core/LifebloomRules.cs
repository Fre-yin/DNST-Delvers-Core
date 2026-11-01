namespace DungeonSettlersDelvers.Core;

// The rules of Lifebloom, a healer's heal over time (in game: Lifebloom). No game types here, so
// the numbers can be tested on their own.
// - A cast adds a stack (at most MaxStacks) and runs for DurationSeconds; every stack heals
//   State.PerStack of the caster's power per second (players: PlayerPerStack, Core's enemies: NpcHealPerStack).
// - When it ends, it blooms once more: State.BurstShare of the power per stack. A recast with less
//   than RefreshBurstBelow seconds left blooms the old stacks first; a dispel blooms as well.
// - MaxCharges charges, one back every TableCooldown seconds; the game's cooldown only runs when
//   the last charge is spent. Off the global cooldown.
// - Core's own enemies heal a share of the target's maximum health instead (NpcPower), raised by
//   their intelligence and willpower.
internal static class LifebloomRules
{
    internal readonly struct State
    {
        internal int Stacks { get; }
        internal double Until { get; }
        internal double Power { get; }
        // Share of the power one stack heals per second, and per stack at the bloom; fixed at the cast
        // (players: settings, Core's enemies: NpcHealPerStack/NpcBurstPerStack). 0 means the player default.
        internal double PerStack { get; }
        internal double BurstShare { get; }
        internal bool Active => Stacks > 0;

        internal State(int stacks, double until, double power, double perStack = 0, double burstShare = 0)
        {
            Stacks = stacks;
            Until = until;
            Power = power;
            PerStack = perStack > 0 ? perStack : PlayerPerStackDefault;
            BurstShare = burstShare > 0 ? burstShare : PlayerBurstDefault;
        }
    }

    // The charges of one caster: NextAt is the game second the next charge comes back.
    internal readonly struct Charges
    {
        internal int Count { get; }
        internal double NextAt { get; }
        internal double Cooldown { get; }

        internal static Charges Full => new(MaxCharges, 0, TableCooldown);

        internal Charges(int count, double nextAt, double cooldown)
        {
            Count = count;
            NextAt = nextAt;
            Cooldown = cooldown;
        }
    }

    internal const string SkillKey = "SKILL_DelversLifebloom";
    internal const string EffectKey = "AFFECTER_DelversCoreLifebloom";
    internal const string SkillTemplate = "SKILL_HealingTouchAttack";
    internal const string EffectTemplate = "AFFECTER_HealthBoost";
    internal const int Range = 5;
    internal const float StartDelay = 0.2f;
    internal const int EnergyCost = 3;
    internal const float TableCooldown = 12f;
    internal const int MaxStacks = 3;
    internal const double DurationSeconds = 15.0;
    // Players (variant B): 0.08 of the magic attack per stack and second, 0.15 per stack at the
    // bloom (it mostly bloomed on full health); both in the settings file, capped.
    internal const string PlayerPerStackKey = "lifebloom.player.perstack";
    internal const string PlayerBurstKey = "lifebloom.player.burst";
    internal const double PlayerPerStackDefault = 0.08;
    internal const double PlayerBurstDefault = 0.15;
    internal const double PlayerPerStackMax = 0.5;
    internal const double PlayerBurstMax = 2.0;
    // Core's enemies keep the numbers they healed with before (tick and bloom), decoupled from the players'.
    internal const double NpcHealPerStack = 0.04;
    internal const double NpcBurstPerStack = 0.25;
    internal const double RefreshBurstBelow = 3.0;
    internal const int MaxCharges = 3;

    // The skill icon shows the charges left (CoreAssets.LifebloomSkillFor), clamped to 0..MaxCharges.
    internal static int IconCharges(int charges) => Math.Max(0, Math.Min(MaxCharges, charges));

    // Lifebloom placed while the global cooldown runs (party AI, the game's AI without group AI, the herbalist).
    internal static string OgcdLine(string unit, int charges, string source)
        => $"CORE_LIFEBLOOM_OGCD unit={unit} charges={charges} source={source}";

    // Our skill stays off the global cooldown whoever casts it (player, party AI, the game's AI, the herbalist); the
    // decision depends on the skill alone. After a cast the global cooldown is not set for it.
    internal static bool SkipsGlobalCooldown(string skillKey) => skillKey == SkillKey;

    // The game's cooldown check of a skill that failed with "cooldown not met": for our skill it only stands while its own
    // cooldown (no charge left) runs; a failure caused by the global cooldown alone passes.
    internal static bool PassesCooldownCheck(string skillKey, bool ownCooldownRunning)
        => SkipsGlobalCooldown(skillKey) && !ownCooldownRunning;

    // The icon refresh on a charge change (no slot shown: slots=0, quiet, not an error).
    internal static string IconLine(Guid caster, int charges, int slots, int set, string source, string sprite)
        => $"CORE_LIFEBLOOM_ICON caster={caster:D} charges={charges} slots={slots} set={set} source={source} sprite={sprite ?? "-"}";
    internal const bool AttackSpeedAdjusted = false;

    // Core's own enemies (settings file): percent of the target's maximum health per stack and second,
    // and how much intelligence plus willpower raise it.
    internal const string NpcPercentKey = "bandits.lifebloom.percent";
    internal const string NpcStatFactorKey = "bandits.lifebloom.statfactor";
    // (balance test): 0.5 stays for now (was 1.0); the key still overrides it.
    internal const double NpcPercentDefault = 0.5;
    internal const double NpcStatFactorDefault = 1.0;
    internal const double NpcPercentMax = 10.0;

    // A missing effect this close to the end counts as the end, not as a dispel.
    internal const double EndTolerance = 1.0;

    internal static readonly UniqueCandidateTranslations Name = new(
        "Lifebloom", "생명의 개화", "Floraison de vie", "Lebensblüte", "Цветение жизни",
        "生命绽放", "生命綻放", "命の開花", "Floración vital", "Floração vital");

    // Tooltip step 1: the description names its numbers as the game's own skills do, {n} from the
    // row's FloatValues (TooltipValues) and the stats as [STATUS_...] tags; the game's tooltip fills and colours both
    // (Tooltip_Base.ApplyTooltipFormatText, StringTools.FormatText). Each language follows the phrasing of the game's own heals
    // in it (SKILL_CheeringHowl, SKILL_StrangeSeed).
    internal static readonly UniqueCandidateTranslations SkillDescription = new(
        "Lets an ally bloom: restores [STATUS_HealthTotal] by {0}% of [STATUS_MagicAttackPower] per stack every second for {1} seconds and stacks up to {2} times. When it ends, it blooms once more for {3}% of [STATUS_MagicAttackPower] per stack. {4} charges.",
        "아군을 개화시킵니다: {1}초 동안 매초 중첩당 [STATUS_HealthTotal]을 [STATUS_MagicAttackPower]의 {0}%만큼 회복시키며, 최대 {2}번 중첩됩니다. 끝날 때 중첩당 [STATUS_MagicAttackPower]의 {3}%만큼 한 번 더 피어납니다. 충전 {4}회.",
        "Fait fleurir un allié : restaure chaque seconde [STATUS_HealthTotal] équivalente à {0}% de [STATUS_MagicAttackPower] par cumul pendant {1} secondes et se cumule jusqu'à {2} fois. À la fin, il fleurit une dernière fois pour {3}% de [STATUS_MagicAttackPower] par cumul. {4} charges.",
        "Lässt eine verbündete Figur erblühen: stellt {1} Sekunden lang jede Sekunde je Stapel [STATUS_HealthTotal] in Höhe von {0}% × [STATUS_MagicAttackPower] wieder her und stapelt bis zu {2} Mal. Zum Ende blüht sie noch einmal auf, je Stapel {3}% × [STATUS_MagicAttackPower]. {4} Ladungen.",
        "Заставляет союзника расцвести: в течение {1} сек. каждую секунду восстанавливает [STATUS_HealthTotal] на {0}% от значения «[STATUS_MagicAttackPower]» за каждое наложение, складывается до {2} раз. В конце расцветает ещё раз: {3}% от значения «[STATUS_MagicAttackPower]» за каждое наложение. Зарядов: {4}.",
        "让一名友方绽放：{1}秒内每秒每层以[STATUS_MagicAttackPower]的 {0}% 恢复[STATUS_HealthTotal]，最多叠加{2}层。结束时再绽放一次，每层恢复[STATUS_MagicAttackPower]的 {3}%。{4}次充能。",
        "讓一名友方綻放：{1}秒內每秒每層恢復[STATUS_HealthTotal]，數值為[STATUS_MagicAttackPower]的{0}%，最多疊加{2}層。結束時再綻放一次，每層數值為[STATUS_MagicAttackPower]的{3}%。{4}次充能。",
        "味方を開花させる：{1}秒間、毎秒スタックごとに[STATUS_HealthTotal]を[STATUS_MagicAttackPower]の{0}%分回復し、最大{2}回まで重なる。終わるときにスタックごとに[STATUS_MagicAttackPower]の{3}%分もう一度咲く。チャージ{4}回。",
        "Hace florecer a un aliado: restaura cada segundo [STATUS_HealthTotal] en un {0}% de [STATUS_MagicAttackPower] por acumulación durante {1} segundos y se acumula hasta {2} veces. Al terminar florece una vez más por un {3}% de [STATUS_MagicAttackPower] por acumulación. {4} cargas.",
        "Faz um aliado florescer: restaura a cada segundo [STATUS_HealthTotal] em {0}% de [STATUS_MagicAttackPower] por acúmulo durante {1} segundos e acumula até {2} vezes. Ao terminar, floresce mais uma vez em {3}% de [STATUS_MagicAttackPower] por acúmulo. {4} cargas.");
    internal static readonly UniqueCandidateTranslations EffectDescription = new(
        "Heals every second. Blooms once more when it ends.", "매초 회복합니다. 끝날 때 한 번 더 피어납니다.",
        "Soigne chaque seconde. Fleurit une dernière fois à la fin.", "Heilt jede Sekunde. Blüht zum Ende noch einmal auf.",
        "Лечит каждую секунду. В конце расцветает ещё раз.", "每秒治疗。结束时再绽放一次。", "每秒治療。結束時再綻放一次。",
        "毎秒回復する。終わるときにもう一度咲く。", "Cura cada segundo. Florece una vez más al terminar.",
        "Cura a cada segundo. Floresce mais uma vez ao terminar.");

    // The skill row's FloatValues: only the game's tooltip reads them ({0} to {4} in SkillDescription); the cast is Core's
    // own (Lifebloom.TakeOver replaces the healing touch's routine). Percent of the magic attack per stack and second,
    // seconds, stacks, percent at the bloom per stack, charges.
    internal static float[] TooltipValues(double perStack, double burst)
        => new[] { (float)Math.Round(perStack * 100.0, 2), (float)DurationSeconds, MaxStacks, (float)Math.Round(burst * 100.0, 2), MaxCharges };

    internal static string SkillNameKey => "TEXTKEY_" + SkillKey + "_NAME";
    internal static string SkillDescKey => "TEXTKEY_" + SkillKey + "_DESC";
    internal static string EffectNameKey => "TEXTKEY_" + EffectKey + "_NAME";
    internal static string EffectDescKey => "TEXTKEY_" + EffectKey + "_DESC";

    // A cast on a target with the state before: one stack more, the duration renewed (never
    // shortened), the power of this cast. burstStacks are the old stacks that bloom first when the
    // buff was about to end.
    internal static State Cast(State before, double now, double power, out int burstStacks, double perStack = 0, double burstShare = 0)
    {
        burstStacks = 0;
        var running = before.Active && before.Until > now;
        if (running && before.Until - now < RefreshBurstBelow) burstStacks = before.Stacks;
        return new State(Math.Min(MaxStacks, (running ? before.Stacks : 0) + 1),
            Math.Max(now + DurationSeconds, running ? before.Until : 0), power, perStack, burstShare);
    }

    // The heal between two game seconds, up to the end of the buff.
    internal static double HealFor(State state, double from, double to)
    {
        if (!state.Active || to <= from) return 0;
        return Math.Max(0, Math.Min(to, state.Until) - from) * state.Stacks * state.PerStack * state.Power;
    }

    // The bloom of stacks with the state's power and share (the state before a recast for the refresh bloom).
    internal static double Burst(State state, int stacks) => Math.Max(0, stacks) * state.BurstShare * state.Power;

    // The pending entry for a processed heal: the first with the same requested amount (rounding of the game's
    // float), else the first one when the amount is unknown, else -1.
    internal static int PendingMatch(IReadOnlyList<double> amounts, double amount)
    {
        if (amounts == null || amounts.Count == 0) return -1;
        if (!(amount > 0)) return 0;
        for (var i = 0; i < amounts.Count; i++)
            if (Math.Abs(amounts[i] - amount) <= 0.01 + Math.Abs(amount) * 1e-5) return i;
        return -1;
    }

    // What a heal really gave: the changes on head and body, never below zero.
    internal static double Effective(double head, double body) => Math.Max(0, head) + Math.Max(0, body);

    // The players' settings; missing or negative values give the default, too large ones the cap.
    internal static double PlayerPerStack(double? setting)
        => setting is { } value && value >= 0 ? Math.Min(value, PlayerPerStackMax) : PlayerPerStackDefault;

    internal static double PlayerBurst(double? setting)
        => setting is { } value && value >= 0 ? Math.Min(value, PlayerBurstMax) : PlayerBurstDefault;

    // The settings of Core's own enemies; missing or negative values give the default.
    internal static double NpcPercent(double? setting)
    {
        if (setting is { } value && value >= 0) return Math.Min(value, NpcPercentMax);
        return NpcPercentDefault;
    }

    internal static double NpcStatFactor(double? setting)
    {
        if (setting is { } value && value >= 0) return Math.Min(value, NpcPercentMax);
        return NpcStatFactorDefault;
    }

    // Every point of intelligence plus willpower adds statFactor percent of the base percent.
    internal static double NpcEffectivePercent(double percent, double intelligence, double willpower, double statFactor)
        => Math.Min(NpcPercentMax, Math.Max(0, percent) * (1 + Math.Max(0, intelligence + willpower) * Math.Max(0, statFactor) / 100));

    // The power that makes one stack heal percent of maxHealth per second.
    internal static double NpcPower(double maxHealth, double percent)
    {
        if (!(maxHealth > 0) || !(percent > 0)) return 0;
        return maxHealth * percent / 100 / NpcHealPerStack;
    }

    internal static bool Expired(State state, double now) => state.Active && now >= state.Until;

    // Why the buff blooms now: "end", "dispel" (the effect is gone early) or null while it runs.
    internal static string BloomReason(State state, double now, bool effectGone)
    {
        if (!state.Active) return null;
        if (Expired(state, now) || (effectGone && state.Until - now <= EndTolerance)) return "end";
        if (!effectGone) return null;
        return "dispel";
    }

    // A heal split over head and body by what each is missing; never more than both miss together.
    // At full health a tick goes out as no heal at all (the game would show a 0).
    internal static bool HealsThroughGame(double missingHead, double missingBody)
        => Math.Max(0, missingHead) + Math.Max(0, missingBody) > WoundRules.OverhealEpsilon;

    internal static (double Head, double Body) Split(double heal, double missingHead, double missingBody)
    {
        missingHead = Math.Max(0, missingHead);
        missingBody = Math.Max(0, missingBody);
        var missing = missingHead + missingBody;
        if (!(heal > 0) || !(missing > 0)) return (0, 0);
        var applied = Math.Min(heal, missing);
        var head = applied * missingHead / missing;
        return (head, applied - head);
    }

    // A cast spends a charge; the recharge starts when the caster was full. letCooldown: the last
    // charge is gone, so the game's cooldown runs (BlockSeconds long).
    internal static Charges Spend(Charges before, double now, double cooldown, out bool letCooldown)
    {
        var wasFull = before.Count >= MaxCharges;
        var count = Math.Max(0, before.Count - 1);
        var step = cooldown > 0 ? cooldown : TableCooldown;
        letCooldown = count == 0;
        return new Charges(count, wasFull ? now + step : before.NextAt, step);
    }

    internal static double BlockSeconds(Charges charges, double now)
    {
        if (charges.Count <= 0) return Math.Max(0, charges.NextAt - now);
        return 0;
    }

    // Game seconds until the caster holds at least need charges again (0 when it does now).
    internal static double SecondsUntilCharges(Charges before, double now, int need)
    {
        var current = Recharge(before, now);
        if (current.Count >= need) return 0;
        return Math.Max(0, current.NextAt - now) + Math.Max(0, need - current.Count - 1) * current.Cooldown;
    }

    // The charges that came back up to now.
    internal static Charges Recharge(Charges before, double now)
    {
        var count = before.Count;
        var nextAt = before.NextAt;
        while (count < MaxCharges && now >= nextAt)
        {
            count++;
            nextAt += before.Cooldown;
        }
        return new Charges(count, nextAt, before.Cooldown);
    }
}
