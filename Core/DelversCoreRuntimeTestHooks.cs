namespace DungeonSettlersDelvers.Core;

// DelversCoreRuntime, the test hooks: wounds, enemy abilities, first hints, the camp probes, the slow motion, the group AI,
// Lifebloom, corpses, bandits, the next test campaign and the equipment marker's summary. The Toolkit calls them by name;
// most act only in the loaded test campaign.
public static partial class DelversCoreRuntime
{
    // The bleeding wound of combat Apocalypse and the creation of a test
    // campaign through the game's screen. GUIDs are strings, results BCL types, for reflection.
    public const string MinimumApiVersionForWounds = "1.8.0";

    // Test hook, only in the loaded test campaign: the wound gets this many stacks (BufferPerStack HP each);
    // 0 closes it. Works without Apocalypse, so a test needs no new campaign.
    public static bool SetWoundForTest(string unitGuid, int stacks)
    {
        if (!Guid.TryParse(unitGuid, out var unit) || unit == Guid.Empty || !LoadedTestCampaign()) return false;
        try
        {
            Wounds.Set(unit, Math.Max(0, stacks) * WoundRules.BufferPerStack, "test");
            DelversHost.Info($"CORE_TEST_WOUND unit={unit:D} stacks={Math.Max(0, stacks)}");
            return true;
        }
        catch (Exception ex)
        {
            DelversHost.Warning("CORE_TEST_WOUND_FAILED: " + ex.Message);
            return false;
        }
    }

    // "stage=deep stacks=15 buffer=150.0" or "none".
    public static string WoundStatus(string unitGuid)
    {
        if (!Guid.TryParse(unitGuid, out var unit)) return "none";
        try { return Wounds.Status(unit); }
        catch (Exception ex)
        {
            DelversHost.Warning("CORE_WOUND_API_READ_FAILED: " + ex.Message);
            return "none";
        }
    }

    // Probe for the wound on the body, only in the loaded test campaign: plays the game's bleeding
    // effect at the unit ("drop" at the game's height, "center" at the middle of the body) or a
    // blood stain on the ground ("ground"). Returns the CORE_WOUND_FX line, or null.
    public static string PlayWoundFxForTest(string unitGuid, string kind)
    {
        if (!Guid.TryParse(unitGuid, out var unit) || unit == Guid.Empty || !WoundFx.TryParse(kind, out var parsed) || !LoadedTestCampaign()) return null;
        try { return WoundFx.Play(unit, parsed); }
        catch (Exception ex)
        {
            DelversHost.Warning("CORE_WOUND_FX_FAILED: " + ex.Message);
            return null;
        }
    }

    // Test hook, only in the loaded test campaign: how an open wound shows on the body until
    // the author has chosen: "pop" (the game's short bleeding pop, default), "loop" (the same as
    // "pop"; the looping pool lies on the ground now) or "off". Not saved; a restart goes back to "pop".
    public static bool SetWoundFxModeForTest(string mode)
    {
        if (!WoundFx.TryParseMode(mode, out var parsed) || !LoadedTestCampaign()) return false;
        WoundFx.SetMode(parsed);
        DelversHost.Info("CORE_TEST_WOUND_FX_MODE mode=" + mode);
        return true;
    }

    // Health the unit's open wound has drained since it opened (Wounds.DrainedTotal); 0 without.
    public static double WoundDrainTotalForTest(string unitGuid)
        => Guid.TryParse(unitGuid, out var unit) ? Wounds.DrainedTotal(unit) : 0;

    // ---- enemy abilities (EnemyAbilities), only in the loaded test campaign ----
    // kind: "bleed", "silence" or "sleep" (EnemyAbilityRules.TryParse).

    // outcome: "always", "never" or "clear" (back to the roll).
    public static bool SetEnemyAbilityRollForTest(string kind, string outcome)
    {
        if (!EnemyAbilityRules.TryParse(kind, out var parsed) || !LoadedTestCampaign()) return false;
        bool? forced;
        switch (outcome)
        {
            case "always": forced = true; break;
            case "never": forced = false; break;
            case "clear": forced = null; break;
            default: return false;
        }
        EnemyAbilities.SetForced(parsed, forced);
        DelversHost.Info("CORE_TEST_ENEMY_ROLL kind=" + kind + " outcome=" + outcome);
        return true;
    }

    // chance in [0, 1]; below 0 clears it.
    public static bool SetEnemyAbilityChanceForTest(string kind, float chance)
    {
        if (!EnemyAbilityRules.TryParse(kind, out var parsed) || chance > 1f || float.IsNaN(chance) || !LoadedTestCampaign()) return false;
        EnemyAbilities.SetChance(parsed, chance < 0f ? null : chance);
        DelversHost.Info("CORE_TEST_ENEMY_CHANCE kind=" + kind + " chance="
            + (chance < 0f ? "clear" : chance.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)));
        return true;
    }

    // Only "bleed": the stacks of a bleeding hit, up to 99; 0 or below clears it.
    public static bool SetEnemyAbilityStacksForTest(string kind, int stacks)
    {
        if (kind != "bleed" || stacks > 99 || !LoadedTestCampaign()) return false;
        EnemyAbilities.SetStacks(EnemyAbilityRules.Kind.Bleed, stacks <= 0 ? null : stacks);
        DelversHost.Info("CORE_TEST_ENEMY_STACKS kind=bleed stacks=" + (stacks <= 0 ? "clear" : stacks.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        return true;
    }

    public static string EnemyAbilityStatus(string kind)
        => EnemyAbilityRules.TryParse(kind, out var parsed) ? EnemyAbilities.Status(parsed) : null;

    // ---- first hints (FirstHints) ----

    // Shows one hint now, also when it was shown before; only in the loaded test campaign.
    public static bool ShowHintForTest(string key)
    {
        if (Array.IndexOf(FirstHintText.Keys, key) < 0 || !LoadedTestCampaign()) return false;
        try { return FirstHints.Show(key, "test"); }
        catch (Exception ex)
        {
            DelversHost.Warning("CORE_TEST_HINT_FAILED: " + ex.Message);
            return false;
        }
    }

    // Every hint counts as not shown again.
    public static bool ResetHintsForTest()
    {
        FirstHints.ForgetAll();
        DelversHost.Info("CORE_TEST_HINTS_RESET");
        return true;
    }

    // Hints on or off for this session, whatever the menu says.
    public static bool SetHintsForTest(bool on)
    {
        FirstHints.SetForTest(on);
        return true;
    }

    // Lifts SetHintsForTest; the menu switch (CoreSettings) decides again.
    public static bool ClearHintsForTest()
    {
        FirstHints.ClearTest();
        return true;
    }

    public static string HintsStatus() => FirstHints.Status();

    // ---- room graph probe of the bandits' camp (BanditCampProbe): only logs ----

    // Logs every generated forest floor 4 (CORE_BANDITS_GRAPH*) for this session; false switches it off.
    public static bool SetCampProbeForTest(bool on)
    {
        BanditCampProbe.SetTest(on);
        return true;
    }

    // Generates nothing: whether the dungeon generator and its level inputs are reachable here
    // (CORE_BANDITS_GRAPH_RECON, also returned).
    public static string CampProbeReconForTest() => BanditCampProbe.Recon();

    // Reads only: the forest floor-4 templates (door mode, door places, size, repeat mode) and level graphs
    // (CORE_BANDITS_TEMPLATE, CORE_BANDITS_LEVELGRAPH); returns a short summary.
    public static string CampTemplatesForTest() => BanditCampTemplates.Probe();

    // Generates forest floor 4 for count seeds from firstSeed with the game's seed scan, only in a loaded test
    // campaign at the base (CORE_BANDITS_SEEDSCAN*); returns "started …" or "error=…". Status while it runs:
    // CampSeedScanStatusForTest.
    public static string CampSeedScanForTest(int firstSeed, int count)
        => LoadedTestCampaign() ? BanditCampTemplates.StartScan(firstSeed, count) : "error=no-test-campaign";

    public static string CampSeedScanStatusForTest() => BanditCampTemplates.ScanStatus();

    // The bandit camp of the next forest floors 4, only in a loaded test campaign: "normal" or "mirrored" builds that form where
    // the floor allows a camp, "force" the normal one also on a floor with the Alpha, "off" builds none, "auto" takes the seed's
    // pick (CORE_TEST_BANDIT_CAMP). Not saved; false for another mode or outside a test campaign.
    public static bool BanditCampForTest(string mode)
    {
        if (!LoadedTestCampaign() || !BanditCampBuilder.SetTestMode(mode)) return false;
        DelversHost.Info("CORE_TEST_BANDIT_CAMP mode=" + mode);
        return true;
    }

    // Reads only: the clock panel, the canvas, the speed buttons and the game's vignettes for the display
    // of the slow motion (CORE_SLOWUI_*); returns a short summary.
    public static string SlowUiProbeForTest() => ApocalypseSlowProbe.Run();

    // Apocalypse's slow motion in a loaded test campaign: "press" (the player's pause), "charge=<seconds>"
    // or "status"; returns "slow= charge= display=" or "error=…" (CORE_TEST_APOCALYPSE_SLOW).
    public static string ApocalypseSlowForTest(string command)
    {
        var result = LoadedTestCampaign() ? ApocalypseTime.TestCommand(command) : "error=no-test-campaign";
        DelversHost.Info("CORE_TEST_APOCALYPSE_SLOW command=" + (string.IsNullOrWhiteSpace(command) ? "-" : command.Trim().Replace(' ', '_')) + " " + result);
        return result;
    }

    // ---- group AI (GroupAiPolicy), Core's enemies and Lifebloom: still in testing ----

    public static bool SetGroupAiForTest(bool on) => GroupAiSettings.SetForTest(on);

    public static string GroupAiStatus() => GroupAiPolicy.Status();

    // Teaches the unit Lifebloom; false when the skill row is missing or the unit is not found.
    public static bool GrantLifebloomForTest(string unitGuid)
    {
        if (!Lifebloom.Active || !Guid.TryParse(unitGuid, out var guid)) return false;
        var unit = CoreGame.FindUnit(guid);
        if (unit == null) return false;
        var tree = Lifebloom.Learn(unit);
        DelversHost.Info($"CORE_TEST_LIFEBLOOM_GRANTED unit={guid:D} tree={tree}");
        return true;
    }

    // What Lifebloom healed per target this campaign: "targets=2 <guid>=123.4/15/3;…" (healed, heals,
    // of them at full health without a heal number).
    public static string LifebloomHealedForTest() => Lifebloom.HealedStatus();

    public static string LifebloomStatus(string unitGuid)
        => Guid.TryParse(unitGuid, out var guid) ? Lifebloom.Status(guid) : null;

    public static string GroupAiBanditStatus() => GroupAiBandits.Status();

    // Test hook: with on the unit counts nowhere for the group AI, neither as a
    // hostile of the bandits (fallen last, hostiles=, protect target, ring, formation) nor as an ally of the
    // party, and Core decides nothing for it; on=false lets it in again. Holds for the session.
    public static bool ExcludeFromGroupAiForTest(string unitGuid, bool on)
    {
        if (!Guid.TryParse(unitGuid, out var guid) || guid == Guid.Empty) return false;
        var done = GroupAiExcluded.Set(guid.ToString("D"), on);
        DelversHost.Info($"CORE_TEST_AI_EXCLUDE unit={guid:D} on={on} count={GroupAiExcluded.Count}");
        return done;
    }

    // Equipment on the body (CorpseKeep): the corpse's state for the Toolkit,
    // "unit=<guid> found=1 dead=1 player=1 items=3 keys=A+B+C carried=0 undressed=0 listed=0" or "found=0".
    // Reads only.
    public static string CorpseStatusForTest(string unitGuid)
        => Guid.TryParse(unitGuid, out var guid) && guid != Guid.Empty ? CorpseKeep.Status(guid) : null;

    // Core's value for the action "undress" (step 2), so the Toolkit finds it in the game's action list.
    public static int CorpseUndressValueForTest() => CorpseRules.UndressTask;

    // The forest bandits (ApocalypseRules.Bandits): only in combat Apocalypse; the Toolkit's override
    // holds for the session, ClearBanditsForTest lifts it.
    public static bool SetBanditsForTest(bool on)
    {
        ApocalypseRules.SetBanditsTest(on);
        return true;
    }

    public static bool ClearBanditsForTest()
    {
        ApocalypseRules.SetBanditsTest(null);
        return true;
    }

    public static bool BanditsActive() => ApocalypseRules.Bandits;

    // Shift held for the bandage on another unit (BandageOnOther), only in a marked test campaign; false lifts it. For checks of
    // Core's handler only: it skips the game's binding check, which a real Shift has to pass (ShiftTwin), and so hid that gap.
    public static bool SetShiftForTest(bool held)
    {
        if (held && !LoadedTestCampaign()) return false;
        BandageOnOther.SetShiftTest(held);
        return true;
    }

    // Test tool: the herbalist healerGuid casts her Strange Seed at targetGuid's cell (null: the nearest hostile) at her next
    // decision in combat, or with upgrade grows her seed into the mucus sprayer. Returns queued or nohealer;
    // CORE_TEST_BANDIT_SEED shows the result of the decision.
    public static string PlantBanditSeedForTest(string healerGuid, string targetGuid = null, bool upgrade = false)
    {
        var healer = Guid.TryParse(healerGuid, out var guid) ? CoreGame.FindUnit(guid) : null;
        var result = healer == null || healer.IsDead || healer.Key != CoreUnitRules.Prefix + "ForestBanditHealer" ? "nohealer" : "queued";
        if (result == "queued") GroupAiBandits.RequestTestSeed(healer.Guid.ToString(), string.IsNullOrEmpty(targetGuid) ? null : targetGuid, upgrade);
        DelversHost.Info(GroupAiHealerSeedRules.TestSeedLine(healerGuid, targetGuid, upgrade, result));
        return result;
    }

    // Test tool for areas (API 1.10.0; a skill order does nothing for enemies): the enemy unitGuid
    // uses skillKey at its next combat decision, at target, a unit GUID or a cell "x,y" (null for skills on the user itself).
    // Only in the loaded test campaign. Returns queued, notest, nounit, noskill, notarget, nopattern, nohook or failed;
    // CORE_TEST_ENEMY_SKILL logs the decision: cast, noskill, cooldown, notarget, range or forced (taunted, for a skill on a
    // cell; the game would lose the cell). Takes effect only when called.
    public static string UseEnemySkillForTest(string unitGuid, string skillKey, string target = null)
    {
        string result;
        try { result = LoadedTestCampaign() ? EnemySkillTest.Queue(unitGuid, skillKey, target) : "notest"; }
        catch (Exception ex)
        {
            DelversHost.Warning("CORE_TEST_ENEMY_SKILL_FAILED: " + ex.Message);
            result = "failed";
        }
        DelversHost.Info(EnemySkillTestRules.Line(unitGuid, skillKey, target, result));
        return result;
    }

    // Whether the bandits shield their healer (on by default).
    public static bool SetBanditProtectForTest(bool on)
    {
        GroupAiBandits.ProtectHealer = on;
        DelversHost.Info($"CORE_TEST_BANDIT_PROTECT on={on}");
        return true;
    }

    // Starts the group AI's measuring window anew.
    public static bool GroupAiResetWindow()
    {
        GroupAiPolicy.ResetWindow();
        DelversHost.Info("CORE_AI_WINDOW_RESET");
        return true;
    }

    // Test hook, only in the loaded test campaign: the unit's next bleeding roll is fixed.
    // outcome: "none", "extra", "jump", a value in [0, 1) such as "0.05" (for the elite chances), or
    // "clear" (no fixed roll, jump cooldown lifted). "extra" stays a deep cut for normal and elite
    // attackers alike.
    public static bool SetWoundRollForTest(string unitGuid, string outcome)
    {
        if (!Guid.TryParse(unitGuid, out var unit) || unit == Guid.Empty || !LoadedTestCampaign()) return false;
        double? value;
        switch (outcome)
        {
            case "none": value = 0.99; break;
            case "extra": value = WoundRules.JumpChance * WoundRules.EliteChanceFactor + 0.0001; break;
            case "jump": value = 0.0; break;
            case "clear": value = null; break;
            default:
                if (!double.TryParse(outcome, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v)
                    || v < 0 || v >= 1) return false;
                value = v;
                break;
        }
        try
        {
            Wounds.ForceRoll(unit, value);
            DelversHost.Info($"CORE_TEST_WOUND_ROLL unit={unit:D} next={outcome}");
            return true;
        }
        catch (Exception ex)
        {
            DelversHost.Warning("CORE_TEST_WOUND_ROLL_FAILED: " + ex.Message);
            return false;
        }
    }

    // The next campaign created through the game's screen becomes a test campaign by its GUID, and
    // for that creation every matching founder offer goes through. A load or 15 minutes disarm it.
    public static bool MarkNextNewCampaignAsTest()
    {
        NextCampaignTest.Shared.Arm();
        DelversHost.Info("CORE_TEST_NEXT_CAMPAIGN_ARMED minutes=" + NextCampaignTest.Lifetime.TotalMinutes);
        return true;
    }

    public static bool IsNextNewCampaignMarkedAsTest() => NextCampaignTest.Shared.Pending;

    // What Apocalypse does right now, for tests, e.g.
    // "combat=true management=false noPause=true slow=false charge=5.0 ambushOff=true sharpened=3".
    public static string ApocalypseStatus() => ApocalypseDifficulty.Describe();

    // Test hook the equipment marker's summary since the last bind, word for word; only in the loaded
    // test campaign, else null, and null once the marker is off. Only reads: no line, no settling.
    public static string EquipmentMarkerSummaryForTest() => LoadedTestCampaign() ? EquipmentMarker.SummaryForTest() : null;
}
