using System.Text;
using System.Text.Json;
using DungeonSettlersDelvers.Core;
using static TestKit;

// The shape of a campaign save as a fixture (Tests/Core/Fixtures/save-structure.json): indented text, "$type" in the
// component data, several units, storage policies, quick slots with an item quick slot, a mod's block and Core's block
// at the end. Core works on the save as text, so its rules are read and written against this shape, not against
// hand-made one-line JSON.
internal static class SaveStructureSection
{
    const string FixtureFile = "save-structure.json";
    const string HotbarProperty = "DungeonSettlers10Slots_Items";
    const string HerbalStandIn = "ITEM_Textile";
    const string LegacyStandIn = "ITEM_ThickTextile";
    // The unit that carries this skill in the fixture's skill component, skill tree and quick slots.
    const string BorrowedSkill = "SKILL_StrangeSeed";

    static readonly string SavePath = Path.Combine(Path.GetTempPath(), "delvers-structure-test", "Saves", "Clan_AutoSave_1.json");

    internal static void Run()
    {
        var host = new TestDelversHost("save-structure");
        DelversHost.Bind(host);
        try
        {
            var fixture = ReadFixture();
            Check(fixture != null, "save structure: the fixture stands in Tests/Core/Fixtures next to Core's sources");
            if (fixture == null) return;
            FixtureShape(fixture);
            CoreBlockContent(fixture);
            GamePartHoldsNoCoreKey(fixture);
            foreach (var (form, text) in LineBreakForms(fixture)) RoundTrip(form, text);
            OwnSkillsInTheirPlaces(fixture);
            KeyRenames(fixture);
        }
        finally
        {
            CoreSaveItems.Clear();
            CoreSaveStore.ResetLifecycle();
            DelversHost.Unbind(host);
        }
    }

    // The fixture with plain line breaks, whatever a checkout did to them; null when the file is not found.
    static string ReadFixture()
    {
        var core = FindCoreSourceDirectory();
        if (core == null) return null;
        var path = Path.Combine(Path.GetDirectoryName(core)!, "Tests", "Core", "Fixtures", FixtureFile);
        return File.Exists(path) ? File.ReadAllText(path).Replace("\r\n", "\n") : null;
    }

    // The two forms a save text has: plain line breaks, and the game's own (its part with CRLF, the mods' blocks after it
    // as they write them).
    static (string Form, string Text)[] LineBreakForms(string fixture)
    {
        var cut = fixture.IndexOf("\n,\"" + HotbarProperty + "\"", StringComparison.Ordinal);
        Check(cut > 0, "save structure: the mods' blocks follow the game's part");
        var game = fixture.Substring(0, cut).Replace("\n", "\r\n") + "\r" + fixture.Substring(cut);
        return new[] { ("LF", fixture), ("CRLF", game) };
    }

    static void FixtureShape(string text)
    {
        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        var names = root.EnumerateObject().Select(property => property.Name).ToList();
        Check(text.Count(c => c == '\n') > 1000 && text.Contains("\n  \"EntityContainerSaveData\": {\n    \"UnitContainerSaveData\": {\n")
                && text.Contains("\n            \"$type\": \"Refactor."),
            "save structure: the fixture is indented text with \"$type\" in the component data");
        Check(names.Count > 20 && names[^1] == CoreSaveBlock.PropertyName && names[^2] == HotbarProperty,
            "save structure: the mod's block and Core's block are the last two properties");
        var components = root.GetProperty("EntityComponentSaveData").GetProperty("EntityComponents");
        var players = root.GetProperty("EntityContainerSaveData").GetProperty("UnitContainerSaveData").GetProperty("Base").GetProperty("PlayerGuidList");
        var typed = 0;
        var all = 0;
        foreach (var unit in components.EnumerateObject())
            foreach (var component in unit.Value.EnumerateArray())
            {
                all++;
                var data = component.GetProperty("Data");
                if (data.EnumerateObject().First().Name == "$type" && data.GetProperty("$type").GetString().StartsWith("Refactor.")) typed++;
            }
        Check(players.GetArrayLength() == 4 && components.EnumerateObject().Count() == 4 && all > 0 && typed == all,
            "save structure: several units, every component's data starts with its \"$type\"");
        var policies = root.GetProperty("StoragePolicySaveData").GetProperty("StoragePolicies");
        var available = policies.EnumerateObject().First().Value.GetProperty("AvailableItems");
        var perKey = available.EnumerateObject().GroupBy(property => property.Name).ToList();
        Check(policies.EnumerateObject().Any() && available.EnumerateObject().Count() > 10 && perKey.Any(group => group.Count() > 1)
                && perKey.Single(group => group.Key == HerbalStandIn).Count() == 4,
            "save structure: storage policies keep a key twice in AvailableItems, as the game writes them");
        var slots = root.GetProperty("PlayerUnitsSaveData").GetProperty("QuickSlots").EnumerateObject()
            .Select(unit => unit.Value.GetProperty("ItemQuickSlotKey")).ToList();
        Check(slots.Count == 4 && slots.Any(key => key.ValueKind == JsonValueKind.String) && slots.Any(key => key.ValueKind == JsonValueKind.Null),
            "save structure: quick slots with an item quick slot set for some units and null for others");
    }

    static void CoreBlockContent(string text)
    {
        var block = CoreSaveBlock.Extract(text);
        var campaign = CoreSaveBlock.CampaignOf(text);
        var data = block == null ? null : CoreSaveData.Parse(block, campaign);
        Check(block != null && CoreSaveBlock.IsObject(block) && campaign != Guid.Empty && data != null
                && CoreSaveBlock.BlockApplies(data.Campaign, campaign),
            "save structure: Core's block is the last property and belongs to the save's campaign");
        var owned = data?.OwnedItems();
        var research = data?.Research();
        Check(data != null && data.Units().Count == 4 && owned.Count == 2 && owned.Values.All(key => key == BandageText.HerbalItemKey)
                && research.Count == 1 && research[0].Key == BandageText.FirstAidResearchKey && research[0].QueueIndex == -1,
            "save structure: Core's block holds the snapshots of four units, two own bandages and the First Aid research");
    }

    static void GamePartHoldsNoCoreKey(string text)
    {
        // The game's part of a save written by Core names none of Core's keys; with nothing to cut, every cutting rule gives the text back.
        var game = CoreSaveBlock.Remove(text);
        var effects = CoreSaveEffects.Strip(game, out var removed);
        Check(!game.Contains("DelversCore") && !game.Contains(LifebloomRules.SkillKey) && game.Contains("\"" + HotbarProperty + "\""),
            "save structure: the game's part names no Core key, the mod's block stays in it");
        Check(ReferenceEquals(effects, game) && removed == 0
                && ReferenceEquals(CoreSaveItems.ToNative(game, new Dictionary<Guid, string>(), new CoreSaveItems.Places()), game)
                && ReferenceEquals(CoreSaveUnits.ToNative(game, new Dictionary<Guid, string>()), game)
                && ReferenceEquals(CoreSaveResearch.ToNative(game, new List<CoreSaveResearch.Entry>()), game)
                && ReferenceEquals(CoreSaveTasks.ToNative(game, new List<CoreSaveTasks.Entry>()), game)
                && ReferenceEquals(CoreSaveSkills.ToNative(game, new List<CoreSaveSkills.Entry>()), game),
            "save structure: with no key of Core's in the game's part, no cutting rule changes the text");
    }

    // Reading, writing and reading again: Core's block comes out byte for byte; the game's part keeps its JSON. The text is
    // compared without whitespace: restoring the First Aid research and cutting it again leaves a space in the table's line.
    static void RoundTrip(string form, string text)
    {
        var block = CoreSaveBlock.Extract(text);
        var campaign = CoreSaveBlock.CampaignOf(text);
        var other = Path.Combine(Path.GetDirectoryName(SavePath)!, "Clan_AutoSave_2.json");
        CoreSaveItems.Clear();
        CoreSaveItems.Register(BandageText.HerbalItemKey, HerbalStandIn);
        CoreSaveItems.RegisterLegacy(BandageText.HerbalItemKey, LegacyStandIn);

        CoreSaveStore.ResetLifecycle();
        var read = CoreSaveStore.Remember(text, SavePath);
        CoreSaveStore.BindLoaded(campaign, SavePath);
        Check(CoreSaveStore.Current.Serialize() == block,
            "save structure (" + form + "): reading binds Core's block, and it serializes back to the same text");
        Check(read != text && JsonDocument.Parse(read) != null && read.Contains("\"Key\": \"" + BandageText.HerbalItemKey + "\"")
                && read.Contains("\"" + BandageText.FirstAidResearchKey + "\""),
            "save structure (" + form + "): reading puts Core's bandages and its research back into the game's part");

        var written = Write(read, SavePath);
        Check(CoreSaveBlock.Extract(written) == block,
            "save structure (" + form + "): writing gives Core's block back byte for byte");
        Check(Squash(written) == Squash(text) && JsonDocument.Parse(written) != null,
            "save structure (" + form + "): reading and writing give back the same JSON, duplicates and the mod's block included");

        CoreSaveStore.ResetLifecycle();
        var readAgain = CoreSaveStore.Remember(written, other);
        CoreSaveStore.BindLoaded(campaign, other);
        var writtenAgain = Write(readAgain, other);
        Check(CoreSaveStore.Current.Serialize() == block && CoreSaveBlock.Extract(writtenAgain) == block && Squash(writtenAgain) == Squash(text),
            "save structure (" + form + "): another cycle gives the same block and the same JSON");

        // The game's rewrite of a file (no campaign save running) keeps the file's block and puts this text's lists into it.
        CoreSaveStore.ResetLifecycle();
        var rewriteRead = CoreSaveStore.Remember(text, SavePath);
        var rewritten = CoreSaveStore.Decorate(SavePath + ".tmp", rewriteRead);
        Check(CoreSaveBlock.Extract(rewritten) == block && Squash(rewritten) == Squash(text),
            "save structure (" + form + "): a rewrite of the file keeps its block and its JSON");
    }

    // What the game's own campaign save writes for the text it serialized.
    static string Write(string text, string path)
    {
        CoreSaveStore.SaveStarted();
        try { return CoreSaveStore.Decorate(path + ".tmp", text); }
        finally { CoreSaveStore.SaveEnded(); }
    }

    static string Squash(string text) => new string(text.Where(c => !char.IsWhiteSpace(c)).ToArray());

    // A skill of Core's in the places the fixture shows (skill component, skill tree, quick slots): cut out, put back.
    static void OwnSkillsInTheirPlaces(string fixture)
    {
        var anchors = fixture.Split("\"" + BorrowedSkill + "\"").Length - 1;
        var own = fixture.Replace(BorrowedSkill, LifebloomRules.SkillKey);
        var kept = new List<CoreSaveSkills.Entry>();
        var cut = CoreSaveSkills.ToNative(own, kept);
        var back = CoreSaveSkills.FromNative(cut, kept, out var units);
        var entry = kept.Count == 1 ? kept[0] : null;
        Check(anchors == 4 && entry != null && entry.Active && entry.Extra && entry.QuickSlot == 2 && entry.AutoUse == "true"
                && !cut.Contains(LifebloomRules.SkillKey) && JsonDocument.Parse(cut) != null && units == 1,
            "save structure: Lifebloom leaves the skill component, the tree and the quick slots of the indented text");
        Check(SkillPlaces(back) == SkillPlaces(own) && SkillPlaces(own).Contains(LifebloomRules.SkillKey) && SkillPlaces(cut) != SkillPlaces(own),
            "save structure: Lifebloom comes back into all three places, in the same slot (the order in a list may change)");
    }

    // The first unit's skills as the game's three places hold them: component lists and AutoUse sorted, quick slots in order.
    static string SkillPlaces(string text)
    {
        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        var unit = root.GetProperty("EntityContainerSaveData").GetProperty("UnitContainerSaveData").GetProperty("Base")
            .GetProperty("PlayerGuidList")[0].GetString();
        var skill = root.GetProperty("EntityComponentSaveData").GetProperty("EntityComponents").GetProperty(unit).EnumerateArray()
            .Select(component => component.GetProperty("Data")).Single(data => data.GetProperty("$type").GetString().Contains("SkillComponent"));
        var player = root.GetProperty("PlayerUnitsSaveData");
        var places = new List<string>
        {
            string.Join(",", skill.GetProperty("ActiveSkills").EnumerateArray().Select(item => item.GetString()).OrderBy(item => item, StringComparer.Ordinal)),
            string.Join(",", skill.GetProperty("AutoUse").EnumerateObject().Select(item => item.Name + "=" + item.Value).OrderBy(item => item, StringComparer.Ordinal)),
            string.Join(",", player.GetProperty("SkillTrees").GetProperty(unit).GetProperty("ExtraSkills").EnumerateArray()
                .Select(item => item.GetString()).OrderBy(item => item, StringComparer.Ordinal)),
            string.Join(",", player.GetProperty("QuickSlots").GetProperty(unit).GetProperty("QuickSlots").EnumerateArray().Select(item => item.ToString()))
        };
        return string.Join("|", places);
    }

    // A pack's rename must reach every key of the fixture, in the indented text and in the compact form alike.
    static void KeyRenames(string fixture)
    {
        var renames = new Dictionary<string, string>
        {
            [HerbalStandIn] = "ITEM_Linen",
            ["AFFECTER_Human"] = "AFFECTER_Person"
        };
        var registry = new SaveKeyRenameRegistry();
        using var lease = registry.Register("fixture-pack", renames);
        var compact = Compact(fixture);
        var before = renames.Keys.ToDictionary(key => key, key => TokenCount(fixture, key));
        var renamed = Rename(registry, fixture, out var tokens);
        var renamedCompact = Rename(registry, compact, out var compactTokens);
        var restored = renamed;
        foreach (var pair in renames) restored = restored.Replace("\"" + pair.Value + "\"", "\"" + pair.Key + "\"");
        Check(before.Values.All(count => count > 0) && tokens == before.Values.Sum() && compactTokens == tokens
                && renames.All(pair => TokenCount(renamed, pair.Key) == 0 && TokenCount(renamed, pair.Value) == before[pair.Key]
                    && TokenCount(renamedCompact, pair.Key) == 0 && TokenCount(renamedCompact, pair.Value) == before[pair.Key]),
            "save structure: a rename reaches every property name and value of the indented text, as in its compact form");
        Check(restored == fixture && CoreSaveBlock.Extract(renamed) == CoreSaveBlock.Extract(fixture)
                && JsonDocument.Parse(renamed) != null,
            "save structure: a rename changes nothing but the renamed strings; Core's block, the duplicates and the layout stay");
    }

    static string Rename(SaveKeyRenameRegistry registry, string text, out int tokens)
        => registry.Apply(text, out tokens, out _);

    // The same JSON without indentation (duplicate properties stay).
    static string Compact(string text)
    {
        using var document = JsonDocument.Parse(text);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) document.RootElement.WriteTo(writer);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    // How many JSON tokens, property names and string values, are exactly this key.
    static int TokenCount(string json, string key)
    {
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));
        var count = 0;
        while (reader.Read())
            if ((reader.TokenType == JsonTokenType.PropertyName || reader.TokenType == JsonTokenType.String) && reader.GetString() == key) count++;
        return count;
    }
}
