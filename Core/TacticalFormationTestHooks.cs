namespace DungeonSettlersDelvers.Core;

// DelversCoreRuntime, the formation window's test hooks for the Toolkit's block "-Only formation": the window's steps
// without the mouse. Only in the loaded test campaign; GUIDs are strings, results BCL types, for reflection. A drag and drop is
// FormationMoveForTest (the drop of the member on the cell), so the mouse itself is checked by hand.
public static partial class DelversCoreRuntime
{
    // Opens the window like the button: in a dungeon for the expedition, in the village for every living party member there.
    public static bool FormationOpenForTest()
    {
        if (!LoadedTestCampaign()) return false;
        return FormationStep("open", () =>
        {
            TacticalFormationWindow.Open(TacticalFormation.Group(TacticalFormation.MapMembers()));
            return TacticalFormationWindow.IsOpen;
        });
    }

    // Closes the window; the places are saved as with its Close button.
    public static bool FormationCloseForTest()
        => LoadedTestCampaign() && FormationStep("close", () =>
        {
            if (!TacticalFormationWindow.IsOpen) return false;
            TacticalFormationWindow.Close(true);
            return true;
        });

    public static bool FormationSetCrownForTest(string unitGuid)
        => Guid.TryParse(unitGuid, out var unit) && LoadedTestCampaign() && FormationStep("crown", () => TacticalFormationWindow.SetCrown(unit));

    // The member dropped on the cell (column 0 left, row 0 front): an occupied cell swaps the two.
    public static bool FormationMoveForTest(string unitGuid, int column, int row)
        => Guid.TryParse(unitGuid, out var unit) && LoadedTestCampaign() && FormationStep("move", () => TacticalFormationWindow.Move(unit, column, row));

    // Two members swap their cells: the first dropped on the second.
    public static bool FormationSwapForTest(string firstGuid, string secondGuid)
    {
        if (!Guid.TryParse(firstGuid, out var first) || !Guid.TryParse(secondGuid, out var second) || !LoadedTestCampaign()) return false;
        return FormationStep("swap", () =>
        {
            var cell = TacticalFormationWindow.CellOf(second);
            return cell != null && TacticalFormationWindow.Move(first, cell.Value.Column, cell.Value.Row);
        });
    }

    // The member dropped on a row of the group list (0 = top, the crown).
    public static bool FormationListMoveForTest(string unitGuid, int index)
        => Guid.TryParse(unitGuid, out var unit) && LoadedTestCampaign() && FormationStep("list", () => TacticalFormationWindow.MoveInList(unit, index));

    // A learned active skill on one of the member's priority places (0 = first), "" empties it.
    public static bool FormationSetCastForTest(string unitGuid, int place, string skillKey)
        => Guid.TryParse(unitGuid, out var unit) && LoadedTestCampaign()
            && FormationStep("cast", () => TacticalFormationWindow.SetCast(unit, place, skillKey ?? ""));

    // The member's learned active skills the window offers, comma separated; "" for none.
    public static string FormationSkillsForTest(string unitGuid)
    {
        if (!Guid.TryParse(unitGuid, out var unit)) return "";
        try { return string.Join(",", TacticalFormation.ActiveSkills(CoreGame.FindUnit(unit))); }
        catch (Exception ex) { return "error=" + ex.Message; }
    }

    // Turns the window's book to a set, 1 to 8 as the pager counts.
    public static bool FormationPageForTest(int set)
        => LoadedTestCampaign() && FormationStep("page", () => TacticalFormationWindow.ShowPage(set - 1));

    // The member's formation switched on or off, like a left click on the button.
    public static bool FormationSwitchForTest(string unitGuid, bool on)
    {
        if (!Guid.TryParse(unitGuid, out var unit) || !LoadedTestCampaign()) return false;
        var entity = CoreGame.FindUnit(unit);
        return entity != null && FormationStep("switch", () => TacticalFormation.Switch(new[] { entity }, on));
    }

    // Whether the member keeps its place in the formation (TacticalFormation.IsOn).
    public static bool FormationIsOnForTest(string unitGuid) => Guid.TryParse(unitGuid, out var unit) && TacticalFormation.IsOn(unit);

    // "on=<0|1> sleep=<0|1> rest=<0|1>": the member's formation, whether sleep took it out (TacticalFormationSleep) and the game's
    // Prioritize Sleep; "-" for an unknown member.
    internal static string FormationSleepForTest(string unitGuid)
    {
        if (!Guid.TryParse(unitGuid, out var unit) || CoreSaveStore.Current == null) return "-";
        var entity = CoreGame.FindUnit(unit);
        if (entity == null) return "-";
        return $"on={(TacticalFormation.IsOn(unit) ? 1 : 0)} sleep={(CoreSaveStore.Current.IsFormationSleep(unit) ? 1 : 0)} rest={(TacticalFormationSleep.IsRestFirst(entity) ? 1 : 0)}";
    }

    public static bool FormationResetForTest() => LoadedTestCampaign() && FormationStep("reset", TacticalFormationWindow.ResetGrid);

    // "Test formation": saves, closes and lines the group up; the CORE_TACTICAL_TEST_* lines follow.
    public static bool FormationTestForTest() => LoadedTestCampaign() && FormationStep("test", TacticalFormationWindow.RunTest);

    // "open=1 leader=<guid> cells=<column>,<row>:<guid>|… saved=<leader>:<places>|… test=idle|running".
    public static string FormationStatusForTest()
    {
        try { return TacticalFormationWindow.Status(); }
        catch (Exception ex) { return "error=" + ex.Message; }
    }

    private static bool FormationStep(string step, Func<bool> action)
    {
        bool done;
        try { done = action(); }
        catch (Exception ex)
        {
            DelversHost.Warning($"CORE_TEST_FORMATION_FAILED step={step}: {ex.Message}");
            return false;
        }
        DelversHost.Info($"CORE_TEST_FORMATION step={step} ok={done}");
        return done;
    }
}
