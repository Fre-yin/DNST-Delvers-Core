using System.Text;
using System.Text.Json;
using DungeonSettlersDelvers.Core;
using static TestKit;

// An own item key outside the item records (CoreSaveItems.Places): a unit's item quick slot, the entries of a storage
// rule's item list and Extended Hotbar's two extra item slots. Writing cuts them out of the game's part (no stand-in, so no second entry of a native key), Core's block
// notes them, reading puts them back where they stood. A real native item in the same places stays what it is.
internal static class ItemPlacesSection
{
    const string OwnKey = BandageText.HerbalItemKey;
    const string StandIn = "ITEM_Textile";
    const string LegacyStandIn = "ITEM_ThickTextile";
    const string FixtureFile = "save-structure.json";
    const string Hotbar = "DungeonSettlers10Slots_Items";
    const string FirstUnit = "00000000-0000-4000-8000-000000000001";
    const string ThirdUnit = "00000000-0000-4000-8000-000000000003";
    const string Rule = "00000000-0000-4000-8000-000000000035";
    const string SmallUnitA = "00000000-0000-4000-8000-0000000000a1";
    const string SmallUnitB = "00000000-0000-4000-8000-0000000000a2";
    const string SmallRule = "00000000-0000-4000-8000-0000000000b1";

    static readonly string SavePath = Path.Combine(Path.GetTempPath(), "delvers-places-test", "Saves", "Clan_AutoSave_1.json");

    internal static void Run()
    {
        var host = new TestDelversHost("item-places");
        DelversHost.Bind(host);
        try
        {
            Register();
            SmallText();
            CutAndRestoreKeepEverythingElse();
            GuardsOnRestore();
            BlockFields();
            HotbarSlots();
            UnknownShape();
            var fixture = ReadFixture();
            Check(fixture != null, "item places: the fixture stands in Tests/Core/Fixtures next to Core's sources");
            if (fixture == null) return;
            FixtureText(fixture);
            FixtureThroughTheStore(fixture);
        }
        finally
        {
            CoreSaveItems.Clear();
            CoreSaveStore.ResetLifecycle();
            DelversHost.Unbind(host);
        }
    }

    static void Register()
    {
        CoreSaveItems.Clear();
        CoreSaveItems.Register(OwnKey, StandIn);
        CoreSaveItems.RegisterLegacy(OwnKey, LegacyStandIn);
    }

    // A save text with its three kinds of place: two units' item quick slots, one rule's item list, and the hotbar mod's block.
    static string Small(string slotA, string slotB, string items, string hotbar = StandIn)
        => "{\n  \"PlayerUnitsSaveData\": {\n    \"QuickSlots\": {\n"
            + "      \"" + SmallUnitA + "\": {\n        \"QuickSlots\": [\n          \"SKILL_A\",\n          \"\"\n        ],\n        \"ItemQuickSlotKey\": " + slotA + "\n      },\n"
            + "      \"" + SmallUnitB + "\": {\n        \"QuickSlots\": [],\n        \"ItemQuickSlotKey\": " + slotB + "\n      }\n    }\n  },\n"
            + "  \"StoragePolicySaveData\": {\n    \"StoragePolicies\": {\n      \"" + SmallRule + "\": {\n        \"MinDurability\": 0,\n"
            + "        \"AvailableItems\": {" + items + "}\n      }\n    }\n  },\n"
            + "  \"" + Hotbar + "\": {\"Version\": 1, \"Units\": {\"" + SmallUnitA + "\": [\"\", \"" + hotbar + "\"]}}\n}";

    static string List(params string[] entries)
        => "\n          " + string.Join(",\n          ", entries) + "\n        ";

    static string Q(string key) => "\"" + key + "\"";

    // A form the shape rules do not know (here an object before the quick slot) falls back to the
    // stand-in, as before, and is counted, so the save writes a warning instead of losing the places silently.
    static void UnknownShape()
    {
        const string unit = "00000000-0000-4000-8000-0000000000a1";
        const string rule = "00000000-0000-4000-8000-0000000000a2";
        var json = "{\"" + unit + "\": {\"Extra\": {\"x\": 1}, \"ItemQuickSlotKey\": " + Q(OwnKey) + "}, "
            + "\"" + rule + "\": {\"AvailableItems\": {\"ITEM_Wood\": true, " + Q(OwnKey) + ": true}}}";
        var places = new CoreSaveItems.Places();
        var native = CoreSaveItems.ToNative(json, new Dictionary<Guid, string>(), places);
        Check(!native.Contains(OwnKey) && places.QuickSlots.Count == 0 && places.Storage.Count == 1 && places.Missed == 1,
            "item places: a quick slot behind a nested object is not caught, falls back to its stand-in and is counted as missed");
        var known = new CoreSaveItems.Places();
        CoreSaveItems.ToNative("{\"" + unit + "\": {\"QuickSlots\": [], \"ItemQuickSlotKey\": " + Q(OwnKey) + "}}", new Dictionary<Guid, string>(), known);
        Check(known.Missed == 0 && known.QuickSlots.Count == 1, "item places: the game's own form leaves nothing missed");
    }

    static void SmallText()
    {
        var items = List("\"ITEM_WildHerb\": true", Q(OwnKey) + ": false", "\"" + StandIn + "\": true", "\"ITEM_Ointment\": true");
        var text = Small(Q(OwnKey), Q(StandIn), items, OwnKey);
        var places = new CoreSaveItems.Places();
        var native = CoreSaveItems.ToNative(text, new Dictionary<Guid, string>(), places);
        var slotUnit = Guid.Parse(SmallUnitA);
        var storage = places.Storage.TryGetValue(Guid.Parse(SmallRule), out var cut) ? cut : null;
        Check(JsonDocument.Parse(native) != null && DuplicateKeys(native) == 0,
            "item places: after writing the text is valid JSON and has no key twice");
        Check(places.QuickSlots.Count == 1 && places.QuickSlots[slotUnit] == OwnKey
                && native.Contains("\"" + SmallUnitA + "\": {\n        \"QuickSlots\": [\n          \"SKILL_A\",\n          \"\"\n        ],\n        \"ItemQuickSlotKey\": null\n"),
            "item places: an own item quick slot is written empty and noted for its unit");
        Check(native.Contains(Q(SmallUnitB).Trim() + ": {\n        \"QuickSlots\": [],\n        \"ItemQuickSlotKey\": " + Q(StandIn) + "\n")
                && !places.QuickSlots.ContainsKey(Guid.Parse(SmallUnitB)),
            "item places: a real native item on the quick slot of another unit stays cloth and is not noted");
        Check(storage != null && storage.Count == 1 && storage[0].Key == OwnKey && !storage[0].On && storage[0].Index == 1
                && native.Contains("\"ITEM_WildHerb\": true,\n          \"" + StandIn + "\": true,\n          \"ITEM_Ointment\": true\n        }"),
            "item places: an own entry is cut out of the item list with its switch and its place; the native entries stay");
        Check(!native.Contains(OwnKey) && native.Contains("\"" + Hotbar + "\": {\"Version\": 1, \"Units\": {\"" + SmallUnitA + "\": [\"\", \"\"]}}")
                && places.HotbarSlots.TryGetValue(slotUnit, out var hotbar) && hotbar[0] == null && hotbar[1] == OwnKey && places.Missed == 0,
            "item places: an own key on the hotbar's second extra slot is written empty and noted at its slot");

        var back = CoreSaveItems.FromNative(native, new Dictionary<Guid, string>(), places, out var restored);
        Check(restored == 3 && back == text,
            "item places: reading puts the quick slot, the list entry and the hotbar slot back exactly where they stood");
    }

    static void CutAndRestoreKeepEverythingElse()
    {
        // Several own entries, an own key twice, a list that is cut empty, a compact list.
        var other = "ITEM_DelversCoreLightBandage";
        CoreSaveItems.Register(other, "ITEM_Ointment");
        try
        {
            var items = List(Q(OwnKey) + ": true", "\"ITEM_WildHerb\": true", Q(other) + ": false", Q(OwnKey) + ": true", "\"" + StandIn + "\": true");
            var text = Small("null", "null", items);
            var places = new CoreSaveItems.Places();
            var native = CoreSaveItems.ToNative(text, new Dictionary<Guid, string>(), places);
            var entries = places.Storage[Guid.Parse(SmallRule)];
            Check(DuplicateKeys(native) == 0 && !native.Contains("DelversCore") && entries.Count == 2
                    && entries[0].Key == OwnKey && entries[0].Index == 0 && entries[1].Key == other && entries[1].Index == 2 && !entries[1].On,
                "item places: each own key is noted once at its first place; a second entry of the same key is cut with it");
            var back = CoreSaveItems.FromNative(native, new Dictionary<Guid, string>(), places, out var restored);
            Check(restored == 2 && back == Small("null", "null", List(Q(OwnKey) + ": true", "\"ITEM_WildHerb\": true", Q(other) + ": false", "\"" + StandIn + "\": true")),
                "item places: reading puts every noted entry back at its place; the doubled own entry comes back once");

            var only = Small("null", "null", List(Q(OwnKey) + ": true"));
            var emptied = CoreSaveItems.ToNative(only, new Dictionary<Guid, string>(), places = new CoreSaveItems.Places());
            Check(emptied.Contains("\"AvailableItems\": {\n        }") && JsonDocument.Parse(emptied) != null
                    && CoreSaveItems.FromNative(emptied, new Dictionary<Guid, string>(), places, out _) == only,
                "item places: a list cut empty stays valid, and the entry comes back with the list's layout");

            var compact = "{\"s\": {\"" + SmallRule + "\": {\"AvailableItems\": {\"ITEM_A\":true,\"" + OwnKey + "\":false,\"ITEM_B\":true}}}}";
            var compactNative = CoreSaveItems.ToNative(compact, new Dictionary<Guid, string>(), places = new CoreSaveItems.Places());
            Check(compactNative == "{\"s\": {\"" + SmallRule + "\": {\"AvailableItems\": {\"ITEM_A\":true,\"ITEM_B\":true}}}}"
                    && CoreSaveItems.FromNative(compactNative, new Dictionary<Guid, string>(), places, out _).Replace(" ", "") == compact.Replace(" ", ""),
                "item places: a compact list is cut and filled without changing its other entries");

            var unchanged = Small("\"ITEM_AidKit\"", "null", List("\"ITEM_WildHerb\": true", "\"" + StandIn + "\": true"));
            Check(ReferenceEquals(CoreSaveItems.ToNative(unchanged, new Dictionary<Guid, string>(), new CoreSaveItems.Places()), unchanged)
                    && ReferenceEquals(CoreSaveItems.FromNative(unchanged, new Dictionary<Guid, string>(), new CoreSaveItems.Places(), out var none), unchanged) && none == 0,
                "item places: a text without an own key comes back as the same instance, writing and reading");
        }
        finally
        {
            Register();
        }
    }

    static void GuardsOnRestore()
    {
        var list = List("\"ITEM_WildHerb\": true", "\"" + StandIn + "\": true");
        var places = new CoreSaveItems.Places();
        places.QuickSlots[Guid.Parse(SmallUnitA)] = OwnKey;
        places.QuickSlots[Guid.Parse(SmallUnitB)] = OwnKey;
        places.Storage[Guid.Parse(SmallRule)] = new List<CoreSaveItems.StorageEntry> { new(OwnKey, true, 9), new("ITEM_DelversCoreUnknown", true, 0) };
        var text = Small("\"ITEM_AidKit\"", "null", list);
        var back = CoreSaveItems.FromNative(text, new Dictionary<Guid, string>(), places, out var restored);
        Check(restored == 2 && back.Contains("\"ItemQuickSlotKey\": \"ITEM_AidKit\"") && back.Contains("\"ItemQuickSlotKey\": " + Q(OwnKey))
                && back.Contains("\"" + StandIn + "\": true,\n          " + Q(OwnKey) + ": true\n        }") && !back.Contains("Unknown"),
            "item places: a filled quick slot stays, a place past the end of the list is appended, a key Core no longer knows is not added");

        var present = Small("\"ITEM_AidKit\"", "\"ITEM_AidKit\"", List(Q(OwnKey) + ": false", "\"ITEM_WildHerb\": true"));
        Check(ReferenceEquals(CoreSaveItems.FromNative(present, new Dictionary<Guid, string>(), places, out var again), present) && again == 0,
            "item places: an entry the list holds already is not added a second time");

        CoreSaveItems.Clear();
        var withoutCore = CoreSaveItems.FromNative(text, new Dictionary<Guid, string>(), places, out var nothing);
        Check(ReferenceEquals(withoutCore, text) && nothing == 0 && DuplicateKeys(withoutCore) == 0,
            "item places: with no own item registered nothing is put back");
        Register();

        var strange = Small("null", "null", "\n          \"ITEM_WildHerb\": true; " + Q(OwnKey) + ": true\n        ");
        Check(ReferenceEquals(CoreSaveItems.ToNative(strange, new Dictionary<Guid, string>(), new CoreSaveItems.Places()), strange) == false
                && !CoreSaveItems.ToNative(strange, new Dictionary<Guid, string>(), new CoreSaveItems.Places()).Contains(OwnKey),
            "item places: a list the rule cannot read is left to the stand-in rule, so the game's part still holds no own key");
    }

    static void BlockFields()
    {
        // The names and the shape of the new block fields are fixed: a save already written carries them.
        var places = new CoreSaveItems.Places();
        places.QuickSlots[Guid.Parse(SmallUnitA)] = OwnKey;
        places.Storage[Guid.Parse(SmallRule)] = new List<CoreSaveItems.StorageEntry> { new(OwnKey, false, 4) };
        var campaign = Guid.Parse("00000000-0000-4000-8000-0000000000c1");
        var data = CoreSaveData.Create(campaign);
        data.SetItemPlaces(places);
        var expected = "{\"version\":1,\"campaign\":\"" + campaign + "\",\"itemSlots\":{\"" + SmallUnitA + "\":\"" + OwnKey + "\"},"
            + "\"storageItems\":{\"" + SmallRule + "\":[{\"key\":\"" + OwnKey + "\",\"on\":false,\"at\":4}]}}";
        Check(data.Serialize() == expected, "item places: Core's block holds itemSlots (unit to key) and storageItems (rule to key, on, at)");
        var read = CoreSaveData.ParseAny(data.Serialize()).ItemPlaces();
        Check(read.QuickSlots.Count == 1 && read.QuickSlots[Guid.Parse(SmallUnitA)] == OwnKey && read.Storage.Count == 1
                && read.Storage[Guid.Parse(SmallRule)].Count == 1 && read.Storage[Guid.Parse(SmallRule)][0].Index == 4 && !read.Storage[Guid.Parse(SmallRule)][0].On,
            "item places: the block's fields are read back into the same places");

        var damaged = CoreSaveData.ParseAny("{\"itemSlots\":{\"no-guid\":\"" + OwnKey + "\",\"" + SmallUnitA + "\":\"ITEM_Textile\",\"" + SmallUnitB + "\":5},"
            + "\"storageItems\":{\"" + SmallRule + "\":[{\"key\":\"ITEM_Textile\",\"on\":true,\"at\":1},{\"key\":\"" + OwnKey + "\",\"on\":true,\"at\":-1},"
            + "{\"key\":\"" + OwnKey + "\",\"on\":true},{\"key\":\"" + OwnKey + "\",\"on\":true,\"at\":2}],\"also-no-guid\":[]}}").ItemPlaces();
        Check(damaged.QuickSlots.Count == 0 && damaged.Storage.Count == 1 && damaged.Storage[Guid.Parse(SmallRule)].Count == 1
                && damaged.Storage[Guid.Parse(SmallRule)][0].Index == 2,
            "item places: a damaged field is skipped entry by entry; a key that is not Core's never reaches the game");

        var hotbarPlaces = new CoreSaveItems.Places();
        hotbarPlaces.HotbarSlots[Guid.Parse(SmallUnitA)] = new[] { null, OwnKey };
        var hotbarData = CoreSaveData.Create(campaign);
        hotbarData.SetItemPlaces(hotbarPlaces);
        Check(hotbarData.Serialize() == "{\"version\":1,\"campaign\":\"" + campaign + "\",\"hotbarSlots\":{\"" + SmallUnitA + "\":[\"\",\"" + OwnKey + "\"]}}",
            "item places: Core's block holds hotbarSlots (unit to its two extra slots, \"\" where none stood)");
        var hotbarRead = CoreSaveData.ParseAny(hotbarData.Serialize()).ItemPlaces();
        Check(hotbarRead.HotbarSlots.Count == 1 && hotbarRead.HotbarSlots[Guid.Parse(SmallUnitA)][0] == null
                && hotbarRead.HotbarSlots[Guid.Parse(SmallUnitA)][1] == OwnKey,
            "item places: hotbarSlots are read back into the same unit and slot");
        var hotbarDamaged = CoreSaveData.ParseAny("{\"hotbarSlots\":{\"no-guid\":[\"\",\"" + OwnKey + "\"],\"" + SmallUnitA + "\":[\"" + OwnKey + "\"],"
            + "\"" + SmallUnitB + "\":[\"ITEM_Textile\",5]}}").ItemPlaces();
        Check(!hotbarDamaged.Any, "item places: a damaged hotbarSlots entry, a list of the wrong length or a key not Core's is skipped");

        var kept = CoreSaveData.ParseAny("{\"version\":1,\"future\":{\"x\":1},\"itemSlots\":{\"" + SmallUnitA + "\":\"" + OwnKey + "\"}}");
        kept.SetItemPlaces(new CoreSaveItems.Places());
        Check(kept.Serialize() == "{\"version\":1,\"future\":{\"x\":1}}" && !kept.ItemPlaces().Any,
            "item places: no places remove the fields, and a field of a newer Core stays");
    }

    // Extended Hotbar's extension in the form its codec writes (compact, Version 1): both slots of one unit own, another unit's
    // native items untouched; reading fills only a slot that is empty again, and a form of a newer hotbar falls back as before.
    static void HotbarSlots()
    {
        var light = "ITEM_DelversCoreLightBandage";
        CoreSaveItems.Register(light, "ITEM_Ointment");
        try
        {
            string Text(string a, string b, string c, string d)
                => "{\"x\":1,\"" + Hotbar + "\":{\"Version\":1,\"Units\":{\"" + SmallUnitA + "\":[\"" + a + "\",\"" + b + "\"],\""
                    + SmallUnitB + "\":[\"" + c + "\",\"" + d + "\"]}}}";
            var text = Text(OwnKey, light, StandIn, "");
            var places = new CoreSaveItems.Places();
            var native = CoreSaveItems.ToNative(text, new Dictionary<Guid, string>(), places);
            Check(native == Text("", "", StandIn, "") && places.HotbarSlots.Count == 1 && places.Missed == 0
                    && places.HotbarSlots[Guid.Parse(SmallUnitA)][0] == OwnKey && places.HotbarSlots[Guid.Parse(SmallUnitA)][1] == light,
                "item places: both own hotbar slots are written empty and noted, a native item on the other unit's slot stays");
            Check(JsonDocument.Parse(native) != null, "item places: the hotbar extension stays valid JSON after the cut");
            var back = CoreSaveItems.FromNative(native, new Dictionary<Guid, string>(), places, out var restored);
            Check(restored == 2 && back == text, "item places: reading gives both hotbar slots their own keys back");

            var filled = CoreSaveItems.FromNative(Text("ITEM_Wood", "", StandIn, ""), new Dictionary<Guid, string>(), places, out var filledBack);
            Check(filledBack == 1 && filled == Text("ITEM_Wood", light, StandIn, ""),
                "item places: a hotbar slot the game filled with a native item meanwhile keeps it; only the empty one is filled");
            var withNull = CoreSaveItems.FromNative(native.Replace("[\"\",\"\"]", "[null,null]"), new Dictionary<Guid, string>(), places, out var nullBack);
            Check(nullBack == 2 && withNull == text,
                "item places: a hotbar slot written as null counts as empty too");

            var newer = "{\"" + Hotbar + "\":{\"Version\":2,\"Units\":{\"" + SmallUnitA + "\":[\"" + OwnKey + "\",\"\"]}}}";
            var newerPlaces = new CoreSaveItems.Places();
            var newerNative = CoreSaveItems.ToNative(newer, new Dictionary<Guid, string>(), newerPlaces);
            Check(!newerNative.Contains(OwnKey) && newerPlaces.HotbarSlots.Count == 0 && newerPlaces.Missed == 1,
                "item places: a hotbar extension of another form falls back to the stand-in and is counted as missed");
        }
        finally
        {
            Register();
        }
    }

    // The fixture with plain line breaks; null when the file is not found.
    static string ReadFixture()
    {
        var core = FindCoreSourceDirectory();
        if (core == null) return null;
        var path = Path.Combine(Path.GetDirectoryName(core)!, "Tests", "Core", "Fixtures", FixtureFile);
        return File.Exists(path) ? File.ReadAllText(path).Replace("\r\n", "\n") : null;
    }

    // The fixture's game part with an own key on the first unit's item quick slot (it held the first aid kit) and in the storage
    // rule's list (after the wild herb), and a real native cloth on the third unit's item quick slot (it held none).
    static string WithOwnKeys(string game)
    {
        var withSlot = ReplaceOnce(game, "\"ItemQuickSlotKey\": \"ITEM_AidKit\"", "\"ItemQuickSlotKey\": " + Q(OwnKey));
        var withCloth = ReplaceOnce(withSlot, "\"ItemQuickSlotKey\": null", "\"ItemQuickSlotKey\": " + Q(StandIn));
        return ReplaceOnce(withCloth, "\"ITEM_WildHerb\": true,\n", "\"ITEM_WildHerb\": true,\n          " + Q(OwnKey) + ": false,\n");
    }

    static string ReplaceOnce(string text, string from, string to)
    {
        var at = text.IndexOf(from, StringComparison.Ordinal);
        return at < 0 ? text : text.Substring(0, at) + to + text.Substring(at + from.Length);
    }

    static void FixtureText(string fixture)
    {
        var game = CoreSaveBlock.Remove(fixture);
        var own = WithOwnKeys(game);
        var places = new CoreSaveItems.Places();
        var native = CoreSaveItems.ToNative(own, new Dictionary<Guid, string>(), places);
        var expected = ReplaceOnce(ReplaceOnce(own, "\"ItemQuickSlotKey\": " + Q(OwnKey), "\"ItemQuickSlotKey\": null"), "          " + Q(OwnKey) + ": false,\n", "");
        var cloth = KeyCount(game, StandIn) + 1;
        Check(own != game && !native.Contains(CoreSaveItems.Prefix) && JsonDocument.Parse(native) != null
                && DuplicateKeys(native) == DuplicateKeys(game) && native == expected,
            "item places: in the fixture's text an own key on a quick slot and in a rule's list leaves no own key and no new key twice");
        Check(places.QuickSlots.Count == 1 && places.QuickSlots[Guid.Parse(FirstUnit)] == OwnKey && ThirdUnitSlot(native) == Q(StandIn)
                && places.Storage[Guid.Parse(Rule)].Count == 1 && places.Storage[Guid.Parse(Rule)][0].Index == 2
                && KeyCount(native, StandIn) == cloth,
            "item places: the real cloth on another unit's quick slot and the four cloth entries of the list stay cloth");
        var back = CoreSaveItems.FromNative(native, new Dictionary<Guid, string>(), places, out var restored);
        Check(restored == 2 && back == own && ThirdUnitSlot(back) == Q(StandIn) && KeyCount(back, StandIn) == cloth,
            "item places: in the fixture's text reading puts both own keys back exactly, and the native cloth stays cloth");
    }

    // The value of the third unit's ItemQuickSlotKey in the text (as written there).
    static string ThirdUnitSlot(string text)
    {
        var from = text.IndexOf("\"" + ThirdUnit + "\": {\n        \"QuickSlots\"", StringComparison.Ordinal);
        if (from < 0) return null;
        var key = text.IndexOf("\"ItemQuickSlotKey\": ", from, StringComparison.Ordinal) + "\"ItemQuickSlotKey\": ".Length;
        var end = text.IndexOf('\n', key);
        return text.Substring(key, end - key);
    }

    // Through the store: the game's own save writes the notes into Core's block, and the next read puts the keys back.
    static void FixtureThroughTheStore(string fixture)
    {
        var campaign = CoreSaveBlock.CampaignOf(fixture);
        var other = Path.Combine(Path.GetDirectoryName(SavePath)!, "Clan_AutoSave_2.json");
        Register();
        CoreSaveStore.ResetLifecycle();
        var read = CoreSaveStore.Remember(fixture, SavePath);
        CoreSaveStore.BindLoaded(campaign, SavePath);
        var edited = WithOwnKeys(read);
        var written = Write(edited, SavePath);
        var block = CoreSaveData.Parse(CoreSaveBlock.Extract(written), campaign);
        var places = block?.ItemPlaces();
        var gamePart = CoreSaveBlock.Remove(written);
        Check(block != null && places.QuickSlots.Count == 1 && places.QuickSlots[Guid.Parse(FirstUnit)] == OwnKey
                && places.Storage.Count == 1 && places.Storage[Guid.Parse(Rule)][0].Index == 2 && !places.Storage[Guid.Parse(Rule)][0].On
                && block.OwnedItems().Count == 2,
            "item places: the game's own save notes the quick slot and the list entry in Core's block, beside the two own bandages");
        Check(!gamePart.Contains("DelversCore") && JsonDocument.Parse(written) != null && DuplicateKeys(gamePart) == DuplicateKeys(CoreSaveBlock.Remove(fixture))
                && KeyCount(gamePart, StandIn) == KeyCount(CoreSaveBlock.Remove(fixture), StandIn) + 1,
            "item places: the written game part holds no own key and no new key twice (one cloth more: the other unit's real one)");

        CoreSaveStore.ResetLifecycle();
        var readAgain = CoreSaveStore.Remember(written, other);
        CoreSaveStore.BindLoaded(campaign, other);
        var writtenAgain = Write(readAgain, other);
        Check(Squash(CoreSaveBlock.Remove(readAgain)) == Squash(CoreSaveBlock.Remove(edited)) && CoreSaveBlock.Extract(writtenAgain) == CoreSaveBlock.Extract(written)
                && Squash(CoreSaveBlock.Remove(writtenAgain)) == Squash(gamePart),
            "item places: reading the written save gives the own keys back where they were, and another cycle changes nothing");

        // A rewrite of the file (the game renames save files without a campaign save) keeps the notes.
        CoreSaveStore.ResetLifecycle();
        var rewriteRead = CoreSaveStore.Remember(written, SavePath);
        var rewritten = CoreSaveStore.Decorate(SavePath + ".tmp", rewriteRead);
        Check(CoreSaveBlock.Extract(rewritten) == CoreSaveBlock.Extract(written) && Squash(CoreSaveBlock.Remove(rewritten)) == Squash(gamePart),
            "item places: a rewrite of the file keeps the notes in its block");
    }

    static string Write(string text, string path)
    {
        CoreSaveStore.SaveStarted();
        try { return CoreSaveStore.Decorate(path + ".tmp", text); }
        finally { CoreSaveStore.SaveEnded(); }
    }

    static string Squash(string text) => new string(text.Where(c => !char.IsWhiteSpace(c)).ToArray());

    // How many JSON strings, property names and values, are exactly this key.
    static int KeyCount(string json, string key)
    {
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));
        var count = 0;
        while (reader.Read())
            if ((reader.TokenType == JsonTokenType.PropertyName || reader.TokenType == JsonTokenType.String) && reader.GetString() == key) count++;
        return count;
    }

    // How many properties repeat a name already used in the same object.
    static int DuplicateKeys(string json)
    {
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));
        var objects = new Stack<HashSet<string>>();
        var duplicates = 0;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.StartObject) objects.Push(new HashSet<string>(StringComparer.Ordinal));
            else if (reader.TokenType == JsonTokenType.EndObject) objects.Pop();
            else if (reader.TokenType == JsonTokenType.PropertyName && !objects.Peek().Add(reader.GetString())) duplicates++;
        }
        return duplicates;
    }
}
