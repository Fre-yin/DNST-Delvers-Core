using DungeonSettlersDelvers.Core;
using static TestKit;

// Group AI: healer stance, shield, cone, counter taunt and vines reasons.
internal static class GroupAiStanceSection
{
    internal static void Run()
    {
        HealerStance();
        StanceUnderTaunt();
        PlayerOrderQuiet();
        ConeTurn();
        ShieldDefence();
        CounterTauntAndVines();
        BandageAtLethalWound();
    }

    static void HealerStance()
    {
        // The party healer keeps about 7 cells from the nearest foe, steps in
        // to 5 for the seed or Lifebloom and back after the cast, and into danger only in the author's emergency (head health only, at
        // most 30 % of it, or a charged bleeding of 70 % of the maximum); the player's order and 3 s after it go first.
        Check(GroupAiRules.HealerStance(7, 7, false, false, false, false) == "hold" && GroupAiRules.HealerStance(6, 7, false, false, false, false) == "hold"
                && GroupAiRules.HealerStance(8, 7, false, false, false, false) == "hold" && GroupAiRules.HealerStance(5, 7, false, false, false, false) == "back"
                && GroupAiRules.HealerStance(4, 7, false, false, false, false) == "back"
                && GroupAiRules.HealerStance(9, 7, true, false, false, false) == "in" && GroupAiRules.HealerStance(9, 7, true, true, false, false) == "in"
                && GroupAiRules.HealerStance(5, 7, false, true, false, false) == "return"
                && GroupAiRules.HealerStance(5, 7, true, false, true, false) == "emergency"
                && GroupAiRules.HealerStance(2, 7, true, true, true, true) == "none"
                // Held in formation only the emergency walk moves her; with the comrade in reach "back" waits.
                && GroupAiRules.StanceMovesHeld(true, "emergency") && !GroupAiRules.StanceMovesHeld(true, "back")
                && !GroupAiRules.StanceMovesHeld(true, "in") && GroupAiRules.StanceMovesHeld(false, "back")
                && GroupAiRules.StanceApproach(false, 5) == (8, 0, 5) && GroupAiRules.StanceApproach(true, 5) == (12, 1, 1)
                && GroupAiRules.HealerEmergency(0, 80, 100, 0, 300) && GroupAiRules.HealerEmergency(50, 30, 100, 0, 300)
                && !GroupAiRules.HealerEmergency(50, 31, 100, 0, 300) && GroupAiRules.HealerEmergency(200, 100, 100, 210, 300)
                && !GroupAiRules.HealerEmergency(200, 100, 100, 209, 300)
                && GroupAiRules.StanceCells(null, 7) == 7 && GroupAiRules.StanceCells(9, 7) == 9 && GroupAiRules.StanceCells(2, 7) == 7
                && GroupAiRules.StanceCells(11, 5) == 5 && GroupAiRules.StanceCells(double.NaN, 5) == 5
                && GroupAiRules.StanceQuietSeconds == 3f && GroupAiRules.StanceDistanceDefault == 7 && GroupAiRules.StanceCastDistanceDefault == 5
                && GroupAiRules.StanceLine("f", "in", 9, "3,4", "approach", 12) == "CORE_AI_STANCE unit=f mode=in nearest=9 to=3,4 reason=approach tick=12"
                && GroupAiRules.StanceLine("f", "hold", int.MaxValue, null, null, 1) == "CORE_AI_STANCE unit=f mode=hold nearest=-1 to=- reason=- tick=1",
            "party healer: about 7 cells from the nearest foe, in to 5 for a heal out of reach and back, danger only in Fre-yin's emergency");
    }

    static void StanceUnderTaunt()
    {
        // Under an enemy's taunt the stance keeps her place and closes in only when the taunter
        // is out of every reach, never nearer than 3.
        Check(GroupAiRules.StanceTauntApproach(true, 9, 8) && !GroupAiRules.StanceTauntApproach(true, 8, 8) && !GroupAiRules.StanceTauntApproach(false, 9, 8)
                && !GroupAiRules.StanceTauntApproach(true, 9, 0) && GroupAiRules.StanceTauntMinDistance == 3
                && GroupAiRules.HealerStance(7, 7, false, false, false, false, true) == "taunt"
                && GroupAiRules.HealerStance(7, 7, false, false, true, false, true) == "emergency"
                // The healer's skills need the exact range.
                && GroupAiRules.InRangeExact(5, 5) && !GroupAiRules.InRangeExact(6, 5) && GroupAiRules.InRange(6, 5) && GroupAiRules.InRangeExact(9, 0)
                // The fight ends when she is down or 3 s out of combat.
                && GroupAiRules.StanceFightOver(true, true, 0f) && GroupAiRules.StanceFightOver(false, false, 3f) && !GroupAiRules.StanceFightOver(false, false, 2.9f)
                && !GroupAiRules.StanceFightOver(false, true, 9f),
            "party healer: an enemy's taunt keeps the target but not her place; out of reach she closes in, never nearer than 3");
    }

    static void PlayerOrderQuiet()
    {
        // A player's order keeps the AI quiet for 3 s; after that a clicked target
        // (not an enemy's taunt) leaves the forced slot and stays the focus; the order line and hp= in the stance line.
        Check(GroupAiRules.StancePlayerQuiet(10f, 8f) && !GroupAiRules.StancePlayerQuiet(11f, 8f) && !GroupAiRules.StancePlayerQuiet(5f, float.MinValue)
                && GroupAiRules.StancePlayerQuiet(5f, 9f) == false
                && GroupAiRules.StanceReleaseClick(false, "a", "a", false) && !GroupAiRules.StanceReleaseClick(true, "a", "a", false)
                && !GroupAiRules.StanceReleaseClick(false, "a", "a", true) && !GroupAiRules.StanceReleaseClick(false, "a", "b", false)
                && !GroupAiRules.StanceReleaseClick(false, null, null, false)
                && GroupAiRules.StanceOrderLine("f", "player", "MoveTask", null, 7) == "CORE_AI_ORDER unit=f source=player task=MoveTask forced=- tick=7"
                && GroupAiRules.StanceOrderLine("f", "click", null, "k", 8) == "CORE_AI_ORDER unit=f source=click task=- forced=k tick=8"
                && GroupAiRules.StanceLine("f", "in", 9, "3,4", "approach", 12, 0.5f) == "CORE_AI_STANCE unit=f mode=in nearest=9 to=3,4 reason=approach hp=0.50 tick=12",
            "party stance: 3 s quiet after a player's order, then a clicked target becomes the focus");
    }

    static void ConeTurn()
    {
        // (010641: blade on 452,151 struck Darius on 451,152, the cone took the fallen Frieren on 452,152):
        // the greatsword's basic attack turns to a foe in reach whose cone is clear; without one it strikes anyway.
        Check(BanditConeRules.ConeBasic("BasicGreatswordAttack") && !BanditConeRules.ConeBasic("BasicClubAttack")
                && BanditConeRules.ConePick(false, new[] { ("darius", true), ("korgas", false) }) == ("switch", "korgas")
                && BanditConeRules.ConePick(false, new[] { ("darius", true) }) == ("none", "darius")
                && BanditConeRules.ConePick(false, new[] { ("darius", false), ("korgas", false) }) == ("keep", "darius")
                && BanditConeRules.ConePick(true, new[] { ("darius", true), ("korgas", false) }) == ("keep", "darius")
                && BanditConeRules.ConePick(false, new (string, bool)[0]) == ("keep", null)
                && BanditConeRules.ConeLine("b", "BasicGreatswordAttack", "switch", "d", "k", 1, 26953)
                    == "CORE_AI_BANDIT_AREA_DOWNED unit=b skill=BasicGreatswordAttack downed=1 action=switch from=d to=k tick=26953",
            "bandit greatsword: the basic attack's cone spares a fallen unit while another foe in reach has a clear cone");
        // Taunted, the cone check does not turn the bandit away from its taunter.
        Check(BanditConeRules.ConeMayTurn(false) && !BanditConeRules.ConeMayTurn(true),
            "C18j: a taunted bandit stays on its taunter despite a fallen unit in the cone");
    }

    static void ShieldDefence()
    {
        // The shield defends the healer first, else the lowest below 35 %; taunt when free,
        // in reach and due, else turn to an attacker that stood 1 s; ranged units see foes up to 8 and walk in while no foe is
        // within 3, two steps at a time, never nearer than their range.
        Check(GroupAiReachRules.CounterVictim("h", new[] { ("p", 0.2f, 1), ("h", 0.9f, 1) }) == "h"
                && GroupAiReachRules.CounterVictim("h", new[] { ("p", 0.2f, 1), ("q", 0.3f, 2), ("h", 0.9f, 0) }) == "p"
                && GroupAiReachRules.CounterVictim("h", new[] { ("p", 0.5f, 1), ("h", 0.9f, 0) }) == null
                && GroupAiReachRules.CounterVictim(null, new[] { ("p", 0.2f, 0) }) == null
                && GroupAiReachRules.CounterAction(true, true, true, 0f, false, false) == ("taunt", "taunt")
                && GroupAiReachRules.CounterSeconds(null) == 8f && GroupAiReachRules.CounterSeconds(12) == 12f && GroupAiReachRules.CounterSeconds(0) == 8f
                && GroupAiReachRules.RangedReachable(8, 5, 6) && !GroupAiReachRules.RangedReachable(8, 5, 3) && GroupAiReachRules.RangedReachable(5, 5, 1)
                && !GroupAiReachRules.RangedReachable(9, 9, 9)
                && GroupAiReachRules.ReachAction(true, 7, 5, 7) == "walk" && GroupAiReachRules.ReachAction(true, 7, 5, 3) == "blocked"
                && GroupAiReachRules.ReachAction(true, 5, 5, 5) == "none" && GroupAiReachRules.ReachAction(false, 9, 5, 9) == "none"
                && GroupAiReachRules.ReachStep(0, 0, 10, 4, 5) == (2, 2) && GroupAiReachRules.ReachStep(0, 0, 6, 0, 5) == (1, 0)
                && GroupAiReachRules.ReachStep(0, 0, 3, 0, 5) == (0, 0)
                && GroupAiReachRules.CounterLine("d", "h", "a", "taunt", "healer", "taunt", 5) == "CORE_AI_COUNTERTANK unit=d victim=h attacker=a action=taunt reason=healer why=taunt tick=5"
                && GroupAiReachRules.ReachLine("k", "walk", "t", "2,2", 7, 5, int.MaxValue, 6) == "CORE_AI_REACH unit=k action=walk target=t to=2,2 distance=7 range=5 nearest=-1 tick=6",
            "C24: counter tank for the healer and the low, ranged focus reach 8 and walking in while no foe is within 3");
        // The shield turns at once when on no attacker, holds 1 s only to switch between attackers;
        // the ranged focus no longer drops the held foe or an enemy healer for a foe stepping close, shoots the nearest in range
        // meanwhile; the bandits' healer kites only while a capable hostile is left.
        Check(GroupAiReachRules.CounterAction(false, false, false, 0f, false, false) == ("retarget", "free")
                && GroupAiReachRules.CounterAction(false, false, false, 0.5f, true, false) == ("wait", "on-attacker")
                && GroupAiReachRules.CounterAction(false, false, false, 0.5f, false, true) == ("wait", "hold")
                && GroupAiReachRules.CounterAction(false, false, false, 1.5f, false, true) == ("retarget", "switch")
                && GroupAiReachRules.RangedReachable(7, 5, 2, keep: true) && !GroupAiReachRules.RangedReachable(7, 5, 2) && !GroupAiReachRules.RangedReachable(9, 5, 9, keep: true)
                && GroupAiReachRules.RangedInterim(7, 5, 3) && !GroupAiReachRules.RangedInterim(7, 5, 4) && !GroupAiReachRules.RangedInterim(5, 5, 1)
                && GroupAiReachRules.KiteAllowed(1) && !GroupAiReachRules.KiteAllowed(0),
            "C24b: the shield turns at once, the ranged focus holds with a shot in range meanwhile, no kiting after the wipe");
    }

    static void CounterTauntAndVines()
    {
        // A: a counter taunt for the healer is always due, for others only after the time limit.
        Check(GroupAiReachRules.CounterDue(true, true, 2f, 1f, 8f) && !GroupAiReachRules.CounterDue(false, true, 2f, 1f, 8f)
                && GroupAiReachRules.CounterDue(false, true, 10f, 1f, 8f) && GroupAiReachRules.CounterDue(false, false, 2f, 0f, 8f),
            "C34 a: the healer's counter taunt waits for no time limit");
        // B: why no vines line, and a recent hit within the window.
        Check(GroupAiRules.VinesNoneReason(true, true) == "friend" && GroupAiRules.VinesNoneReason(false, true) == "blocked" && GroupAiRules.VinesNoneReason(false, false) == "no-hit"
                && GroupAiRules.RecentHit(5f, 3.5f, 2f) && !GroupAiRules.RecentHit(5f, 2.9f, 2f) && !GroupAiRules.RecentHit(3f, 4f, 2f),
            "C34 b: vines none reasons and recent hits");
    }

    static void BandageAtLethalWound()
    {
        // C: a bandage at a lethal wound, or with no other heal free while bleeding and the pool low (the healer's limit higher);
        // wait for a free foe next to the unit.
        Check(GroupAiBandageRules.Trigger("heavy", false, true, 0.9f, false) == "heavy"
                && GroupAiBandageRules.Trigger(null, true, false, 0.45f, false) == "cd" && GroupAiBandageRules.Trigger(null, true, false, 0.55f, false) == null
                && GroupAiBandageRules.Trigger(null, true, false, 0.55f, true) == "cd" && GroupAiBandageRules.Trigger(null, true, true, 0.2f, false) == null
                && GroupAiBandageRules.Trigger(null, false, false, 0.2f, false) == null
                && GroupAiBandageRules.Safety(2, false, 0.4f) == "use" && GroupAiBandageRules.Safety(1, false, 0.4f) == "wait:nearest"
                && GroupAiBandageRules.Safety(1, true, 0.4f) == "use" && GroupAiBandageRules.Safety(1, false, 0.2f) == "use"
                && GroupAiBandageRules.Line("f", "salve", "lethal", 0.42f, "lethal", true, 1, false, "use", "extra1", "q:-,e1:salve,e2:-", false,
                        new GroupAiBandageRules.BagCounts(2, 1, 0, 3, 0), 9, false)
                    == "CORE_AI_BANDAGE unit=f item=salve trigger=lethal pool=0.42 stage=lethal bleeding=1 nearest=1 taunted=0 result=use from=extra1 slots=q:-,e1:salve,e2:- bagopt=0 salves=2 herbals=1 heavies=0 lights=3 tick=9"
                && GroupAiBandageRules.Line("f", null, "cd", 0.4f, "light", true, int.MaxValue, false, "none:noslot", null, null, false,
                        new GroupAiBandageRules.BagCounts(0, 2, 1, 0, 0), 3, false)
                    == "CORE_AI_BANDAGE unit=f item=- trigger=cd pool=0.40 stage=light bleeding=1 nearest=-1 taunted=0 result=none:noslot from=- slots=- bagopt=0 salves=0 herbals=2 heavies=1 lights=0 tick=3"
                && GroupAiBandageRules.LostSeconds == 4f && GroupAiBandageRules.LostReason(true, true, true) == "dead"
                && GroupAiBandageRules.LostReason(false, true, true) == "down" && GroupAiBandageRules.LostReason(false, false, true) == "stun"
                && GroupAiBandageRules.LostReason(false, false, false) == "unknown"
                && GroupAiBandageRules.LostLine("k", "salve", 4.02f, "unknown", "AttackBehaviour", 41340)
                    == "CORE_AI_BANDAGE_LOST unit=k item=salve waited=4.0 reason=unknown behaviour=AttackBehaviour tick=41340",
            "C34 c: the party puts a bandage on when nothing else heals or the wound is lethal");
    }
}
