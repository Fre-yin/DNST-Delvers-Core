using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// GroupAiRules, the log lines of the group AI (party and bandits) and their parts, in one place: each builds
// one CORE_AI_ line (or a tail of one) from plain values, so the Toolkit's parsers and the tests read the same text.
internal static partial class GroupAiRules
{
    internal static string SeedCheckLine(string unit, string stage, string upgrade, bool free, int seeds, int own, string summoner,
                                         string self, int? distance, int? range, float? lowest, bool bloomFree, long tick)
        => $"CORE_AI_SEED_BURST_CHECK unit={unit} stage={stage} ok={(stage == "burst" ? 1 : 0)} upgrade={Short(upgrade ?? "-")} "
           + $"free={(free ? 1 : 0)} seeds={seeds} own={own} summoner={(string.IsNullOrEmpty(summoner) ? "-" : summoner)} self={self} "
           + $"dist={distance?.ToString(CultureInfo.InvariantCulture) ?? "-"} range={range?.ToString(CultureInfo.InvariantCulture) ?? "-"} lowest={(lowest is { } share ? share.ToString("0.00", CultureInfo.InvariantCulture) : "-")} "
           + $"bloomFree={(bloomFree ? 1 : 0)} tick={tick}";

    internal static string SkippedText(string skill, string ally, string flag)
        => string.IsNullOrEmpty(skill) ? "" : " skipped=friendly-fire ffSkill=" + Short(skill) + " ffAlly=" + (ally ?? "-") + " " + (flag ?? "ffFlag=-");

    // The log lines of a hold and its redemption, in game ticks (the Toolkit measures in game time).
    internal static string HoldEndLine(string side, string unit, string skill, bool free, double waited, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_HOLD_END side={0} unit={1} skill={2} result={3} waited={4:0.0} tick={5}",
            side, unit, Short(skill), free ? "free" : "blocked", waited, tick);

    internal static string AnnouncedLine(string side, string unit, string skill, string blocker, long tick)
        => blocker == null
            ? string.Format(CultureInfo.InvariantCulture, "CORE_AI_ANNOUNCED_CAST side={0} unit={1} skill={2} tick={3}", side, unit, Short(skill), tick)
            : string.Format(CultureInfo.InvariantCulture, "CORE_AI_ANNOUNCED_DROPPED side={0} unit={1} skill={2} reason={3} tick={4}", side, unit, Short(skill), blocker, tick);

    internal static string VinesLine(bool player, string unit, string result, int dirX, int dirY, string target, bool threat, int near, long tick)
        => string.Format(CultureInfo.InvariantCulture, "{0} unit={1} result={2} dir={3},{4} target={5} threat={6} near={7} tick={8}",
            player ? "CORE_AI_PLAYER_VINES" : "CORE_AI_BANDIT_VINES", unit, result, dirX, dirY, target, threat ? 1 : 0, near, tick);

    // The tail of CORE_AI_BANDIT_MOVE: where the unit stood, the game's order path, the deadline in ticks.
    internal static string MoveOrderFields(int fromX, int fromY, string path, int deadline, long tick)
        => string.Format(CultureInfo.InvariantCulture, " from={0},{1} path={2} deadline={3} tick={4}", fromX, fromY, path, deadline, tick);

    // One line per order at its end; the Toolkit reads it (Test-BanditMoves.ps1).
    internal static string MoveDoneLine(bool player, string unit, int fromX, int fromY, int toX, int toY, int atX, int atY,
        string result, string reason, string path, int moved, long ticks, string task, bool own, string by, int waits, int stops,
        double speed, long tick)
        => string.Format(CultureInfo.InvariantCulture,
            "{0} unit={1} from={2},{3} to={4},{5} at={6},{7} result={8} arrived={9} reason={10} path={11} moved={12} ticks={13} task={14} own={15} by={16} waits={17} stops={18} slowed={19} speed={20:0.00} tick={21}",
            player ? "CORE_AI_PLAYER_MOVE_DONE" : "CORE_AI_BANDIT_MOVE_DONE", unit, fromX, fromY, toX, toY, atX, atY,
            result, result == "arrived" ? "True" : "False", reason, path, moved, ticks, task, own ? 1 : 0, by, waits, stops,
            Slowed(speed) ? 1 : 0, speed, tick);

    internal static string SkipDownedLine(string unit, string downed, string to, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_BANDIT_SKIP_DOWNED unit={0} downed={1} to={2} tick={3}", unit, downed, to, tick);

    internal static string ExcludedRetargetLine(string unit, string to, string source, string action, long tick)
        => $"CORE_AI_BANDIT_RETARGET unit={unit} to={to ?? "-"} rule=skip-excluded source={source} action={action} tick={tick.ToString(CultureInfo.InvariantCulture)}";

    internal static string FocusLine(string unit, string target, string previous, int rank, string role, string reason, long tick)
        => $"CORE_AI_TARGET unit={unit} rule=focus rank={rank} role={role} target={target ?? "-"} previous={previous ?? "-"} reason={reason} tick={tick.ToString(CultureInfo.InvariantCulture)}";

    // Via= the game's input path of a player's order (GroupAiOrderSource), "-" when none was seen; left out when not asked.
    internal static string StanceOrderLine(string unit, string source, string task, string forced, long tick, string via = null)
        => $"CORE_AI_ORDER unit={unit} source={source} task={task ?? "-"} forced={forced ?? "-"}" + (via == null ? "" : " via=" + via)
            + $" tick={tick.ToString(CultureInfo.InvariantCulture)}";

    // The input paths nested at an order, outermost first ("left-click>trymove").
    internal static string OrderVia(IReadOnlyList<string> senders) => senders == null || senders.Count == 0 ? "-" : string.Join(">", senders);

    internal static string ClickSender(bool alt, bool shift) => "left-click" + (shift ? "+shift" : "") + (alt ? "+alt" : "");

    internal static string OrderSourceLine(string unit, string via, string task, long tick)
        => $"CORE_AI_ORDER_SOURCE unit={unit ?? "-"} via={via ?? "-"} task={task ?? "-"} tick={tick.ToString(CultureInfo.InvariantCulture)}";

    // Hp= her share of head and body together, as in CORE_AI_RULE, at the moment of the line.
    internal static string StanceLine(string unit, string mode, int nearest, string to, string reason, long tick, float hp = -1f)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_STANCE unit={0} mode={1} nearest={2} to={3} reason={4}{6} tick={5}",
            unit, mode, nearest == int.MaxValue ? -1 : nearest, to ?? "-", reason ?? "-", tick,
            hp < 0f ? "" : " hp=" + hp.ToString("0.00", CultureInfo.InvariantCulture));

    internal static string HealPathEndLine(string unit, string target, string result, int steps, int walked, float seconds, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_BANDIT_HEALPATH_END unit={0} target={1} result={2} steps={3} walked={4} seconds={5:0.0} tick={6}",
            unit, target, result, steps, walked, seconds, tick);

    internal static string GroupWakeLine(string trigger, int woken, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_BANDIT_GROUP_WAKE trigger={0} woken={1} radius={2} tick={3}", trigger, woken, GroupWakeCells, tick);

    internal static string VinesFilterLine(string unit, int skipped, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_BANDIT_VINES_FILTER unit={0} skipped={1} tick={2}", unit, skipped, tick);

    // A vines segment whose cell is no longer on the caster's line (BanditVines.SegmentGone): its index, the line's length now.
    internal static bool VinesSegmentGone(int index, int tiles) => index < 0 || index >= tiles;

    internal static string VinesShortLine(string unit, int index, int tiles, int x, int y, int cellX, int cellY, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_BANDIT_VINES_SHORT unit={0} index={1} tiles={2} at={3},{4} cell={5},{6} tick={7}",
            unit, index, tiles, x, y, cellX, cellY, tick);

    internal static string PierceFilterLine(string unit, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_BANDIT_PIERCE_FILTER unit={0} tick={1}", unit, tick);

    internal static string AreaDownedLine(string unit, string skill, int downed, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_BANDIT_AREA_DOWNED unit={0} skill={1} downed={2} tick={3}", unit, Short(skill), downed, tick);

    internal static string FinishLine(string unit, string target, string reason, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_BANDIT_FINISH unit={0} target={1} reason={2} tick={3}", unit, target, reason, tick);

    internal static string HealerOrderLine(string unit, bool near, int closest, bool attacked, string rule, int stacks, long tick)
        => string.Format(CultureInfo.InvariantCulture,
            "CORE_AI_BANDIT_HEALER_ORDER unit={0} near={1} closest={2} attacked={3} rule={4} stacks={5} tick={6}",
            unit, near ? "true" : "false", closest, attacked ? "true" : "false", rule, stacks, tick);

    internal static string ProtectLine(string healer, string target, string by, float damage, int hitters, int distance, long tick)
        => string.Format(CultureInfo.InvariantCulture,
            "CORE_AI_BANDIT_PROTECT healer={0} target={1} by={2} dmg3s={3:0} hitters={4} dist={5} tick={6}",
            healer, target, by, damage, hitters, distance, tick);

    internal static string ShieldUpOutsideLine(string unit, string reason, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_SHIELDUP_OUTSIDE unit={0} reason={1} tick={2}", unit, reason ?? "-", tick);

    // Probe: the own slot check (target self for ally skills) next to the game's (target the closest enemy).
    // The game's DecideSkill: no slot during the global cooldown; per slot not in cooldown, auto use, energy
    // (these two were missing), then the use check against a context with target null and the cooldown
    // skipped (NativeSkipsCooldown). The first step that stops it, null when the use check decides; "nodata" and "noskill" when
    // Core cannot read the slot.
    internal const bool NativeSkipsCooldown = true;

    internal static string NativeStep(bool globalCooldown, bool hasData, bool onCooldown, bool autoUse, bool energy, bool hasSkill)
        => globalCooldown ? "gcd" : !hasData ? "nodata" : onCooldown ? "cd" : !autoUse ? "auto" : !energy ? "energy" : !hasSkill ? "noskill" : null;

    internal static string NativeProbeLine(string unit, string slotState, string slotNative, string chosen, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_NATIVE_PROBE unit={0} skill=DelversLifebloom slotState={1} slotNative={2} chosen={3} tick={4}",
            unit, slotState, slotNative, string.IsNullOrEmpty(chosen) ? "-" : Short(chosen), tick);

    internal static string ShieldWallLine(string unit, string reason, string enemy, float share, long tick)
        => string.Format(CultureInfo.InvariantCulture,
            "CORE_AI_SHIELDWALL unit={0} reason={1} enemy={2} hp={3:0.00} tick={4}", unit, reason, enemy, share, tick);

    internal static string TauntOwnLine(string unit, string by, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_BANDIT_TAUNT_OWN unit={0} by={1} skill=ChainGrap tick={2}", unit, by, tick);

    internal static string TauntedLine(string unit, string by, double nativeSeconds, double factor, double heedSeconds, long tick)
        => string.Format(CultureInfo.InvariantCulture,
            "CORE_AI_BANDIT_TAUNTED unit={0} by={1} native={2:0.0} factor={3:0.00} until={4:0.0} tick={5}", unit, by, nativeSeconds, factor, heedSeconds, tick);

    // units: the struck units' GUIDs joined by "+" (summons included; Toolkit sammelblock 185848: targets=3 with two party
    // figures in the ring, the third one not in the party list); renewed: how many of them carried the slow already.
    internal static string SlowRingLine(string unit, int targets, double seconds, long tick, IReadOnlyList<string> units = null, int renewed = 0)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_BANDIT_SLOW_RING unit={0} targets={1} effect={2} seconds={3:0.0} tick={4}",
            unit, targets, SlowKey, seconds, tick)
            + (units == null ? "" : " units=" + (units.Count == 0 ? "-" : string.Join("+", units)) + " renewed=" + renewed.ToString(CultureInfo.InvariantCulture));

    // detail: below or loss for healer_drop (HealerDropReason), else -.
    internal static string TauntEndLine(string unit, string by, string cause, string detail, long tick)
        => string.Format(CultureInfo.InvariantCulture,
            "CORE_AI_BANDIT_TAUNT_END unit={0} by={1} cause={2} detail={3} tick={4}", unit, by, cause, detail ?? "-", tick);

    // The skill of a decision as the rule lines show it: the short key, "basic" for the basic attack (empty key).
    internal static string ChosenSkillText(string skill) => skill.Length > 0 ? Short(skill) : "basic";

    // One line per decision of a bandit (capped by the bandit log); the Toolkit's parsers read unit= kit= rule= skill= target=
    // in this order. hp= and lowest= are shares of health, hostiles= without the downed, downed= the downed foes in reach.
    internal static string BanditRuleLine(string unit, string kit, string rule, string skill, string target, float hp, float lowest,
        int hostiles, int downed, string focus, bool globalCooldown, string state, string slots,
        string skippedSkill, string skippedAlly, string skippedFlag, string lowestUnit, long tick)
        => string.Format(CultureInfo.InvariantCulture,
               "CORE_AI_BANDIT_RULE unit={0} kit={1} rule={2} skill={3} target={4} hp={5:0.00} lowest={6:0.00} hostiles={7} downed={12} focus={8} gcd={9} state={10} slots={11}",
               unit, kit, rule, ChosenSkillText(skill), target, hp, lowest, hostiles, focus, globalCooldown ? 1 : 0, state, slots, downed)
           + SkippedText(skippedSkill, skippedAlly, skippedFlag)
           + " lowestUnit=" + lowestUnit
           + " tick=" + tick.ToString(CultureInfo.InvariantCulture);

    // One line per decision of a party member (capped by the party log); unit= role= rule= skill= target= come first, as the
    // Toolkit's parsers read them. energy= is the current and the maximum energy.
    internal static string PartyRuleLine(string unit, GroupAiRole role, string rule, string skill, string target, float hp, float lowest,
        float energy, float maxEnergy, bool globalCooldown, string state, string slots, string lowestUnit,
        string skippedSkill, string skippedAlly, string skippedFlag)
        => string.Format(CultureInfo.InvariantCulture,
               "CORE_AI_RULE unit={0} role={1} rule={2} skill={3} target={4} hp={5:0.00} lowest={6:0.00} lowestUnit={12} energy={7:0}/{8:0} gcd={9} state={10} slots={11}",
               unit, role, rule, ChosenSkillText(skill), target, hp, lowest, energy, maxEnergy, globalCooldown ? 1 : 0, state, slots, lowestUnit)
           + SkippedText(skippedSkill, skippedAlly, skippedFlag);

    // The healer's Lifebloom outside the global cooldown: the second, shorter format of the party's rule line, always role=Heal.
    internal static string OffGcdRuleLine(string unit, string rule, string skill, string target, string lowestUnit)
        => $"CORE_AI_RULE unit={unit} role=Heal rule={rule} skill={Short(skill)} target={target} lowestUnit={lowestUnit} gcd=1 offgcd=1";

    internal static string AddsFirstLine(string unit, string target, string boss)
        => $"CORE_AI_TARGET unit={unit} rule=adds-first target={target} boss={boss}";

    internal static string SeedBurstLine(string unit, string seed, string skill, float? lowest, long tick)
        => $"CORE_AI_SEED_BURST unit={unit} seed={seed} skill={Short(skill)} lowest={(lowest ?? 1f).ToString("0.00", CultureInfo.InvariantCulture)} tick={tick.ToString(CultureInfo.InvariantCulture)}";

    internal static string LifebloomLine(string unit, string target, int stacks, double secondsLeft, int charges, string rule)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_LIFEBLOOM unit={0} target={1} stacks={2} left={3} charges={4} rule={5}",
            unit, target, stacks, secondsLeft.ToString("0.0", CultureInfo.InvariantCulture), charges, rule);

    // The announcement of an area skill whose zone is held for HoldSeconds; allies= are the GUIDs of the allies inside, joined by "+".
    internal static string AnnounceLine(bool bandit, string unit, string skill, IReadOnlyList<string> allies, string flag)
        => (bandit ? "CORE_AI_BANDIT_ANSAGE" : "CORE_AI_ANSAGE") + $" unit={unit} skill={Short(skill)} allies={string.Join("+", allies)} {flag} hold={HoldSeconds.ToString("0", CultureInfo.InvariantCulture)}s";

    internal static string RoleLine(string unit, GroupAiRole role, string basicAttack, IReadOnlyList<string> slots)
    {
        var shown = new List<string>();
        foreach (var slot in slots) shown.Add(Short(slot));
        return $"CORE_AI_ROLE unit={unit} role={role} basic={basicAttack} slots={string.Join("+", shown)}";
    }

    internal static string BanditKitLine(string unit, string key, string kit)
        => $"CORE_AI_BANDIT_KIT unit={unit} key={key} kit={kit}";

    internal static string BandageGateLine(string unit, string reason, WoundRules.Stage stage)
        => $"CORE_AI_BANDAGE_GATE unit={unit} reason={reason} stage={WoundRules.Describe(stage)}";

    // extra= names the Extended Hotbar reader (type and method) or "none".
    internal static string BandageSlotsLine(string extra) => "CORE_AI_BANDAGE_SLOTS extra=" + extra;

    internal static string VinesProbeLine(int x, int y)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_VINES_PROBE cell={0},{1} blocked=1 occupied=1", x, y);
}
