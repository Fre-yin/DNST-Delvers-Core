using DungeonSettlersDelvers.Core;
using static TestKit;

// Healing a wound: heal sources, bandages and their texts.
internal static class WoundHealingSection
{
    internal static void Run()
    {
        HealSources();
        BandageDecisions();
        AidKitOnStandingUnit();
        BandageTexts();
        BandColours();
    }

    // The band runs in red from the lightest (light) to the dark red (lethal), each stage darker than the
    // one before in even steps, from one table; the lethal zigzag's second red is darker still.
    private static void BandColours()
    {
        static double Luminance(string hex)
        {
            double Channel(int at)
            {
                var c = Convert.ToInt32(hex.Substring(at, 2), 16) / 255.0;
                return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
            }
            return 0.2126 * Channel(1) + 0.7152 * Channel(3) + 0.0722 * Channel(5);
        }
        static double Ratio(string a, string b)
        {
            var (x, y) = (Luminance(a), Luminance(b));
            return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
        }
        var stages = new[] { WoundRules.Stage.Light, WoundRules.Stage.Deep, WoundRules.Stage.Heavy, WoundRules.Stage.Fatal };
        var steps = true;
        for (var i = 1; i < stages.Length; i++)
            steps &= Luminance(WoundText.Colour(stages[i])) < Luminance(WoundText.Colour(stages[i - 1]))
                && Ratio(WoundText.Colour(stages[i]), WoundText.Colour(stages[i - 1])) >= 1.25;
        Check(steps && WoundText.Colours.Count == 4 && stages.All(stage => WoundText.Colour(stage) == WoundText.Colours[stage])
                && Luminance(WoundText.LethalDarkColour) < Luminance(WoundText.Colour(WoundRules.Stage.Fatal)),
            "Wound band colours: light to dark, each step at least 1.25 in contrast, one table");
        // The band's fill ends where the bar's fill ends, on the rectangles the canvas draws (the green column).
        Check(Math.Abs(WoundRules.FillTo(-35f, 70f, -35f, 70f, 0.4f) - 0.4f) < 1e-5f
                && Math.Abs(WoundRules.FillTo(-35.4f, 70.4f, -14f, 49f, 0.5f) - (-35.4f + 35.2f + 14f) / 49f) < 1e-5f
                && WoundRules.FillTo(0f, 70f, 10f, 0f, 0.5f) == 0f && WoundRules.FillTo(0f, 70f, 50f, 20f, 0.5f) == 0f
                && WoundRules.FillTo(0f, 70f, 0f, 70f, 1.2f) == 1f,
            "Wound band: its fill ends on the bar's fill end, measured on the drawn rectangles");
        // Over the head one red for every stage, readable on the head bar's green (#6C9C41) and its empty part (#28262D).
        Check(Ratio(WoundText.HudColour, "#6C9C41") >= 2.0 && Ratio(WoundText.HudColour, "#28262D") >= 2.0,
            "Wound band over the head: one red, readable on the bar's green and on its empty part");
    }

    static void HealSources()
    {
        {
            var (deepHeal, deepTaken) = WoundRules.Heal(WoundRules.Stage.Deep, WoundRules.HealKind.Sourced, 100, 60, 134, WoundRules.HealFactor(WoundRules.Stage.Deep));
            var (lightHeal, lightTaken) = WoundRules.Heal(WoundRules.Stage.Light, WoundRules.HealKind.Sourced, 100, 0, 134, WoundRules.HealFactor(WoundRules.Stage.Light));
            var (lethalHeal, lethalTaken) = WoundRules.Heal(WoundRules.Stage.Heavy, WoundRules.HealKind.Sourced, 100, 134, 134, WoundRules.HealFactor(WoundRules.Stage.Heavy));
            Check(Math.Abs(deepHeal - 80) < 1e-9 && Math.Abs(deepTaken - 6) < 1e-9 && Math.Abs(lightHeal - 90) < 1e-9 && lightTaken == 0
                    && Math.Abs(lethalHeal - 70) < 1e-9 && Math.Abs(lethalTaken - 70) < 1e-9,
                "wound: heals with a source are 10, 20 or 30 % weaker, and what goes beyond full health takes the buffer");
        // The game's heal execution carries no source: inside the mark it is a heal with a
        // source; regeneration and the portal outside it stay as they were.
        Check(WoundRules.KindOf(true, false, false) == WoundRules.HealKind.Sourced && WoundRules.KindOf(false, true, false) == WoundRules.HealKind.Sourced
                && WoundRules.KindOf(false, true, true) == WoundRules.HealKind.Sourced
                && WoundRules.KindOf(false, false, false) == WoundRules.HealKind.Regeneration && WoundRules.KindOf(false, false, true) == WoundRules.HealKind.Portal
                && WoundRules.Heal(WoundRules.Stage.Light, WoundRules.KindOf(false, false, false), 100, 300, 500, WoundRules.HealFactor(WoundRules.Stage.Light)) == (0, 0)
                && Math.Abs(WoundRules.Heal(WoundRules.Stage.Light, WoundRules.KindOf(false, true, false), 100, 300, 500, WoundRules.HealFactor(WoundRules.Stage.Light)).Heal - 90) < 1e-9
                && WoundRules.Heal(WoundRules.Stage.Deep, WoundRules.KindOf(false, false, true), 100, 300, 500, WoundRules.HealFactor(WoundRules.Stage.Deep)) == (100, 100),
            "wound: a heal of the game's heal execution counts as one with a source; regeneration and portal stay");
        // Lifebloom at full health: no heal goes out (no 0 shown), the overheal still takes the buffer.
        Check(!LifebloomRules.HealsThroughGame(0, 0) && !LifebloomRules.HealsThroughGame(-2, 0.005) && LifebloomRules.HealsThroughGame(0, 1)
                && Math.Abs(WoundRules.Heal(WoundRules.Stage.Light, WoundRules.HealKind.Sourced, 100, 500, 500, WoundRules.HealFactor(WoundRules.Stage.Light)).Buffer - 90) < 1e-9
                && Math.Abs(WoundRules.Heal(WoundRules.Stage.Heavy, WoundRules.HealKind.Sourced, 40, 500, 500, WoundRules.HealFactor(WoundRules.Stage.Heavy)).Heal - 28) < 1e-9
                && Math.Abs(WoundRules.Heal(WoundRules.Stage.Heavy, WoundRules.HealKind.Sourced, 40, 500, 500, WoundRules.HealFactor(WoundRules.Stage.Heavy)).Buffer - 28) < 1e-9
                && WoundRules.HealLine("u", "over", "lifebloom", WoundRules.HealKind.Sourced, WoundRules.Stage.Light, 100, 0, 0, 95, 7)
                    == "CORE_WOUND_HEAL unit=u stat=over src=lifebloom kind=sourced tier=light in=100.0 out=0.0 room=0.0 buffer=95.0 tick=7"
                && WoundRules.HealLine("u", "body", "exec", WoundRules.HealKind.Sourced, WoundRules.Stage.Light, 10, 9.5, 1, 8.5, 8).Contains(" room=1.0 buffer=8.5 ")
                && WoundRules.Room(-0.25) == 0 && WoundRules.Room(double.NaN) == 0 && WoundRules.Room(3.5) == 3.5,
            "lifebloom: at full health with an open bleeding wound no heal number, the buffer drops as before");
        }
        Check(Math.Abs(WoundRules.Bandage(250, WoundRules.LightBandageShare, 700) - 180) < 1e-9
                && WoundRules.Bandage(60, WoundRules.LightBandageShare, 700) == 0
                && Math.Abs(WoundRules.Bandage(250, WoundRules.HeavyBandageShare, 700) - 110) < 1e-9
                && Math.Abs(WoundRules.Bandage(250, WoundRules.HeavyBandageShare, 1000) - 50) < 1e-9
                && WoundRules.Bandage(250, WoundRules.ClosesWound, 700) == 0,
            "wound: a light bandage takes 10 % of the unit's whole health, a heavy one 20 %, herbal and salve close the wound");
    }

    static void BandageDecisions()
    {
        var light = BandageRules.KindOf(BandageText.LightItemKey).Value;
        var heavy = BandageRules.KindOf(BandageText.HeavyItemKey).Value;
        var herbal = BandageRules.KindOf(BandageText.HerbalItemKey).Value;
        var salve = BandageRules.KindOf(BandageText.SalveItemKey).Value;
        Check(light == BandageRules.Kind.Light && heavy == BandageRules.Kind.Heavy && herbal == BandageRules.Kind.Herbal && salve == BandageRules.Kind.Salve
                && BandageRules.KindOf("ITEM_AidKit") == null && BandageRules.KindOf("ITEM_Textile") == null && BandageRules.KindOf("") == null && BandageRules.KindOf(null) == null,
            "bandages: the four item keys name the four kinds, every other item is none");
        // First Aid as the root of a tree of its own below the game's, open from the start.
        Check(DelversResearchTree.Place(0, 0) == (0, 21) && DelversResearchTree.Row > DelversResearchTree.GameLastRow + 1
                && DelversResearchTree.Place(2, 1) == (2, 22) && DelversResearchTree.NoPrerequisites.SequenceEqual(new[] { "NULL" }),
            "research: the Delvers tree's root stands in column 0 one empty row below the game's last, later nodes to its right and below, a root without prerequisites as the game writes it");
        // The screen's content ended above row 21.
        Check(DelversResearchTree.ContentGrowth(-1000f, -1100f, 50f) == 150f && DelversResearchTree.ContentGrowth(-1300f, -1100f, 50f) == 0f
                && DelversResearchTree.SizeFor(150f, 1f) == 150f && DelversResearchTree.SizeFor(150f, 0.5f) == 300f,
            "research: the content reaches down to the lowest node and half a node more, growing around its pivot, and not again once it reaches");
        var kit = BandageRules.Kind.Kit;
        Check(BandageRules.KindOnOther(BandageText.SalveItemKey) == salve && BandageRules.KindOnOther("ITEM_WildHerb") == light
                && BandageRules.KindOnOther("ITEM_Ointment") == null && BandageRules.KindOnOther("ITEM_AidKit") == kit
                && BandageRules.KindOnOther("ITEM_Textile") == null && BandageRules.KindOnOther(null) == null
                && BandageRules.CutsBleeding(kit) && BandageRules.BleedingCutShare(kit) == 1.0 && BandageRules.ShareOf(kit) == WoundRules.ClosesWound
                && BandageRules.BufferAfter(WoundRules.Stage.Fatal, kit, 900, 700, false) == 0 && !BandageRules.TakesFatalShare(WoundRules.Stage.Fatal, kit),
            "use on another: raw herbs like a light bandage, the aid kit closes every wound with all stacks, the game's ointment heals as in the game");
        // Review of p4: a light bandage put on a comrade with the game's bleeding alone was used up for nothing.
        Check(!BandageRules.TakesOnOther(light, false, 4) && !BandageRules.TakesOnOther(heavy, false, 4)
                && !BandageRules.TakesOnOther(BandageRules.KindOnOther("ITEM_WildHerb"), false, 4)
                && BandageRules.TakesOnOther(herbal, false, 4) && BandageRules.TakesOnOther(salve, false, 4) && BandageRules.TakesOnOther(kit, false, 4)
                && BandageRules.TakesOnOther(light, true, 0) && !BandageRules.TakesOnOther(herbal, false, 0)
                && BandageRules.TakesOnOther(BandageRules.KindOnOther("ITEM_Ointment"), false, 2) && !BandageRules.TakesOnOther(null, false, 0),
            "use on another: a target with only the game's bleeding takes the bandages of first aid and the kit, never a light or heavy bandage or raw herbs");
        Check(BandageRules.BufferAfter(WoundRules.Stage.Deep, light, 200, 700, false) == 130
                && BandageRules.BufferAfter(WoundRules.Stage.Heavy, heavy, 300, 700, false) == 160
                && BandageRules.BleedingCutShare(light) == 0 && BandageRules.BleedingCutShare(herbal) == WoundRules.HerbalBleedingCut,
            "use on another: on a fallen unit every item works at its own strength, as standing (the light one takes 10 %, the heavy 20 %, stacks stay)");
        Check(!BandageRules.CutsBleeding(light) && !BandageRules.CutsBleeding(heavy) && BandageRules.CutsBleeding(herbal) && BandageRules.CutsBleeding(salve)
                && BandageRules.BleedingCutShare(herbal) == WoundRules.HerbalBleedingCut && BandageRules.BleedingCutShare(salve) == WoundRules.SalveBleedingCut
                && BandageRules.BleedingCutShare(light) == 0 && BandageRules.BleedingCutShare(heavy) == 0
                && BandageRules.LeavesRoutineRunning(salve) && !BandageRules.LeavesRoutineRunning(herbal)
                && !BandageRules.LeavesRoutineRunning(light) && !BandageRules.LeavesRoutineRunning(heavy),
            "bandages: only herbal and salve cut the game's bleeding (a third, a half); only the salve lets the ointment's routine run on");
        Check(BandageRules.ShareOf(light) == WoundRules.LightBandageShare && BandageRules.ShareOf(heavy) == WoundRules.HeavyBandageShare
                && BandageRules.ShareOf(herbal) == WoundRules.ClosesWound && BandageRules.ShareOf(salve) == WoundRules.ClosesWound,
            "bandages: light takes 10 % and heavy 20 % of the whole health, herbal and salve close the wound");
        // Use: only with an open wound, or for the two of first aid also with the game's bleeding alone.
        Check(BandageRules.AllowsUse(true, light, 0) && BandageRules.AllowsUse(true, heavy, 0) && !BandageRules.AllowsUse(false, light, 5)
                && !BandageRules.AllowsUse(false, heavy, 5) && BandageRules.AllowsUse(false, herbal, 1) && BandageRules.AllowsUse(false, salve, 3)
                && !BandageRules.AllowsUse(false, herbal, 0) && !BandageRules.AllowsUse(false, salve, 0),
            "bandages: refused without a wound; herbal and salve are also allowed with the game's bleeding alone, light and heavy are not");
        // The buffer after the bandage, on every stage but lethal as before (WoundRules.Bandage).
        const double maxLife = 700;
        Check(Math.Abs(BandageRules.BufferAfter(WoundRules.Stage.Light, light, 60, maxLife, false)) < 1e-9
                && Math.Abs(BandageRules.BufferAfter(WoundRules.Stage.Deep, light, 250, maxLife, false) - 180) < 1e-9
                && Math.Abs(BandageRules.BufferAfter(WoundRules.Stage.Heavy, heavy, 250, maxLife, false) - 110) < 1e-9
                && BandageRules.BufferAfter(WoundRules.Stage.Light, herbal, 60, maxLife, false) == 0
                && BandageRules.BufferAfter(WoundRules.Stage.Deep, herbal, 150, maxLife, false) == 0
                && BandageRules.BufferAfter(WoundRules.Stage.Heavy, salve, 250, maxLife, false) == 0,
            "bandages: below lethal the light one takes 10 %, the heavy one 20 %, herbal and salve close the wound");
        // On a lethal wound every bandage takes a share of the whole health: herbal 30 %, salve 40 %;
        // only the setting lets them close it. Light and heavy take their share as on any stage.
        Check(Math.Abs(BandageRules.BufferAfter(WoundRules.Stage.Fatal, herbal, 600, maxLife, false) - 390) < 1e-9
                && Math.Abs(BandageRules.BufferAfter(WoundRules.Stage.Fatal, salve, 600, maxLife, false) - 320) < 1e-9
                && BandageRules.BufferAfter(WoundRules.Stage.Fatal, salve, 200, maxLife, false) == 0
                && BandageRules.BufferAfter(WoundRules.Stage.Fatal, herbal, 600, maxLife, true) == 0
                && BandageRules.BufferAfter(WoundRules.Stage.Fatal, salve, 600, maxLife, true) == 0,
            "lethal wound: the herbal bandage at T4 takes 30 % of the whole health, the salve 40 %, closing only with the setting");
        // Several bandages one after the other on a lethal wound of 1000 at 700 whole health and 300 health left: the salve
        // leaves 720, still lethal; the herbal one 510, still lethal; the heavy one 370; a light one 300, exactly the health
        // left, still lethal; the next light one 230, below it, so the lethal wound ends and the stage is heavy again (25 % = 175).
        var chain = new[] { (salve, 720.0), (herbal, 510.0), (heavy, 370.0), (light, 300.0), (light, 230.0) };
        var buffer = 1000.0;
        var ends = new List<bool>();
        foreach (var (kind, expected) in chain)
        {
            buffer = BandageRules.BufferAfter(WoundRules.Stage.Fatal, kind, buffer, maxLife, false);
            ends.Add(Math.Abs(buffer - expected) < 1e-9 && WoundRules.LeavesFatal(buffer, 300));
        }
        Check(ends.SequenceEqual(new[] { false, false, false, false, true })
                && WoundRules.StageOf(buffer, maxLife, false) == WoundRules.Stage.Heavy
                && WoundRules.StageOf(buffer, maxLife, true) == WoundRules.Stage.Fatal,
            "lethal wound: bandages one after the other; exactly at the health left it stays lethal, below it the stage follows the thresholds");
        Check(WoundRules.LeavesFatal(99.9, 100) && !WoundRules.LeavesFatal(100, 100) && !WoundRules.LeavesFatal(150, 100)
                && WoundRules.LeavesFatal(0.1, 0.5) && !WoundRules.LeavesFatal(5, double.NaN)
                && WoundRules.LeavesFatal(30, -5) == false,
            "lethal wound: it ends below the health left, not at it; unknown health keeps it");
        Check(WoundRules.EndsAtDeath(0.5, 0, 0) && WoundRules.EndsAtDeath(0, 3, 0) && WoundRules.EndsAtDeath(0, 0, 20)
                && !WoundRules.EndsAtDeath(0, 0, 0) && !WoundRules.EndsAtDeath(-1, 0, 0)
                && WoundRules.DeathLine("u", "load", true, 200, 4, 20) == "CORE_WOUND_DEATH unit=u source=load party=1 buffer=200.0->0 bleeding=4->0 effect=20->0"
                && WoundRules.DeathLine("n", "death", false, 0, 2, 0) == "CORE_WOUND_DEATH unit=n source=death party=0 buffer=0.0->0 bleeding=2->0 effect=0->0",
            "death: a buffer, the game's bleeding or a stage effect left on a corpse ends, logged once per death or load");
        Check(Math.Abs(BandageRules.BufferAfter(WoundRules.Stage.Fatal, light, 600, maxLife, false) - 530) < 1e-9
                && Math.Abs(BandageRules.BufferAfter(WoundRules.Stage.Fatal, heavy, 600, maxLife, false) - 460) < 1e-9
                && BandageRules.BufferAfter(WoundRules.Stage.Fatal, light, 40, maxLife, false) == 0,
            "lethal wound: the light and the heavy bandage take their share of the whole health at T4 as on any stage");
    }

    static void AidKitOnStandingUnit()
    {
        // The first aid kit also stops a bleeding completely before the unit is down. A standing, bleeding unit gets the kit
        // ordered on itself: Core's wound, the game's bleeding or both, at every stage up to lethal.
        Check(BandageRules.AidKitRefusal(Facts(woundOpen: true, bleeding: 0)) == null
                && BandageRules.AidKitRefusal(Facts(woundOpen: false, bleeding: 4)) == null
                && BandageRules.AidKitRefusal(Facts(woundOpen: true, bleeding: 7)) == null
                && BandageRules.AidKitRefusal(Facts(woundsRule: false, woundOpen: true, bleeding: 2)) == null,
            "aid kit: a standing, bleeding unit gets the kit ordered on itself, with Core's wound, with the game's bleeding or with both");
        // The fallen unit is the game's own case, and so is a unit that bleeds nowhere, is not ours, or has no kit in hand.
        Check(BandageRules.AidKitRefusal(Facts(woundOpen: true, bleeding: 3, down: true)) == BandageRules.RefusalDown
                && BandageRules.AidKitRefusal(Facts(woundOpen: false, bleeding: 0)) == BandageRules.RefusalNoWound
                && BandageRules.AidKitRefusal(Facts(woundOpen: false, bleeding: -1)) == BandageRules.RefusalNoWound
                && BandageRules.AidKitRefusal(Facts(woundOpen: true, bleeding: 3, party: false)) == BandageRules.RefusalNotParty
                && BandageRules.AidKitRefusal(Facts(woundOpen: true, bleeding: 3, affecters: false)) == BandageRules.RefusalNoAffecters
                && BandageRules.AidKitRefusal(Facts(woundOpen: true, bleeding: 3, item: false)) == BandageRules.RefusalNoItem,
            "aid kit: a fallen unit, a unit without a wound, a stranger or a missing kit stay with the game's own handler");
        // Without the wound rule only an already open wound counts; the game's bleeding alone is no reason then.
        Check(BandageRules.AidKitRefusal(Facts(woundsRule: false, woundOpen: false, bleeding: 5)) == BandageRules.RefusalNoRule
                && BandageRules.AidKitRefusal(Facts(woundsRule: true, woundOpen: false, bleeding: 5)) == null
                && BandageRules.AidKitRefusal(Facts(item: false, woundsRule: false, woundOpen: false, bleeding: 0)) == BandageRules.RefusalNoItem
                && new[] { "native:noitem", "native:norule", "native:notparty", "native:noaffecters", "native:down", "native:nowound" }
                    .SequenceEqual(new[] { BandageRules.RefusalNoItem, BandageRules.RefusalNoRule, BandageRules.RefusalNotParty,
                        BandageRules.RefusalNoAffecters, BandageRules.RefusalDown, BandageRules.RefusalNoWound }),
            "aid kit: without the wound rule an open wound still counts, the game's bleeding alone does not; the log words stay");
    }

    static BandageRules.AidKitFacts Facts(bool item = true, bool woundsRule = true, bool woundOpen = false, bool party = true,
        bool affecters = true, bool down = false, int bleeding = 0)
        => new(item, woundsRule, woundOpen, party, affecters, down, bleeding);

    static void BandageTexts()
    {
        Check(BandageText.All.All(entry => entry.Text.AllLanguages.Count == 10
                && entry.Text.AllLanguages.All(text => !string.IsNullOrWhiteSpace(text) && text.IndexOf('–') < 0 && text.IndexOf('—') < 0
                    && text.IndexOf('-') < 0))
                && BandageText.LightItemKey == "ITEM_DelversCoreLightBandage" && BandageText.HeavyItemKey == "ITEM_DelversCoreHeavyBandage"
                && BandageText.LightDescription.German == "Lindert eine Blutung."
                && BandageText.HeavyDescription.German == "Lindert eine Blutung stark."
                && BandageText.HeavyDescription.English == "Greatly eases bleeding."
                && BandageText.HerbalItemKey == "ITEM_DelversCoreHerbalBandage" && BandageText.SalveItemKey == "ITEM_DelversCoreSalveBandage"
                && BandageText.FirstAidResearchKey == "RESEARCH_DelversCoreFirstAid"
                && BandageText.ResearchNameKey(BandageText.FirstAidResearchKey) == "TEXTKEY_RESEARCH_DelversCoreFirstAid_NAME"
                && BandageText.HerbalFlavor.German == "Mit Wildkräutern versetzt, stillt er Blut, wo Leinen allein versagt."
                && BandageText.All.Count() == 14 && BandageText.All.Select(entry => entry.Name).Distinct().Count() == 14
                && BandageText.All.All(entry => CoreTextRows.Registered.Any(row => row.Key == entry.Name))
                && BandageText.UseOnOtherName.German == "Auf andere Figur anwenden" && BandageText.UseOnOtherName.English == "Use on another character",
            "bandages: words in all ten languages, item keys under Core's prefix, so saves write them as cloth; the inventory's option to use one on another character");
    }
}
