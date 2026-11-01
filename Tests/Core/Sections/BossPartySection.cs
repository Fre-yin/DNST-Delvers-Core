using DungeonSettlersDelvers.Core;
using static TestKit;

// The boss party counts as bosses.
internal static class BossPartySection
{
    internal static void Run()
    {
        // (17./): the boss party counts as bosses; one segment per bandit, the slayer first, effects in two rows.
        Check(CoreUnitRules.BossTrait == "AFFECTER_BossMonster"
                && CoreUnitRules.Units.All(u => CoreUnitRules.TraitsFor(u).SequenceEqual(new[] { CoreUnitRules.EliteTrait, CoreUnitRules.BossTrait }))
                && CoreUnitRules.TraitsFor(null).SequenceEqual(new[] { CoreUnitRules.EliteTrait })
                && BanditBossBarRules.RoleOrder.SequenceEqual(new[] { "blade", "guard", "archer", "healer" })
                && CoreUnitRules.Units.All(u => BanditBossBarRules.SlotOf(u.Role) >= 0) && BanditBossBarRules.SlotOf("blade") == 0 && BanditBossBarRules.SlotOf("x") == -1
                && BanditBossBarRules.SegmentWidth(4) == 119f && BanditBossBarRules.SegmentCenter(0, 4) == -190.5f && BanditBossBarRules.SegmentCenter(3, 4) == 190.5f
                && BanditBossBarRules.SegmentLeft(0, 4) == -250f && BanditBossBarRules.IconsPerRow(4) == 3
                && Math.Abs(ShieldRules.TileMultiplier(16f, 16f, 100f, 3f, 1f) - 33.333f) < 0.01f && ShieldRules.TileMultiplier(0f, 16f, 100f, 3f, 1f) == 1f
                && ShieldRules.ShieldWidth(28f, 0.5f) == 14f && ShieldRules.ShieldWidth(28f, 2f) == 28f && ShieldRules.ShieldWidth(28f, float.NaN) == 0f
                && BanditBossBarRules.Drop(null) == BanditBossBarRules.DropDefault && BanditBossBarRules.Drop(-5) == 0f && BanditBossBarRules.Drop(999) == BanditBossBarRules.MaxDrop
                && BanditBossBarRules.Drop(24) == 24f
                // One segment per bandit there at the start: 1, 2 and 3 bandits share the whole bar.
                && BanditBossBarRules.SegmentWidth(1) == 500f && BanditBossBarRules.SegmentCenter(0, 1) == 0f && BanditBossBarRules.IconsPerRow(1) == 15
                && BanditBossBarRules.SegmentWidth(2) == 246f && BanditBossBarRules.SegmentCenter(0, 2) == -127f && BanditBossBarRules.SegmentCenter(1, 2) == 127f
                && BanditBossBarRules.IconsPerRow(2) == 7
                && Math.Abs(BanditBossBarRules.SegmentWidth(3) - 484f / 3f) < 0.001f && BanditBossBarRules.IconsPerRow(3) == 5
                && Math.Abs(BanditBossBarRules.SegmentLeft(0, 3) + 250f) < 0.001f
                && Math.Abs(BanditBossBarRules.SegmentLeft(2, 3) + BanditBossBarRules.SegmentWidth(3) - 250f) < 0.001f
                && BanditBossBarRules.SegmentWidth(0) == 500f && BanditBossBarRules.SegmentWidth(9) == 119f
                && BanditBossBarRules.PresentSlots(new[] { true, false, false, false }).SequenceEqual(new[] { 0 })
                && BanditBossBarRules.PresentSlots(new[] { false, true, false, true }).SequenceEqual(new[] { 1, 3 })
                && BanditBossBarRules.PresentSlots(new[] { true, true, true, false }).SequenceEqual(new[] { 0, 1, 2 })
                && BanditBossBarRules.PresentSlots(new[] { true, true, true, true }).SequenceEqual(new[] { 0, 1, 2, 3 })
                && BanditBossBarRules.PresentSlots(new bool[4]).Length == 0 && BanditBossBarRules.PresentSlots(null).Length == 0
                && BanditBossBarRules.Rows(20, 1) == (15, 5, 0) && BanditBossBarRules.Rows(16, 2) == (7, 7, 2)
                && BanditBossBarRules.Fill(100, 300, 200, 600, false) == 0.5f && BanditBossBarRules.Fill(100, 300, 200, 600, true) == 0f
                && BanditBossBarRules.Fill(-5, 0, 200, 600, false) == 0f && BanditBossBarRules.Fill(1000, 1000, 200, 600, false) == 1f
                && BanditBossBarRules.Fill(float.NaN, 1, 1, 1, false) == 0f && BanditBossBarRules.Fill(1, 1, 0, 0, false) == 0f
                && BanditBossBarRules.Rows(0, 4) == (0, 0, 0) && BanditBossBarRules.Rows(2, 4) == (2, 0, 0) && BanditBossBarRules.Rows(5, 4) == (3, 2, 0) && BanditBossBarRules.Rows(8, 4) == (3, 3, 2)
                && BanditBossBarRules.KeepsOpen(true, 1) && !BanditBossBarRules.KeepsOpen(true, 0) && !BanditBossBarRules.KeepsOpen(false, 3)
                && BanditBossBarRules.OnStart(false, true, 0) == "run" && BanditBossBarRules.OnStart(false, false, 2) == "run"
                && BanditBossBarRules.OnStart(true, true, 1) == "skip" && BanditBossBarRules.OnStart(true, true, 0) == "stale"
                && BanditBossBarRules.OnStart(true, false, 3) == "other-boss"
                && BanditBossBarRules.OnStart(true, true, 1, true) == "rebuild" && BanditBossBarRules.OnStart(true, true, 0, true) == "stale"
                && BanditBossBarRules.OnStart(true, true, 1, true, true) == "skip" && BanditBossBarRules.OnStart(true, true, 0, true, true) == "stale"
                && BanditBossBarRules.KeepSlots(new[] { "A", null, "deadC", null }, new[] { "x", "B", "y", null }).SequenceEqual(new[] { "A", "B", "deadC", null })
                && BanditBossBarRules.KeepSlots<string>(null, new[] { "x", "B" }).SequenceEqual(new[] { "x", "B", null, null })
                && BanditBossBarRules.Width(null) == 500f && BanditBossBarRules.Width(420) == 420f && BanditBossBarRules.Width(50) == BanditBossBarRules.MinWidth
                && BanditBossBarRules.Width(900) == 500f && BanditBossBarRules.SegmentWidth(4, 420f) == 99f && BanditBossBarRules.SegmentLeft(0, 4, 420f) == -210f
                && CoreUnitRules.Units.All(CoreUnitRules.IsBossParty) && !CoreUnitRules.IsBossParty(null)
                && BanditBossBarRules.OnStart(false, true, 0, true) == "run" && BanditBossBarRules.OnStart(true, false, 1, true) == "other-boss"
                && BanditBossBarRules.ShieldOpacity(null) == BanditBossBarRules.ShieldAlpha && BanditBossBarRules.ShieldOpacity(0.85) == 0.85f
                && BanditBossBarRules.ShieldAlpha == 0.85f && ShieldRules.ShieldCell(null) == 12f && ShieldRules.ShieldCell(48) == 48f
                && ShieldRules.ShieldCell(2) == ShieldRules.ShieldCellMin && ShieldRules.ShieldCell(500) == ShieldRules.ShieldCellMax
                && Math.Abs(ShieldRules.TileMultiplier(64f, 16f, 100f, 32f, 1f) - 12.5f) < 0.001f
                && ShieldRules.ShieldBand(-64f, 8f, 32f) == (-28f, 72f) && ShieldRules.ShieldBand(8f, -8f, 32f) == (0f, 32f)
                && ShieldRules.ShieldBand(-8f, 8f, 8f) == (0f, 16f)
                && ShieldRules.ShieldEdges(-60f, 53f, true, -61f, 55f) == (-61f, 55f) && ShieldRules.ShieldEdges(-60f, 53f, false, -90f, 90f) == (-60f, 53f)
                && ShieldRules.ShieldBorder > 0f && BanditBossBarRules.ViewOn(null)
                && BanditBossBarRules.Width(double.NaN) == BanditBossBarRules.BarWidth && ShieldRules.ShieldCell(double.NaN) == ShieldRules.ShieldCellDefault
                && Math.Abs(ShieldRules.TileMultiplier(ShieldRules.HoneyHeight, 16f, 100f, 34f, ShieldRules.TileRows(34f, 12f)) - 7.721f) < 0.001f
                && ShieldRules.ShieldLayerCount == 2 && ShieldRules.ShieldLayers(1.5f, ShieldRules.ShieldLayerCount) == (0, 1, 0.5f)
                && ShieldRules.ShieldLayers(2f, ShieldRules.ShieldLayerCount) == (0, 1, 1f) && ShieldRules.ShieldLayers(2.4f, 2) == (0, 1, 1f)
                && ShieldRules.ShieldColourKey(1) == "ui.shield.colour.2" && ShieldRules.ShieldColourDefault(1) == 0x8FD3FF
                && ShieldRules.ShieldColourDefault(5) == 0x8FD3FF && ShieldRules.ShieldColourDefault(-1) == 0xFFFFFF
                && ShieldRules.BloodColourKey == "ui.shield.colour.blood" && ShieldRules.BloodColourDefault == 0xF04848
                && ShieldRules.BloodSections(100f, 0.25f, 0.5f) == (50f, 50f, 25f, 0) && ShieldRules.BloodSections(100f, 1.5f, 0.5f) == (25f, 25f, 75f, 1)
                && ShieldRules.BloodSections(100f, 0.5f, 1.5f) == (75f, 75f, 25f, 0) && ShieldRules.BloodSections(100f, 0f, 0.5f) == (50f, 50f, 0f, -1)
                && ShieldRules.BloodSections(100f, 0.75f, 0.25f) == (25f, 25f, 75f, 0)
                && BossEffectRules.StillPending(false, 105, 100) && !BossEffectRules.StillPending(true, 101, 100)
                && !BossEffectRules.StillPending(false, 100 + BossEffectRules.AppliedGraceTicks + 1, 100)
                && BossEffectRules.CapRaisePercent(1000f, 0f, 500f, 400) == 0f
                && Math.Abs(BossEffectRules.CapRaisePercent(1000f, 0f, 2000f, 400) - 20.05f) < 0.01f
                && Math.Abs(BossEffectRules.CapRaisePercent(1000f, -33f, 1900f, 300) - 6.7335f) < 0.01f
                && ShieldRules.PartyShieldOpacity(null) == 0.55f && ShieldRules.PartyShieldOpacity(0.6) == 0.6f && ShieldRules.PartyShieldOpacity(3) == 1f
                && ShieldRules.PartyFillOpacity(null) == 0.1f && ShieldRules.PartyFillOpacity(-1) == 0f
                && ShieldRules.LayerOpacity(0.5f, 0, null) == 0.5f && Math.Abs(ShieldRules.LayerOpacity(0.5f, 1, null) - 0.65f) < 1e-5f
                && ShieldRules.LayerOpacity(0.8f, 1, 2.0) == 1f && ShieldRules.LayerOpacity(0.5f, 1, 0.5) == 0.5f
                && ShieldRules.CellAlpha(0.05f, 0.5f) == 26 && ShieldRules.CellAlpha(0.8f, 0.4f) == 255 && ShieldRules.CellAlpha(0.1f, 0f) == 0
                && ShieldRules.WaveFactor(0f, 0f, 60f, 1.6f, 0.55f) == 1f && Math.Abs(ShieldRules.WaveFactor(30f, 0f, 60f, 1.6f, 0.55f) - 0.55f) < 1e-5f
                && Math.Abs(ShieldRules.WaveFactor(60f, 1.6f, 60f, 1.6f, 0.55f) - 1f) < 1e-5f && Math.Abs(ShieldRules.WaveFactor(15f, 0f, 60f, 1.6f, 0.55f) - 0.775f) < 1e-5f
                && Math.Abs(ShieldRules.WaveFactor(0f, 0.8f, 60f, 1.6f, 0.35f) - 0.35f) < 1e-5f && ShieldRules.WaveFactor(10f, 0f, 0f, 1.6f, 0.5f) == 1f
                && Enumerable.Range(0, 120).All(x => ShieldRules.WaveFactor(x, 0.3f, 48f, 1.6f, 0.35f) is var f && f >= 0.35f - 1e-5f && f <= 1f + 1e-5f)
                && ShieldRules.WaveSettings(false, null, null, null, null) == (true, 0.55f, 60f, 1.6f)
                && ShieldRules.WaveSettings(true, null, null, null, null) == (true, 0.35f, 48f, 1.6f)
                && ShieldRules.WaveSettings(false, 0, 2, 5, 9) == (false, 1f, 16f, 6f)
                && ShieldRules.WaveStrips(119f) == 30 && ShieldRules.WaveStrips(0f) == 1 && ShieldRules.WaveFailureLimit == 3
                && ShieldRules.Overlaps(0, 0, 10, 10, 5, 5, 15, 15) && !ShieldRules.Overlaps(0, 0, 10, 10, 10, 0, 20, 10)
                && !ShieldRules.Overlaps(0, 0, 10, 10, 0, 11, 10, 20) && ShieldRules.Overlaps(0, 0, 10, 10, 2, 2, 3, 3)
                && ShieldRules.LowerSpan(100f, 0.5f) == (50f, 50f) && ShieldRules.LowerSpan(100f, 1f) == (100f, 0f)
                && ShieldRules.LowerSpan(100f, 0f) == (0f, 100f) && ShieldRules.LowerSpan(float.NaN, 0.5f) == (0f, 0f)
                && !ShieldRules.ShieldShown(0f) && !ShieldRules.ShieldShown(2f) && ShieldRules.ShieldShown(2.5f) && !ShieldRules.ShieldShown(float.NaN)
                && ShieldRules.ShieldLayers(0f, 3) == (-1, -1, 0f) && ShieldRules.ShieldLayers(0.5f, 3) == (-1, 0, 0.5f)
                && ShieldRules.ShieldLayers(1f, 3) == (-1, 0, 1f) && ShieldRules.ShieldLayers(1.5f, 3) == (0, 1, 0.5f)
                && ShieldRules.ShieldLayers(2f, 3) == (0, 1, 1f) && ShieldRules.ShieldLayers(2.5f, 3) == (1, 2, 0.5f)
                && ShieldRules.ShieldLayers(4f, 3) == (1, 2, 1f) && ShieldRules.ShieldLayers(float.NaN, 3) == (-1, -1, 0f)
                && ShieldRules.ShieldColour(null, 0x8FD3FF) == (0x8F / 255f, 0xD3 / 255f, 1f) && ShieldRules.ShieldColour(0xFF0000, 0) == (1f, 0f, 0f)
                && ShieldRules.ShieldColour(-1, 0x00FF00) == (0f, 1f, 0f) && ShieldRules.ShieldColour(1.5, 0x0000FF) == (0f, 0f, 1f)
                && ShieldRules.ShieldShare(300f, 200f, false) == 1.5f && ShieldRules.ShieldShare(300f, 200f, true) == 0f
                && ShieldRules.ShieldShare(50f, 0f, false) == 0f && ShieldRules.ShieldShare(50f, float.NaN, false) == 0f
                && ShieldRules.ShieldPreview(1.5, true) == 1.5f && ShieldRules.ShieldPreview(1.5, false) == null
                && ShieldRules.ShieldPreview(9, true) == 2f && ShieldRules.ShieldPreview(null, true) == null
                && ShieldRules.TileRows(72f, 24f) == 3 && ShieldRules.TileRows(72f, 32f) == 2 && ShieldRules.TileRows(72f, 48f) == 2
                && ShieldRules.TileRows(10f, 32f) == 1 && ShieldRules.TileRows(0f, 32f) == 1
                && ShieldRules.HoneyKind(0, 0) == 0 && ShieldRules.HoneyKind(12, 7) == 0 && ShieldRules.HoneyKind(6, 3) == 1
                && ShieldRules.HoneyKind(0, 7) == 1 && ShieldRules.HoneyKind(6 + 24, 3 - 14) == 1 && ShieldRules.HoneyKind(-24, 14) == 0
                && Enumerable.Range(0, 24 * 14).Count(i => ShieldRules.HoneyKind(i % 24, i / 24) == 1) == 72
                && Enumerable.Range(0, 24 * 14).Count(i => ShieldRules.HoneyKind(i % 24, i / 24) == 2) == 40
                && BanditBossBarRules.ShieldOpacity(2) == 1f && BanditBossBarRules.ShieldOpacity(-1) == 0f && BanditBossBarRules.ShieldOpacity(double.NaN) == BanditBossBarRules.ShieldAlpha
                && BanditBossBarRules.ViewOn(null) && BanditBossBarRules.ViewOn(1) && BanditBossBarRules.ViewOn(double.NaN) && !BanditBossBarRules.ViewOn(0)
                && BanditBossBarRules.Name.AllLanguages.Count == 10 && BanditBossBarRules.Name.German == "Waldbanditen" && BanditBossBarRules.Name.English == "Forest Bandits"
                && BanditBossBarRules.Name.AllLanguages.All(t => t.IndexOf('-') < 0)
                && BanditBossBarRules.SegmentLine(1, "guard", "u", "dead") == "CORE_BANDIT_BOSSBAR at=segment index=1 role=guard unit=u reason=dead"
                && BanditBossBarRules.Line("start", "") == "CORE_BANDIT_BOSSBAR at=start",
            "bandit boss bar C26: the boss marker for the boss party only, one segment per bandit there at the start (1 to 4), slayer first, fill head plus body, effects in 2 rows");
        // A member capped by the revival sickness (Core's share -90 of MaxHealthTotalPerModifier, 10 %)
        // gets the Blood Warden; the raise and its drop come in one flush of the game's queue, so Core's cap is the same after.
        var own = ReviveShareRules.ShareForCap(0.1f, 0f);
        var full = 1000f;
        var maxNow = full * (1f + own / 100f);
        var raise = BossEffectRules.CapRaisePercent(maxNow, own, 150f, 100);
        var maxDuring = ReviveShareRules.FullMax(maxNow, own, own) * (1f + (own + raise) / 100f);
        Check(Math.Abs(own + 90f) < 0.001f && Math.Abs(maxNow - 100f) < 0.01f && raise > 0f && 2f * maxDuring - 150f >= 100f
                && Math.Abs(ReviveShareRules.CapOfShare(own, ReviveShareRules.Foreign(own + raise - raise, own)) - 0.1f) < 0.0001f,
            "blood warden: a member capped by the sickness gets room for the whole shield, and Core's cap reads the same once the raise is gone");
        Check(CoreUnitRules.Units.All(u => CoreUnitRules.PhysicalAttackOf(u, 10f) == (u.Role == "guard" ? 8f : 10f))
                && CoreUnitRules.GuardDamageFactor == 0.8f && CoreUnitRules.PhysicalAttackOf(null, 10f) == 10f
                && CoreUnitRules.Units.All(u => u.Role == "guard" || u.DamageFactor == 1f),
            "boss party balance: the shieldbearer's physical attack is 0.8 of the template's, the other three keep it");
    }
}
