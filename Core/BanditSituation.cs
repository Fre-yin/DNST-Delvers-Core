using UnityEngine;

namespace DungeonSettlersDelvers.Core;

// GroupAiBandits, the situation read from the game: Build gathers the unit, its allies, the hostiles and the skill
// slots for one decision; the kit by the unit key, the player healer in focus (FocusOf, IsHealer) and the distance
// to the closest hostile come from here too.
internal static partial class GroupAiBandits
{
    internal const int FocusRadius = 6;
    internal const float HealerCacheSeconds = 5f;

    private static readonly Dictionary<string, Kit> kits = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, (bool Healer, float At)> healers = new(StringComparer.Ordinal);

    // The distance to the closest capable player unit, int.MaxValue without one.
    private static int ClosestHostileDistance(UnitEntity unit, IInMapDataSupplier inMap)
    {
        var hostiles = inMap.GetAllCombatableHostiles(unit.Guid);
        if (hostiles == null) return int.MaxValue;
        var from = inMap.GetLogicalPosition(unit.Guid);
        var best = int.MaxValue;
        for (var i = 0; i < hostiles.Count; i++)
        {
            var hostile = hostiles[i];
            if (hostile == null || hostile.IsDead || hostile.Faction != FactionType.Player || IsDowned(hostile)
                || GroupAiExcluded.Has(hostile.Guid.ToString())) continue;
            var at = inMap.GetLogicalPosition(hostile.Guid);
            best = Math.Min(best, GroupAiRules.Distance(from.x, from.y, at.x, at.y));
        }
        return best;
    }

    // Null without a hostile to fight. The skills come from the unit's definition (CoreUnitRules),
    // its first skill standing in for a missing basic attack.
    private static Situation Build(UnitEntity unit, IInMapDataSupplier inMap, Kit kit)
    {
        var hostiles = inMap.GetAllCombatableHostiles(unit.Guid);
        if (hostiles == null || hostiles.Count == 0) return null;
        var s = new Situation
        {
            Unit = unit,
            InMap = inMap,
            Guid = unit.Guid.ToString(),
            Kit = kit
        };
        var entity = unit.Cast<IEntity>();
        var skills = CoreGame.Component<SkillComponent>(entity);
        CoreUnitRules.TryGet(unit.Key, out var definition);
        var basic = skills?.GetBasicAttackSkill();
        if (string.IsNullOrEmpty(basic)) basic = definition != null && definition.Skills.Length != 0 ? definition.Skills[0] : "";
        s.BasicAttack = basic;
        s.GlobalCooldown = skills?.HasGlobalCooldown() ?? false;
        var info = CoreGame.Component<CombatInfoComponent>(entity);
        s.CombatTarget = info?.GetCombatTarget();
        s.State = info?.GetCombatState() ?? CombatState.Unawared;
        var position = inMap.GetLogicalPosition(unit.Guid);
        s.Self = new Figure
        {
            Unit = unit,
            Guid = s.Guid,
            Share = GroupAiSituation.ShareOf(unit),
            Position = position,
            Distance = 0
        };
        var alive = inMap.GetAliveUnitsOnMap();
        for (var i = 0; alive != null && i < alive.Count; i++)
        {
            var other = alive[i];
            if (other == null || other.IsDead || !CoreUnitRules.IsCoreUnit(other.Key) || other.Guid.ToString() == s.Guid
                || GroupAiExcluded.Has(other.Guid.ToString())) continue;
            // P4: several bandit groups on one map; only the bandits of its own group are a bandit's allies (GroupAiRules.IsGroupAlly).
            var ally = FigureOf(inMap, other, position, player: false);
            if (GroupAiRules.IsGroupAlly(ally.Distance)) s.Allies.Add(ally);
        }
        for (var i = 0; i < hostiles.Count; i++)
        {
            var hostile = hostiles[i];
            // A unit excluded for tests is no hostile, so neither a capable unit for "fallen last" nor a target.
            if (hostile == null || hostile.IsDead || GroupAiExcluded.Has(hostile.Guid.ToString())) continue;
            s.Hostiles.Add(FigureOf(inMap, hostile, position, hostile.Faction == FactionType.Player));
        }
        SkipDowned(s);
        if (s.Hostiles.Count == 0) return null;
        s.Focus = FocusOf(s);
        RetargetFromDowned(s);
        s.Guard = AllyOfKit(s, Kit.Guard);
        s.Healer = kit == Kit.Healer ? s.Self : AllyOfKit(s, Kit.Healer);
        // A taunted bandit turns on its taunter (Focus, so basic attacks and skills go
        // there), but breaks free early; the others at once when the healer falls hard.
        var gameNow = GameNow();
        if (s.Healer != null) NoteHealerShare(s.Healer.Guid, s.Healer.Share, gameNow);
        watched[s.Guid] = new Watch { Unit = unit, Kit = kit, Healer = s.Healer?.Unit, HealerGuid = s.Healer?.Guid };
        s.Taunter = TaunterOf(s, unit, kit, info, gameNow);
        if (s.Taunter != null) s.Focus = s.Taunter.Unit.Cast<IEntity>();
        watched[s.Guid].FocusGuid = s.Focus?.Guid.ToString();
        // (b): chain and counter-attack go for the most damage on the healer in the
        // last ProtectWindowSeconds; without hits the attacker targeting her. The chain pulls that same protected attacker.
        var dealt = 0f;
        var hitters = 0;
        var byDamage = s.Healer != null ? TopHealerAttacker(s, s.Healer, out dealt, out hitters) : null;
        s.AttackerByDamage = byDamage != null;
        // One protected target for all bandits, held; the chain pulls only it.
        s.HealerAttacker = s.Healer != null ? ProtectedTarget(s, s.Healer, gameNow) : null;
        if (s.Healer != null && s.HealerAttacker != null
            && (!protectTarget.TryGetValue(s.Healer.Guid, out var logged) || logged != s.HealerAttacker.Guid))
        {
            protectTarget[s.Healer.Guid] = s.HealerAttacker.Guid;
            Log(GroupAiRules.ProtectLine(s.Healer.Guid, s.HealerAttacker.Guid, byDamage != null ? "dmg3s" : "targets",
                byDamage != null ? dealt : 0f, byDamage != null ? hitters : 0, GroupAiSituation.Distance(s.HealerAttacker.Position, s.Healer.Position), Tick()));
        }
        if (definition != null)
        {
            // The skills of the unit's row (CoreUnitRules.SkillsFor): with Lifebloom on, the healer's
            // row carries it, and its rules need the slot (none fired).
            foreach (var key in CoreUnitRules.SkillsFor(definition, Lifebloom.Active))
            {
                if (key == s.BasicAttack) continue;
                var slot = new Slot
                {
                    Key = key,
                    Data = GroupAiRoles.Sheet(key)
                };
                // (CORE_AI_BANDIT_SLOT_FAILED slot=SKILL_LifeBurst): a passive skill (the herbalist's
                // Life Burst) is no slot to decide on; the game runs it.
                if (slot.Data != null && slot.Data.SkillType == SkillType.Passive) continue;
                slot.State = StateOf(s, skills, slot);
                s.Slots.Add(slot);
            }
        }
        return s;
    }

    private static Figure FigureOf(IInMapDataSupplier inMap, UnitEntity unit, Vector2Int from, bool player)
    {
        var position = inMap.GetLogicalPosition(unit.Guid);
        return new Figure
        {
            Unit = unit,
            Guid = unit.Guid.ToString(),
            Share = GroupAiSituation.ShareOf(unit),
            Position = position,
            Distance = GroupAiRules.Distance(from.x, from.y, position.x, position.y),
            Player = player
        };
    }

    // The nearest player healer within FocusRadius.
    private static IEntity FocusOf(Situation s)
    {
        Figure focus = null;
        foreach (var hostile in s.Hostiles)
        {
            if (hostile.Player && hostile.Distance <= FocusRadius && IsHealer(hostile) && (focus == null || hostile.Distance < focus.Distance))
                focus = hostile;
        }
        return focus?.Unit.Cast<IEntity>();
    }

    // A healer by its basic attack or a heal skill (GroupAiRules.HealSkills), cached for HealerCacheSeconds.
    private static bool IsHealer(Figure figure)
    {
        var now = GameClock.Now;
        if (healers.TryGetValue(figure.Guid, out var cached) && !GameClockRules.Elapsed(now, cached.At, HealerCacheSeconds)) return cached.Healer;
        var healer = false;
        try
        {
            var skills = CoreGame.Component<SkillComponent>(figure.Unit.Cast<IEntity>());
            if (skills != null)
            {
                healer = GroupAiRules.HealSkills.Contains(skills.GetBasicAttackSkill() ?? "");
                if (!healer)
                {
                    foreach (var key in GroupAiRules.HealSkills)
                    {
                        if (skills.HasSkill(key))
                        {
                            healer = true;
                            break;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Warn("CORE_AI_BANDIT_HEALER_FAILED unit=" + figure.Guid + ": " + ex.Message);
        }
        healers[figure.Guid] = (healer, now);
        return healer;
    }

    private static Figure AllyOfKit(Situation s, Kit kit)
    {
        foreach (var ally in s.Allies)
            if (!ally.Unit.IsDead && KitOf(ally.Unit) == kit) return ally;
        return null;
    }

    // The kit by the end of the unit key; cached per unit.
    private static Kit KitOf(UnitEntity unit)
    {
        var key = unit.Key ?? "";
        if (!CoreUnitRules.IsCoreUnit(key)) return Kit.None;
        var guid = unit.Guid.ToString();
        if (kits.TryGetValue(guid, out var known)) return known;
        var kit = key.EndsWith("Blade", StringComparison.Ordinal) ? Kit.Blade
            : key.EndsWith("Guard", StringComparison.Ordinal) ? Kit.Guard
            : key.EndsWith("Archer", StringComparison.Ordinal) ? Kit.Archer
            : key.EndsWith("Healer", StringComparison.Ordinal) ? Kit.Healer
            : key.EndsWith("Spear", StringComparison.Ordinal) ? Kit.Spear
            : key.EndsWith("Sword", StringComparison.Ordinal) ? Kit.Sword
            : key.EndsWith("WaterMage", StringComparison.Ordinal) ? Kit.WaterMage
            : key.EndsWith("FireMage", StringComparison.Ordinal) ? Kit.FireMage
            : Kit.None;
        kits[guid] = kit;
        Log(GroupAiRules.BanditKitLine(guid, key, kit.ToString()));
        return kit;
    }

    private static IEntity CombatTargetOf(UnitEntity unit)
        => CoreGame.Component<CombatInfoComponent>(unit.Cast<IEntity>())?.GetCombatTarget();
}
