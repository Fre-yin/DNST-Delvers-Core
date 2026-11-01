namespace DungeonSettlersDelvers.Core;

// The bleeding wound of combat Apocalypse. Every stack of the game's bleeding
// that lands on a party member adds BufferPerStack HP to the wound's buffer; the buffer drains as
// lost body health and the wound closes when it is empty. Short and hard (so long fights cost blood and a wound cannot be ignored). Pure, so the
// offline tests cover it; Wounds applies it in game. All numbers are starting values that the author
// calibrates in game.
internal static partial class WoundRules
{
    // Fatal (in the logs): the wound would bleed out all the health left. It is not read from the buffer
    // alone; Wounds keeps it per unit until the wound is closed.
    internal enum Stage { None = 0, Light = 1, Deep = 2, Heavy = 3, Fatal = 4 }

    // (wound model set C: "one stack is 10 HP"): every bleeding stack is worth 10 health, and the stages
    // measure the buffer against the unit's whole health (body + head): deep from 10 %, heavy from 25 %. The buffer is kept
    // in health, so a save from before (6 a stack) loads as it is; only its stage is read anew from the unit's health.
    internal const double BufferPerStack = 10.0;
    internal const double DeepShare = 0.10;
    internal const double HeavyShare = 0.25;

    // When the unit's health cannot be read (a load before the first tick): about a party member's.
    internal const double DefaultMaxLife = 700.0;

    internal static double MaxLifeOr(double maxLife) => maxLife > 0 && !double.IsInfinity(maxLife) ? maxLife : DefaultMaxLife;

    internal static double DeepFrom(double maxLife) => DeepShare * MaxLifeOr(maxLife);
    internal static double HeavyFrom(double maxLife) => HeavyShare * MaxLifeOr(maxLife);

    // The game clock counts about 60 ticks per second at speed 1 and runs with the game speed
    // (wound probe 57 to 60, 70 after a speed change); pause stops it.
    internal const double TicksPerSecond = 60.0;

    internal static int Stacks(double buffer)
        => buffer <= 0 ? 0 : (int)Math.Ceiling(buffer / BufferPerStack - 1e-9);

    // The stage of a buffer for a unit of the given whole health (body + head).
    internal static Stage StageOf(double buffer, double maxLife)
    {
        if (!(buffer > 0)) return Stage.None;
        if (buffer >= HeavyFrom(maxLife) - 1e-9) return Stage.Heavy;
        return buffer >= DeepFrom(maxLife) - 1e-9 ? Stage.Deep : Stage.Light;
    }

    // The stage of a unit: a fatal wound stays fatal while it is open.
    internal static Stage StageOf(double buffer, double maxLife, bool fatal)
        => !(buffer > 0) ? Stage.None : fatal ? Stage.Fatal : StageOf(buffer, maxLife);

    // ---- the lethal wound ----
    // It begins when the buffer covers all the health left (body + head), and by default only from heavy on: the buffer
    // must also reach the heavy threshold (FromHeavyOnlyKey, on). It ends when the wound is closed, the buffer
    // is empty, or a bandage leaves less buffer than health (LeavesFatal; replacing "no way back"); the stage then follows
    // the thresholds again. Bleeding 2 a second, heals at half, and the game's bleeding
    // stacks are taken into the buffer at 10 health each (TakeOver), on entering and while it lasts.
    internal const string FromHeavyOnlyKey = "wounds.fatal.fromheavyonly";
    internal const double FatalHealFactor = 0.5;

    internal static bool BecomesFatal(double buffer, double maxLife, double life, bool fromHeavyOnly)
    {
        if (!(buffer > 0) || double.IsNaN(life)) return false;
        if (fromHeavyOnly && StageOf(buffer, maxLife) < Stage.Heavy) return false;
        return buffer >= Math.Max(0, life);
    }

    // After a bandage: the lethal wound ends when the buffer no longer covers the health left, the line it began at.
    internal static bool LeavesFatal(double buffer, double life) => !double.IsNaN(life) && buffer < Math.Max(0, life);

    // When a unit goes down, the game itself fills the empty head to
    // DeadlyRecovery (80 %) of its maximum and empties the body (StatusApplier.CalculateSideEffect, HealthHead_Zero).
    // A wound that is lethal already grows by the same amount, so it stays lethal without help and its band covers all.
    // Only that refill counts: a heal without source on an empty head; heals after the fall do not (an open question).
    internal static double FatalRecovery(bool fatal, double headBefore, bool sourced, double amount)
        => fatal && !sourced && !double.IsNaN(headBefore) && headBefore <= 0 && amount > 0 ? amount : 0;

    // The count on the stage's effect: the stacks, but one for the lethal wound; its whole value stands in the health bar's tooltip line.
    internal static int EffectStacks(Stage stage, int stacks) => stage == Stage.Fatal ? 1 : Math.Max(0, stacks);

    // The buffer after taking the game's bleeding stacks in.
    internal static double TakeOver(double buffer, int bleedingStacks) => Hit(buffer, bleedingStacks);

    // The stacks a hit added to the game's bleeding, read from the count before and after the game's own apply. At the game's
    // cap the count no longer rises; then the hit's own stacks count as they came.
    internal static int BleedingAdded(int before, int after, int hitStacks)
    {
        var added = after - before;
        return added <= 0 && after > 0 && after == before ? hitStacks : added;
    }

    // A unit lies when it has AFFECTER_Deadly or is unconscious (the game's category, GroupAiBandits.IsDowned).
    internal static bool IsLying(bool deadly, bool downed) => deadly || downed;

    // Bleeding stacks that landed on the unit.
    internal static double Hit(double buffer, int stacks) => Math.Max(0, buffer) + Math.Max(0, stacks) * BufferPerStack;

    // Seconds of game time per lost point of body health, 20, 60 and 120 a minute; a bed halves the
    // speed.
    internal static double SecondsPerPoint(Stage stage, bool inBed)
    {
        var seconds = stage switch
        {
            Stage.Light => 3.0,
            Stage.Deep => 1.0,
            Stage.Heavy => 0.5,
            Stage.Fatal => 0.5,
            _ => double.PositiveInfinity
        };
        return inBed ? seconds * 2 : seconds;
    }

    // Every stage bleeds on at the head once the body is empty, also at near-death and in the after-effect, so a wound left alone
    // costs the unit (at first only lethal; for the after-effect).

    // The blood loss takes the body first. An empty body does nothing in the game (the unit lives and
    // walks; probe K1), only an empty head brings near-death and then death. So once the
    // body is empty the wound bleeds on at the head.
    internal static bool DrainsHead(double body) => !(body > 0);

    // The fill of the band's image (WoundBars, drawn from its start to the bar's right end) that ends where the bar's own fill ends:
    // both rectangles as the canvas draws them, in the bar's space. A band of no width shows nothing.
    internal static float FillTo(float barMin, float barWidth, float bandMin, float bandWidth, float to)
        => bandWidth > 0f ? Math.Clamp((barMin + to * barWidth - bandMin) / bandWidth, 0f, 1f) : 0f;

    // The band on a health bar (WoundBars) as a share of the bar's width: from the health that is
    // left after the wound to the current fill. The body bar shows the whole buffer, the head bar
    // only the part the body cannot take; null when nothing drains there.
    internal static (float From, float To)? BandShare(bool head, Stage stage, double buffer, double body, double headHealth, float fill)
    {
        if (stage == Stage.None || !(buffer > 0) || !(fill > 0)) return null;
        var health = head ? headHealth : body;
        var drain = head ? buffer - Math.Max(0, body) : buffer;
        if (!(health > 0) || !(drain > 0)) return null;
        return (fill * (float)Math.Clamp((health - drain) / health, 0, 1), fill);
    }

    // While the wound bleeds at the head, the head's own regeneration stops too; otherwise it outheals
    // every stage (about 16 a minute at 134 against 15 for lethal). The portal
    // still heals, and the game's own reset when a unit goes down (an unsourced heal at head 0 to
    // about 80 %) passes (option a).
    internal static bool BlocksHeadRegeneration(Stage stage, double body, double head, bool portal)
        => stage != Stage.None && DrainsHead(body) && head > 0 && !portal;

    // The game tick that lies the given game seconds after tick (TicksPerSecond).
    internal static long TickAfter(long tick, double seconds) => tick + (long)Math.Round(Math.Max(0, seconds) * TicksPerSecond);

    // ---- the wound on the body (the wound shows at the unit, a sparse trail
    // only for a lethal wound; stains fade, a unit keeps only a few) ----
    // A lethal wound leaves a blood stain at a drained point only where the unit has walked at least
    // TrailMinCells since the last stain, so standing still leaves no pool. The game's stains stay
    // until stopped (probe), so each lasts TrailStainSeconds and a unit keeps at most
    // TrailMaxStains. Starting values, the author decides.
    internal const int TrailMinCells = 2;
    internal const float TrailStainSeconds = 60f;
    internal const int TrailMaxStains = 6;

    // Distance in cells, counting diagonal steps as one.
    internal static int Cells(int dx, int dy) => Math.Max(Math.Abs(dx), Math.Abs(dy));

    // (the pool walked along with Frieren): blood on the ground stays where it fell. Every
    // bleeding stage leaves a small stain, sparingly; from a deep wound on the game's pool (Bleeding_Always) instead, laid
    // on the ground and never bound to the unit. A new one only after the unit moved SpotMinCells, each lives SpotSeconds
    // of game time, a unit keeps at most SpotMaxPerUnit. The lethal trail stays as it is.
    internal const int SpotMinCells = 1;
    internal const float SpotSeconds = 6f;
    internal const int SpotMaxPerUnit = 3;

    // "pool", "spot" or null for none.
    // On the ground only the
    // pool from a deep wound on; the small stains moved to the body (BodyMarks).
    // : a new pool only once the unit has stood on its cell PoolStillKey seconds (2 by
    // default); a walking unit got a fresh pool at its feet every cell while the oldest went, which looked like a pool walking
    // along. While it walks only the trail marks the way, from heavy on (LeavesTrail).
    // Standing on the cell of its last pool, the unit gets a new one once that pool is
    // PoolRenewSeconds old, before it goes after SpotSeconds, so a standing, bleeding unit always lies in a pool (at most
    // SpotMaxPerUnit).
    internal static string BloodSpot(Stage stage, bool hasLastSpot, int cellsSinceLastSpot, double stillSeconds = double.PositiveInfinity,
        double needStill = 0, double sinceLastSpot = 0)
        => stage < Stage.Deep || stillSeconds < needStill
            || (hasLastSpot && cellsSinceLastSpot < SpotMinCells && sinceLastSpot < PoolRenewSeconds) ? null : "pool";

    internal const double PoolRenewSeconds = SpotSeconds - 1.5;

    // (carrying: a pool on a cell on the way): a lying unit's marks on the ground follow
    // the same rule, laid only once it has lain still PoolStillKey seconds, so a carried or pulled unit leaves none on the way.
    internal static bool LyingMarksAllowed(double stillSeconds, double needStill) => stillSeconds >= needStill;

    internal const string PoolStillKey = "wounds.fx.poolstillseconds";
    internal const double PoolStillDefault = 2.0;

    // The setting in seconds, 0 to 30; missing or unreadable: 2.
    internal static double PoolStill(double? setting)
        => setting is { } value && !double.IsNaN(value) ? Math.Clamp(value, 0.0, 30.0) : PoolStillDefault;

    // Seconds the unit has stood on its cell: grows while the cell stays, 0 after a step or when nothing is known yet.
    internal static double StillSeconds(bool known, int cellsMoved, double before, double seconds)
        => known && cellsMoved == 0 ? before + Math.Max(0, seconds) : 0;

    // The author's sorting of the blood assets: on the ground HitBloodGround_red_B and _C (VFXID 45, 46) in turn;
    // HitBloodGround_red (44) is not used any more.
    internal static int GroundStain(int counter) => GroundStainFirst + (counter & 1);
    internal const int GroundStainFirst = 45;

    // The picture of a lying unit's body mark, HitBloodGround_red, _B, _C
    // (44-46) in turn; the only place 03 is still used.
    internal static int BodyMarkPicture(int counter) => 44 + counter % 3;

    internal static string GroundLine(string unit, string kind, int vfx, int index, int of, long tick)
        => string.Format(System.Globalization.CultureInfo.InvariantCulture, "CORE_WOUND_FX_GROUND unit={0} kind={1} vfx={2} n={3}/{4} tick={5}",
            unit, kind, vfx, index, of, tick);

    internal static string FxSkipLine(string unit, string reason) => "CORE_WOUND_FX_SKIP unit=" + unit + " reason=" + reason;

    // A death ends all bleeding on the unit (the corpse still showed the bleeding symbol): there is
    // something to end while Core's buffer, the game's bleeding stacks or a stage effect is left on it.
    internal static bool EndsAtDeath(double buffer, int bleeding, int effectStacks) => buffer > 0 || bleeding > 0 || effectStacks > 0;

    // CORE_WOUND_DEATH: what a death (source=death) or a corpse in a loaded save (source=load) took off the unit.
    internal static string DeathLine(string unit, string source, bool party, double buffer, int bleeding, int effectStacks)
        => string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "CORE_WOUND_DEATH unit={0} source={1} party={2} buffer={3:0.0}->0 bleeding={4}->0 effect={5}->0",
            unit, source, party ? 1 : 0, buffer, bleeding, effectStacks);

    // The red scratches bound to the bleeding unit's body, one per stage (light 1, deep 2, lethal 3), each living
    // SpotSeconds and renewed while the wound bleeds.
    internal static int BodyMarks(Stage stage) => stage switch
    {
        Stage.Light => 1,
        Stage.Deep => 2,
        Stage.Heavy => 3,
        Stage.Fatal => 3,
        _ => 0
    };

    // The trail of stains: heavy and lethal.
    internal static bool LeavesTrail(Stage stage, bool hasLastStain, int cellsSinceLastStain)
        => stage >= Stage.Heavy && (!hasLastStain || cellsSinceLastStain >= TrailMinCells);

    // Whole points to drain for the elapsed game seconds; the rest carries over to the next call.
    internal static int Drain(Stage stage, bool inBed, double seconds, ref double carry)
    {
        var per = SecondsPerPoint(stage, inBed);
        if (double.IsInfinity(per) || !(seconds > 0))
        {
            if (stage == Stage.None) carry = 0;
            return 0;
        }
        carry += seconds / per;
        var points = (int)Math.Floor(carry);
        carry -= points;
        return points;
    }

    // (variant B "strongly staged"): on a lethal wound a heal works at 50 % while the buffer is at most the
    // unit's whole health, then at 50 % × whole health / buffer, never below 25 %; read anew at every heal.
    internal const double FatalHealFloor = 0.25;

    internal static double FatalFactor(double buffer, double maxLife)
    {
        var max = MaxLifeOr(maxLife);
        return !(buffer > max) ? FatalHealFactor : Math.Max(FatalHealFloor, FatalHealFactor * max / buffer);
    }

    // The factor of a heal with a source for this wound.
    internal static double HealFactor(Stage stage, double buffer, double maxLife)
        => stage == Stage.Fatal ? FatalFactor(buffer, maxLife) : HealFactor(stage);

    // Incoming heals on the body are weaker by stage (90, 80, 70 and 50 %; bleeding drains slowly but
    // should brake healing noticeably).
    internal static double HealFactor(Stage stage) => stage switch
    {
        Stage.Light => 0.90,
        Stage.Deep => 0.80,
        Stage.Heavy => 0.70,
        Stage.Fatal => FatalHealFactor,
        _ => 1.0
    };

    internal enum HealKind { Sourced, Regeneration, Portal }

    // Below this, a capped heal leaves no overheal (float rounding of the game's split).
    internal const double OverhealEpsilon = 0.01;

    // A heal with a source has an attacker's From or comes from the game's heal
    // execution (HealSources); without both it is the portal's aura or else regeneration.
    internal static HealKind KindOf(bool from, bool execution, bool portal)
        => from || execution ? HealKind.Sourced : portal ? HealKind.Portal : HealKind.Regeneration;

    // CORE_WOUND_HEAL: one heal on a wounded unit, before (in) and after (out) the wound, the healed
    // stat's free health before it (room; the game caps out there), and what it took from the buffer.
    // stat: body, head, or over (beyond full health); src: exec (the game's heal execution), from (a
    // source on the change), lifebloom (its tick at full health) or none.
    internal static string HealLine(string unit, string stat, string source, HealKind kind, Stage stage, double amountIn, double amountOut,
        double room, double buffer, long tick, double factor = double.NaN)
        => string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "CORE_WOUND_HEAL unit={0} stat={1} src={2} kind={3} tier={4} in={5:0.0} out={6:0.0} room={7:0.0} buffer={8:0.0} tick={9}",
            unit, stat, source, kind == HealKind.Sourced ? "sourced" : kind == HealKind.Portal ? "portal" : "regen",
            stage.ToString().ToLowerInvariant(), amountIn, amountOut, Room(room), buffer, tick)
            + (double.IsNaN(factor) ? "" : " factor=" + factor.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));

    // Free health as logged: never negative, 0 when unknown.
    internal static double Room(double room) => double.IsNaN(room) || room < 0 ? 0 : room;

    // What a heal on the body of a wounded unit does: how much health it gives and how much of the
    // buffer it takes.
    // - Regeneration without a source stops while the wound is open (variant A).
    // - The portal's aura heals as usual and takes the same amount from the buffer.
    // - Heals with a source (skills, items) are weaker by stage; what goes beyond full health
    //   (overheal) takes the buffer instead (only heals with a source count).
    // factor: the heal's factor for this wound (HealFactor with buffer and whole health).
    internal static (double Heal, double Buffer) Heal(Stage stage, HealKind kind, double amount, double current, double max, double factor)
    {
        if (stage == Stage.None || !(amount > 0)) return (Math.Max(0, amount), 0);
        switch (kind)
        {
            case HealKind.Regeneration:
                return (0, 0);
            case HealKind.Portal:
                return (amount, amount);
            default:
                var heal = amount * factor;
                var room = Math.Max(0, max - Math.Max(0, current));
                return (heal, Math.Max(0, heal - room));
        }
    }

    // ---- the roll of a bleeding hit (a good start, calibrated after the first
    // measurements) ----
    // On top of the game's stacks, every bleeding that lands on a party member rolls once: mostly
    // nothing, sometimes a deep cut, rarely a jump to the next stage. Bosses and elites roll with
    // twice the chances; a unit jumps at most once in JumpCooldownSeconds of game time, a jump
    // inside that time counts as a deep cut. The game itself does not roll: its bleeding skills
    // give fixed stacks (rat 3, gloth 2, blood tear 3, blood rain 2 per hit).
    internal enum Roll { None, Extra, Jump }

    internal const double ExtraChance = 0.12;
    internal const double JumpChance = 0.03;
    internal const double EliteChanceFactor = 2.0;
    internal const int ExtraStacks = 3;
    internal const double JumpCooldownSeconds = 30.0;

    // The bleeding trait of the enemy round: chance per hit to bleed two stacks (one
    // before), then the same roll.
    internal const double TraitBleedChance = 0.30;
    internal const int TraitBleedStacks = 2;

    // value is uniform in [0, 1).
    internal static Roll RollOf(double value, bool elite)
    {
        var factor = elite ? EliteChanceFactor : 1.0;
        var jump = JumpChance * factor;
        if (value < jump) return Roll.Jump;
        return value < jump + ExtraChance * factor ? Roll.Extra : Roll.None;
    }

    internal static Roll Limit(Roll roll, double secondsSinceJump)
        => roll == Roll.Jump && secondsSinceJump < JumpCooldownSeconds ? Roll.Extra : roll;

    // The buffer after the roll: a deep cut adds ExtraStacks; a jump lifts a light wound to the
    // threshold of deep and a deep one to the threshold of heavy (set C, of the unit's whole health), and adds ExtraStacks
    // to a heavy one. A jump never makes a wound lethal: that stage comes from the health left.
    internal static double AfterRoll(double buffer, Roll roll, double maxLife)
    {
        switch (roll)
        {
            case Roll.Extra:
                return Hit(buffer, ExtraStacks);
            case Roll.Jump:
                var stage = StageOf(buffer, maxLife);
                if (stage >= Stage.Heavy) return Hit(buffer, ExtraStacks);
                var first = stage == Stage.Deep ? HeavyFrom(maxLife) : DeepFrom(maxLife);
                return Math.Max(Math.Max(0, buffer), first);
            default:
                return Math.Max(0, buffer);
        }
    }

    internal static string Describe(Roll roll) => roll switch
    {
        Roll.Extra => "extra",
        Roll.Jump => "jump",
        _ => "none"
    };

    internal static string Describe(Stage stage) => stage switch
    {
        Stage.Light => "light",
        Stage.Deep => "deep",
        Stage.Heavy => "heavy",
        Stage.Fatal => "lethal",
        _ => "none"
    };
}
