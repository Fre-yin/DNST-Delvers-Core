using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace DungeonSettlersDelvers.Core;

// The boss bandits' own parts in the game (BossItemRules): their rows copied from the blood wood parts (CoreItemRows) and the
// save's stand-ins (CoreSaveItems), the way the bandages do it. The boss party wears them (CoreUnitRules.Units) and drops them
// with its kit when undressed (CoreUnits.DropKit, condition: KitDrops); only the boss the floor's seed picks keeps its part
// (BossItemRules.ChosenIndex), the other corpses lie without it. Their violet look: BossItemLook.
internal static class BossItems
{
    private static string chosenLogged;

    internal static void Initialize()
    {
        foreach (var (key, native) in BossItemRules.Parts) CoreSaveItems.Register(key, native);
    }

    // Whether this dead boss lies without its boss part: a boss of the party that the floor's seed did not pick. Without the
    // game's dungeon data every boss keeps its part.
    internal static bool PartGone(CoreUnitRules.Definition unit)
    {
        var index = Array.IndexOf(CoreUnitRules.Units, unit);
        if (index < 0 || !CoreUnitRules.IsBossParty(unit) || CoreUnitRules.SpecialCount(unit) == 0) return false;
        var dungeon = CoreGame.Flow?._campaignData?.DungeonDataContainer;
        if (dungeon == null) return false;
        var seed = dungeon.GetCurrentSeed();
        var region = (int)dungeon.GetCurrentSelectedRegion();
        var floor = dungeon.GetCurrentFloor();
        var chosen = BossItemRules.ChosenIndex(seed, region, floor, CoreUnitRules.Units.Length);
        var id = seed + "|" + region + "|" + floor;
        if (chosenLogged != id)
        {
            chosenLogged = id;
            DelversHost.Info(BossItemRules.ChosenLine(seed, region, floor, chosen, chosen >= 0 ? CoreUnitRules.Units[chosen].Role : "-"));
        }
        return index != chosen;
    }

    // The part's own base attack and statuses where they differ from the blood wood part (BossItemRules.BaseAttack, Statuses).
    private static void SetValues(ItemTableData row, string key, ItemTableData template)
    {
        var nativeBase = (template.Equipment_PhysicalAttackPower, template.Equipment_MagicAttackPower);
        var ownBase = BossItemRules.BaseAttack(key, nativeBase.Item1, nativeBase.Item2);
        row.Equipment_PhysicalAttackPower = ownBase.Physical;
        row.Equipment_MagicAttackPower = ownBase.Magic;
        var source = template.Equipment_AdditionalStatuses;
        var native = new string[source?.Length ?? 0];
        for (var i = 0; i < native.Length; i++) native[i] = source[i];
        var own = BossItemRules.Statuses(key, native);
        if (!own.SequenceEqual(native)) row.Equipment_AdditionalStatuses = new Il2CppStringArray(own);
        row.Equipment_OnEquipAffecters = new Il2CppStringArray(new[] { BossItemRules.EffectKey(key) });
        var defaultSkills = template.Equipment_ItemDefaultSkills;
        var skills = new string[defaultSkills?.Length ?? 0];
        for (var i = 0; i < skills.Length; i++) skills[i] = defaultSkills[i];
        DelversHost.Info(BossItemRules.StatsLine(key, nativeBase, ownBase, native, own, skills));
    }

    // The parts' equipment effects (BossItemRules.EffectKey) as rows made from Devrick's pendant's, shown to the player as
    // the pendant's is; added with Core's other effects (ReviveEffectsSheetPatch).
    internal static void RegisterEffects(AffecterSheet sheet)
    {
        if (sheet?._affecterTable == null) return;
        var template = sheet.GetData(BossItemRules.EffectTemplate);
        foreach (var (key, _) in BossItemRules.Parts)
        {
            var effect = BossItemRules.EffectKey(key);
            if (sheet.GetData(effect) != null) continue;
            var exposed = template != null && template.IsExposedToPlayer;
            var (ok, _) = CoreAffecterRows.Add(sheet, effect, BossItemRules.EffectTemplate, "boss part equipment effect of " + key, null, false,
                template?.MaxStack ?? 1, template != null && template.HasDuration, null, true, exposed);
            var line = BossItemRules.EffectLine(effect, BossItemRules.EffectTemplate, ok, exposed);
            if (ok) DelversHost.Info(line);
            else DelversHost.Warning(line);
        }
        // The Blood Warden's shield: a Barrier effect of its own (categories and stacks as the game's), shown, with a duration.
        if (sheet.GetData(BossEffectRules.BloodGuardKey) != null) return;
        var guardMax = BossEffectRules.ShieldMaxStack(sheet.GetData(BossEffectRules.BloodGuardTemplate)?.MaxStack);
        var (guard, _) = CoreAffecterRows.Add(sheet, BossEffectRules.BloodGuardKey, BossEffectRules.BloodGuardTemplate,
            "Blood Warden's shield of the boss shield", null, false, guardMax, true, null, true, true);
        var guardLine = BossItemRules.EffectLine(BossEffectRules.BloodGuardKey, BossEffectRules.BloodGuardTemplate, guard, true);
        if (guard) DelversHost.Info(guardLine);
        else DelversHost.Warning(guardLine);
    }

    // After a load the parts' equipment effects are gone: CoreSaveEffects keeps them out of the save and the game does not
    // put them back for equipment it loads. For every living party member they come back for the
    // boss parts it wears, once each (BossItemRules.EffectsFor), as the revive sickness does (ReviveSickness.Restore).
    internal static void RestoreEffects()
    {
        var maps = CoreGame.Flow?._inMapDatas;
        if (maps == null) return;
        var units = 0;
        var restored = 0;
        foreach (var pair in maps)
        {
            var members = pair.Value?.GetAllPlayerUnits(false);
            for (var i = 0; members != null && i < members.Count; i++)
            {
                var unit = members[i];
                if (unit == null || unit.IsDead) continue;
                var affecters = CoreGame.Affecters(unit);
                if (affecters == null) continue;
                var any = false;
                foreach (var effect in BossItemRules.EffectsFor(CorpseKeep.EquippedKeys(unit.Cast<IEntity>())))
                {
                    if (affecters.Has(effect)) continue;
                    CoreGame.Apply(pair.Key, new AffecterApplyData(unit.Cast<IEntity>(), effect, 1, 0f, false, null).Cast<IApplyData>());
                    restored++;
                    any = true;
                }
                if (any) units++;
            }
        }
        if (restored > 0) DelversHost.Info(BossItemRules.RestoredLine(units, restored));
    }

    // The game finds an item's recipe by its output only through a bench (RecipeSheet.OutputMapping fills _outputToRecipe in
    // its loop over RecipeAvailableBuildings), and the repair sound asks for it (PlayOneTimeSFX.ConvertTaskSFXID ->
    // GetRecipeDataByOutputItem; ArgumentNullException "key"). After the parse each part points to
    // its repair recipe there, still at no bench.
    internal static void MapRecipeOutputs(RecipeSheet sheet)
    {
        var outputs = sheet?._outputToRecipe;
        var table = sheet?._recipeTable;
        if (outputs == null || table == null) return;
        foreach (var (key, _) in BossItemRules.Parts)
        {
            var recipe = BossItemRules.RepairRecipeKey(key);
            if (!table.ContainsKey(recipe)) continue;
            var mapped = !outputs.ContainsKey(key);
            if (mapped) outputs[key] = recipe;
            DelversHost.Info(BossItemRules.OutputLine(key, recipe, mapped));
        }
    }

    // The parts' repair recipes (BossItemRules.RepairRecipeKey), copied from the blood wood parts' with no bench to make them
    // at; added beside Core's first aid recipes before the game parses the table (FirstAidRecipePatch).
    internal static void RegisterRecipes(Il2CppSystem.Collections.Generic.List<RecipeTableData> rows)
    {
        if (rows == null) return;
        foreach (var (key, native) in BossItemRules.Parts)
        {
            var recipeKey = BossItemRules.RepairRecipeKey(key);
            if (FirstAid.Find(rows, recipeKey) != null) continue;
            var templateKey = BossItemRules.RepairRecipeKey(native);
            var t = FirstAid.Find(rows, templateKey);
            if (t == null)
            {
                DelversHost.Warning(BossItemRules.RecipeLine(recipeKey, templateKey, false, 0f));
                continue;
            }
            rows.Add(new RecipeTableData
            {
                Key = recipeKey,
                DevComment1 = "Delvers Core: repair recipe of " + key,
                OutputItem = key,
                OutputItemAmount = t.OutputItemAmount,
                RecipeSFX = t.RecipeSFX,
                WorkType = t.WorkType,
                RecipeAvailableBuildings = new Il2CppStringArray(0),
                CraftingWorkloadRequired = t.CraftingWorkloadRequired,
                IngredientKeys = t.IngredientKeys,
                IngredientAmounts = t.IngredientAmounts
            });
            DelversHost.Info(BossItemRules.RecipeLine(recipeKey, templateKey, true, t.CraftingWorkloadRequired));
        }
    }

    internal static void RegisterItems(ItemSheet sheet)
    {
        if (sheet?._itemTable == null) return;
        foreach (var (key, native) in BossItemRules.Parts)
        {
            if (sheet._itemTable.ContainsKey(key)) continue;
            var found = sheet._itemTable.TryGetValue(native, out var t) && t != null;
            if (found)
            {
                var row = CoreItemRows.CopyOf(t, key, "Delvers Core: boss bandit part; written as " + native + " in the game's save.");
                row.Rarity = (Rarity)BossItemRules.RarityValue;
                SetValues(row, key, t);
                CoreItemRows.Add(sheet, row);
            }
            var line = BossItemRules.RegisteredLine(key, native, BossItemRules.RarityValue, found);
            if (found) DelversHost.Info(line);
            else DelversHost.Warning(line);
        }
    }
}

[HarmonyPatch(typeof(ItemSheet), nameof(ItemSheet.Parse))]
internal static class BossItemSheetPatch
{
    private static void Postfix(ItemSheet __instance)
    {
        try { BossItems.RegisterItems(__instance); }
        catch (Exception ex) { DelversHost.Error("CORE_BOSS_ITEM_FAILED: " + ex); }
    }
}
