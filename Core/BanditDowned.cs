using UnityEngine;
#if BEPINEX
using CombatUtility = global::Refactor.Combat.CombatUtility;
using ExecutionArea = global::Refactor.Combat.ExecutionArea;
using AffectTarget = global::Refactor.Combat.AffectTarget;
using TargetType = global::Refactor.Combat.TargetType;
#else
using CombatUtility = Il2CppRefactor.Combat.CombatUtility;
using ExecutionArea = Il2CppRefactor.Combat.ExecutionArea;
using AffectTarget = Il2CppRefactor.Combat.AffectTarget;
using TargetType = Il2CppRefactor.Combat.TargetType;
#endif

namespace DungeonSettlersDelvers.Core;

// GroupAiBandits, fallen player units and units excluded for tests: skipped while a capable one stands near,
// finished only once none is (SKIP_DOWNED, FINISH), and turned away from between decisions.
internal static partial class GroupAiBandits
{
    // Fallen units: settings of the campaign, the last fallen unit skipped per bandit,
    // and every bandit|target logged as finished (once each).
    private static bool finishDownedAlways;
    // The cone sparing is off by default (bandits.sparedownedincone).
    private static bool spareDownedInCone;
    private static bool areaIgnoresDowned = true;
    private static int chaseCells = GroupAiRules.ChaseCellsDefault;
    private static readonly Dictionary<string, string> skipLogged = new(StringComparer.Ordinal);
    private static readonly HashSet<string> finishLogged = new(StringComparer.Ordinal);
    // bandit|fallen target turned away from between decisions (SkipFallenTarget), once each.
    private static readonly HashSet<string> tickSkipped = new(StringComparer.Ordinal);

    // A player unit lying unconscious: near death or severe exhaustion (the game's category Unconscious).
    internal static bool IsDowned(UnitEntity unit)
        => unit != null && CoreGame.Affecters(unit)?.HasCategory(AffecterCategory.Unconscious) == true;

    // Fallen player units leave the hostiles while a capable one stands within chaseCells; with none capable left or
    // near they stay and may be finished.
    // Cells from a position to this bandit and to every living comrade in combat.
    // (a sleeping second camp nearby made a fallen unit skipped instead of finished): only
    // comrades that fight count (GroupAiRules.BandDistances); a camp that has not woken is no part of this fight.
    private static List<int> BandDistances(Situation s, Vector2Int at)
    {
        var allies = new List<(int Distance, bool InCombat)>();
        foreach (var ally in s.Allies)
            allies.Add((GroupAiSituation.Distance(ally.Position, at),
                CoreGame.Component<CombatInfoComponent>(ally.Unit.Cast<IEntity>())?.GetCombatState() == CombatState.InCombat));
        return GroupAiRules.BandDistances(GroupAiSituation.Distance(s.Self.Position, at), allies);
    }

    private static void SkipDowned(Situation s)
    {
        List<Figure> downed = null;
        var capable = 0;
        var near = 0;
        foreach (var hostile in s.Hostiles)
        {
            if (!hostile.Player) continue;
            if (IsDowned(hostile.Unit))
            {
                (downed ??= new List<Figure>()).Add(hostile);
                continue;
            }
            capable++;
            // (bound aggro, the band fights as one): "fled" counts from every living bandit,
            // not only from this one standing at the back (FINISH reason=fled while her comrades fought).
            if (GroupAiRules.GroupCapableNear(BandDistances(s, hostile.Position), chaseCells)) near++;
        }
        s.Downed = downed;
        if (downed == null) return;
        s.FinishReason = finishDownedAlways ? "always" : GroupAiRules.FinishReason(capable, near);
        if (s.FinishReason == null) s.Hostiles.RemoveAll(downed.Contains);
    }

    // A combat target on a fallen unit: to the focus or the closest capable one (SKIP_DOWNED), or, when
    // fallen units may be finished, logged once (FINISH). A combat target excluded for tests (the Toolkit's
    // dummy) is no target at all: to the focus or the closest one (GroupAiRules.CombatTargetAction).
    private static void RetargetFromDowned(Situation s)
    {
        var current = s.CombatTarget?.Guid.ToString();
        var fallen = current != null && s.Downed != null ? s.Downed.Find(d => d.Guid == current) : null;
        var action = GroupAiRules.CombatTargetAction(current != null && GroupAiExcluded.Has(current), fallen != null, s.FinishReason != null);
        if (action == "keep")
        {
            skipLogged.Remove(s.Guid);
            return;
        }
        if (action == "finish")
        {
            NoteFinish(s, current);
            return;
        }
        var to = s.Focus ?? s.ClosestHostile?.Unit.Cast<IEntity>();
        if (to == null)
        {
            if (action == "skip-excluded") s.CombatTarget = null;
            return;
        }
        var toGuid = to.Guid.ToString();
        RetargetTo(s, to, toGuid, action);
        if (action == "skip-excluded") return;
        if (!skipLogged.TryGetValue(s.Guid, out var skipped) || skipped != current)
        {
            skipLogged[s.Guid] = current;
            Log(GroupAiRules.SkipDownedLine(s.Guid, current, toGuid, Tick()));
        }
    }

    // Hostiles= counts the capable ones; fallen units stay among the hostiles only while
    // they may be finished and show apart as downed=.
    private static int DownedAmongHostiles(Situation s)
    {
        if (s.Downed == null) return 0;
        var count = 0;
        foreach (var hostile in s.Hostiles)
            if (s.Downed.Contains(hostile)) count++;
        return count;
    }

    // (135916: a club attack decided on a standing unit hit her 160 ticks after she fell): every tick,
    // a bandit whose combat target has fallen while a capable player unit stands within chase reach drops its running
    // action and turns to the nearest capable one (GroupAiRules.SkipsFallenNow); once per bandit and fallen target.
    // It turns to the bandit's focus when that one is capable, else to the nearest (GroupAiRules.SkipTargetPick), so
    // the game's combat target, SKIP_DOWNED to= and the basic attack agree.
    // (the blade hit the fallen Korgas next to it 9×, its rules aimed elsewhere): the game keeps a combat
    // target in reach of a basic attack over the pattern's selector (DecideNonPlayerCombatBehaviour, SpecificUsableTarget), so once the
    // fallen unit was the combat target again nothing turned it away. Now on every tick it is fallen (as did for the
    // test dummy), and at each decision (SkipFallenOnDecide); the line is written once per bandit and fallen target.
    internal static void SkipFallenOnDecide(UnitEntity unit)
    {
        var info = CoreGame.Component<CombatInfoComponent>(unit.Cast<IEntity>());
        if (info == null) return;
        var guid = unit.Guid.ToString();
        SkipFallenTarget(guid, unit, info, watched.TryGetValue(guid, out var w) ? w.FocusGuid : null, Tick());
    }

    // (archer and guard hit the fallen Pamela 3 to 4 s after SKIP_DOWNED, no decision in between): the game
    // runs the attack task it made from the last decision (ActCombatBehaviourTask with its own TargetEntity). The decision node
    // itself does not look at a running task (BrainTask.Set replaces it); in 8 measured calls the tree reached it only without
    // one (when the tree reaches it lies in its asset, not read). So the net also looks
    // at the running behaviour's target; when that one has fallen, the task is aborted the game's way (TaskAbortData, as the
    // silence does), so the next decision goes through Core. Logged with source=behaviour.
    private static readonly Dictionary<string, IntPtr> abortedBehaviour = new(StringComparer.Ordinal);

    private static void SkipFallenTarget(string guid, UnitEntity unit, CombatInfoComponent info, string focusGuid, long tick)
    {
        var combat = info.GetCombatTarget()?.TryCast<UnitEntity>();
        var behaviour = info.GetCombatBehaviour();
        var acting = behaviour?.TargetEntity?.TryCast<UnitEntity>();
        var combatFallen = combat != null && combat.Faction == FactionType.Player && IsDowned(combat);
        var actingFallen = acting != null && acting.Faction == FactionType.Player && IsDowned(acting);
        if (!combatFallen && !actingFallen) return;
        // Only the running behaviour points at a fallen unit, and it was aborted already: nothing more to do.
        if (!combatFallen && abortedBehaviour.TryGetValue(guid, out var aborted) && aborted == behaviour.Pointer) return;
        var target = combatFallen ? combat : acting;
        var targetGuid = target.Guid.ToString();
        if (!CoreGame.TryMapOfUnit(unit, out var map)) return;
        var maps = CoreGame.Flow?._inMapDatas;
        var inMap = maps != null && maps.ContainsKey(map) ? maps[map] : null;
        var hostiles = inMap?.GetAllCombatableHostiles(unit.Guid);
        if (hostiles == null) return;
        var from = inMap.GetLogicalPosition(unit.Guid);
        var near = 0;
        var capable = new List<(string Guid, UnitEntity Unit, int Distance)>();
        for (var i = 0; i < hostiles.Count; i++)
        {
            var hostile = hostiles[i];
            if (hostile == null || hostile.IsDead || hostile.Faction != FactionType.Player || IsDowned(hostile)
                || GroupAiExcluded.Has(hostile.Guid.ToString())) continue;
            var at = inMap.GetLogicalPosition(hostile.Guid);
            var distance = GroupAiRules.Distance(from.x, from.y, at.x, at.y);
            if (distance <= chaseCells) near++;
            capable.Add((hostile.Guid.ToString(), hostile, distance));
        }
        if (capable.Count == 0 || !GroupAiRules.SkipsFallenNow(true, finishDownedAlways, capable.Count, near)) return;
        capable.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        var pick = GroupAiRules.SkipTargetPick(focusGuid, capable.ConvertAll(c => c.Guid));
        var to = capable.Find(c => c.Guid == pick).Unit;
        CoreGame.TickHandler(map)?._taskSystem?._performers?.GetEntityBrain(unit.Cast<IEntity>())?.InterruptActions(false, false, true);
        // Once per behaviour: a behaviour left over after its task must not abort Core's later orders every tick.
        if (actingFallen && (!abortedBehaviour.TryGetValue(guid, out var done) || done != behaviour.Pointer))
        {
            abortedBehaviour[guid] = behaviour.Pointer;
            CoreGame.Apply(map, new TaskAbortData(unit.Cast<IEntity>()).Cast<IApplyData>());
        }
        CoreTargets.SetCombat(unit, info, to.Cast<IEntity>());
        var source = actingFallen && !combatFallen ? "behaviour" : "tick";
        if (tickSkipped.Add(guid + "|" + targetGuid + "|" + source)) Log(GroupAiRules.SkipDownedLine(guid, targetGuid, pick, tick) + " source=" + source);
    }

    // (B' #4: after the wipe the guard hit the stealthed dummy, 114 AlertDTO exceptions): every
    // tick, a bandit whose combat target is a unit excluded for tests turns to its focus or the nearest other player unit,
    // fallen ones included (they may be finished then), and drops the running action.
    // (12,313 AlertDTO exceptions): it acts on every tick the target is excluded, not once per bandit and
    // target, and without another player unit the combat target is cleared. DecideNonPlayerCombatBehaviour
    // .OnUpdate: the game asks the pattern first (CombatPattern.Decide), then keeps a combat target in range over the
    // pattern's selector for a basic attack (GetCombatTarget, SpecificUsableTarget; same line); so a dummy left as combat target came back with
    // every decision. An empty combat target ends neither the fight nor the combat state (only the combat state applier
    // does): the next decision goes through CoreUnitCombat.Decide, which gives nothing on the dummy (ExcludedPick) and the
    // bandit holds; no reset to the camp, no loop of orders. The line is logged once per bandit and target.
    private static void SkipExcludedTarget(string guid, UnitEntity unit, CombatInfoComponent info, string focusGuid, long tick)
    {
        var target = info.GetCombatTarget();
        if (target == null) return;
        var targetGuid = target.Guid.ToString();
        if (!GroupAiExcluded.Has(targetGuid) || !CoreGame.TryMapOfUnit(unit, out var map)) return;
        var maps = CoreGame.Flow?._inMapDatas;
        var inMap = maps != null && maps.ContainsKey(map) ? maps[map] : null;
        if (inMap == null) return;
        var pick = PickOtherPlayer(unit, inMap, focusGuid, out var to);
        CoreTargets.SetCombat(unit, info, to);
        CoreGame.TickHandler(map)?._taskSystem?._performers?.GetEntityBrain(unit.Cast<IEntity>())?.InterruptActions(false, false, true);
        if (tickSkipped.Add(guid + "|" + targetGuid))
            Log(GroupAiRules.ExcludedRetargetLine(guid, pick, "tick", GroupAiRules.ExcludedTargetAction(true, pick != null), tick));
    }

    // Before the game picks the closest hostile for a bandit's attack without a target of Core's: when that pick or
    // the bandit's combat target is a unit excluded for tests, the combat target is cleared and the focus or the nearest
    // other player unit comes back ("replace"), or none ("drop": the bandit holds). "keep" leaves the game's pick; with a
    // target of Core's (needsTarget false) only the excluded combat target is cleared, so the game does not keep it.
    internal static string ExcludedPick(UnitEntity unit, IInMapDataSupplier inMap, bool needsTarget, out IEntity replacement)
    {
        replacement = null;
        var self = unit.Cast<IEntity>();
        var info = CoreGame.Component<CombatInfoComponent>(self);
        var current = info?.GetCombatTarget();
        var currentExcluded = current != null && GroupAiExcluded.Has(current.Guid.ToString());
        var closest = CombatUtility.GetClosestSingleEnemy(self, inMap);
        var closestExcluded = closest != null && GroupAiExcluded.Has(closest.Guid.ToString());
        if (currentExcluded) CoreTargets.SetCombat(unit, info, null);
        if (!needsTarget || !closestExcluded) return "keep";
        var guid = unit.Guid.ToString();
        var pick = PickOtherPlayer(unit, inMap, watched.TryGetValue(guid, out var w) ? w.FocusGuid : null, out replacement);
        var action = GroupAiRules.ExcludedTargetAction(true, pick != null);
        if (tickSkipped.Add(guid + "|" + closest.Guid + "|decide"))
            Log(GroupAiRules.ExcludedRetargetLine(guid, pick, "decide", action, Tick()));
        return action;
    }

    // The bandit's focus or the nearest living player unit not excluded for tests, fallen ones included; null for none.
    private static string PickOtherPlayer(UnitEntity unit, IInMapDataSupplier inMap, string focusGuid, out IEntity to)
    {
        to = null;
        var hostiles = inMap.GetAllCombatableHostiles(unit.Guid);
        if (hostiles == null) return null;
        var from = inMap.GetLogicalPosition(unit.Guid);
        var others = new List<(string Guid, UnitEntity Unit, int Distance)>();
        for (var i = 0; i < hostiles.Count; i++)
        {
            var hostile = hostiles[i];
            if (hostile == null || hostile.IsDead || hostile.Faction != FactionType.Player || GroupAiExcluded.Has(hostile.Guid.ToString())) continue;
            var at = inMap.GetLogicalPosition(hostile.Guid);
            others.Add((hostile.Guid.ToString(), hostile, GroupAiRules.Distance(from.x, from.y, at.x, at.y)));
        }
        others.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        var pick = GroupAiRules.SkipTargetPick(focusGuid, others.ConvertAll(c => c.Guid));
        if (pick != null) to = others.Find(c => c.Guid == pick).Unit.Cast<IEntity>();
        return pick;
    }

    // CORE_AI_BANDIT_FINISH once per bandit and fallen target.
    private static void NoteFinish(Situation s, string target)
    {
        if (finishLogged.Add(s.Guid + "|" + target)) Log(GroupAiRules.FinishLine(s.Guid, target, s.FinishReason, Tick()));
    }

    // Core's own choice of a fallen unit is a finish too, also when the game's
    // combat target is none.
    private static void NoteFinishChoice(Situation s, Choice choice)
    {
        if (s.FinishReason == null || s.Downed == null || choice?.Target == null) return;
        var target = choice.Target.Guid.ToString();
        if (s.Downed.Exists(d => d.Guid == target)) NoteFinish(s, target);
    }

    // The skill's own area at the cell hits a fallen unit while fallen units are skipped and the
    // basic attack is left (GroupAiRules.AvoidsDownedArea); logged as CORE_AI_BANDIT_AREA_DOWNED.
    private static bool HitsDowned(Situation s, Slot slot, Vector2Int at)
    {
        if (s.Downed == null || s.Downed.Count == 0 || s.FinishReason != null) return false;
        var downed = 0;
        try
        {
            var targets = slot.Skill?.GetAffectTargets(s.InMap, s.Unit.Cast<IEntity>(), at);
            for (var i = 0; targets != null && i < targets.Count; i++)
            {
                var guid = targets[i]?.Guid.ToString();
                if (guid != null && s.Downed.Exists(d => d.Guid == guid)) downed++;
            }
        }
        catch (Exception ex)
        {
            Warn("CORE_AI_BANDIT_AREA_FAILED slot=" + slot.Key + ": " + ex.Message);
        }
        if (!GroupAiRules.AvoidsDownedArea(downed, s.FinishReason != null, s.BasicAttack.Length > 0)) return false;
        Log(GroupAiRules.AreaDownedLine(s.Guid, slot.Key, downed, Tick()));
        return true;
    }

    // (BanditAreaRules): with bandits.areaignoresdowned a fallen unit in the skill's own area no longer
    // rules it out; the skill loses only when the best other choice of the kit's order is worth more. True: leave it.
    private static readonly Dictionary<string, string> areaValueLogged = new(StringComparer.Ordinal);

    private static bool AreaLoses(Situation s, Slot slot, Vector2Int at, string[] order)
    {
        var (standing, downed) = AreaCount(s, slot, at);
        if (downed == 0) return false;
        var value = BanditAreaRules.AreaValue(slot.Key, standing);
        string alt = null;
        var altValue = 0.0;
        foreach (var key in order)
        {
            if (key == slot.Key) continue;
            var other = s.Find(key);
            if (other == null || !other.Free || other.Data == null || other.Data.TargetType == TargetType.SelfTarget) continue;
            if (other.Data.AffectTarget is AffectTarget.SingleAlly or AffectTarget.MultipleAlly) continue;
            if (!GroupAiRules.InRange(GroupAiSituation.Distance(s.Self.Position, at), other.Data.Range)) continue;
            var single = BanditAreaRules.SingleValue(other.Key);
            if (alt == null || single > altValue)
            {
                alt = other.Key;
                altValue = single;
            }
        }
        if (alt == null)
        {
            alt = s.BasicAttack;
            altValue = BanditAreaRules.SingleValue(s.BasicAttack);
        }
        var pick = BanditAreaRules.Pick(value, altValue);
        var logKey = slot.Key + "|" + pick + "|" + standing + "|" + downed;
        if (!areaValueLogged.TryGetValue(s.Guid, out var last) || last != logKey)
        {
            if (areaValueLogged.Count >= 128) areaValueLogged.Clear();
            areaValueLogged[s.Guid] = logKey;
            Log(BanditAreaRules.ValueLine(s.Guid, GroupAiRules.Short(slot.Key), standing, downed, value, GroupAiRules.Short(alt), altValue, pick, Tick()));
        }
        return pick == "alt";
    }

    // Standing foes and fallen units in the skill's own area at the cell (the game's GetAffectTargets).
    private static (int Standing, int Downed) AreaCount(Situation s, Slot slot, Vector2Int at)
    {
        int standing = 0, downed = 0;
        try
        {
            var targets = slot.Skill?.GetAffectTargets(s.InMap, s.Unit.Cast<IEntity>(), at);
            for (var i = 0; targets != null && i < targets.Count; i++)
            {
                var guid = targets[i]?.Guid.ToString();
                if (guid == null) continue;
                if (s.Downed != null && s.Downed.Exists(d => d.Guid == guid)) downed++;
                else if (s.Hostiles.Exists(h => h.Guid == guid && h.Player)) standing++;
            }
        }
        catch (Exception ex)
        {
            Warn("CORE_AI_BANDIT_AREA_FAILED slot=" + slot.Key + ": " + ex.Message);
        }
        return (standing, downed);
    }

    // The greatsword's basic attack hits the game's cone (BanditConeRules). Before the decision is given, a basic
    // attack whose cone would take a fallen unit turns to another foe in reach with a clear cone; the game's combat target
    // follows (it keeps a target in reach over the pattern's selector). Logged as CORE_AI_BANDIT_AREA_DOWNED, once per
    // bandit, action and target.
    private static readonly HashSet<string> coneLogged = new(StringComparer.Ordinal);

    private static Choice SpareDownedInCone(Situation s, Choice choice)
    {
        if (choice == null || choice.Skill.Length > 0 || s.Downed == null || s.Downed.Count == 0) return choice;
        // (26098: taunted by Darius, the blade turned to Frieren for a clear cone): taunted,
        // the bandit stays on its taunter, the cone does not take it away (GroupAiRules.TauntAllows "attack-taunter").
        if (!BanditConeRules.ConeMayTurn(s.Taunter != null)) return choice;
        if (!BanditConeRules.ConeBasic(GroupAiRules.Short(s.BasicAttack))) return choice;
        var current = choice.Target ?? s.Intended;
        var currentGuid = current?.Guid.ToString();
        if (currentGuid == null || current.Pointer == s.Unit.Pointer) return choice;
        var reach = Math.Max(1, s.Find(s.BasicAttack)?.Data?.Range ?? 1);
        var order = new List<Figure>();
        var first = s.Hostiles.Find(h => h.Guid == currentGuid);
        if (first == null) return choice;
        order.Add(first);
        var focus = s.Focus?.Guid.ToString();
        var byFocus = focus != null && focus != currentGuid ? s.Hostiles.Find(h => h.Guid == focus && h.Distance <= reach) : null;
        if (byFocus != null) order.Add(byFocus);
        var rest = s.Hostiles.FindAll(h => h != first && h != byFocus && h.Player && h.Distance <= reach && !s.Downed.Contains(h));
        rest.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        order.AddRange(rest);
        var candidates = new List<(string Guid, bool HitsDowned)>();
        var downedHit = 0;
        foreach (var figure in order)
        {
            var hits = DownedInCone(s, figure.Position);
            if (figure == first) downedHit = hits;
            candidates.Add((figure.Guid, hits > 0));
        }
        var (action, pick) = BanditConeRules.ConePick(s.FinishReason != null, candidates);
        if (action == "keep") return choice;
        if (coneLogged.Add(s.Guid + "|" + action + "|" + pick))
            Log(BanditConeRules.ConeLine(s.Guid, GroupAiRules.Short(s.BasicAttack), action, currentGuid, pick, downedHit, Tick()));
        if (action != "switch") return choice;
        var to = order.Find(f => f.Guid == pick).Unit.Cast<IEntity>();
        RetargetTo(s, to, pick, "cone-downed");
        return NewChoice("", to, choice.Rule);
    }

    // Fallen units in the cone of a basic attack from the bandit's cell at a target cell (the game's own area).
    private static int DownedInCone(Situation s, Vector2Int at)
    {
        var downed = new List<Vector2Int>();
        foreach (var figure in s.Downed) downed.Add(figure.Position);
        return DownedInCone(s.Self.Position, at, downed, s.BasicAttack);
    }

    private static int DownedInCone(Vector2Int from, Vector2Int at, List<Vector2Int> downed, string skill)
    {
        try
        {
            var cells = ExecutionArea.ConeExceptMe.GetArea(from, at, BanditConeRules.ConeRadius);
            if (cells == null) return 0;
            var count = 0;
            foreach (var position in downed)
                for (var i = 0; i < cells.Count; i++)
                    if (cells[i] == position)
                    {
                        count++;
                        break;
                    }
            return count;
        }
        catch (Exception ex)
        {
            Warn("CORE_AI_BANDIT_AREA_FAILED slot=" + skill + ": " + ex.Message);
            return 0;
        }
    }

    // (the blade decided a basic attack on Pamela, walked three cells and struck
    // from there with the fallen Frieren in its cone, no new decision in between): every tick, a bandit with a cone basic
    // attack whose combat target stands within its reach is checked from the cell it stands on now; with a fallen unit in
    // that cone and another foe in reach with a clear cone it turns to that one and drops the running action, as
    // SkipFallenTarget does. Only while fallen units are skipped; without a clear foe nothing changes.
    // CORE_AI_BANDIT_AREA_DOWNED ... source=tick, once per bandit and pair.
    private static void ConeGuardTick(string guid, UnitEntity unit, CombatInfoComponent info, string focusGuid, long tick)
    {
        // A taunted bandit (the game's forced target) is not turned away from its taunter.
        if (finishDownedAlways || !BanditConeRules.ConeMayTurn(info.GetForceAttackTarget() != null)) return;
        var basic = CoreGame.Component<SkillComponent>(unit.Cast<IEntity>())?.GetBasicAttackSkill();
        if (!BanditConeRules.ConeBasic(GroupAiRules.Short(basic))) return;
        var target = info.GetCombatTarget()?.TryCast<UnitEntity>();
        if (target == null || target.Faction != FactionType.Player || IsDowned(target)) return;
        if (!CoreGame.TryMapOfUnit(unit, out var map)) return;
        var maps = CoreGame.Flow?._inMapDatas;
        var inMap = maps != null && maps.ContainsKey(map) ? maps[map] : null;
        var hostiles = inMap?.GetAllCombatableHostiles(unit.Guid);
        if (hostiles == null) return;
        var reach = Math.Max(1, DataSheetManager.Instance?.GetSkillData(basic)?.Range ?? 1);
        var from = inMap.GetLogicalPosition(unit.Guid);
        var targetAt = inMap.GetLogicalPosition(target.Guid);
        if (GroupAiRules.Distance(from.x, from.y, targetAt.x, targetAt.y) > reach) return;
        var downed = new List<Vector2Int>();
        var capable = new List<(string Guid, UnitEntity Unit, Vector2Int At, int Distance)>();
        var near = 0;
        for (var i = 0; i < hostiles.Count; i++)
        {
            var hostile = hostiles[i];
            if (hostile == null || hostile.IsDead || hostile.Faction != FactionType.Player || GroupAiExcluded.Has(hostile.Guid.ToString())) continue;
            var at = inMap.GetLogicalPosition(hostile.Guid);
            if (IsDowned(hostile))
            {
                downed.Add(at);
                continue;
            }
            var distance = GroupAiRules.Distance(from.x, from.y, at.x, at.y);
            if (distance <= chaseCells) near++;
            capable.Add((hostile.Guid.ToString(), hostile, at, distance));
        }
        if (downed.Count == 0 || !GroupAiRules.SkipsFallenNow(true, finishDownedAlways, capable.Count, near)) return;
        var targetGuid = target.Guid.ToString();
        var order = new List<(string Guid, UnitEntity Unit, Vector2Int At, int Distance)>();
        var first = capable.Find(c => c.Guid == targetGuid);
        if (first.Unit == null) return;
        order.Add(first);
        var byFocus = focusGuid != null && focusGuid != targetGuid ? capable.Find(c => c.Guid == focusGuid && c.Distance <= reach) : default;
        if (byFocus.Unit != null) order.Add(byFocus);
        var rest = capable.FindAll(c => c.Guid != targetGuid && c.Guid != byFocus.Guid && c.Distance <= reach);
        rest.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        order.AddRange(rest);
        var candidates = new List<(string Guid, bool HitsDowned)>();
        var downedHit = 0;
        foreach (var c in order)
        {
            var hits = DownedInCone(from, c.At, downed, basic);
            if (c.Guid == targetGuid) downedHit = hits;
            candidates.Add((c.Guid, hits > 0));
        }
        var (action, pick) = BanditConeRules.ConePick(false, candidates);
        // Without a clear foe the check still shows in the log (action=none), once per pair.
        if (action == "none" && coneLogged.Add(guid + "|tick-none|" + targetGuid))
            Log(BanditConeRules.ConeLine(guid, GroupAiRules.Short(basic), action, targetGuid, pick, downedHit, tick) + " source=tick");
        if (action != "switch") return;
        var to = order.Find(c => c.Guid == pick).Unit;
        CoreGame.TickHandler(map)?._taskSystem?._performers?.GetEntityBrain(unit.Cast<IEntity>())?.InterruptActions(false, false, true);
        CoreTargets.SetCombat(unit, info, to.Cast<IEntity>());
        if (coneLogged.Add(guid + "|tick|" + targetGuid + "|" + pick))
            Log(BanditConeRules.ConeLine(guid, GroupAiRules.Short(basic), action, targetGuid, pick, downedHit, tick) + " source=tick");
    }
}
