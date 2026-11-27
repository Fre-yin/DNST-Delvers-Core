using DungeonSettlersDelvers.Core;
using static DungeonSettlersDelvers.Core.ApocalypseDifficultyRules;
using static TestKit;

// The Apocalypse difficulty state: screen selection, remembered start, active parts, load and test marker.
internal static class ApocalypseDifficultySection
{
    internal static void Run()
    {
        ArrowSteps();
        SelectionToActive();
        NormalCampaignAfterApocalypse();
        LoadAndTestMarker();
    }

    static State Selected(bool combat, bool management)
        => default(State).WithSelected(true, combat).WithSelected(false, management);

    static void ArrowSteps()
    {
        var up = Step(Downfall, selected: false, increase: true, available: true);
        var topAgain = Step(Downfall, selected: true, increase: true, available: true);
        var down = Step(Downfall, selected: true, increase: false, available: true);
        Check(up == new StepResult(true, true, true) && topAgain == new StepResult(true, true, false)
                && down == new StepResult(true, false, true),
            "difficulty: up from Downfall selects Apocalypse once, down from it deselects");
        var nativeDown = Step(Downfall, selected: false, increase: false, available: true);
        var locked = Step(Downfall, selected: false, increase: true, available: false);
        Check(!nativeDown.Handled && !nativeDown.Changed && !locked.Handled && !locked.Changed && !locked.Selected,
            "difficulty: without a selection the game steps down, and with the switch off or Downfall locked it stays native");
        var away = Step(Downfall - 1, selected: true, increase: true, available: true);
        Check(!away.Handled && !away.Selected && !away.Changed,
            "difficulty: a native step away from Downfall drops the selection and is left to the game");
        HellSteps();
    }

    // With Amfi's Hell (the game's own value 5) the order is Downfall, Apocalypse, Hell; the game makes the steps to and from 5.
    static void HellSteps()
    {
        var toApocalypse = Step(Downfall, selected: false, increase: true, available: true, hell: true);
        var toHell = Step(Downfall, selected: true, increase: true, available: true, hell: true);
        var fromHell = Step(Hell, selected: false, increase: false, available: true, hell: true);
        var aboveHell = Step(Hell, selected: false, increase: true, available: true, hell: true);
        Check(toApocalypse == new StepResult(true, true, true) && toHell == new StepResult(false, false, true)
                && fromHell == new StepResult(false, true, true) && aboveHell == new StepResult(false, false, false),
            "difficulty: with Hell, up from Downfall is Apocalypse, up from it the game's Hell, down from Hell Apocalypse again");
        var offFromHell = Step(Hell, selected: false, increase: false, available: false, hell: true);
        var withoutHell = Step(Hell, selected: false, increase: false, available: true, hell: false);
        Check(offFromHell == new StepResult(false, false, false) && withoutHell == new StepResult(false, false, false)
                && Step(Downfall, selected: true, increase: true, available: true) == new StepResult(true, true, false),
            "difficulty: with the switch off Hell steps down to Downfall, and without Hell Apocalypse stays the top step");
        var shown = default(State).WithSelected(true, fromHell.Selected);
        Check(shown.Shows(true, Downfall) && !shown.Shows(true, Hell) && !shown.Shows(false, Downfall),
            "difficulty: after the step down from Hell the screen shows Apocalypse on the game's Downfall of that part");
    }

    static void SelectionToActive()
    {
        var request = new RequestedDifficulty(Downfall, Downfall);
        var combatOnly = Selected(true, false).Remembered(Downfall, Downfall).Begun(request);
        Check(combatOnly.ActiveCombat && !combatOnly.ActiveManagement,
            "difficulty: only the part that was selected becomes active, combat and management apart");
        var management = Selected(false, true).Remembered(Downfall, Downfall).Begun(request);
        Check(!management.ActiveCombat && management.ActiveManagement,
            "difficulty: management Apocalypse stays management only");
        var changedOnScreen = Selected(true, true).Remembered(Downfall - 1, Downfall);
        Check(!changedOnScreen.PendingCombat && changedOnScreen.PendingManagement && !changedOnScreen.Begun(request).ActiveCombat,
            "difficulty: a part whose difficulty is not Downfall at start is not remembered");
        var changedByRequest = Selected(true, true).Remembered(Downfall, Downfall)
            .Begun(new RequestedDifficulty(Downfall - 1, Downfall));
        Check(!changedByRequest.ActiveCombat && changedByRequest.ActiveManagement,
            "difficulty: a created campaign whose requested difficulty is not Downfall is not Apocalypse");
        var reopened = Selected(true, true).Remembered(Downfall, Downfall).SelectionCleared();
        Check(!reopened.CombatSelected && !reopened.PendingManagement && !reopened.Begun(request).ActiveCombat,
            "difficulty: opening the screen again clears the selection and what was remembered");
    }

    // A normal campaign after an Apocalypse campaign in the same session stays normal.
    static void NormalCampaignAfterApocalypse()
    {
        var request = new RequestedDifficulty(Downfall, Downfall);
        var apocalypse = Selected(true, true).Remembered(Downfall, Downfall).Begun(request).Created();
        var withoutScreen = apocalypse.Begun(null);
        Check(apocalypse.ActiveCombat && apocalypse.ActiveManagement && !withoutScreen.ActiveCombat && !withoutScreen.ActiveManagement
                && !withoutScreen.CombatSelected && !withoutScreen.PendingCombat,
            "difficulty: a campaign without the screen after an Apocalypse campaign has nothing active, selected or pending");
        var normal = apocalypse.Begun(request);
        Check(!normal.ActiveCombat && !normal.ActiveManagement && !normal.CombatSelected && !normal.ManagementSelected,
            "difficulty: a normal campaign from the screen after an Apocalypse campaign stays normal");
        var afterStale = Selected(true, false).Remembered(Downfall, Downfall).Begun(null).Begun(request);
        Check(!afterStale.ActiveCombat && !afterStale.ActiveManagement,
            "difficulty: a remembered start does not survive into a later creation");
        var inCreation = apocalypse with { CreatingFromScreen = true };
        var reset = inCreation.SelectionCleared().ActiveCleared();
        Check(!reset.ActiveCombat && !reset.ActiveManagement && reset.CreatingFromScreen,
            "difficulty: the lifecycle reset clears the parts and the selection only");
    }

    // The game runs a throwaway creation at the start of every load; it must not use up the test marker.
    static void LoadAndTestMarker()
    {
        var now = new DateTime(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);
        var next = new NextCampaignTest(() => now);
        var request = new RequestedDifficulty(Downfall, Downfall);

        next.Arm();
        var loading = default(State).Begun(null);
        var loadClaims = loading.ClaimsTestMark(next);
        loading = loading.Created().Loaded(new ApocalypseMark(true, false));
        Check(!loadClaims && next.Pending && loading.ActiveCombat && !loading.ActiveManagement && !loading.CreatingFromScreen,
            "difficulty: the throwaway campaign at a load does not use up the test marker, and the load sets the parts");

        var created = loading.Begun(request);
        var first = created.ClaimsTestMark(next);
        var second = created.ClaimsTestMark(next);
        Check(first && !second && !next.Pending && !created.Created().ClaimsTestMark(next),
            "difficulty: a creation from the screen uses the test marker up exactly once");

        var plain = default(State).Begun(request);
        Check(!plain.ClaimsTestMark(next) && !default(State).Loaded(default).ActiveCombat,
            "difficulty: without an armed marker nothing is claimed, and a load without a mark has no parts");

        var both = new ApocalypseMark(true, true);
        var onDownfall = Checked(both, (Downfall, Downfall));
        var onHell = Checked(both, (Hell, Downfall));
        var unknown = Checked(both, null);
        Check(onDownfall.Combat && onDownfall.Management && !onHell.Combat && onHell.Management && unknown.Combat && unknown.Management
                && !Checked(both, (Hell, Hell)).Any && !Checked(default, (Downfall, Downfall)).Any,
            "difficulty: a save's mark counts only for a part on the game's Downfall, so a Hell world never loads as Apocalypse");
        var test = Checked(both, (1, 1), testCampaign: true);
        Check(test.Combat && test.Management && !Checked(both, (1, 1)).Any,
            "difficulty: a test campaign keeps the mark the tools set on any difficulty, a normal world on Normal does not");
        var campaign = Guid.Parse("4799f5bf-0000-4000-8000-000000000001");
        Check(IgnoredLine(campaign, both, onHell, (Hell, Downfall)) == "CORE_APOCALYPSE_MARK_IGNORED campaign=4799f5bf-0000-4000-8000-000000000001 combat=5 management=-",
            "difficulty: the line names each ignored part with its game value");
    }
}
