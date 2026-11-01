using System.Globalization;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
#if BEPINEX
using EntityAI = global::Refactor.Task.EntityAI;
using StatUtility = global::Refactor.Component.Status.StatUtility;
#else
using EntityAI = Il2CppRefactor.Task.EntityAI;
using StatUtility = Il2CppRefactor.Component.Status.StatUtility;
#endif

namespace DungeonSettlersDelvers.Core;

// The forest bandits in the game (rules: CoreUnitRules), a part of combat Apocalypse
// (ApocalypseRules.Bandits). Their rows are always registered: rows alone spawn
// nothing, and saves keep the goblin stand-in (CoreSaveUnits).
// - Before the game parses its unit table, each bandit gets a row copied from its goblin template:
//   enemy NPC, elite trait, its skills, the pattern OnlyBasicAttack, the player's sprite height and
//   the health of the settings (CoreUnitSheetPatch).
// - On spawn a bandit loads the human player prefab of a visual profile picked by its guid and is
//   dressed in its kit (CoreUnitSpawnObjectPatch, CoreUnitObjectPatch, CoreUnitFactoryPatch).
// - A dead bandit keeps its kit on the body and drops no loot (CoreUnitDropPatch, CorpseKeep); undressing drops the kit (step 2, DropKit).
internal static class CoreUnits
{
    private const string PlayerRowKey = "UNIT_PlayerUnit";
    private static bool statsReported;
    private static Dictionary<string, List<UnitProfileTableData>> profiles;
    private static UnitProfileTableData spawningProfile;
    private static CoreUnitRules.Definition spawningUnit;
    private const int DespawnLogCap = 200;
    private static int despawnLines;
    // The unit being spawned, and the bodies of bandits by GUID, so an undressed corpse can be drawn naked
    // (equipment on the body, step 2).
    private static string spawningGuid;
    private static bool spawningDead;
    private static readonly Dictionary<string, UnitObject> bodies = new(StringComparer.Ordinal);

    internal static bool Active { get; private set; }

    internal static void Register(UnitTable table)
    {
        var rows = table?.tableDatas;
        if (rows == null) return;
        var player = Find(rows, PlayerRowKey);
        // The boss party and the weak bandits (CoreUnitRules.All); a weak one's health is its share of the boss row's.
        foreach (var unit in CoreUnitRules.All)
        {
            if (Find(rows, unit.Key) != null)
            {
                Active = true;
                continue;
            }
            var template = Find(rows, unit.Template);
            if (template == null)
            {
                DelversHost.Warning("CORE_UNIT_TEMPLATE_MISSING key=" + unit.Key + " template=" + unit.Template);
                continue;
            }
            // bandits.health.factor scales the row health (BanditArmorRules), 2 by default (stage B).
        var healthFactor = BanditArmorRules.HealthFactor(CoreSettings.Number(BanditArmorRules.HealthFactorKey));
        var health = BanditArmorRules.ScaledHealth(CoreUnitRules.HealthOf(template.MaxHealthTotal, CoreSettings.Number(CoreUnitRules.HealthKey(unit))), healthFactor * unit.HealthFactor);
            rows.Add(Copy(template, player, unit, health));
            Active = true;
            DelversHost.Info($"CORE_UNIT_REGISTERED key={unit.Key} template={unit.Template} faction={CoreUnitRules.FactionEnemy}/{CoreUnitRules.SubFactionNpc} pattern={CoreUnitRules.Pattern} standIn={CoreUnitRules.StandIn} skills={CoreUnitRules.SkillsFor(unit, true).Length} health={health.ToString("0", CultureInfo.InvariantCulture)}{(health == template.MaxHealthTotal ? "/template" : "/setting")} healthFactor={healthFactor.ToString("0.##", CultureInfo.InvariantCulture)} phys={rows[rows.Count - 1].PhysicalResistance.ToString("0.#", CultureInfo.InvariantCulture)} magic={rows[rows.Count - 1].MagicResistance.ToString("0.#", CultureInfo.InvariantCulture)} matk={CoreUnitRules.MagicAttackOf(unit, template.MagicAttackPower, CoreSettings.Number(CoreUnitRules.HealerMagicKey)).ToString("0.#", CultureInfo.InvariantCulture)} patk={rows[rows.Count - 1].PhysicalAttackPower.ToString("0.##", CultureInfo.InvariantCulture)}/{template.PhysicalAttackPower.ToString("0.##", CultureInfo.InvariantCulture)}");
        }
        ReadTuning();
    }

    // The bandits' numbers from the settings: the healer's lifebloom and the bleeding of their hits.
    private static void ReadTuning()
    {
        Lifebloom.NpcPercent = LifebloomRules.NpcPercent(CoreSettings.Number(LifebloomRules.NpcPercentKey));
        Lifebloom.NpcStatFactor = LifebloomRules.NpcStatFactor(CoreSettings.Number(LifebloomRules.NpcStatFactorKey));
        var bleed = EnemyAbilityRules.BanditBleedOf(CoreSettings.Number(EnemyAbilityRules.BanditBleedChanceKey), CoreSettings.Number(EnemyAbilityRules.BanditBleedStacksKey));
        EnemyAbilityRules.UseBanditBleed(bleed);
        DelversHost.Info($"CORE_UNIT_TUNING lifebloom={Lifebloom.NpcPercent.ToString("0.##", CultureInfo.InvariantCulture)}% statfactor={Lifebloom.NpcStatFactor.ToString("0.##", CultureInfo.InvariantCulture)} bleed={(bleed.Chance * 100.0).ToString("0.#", CultureInfo.InvariantCulture)}%x{bleed.Stacks}");
    }

    private static UnitTableData Copy(UnitTableData t, UnitTableData player, CoreUnitRules.Definition unit, float health)
    {
        var row = new UnitTableData();
        row.Key = unit.Key;
        row.DevComment1 = "Delvers Core: " + unit.Key;
        row.Dev_Art = t.Dev_Art;
        row.Dev_CopseArt = t.Dev_CopseArt;
        row.Faction = (FactionType)CoreUnitRules.FactionEnemy; // trap-ok: table row before any spawn
        row.SubFaction = (SubFactionType)CoreUnitRules.SubFactionNpc; // trap-ok: table row before any spawn
        row.DefaultTraits = new Il2CppStringArray(CoreUnitRules.RowValues(CoreUnitRules.TraitsFor(unit)));
        // The healer always has Lifebloom; its row is always registered.
        row.DefaultSkills = new Il2CppStringArray(CoreUnitRules.RowValues(CoreUnitRules.SkillsFor(unit, true)));
        // Bandits drop exactly what they wear, when undressed; no goblin loot.
        row.OnDeathItemsAndDrops = new Il2CppStringArray(CoreUnitRules.RowValues(CorpseRules.BanditDeathLoot));
        row.FirstKillItemsAndDrops = new Il2CppStringArray(CoreUnitRules.RowValues(CorpseRules.BanditDeathLoot));
        row.BehaviourType = (EntityAI)CoreUnitRules.BehaviourDefaultEnemy;
        row.CombatPattern = CoreUnitRules.Pattern;
        row.OnDeathExp = t.OnDeathExp;
        row.BloodType = t.BloodType;
        row.Size = new Il2CppStructArray<int>(new[] { t.Size[0], t.Size[1] });
        // The human prefab is as tall as a party member.
        row.SpriteHeight = player?.SpriteHeight ?? t.SpriteHeight;
        row.BodyHealthRatio_Head = t.BodyHealthRatio_Head;
        row.BodyHealthRatio_Body = t.BodyHealthRatio_Body;
        row.MaxHealthTotal = health;
        row.PhysicalAttackPower = CoreUnitRules.PhysicalAttackOf(unit, t.PhysicalAttackPower);
        row.MagicAttackPower = HealerRowMagic(unit, t.MagicAttackPower) * unit.MagicFactor;
        row.AttackSpeed = t.AttackSpeed;
        row.CriticalChance = t.CriticalChance;
        // bandits.armor[.<role>].physical / .magic add to the template's resistance (BanditArmorRules), 50 by default (stage B);
        // a weak bandit adds its level's own bonus instead (WeakBanditRules).
        row.PhysicalResistance = unit.ArmorBonus is double physical ? BanditArmorRules.RowResistance(t.PhysicalResistance, physical, null)
            : BanditArmorRules.RowResistance(t.PhysicalResistance,
                CoreSettings.Number(BanditArmorRules.Key(null, unit.Role, "physical")), CoreSettings.Number(BanditArmorRules.Key(null, null, "physical")));
        row.MagicResistance = unit.ArmorBonus is double magic ? BanditArmorRules.RowResistance(t.MagicResistance, magic, null)
            : BanditArmorRules.RowResistance(t.MagicResistance,
                CoreSettings.Number(BanditArmorRules.Key(null, unit.Role, "magic")), CoreSettings.Number(BanditArmorRules.Key(null, null, "magic")));
        row.MovementSpeed = t.MovementSpeed;
        row.VisionRange = t.VisionRange;
        return row;
    }

    internal static void ReportStats()
    {
        if (Active && !statsReported)
        {
            statsReported = true;
            foreach (var unit in CoreUnitRules.All)
                DelversHost.Info("CORE_UNIT_STATS key=" + unit.Key + " ok=" + (StatUtility.GetDefaultStats(unit.Key) != null ? "true" : "false"));
            var forms = Profiles();
            var parts = new List<string>();
            foreach (var pair in forms) parts.Add(pair.Key + "=" + pair.Value.Count);
            DelversHost.Info("CORE_UNIT_PROFILES forms=" + string.Join(",", parts));
            // Probe: whether the game has the player prefab of every form (ResourceLoader.LoadUnitObject).
            var loader = ResourceLoader.Instance;
            var prefabs = new List<string>();
            foreach (var pair in forms)
            {
                UnitObject prefab = null;
                try { prefab = loader?.LoadUnitObject(CoreUnitRules.PlayerPrefabPrefix + pair.Key); }
                catch (Exception) { }
                prefabs.Add(pair.Key + "=" + (prefab != null ? "ok" : "missing"));
            }
            DelversHost.Info("CORE_UNIT_PREFABS " + string.Join(",", prefabs));
        }
    }

    // The healer's row value is bandits.healer.matk itself (CoreUnitRules.MagicAttackOf); CORE_UNIT_MAGIC says what it gives
    // on Normal. Every other role keeps the template's.
    private static float HealerRowMagic(CoreUnitRules.Definition unit, float template)
    {
        var row = CoreUnitRules.MagicAttackOf(unit, template, CoreSettings.Number(CoreUnitRules.HealerMagicKey));
        if (unit?.Role != "healer") return row;
        // The affecter data is not loaded yet when the rows are registered (CORE_UNIT_MAGIC_FAILED in 4DC3E809), so Normal's
        // measured modifier stands as a constant; the probe keeps checking it in the fight.
        var normal = CoreUnitRules.NormalMagicPerModifier;
        DelversHost.Info(GroupAiHealerSeedRules.MagicSetLine(CoreUnitRules.EffectiveOf(row, normal), normal, row));
        return row;
    }

    // Around EntityObjectsContainer.SpawnUnitObject: the look of the bandit being created.
    internal static void BeginSpawn(UnitEntity entity)
    {
        spawningProfile = null;
        spawningUnit = null;
        spawningGuid = null;
        spawningDead = entity != null && entity.IsDead;
        if (entity != null && CoreUnitRules.TryGet(entity.Key, out var definition))
        {
            // The form by role (humans half of the time; the herbalist only female), then the profile within it.
            var forms = Profiles();
            var guid = CoreGame.GuidOf(entity);
            var form = CoreUnitRules.PickForm(guid, CoreUnitRules.HumanFormsFor(definition), CoreUnitRules.OtherFormsFor(definition),
                f => forms.TryGetValue(f, out var list) ? list.Count : 0);
            var pool = form != null ? forms[form] : null;
            var index = pool == null ? -1 : CoreUnitRules.ProfileIndexIn(guid, pool.Count);
            if (index >= 0)
            {
                spawningProfile = pool[index];
                spawningUnit = definition;
                spawningGuid = entity.Guid.ToString();
                CorpseKeep.NoteLoadedCorpse(entity);
            }
        }
    }

    internal static void EndSpawn() => BeginSpawn(null);

    internal static string PrefabFor(string key)
        => spawningUnit != null && key == spawningUnit.Key
            ? CoreUnitRules.PlayerPrefabPrefix + CoreUnitRules.VisualTypeOf(spawningProfile.Key)
            : null;

    // The prefab of a form was missing: a human profile of the unit's role takes over (logged), for the object and the look.
    internal static string Fallback(string key)
    {
        if (spawningUnit == null || key != spawningUnit.Key) return null;
        var form = CoreUnitRules.HumanFormsFor(spawningUnit)[0];
        var forms = Profiles();
        if (!forms.TryGetValue(form, out var pool) || pool.Count == 0) return null;
        var missing = spawningProfile?.Key;
        spawningProfile = pool[CoreUnitRules.ProfileIndexIn(Guid.TryParse(spawningGuid, out var guid) ? guid : Guid.Empty, pool.Count)];
        DelversHost.Warning("CORE_UNIT_PREFAB_FALLBACK key=" + key + " missing=" + (missing ?? "-") + " profile=" + spawningProfile.Key);
        return CoreUnitRules.PlayerPrefabPrefix + form;
    }

    internal static void Dress(UnitObject unitObject)
    {
        if (spawningUnit == null || unitObject == null) return;
        var visual = unitObject.GetComponentInChildren<UnitVisual>(true);
        if (visual == null)
        {
            DelversHost.Warning("CORE_UNIT_VISUAL_MISSING key=" + spawningUnit.Key);
            return;
        }
        visual.SetProfileData(new UnitProfileData(spawningProfile));
        // An undressed corpse stays naked after a load (equipment on the body, step 2).
        var undressed = spawningGuid != null && Guid.TryParse(spawningGuid, out var guid) && CorpseKeep.IsUndressed(guid);
        // A loaded boss corpse without its part (BossItems.PartGone) is drawn so.
        var partGone = spawningDead && BossItems.PartGone(spawningUnit);
        var equipment = new BodyPartsStringDictionary();
        foreach (var (part, item) in CoreUnitRules.EquipInfo(spawningUnit, undressed, partGone))
            equipment.Add(Enum.Parse<BodyPartsType>(part), item);
        visual.RefreshAllEquipment(equipment);
        visual.PaintAll();
        if (spawningGuid != null) bodies[spawningGuid] = unitObject;
        DelversHost.Info($"CORE_UNIT_DRESSED key={spawningUnit.Key} profile={spawningProfile.Key} form={CoreUnitRules.VisualTypeOf(spawningProfile.Key)} kit={spawningUnit.Kit.Length} undressed={(undressed ? 1 : 0)}");
    }

    // Step 2: the corpse's body drawn with every slot empty; false when the body is gone (a load draws it naked then).
    internal static bool DrawNaked(string guid, CoreUnitRules.Definition definition) => DrawCorpse(guid, definition, true, false);

    // The corpse's body drawn again: undressed, or dressed without its boss part (BossItems.PartGone).
    internal static bool DrawCorpse(string guid, CoreUnitRules.Definition definition, bool undressed, bool partGone)
    {
        if (guid == null || definition == null || !bodies.TryGetValue(guid, out var body) || body == null) return false;
        var visual = body.GetComponentInChildren<UnitVisual>(true);
        if (visual == null) return false;
        var equipment = new BodyPartsStringDictionary();
        foreach (var (part, item) in CoreUnitRules.EquipInfo(definition, undressed, partGone))
            equipment.Add(Enum.Parse<BodyPartsType>(part), item);
        visual.RefreshAllEquipment(equipment);
        visual.PaintAll();
        return true;
    }

    internal static void ForgetBody(string guid)
    {
        if (guid != null) bodies.Remove(guid);
    }

    internal static void ClearBodies() => bodies.Clear();

    // The kit as items on the bandit's cell, through the game's own spawn request.
    internal static void DropKit(MapType map, UnitEntity unit)
    {
        if (unit == null || !CoreUnitRules.TryGet(unit.Key, out var definition)) return;
        var inMap = InMapOf(map);
        if (inMap == null) return;
        var at = inMap.GetLogicalPosition(unit.Guid);
        var items = CoreUnitRules.DropItems(definition, BossItems.PartGone(definition));
        foreach (var item in items)
        {
            KitDrops.NoteDrop(item, at);
            CoreGame.Enqueue(new SpawnItemRequested(map, item, at).Cast<IEventData>());
        }
        DelversHost.Info($"CORE_UNIT_DROPPED key={unit.Key} items={string.Join(",", items)} at={at.x},{at.y}");
    }

    internal static void ReportDespawn(MapType map, UnitEntity unit)
    {
        if (unit != null && CoreUnitRules.IsCoreUnit(unit.Key) && despawnLines++ < DespawnLogCap)
            DelversHost.Info($"CORE_UNIT_DESPAWNED key={unit.Key} unit={CoreGame.GuidOf(unit):D} dead={unit.IsDead} map={map}");
    }

    private static IInMapDataSupplier InMapOf(MapType map)
    {
        var maps = CoreGame.Flow?._inMapDatas;
        if (maps == null) return null;
        foreach (var pair in maps)
            if (pair.Key == map) return pair.Value;
        return null;
    }

    // The game's visual profiles of every form the bandits may take (CoreUnitRules.HumanForms, OtherForms), each form in a
    // fixed order, read once. Profiles are numbered from 0 or 1 per form, gaps end a form after a few misses.
    private static Dictionary<string, List<UnitProfileTableData>> Profiles()
    {
        if (profiles != null) return profiles;
        var sheets = DataSheetManager.Instance;
        var byForm = new Dictionary<string, List<UnitProfileTableData>>(StringComparer.Ordinal);
        if (sheets != null)
        {
            foreach (var form in CoreUnitRules.HumanForms) byForm[form] = Read(sheets, form);
            foreach (var form in CoreUnitRules.OtherForms) byForm[form] = Read(sheets, form);
        }
        profiles = byForm;
        return profiles;
    }

    private static List<UnitProfileTableData> Read(DataSheetManager sheets, string form)
    {
        var sorted = new SortedDictionary<string, UnitProfileTableData>(StringComparer.Ordinal);
        var misses = 0;
        for (var i = 0; i < 100 && misses < 5; i++)
        {
            var profile = sheets.GetUnitProfileData(CoreUnitRules.ProfilePrefix(form) + i);
            if (profile == null) { misses++; continue; }
            misses = 0;
            if (CoreUnitRules.IsRotationProfile(profile.Key)) sorted[profile.Key] = profile;
        }
        return new List<UnitProfileTableData>(sorted.Values);
    }

    private static UnitTableData Find(Il2CppSystem.Collections.Generic.List<UnitTableData> rows, string key)
    {
        for (var i = 0; i < rows.Count; i++)
            if (rows[i] != null && rows[i].Key == key) return rows[i];
        return null;
    }
}
