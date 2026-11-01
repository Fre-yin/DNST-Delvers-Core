using DungeonSettlersDelvers.Core;
using static TestKit;

// Core's own data inside the campaign save: block, effects, items, data and store.
internal static class CoreSaveSection
{
    const string OwnScroll = CoreSaveItems.Prefix + "ReviveScroll";

    static readonly string SaveRoot = Path.Combine(Path.GetTempPath(), "delvers-store-test", "Saves");
    static readonly string SavePath = Path.Combine(SaveRoot, "Clan_AutoSave_1.json");
    static readonly string OtherPath = Path.Combine(SaveRoot, "Clan_AutoSave_2.json");
    const string Plain = "{\n  \"EntityContainerSaveData\": {},\n  \"CampaignSaveHeader\": {\"SaveName\": \"Clan_AutoSave_1\"}\n}";

    static string ItemEntry(Guid guid, string key) => "\"" + guid + "\": {\n  \"Guid\": \"" + guid + "\",\n  \"Key\": \"" + key + "\"\n}";

    internal static void Run()
    {
        try
        {
            SaveBlockText();
            OwnEffects();
            OwnItems();
            SaveData();
            SaveStore();
            WoundBufferData();
            OwnDataKept();
            GamePartStaysClean();
        }
        finally
        {
            CoreSaveItems.Clear();
            CoreSaveStore.ResetLifecycle();
        }
    }

    static void SaveBlockText()
    {
        // Core's own data inside the campaign save: text work, content and the store.
        var save = "{\r\n  \"EntityContainerSaveData\": {\"a\": [1, 2, {\"b\": \"x}\\\"y\"}]},\r\n  \"CampaignSaveHeader\": {\"SaveName\": \"Test\"}\r\n}\r\n";
        var block = "{\"version\":1,\"campaign\":\"c68842b3-e50b-43df-8604-07dc8ca269b2\"}";
        var with = CoreSaveBlock.Append(save, block);
        Check(System.Text.Json.JsonDocument.Parse(with).RootElement.GetProperty("DelversCore").GetProperty("version").GetInt32() == 1
                && CoreSaveBlock.Extract(with) == block && CoreSaveBlock.Extract(save) == null,
            "save block: appended as the last property, valid JSON, read back; none without");
        var twice = CoreSaveBlock.Append(with, "{\"version\":2}");
        Check(CoreSaveBlock.Extract(twice) == "{\"version\":2}" && twice.Split("DelversCore").Length == 2,
            "save block: appending again replaces the block");
        var removed = CoreSaveBlock.Remove(with);
        Check(!System.Text.Json.JsonDocument.Parse(removed).RootElement.TryGetProperty("DelversCore", out _)
                && System.Text.Json.JsonDocument.Parse(removed).RootElement.TryGetProperty("CampaignSaveHeader", out _),
            "save block: removing leaves the save intact");
        Check(CoreSaveBlock.Append(save, "[1]") == save && CoreSaveBlock.Append(save, "{broken") == save
                && CoreSaveBlock.Append("not json", block) == "not json",
            "save block: a broken block or a save that is no object stays untouched");
        Check(System.Text.Json.JsonDocument.Parse(CoreSaveBlock.Append("{}", block)).RootElement.GetProperty("DelversCore").ValueKind
                == System.Text.Json.JsonValueKind.Object,
            "save block: an empty save object gets the block without a comma");
        Check(CoreSaveBlock.Extract("{\"x\": {\"DelversCore\": {\"a\":1}}, \"y\": 2}") == null
                && CoreSaveBlock.Extract("{\"n\": \"say \\\"DelversCore\\\": {}\"}") == null,
            "save block: a nested property or a name inside a string never counts");
        var nestedBlock = "{\"revive\":{\"u\":{\"snapshot\":[\"AFFECTER_Elf:1\",\"K:2\"],\"revives\":1}},\"t\":\"}{\"}";
        Check(CoreSaveBlock.Extract(CoreSaveBlock.Append(save, nestedBlock)) == nestedBlock,
            "save block: nested arrays and braces inside strings survive");
    }

    static void OwnEffects()
    {
        // Core's own effects stay out of the game's part of a save.
        string Holders(params string[] keys)
            => "{\"AffecterHolders\": [" + string.Join(",", keys.Select(key =>
                "\n  {\n    \"Key\": \"" + key + "\",\n    \"Stack\": 1,\n    \"Duration\": 3.5,\n    \"InitialDuration\": 7.0\n  }")) + "\n], \"Next\": 1}";
        List<string> Keys(string json) => System.Text.Json.JsonDocument.Parse(json).RootElement.GetProperty("AffecterHolders")
            .EnumerateArray().Select(holder => holder.GetProperty("Key").GetString()).ToList();
        var sickKey = CoreSaveEffects.Prefix + "RevivalSickness";
        var panicKey = CoreSaveEffects.Prefix + "DungeonPanic";
        foreach (var (input, expected, name) in new[]
        {
            (Holders("AFFECTER_Elf", sickKey, "AFFECTER_Brave"), new[] { "AFFECTER_Elf", "AFFECTER_Brave" }, "middle"),
            (Holders(sickKey, "AFFECTER_Elf"), new[] { "AFFECTER_Elf" }, "first"),
            (Holders("AFFECTER_Elf", panicKey), new[] { "AFFECTER_Elf" }, "last"),
            (Holders(sickKey, panicKey), Array.Empty<string>(), "all"),
            (Holders("AFFECTER_Elf", sickKey, panicKey, "AFFECTER_Brave"), new[] { "AFFECTER_Elf", "AFFECTER_Brave" }, "two in a row")
        })
        {
            var stripped = CoreSaveEffects.Strip(input, out var count);
            Check(Keys(stripped).SequenceEqual(expected) && count == input.Split(CoreSaveEffects.Prefix).Length - 1,
                "own effects: removed from the game's list (" + name + "), the JSON stays valid");
        }
        var foreign = Holders("AFFECTER_Elf", "AFFECTER_DelversElfArchmage", "AFFECTER_DelversBooklover");
        Check(ReferenceEquals(CoreSaveEffects.Strip(foreign, out var none), foreign) && none == 0,
            "own effects: Frieren's AFFECTER_Delvers keys and native keys stay untouched");
    }

    static void OwnItems()
    {
        // Core's own items: written as their native stand-in, turned back by GUID when read.
        CoreSaveItems.Clear();
        CoreSaveItems.Register(OwnScroll, "ITEM_ReturnScroll");
        var scrollA = Guid.NewGuid();
        var plainScroll = Guid.NewGuid();
        var items = "{\"ItemSaveData\": {" + ItemEntry(scrollA, OwnScroll) + ", " + ItemEntry(plainScroll, "ITEM_ReturnScroll")
            + "}, \"Record\": [\"" + OwnScroll + "\"]}";
        var ownedItems = new Dictionary<Guid, string>();
        var native = CoreSaveItems.ToNative(items, ownedItems, new CoreSaveItems.Places());
        Check(!native.Contains(CoreSaveItems.Prefix) && System.Text.Json.JsonDocument.Parse(native) != null
                && ownedItems.Count == 1 && ownedItems[scrollA] == OwnScroll,
            "own items: the game's part only holds the native return scroll, Core records which entity was its own");
        var itemsBack = CoreSaveItems.FromNative(native, ownedItems, new CoreSaveItems.Places(), out var restoredItems);
        Check(restoredItems == 1 && itemsBack.Contains("\"Key\": \"" + OwnScroll + "\"")
                && itemsBack.Contains("\"Guid\": \"" + plainScroll + "\",\n  \"Key\": \"ITEM_ReturnScroll\""),
            "own items: reading turns exactly the recorded entity back; a real return scroll stays one");
        // The herbal bandage is written as cloth now; a save from before holds thick cloth and still comes back.
        {
            const string ownHerbal = CoreSaveItems.Prefix + "HerbalBandage";
            CoreSaveItems.Register(ownHerbal, "ITEM_Textile");
            CoreSaveItems.RegisterLegacy(ownHerbal, "ITEM_ThickTextile");
            var oldHerbal = Guid.NewGuid();
            var newHerbal = Guid.NewGuid();
            var realThick = Guid.NewGuid();
            var herbalJson = "{\"ItemSaveData\": {" + ItemEntry(oldHerbal, "ITEM_ThickTextile") + ", " + ItemEntry(newHerbal, "ITEM_Textile") + ", "
                + ItemEntry(realThick, "ITEM_ThickTextile") + "}}";
            var herbalOwned = new Dictionary<Guid, string> { [oldHerbal] = ownHerbal, [newHerbal] = ownHerbal };
            var herbalBack = CoreSaveItems.FromNative(herbalJson, herbalOwned, new CoreSaveItems.Places(), out var herbalRestored);
            var written = CoreSaveItems.ToNative("{\"ItemSaveData\": {" + ItemEntry(newHerbal, ownHerbal) + "}}", new Dictionary<Guid, string>(), new CoreSaveItems.Places());
            Check(herbalRestored == 2 && herbalBack.Contains("\"Guid\": \"" + oldHerbal + "\",\n  \"Key\": \"" + ownHerbal + "\"")
                    && herbalBack.Contains("\"Guid\": \"" + realThick + "\",\n  \"Key\": \"ITEM_ThickTextile\"")
                    && written.Contains("\"Key\": \"ITEM_Textile\"") && !written.Contains("ThickTextile"),
                "own items: the herbal bandage is written as cloth; one written as thick cloth by an older Core still comes back");
            CoreSaveItems.Clear();
            CoreSaveItems.Register(OwnScroll, "ITEM_ReturnScroll");
        }
        var itemData = CoreSaveData.Create(Guid.NewGuid());
        itemData.SetOwnedItems(ownedItems);
        Check(CoreSaveData.ParseAny(itemData.Serialize()).OwnedItems().TryGetValue(scrollA, out var ownBack) && ownBack == OwnScroll
                && CoreSaveData.ParseAny("{broken") == null,
            "own items: the record survives Core's save data");
        var rejected = false;
        try { CoreSaveItems.Register("ITEM_ReviveScroll", "ITEM_ReturnScroll"); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "own items: only keys under Core's item prefix can have a stand-in");
    }

    static void SaveData()
    {
        var campaign = Guid.NewGuid();
        var unit = Guid.NewGuid();
        var data = CoreSaveData.Create(campaign);
        data.Apocalypse = new ApocalypseMark(true, false);
        data.SetSnapshot(unit, new[] { new TraitStack("AFFECTER_Elf", 1), new TraitStack("AFFECTER_Brave", 2) });
        data.SetRevivesUsed(unit, 1);
        var text = data.Serialize();
        var back = CoreSaveData.Parse(text, campaign);
        Check(back != null && back.Apocalypse.Combat && !back.Apocalypse.Management && back.RevivesUsed(unit) == 1
                && back.Snapshot(unit).Count == 2 && back.Snapshot(unit)[1].Stacks == 2 && back.Units().Count == 1,
            "save data: Apocalypse, snapshot and revives survive a round trip");
        Check(CoreSaveData.Parse(text, Guid.NewGuid()) == null && CoreSaveData.Parse("{broken", campaign) == null
                && CoreSaveData.Parse("[1]", campaign) == null,
            "save data: another campaign's or broken data is refused");
        var future = CoreSaveData.Parse(text.Insert(1, "\"future\":{\"x\":1},"), campaign);
        future.SetRevivesUsed(unit, 2);
        Check(future.Serialize().Contains("\"future\"") && future.RevivesUsed(unit) == 2,
            "save data: properties of a newer Core survive");
        back.Apocalypse = default;
        Check(!back.Serialize().Contains("apocalypse") && back.RevivesUsed(Guid.NewGuid()) == 0 && back.Snapshot(Guid.NewGuid()).Count == 0,
            "save data: no Apocalypse leaves no entry, unknown units read empty");
        back.SetSickness(unit, 3856.0, 7711.0);
        var sick = CoreSaveData.Parse(back.Serialize(), campaign);
        Check(sick.TryGetSickness(unit, out var left, out var all) && left == 3856.0 && all == 7711.0
                && !sick.TryGetSickness(Guid.NewGuid(), out _, out _),
            "save data: the revival sickness keeps its remaining and total length");
        sick.SetSickness(unit, 9999.0, 7711.0);
        Check(sick.TryGetSickness(unit, out left, out _) && left == 7711.0, "save data: the remaining length never exceeds the total");
        sick.SetSickness(unit, 0.0, 7711.0);
        Check(!sick.TryGetSickness(unit, out _, out _) && sick.RevivesUsed(unit) == 1,
            "save data: an ended sickness is removed, the revive count stays");
        Check(!CoreSaveData.Parse(text.Replace("\"revives\":1", "\"revives\":1,\"sickness\":{\"remaining\":\"x\",\"total\":5}"), campaign)
                .TryGetSickness(unit, out _, out _),
            "save data: a damaged sickness entry counts as none");
    }

    static void SaveStore()
    {
        var storeHost = new TestDelversHost("save-store");
        DelversHost.Bind(storeHost);
        try
        {
            StoreDecoratesAndLoads(storeHost);
            OwnItemsAndForeignBlock();
            CampaignFile(storeHost);
        }
        finally
        {
            CoreSaveStore.ResetLifecycle();
            DelversHost.Unbind(storeHost);
        }
    }

    static void StoreDecoratesAndLoads(TestDelversHost storeHost)
    {
        var campaign = Guid.NewGuid();
        var unit = Guid.NewGuid();
        CoreSaveStore.ResetLifecycle();
        Check(CoreSaveStore.Decorate(SavePath + ".tmp", Plain) == Plain, "save store: a file without a known block is written unchanged");
        CoreSaveStore.BeginNewCampaign(campaign);
        CoreSaveStore.Current.Apocalypse = new ApocalypseMark(true, true);
        CoreSaveStore.Current.SetRevivesUsed(unit, 1);
        CoreSaveStore.SaveStarted();
        var written = CoreSaveStore.Decorate(SavePath + ".tmp", Plain);
        CoreSaveStore.SaveEnded();
        Check(CoreSaveBlock.Extract(written) != null && System.Text.Json.JsonDocument.Parse(written) != null,
            "save store: the game's campaign save carries the current block");
        Check(CoreSaveStore.Decorate(Path.Combine(Path.GetTempPath(), "UserSetting.json.tmp"), Plain) == Plain
                && CoreSaveStore.Decorate(Path.Combine(SaveRoot, "SaveHeaderCache.json"), Plain) == Plain,
            "save store: other files and final file names stay untouched");
        CoreSaveStore.ResetLifecycle();
        CoreSaveStore.Remember(written, SavePath);
        CoreSaveStore.Remember(Plain, OtherPath);
        CoreSaveStore.BindLoaded(campaign, SavePath.Replace('\\', '/').ToUpperInvariant());
        Check(CoreSaveStore.Current.Apocalypse.Combat && CoreSaveStore.Current.Apocalypse.Management && CoreSaveStore.Current.RevivesUsed(unit) == 1,
            "save store: loading takes over the file's block, whatever the path spelling");
        CoreSaveStore.BindLoaded(campaign, OtherPath);
        Check(CoreSaveStore.Current.Units().Count == 0 && !CoreSaveStore.Current.Apocalypse.Any, "save store: a save without block starts empty");
        CoreSaveStore.BindLoaded(Guid.NewGuid(), SavePath);
        Check(CoreSaveStore.Current.Units().Count == 0 && storeHost.AllMessages.Any(message => message.Contains("CORE_SAVE_DATA_IGNORED")),
            "save store: another campaign's block is ignored and logged");
        CoreSaveStore.BindLoaded(campaign, null);
        Check(CoreSaveStore.Current.Campaign == campaign, "save store: an unknown path starts empty data for the campaign");
        var rewritten = CoreSaveStore.Decorate(SavePath + ".tmp", Plain.Replace("Clan_AutoSave_1", "Clan_AutoSave_1b"));
        Check(CoreSaveBlock.Extract(rewritten) == CoreSaveBlock.Extract(written), "save store: the game's rewrite of a file keeps its block");
        Check(CoreSaveStore.IsCampaignSaveTemp(SavePath + ".tmp") && !CoreSaveStore.IsCampaignSaveTemp(SavePath)
                && !CoreSaveStore.IsCampaignSaveTemp(null) && !CoreSaveStore.IsCampaignSaveTemp("x.json.tmp"),
            "save store: only Saves\\*.json.tmp counts");
        CoreSaveStore.SaveStarted();
        var odd = CoreSaveStore.Decorate(SavePath + ".tmp", "not json");
        CoreSaveStore.Decorate(SavePath + ".tmp", "still not json");
        CoreSaveStore.SaveEnded();
        Check(odd == "not json" && storeHost.AllMessages.Count(message => message.Contains("CORE_SAVE_DATA_FAILED stage=append")) == 1,
            "save store: an odd text stays as it is and is reported once");
        CoreSaveStore.SaveEnded();
        CoreSaveStore.SaveEnded();
        Check(CoreSaveStore.Decorate(OtherPath + ".tmp", Plain) == Plain, "save store: the save scope never goes below zero");
        CoreSaveStore.BeginNewCampaign(campaign);
        CoreSaveStore.Unbind();
        CoreSaveStore.SaveStarted();
        var unbound = CoreSaveStore.Decorate(Path.Combine(SaveRoot, "New_AutoSave_1.json.tmp"), Plain);
        CoreSaveStore.SaveEnded();
        Check(unbound == Plain, "save store: a new campaign without data writes no earlier campaign's block");
    }

    static void OwnItemsAndForeignBlock()
    {
        CoreSaveItems.Clear();
        CoreSaveItems.Register(OwnScroll, "ITEM_ReturnScroll");
        var scrollA = Guid.NewGuid();
        var campaign = Guid.NewGuid();
        // Own items through the whole store: native in the game's part, Core's again when read.
        CoreSaveStore.BeginNewCampaign(campaign);
        var itemSave = "{\n  \"ItemContainerSaveData\": {\"Base\": {\"ItemSaveData\": {" + ItemEntry(scrollA, OwnScroll) + "}}},\n  \"CampaignSaveHeader\": {}\n}";
        CoreSaveStore.SaveStarted();
        var itemWritten = CoreSaveStore.Decorate(SavePath + ".tmp", itemSave);
        CoreSaveStore.SaveEnded();
        var itemRead = CoreSaveStore.Remember(itemWritten, SavePath);
        Check(!CoreSaveBlock.Remove(itemWritten).Contains(CoreSaveItems.Prefix)
                && itemRead.Contains("\"Key\": \"" + OwnScroll + "\"") && CoreSaveStore.Current.OwnedItems().ContainsKey(scrollA),
            "save store: an own item is a return scroll in the file and Core's scroll again when the game reads it");

        // (version 4): a block of another campaign restores nothing into a save text that names its campaign.
        var foreignText = "{\n  \"ItemContainerSaveData\": {\"Base\": {\"ItemSaveData\": {" + ItemEntry(scrollA, CoreSaveItems.Prefix + "X") + "}}},\n  \"ClanSaveData\": {\"CampaignGuid\": \"" + Guid.NewGuid().ToString("D") + "\"}\n}";
        var foreignBlockText = CoreSaveBlock.Append(foreignText, CoreSaveBlock.Extract(itemWritten));
        Check(CoreSaveStore.Remember(foreignBlockText, SavePath + ".foreign") == foreignBlockText
                && CoreSaveBlock.CampaignOf(foreignText) != Guid.Empty && CoreSaveBlock.CampaignOf(itemSave) == Guid.Empty
                && CoreSaveBlock.BlockApplies(campaign, Guid.Empty) && CoreSaveBlock.BlockApplies(Guid.Empty, campaign)
                && CoreSaveBlock.BlockApplies(campaign, campaign) && !CoreSaveBlock.BlockApplies(campaign, Guid.NewGuid()),
            "save store: a block of another campaign restores nothing into a save text of a named campaign");
    }

    static void CampaignFile(TestDelversHost storeHost)
    {
        var unit = Guid.NewGuid();
        // Core's own file per campaign.
        var archive = Path.Combine(Path.GetTempPath(), "delvers-archive-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            CoreSaveStore.ResetLifecycle();
            CoreSaveStore.UseArchive(archive);
            var archived = Guid.NewGuid();
            // The game sets up a throwaway campaign at the start of every load: no file for it.
            var throwaway = Guid.NewGuid();
            CoreSaveStore.BeginNewCampaign(throwaway);
            Check(!File.Exists(CoreSaveStore.ArchivePath(throwaway)) && !Directory.Exists(archive),
                "campaign file: setting up a campaign without saving leaves no file");
            CoreSaveStore.BeginNewCampaign(archived);
            CoreSaveStore.Current.Apocalypse = new ApocalypseMark(true, false);
            CoreSaveStore.Current.SetRevivesUsed(unit, 3);
            CoreSaveStore.SaveStarted();
            CoreSaveStore.Decorate(SavePath + ".tmp", Plain);
            CoreSaveStore.SaveEnded();
            var stored = CoreSaveData.Parse(File.ReadAllText(CoreSaveStore.ArchivePath(archived)), archived);
            Check(stored != null && stored.Apocalypse.Combat && stored.RevivesUsed(unit) == 3
                    && Directory.GetFiles(archive).Length == 1,
                "campaign file: the first save creates it, every save renews it, and only that campaign has one");
            CoreSaveStore.Decorate(SavePath + ".tmp", Plain.Replace("Clan_AutoSave_1", "Renamed"));
            Check(CoreSaveData.Parse(File.ReadAllText(CoreSaveStore.ArchivePath(archived)), archived).RevivesUsed(unit) == 3,
                "campaign file: a rewrite of another file does not touch it");
            CoreSaveStore.Remember(Plain, OtherPath);
            CoreSaveStore.BindLoaded(archived, OtherPath);
            Check(CoreSaveStore.Current.Apocalypse.Combat && CoreSaveStore.Current.RevivesUsed(unit) == 3
                    && storeHost.AllMessages.Any(message => message.Contains("source=archive")),
                "campaign file: a save without block takes the campaign's own file over");
            CoreSaveStore.BindLoaded(Guid.NewGuid(), OtherPath);
            Check(CoreSaveStore.Current.Units().Count == 0, "campaign file: another campaign never reads it");
            Check(CoreSaveStore.ForgetArchive(archived) && !File.Exists(CoreSaveStore.ArchivePath(archived)),
                "campaign file: test cleanup removes it");
        }
        finally
        {
            if (Directory.Exists(archive)) Directory.Delete(archive, true);
        }
    }

    static void WoundBufferData()
    {
        var woundCampaign = Guid.NewGuid();
        var woundUnit = Guid.NewGuid();
        var woundData = CoreSaveData.Create(woundCampaign);
        woundData.SetWoundBuffer(woundUnit, 150);
        var woundBack = CoreSaveData.Parse(woundData.Serialize(), woundCampaign);
        var keptBuffer = woundBack.WoundBuffer(woundUnit);
        var listed = woundBack.WoundedUnits().Contains(woundUnit);
        woundBack.SetWoundBuffer(woundUnit, 0);
        Check(keptBuffer == 150 && listed && woundBack.WoundBuffer(woundUnit) == 0 && !woundBack.WoundedUnits().Contains(woundUnit)
                && !woundBack.Serialize().Contains("\"wound\""),
            "save data: the wound's buffer lives in Core's own field and goes when the wound closes");
    }

    static void OwnDataKept()
    {
        // Core's save data keeps what the game's part must not hold: own units, skills, Lifebloom's charges,
        // first aid research and tasks.
        var campaign = Guid.NewGuid();
        var unit = Guid.NewGuid();
        var data = CoreSaveData.Create(campaign);
        data.SetOwnedUnits(new Dictionary<Guid, string> { [unit] = CoreUnitRules.Prefix + "ForestBanditGuard" });
        data.SetOwnedSkills(new[] { new CoreSaveSkills.Entry { Unit = unit, Key = LifebloomRules.SkillKey, Active = true, Extra = true, QuickSlot = 2, Cooldown = "4.5" } });
        data.SetLifebloomCharges(new Dictionary<Guid, LifebloomRules.Charges> { [unit] = new LifebloomRules.Charges(2, 33.5, 12) });
        data.SetResearch(new[] { new CoreSaveResearch.Entry(BandageText.FirstAidResearchKey, "{\"Progress\":50}", 1) });
        data.SetTasks(new[] { new CoreSaveTasks.Entry(0, 3, "{\"RecipeKey\":\"RECIPE_DelversCoreHerbalBandage\"}") });
        var back = CoreSaveData.Parse(data.Serialize(), campaign);
        var skill = back.OwnedSkills().Single();
        var charges = back.LifebloomCharges()[unit];
        var research = back.Research().Single();
        var task = back.Tasks().Single();
        back.SetOwnedUnits(null);
        back.SetOwnedSkills(null);
        back.SetLifebloomCharges(null);
        back.SetResearch(null);
        back.SetTasks(null);
        var empty = back.Serialize();
        Check(skill.Unit == unit && skill.Active && skill.Extra && skill.QuickSlot == 2 && skill.Cooldown == "4.5" && skill.AutoUse == null
                && charges.Count == 2 && charges.NextAt == 33.5 && charges.Cooldown == 12
                && research.Key == BandageText.FirstAidResearchKey && research.Body == "{\"Progress\":50}" && research.QueueIndex == 1
                && task.Container == 0 && task.Index == 3 && task.Text.Contains("RECIPE_DelversCoreHerbalBandage")
                && !empty.Contains("ownUnits") && !empty.Contains("ownSkills") && !empty.Contains("lifebloom") && !empty.Contains("research")
                && !empty.Contains("tasks"),
            "save data: own units, skills, Lifebloom charges, research and tasks survive a round trip and go when empty");
        var foreign = CoreSaveData.Create(campaign);
        foreign.SetOwnedUnits(new Dictionary<Guid, string> { [unit] = "UNIT_GoblinBrawler" });
        foreign.SetResearch(new[] { new CoreSaveResearch.Entry("RESEARCH_BasicAlchemy", null, -1) });
        foreign.SetTasks(new[] { new CoreSaveTasks.Entry(0, 0, "{\"RecipeKey\":\"RECIPE_Textile\"}") });
        var foreignBack = CoreSaveData.Parse(foreign.Serialize(), campaign);
        Check(foreignBack.OwnedUnits().Count == 0 && foreignBack.Research().Count == 0 && foreignBack.Tasks().Count == 0,
            "save data: only Core's own keys come back from Core's block");
    }

    static void GamePartStaysClean()
    {
        // The game's part of a save never names Core's units, research, tasks or skills; reading puts them back.
        // Cutting again leaves a space where a comma went; the JSON is the same, so compare without whitespace.
        static string Squash(string text) => new string(text.Where(c => !char.IsWhiteSpace(c)).ToArray());
        {
            var unit = Guid.NewGuid().ToString("D");
            var json = "{\"Units\":[{\"Guid\":\"" + unit + "\",\"Key\":\"" + CoreUnitRules.Prefix + "ForestBanditArcher\",\"X\":1},"
                + "{\"Guid\":\"" + Guid.NewGuid().ToString("D") + "\",\"Key\":\"UNIT_GoblinBrawler\"}]}";
            var owned = new Dictionary<Guid, string>();
            var native = CoreSaveUnits.ToNative(json, owned);
            var restored = CoreSaveUnits.FromNative(native, owned, out var count);
            Check(!native.Contains(CoreUnitRules.Prefix) && owned.Count == 1 && restored == json && count == 1
                    && ReferenceEquals(CoreSaveUnits.ToNative(native, new Dictionary<Guid, string>()), native),
                "save units: Core's bandits are written as the goblin brawler and come back by GUID; a real brawler stays");
        }
        {
            var json = "{\"ResearchDatas\":{\"RESEARCH_BasicAlchemy\":{\"Progress\":200},\"RESEARCH_DelversCoreFirstAid\":{\"Progress\":50}},"
                + "\"ResearchKeyQueue\":[\"RESEARCH_A\",\"RESEARCH_DelversCoreFirstAid\",\"RESEARCH_B\"]}";
            var cut = new List<CoreSaveResearch.Entry>();
            var native = CoreSaveResearch.ToNative(json, cut);
            var restored = CoreSaveResearch.FromNative(native, cut, out var count);
            Check(!native.Contains("DelversCore") && cut.Count == 1 && cut[0].QueueIndex == 1 && cut[0].Body == "{\"Progress\":50}"
                    && count == 1 && System.Text.Json.JsonDocument.Parse(native) != null
                    && Squash(CoreSaveResearch.ToNative(restored, new List<CoreSaveResearch.Entry>())) == Squash(native)
                    && System.Text.Json.JsonDocument.Parse(restored).RootElement.GetProperty("ResearchKeyQueue")[1].GetString() == "RESEARCH_DelversCoreFirstAid",
                "save research: First Aid leaves the game's research data and queue and comes back at its place");
        }
        {
            var json = "{\"A\":{\"AllTasks\":[{\"RecipeKey\":\"RECIPE_Textile\",\"Id\":1},{\"RecipeKey\":\"RECIPE_DelversCoreHerbalBandage\",\"Id\":2}]},"
                + "\"B\":{\"AllTasks\":[{\"RecipeKey\":\"RECIPE_DelversCoreSalveBandage\",\"Id\":3}]}}";
            var cut = new List<CoreSaveTasks.Entry>();
            var native = CoreSaveTasks.ToNative(json, cut);
            var restored = CoreSaveTasks.FromNative(native, cut, out var count);
            Check(!native.Contains("DelversCore") && cut.Count == 2 && cut[0].Container == 0 && cut[0].Index == 1 && cut[1].Container == 1
                    && count == 2 && System.Text.Json.JsonDocument.Parse(native) != null
                    && Squash(CoreSaveTasks.ToNative(restored, new List<CoreSaveTasks.Entry>())) == Squash(native)
                    && System.Text.Json.JsonDocument.Parse(restored).RootElement.GetProperty("A").GetProperty("AllTasks")[1].GetProperty("Id").GetInt32() == 2,
                "save tasks: work orders for Core's recipes leave the task lists and come back at their place");
        }
        {
            var unit = Guid.NewGuid().ToString("D");
            var json = "{\"Components\":{\"" + unit + "\":[{\"Type\":5,\"$type\":\"Refactor.SkillComponentSaveData, Assembly-CSharp\","
                + "\"ActiveSkills\":[\"SKILL_A\",\"SKILL_DelversLifebloom\"],\"Cooldowns\":{\"SKILL_A\":1.5,\"SKILL_DelversLifebloom\":4.25},"
                + "\"SkillDurations\":{},\"AutoUse\":{\"SKILL_DelversLifebloom\":true}}]},"
                + "\"Trees\":{\"" + unit + "\":{\"SkillTrees\":{\"A\":1},\"ExtraSkills\":[\"SKILL_X\",\"SKILL_DelversLifebloom\"]}},"
                + "\"Slots\":{\"" + unit + "\":{\"QuickSlots\":[\"SKILL_A\",\"SKILL_DelversLifebloom\",\"\"]}}}";
            var cut = new List<CoreSaveSkills.Entry>();
            var native = CoreSaveSkills.ToNative(json, cut);
            var restored = CoreSaveSkills.FromNative(native, cut, out var units);
            var entry = cut.Count == 1 ? cut[0] : null;
            var root = System.Text.Json.JsonDocument.Parse(restored).RootElement;
            Check(!native.Contains("DelversLifebloom") && entry != null && entry.Active && entry.Extra && entry.QuickSlot == 1
                    && entry.Cooldown == "4.25" && entry.AutoUse == "true" && units == 1 && System.Text.Json.JsonDocument.Parse(native) != null
                    && root.GetProperty("Slots").GetProperty(unit).GetProperty("QuickSlots")[1].GetString() == LifebloomRules.SkillKey
                    && root.GetProperty("Trees").GetProperty(unit).GetProperty("ExtraSkills")[1].GetString() == LifebloomRules.SkillKey
                    && Squash(restored).Contains("\"SKILL_DelversLifebloom\":4.25") && Squash(CoreSaveSkills.ToNative(restored, new List<CoreSaveSkills.Entry>())) == Squash(native),
                "save skills: Lifebloom leaves the skill component, the tree and the quick slots and comes back in all of them");
        }
    }
}
