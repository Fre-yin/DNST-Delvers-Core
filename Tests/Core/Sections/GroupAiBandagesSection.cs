using DungeonSettlersDelvers.Core;
using static TestKit;

// Group AI: bandages on the quick slots, the hotbar reader and the group AI texts.
internal static class GroupAiBandagesSection
{
    internal static void Run()
    {
        QuickSlotBandages();
        ShiftTwins();
        Aftercare();
        Formation();
        Priority();
        HotbarReader();
        GroupAiTexts();
    }

    // The Shift twin of the item quick slot keys (ShiftTwinRules): Q and the hotbar's extra item keys gain Shift for the item
    // slots only; the player's own Shift binding stays, keys with a modifier and keys without an item slot get none.
    static void ShiftTwins()
    {
        const int q = 113, e = 101, z = 122, item = 60, rotate = 57, extra = 12101, other = 42;
        var slots = new HashSet<int> { item, 12101, 12102 };
        var index = new List<ShiftTwinRules.Entry>
        {
            new(q, true, 0, new[] { rotate, item }),
            new(e, true, 0, new[] { extra }),
            new(e, true, ShiftTwinRules.LeftShift, new[] { other }),
            new(z, true, 0, new[] { other }),
            new(q, true, 306, new[] { other })
        };
        var twins = ShiftTwinRules.Plan(index, slots);
        Check(twins.Count == 3
                && twins.Any(t => t.Key == q && t.Modifier == ShiftTwinRules.LeftShift && t.Down && t.Types.SequenceEqual(new[] { item }))
                && twins.Any(t => t.Key == q && t.Modifier == ShiftTwinRules.RightShift && t.Types.SequenceEqual(new[] { item }))
                && twins.Any(t => t.Key == e && t.Modifier == ShiftTwinRules.RightShift && t.Types.SequenceEqual(new[] { extra }))
                && !twins.Any(t => t.Key == e && t.Modifier == ShiftTwinRules.LeftShift) && !twins.Any(t => t.Key == z),
            "shift twin: the quick slot key and the hotbar's extra item key gain Shift for the item slot only; a player's Shift binding stays, other keys none");
        Check(ShiftTwinRules.Plan(new List<ShiftTwinRules.Entry> { new(ShiftTwinRules.LeftShift, true, 0, new[] { item }) }, slots).Count == 0
                && ShiftTwinRules.Plan(null, slots).Count == 0 && ShiftTwinRules.Plan(index, new HashSet<int>()).Count == 0
                && ShiftTwinRules.Plan(index.Concat(twins).ToList(), slots).Count == 0,
            "shift twin: a Shift key gets no twin, nothing without slots, and a second rebuild adds nothing twice");
        Check(ShiftTwinRules.Line(twins) == "CORE_SHIFT_TWIN added=3 keys=113+101" && ShiftTwinRules.Line(new List<ShiftTwinRules.Entry>()) == "CORE_SHIFT_TWIN added=0 keys=-",
            "shift twin: the log line");
    }

    static void QuickSlotBandages()
    {
        // The bandages on the item quick slots (Q, extra 1 and 2), from the bag only with the option on (off by
        // default); the salve at a lethal wound or below 50 %, else the herbal one; the other kind stands in, both ways; no salve kept.
        var salveOnQ = new[] { "salve", null, null };
        var herbalOnExtra = new[] { null, null, "herbal" };
        var both = new[] { "herbal", "salve", null };
        var none = new string[3];
        Check(GroupAiBandageRules.FromBagKey == "groupai.bandagesfrombag"
                && GroupAiBandageRules.Want(true, 0.9f) == "salve" && GroupAiBandageRules.Want(false, 0.49f) == "salve"
                && GroupAiBandageRules.Want(false, 0.5f) == "herbal" && GroupAiBandageRules.Want(false, 0.8f) == "herbal",
            "C34 c: the kind follows the situation");
        Check(GroupAiBandageRules.Ready("salve", salveOnQ, 1, false) && !GroupAiBandageRules.Ready("salve", salveOnQ, 0, false)
                && !GroupAiBandageRules.Ready("herbal", salveOnQ, 3, false) && GroupAiBandageRules.Ready("herbal", salveOnQ, 3, true)
                && GroupAiBandageRules.Ready("herbal", herbalOnExtra, 1, false) && !GroupAiBandageRules.Ready("salve", null, 2, false)
                && GroupAiBandageRules.Ready("salve", null, 2, true),
            "C34 c: a bandage is ready on a quick slot, from the bag only with the option");
        Check(Pick("salve", true, "salve", "herbal") == "salve" && Pick("herbal", true, "salve", "herbal") == "herbal"
                && Pick("salve", true, "herbal") == "herbal" && Pick("herbal", true, "salve") == "salve"
                && Pick("salve", true) == null && Pick("herbal", true) == null
            // The aid kit first at a lethal wound, else the salve; never below lethal.
            && GroupAiBandageRules.Want(true, 0.9f, true) == "aidkit" && GroupAiBandageRules.Want(true, 0.9f, false) == "salve"
            && GroupAiBandageRules.Want(false, 0.8f, false) == "herbal"
            && Pick("aidkit", true, "aidkit", "salve", "herbal") == "aidkit" && Pick("aidkit", true, "salve", "herbal") == "salve"
            && Pick("aidkit", true, "herbal") == "herbal" && Pick("aidkit", true) == null
            && Pick("salve", true, "aidkit") == null && Pick("herbal", true, "aidkit", "herbal") == "herbal"
            && GroupAiBandageRules.KindOf("ITEM_AidKit") == "aidkit" && GroupAiBandageRules.KindOf(BandageText.SalveItemKey) == "salve"
            && GroupAiBandageRules.Line("u", "aidkit", "lethal", 0.2f, "lethal", false, 3, false, "use", "q", "q:aidkit", false,
                new GroupAiBandageRules.BagCounts(1, 0, 0, 0, 2), 9, true).EndsWith(" tick=9 kits=2"),
            "C34 c: the other kind stands in, both ways");
        // The heavy and light bandage stand in last, heavy first, and only on an open wound.
        Check(Pick("salve", true, "heavy", "light") == "heavy" && Pick("herbal", true, "light") == "light"
                && Pick("aidkit", true, "heavy") == "heavy" && Pick("salve", true, "herbal", "heavy") == "herbal"
                && Pick("salve", false, "heavy", "light") == null
                && GroupAiBandageRules.KindOf(BandageText.HeavyItemKey) == "heavy" && GroupAiBandageRules.KindOf(BandageText.LightItemKey) == "light",
            "bandages: heavy before light as the last stand-in, only on an open wound");
        // The last salve is used at a light wound when no herbal one is ready (no reserve).
        Check(GroupAiBandageRules.Pick(GroupAiBandageRules.Want(false, 0.8f),
                    kind => GroupAiBandageRules.Ready(kind, salveOnQ, kind == "salve" ? 1 : 2, false), true) == "salve",
            "C34 c: no salve is kept back");
        Check(GroupAiBandageRules.From("salve", salveOnQ) == "q" && GroupAiBandageRules.From("herbal", herbalOnExtra) == "extra2"
                && GroupAiBandageRules.From("salve", both) == "extra1" && GroupAiBandageRules.From("salve", none) == "bag"
                && GroupAiBandageRules.From("herbal", null) == "bag"
                && GroupAiBandageRules.Slots(both) == "q:herbal,e1:salve,e2:-" && GroupAiBandageRules.Slots(null) == "q:-,e1:-,e2:-"
                && GroupAiBandageRules.NoneReason(1, false) == "none:noslot" && GroupAiBandageRules.NoneReason(0, false) == "none"
                && GroupAiBandageRules.NoneReason(3, true) == "none"
                && GroupAiBandageRules.KindOf(BandageText.HerbalItemKey) == "herbal"
                && GroupAiBandageRules.KindOf("ITEM_Ointment") == null && GroupAiBandageRules.KindOf("") == null
                && GroupAiBandageRules.KindOf(null) == null
                && new GroupAiBandageRules.BagCounts(1, 2, 3, 4, 5).Bandages == 10,
            "C34 c: where the bandage comes from, the slots in the log, why none");
    }

    // Aftercare once the fight is over, the smallest bandage that covers the blood loss, the most urgent first.
    static void Aftercare()
    {
        const double life = 200;
        string Care(WoundRules.Stage stage, double buffer, bool stacks, params string[] ready)
            => GroupAiAftercareRules.Pick(stage, buffer, life, stacks, kind => Array.IndexOf(ready, kind) >= 0);
        var all = new[] { "aidkit", "salve", "herbal", "heavy", "light" };
        Check(Care(WoundRules.Stage.Light, 20, false, all) == "light" && Care(WoundRules.Stage.Deep, 21, false, all) == "heavy"
                && Care(WoundRules.Stage.Deep, 40, false, all) == "heavy" && Care(WoundRules.Stage.Heavy, 41, false, all) == "herbal"
                && Care(WoundRules.Stage.Light, 20, false, "salve", "herbal") == "herbal" && Care(WoundRules.Stage.Heavy, 60, false, "light") == "light",
            "aftercare: the smallest bandage that covers the blood loss (light to 10 %, heavy to 20 % of the whole health), then the next");
        Check(Care(WoundRules.Stage.Fatal, 300, false, all) == "aidkit" && Care(WoundRules.Stage.Fatal, 300, false, "herbal", "salve") == "salve"
                && Care(WoundRules.Stage.Light, 5, true, all) == "herbal" && Care(WoundRules.Stage.Light, 5, true, "light") == "light"
                && Care(WoundRules.Stage.Light, 5, false) == null && GroupAiAftercareRules.Pick(WoundRules.Stage.Light, 5, 0, false, _ => true) == "herbal",
            "aftercare: a lethal wound wants the aid kit, else the salve; running stacks a bandage of first aid; no health known, the two of first aid");
        var units = new List<(WoundRules.Stage Stage, double Buffer)>
            { (WoundRules.Stage.Light, 30), (WoundRules.Stage.Fatal, 10), (WoundRules.Stage.Light, 50), (WoundRules.Stage.Heavy, 5) };
        units.Sort(GroupAiAftercareRules.Urgency);
        Check(units.SequenceEqual(new[] { (WoundRules.Stage.Fatal, 10.0), (WoundRules.Stage.Heavy, 5.0), (WoundRules.Stage.Light, 50.0), (WoundRules.Stage.Light, 30.0) })
                && GroupAiAftercareRules.Quiet(13f, 10f) && !GroupAiAftercareRules.Quiet(12.9f, 10f),
            "aftercare: the higher stage first, then the greater blood loss; only 3 s after the last combat");
        Check(GroupAiAftercareRules.Line("u", "light", "light", 12.04, 0.8f, "use", "q", 7)
                    == "CORE_AI_AFTERCARE unit=u item=light stage=light buffer=12.0 pool=0.80 result=use from=q tick=7"
                && GroupAiAftercareRules.Line("u", null, "deep", 3, 0.5f, "none:noslot", null, 8).Contains(" item=- ")
                && GroupAiAftercareRules.DoneLine(12.5f, 3) == "CORE_AI_AFTERCARE_DONE seconds=12.5 used=3",
            "aftercare: one line per order or reason, one when no party unit bleeds any more");
    }

    // The formation in the dungeon; follow out of combat, take the places at the fight's start, back after 2.5 s
    // without a hit, strike only what is in reach of the place.
    static void Formation()
    {
        Check(GroupAiFormationRules.Why(false, false, 5f, 4.9f) == GroupAiFormationRules.Reason.Follow
                && GroupAiFormationRules.Why(true, true, 5f, 4.9f) == GroupAiFormationRules.Reason.Start
                && GroupAiFormationRules.Why(true, false, 5f, 4f) == null
                && GroupAiFormationRules.Why(true, false, 6.5f, 4f) == GroupAiFormationRules.Reason.Return
                && GroupAiFormationRules.Why(true, false, 3f, float.NegativeInfinity) == GroupAiFormationRules.Reason.Return,
            "formation: follow out of combat, take the place when the fight starts, back only 2.5 s after the last hit");
        Check(GroupAiFormationRules.NeedsOrder((1, 1), (2, 1), null, false, 0f) && !GroupAiFormationRules.NeedsOrder((2, 1), (2, 1), null, false, 9f)
                && !GroupAiFormationRules.NeedsOrder((1, 1), (2, 1), (2, 1), false, 1.9f) && GroupAiFormationRules.NeedsOrder((1, 1), (2, 1), (3, 1), true, 0f)
                // Pushed off its place (a dodge, a player's order): sent again 2 s after the last order, once that order is over.
                && GroupAiFormationRules.NeedsOrder((1, 1), (2, 1), (2, 1), false, 2f) && !GroupAiFormationRules.NeedsOrder((1, 1), (2, 1), (2, 1), true, 5f)
                && GroupAiFormationRules.InReach(0, 0, 3, -2, 3) && !GroupAiFormationRules.InReach(0, 0, 4, 0, 3)
                && GroupAiFormationRules.InReach(0, 0, 1, 1, 0)
                && GroupAiFormationRules.IsWalk(0, -2) && GroupAiFormationRules.IsWalk(2, 1) && !GroupAiFormationRules.IsWalk(1, -1) && !GroupAiFormationRules.IsWalk(0, 0)
                && GroupAiFormationRules.Line("u", GroupAiFormationRules.Reason.Return, 4, 7, "north") == "CORE_AI_FORMATION unit=u reason=return cell=4,7 facing=north",
            "formation: one order per new place, again 2 s after an order that did not arrive; reach counts diagonal steps as one, at least 1");
        Check(GroupAiFormationRules.Keeps(true, true, false) && !GroupAiFormationRules.Keeps(true, false, false) && !GroupAiFormationRules.Keeps(false, true, false)
                && GroupAiFormationRules.FreesCell(false, true) && !GroupAiFormationRules.FreesCell(false, false) && !GroupAiFormationRules.FreesCell(true, true),
            "formation: a member switched off neither follows nor holds, and its cell stays taken like the leader's");
        Check(SlowWalkTurns() && WiggleAndStepDoNot(),
            "formation: the walk counts from where the leader last stood, so a slow leader turns it; a wiggle or one step does not");
        Func<int, int, bool> freeButLeft = (x, y) => x >= 0;
        Check(GroupAiFormationRules.Unmoved(1, true, (2, 3), (2, 3)) == 2 && GroupAiFormationRules.Unmoved(1, true, (2, 4), (2, 3)) == 0
                && GroupAiFormationRules.Unmoved(3, false, (2, 3), (2, 3)) == 0 && GroupAiFormationRules.Unmoved(0, true, (2, 3), null) == 0
                && GroupAiFormationRules.Aside((0, 0), (0, 2), (_, _) => true) == (0, 1)
                && GroupAiFormationRules.Aside((0, 0), (-3, 0), freeButLeft) == (0, -1)
                && GroupAiFormationRules.Aside((0, 0), (0, 2), (_, _) => false) == null
                && GroupAiFormationRules.Aside((0, 0), (0, 1), (_, _) => true) == null && GroupAiFormationRules.Aside((1, 1), (0, 0), (_, _) => true) == null
                && GroupAiFormationRules.Aside((0, 0), (1, 2), (_, _) => true) != (1, 2)
                && GroupAiFormationRules.AsideLine("u", (1, 2), (1, 3), (1, 4), 2) == "CORE_AI_FORMATION_ASIDE unit=u from=1,2 to=1,3 place=1,4 unmoved=2",
            "formation: after two orders without a step from the same cell the member steps aside to the free neighbour nearest its place");
        Check(GroupAiFormationRules.StaysStill(true, true) && !GroupAiFormationRules.StaysStill(true, false) && !GroupAiFormationRules.StaysStill(false, true)
                && GroupAiFormationRules.IdleLine("u", 4, 7) == "CORE_AI_FORMATION_IDLE unit=u cell=4,7",
            "formation: an idle member of the formation does not wander, one switched off or outside it does");
        Check(!GroupAiFormationRules.Keeps(true, true, true) && !GroupAiFormationRules.FreesCell(false, GroupAiFormationRules.Keeps(true, true, true)),
            "formation: a member lying at near-death gets no order and keeps its cell taken until it stands again");
    }

    // The leader's cells one look (0.5 s) apart, read as GroupAiFormation does; the facing at the end.
    static TacticalFormationRules.Facing FacingAfter(TacticalFormationRules.Facing start, params (int X, int Y)[] cells)
    {
        var turn = new TacticalFormationRules.Turn(start);
        var walk = default(GroupAiFormationRules.Walk);
        (int X, int Y)? last = null;
        for (var i = 0; i < cells.Length; i++)
        {
            var (next, moved) = GroupAiFormationRules.Track(walk, cells[i], last);
            walk = next;
            if (moved is { } step)
            {
                var facing = turn.Facing;
                turn = TacticalFormationRules.Next(turn, step.Dx, step.Dy, i * GroupAiFormationRules.LookSeconds);
                if (turn.Facing != facing) walk = GroupAiFormationRules.Restart(cells[i]);
            }
            last = cells[i];
        }
        return turn.Facing;
    }

    // A tank walking one cell a second, four cells: before, two cells in the last second never came, so it never turned.
    static bool SlowWalkTurns()
    {
        var back = TacticalFormationRules.FacingOf(0, -1).Value;
        var ahead = TacticalFormationRules.FacingOf(0, 1).Value;
        return FacingAfter(back, (0, 0), (0, 0), (0, 0), (0, 0), (0, 1), (0, 1), (0, 2), (0, 2), (0, 3), (0, 3), (0, 4), (0, 4)) == ahead
            && FacingAfter(back, (0, 0), (0, 2), (0, 4), (0, 6)) == ahead
            // A turn back without a stop: four cells back, not past the start.
            && FacingAfter(ahead, (0, 0), (0, 2), (0, 4), (0, 6), (0, 4), (0, 2)) == back
            // The corridor pull with the AI on: one cell back, then three cells forward at the tank's pace.
            && FacingAfter(back, (0, 0), (0, 0), (0, 0), (0, 0), (0, -1), (0, -1), (0, 0), (0, 1), (0, 1), (0, 2), (0, 2), (0, 2)) == ahead
            // Two cells west (too short to turn it), then four towards the exit without a stop: it faces the last walk, not
            // the diagonal (-2, 4) the two together make.
            && FacingAfter(back, (2, 0), (2, 0), (2, 0), (2, 0), (1, 0), (0, 0), (0, 1), (0, 2), (0, 3), (0, 4)) == ahead
            // A straight walk on a path that wiggles by single cells still turns it.
            && FacingAfter(ahead, (0, 0), (0, 0), (0, 0), (0, 0), (1, -1), (0, -2), (1, -3), (0, -4)) == back
            // Three cells from a standstill turn it, two do not.
            && FacingAfter(back, (0, 0), (0, 0), (0, 0), (0, 0), (0, 1), (0, 2), (0, 3), (0, 3)) == ahead
            && FacingAfter(back, (0, 0), (0, 0), (0, 0), (0, 0), (0, 1), (0, 2), (0, 2), (0, 2)) == back;
    }

    // One correcting step, and the path wiggling between neighbouring cells, keep the facing.
    static bool WiggleAndStepDoNot()
    {
        var north = TacticalFormationRules.FacingOf(0, -1).Value;
        return FacingAfter(north, (0, 0), (0, 0), (0, 0), (0, 0), (1, 0), (1, 0), (1, 0), (1, 0)) == north
            && FacingAfter(north, (0, 0), (1, 0), (0, 0), (1, 0), (0, 0), (1, 0)) == north
            && FacingAfter(north, (0, 0), (0, -2), (0, -4), (0, -6), (1, -6), (1, -6)) == north;
    }

    // The priority casts in the laid order; the healer's seed or Lifebloom first in her emergency.
    static void Priority()
    {
        var places = new[] { "SKILL_A", "", GroupAiHealerSeedRules.SeedSkill };
        Check(GroupAiPriorityRules.Order(places, false).SequenceEqual(new[] { 0, 2 })
                && GroupAiPriorityRules.Order(places, true).SequenceEqual(new[] { 2, 0 })
                && GroupAiPriorityRules.Order(new[] { LifebloomRules.SkillKey, GroupAiHealerSeedRules.SeedSkill, "SKILL_B" }, true).SequenceEqual(new[] { 0, 1, 2 })
                && GroupAiPriorityRules.Order(new[] { "", "", "" }, true).Count == 0 && GroupAiPriorityRules.Order(null, false).Count == 0
                // Five places: the order follows the list, however long.
                && GroupAiPriorityRules.Order(new[] { "SKILL_A", "", "SKILL_C", "SKILL_D", LifebloomRules.SkillKey }, true).SequenceEqual(new[] { 4, 0, 2, 3 })
                && GroupAiPriorityRules.Line("u", 4, "SKILL_E", "cast", false).Contains(" slot=5 ")
                && GroupAiPriorityRules.Line("u", 2, "SKILL_A", "autooff", false) == "CORE_AI_PRIORITY unit=u slot=3 skill=SKILL_A result=autooff"
                && GroupAiPriorityRules.Line("u", 0, "SKILL_A", "cast", true).EndsWith(" emergency=1"),
            "priority casts: the places in their order, empty ones skipped; the healer's emergency casts first in an emergency");
    }

    // Pick with the given kinds ready.
    static string Pick(string want, bool woundOpen, params string[] ready)
        => GroupAiBandageRules.Pick(want, kind => Array.IndexOf(ready, kind) >= 0, woundOpen);

    static void HotbarReader()
    {
        // The Extended Hotbar's extra slots, found softly: a public static string[] ExtraItemKeys(string or Guid); without it only Q.
        var method = GroupAiBandageSlotReader.FindIn(typeof(TestHotbarSlots).Assembly);
        Check(GroupAiBandageSlotReader.IsHotbarAssembly("DungeonSettlersHotbar.BepInEx") && GroupAiBandageSlotReader.IsHotbarAssembly("DungeonSettlers10Slots")
                && !GroupAiBandageSlotReader.IsHotbarAssembly("DungeonSettlersDelvers.Core") && !GroupAiBandageSlotReader.IsHotbarAssembly(null)
                && GroupAiBandageSlotReader.Find(new[] { typeof(TestHotbarSlots).Assembly }) == null
                && method != null && method.DeclaringType == typeof(TestHotbarSlots) && method.GetParameters()[0].ParameterType == typeof(string)
                && GroupAiBandageSlotReader.Read(typeof(TestHotbarSlots).GetMethod("ExtraItemKeys", new[] { typeof(Guid) }), "00000000-0000-0000-0000-000000000001").SequenceEqual(new[] { null, BandageText.HerbalItemKey })
                && GroupAiBandageSlotReader.Read(typeof(TestHotbarSlots).GetMethod("ExtraItemKeys", new[] { typeof(Guid) }), "not-a-guid").SequenceEqual(new string[2])
                && GroupAiBandageSlotReader.Read(method, "u1").SequenceEqual(new[] { BandageText.SalveItemKey, null })
                && GroupAiBandageSlotReader.Read(method, "u2").SequenceEqual(new string[2])
                && GroupAiBandageSlotReader.Read(method, "throw").SequenceEqual(new string[2])
                && GroupAiBandageSlotReader.Read(null, "u1").SequenceEqual(new string[2])
                && !GroupAiBandageSlotReader.Fits(typeof(TestHotbarSlots).GetMethod(nameof(TestHotbarSlots.ExtraItemCount))),
            "C34 c: the hotbar's extra slots are read through a public method, softly, the string overload first");
    }

    static void GroupAiTexts()
    {
        // The group AI is rather an accessibility feature, locked from Very Hard up; its texts in all ten languages.
        Check(!GroupAiLockRules.Locked(0) && !GroupAiLockRules.Locked(2) && GroupAiLockRules.Locked(3) && GroupAiLockRules.Locked(4) && GroupAiLockRules.Locked(5)
                && GroupAiLockRules.Locked((int)ReviveRules.Level.VeryHard) && !GroupAiLockRules.Locked((int)ReviveRules.Level.Hard)
                && GroupAiLockRules.Line(true, 3) == "CORE_AI_GROUP_LOCK locked=1 difficulty=VeryHard"
                && GroupAiLockRules.Line(false, 1) == "CORE_AI_GROUP_LOCK locked=0 difficulty=Normal"
                && GroupAiLockRules.Name(5) == "Apocalypse",
            "Group AI: locked from Very Hard up");
        Check(GroupAiText.All.All(entry => entry.Text.AllLanguages.Count == 10 && entry.Text.AllLanguages.All(t => !string.IsNullOrWhiteSpace(t) && !t.Contains('—')))
                && GroupAiText.NoticeKey == "TEXTKEY_DelversCore_GroupAiNotice"
                && GroupAiText.MenuBandagesFromBag.German == "KI darf Verbände und Verbandkästen aus der Tasche benutzen"
                && GroupAiText.Notice.German.Contains(GroupAiText.MenuBandagesFromBag.German) && GroupAiText.Notice.English.Contains(GroupAiText.MenuBandagesFromBag.English)
                && GroupAiText.MenuHint.German.Contains("Barrierefreiheit") && GroupAiText.MenuHint.English.Contains("accessibility"),
            "Group AI: menu texts and notice");
    }
}
