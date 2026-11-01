using UnityEngine;
#if BEPINEX
using ActiveSkill_Base = global::Refactor.Combat.ActiveSkill_Base;
using AffectTarget = global::Refactor.Combat.AffectTarget;
using CombatUtility = global::Refactor.Combat.CombatUtility;
using DecidePlayerCombatBehaviour = global::Refactor.Task.DecidePlayerCombatBehaviour;
using ExecutionCheckResult = global::Refactor.Combat.ExecutionCheckResult;
using IExecutionPublisher = global::Refactor.Combat.IExecutionPublisher;
using SkillExecutionContext = global::Refactor.Combat.ExecutionContext;
#else
using ActiveSkill_Base = Il2CppRefactor.Combat.ActiveSkill_Base;
using AffectTarget = Il2CppRefactor.Combat.AffectTarget;
using CombatUtility = Il2CppRefactor.Combat.CombatUtility;
using DecidePlayerCombatBehaviour = Il2CppRefactor.Task.DecidePlayerCombatBehaviour;
using ExecutionCheckResult = Il2CppRefactor.Combat.ExecutionCheckResult;
using IExecutionPublisher = Il2CppRefactor.Combat.IExecutionPublisher;
using SkillExecutionContext = Il2CppRefactor.Combat.ExecutionContext;
#endif

namespace DungeonSettlersDelvers.Core;

// What a party member sees when it decides (GroupAiPolicy): its health, energy, role and quick
// slots, the other party members and the enemies on its map with their grid distance, the boss
// (AFFECTER_BossMonster, else the enemy with the most health) and the boss's target. The quick slots
// are checked with the game's own use check (cooldown excepted), so a "free" slot can be cast now at the game's combat
// target; at another target range or line can still stop it.
internal sealed class GroupAiSituation
{
    internal sealed class Slot
    {
        internal string Key = "";
        internal SkillTableData Data;
        internal ActiveSkill_Base Skill;
        internal string State = "";
        // Seconds until the game's cooldown of the slot ends (State "cd"), else 0.
        internal float CooldownLeft;

        internal bool Free => State == "free";
    }

    internal sealed class Figure
    {
        internal UnitEntity Unit;
        internal string Guid = "";
        internal float Share;
        internal Vector2Int Position;
        internal int Distance;
    }

    internal DecidePlayerCombatBehaviour Decide;
    internal IInMapDataSupplier InMap;
    internal UnitEntity Owner;
    internal string Guid = "";
    internal GroupAiRole Role;
    internal string BasicAttack = "";
    internal readonly List<Slot> Slots = new();
    internal readonly List<Figure> Allies = new();
    internal readonly List<Figure> Enemies = new();
    internal Figure Self;
    internal IEntity CombatTarget;
    internal UnitEntity Boss;
    internal UnitEntity Leader;
    // An elite enemy whose target is this unit (GroupAiRules.ShieldWallReason), else null.
    internal UnitEntity EliteOnMe;
    internal IEntity BossTarget;
    internal float Energy;
    internal float MaxEnergy;
    internal bool GlobalCooldown;
    internal CombatState State;
    // The first area skill skipped for friendly fire in this decision, for the rule log.
    internal string SkippedSkill;
    internal string SkippedAlly;
    internal string SkippedFlag;

    internal Figure LowestAlly
    {
        get
        {
            Figure lowest = null;
            foreach (var ally in Allies)
                if (lowest == null || ally.Share < lowest.Share) lowest = ally;
            return lowest;
        }
    }

    internal Figure ClosestEnemy
    {
        get
        {
            Figure closest = null;
            foreach (var enemy in Enemies)
                if (closest == null || enemy.Distance < closest.Distance) closest = enemy;
            return closest;
        }
    }

    internal Slot Find(string key)
    {
        foreach (var slot in Slots)
            if (slot.Key == key) return slot;
        return null;
    }

    internal bool AnyAllyBelow(float share) => AlliesBelow(share) > 0;

    // Party members below the share, the unit itself included.
    internal int AlliesBelow(float share)
    {
        var count = Self.Share < share ? 1 : 0;
        foreach (var ally in Allies)
            if (ally.Share < share) count++;
        return count;
    }

    internal static GroupAiSituation Build(DecidePlayerCombatBehaviour decide, bool withSlots)
    {
        var owner = decide._owner;
        var inMap = decide._inMapData;
        var s = new GroupAiSituation
        {
            Decide = decide,
            InMap = inMap,
            Owner = owner,
            Guid = owner.Guid.ToString()
        };
        var skills = decide._skillReader;
        s.BasicAttack = skills.GetBasicAttackSkill() ?? "";
        s.GlobalCooldown = skills.HasGlobalCooldown();
        s.Energy = CoreGame.Stat(owner, StatType.Energy);
        s.MaxEnergy = CoreGame.Stat(owner, StatType.MaxEnergy);
        var info = CoreGame.Component<CombatInfoComponent>(owner.Cast<IEntity>());
        s.CombatTarget = info?.GetCombatTarget();
        s.State = info?.GetCombatState() ?? CombatState.Unawared;
        var position = inMap.GetLogicalPosition(owner.Guid);
        s.Self = new Figure
        {
            Unit = owner,
            Guid = s.Guid,
            Share = ShareOf(owner),
            Position = position,
            Distance = 0
        };
        var keys = new List<string>();
        var quickSlots = decide._quickSlotViewer?.GetAll(owner.Guid);
        for (var i = 0; quickSlots != null && i < quickSlots.Length; i++)
            if (!string.IsNullOrEmpty(quickSlots[i])) keys.Add(quickSlots[i]);
        s.Role = GroupAiRoles.Of(s.Guid, keys, s.BasicAttack);
        var players = inMap.GetAllPlayerUnits();
        for (var i = 0; players != null && i < players.Count; i++)
        {
            var unit = players[i];
            if (unit == null || unit.Guid.ToString() == s.Guid || GroupAiExcluded.Has(unit.Guid.ToString())) continue;
            s.Allies.Add(FigureOf(inMap, unit, position));
        }
        var alive = inMap.GetAliveUnitsOnMap();
        for (var i = 0; alive != null && i < alive.Count; i++)
        {
            var unit = alive[i];
            if (unit == null || unit.IsDead || unit.Faction != FactionType.Enemy || GroupAiExcluded.Has(unit.Guid.ToString())) continue;
            s.Enemies.Add(FigureOf(inMap, unit, position));
            // The bandits' boss party carries the boss marker for the boss HUD and the inscriptions only; for the party AI
            // they stay a group (no "adds first" around one of them), as before.
            if (s.Boss == null && !CoreUnitRules.IsCoreUnit(unit.Key) && HasAffecter(unit, "AFFECTER_BossMonster")) s.Boss = unit;
            // Only for the shield wall (GroupAiRules.ShieldWallReason); Boss and Leader stay as they are.
            if (s.EliteOnMe == null && HasAffecter(unit, "AFFECTER_EliteMonster")
                && CoreGame.Component<CombatInfoComponent>(unit.Cast<IEntity>())?.GetCombatTarget()?.Guid.ToString() == s.Guid)
                s.EliteOnMe = unit;
        }
        var leader = s.Leader = s.Boss ?? Strongest(s.Enemies);
        if (leader != null) s.BossTarget = CoreGame.Component<CombatInfoComponent>(leader.Cast<IEntity>())?.GetCombatTarget();
        if (!withSlots) return s;
        foreach (var key in keys)
        {
            var slot = new Slot
            {
                Key = key,
                Data = GroupAiRoles.Sheet(key)
            };
            slot.State = StateOf(decide, owner, slot, s.CombatTarget);
            if (slot.State == "cd")
            {
                try { slot.CooldownLeft = decide._skillReader.GetCooldown(slot.Key); }
                catch (Exception) { slot.CooldownLeft = 0f; }
            }
            s.Slots.Add(slot);
        }
        return s;
    }

    private static UnitEntity Strongest(List<Figure> enemies)
    {
        UnitEntity strongest = null;
        var most = float.MinValue;
        foreach (var enemy in enemies)
        {
            var health = CoreGame.Stat(enemy.Unit, StatType.MaxHealthTotal);
            if (float.IsNaN(health)) health = float.MinValue;
            if (strongest == null || health > most)
            {
                strongest = enemy.Unit;
                most = health;
            }
        }
        return strongest;
    }

    private static Figure FigureOf(IInMapDataSupplier inMap, UnitEntity unit, Vector2Int from)
    {
        var position = inMap.GetLogicalPosition(unit.Guid);
        return new Figure
        {
            Unit = unit,
            Guid = unit.Guid.ToString(),
            Share = ShareOf(unit),
            Position = position,
            Distance = GroupAiRules.Distance(from.x, from.y, position.x, position.y)
        };
    }

    // "free", or why the slot cannot be cast now: nodata, cd, auto (auto use off), noskill,
    // check:<the game's refusal>, err.
    private static string StateOf(DecidePlayerCombatBehaviour decide, UnitEntity owner, Slot slot, IEntity combatTarget)
    {
        try
        {
            var skills = decide._skillReader;
            if (slot.Data == null) return "nodata";
            if (skills.HasCooldown(slot.Key)) return "cd";
            if (!skills.IsSkillAutoUse(slot.Key)) return "auto";
            slot.Skill = CombatDataManager.Instance?.GetActiveSkillData(slot.Key);
            if (slot.Skill == null) return "noskill";
            var target = (slot.Data.AffectTarget is AffectTarget.SingleHostile or AffectTarget.MultipleHostile) ? combatTarget : owner.Cast<IEntity>();
            var context = SkillExecutionContext.Convert(slot.Skill.Cast<IExecutionPublisher>(), owner.Cast<IEntity>(), target, null);
            var result = slot.Skill.CheckForUseSkill(decide._inMapData, context, execeptCooldown: true);
            return result == ExecutionCheckResult.Successed ? "free" : "check:" + result.ToString().Replace("Failed_", "");
        }
        catch (Exception ex)
        {
            GroupAiPolicy.Warn("CORE_AI_SLOT_FAILED slot=" + slot.Key + ": " + ex.Message);
            return "err";
        }
    }

    // The slot as the game's own DecideSkill checks it (the use check;
    // the order, target null and the cooldown skipped; GroupAiRules.NativeStep). Before, Core checked against the closest enemy with the
    // cooldown, not the game's verdict. The range check that
    // follows in the game is not part of it. The null target goes through the entity overload of Convert; with null either
    // overload leaves the target empty. Only read, never changes the choice; slotNative next to the own slot state shows
    // why the game never casts an ally skill like Lifebloom.
    internal static string NativeStateOf(DecidePlayerCombatBehaviour decide, UnitEntity owner, string key)
    {
        try
        {
            var skills = decide._skillReader;
            var globalCooldown = skills.HasGlobalCooldown();
            var hasData = GroupAiRoles.Sheet(key) != null;
            var onCooldown = hasData && skills.HasCooldown(key);
            var autoUse = hasData && !onCooldown && skills.IsSkillAutoUse(key);
            // The game's energy step (DecidePlayerCombatBehaviour.IsEnergyCondition, too little energy when less
            // than 40 % of the maximum would stay after the skill's adjusted cost) is not mirrored yet: calling it needs the
            // behaviour tree's assembly, which Core does not reference. Open.
            const bool energy = true;
            var skill = hasData && !onCooldown && autoUse ? CombatDataManager.Instance?.GetActiveSkillData(key) : null;
            var step = GroupAiRules.NativeStep(globalCooldown, hasData, onCooldown, autoUse, energy, skill != null);
            if (step != null) return step;
            var context = SkillExecutionContext.Convert(skill.Cast<IExecutionPublisher>(), owner.Cast<IEntity>(), (IEntity)null, null);
            var result = skill.CheckForUseSkill(decide._inMapData, context, execeptCooldown: GroupAiRules.NativeSkipsCooldown);
            return result == ExecutionCheckResult.Successed ? "free" : "check:" + result.ToString().Replace("Failed_", "");
        }
        catch (Exception ex)
        {
            return "err:" + ex.GetType().Name;
        }
    }

    internal static float ShareOf(UnitEntity unit)
        => GroupAiRules.Share(CoreGame.Stat(unit, StatType.HealthHead), CoreGame.Stat(unit, StatType.HealthBody), CoreGame.Stat(unit, StatType.MaxHealthTotal));

    internal static bool HasAffecter(UnitEntity unit, string key)
    {
        try
        {
            return CoreGame.Affecters(unit)?.Has(key) ?? false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    internal Vector2Int PositionOf(IEntity entity)
    {
        try
        {
            return InMap.GetLogicalPosition(entity.Guid);
        }
        catch (Exception)
        {
            return Self.Position;
        }
    }

    internal static int Distance(Vector2Int a, Vector2Int b) => GroupAiRules.Distance(a.x, a.y, b.x, b.y);

    internal List<Figure> AlliesOn(List<Vector2Int> cells)
    {
        var found = new List<Figure>();
        if (cells == null) return found;
        var keys = new HashSet<long>();
        foreach (var cell in cells) keys.Add(GroupAiRules.CellKey(cell.x, cell.y));
        foreach (var ally in Allies)
            if (keys.Contains(GroupAiRules.CellKey(ally.Position.x, ally.Position.y))) found.Add(ally);
        return found;
    }

    // Party members the slot's skill would hit when cast at the position (the game's own target list).
    internal List<Figure> AlliesInArea(Slot slot, Vector2Int position)
    {
        var found = new List<Figure>();
        try
        {
            var targets = slot.Skill?.GetAffectTargets(InMap, Owner.Cast<IEntity>(), position);
            for (var i = 0; targets != null && i < targets.Count; i++)
            {
                var entity = targets[i];
                if (entity == null) continue;
                var guid = entity.Guid.ToString();
                foreach (var ally in Allies)
                {
                    if (ally.Guid == guid)
                    {
                        found.Add(ally);
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            GroupAiPolicy.Warn("CORE_AI_AREA_FAILED slot=" + slot.Key + ": " + ex.Message);
        }
        return found;
    }
}
