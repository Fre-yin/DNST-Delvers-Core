namespace DungeonSettlersDelvers.Core;

// DelversCoreRuntime, the formation window's test hooks for the formation tests: the window's steps
// without the mouse. Only in the loaded test campaign; GUIDs are strings, results BCL types, for reflection. A drag and drop is
// FormationMoveForTest (the drop of the member on the cell), so the mouse itself is checked by hand.
public static partial class DelversCoreRuntime
{
    // Opens the window like the button. Each set keeps its own rows: a saved set opens with its rows, only an empty one starts
    // with the party (in a dungeon the expedition, in the village every living member there). A member not in the rows comes
    // in through FormationAddForTest, as with the window's "+".
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

    // The fixed selection (TacticalFormationWindowSelection), like the "+" choice and a right click while the window is open: a
    // member wherever it is, a summon place of a summoner in the set and a kind (the unit key), a row taken out.
    public static bool FormationAddForTest(string unitGuid)
        => Guid.TryParse(unitGuid, out var unit) && LoadedTestCampaign() && FormationStep("add", () => TacticalFormationWindow.AddMember(unit));

    public static bool FormationAddSummonForTest(string summonerGuid, string kind)
        => Guid.TryParse(summonerGuid, out var summoner) && LoadedTestCampaign() && FormationStep("add-summon", () => TacticalFormationWindow.AddSummon(summoner, kind));

    public static bool FormationRemoveForTest(string unitGuid)
        => Guid.TryParse(unitGuid, out var unit) && LoadedTestCampaign() && FormationStep("remove", () => TacticalFormationWindow.RemoveRow(unit));

    // A member's role in the shown formation, like a choice in its role field: tank, damage, support or neutral.
    public static bool FormationSetRoleForTest(string unitGuid, string role)
        => Guid.TryParse(unitGuid, out var unit) && LoadedTestCampaign()
           && FormationStep("role", () => TacticalFormationWindow.SetRole(unit, TacticalRoles.Parse(role)));

    // A member's role as the group AI reads it in the dungeon: "role=<word> acts=<rule set|-> reach=<cells> out=<0|1> roam=<0|1>
    // center=<x>,<y>" (GroupAiRoles, GroupAiRoleMoves: out is a tank gathering, roam a damage dealer gone to the foes; center the cell
    // the places stand around, GroupAiFormation.Center).
    public static string FormationRoleStatusForTest(string unitGuid)
    {
        try
        {
            if (!Guid.TryParse(unitGuid, out var unit)) return "error=guid";
            var guid = unit.ToString("D");
            var center = GroupAiFormation.Center;
            return $"role={TacticalRoles.Word(GroupAiRoles.PlayerOf(guid)) ?? "neutral"} acts={GroupAiRoles.Known(guid)?.ToString() ?? "-"} {GroupAiRoleMoves.Status(unit)} center={center.X},{center.Y}";
        }
        catch (Exception ex) { return "error=" + ex.Message; }
    }

    // What "+" offers now, "<key>=<name>/<where or summoner>|…" (a member's key its GUID, a summon kind's "<kind>|<summoner>" written
    // with ";" for the bar); "-" for nothing.
    public static string FormationCandidatesForTest()
    {
        try
        {
            var found = TacticalFormationWindow.Candidates();
            return found.Count == 0 ? "-" : string.Join("|", found.Select(c => c.Key.Replace('|', ';') + "=" + c.Name + "/" + c.Note));
        }
        catch (Exception ex) { return "error=" + ex.Message; }
    }

    // The tempo button below the grid, like a click on it while the window is open (TacticalFormationTempo).
    public static bool FormationTempoForTest(bool on) => LoadedTestCampaign() && FormationStep("tempo", () => TacticalFormationWindow.SetTempo(on));

    // "on=<0|1> stacks=<unit>:<stacks>,…|-": the button and the stacks of Core's tempo effect on the party (TacticalFormationTempo.Status).
    public static string FormationTempoStatusForTest()
    {
        try { return TacticalFormationTempo.Status(); }
        catch (Exception ex) { return "error=" + ex.Message; }
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
