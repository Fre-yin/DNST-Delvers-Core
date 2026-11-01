using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace DungeonSettlersDelvers.Core;

// First aid (build 3d): the bandages can be made. Light and heavy bandages are
// cloth work at the basic workstation (from cloth and thick cloth); herbal and salve bandages are made
// at the alchemist's workstation from a heavy bandage and wild herbs or an ointment. Weaving
// (RESEARCH_WeaveProcessing) also unlocks the heavy bandage. The new research First Aid (one tech book) unlocks the herbal and the
// salve bandage; it is the root of the Delvers tree (DelversResearchTree), open from the start (before, it
// came after basic alchemy and weaving and stood among the game's nodes at 2,14, on the line from cooking to drying). Core's rows join the game's tables before the game parses them (FirstAidRecipePatch,
// FirstAidResearchPatch); in a save, Core's research and tasks are kept out of the game's part
// (CoreSaveResearch, CoreSaveTasks). Recipe keys follow the item: ITEM_DelversCoreX -> RECIPE_DelversCoreX.
internal static class FirstAid
{
    private readonly struct Recipe
    {
        internal string Item { get; }
        internal bool Alchemy { get; }
        internal float Workload { get; }
        internal string[] Ingredients { get; }
        internal int[] Amounts { get; }

        internal Recipe(string item, bool alchemy, float workload, string[] ingredients, int[] amounts)
        {
            Item = item;
            Alchemy = alchemy;
            Workload = workload;
            Ingredients = ingredients;
            Amounts = amounts;
        }
    }

    internal const string ResearchKey = BandageText.FirstAidResearchKey;
    internal const string AlchemyKey = "RESEARCH_BasicAlchemy";
    internal const string WeavingKey = "RESEARCH_WeaveProcessing";
    private const string FabricTemplate = "RECIPE_Textile";
    private const string AlchemyTemplate = "RECIPE_Ointment";
    private const string BasicBench = "BUILDING_BasicWorkstation";
    private const string AlchemyBench = "BUILDING_AlchemistWorkstation";
    private const int ResearchWorkload = 200;
    private const string ResearchBook = "ITEM_TechBook_2";


    private static readonly Recipe[] Recipes =
    {
        new(Bandages.LightKey, false, 200f, new[] { "ITEM_Textile" }, new[] { 1 }),
        new(Bandages.HeavyKey, false, 400f, new[] { "ITEM_ThickTextile" }, new[] { 1 }),
        // The herbal bandage upgrades the light one, as the salve upgrades the heavy one.
        new(Bandages.HerbalKey, true, 500f, new[] { Bandages.LightKey, "ITEM_WildHerb" }, new[] { 1, 2 }),
        new(Bandages.SalveKey, true, 500f, new[] { Bandages.HeavyKey, "ITEM_Ointment" }, new[] { 1, 1 })
    };

    internal static string RecipeKey(string itemKey) => CoreSaveTasks.Prefix + itemKey.Substring(CoreSaveItems.Prefix.Length);

    // Sound and work type come from the game's own cloth or ointment recipe.
    internal static void RegisterRecipes(RecipeTable table)
    {
        var rows = table?.tableDatas;
        if (rows == null) return;
        var fabric = Find(rows, FabricTemplate);
        var alchemy = Find(rows, AlchemyTemplate);
        foreach (var recipe in Recipes)
        {
            var key = RecipeKey(recipe.Item);
            if (Find(rows, key) != null) continue;
            var template = recipe.Alchemy ? alchemy : fabric;
            if (template == null)
            {
                DelversHost.Warning("CORE_FIRSTAID_TEMPLATE_MISSING key=" + (recipe.Alchemy ? AlchemyTemplate : FabricTemplate));
                continue;
            }
            var bench = recipe.Alchemy ? AlchemyBench : BasicBench;
            rows.Add(new RecipeTableData
            {
                Key = key,
                DevComment1 = "Delvers Core: " + recipe.Item,
                OutputItem = recipe.Item,
                OutputItemAmount = 1,
                RecipeSFX = template.RecipeSFX,
                WorkType = template.WorkType,
                RecipeAvailableBuildings = new Il2CppStringArray(new[] { bench }),
                CraftingWorkloadRequired = recipe.Workload,
                IngredientKeys = new Il2CppStringArray(recipe.Ingredients),
                IngredientAmounts = new Il2CppStructArray<int>(recipe.Amounts)
            });
            DelversHost.Info($"CORE_FIRSTAID_RECIPE key={key} bench={bench} workload={recipe.Workload:0}");
        }
    }

    internal static void RegisterResearch(ResearchTable table)
    {
        var rows = table?.tableDatas;
        if (rows == null) return;
        ResearchTableData weaving = null;
        ResearchTableData own = null;
        var taken = new HashSet<(int X, int Y)>();
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row == null) continue;
            if (row.Key == WeavingKey) weaving = row;
            else if (row.Key == ResearchKey) own = row;
            taken.Add((row.GridX, row.GridY));
        }
        if (weaving == null)
        {
            DelversHost.Warning("CORE_FIRSTAID_RESEARCH_MISSING weaving=False");
            return;
        }
        if (!Contains(weaving.ResearchResult, Bandages.HeavyKey))
        {
            weaving.ResearchResult = Append(weaving.ResearchResult, Bandages.HeavyKey);
            DelversHost.Info("CORE_FIRSTAID_WEAVING result+=" + Bandages.HeavyKey);
        }
        if (own != null) return;
        // A game row on the root's place (a later game update) is reported; the root stands there all the same.
        var slot = DelversResearchTree.Place(0, 0);
        var free = !taken.Contains(slot);
        rows.Add(new ResearchTableData
        {
            Key = ResearchKey,
            DevComment1 = "Delvers Core: First Aid",
            GridX = slot.X,
            GridY = slot.Y,
            Prerequisites = new Il2CppStringArray(DelversResearchTree.NoPrerequisites),
            RequiredWorkload = ResearchWorkload,
            UnlockRequiredItem = new Il2CppStringArray(new[] { ResearchBook }),
            ResearchResult = new Il2CppStringArray(new[] { Bandages.HerbalKey, Bandages.SalveKey })
        });
        DelversHost.Info($"CORE_FIRSTAID_RESEARCH key={ResearchKey} grid={slot.X},{slot.Y} free={free} workload={ResearchWorkload}");
    }

    internal static RecipeTableData Find(Il2CppSystem.Collections.Generic.List<RecipeTableData> rows, string key)
    {
        for (var i = 0; i < rows.Count; i++)
            if (rows[i] != null && rows[i].Key == key) return rows[i];
        return null;
    }

    private static bool Contains(Il2CppStringArray values, string value)
    {
        if (values == null) return false;
        for (var i = 0; i < values.Length; i++)
            if (values[i] == value) return true;
        return false;
    }

    private static Il2CppStringArray Append(Il2CppStringArray values, string value)
    {
        var count = values?.Length ?? 0;
        var result = new Il2CppStringArray(count + 1);
        for (var i = 0; i < count; i++) result[i] = values[i];
        result[count] = value;
        return result;
    }
}
