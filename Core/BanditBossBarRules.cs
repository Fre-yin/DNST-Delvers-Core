using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// (17./): the forest bandits' boss bar in the game's own boss HUD. One segment per bandit there when it opens, in the order
// slayer, shieldbearer, archer, herbalist ("the slayer first"); a fallen one is empty and dark. Under each segment its bandit's
// effects, a second row when one does not hold them ("two rows when buffs and debuffs exceed the segment"). Boss music as the
// game's own start. Each fall raises SegmentFell for effects to come (none yet). Rules without the game.
internal static class BanditBossBarRules
{
    internal static readonly string[] RoleOrder = { "blade", "guard", "archer", "healer" };
    internal const int Segments = 4;
    // The game's body bar (probe, self-test): 500 wide, its fill 6 narrower, the effect icons 32 wide.
    internal const float BarWidth = 500f;
    internal const float FillInset = 6f;
    internal const float Gap = 8f;
    internal const float IconWidth = 32f;
    internal const int RowsPerSegment = 2;
    internal const string NameKey = "TEXTKEY_DelversCore_ForestBandits_NAME";

    internal static readonly UniqueCandidateTranslations Name = new(
        "Forest Bandits", "숲 도적단", "Bandits des bois", "Waldbanditen", "Лесные бандиты",
        "森林强盗团", "森林強盜團", "森の盗賊団", "Bandidos del bosque", "Bandidos da floresta");

    internal static int SlotOf(string role) => Array.IndexOf(RoleOrder, role);

    // One segment per bandit there when the bar opens (with one bandit the three empty segments fell at
    // once): the role slots that hold a bandit, in the bar's order. At most Segments.
    internal static int[] PresentSlots(bool[] filled)
    {
        var slots = new List<int>();
        for (var i = 0; filled != null && i < Math.Min(filled.Length, Segments); i++) if (filled[i]) slots.Add(i);
        return slots.ToArray();
    }

    private static int Count(int count) => Math.Clamp(count, 1, Segments);

    // A segment's width when the bar holds count segments: the field (width, the body bar's by default) shared, Gap between them.
    internal static float SegmentWidth(int count, float width = BarWidth) => (width - Gap * (Count(count) - 1)) / Count(count);

    // The centre of a segment, from the bar's centre.
    internal static float SegmentCenter(int index, int count, float width = BarWidth)
        => -width / 2f + SegmentWidth(count, width) / 2f + index * (SegmentWidth(count, width) + Gap);

    internal static float SegmentLeft(int index, int count, float width = BarWidth) => SegmentCenter(index, count, width) - SegmentWidth(count, width) / 2f;

    // Effect icons in one row of a segment.
    internal static int IconsPerRow(int count, float width = BarWidth) => Math.Max(1, (int)Math.Floor(SegmentWidth(count, width) / IconWidth));

    // (2560x1440: the panel reaches into the party bar): Core's segment field as setting bandits.bossbar.width,
    // the body bar's 500 by default, 200 to 500; the game's own panel is not touched. The author judges at the picture.
    internal const string WidthKey = "bandits.bossbar.width";
    internal const float MinWidth = 200f;

    internal static float Width(double? setting)
        => setting is double v && !double.IsNaN(v) ? (float)Math.Clamp(v, MinWidth, BarWidth) : BarWidth;

    // One bar's fill the way the game's boss HUD sets it (BossHUDPresenter.RefreshOnFrame: _headHP = HealthHead / MaxHealthHead,
    // _bodyHP = HealthBody / MaxHealthBody), 0 for a fallen bandit or an unknown maximum.
    internal static float BarFill(float value, float max, bool dead)
        => dead || float.IsNaN(max) || !(max > 0f) || float.IsNaN(value) ? 0f : Math.Clamp(Math.Max(0f, value) / max, 0f, 1f);

    // A colour as RRGGBBAA for the log. Not ColorUtility.ToHtmlStringRGBA: the game's build has stripped it, the call threw
    // "Method unstripping failed" in the boss bar's start (no bar in two runs). A source check in the tests keeps the call out.
    internal static string ColourText(float r, float g, float b, float a)
    {
        static int Byte(float v) => (int)Math.Round(Math.Clamp(float.IsNaN(v) ? 0f : v, 0f, 1f) * 255f);
        return Byte(r).ToString("X2") + Byte(g).ToString("X2") + Byte(b).ToString("X2") + Byte(a).ToString("X2");
    }

    // The game's large boss ring and the normal height of a HUD's lower pivot (BanditUnitHudInitPatch; UnitEntityHUD.Init: ring, height).
    internal const string HugeRing = "SelectionMarker_Huge";
    internal const float NormalLowerY = -0.359375f;

    // The shield is a half transparent band over the health, honeycombed like the
    // game's AFFECTER_Shell. , at the picture of the shell's own sprite tiled. The shell's sprite wraps a figure, so Core draws its
    // own flat honeycomb in the shell's look (HoneyKind, HoneyColours); no game image. The names stand above the bars, not in
    // them (LabelGap of the label's height between).
    // (pictures at 0.55, 0.7, 0.85): 0.85 by default.
    internal const float ShieldAlpha = 0.85f;

    // The band's opacity as setting bandits.bossbar.shield (0 to 1, ShieldAlpha
    // by default), so Toolkit can show values side by side and the author picks one at the picture.
    internal const string ShieldKey = "bandits.bossbar.shield";

    internal static float ShieldOpacity(double? setting)
        => setting is double v && !double.IsNaN(v) ? (float)Math.Clamp(v, 0.0, 1.0) : ShieldAlpha;

    // The segments, their head bars, names and effect rows sit this far below the game's bars (UI units), so the
    // names keep a gap to the title. Setting bandits.bossbar.drop; the author judges.
    internal const string DropKey = "bandits.bossbar.drop";
    internal const float DropDefault = 16f;
    internal const float MaxDrop = 200f;

    internal static float Drop(double? setting)
        => setting is double v && !double.IsNaN(v) ? (float)Math.Clamp(v, 0.0, MaxDrop) : DropDefault;
    internal const float LabelGap = 0.15f;

    // The label's centre height above the highest bar top of its segment (world units).
    internal static float LabelCenterY(float barTop, float labelHeight) => barTop + labelHeight * (0.5f + LabelGap);

    // A copy of the game's head bar per segment: its width the segment's share of the head bar as the body bar's share of the
    // body bar, its offset from the body bar scaled the same way.
    internal static float HeadWidth(float headWidth, float bodyWidth, int count, float width = BarWidth)
        => bodyWidth > 0f ? headWidth * SegmentWidth(count, width) / bodyWidth : SegmentWidth(count, width);

    // The game has one
    // boss HUD for one unit only (BossHUDView: one _bossNameText, one head and one body bar), no view for several bosses. Variant 1:
    // above each segment a copy of the game's own boss name text with the name the game shows over that bandit's head
    // (TEXTKEY_<unit>_NAME), LabelScale of the title's size. Setting bandits.bossbar.names, 0 turns it off.
    internal const float LabelScale = 0.45f;

    internal static float LabelWidth(int count, float width = BarWidth) => SegmentWidth(count, width) / LabelScale;

    // The share of a bandit's health left, head and body together; 0 when unreadable or dead.
    internal static float Fill(float head, float body, float maxHead, float maxBody, bool dead)
    {
        var max = maxHead + maxBody;
        if (dead || float.IsNaN(max) || !(max > 0f) || float.IsNaN(head) || float.IsNaN(body)) return 0f;
        return Math.Clamp((Math.Max(0f, head) + Math.Max(0f, body)) / max, 0f, 1f);
    }

    // How many effects go into each row (first, second) and how many are left out.
    internal static (int First, int Second, int Left) Rows(int count, int segments, float width = BarWidth)
    {
        var n = Math.Max(0, count);
        var perRow = IconsPerRow(segments, width);
        var first = Math.Min(n, perRow);
        var second = Math.Min(n - first, perRow);
        return (first, second, n - first - second);
    }

    // The game's boss bar ends only when no bandit of the party is left alive.
    internal static bool KeepsOpen(bool active, int alive) => active && alive > 0;

    // The game's boss start (BossHUDPresenter.Init) while Core's bar may be open; alive counts the bar's bandits alive on the map
    // now (a bar left from a wiped fight must not swallow the next one's start).
    // "run": the game's start runs (no bar open); "skip": a bandit of the bar, or one whose role the bar already shows, while one
    // of the bar lives; "rebuild": a bandit of a role the bar does not show yet while
    // one of the bar lives, the bar closes and the start runs anew with every bandit present, at most once per
    // game tick (four joins in one moment started the boss music four times; it may restart); "stale": a
    // bandit while none of the bar lives, the bar closes and the start runs anew; "other-boss": another boss, the bar closes.
    internal static string OnStart(bool active, bool bandit, int alive, bool newMember = false, bool rebuiltThisTick = false)
        => !active ? "run" : !bandit ? "other-boss" : alive > 0 ? (newMember && !rebuiltThisTick ? "rebuild" : "skip") : "stale";

    // A rebuild keeps the bar's bandits, the fallen ones too (an empty, dark segment as with the game's bosses);
    // a role the bar did not show takes the bandit present now. Per slot: the kept one, else the new one.
    internal static T[] KeepSlots<T>(T[] kept, T[] present) where T : class
    {
        var slots = new T[Segments];
        for (var i = 0; i < Segments; i++)
            slots[i] = (kept != null && i < kept.Length ? kept[i] : null) ?? (present != null && i < present.Length ? present[i] : null);
        return slots;
    }

    // The setting bandits.bossbar.view: only 0 turns Core's view off; unset or unreadable keeps it on.
    internal static bool ViewOn(double? setting) => setting is not double v || double.IsNaN(v) || v != 0.0;

    internal static string Line(string at, string detail) => "CORE_BANDIT_BOSSBAR at=" + at + (string.IsNullOrEmpty(detail) ? "" : " " + detail);

    internal static string SegmentLine(int index, string role, string unit, string reason)
        => string.Format(CultureInfo.InvariantCulture, "CORE_BANDIT_BOSSBAR at=segment index={0} role={1} unit={2} reason={3}", index, role ?? "-", unit ?? "-", reason);
}
