using DungeonSettlersDelvers.Core;
using static TestKit;

// Pictures that ship with Core: bandage icons, body blood frames, the Lifebloom icon.
internal static class ShippedAssetsSection
{
    internal static void Run()
    {
        BandageBaseIcon();
        BodyBloodFrames();
        BandageIcons();
        LifebloomIcon();
    }

    static void BandageBaseIcon()
    {
        // The bandages' base icon: the run-time probe line (size, atlas, pixels per unit).
        Check(BandageIconRules.BaseIcon == "ITEM_Bandage"
                && BandageIconRules.ProbeLine("ITEM_Bandage", true, 16, 16, 512, 512, 16f, false, true, "Point")
                    == "CORE_BANDAGE_ICON_PROBE key=ITEM_Bandage rect=16x16 texture=512x512 atlas=1 ppu=16 readable=0 packed=1 filter=Point"
                && BandageIconRules.ProbeLine("ITEM_Bandage", true, 16, 16, 16, 16, 16f, true, false, "Point").Contains(" atlas=0 ")
                && BandageIconRules.ProbeLine("ITEM_Bandage", false, 0, 0, 0, 0, 0f, false, false, "-") == "CORE_BANDAGE_ICON_PROBE key=ITEM_Bandage found=0",
            "bandages: the probe line of the native base icon");
    }

    static void BodyBloodFrames()
    {
        // The author's drawn body blood: frames 1-5 at 100 ms, frame 5 held 700 ms; one overlay per stage, standing only;
        // sorted above skin and armour, below head and front hand; the 15 pictures ship with Core, 64x40 each.
        var assets = Path.Combine(FindCoreSourceDirectory(), "Assets");
        static (int W, int H) Png(byte[] b) => ((b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19], (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23]);
        Check(WoundBodyArtRules.FrameAt(0) == 0 && WoundBodyArtRules.FrameAt(99) == 0 && WoundBodyArtRules.FrameAt(100) == 1
                && WoundBodyArtRules.FrameAt(499) == 4 && WoundBodyArtRules.FrameAt(500) == 4 && WoundBodyArtRules.FrameAt(1199) == 4
                && WoundBodyArtRules.FrameAt(1200) == 0 && WoundBodyArtRules.FrameAt(1350) == 1 && WoundBodyArtRules.FrameAt(-5) == 0
                && WoundBodyArtRules.LoopMs == 1200 && WoundBodyArtRules.FrameFor(0, 350) == 0 && WoundBodyArtRules.FrameFor(1, 350) == 3 && WoundBodyArtRules.FrameFor(2, 800) == 4,
            "body blood: frames 1-5 at 100 ms, then frame 5 held, 1.2 s a loop");
        Check(WoundBodyArtRules.StageIndex(WoundRules.Stage.Light) == 0 && WoundBodyArtRules.StageIndex(WoundRules.Stage.Deep) == 1
                && WoundBodyArtRules.StageIndex(WoundRules.Stage.Heavy) == 2 && WoundBodyArtRules.StageIndex(WoundRules.Stage.None) == -1
            && WoundBodyArtRules.StageIndex(WoundRules.Stage.Fatal) == 3 && WoundBodyArtRules.Drawing(WoundBodyArtRules.StageIndex(WoundRules.Stage.Fatal)) == 2
            // The body's renderer is the first active "Body" with a sprite, else the largest active body part.
            && WoundBodyArtRules.PickSkin(new[] { ("Body/Body", false, true, 9f), ("Sprite/Body", true, true, 1f), ("Head_Front/Head", true, true, 5f) }) == 1
            && WoundBodyArtRules.PickSkin(new[] { ("Body/Body", false, true, 9f), ("Torso", true, true, 2f), ("Chest", true, true, 3f), ("Hand_Front/Hand", true, true, 8f) }) == 2
            && WoundBodyArtRules.PickSkin(new[] { ("Body/Body", false, true, 9f) }) == -1
            && WoundBodyArtRules.KindOf("Head_Front/Head") == "head" && WoundBodyArtRules.KindOf("Body/Equipment_Body") == "armour"
            && WoundBodyArtRules.BodyOrder(new[] { ("Body/Body", true, 3), ("Body/Equipment_Body", true, 4), ("Head_Front/Head", true, 7), ("Hand_Back/Hand", true, 1), ("Body/X", false, 9) }) == (4, 7)
            && WoundBodyArtRules.BodyOrder(new[] { ("Body/Body", false, 3) }) == (0, int.MaxValue)
                && WoundBodyArtRules.Decide(false, 0, -1) == "show" && WoundBodyArtRules.Decide(false, 1, 0) == "swap"
                && WoundBodyArtRules.Decide(false, 1, 1) == "keep" && WoundBodyArtRules.Decide(true, 1, 1) == "remove:lying"
                && WoundBodyArtRules.Decide(true, 1, -1) == "keep" && WoundBodyArtRules.Decide(false, -1, 2) == "remove:closed"
                && WoundBodyArtRules.Decide(false, -1, -1) == "keep"
                && WoundBodyArtRules.Drawing(0) == 0 && WoundBodyArtRules.Drawing(1) == 0 && WoundBodyArtRules.Drawing(2) == 1 && WoundBodyArtRules.Drawing(3) == 2
                && WoundBodyArtRules.Drawing(-1) == -1 && WoundBodyArtRules.WoundStageNames[2] == "heavy",
            "body blood: shown standing, swapped with the stage, removed lying or when the wound closes");
        Check(WoundBodyArtRules.Sort(0, 1, 4, 5) == (2, 0f) && WoundBodyArtRules.Sort(0, 0, 0, 0) == (0, -0.001f)
                && WoundBodyArtRules.Sort(3, 1, 4, 9) == (3, -0.001f)
                && WoundBodyArtRules.Line("u", "tief", "shown") == "CORE_WOUND_FX_BODYART unit=u stage=tief result=shown",
            "body blood: sorted between armour and head");
        // Picture 19: the overlay takes the body sprite's own pivot and PPU.
        Check(Math.Abs(WoundBodyArtRules.PivotOf(32f, 2.4f, 64f, 40f, 16f).Y - 0.06f) < 1e-5f && WoundBodyArtRules.PivotOf(32f, 2.4f, 64f, 40f, 16f).X == 0.5f && WoundBodyArtRules.PivotOf(32f, 20f, 64f, 40f, 16f) == (0.5f, 0.5f, 16f)
                && WoundBodyArtRules.PivotOf(0f, 0f, 0f, 0f, 0f) == (0.5f, 0.5f, 16f)
                && WoundBodyArtRules.SpriteKey(1, 4, 0.5f, 0.06f, 16f) == "1_4_0.5_0.06_16"
                && WoundBodyArtRules.GeometryLine("u", "Peach", 32f, 2.4f, 64f, 40f, 16f, 0.5f, 0.06f, "Body", 0f, 0f, 0f, 0f)
                    == "CORE_WOUND_FX_BODYART_GEOMETRY unit=u bodySprite=Peach pivot=32,2.4 rect=64x40 ppu=16 overlayPivot=0.5,0.06 parent=Body localPos=0,0 bodyLocal=0,0",
            "body blood: the overlay takes the body sprite's pivot");
        Check(WoundBodyArtRules.File(1, 0) == "Koerperblut_tief_f01.png" && WoundBodyArtRules.AllFiles().Count() == 15
                && WoundBodyArtRules.AllFiles().All(f => File.Exists(Path.Combine(assets, f)) && Png(File.ReadAllBytes(Path.Combine(assets, f))) == (64, 40)),
            "body blood: the 15 pictures are in Core/Assets, 64x40");
        Check(WoundRules.GroundLine("u", "lying", 45, 2, 3, 9) == "CORE_WOUND_FX_GROUND unit=u kind=lying vfx=45 n=2/3 tick=9",
            "blood: a lying unit's marks lie on the ground, logged with kind=lying");
    }

    static void BandageIcons()
    {
        // The bandages' own 16x16 icons; without a file the placeholder stays, never ITEM_Bandage.
        var bandageAssets = Path.Combine(FindCoreSourceDirectory(), "Assets");
        static (int W, int H) BandagePng(byte[] b) => ((b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19], (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23]);
        Check(BandageIconRules.OwnIconFile(BandageText.LightItemKey) == "Verband_leicht_16x16.png"
                && BandageIconRules.OwnIconFile(BandageText.HeavyItemKey) == "Verband_schwer_16x16.png"
                && BandageIconRules.OwnIconFile(BandageText.HerbalItemKey) == "Verband_Kraeuter_16x16.png"
                && BandageIconRules.OwnIconFile(BandageText.SalveItemKey) == "Verband_Salbe_16x16.png"
                && BandageIconRules.OwnIconFile("ITEM_Textile") == null && BandageIconRules.OwnIconFile(BandageIconRules.BaseIcon) == null
                && BandageIconRules.IconsLine(4, new string[0]) == "CORE_BANDAGE_ICONS loaded=4 missing=- fallback=none"
                && BandageIconRules.IconsLine(3, new[] { "Verband_Salbe_16x16.png" }) == "CORE_BANDAGE_ICONS loaded=3 missing=Verband_Salbe_16x16.png fallback=placeholder"
                && BandageIconRules.OwnIconFiles.All(f => File.Exists(Path.Combine(bandageAssets, f)) && BandagePng(File.ReadAllBytes(Path.Combine(bandageAssets, f))) == (16, 16)),
            "bandage icons: one own 16x16 icon per bandage next to Core, the placeholder when a file is missing");
    }

    static void LifebloomIcon()
    {
        // The Lifebloom skill icon shows the charges left; its files ship with Core in 32x32.
        var assetDirectory = Path.Combine(FindCoreSourceDirectory(), "Assets");
        var iconFiles = new[] { "Lifebloom_Zaubersymbol_32x32.png", "Lifebloom_Zaubersymbol_32x32_0.png", "Lifebloom_Zaubersymbol_32x32_1.png", "Lifebloom_Zaubersymbol_32x32_2.png" };
        static (int W, int H) PngSize(byte[] b) => ((b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19], (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23]);
        Check(LifebloomRules.IconCharges(3) == 3 && LifebloomRules.IconCharges(2) == 2 && LifebloomRules.IconCharges(0) == 0
                && LifebloomRules.IconCharges(-1) == 0 && LifebloomRules.IconCharges(7) == 3
                && LifebloomRules.IconLine(new Guid("11111111-2222-3333-4444-555555555555"), 2, 1, 1, "charge", "Lifebloom_Zaubersymbol_32x32_2")
                    == "CORE_LIFEBLOOM_ICON caster=11111111-2222-3333-4444-555555555555 charges=2 slots=1 set=1 source=charge sprite=Lifebloom_Zaubersymbol_32x32_2"
                && iconFiles.All(f => File.Exists(Path.Combine(assetDirectory, f)) && PngSize(File.ReadAllBytes(Path.Combine(assetDirectory, f))) == (32, 32)),
            "Lifebloom icon: one picture per charge count (3, 2, 1, 0), all 32x32 next to Core");
        // One leaf less only once a charge is really spent, one more when a charge is back; a cancelled cast spends
        // nothing (no Spend), so the count and the picture stay.
        var full = LifebloomRules.Charges.Full;
        var spent = LifebloomRules.Spend(full, 100, 12, out _);
        Check(LifebloomRules.IconCharges(LifebloomRules.Recharge(full, 101).Count) == 3
                && LifebloomRules.IconCharges(spent.Count) == 2
                && LifebloomRules.IconCharges(LifebloomRules.Recharge(spent, 105).Count) == 2
                && LifebloomRules.IconCharges(LifebloomRules.Recharge(spent, 113).Count) == 3,
            "Lifebloom icon: a cancelled cast changes no leaf, a spent charge one less, a recharged one back");
        // Lifebloom off the global cooldown for the party AI, the game's AI and the herbalist alike.
        Check(LifebloomRules.OgcdLine("u", 2, "offgcd-ai") == "CORE_LIFEBLOOM_OGCD unit=u charges=2 source=offgcd-ai"
                && LifebloomRules.OgcdLine("h", 1, "herbalist") == "CORE_LIFEBLOOM_OGCD unit=h charges=1 source=herbalist",
            "Lifebloom: off the global cooldown, logged when placed while it runs");
    }
}
