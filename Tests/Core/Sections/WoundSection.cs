using DungeonSettlersDelvers.Core;
using static TestKit;

// The wound: bleeding buffer, lethal wound, rolls, texts.
internal static class WoundSection
{
    internal static void Run()
    {
        BleedingBuffer();
        LethalWound();
        LethalFactor();
        LethalRecovery();
        HitAndLying();
        WoundTexts();
    }

    static void HitAndLying()
    {
        // The stacks a hit adds are read from the game's count before and after its apply; at the game's cap (99) the count no
        // longer rises and the hit's own stacks count as they came.
        Check(WoundRules.BleedingAdded(0, 3, 3) == 3 && WoundRules.BleedingAdded(5, 7, 2) == 2 && WoundRules.BleedingAdded(98, 99, 3) == 1
                && WoundRules.BleedingAdded(99, 99, 3) == 3 && WoundRules.BleedingAdded(0, 0, 3) == 0 && WoundRules.BleedingAdded(6, 4, 2) == -2,
            "wound hit: the added stacks are the rise of the game's count, the hit's own stacks when the count sits at its cap");
        // Every unconscious unit lies, and one with AFFECTER_Deadly.
        Check(WoundRules.IsLying(true, false) && WoundRules.IsLying(false, true) && WoundRules.IsLying(true, true) && !WoundRules.IsLying(false, false),
            "wound: a unit lies when it is deadly or unconscious");
    }

    static void BleedingBuffer()
    {
        // The bleeding wound of combat Apocalypse (short and hard; set C): 10 HP of
        // buffer per bleeding stack, deep from 10 % and heavy from 25 % of the unit's whole health, drained in game time.
        {
            const double b = WoundRules.BufferPerStack;
            Check(b == 10 && WoundRules.Stacks(0) == 0 && WoundRules.Stacks(b / 2) == 1 && WoundRules.Stacks(b) == 1 && WoundRules.Stacks(b + 0.1) == 2
                    && WoundRules.StageOf(69, 695) == WoundRules.Stage.Light && WoundRules.StageOf(69.5, 695) == WoundRules.Stage.Deep
                    && WoundRules.StageOf(173, 695) == WoundRules.Stage.Deep && WoundRules.StageOf(173.75, 695) == WoundRules.Stage.Heavy
                    && WoundRules.StageOf(76, 769) == WoundRules.Stage.Light && WoundRules.StageOf(192.25, 769) == WoundRules.Stage.Heavy
                    && WoundRules.StageOf(0, 695) == WoundRules.Stage.None && WoundRules.StageOf(-3, 695) == WoundRules.Stage.None
                    && WoundRules.Hit(0, 5) == 50 && WoundRules.Hit(33, 3) == 63,
                "wound: every bleeding stack adds 10 HP, deep from 10 % and heavy from 25 % of the unit's whole health");
            Check(WoundRules.MaxLifeOr(0) == WoundRules.DefaultMaxLife && WoundRules.MaxLifeOr(double.NaN) == WoundRules.DefaultMaxLife
                    && WoundRules.MaxLifeOr(1000) == 1000 && WoundRules.StageOf(70, 0) == WoundRules.Stage.Deep && WoundRules.StageOf(69, 0) == WoundRules.Stage.Light,
                "wound: a unit Core has not seen yet counts with 700 health until the first tick");
            // A save from before set C keeps its buffer in health: 120 HP (20 stacks at 6) read anew for a 695 unit is deep.
            Check(WoundRules.StageOf(120, 695) == WoundRules.Stage.Deep && WoundRules.Stacks(120) == 12,
                "wound: an old buffer loads unchanged and only its stage is read anew");
        }
        {
            double c1 = 0, c2 = 0, c3 = 0, c4 = 0, c5 = 0;
            var light = WoundRules.Drain(WoundRules.Stage.Light, false, 60, ref c1);
            var deep = WoundRules.Drain(WoundRules.Stage.Deep, false, 60, ref c2);
            var lethal = WoundRules.Drain(WoundRules.Stage.Heavy, false, 60, ref c3);
            var bed = WoundRules.Drain(WoundRules.Stage.Deep, true, 60, ref c4);
            var first = WoundRules.Drain(WoundRules.Stage.Light, false, 2, ref c5);
            var second = WoundRules.Drain(WoundRules.Stage.Light, false, 2, ref c5);
            Check(light == 20 && deep == 60 && lethal == 120 && bed == 30 && first == 0 && second == 1 && Math.Abs(c5 - 1.0 / 3) < 1e-9,
                "wound: 20, 60 or 120 body health a minute of game time, half as fast in bed, the rest carries over");
        }
        Check(!WoundRules.DrainsHead(12.5) && WoundRules.DrainsHead(0) && WoundRules.DrainsHead(-1) && WoundRules.DrainsHead(double.NaN),
            "wound: the body bleeds first, an empty body bleeds on at the head (only the head brings near-death and death)");
        Check(WoundRules.BlocksHeadRegeneration(WoundRules.Stage.Heavy, 0, 107.2, false) && WoundRules.BlocksHeadRegeneration(WoundRules.Stage.Light, 0, 4, false)
                && !WoundRules.BlocksHeadRegeneration(WoundRules.Stage.Heavy, 0, 0, false)
                && !WoundRules.BlocksHeadRegeneration(WoundRules.Stage.Deep, 0, 50, true)
                && !WoundRules.BlocksHeadRegeneration(WoundRules.Stage.Deep, 12.5, 50, false)
                && !WoundRules.BlocksHeadRegeneration(WoundRules.Stage.None, 0, 50, false),
            "wound: with an empty body the head stops regenerating; the reset at head 0, the portal, a full body and a closed wound pass");
        Check(WoundRules.RollOf(0.0, false) == WoundRules.Roll.Jump && WoundRules.RollOf(0.0299, false) == WoundRules.Roll.Jump
                && WoundRules.RollOf(0.03, false) == WoundRules.Roll.Extra && WoundRules.RollOf(0.1499, false) == WoundRules.Roll.Extra
                && WoundRules.RollOf(0.15, false) == WoundRules.Roll.None && WoundRules.RollOf(0.999, false) == WoundRules.Roll.None,
            "wound roll: 3 % jump, 12 % deep cut, 85 % nothing");
        Check(WoundRules.RollOf(0.0599, true) == WoundRules.Roll.Jump && WoundRules.RollOf(0.06, true) == WoundRules.Roll.Extra
                && WoundRules.RollOf(0.2999, true) == WoundRules.Roll.Extra && WoundRules.RollOf(0.30, true) == WoundRules.Roll.None
                && (WoundRules.JumpChance + WoundRules.ExtraChance) * WoundRules.EliteChanceFactor < 1,
            "wound roll: bosses and elites roll with twice the chances (6 % jump, 24 % deep cut)");
        Check(WoundRules.RollOf(WoundRules.JumpChance * WoundRules.EliteChanceFactor + 0.0001, false) == WoundRules.Roll.Extra
                && WoundRules.RollOf(WoundRules.JumpChance * WoundRules.EliteChanceFactor + 0.0001, true) == WoundRules.Roll.Extra
                && WoundRules.RollOf(0.99, true) == WoundRules.Roll.None,
            "wound roll: the test values for extra and none hold for normal and elite attackers");
        Check(WoundRules.Limit(WoundRules.Roll.Jump, 10) == WoundRules.Roll.Extra && WoundRules.Limit(WoundRules.Roll.Jump, 30) == WoundRules.Roll.Jump
                && WoundRules.Limit(WoundRules.Roll.Jump, double.PositiveInfinity) == WoundRules.Roll.Jump
                && WoundRules.Limit(WoundRules.Roll.Extra, 0) == WoundRules.Roll.Extra,
            "wound roll: one jump per unit in 30 s, a jump inside that time counts as a deep cut");
        Check(Math.Abs(WoundRules.AfterRoll(30, WoundRules.Roll.Jump, 700) - 70) < 1e-9
                && Math.Abs(WoundRules.AfterRoll(100, WoundRules.Roll.Jump, 700) - 175) < 1e-9
                && WoundRules.StageOf(WoundRules.AfterRoll(100, WoundRules.Roll.Jump, 700), 700) == WoundRules.Stage.Heavy
                && WoundRules.AfterRoll(200, WoundRules.Roll.Jump, 700) == 230
                && WoundRules.AfterRoll(20, WoundRules.Roll.Extra, 700) == 50
                && WoundRules.AfterRoll(60, WoundRules.Roll.None, 700) == 60,
            "wound roll: a jump lifts light to the deep threshold (10 %) and deep to the heavy one (25 %), adds 3 stacks at heavy; a deep cut adds 3");
    }

    static void LethalWound()
    {
        // The lethal wound: the buffer covers the health left, by default only from heavy on; no way back,
        // 2 a second, heals at half, the game's bleeding stacks go into the buffer at 10 each, herbal and salve take a quarter
        // and a third of it.
        Check(WoundRules.BecomesFatal(200, 700, 180, true) && !WoundRules.BecomesFatal(150, 700, 100, true)
                && WoundRules.BecomesFatal(150, 700, 100, false) && WoundRules.BecomesFatal(60, 700, 60, false)
                && !WoundRules.BecomesFatal(200, 700, 201, true) && !WoundRules.BecomesFatal(0, 700, 0, false)
                && !WoundRules.BecomesFatal(200, 700, double.NaN, true) && WoundRules.BecomesFatal(200, 700, -5, true),
            "lethal wound: the buffer covers the health left, by default only from the heavy threshold on");
        Check(WoundRules.StageOf(50, 700, true) == WoundRules.Stage.Fatal && WoundRules.StageOf(0, 700, true) == WoundRules.Stage.None
                && WoundRules.StageOf(50, 700, false) == WoundRules.Stage.Light && WoundRules.Describe(WoundRules.Stage.Fatal) == "lethal"
                && WoundRules.SecondsPerPoint(WoundRules.Stage.Fatal, false) == 0.5 && WoundRules.HealFactor(WoundRules.Stage.Fatal) == 0.5
                && WoundRules.SevereName(WoundRules.Stage.Fatal) == "lethal" && WoundRules.SevereName(WoundRules.Stage.Heavy) == "heavy"
                && WoundRules.SevereName(WoundRules.Stage.Deep) == null
                && WoundRules.LeavesTrail(WoundRules.Stage.Fatal, false, 0) && WoundRules.BodyMarks(WoundRules.Stage.Fatal) == 3
                && WoundRules.FromHeavyOnlyKey == "wounds.fatal.fromheavyonly",
            "lethal wound: stays lethal while open even at a small buffer, bleeds 2 a second, heals at half, leaves the trail");
        Check(WoundRules.EffectStacks(WoundRules.Stage.Fatal, 42) == 1 && WoundRules.EffectStacks(WoundRules.Stage.Heavy, 18) == 18
                && WoundRules.EffectStacks(WoundRules.Stage.Light, 3) == 3 && WoundRules.EffectStacks(WoundRules.Stage.Deep, -2) == 0,
            "lethal wound: its effect shows one stack, the other stages their stacks");
    }

    static void LethalFactor()
    {
        // (variant B): on a lethal wound 50 % up to the whole health, then 50 % × health / buffer, at least 25 %.
        Check(WoundRules.FatalFactor(695, 695) == 0.5 && WoundRules.FatalFactor(300, 695) == 0.5
                && Math.Abs(WoundRules.FatalFactor(1042.5, 695) - 1.0 / 3.0) < 1e-9 && WoundRules.FatalFactor(1390, 695) == 0.25
                && WoundRules.FatalFactor(3000, 695) == 0.25
                && Math.Abs(WoundRules.HealFactor(WoundRules.Stage.Fatal, 1042.5, 695) - 1.0 / 3.0) < 1e-9
                && WoundRules.HealFactor(WoundRules.Stage.Heavy, 1042.5, 695) == 0.70
                && Math.Abs(WoundRules.Heal(WoundRules.Stage.Fatal, WoundRules.HealKind.Sourced, 90, 100, 695, 1.0 / 3.0).Heal - 30) < 1e-9
                && WoundRules.HealLine("u", "body", "exec", WoundRules.HealKind.Sourced, WoundRules.Stage.Fatal, 90, 30, 595, 0, 9, 1.0 / 3.0).EndsWith(" tick=9 factor=0.33")
                && !WoundRules.HealLine("u", "body", "none", WoundRules.HealKind.Regeneration, WoundRules.Stage.Fatal, 1, 0, 5, 0, 9).Contains("factor="),
            "lethal wound: heals at 50 % up to the whole health, then falling with the buffer to at least 25 %; factor= in the heal line");
        Check(WoundRules.TakeOver(300, 12) == 420 && WoundRules.TakeOver(300, 0) == 300
                && Math.Abs(WoundRules.FatalBandage(600, false, 700, false) - 390) < 1e-9 && Math.Abs(WoundRules.FatalBandage(600, true, 700, false) - 320) < 1e-9
                && WoundRules.FatalBandage(600, true, 700, true) == 0 && WoundRules.FatalBandage(-3, false, 700, false) == 0
                && WoundRules.FatalBandageClosesKey == "wounds.fatal.bandagecloses",
            "lethal wound: the game's stacks go in at 10 each; herbal takes 30 %, salve 40 % of the whole health, closing only with the switch");
        {
            var save = CoreSaveData.Create(Guid.NewGuid());
            var unit = Guid.NewGuid();
            save.SetWoundFatal(unit, true);
            var noWound = !save.IsWoundFatal(unit);
            save.SetWoundBuffer(unit, 250);
            save.SetWoundFatal(unit, true);
            var fatal = save.IsWoundFatal(unit);
            save.SetWoundBuffer(unit, 0);
            var closed = !save.IsWoundFatal(unit);
            save.SetWoundBuffer(unit, 40);
            var reopened = !save.IsWoundFatal(unit);
            // A save with the lethal mark loads lethal; the way back ends the mark and leaves the buffer.
            save.SetWoundBuffer(unit, 230);
            save.SetWoundFatal(unit, true);
            var loaded = CoreSaveData.ParseAny(save.Serialize());
            var loadedFatal = loaded != null && loaded.IsWoundFatal(unit);
            loaded?.SetWoundFatal(unit, false);
            Check(noWound && fatal && closed && reopened && loadedFatal && loaded != null && !loaded.IsWoundFatal(unit) && loaded.WoundBuffer(unit) == 230,
                "lethal wound: kept in Core's save only with an open wound; closing the wound ends it; a loaded mark ends on the way back, the buffer stays");
        }
        Check(WoundRules.TraitBleedChance == 0.30 && WoundRules.TraitBleedStacks == 2,
            "wound roll: the enemies' bleeding trait bleeds two stacks in 30 % of hits");
        Check(WoundRules.Cells(2, -1) == 2 && WoundRules.Cells(-1, -1) == 1 && WoundRules.Cells(0, 0) == 0,
            "wound trail: cells count diagonal steps as one");
    }

    static void LethalRecovery()
    {
        // (option C): a lethal wound grows by the head the game refills when the unit goes down.
        Check(WoundRules.FatalRecovery(true, 0, false, 332) == 332 && WoundRules.FatalRecovery(true, -3, false, 335) == 335
                && WoundRules.FatalRecovery(false, 0, false, 332) == 0 && WoundRules.FatalRecovery(true, 12, false, 320) == 0
                && WoundRules.FatalRecovery(true, 0, true, 332) == 0 && WoundRules.FatalRecovery(true, 0, false, 0) == 0
                && WoundRules.FatalRecovery(true, double.NaN, false, 332) == 0,
            "lethal wound: grows by the game's refill of an empty head on going down, only when lethal already and only that heal");
    }

    static void WoundTexts()
    {
        Check(WoundText.All.All(entry => entry.Text.AllLanguages.Count == 10
                && entry.Text.AllLanguages.All(text => !string.IsNullOrWhiteSpace(text) && text.IndexOf('–') < 0 && text.IndexOf('—') < 0))
                && WoundText.LightKey == "AFFECTER_DelversCoreWoundLight" && WoundText.DeepKey == "AFFECTER_DelversCoreWoundDeep"
                && WoundText.HeavyKey == "AFFECTER_DelversCoreWoundLethal"
                && WoundText.LightDescription.German == "Blutet leicht nach und heilt nicht von selbst. Heilung wirkt etwas schwächer."
                && WoundText.DeepDescription.English == "Bleeds heavily and does not heal on its own. Healing is weaker."
                && WoundText.HeavyDescription.German == "Blutet heftig und heilt nicht von selbst. Heilung wirkt deutlich schwächer."
                && new[] { WoundText.LightDescription, WoundText.DeepDescription, WoundText.HeavyDescription }
                    .All(text => text.AllLanguages.All(line => !line.Any(char.IsDigit)))
                && string.Format(WoundText.StatusLine.German, "Tiefe Wunde", 150) == "Tiefe Wunde: blutet ohne Behandlung noch 150 Leben aus."
                && string.Format(WoundText.StatusLine.English, "Deep Wound", 150) == "Deep Wound: bleeds out 150 more health without treatment."
                && WoundText.All.All(entry => entry.Text.AllLanguages.All(text => text.IndexOf('-') < 0)),
            "wound: words in all ten languages without long dashes, keys under Core's prefix with one underscore");
    }
}
