namespace DungeonSettlersDelvers.Core;

// Which parts of a campaign run on Apocalypse. The save itself keeps the native Downfall level,
// so a save without Core loads as Downfall and no unknown level reaches the save or the game's
// analytics. The mark lives in Core's block of the save (CoreSaveData) and travels with it, Steam
// Cloud included.
internal readonly struct ApocalypseMark
{
    internal ApocalypseMark(bool combat, bool management) { Combat = combat; Management = management; }

    internal bool Combat { get; }
    internal bool Management { get; }
    internal bool Any => Combat || Management;
}

// The pure part of ApocalypseDifficulty, so the offline tests cover it: what the
// campaign screen has selected, what is remembered when the player starts, which Apocalypse parts the
// loaded or newly created campaign has, and whether a creation may use up the "next campaign is a test
// campaign" marker. ApocalypseDifficulty keeps one State and does the game side (panel, log, save data).
internal static class ApocalypseDifficultyRules
{
    // The game's highest level. Apocalypse is the step above it: the screen shows it while the game's
    // own value stays Downfall.
    internal const int Downfall = 4;

    // The difficulties of a campaign the game is about to create from the campaign screen.
    internal readonly record struct RequestedDifficulty(int Combat, int Management);

    // The result of one arrow step for one part: whether Core handled it (the game's step is then
    // skipped), the selection afterwards, and whether the selection changed.
    internal readonly record struct StepResult(bool Handled, bool Selected, bool Changed);

    // Everything Core remembers about Apocalypse while the screen, a creation or a load runs.
    internal readonly record struct State(bool CombatSelected, bool ManagementSelected,
        bool PendingCombat, bool PendingManagement, bool CreatingFromScreen, bool ActiveCombat, bool ActiveManagement)
    {
        internal bool Selected(bool combat) => combat ? CombatSelected : ManagementSelected;

        // True while the screen shows Apocalypse in place of the game's Downfall step.
        internal bool Shows(bool combat, int difficulty) => difficulty == Downfall && Selected(combat);

        internal State WithSelected(bool combat, bool selected)
            => combat ? this with { CombatSelected = selected } : this with { ManagementSelected = selected };

        // The screen opens again, or a creation consumed the choice: nothing is selected or pending.
        internal State SelectionCleared()
            => this with { CombatSelected = false, ManagementSelected = false, PendingCombat = false, PendingManagement = false };

        internal State ActiveCleared() => this with { ActiveCombat = false, ActiveManagement = false };

        // The screen's last call before the game creates the campaign.
        internal State Remembered(int combatDifficulty, int managementDifficulty)
            => this with
            {
                PendingCombat = CombatSelected && combatDifficulty == Downfall,
                PendingManagement = ManagementSelected && managementDifficulty == Downfall,
            };

        // The game begins a new campaign. Without a request (the game also does this at the start of
        // every load, with a throwaway campaign) nothing of an earlier campaign may stay active, and
        // the creation is not the screen's.
        internal State Begun(RequestedDifficulty? request)
        {
            var combat = request is { } asked && PendingCombat && asked.Combat == Downfall;
            var management = request is { } other && PendingManagement && other.Management == Downfall;
            return SelectionCleared() with { ActiveCombat = combat, ActiveManagement = management, CreatingFromScreen = request != null };
        }

        // A save is applied: the campaign has exactly the parts its mark carries.
        internal State Loaded(ApocalypseMark mark) => this with { ActiveCombat = mark.Combat, ActiveManagement = mark.Management };

        // Only a creation from the screen may use up the armed test marker; a load's throwaway
        // campaign must not (the marker is only asked when the creation is the screen's).
        internal bool ClaimsTestMark(NextCampaignTest next) => CreatingFromScreen && next.Consume();

        // The new campaign exists and has been bound.
        internal State Created() => this with { CreatingFromScreen = false };
    }

    // One arrow step on the campaign screen for one part. current is the game's value on the screen,
    // selected whether Apocalypse is selected for the part, available whether the screen may offer it
    // (switch on, Downfall unlocked).
    internal static StepResult Step(int current, bool selected, bool increase, bool available)
    {
        // Any native step away from Downfall drops a selection.
        if (current != Downfall) return new StepResult(false, false, false);
        if (increase)
        {
            if (selected) return new StepResult(true, true, false);   // already the top step
            return available ? new StepResult(true, true, true) : new StepResult(false, false, false);
        }
        // Without a selection the game steps down from Downfall to VeryHard.
        return selected ? new StepResult(true, false, true) : new StepResult(false, false, false);
    }
}
