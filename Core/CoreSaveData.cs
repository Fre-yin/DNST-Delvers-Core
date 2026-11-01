using System.Text.Json;
using System.Text.Json.Nodes;

namespace DungeonSettlersDelvers.Core;

// What Core keeps inside a campaign save (CoreSaveBlock): the campaign's Apocalypse parts, Core's
// items, own units and own skills, Lifebloom, research, tasks, the undressed corpses and the tactical formations, and per
// party member the revive snapshot, the revives used, the revival sickness and the wound. Pure, so
// the offline tests cover it. Unknown properties are kept as they are, so a newer Core's data survives an older one.
internal sealed class CoreSaveData
{
    internal const int Version = 1;

    private readonly JsonObject root;

    private CoreSaveData(JsonObject root) => this.root = root;

    internal static CoreSaveData Create(Guid campaign)
    {
        var data = new CoreSaveData(new JsonObject());
        data.root["version"] = Version;
        data.root["campaign"] = campaign.ToString("D");
        return data;
    }

    // Null when the block is missing, damaged or belongs to another campaign.
    internal static CoreSaveData Parse(string block, Guid campaign)
    {
        if (string.IsNullOrWhiteSpace(block)) return null;
        try
        {
            if (JsonNode.Parse(block) is not JsonObject root) return null;
            var data = new CoreSaveData(root);
            return data.Campaign == campaign ? data : null;
        }
        catch (JsonException) { return null; }
        catch (FormatException) { return null; }
        catch (InvalidOperationException) { return null; }
    }

    // Without the campaign check: the game also rewrites saves of other campaigns (renames), and
    // their block must survive that.
    internal static CoreSaveData ParseAny(string block)
    {
        if (string.IsNullOrWhiteSpace(block)) return null;
        try { return JsonNode.Parse(block) is JsonObject root ? new CoreSaveData(root) : null; }
        catch (JsonException) { return null; }
        catch (FormatException) { return null; }
        catch (InvalidOperationException) { return null; }
    }

    internal Guid Campaign
        => root["campaign"] is JsonValue value && value.TryGetValue<string>(out var text) && Guid.TryParse(text, out var guid)
            ? guid : Guid.Empty;

    // ---- own items (CoreSaveItems): which item entities are Core's, by GUID ----

    internal Dictionary<Guid, string> OwnedItems()
    {
        var result = new Dictionary<Guid, string>();
        if (root["items"] is not JsonObject items) return result;
        foreach (var pair in items)
            if (Guid.TryParse(pair.Key, out var guid) && pair.Value is JsonValue value && value.TryGetValue<string>(out var key)
                && key.StartsWith(CoreSaveItems.Prefix, StringComparison.Ordinal))
                result[guid] = key;
        return result;
    }

    internal void SetOwnedItems(IReadOnlyDictionary<Guid, string> owned)
    {
        if (owned == null || owned.Count == 0)
        {
            root.Remove("items");
            return;
        }
        var items = new JsonObject();
        foreach (var pair in owned) items[pair.Key.ToString("D")] = pair.Value;
        root["items"] = items;
    }

    // ---- own item keys outside the item records (CoreSaveItems.Places): the item quick slot per unit and the cut
    // entries of the storage rules' item lists, per rule GUID. "itemSlots": { "<unit>": "<key>" },
    // "storageItems": { "<rule>": [ { "key": "<key>", "on": true, "at": 4 } ] } (at: the entry's place in the list, from 0);
    // Extended Hotbar's two extra item slots per unit, "hotbarSlots": { "<unit>": [ "<key>", "" ] } ("" where none stood) ----

    private const string ItemSlotsName = "itemSlots";
    private const string StorageItemsName = "storageItems";
    private const string HotbarSlotsName = "hotbarSlots";

    internal CoreSaveItems.Places ItemPlaces()
    {
        var places = new CoreSaveItems.Places();
        ReadItemSlots(places);
        ReadStorageItems(places);
        ReadHotbarSlots(places);
        return places;
    }

    private void ReadItemSlots(CoreSaveItems.Places places)
    {
        if (root[ItemSlotsName] is not JsonObject slots) return;
        foreach (var pair in slots)
            if (Guid.TryParse(pair.Key, out var unit) && pair.Value is JsonValue value && value.TryGetValue<string>(out var key)
                && key.StartsWith(CoreSaveItems.Prefix, StringComparison.Ordinal))
                places.QuickSlots[unit] = key;
    }

    private void ReadStorageItems(CoreSaveItems.Places places)
    {
        if (root[StorageItemsName] is not JsonObject rules) return;
        foreach (var pair in rules)
        {
            if (!Guid.TryParse(pair.Key, out var rule) || pair.Value is not JsonArray list) continue;
            var entries = new List<CoreSaveItems.StorageEntry>();
            foreach (var node in list)
            {
                var key = node is JsonObject entry ? Text(entry, "key") : null;
                if (key == null || !key.StartsWith(CoreSaveItems.Prefix, StringComparison.Ordinal)
                    || !Int(node as JsonObject, "at", out var at) || at < 0) continue;
                entries.Add(new CoreSaveItems.StorageEntry(key, Flag(node as JsonObject, "on"), at));
            }
            if (entries.Count > 0) places.Storage[rule] = entries;
        }
    }

    private void ReadHotbarSlots(CoreSaveItems.Places places)
    {
        if (root[HotbarSlotsName] is not JsonObject units) return;
        foreach (var pair in units)
        {
            if (!Guid.TryParse(pair.Key, out var unit) || pair.Value is not JsonArray list || list.Count != 2) continue;
            var keys = new string[2];
            var any = false;
            for (var i = 0; i < 2; i++)
            {
                if (list[i] is not JsonValue value || !value.TryGetValue<string>(out var key)
                    || !key.StartsWith(CoreSaveItems.Prefix, StringComparison.Ordinal)) continue;
                keys[i] = key;
                any = true;
            }
            if (any) places.HotbarSlots[unit] = keys;
        }
    }

    internal void SetItemPlaces(CoreSaveItems.Places places)
    {
        SetItemSlots(places);
        SetStorageItems(places);
        SetHotbarSlots(places);
    }

    private void SetHotbarSlots(CoreSaveItems.Places places)
    {
        if (places == null || places.HotbarSlots.Count == 0)
        {
            root.Remove(HotbarSlotsName);
            return;
        }
        var units = new JsonObject();
        foreach (var pair in places.HotbarSlots)
            units[pair.Key.ToString("D")] = new JsonArray(pair.Value[0] ?? "", pair.Value[1] ?? "");
        root[HotbarSlotsName] = units;
    }

    private void SetItemSlots(CoreSaveItems.Places places)
    {
        if (places == null || places.QuickSlots.Count == 0)
        {
            root.Remove(ItemSlotsName);
            return;
        }
        var slots = new JsonObject();
        foreach (var pair in places.QuickSlots) slots[pair.Key.ToString("D")] = pair.Value;
        root[ItemSlotsName] = slots;
    }

    private void SetStorageItems(CoreSaveItems.Places places)
    {
        if (places == null || places.Storage.Count == 0)
        {
            root.Remove(StorageItemsName);
            return;
        }
        var rules = new JsonObject();
        foreach (var pair in places.Storage)
        {
            var list = new JsonArray();
            foreach (var entry in pair.Value)
                list.Add(new JsonObject { ["key"] = entry.Key, ["on"] = entry.On, ["at"] = entry.Index });
            rules[pair.Key.ToString("D")] = list;
        }
        root[StorageItemsName] = rules;
    }

    // ---- own units (CoreSaveUnits): which unit entities are Core's enemies, by GUID ----

    internal Dictionary<Guid, string> OwnedUnits()
    {
        var result = new Dictionary<Guid, string>();
        if (root["ownUnits"] is not JsonObject units) return result;
        foreach (var pair in units)
            if (Guid.TryParse(pair.Key, out var guid) && pair.Value is JsonValue value && value.TryGetValue<string>(out var key)
                && CoreUnitRules.IsCoreUnit(key))
                result[guid] = key;
        return result;
    }

    internal void SetOwnedUnits(IReadOnlyDictionary<Guid, string> owned)
    {
        if (owned == null || owned.Count == 0)
        {
            root.Remove("ownUnits");
            return;
        }
        var units = new JsonObject();
        foreach (var pair in owned) units[pair.Key.ToString("D")] = pair.Value;
        root["ownUnits"] = units;
    }

    // ---- own skills (CoreSaveSkills): Core's skills on units, with their slot and timers ----

    internal List<CoreSaveSkills.Entry> OwnedSkills()
    {
        var result = new List<CoreSaveSkills.Entry>();
        if (root["ownSkills"] is not JsonArray skills) return result;
        foreach (var node in skills)
        {
            if (node is not JsonObject skill) continue;
            var key = Text(skill, "key");
            if (key == null || !CoreSaveSkills.IsOwn(key) || !Guid.TryParse(Text(skill, "unit"), out var unit)) continue;
            result.Add(new CoreSaveSkills.Entry
            {
                Unit = unit,
                Key = key,
                Active = Flag(skill, "active"),
                Extra = Flag(skill, "extra"),
                QuickSlot = skill["slot"] is JsonValue slot && slot.TryGetValue<int>(out var index) ? index : -1,
                AutoUse = Text(skill, "autoUse"),
                Cooldown = Text(skill, "cooldown"),
                Duration = Text(skill, "duration")
            });
        }
        return result;
    }

    internal void SetOwnedSkills(IReadOnlyList<CoreSaveSkills.Entry> entries)
    {
        if (entries == null || entries.Count == 0)
        {
            root.Remove("ownSkills");
            return;
        }
        var skills = new JsonArray();
        foreach (var entry in entries)
        {
            var skill = new JsonObject
            {
                ["unit"] = entry.Unit.ToString("D"),
                ["key"] = entry.Key,
                ["active"] = entry.Active,
                ["extra"] = entry.Extra,
                ["slot"] = entry.QuickSlot
            };
            if (entry.AutoUse != null) skill["autoUse"] = entry.AutoUse;
            if (entry.Cooldown != null) skill["cooldown"] = entry.Cooldown;
            if (entry.Duration != null) skill["duration"] = entry.Duration;
            skills.Add(skill);
        }
        root["ownSkills"] = skills;
    }

    // ---- Lifebloom: the charges per unit (Lifebloom) ----

    internal Dictionary<Guid, LifebloomRules.Charges> LifebloomCharges()
    {
        var result = new Dictionary<Guid, LifebloomRules.Charges>();
        if (root["lifebloom"] is not JsonObject units) return result;
        foreach (var pair in units)
        {
            if (!Guid.TryParse(pair.Key, out var guid) || pair.Value is not JsonObject entry
                || entry["count"] is not JsonValue count || !count.TryGetValue<int>(out var charges)) continue;
            var nextAt = entry["next"] is JsonValue next && next.TryGetValue<double>(out var at) ? at : 0.0;
            var cooldown = entry["cooldown"] is JsonValue value && value.TryGetValue<double>(out var seconds) ? seconds : LifebloomRules.TableCooldown;
            result[guid] = new LifebloomRules.Charges(Math.Clamp(charges, 0, LifebloomRules.MaxCharges), nextAt, cooldown);
        }
        return result;
    }

    internal void SetLifebloomCharges(IReadOnlyDictionary<Guid, LifebloomRules.Charges> charges)
    {
        if (charges == null || charges.Count == 0)
        {
            root.Remove("lifebloom");
            return;
        }
        var units = new JsonObject();
        foreach (var pair in charges)
            units[pair.Key.ToString("D")] = new JsonObject
            {
                ["count"] = pair.Value.Count,
                ["next"] = pair.Value.NextAt,
                ["cooldown"] = pair.Value.Cooldown
            };
        root["lifebloom"] = units;
    }

    // ---- first aid (CoreSaveResearch, CoreSaveTasks): Core's research and the work orders for
    // its recipes, kept out of the game's part ----

    internal IReadOnlyList<CoreSaveResearch.Entry> Research()
    {
        var result = new List<CoreSaveResearch.Entry>();
        if (root["research"] is not JsonArray research) return result;
        foreach (var node in research)
        {
            if (node is not JsonObject entry) continue;
            var key = Text(entry, "key");
            if (key == null || !key.StartsWith(CoreSaveResearch.Prefix, StringComparison.Ordinal)) continue;
            var queue = entry["queue"] is JsonValue value && value.TryGetValue<int>(out var index) ? index : -1;
            result.Add(new CoreSaveResearch.Entry(key, Text(entry, "body"), queue));
        }
        return result;
    }

    internal void SetResearch(IReadOnlyList<CoreSaveResearch.Entry> entries)
    {
        if (entries == null || entries.Count == 0)
        {
            root.Remove("research");
            return;
        }
        var research = new JsonArray();
        foreach (var entry in entries)
        {
            var node = new JsonObject { ["key"] = entry.Key, ["queue"] = entry.QueueIndex };
            if (entry.Body != null) node["body"] = entry.Body;
            research.Add(node);
        }
        root["research"] = research;
    }

    internal IReadOnlyList<CoreSaveTasks.Entry> Tasks()
    {
        var result = new List<CoreSaveTasks.Entry>();
        if (root["tasks"] is not JsonArray tasks) return result;
        foreach (var node in tasks)
        {
            if (node is not JsonObject entry) continue;
            var text = Text(entry, "text");
            if (text == null || text.IndexOf(CoreSaveTasks.Prefix, StringComparison.Ordinal) < 0) continue;
            var list = entry["list"] is JsonValue container && container.TryGetValue<int>(out var listIndex) ? listIndex : -1;
            var index = entry["index"] is JsonValue position && position.TryGetValue<int>(out var taskIndex) ? taskIndex : -1;
            result.Add(new CoreSaveTasks.Entry(list, index, text));
        }
        return result;
    }

    internal void SetTasks(IReadOnlyList<CoreSaveTasks.Entry> entries)
    {
        if (entries == null || entries.Count == 0)
        {
            root.Remove("tasks");
            return;
        }
        var tasks = new JsonArray();
        foreach (var entry in entries)
            tasks.Add(new JsonObject { ["list"] = entry.Container, ["index"] = entry.Index, ["text"] = entry.Text });
        root["tasks"] = tasks;
    }

    // The bandit room's placed parts (BanditRoomFeatureRules.Part): kind, map and anchor cell, short names k, m, x, y.
    internal List<BanditRoomFeatureRules.Part> RoomFeatures()
    {
        var result = new List<BanditRoomFeatureRules.Part>();
        if (root["roomFeatures"] is not JsonArray parts) return result;
        foreach (var node in parts)
        {
            if (node is not JsonObject entry) continue;
            var kind = Text(entry, "k");
            if (!BanditRoomFeatureRules.IsKind(kind) || !Int(entry, "m", out var map) || !Int(entry, "x", out var x) || !Int(entry, "y", out var y)) continue;
            result.Add(new BanditRoomFeatureRules.Part(kind, map, x, y, Int(entry, "s", out var seed) ? seed : 0));
        }
        return result;
    }

    internal void SetRoomFeatures(IReadOnlyList<BanditRoomFeatureRules.Part> parts)
    {
        if (parts == null || parts.Count == 0)
        {
            root.Remove("roomFeatures");
            return;
        }
        var array = new JsonArray();
        foreach (var part in parts)
        {
            var entry = new JsonObject { ["k"] = part.Kind, ["m"] = part.Map, ["x"] = part.X, ["y"] = part.Y };
            // A part without a floor keeps the entry it had before the seed.
            if (part.Seed != 0) entry["s"] = part.Seed;
            array.Add(entry);
        }
        root["roomFeatures"] = array;
    }

    // The bandit camp of the floor (BanditCampFurnish): the floor's seed, the wagon's GUID while it stands, so its loot stays the
    // camp's own after a save and a load, and the GUIDs of the buildings whose picture is mirrored (BanditCampFlip).
    // flips: the buildings with their picture and their footprint mirrored (BanditCampFlip, BanditCampFootprint); box: the camp's
    // cells on the shown map, for its wake (BanditCampWake).
    // wakes: the camp's file wakes its bandits on entering (BanditCampWakeRules.WakesOnEnter); without "wake" in the block, as in
    // every block written before, it does not.
    internal void SetCamp(int seed, Guid wagon, IEnumerable<Guid> flips, (int MinX, int MinY, int MaxX, int MaxY)? box = null, bool wakes = false)
    {
        var camp = new JsonObject { ["seed"] = seed };
        if (box is { } b) camp["box"] = new JsonArray(b.MinX, b.MinY, b.MaxX, b.MaxY);
        if (box != null && wakes) camp["wake"] = true;
        if (wagon != Guid.Empty) camp["wagon"] = wagon.ToString("D");
        var list = new JsonArray();
        foreach (var guid in flips ?? Enumerable.Empty<Guid>()) list.Add(guid.ToString("D"));
        if (list.Count > 0) camp["flip"] = list;
        if (wagon == Guid.Empty && list.Count == 0 && box == null) root.Remove("camp");
        else root["camp"] = camp;
    }

    internal (int MinX, int MinY, int MaxX, int MaxY)? CampBox(out int seed)
    {
        seed = 0;
        if (root["camp"] is not JsonObject camp || !Int(camp, "seed", out seed) || camp["box"] is not JsonArray box || box.Count != 4) return null;
        var values = new int[4];
        for (var i = 0; i < 4; i++)
            if (box[i] is not JsonValue value || !value.TryGetValue(out values[i])) return null;
        return (values[0], values[1], values[2], values[3]);
    }

    internal bool CampWakes() => root["camp"] is JsonObject camp && camp["wake"] is JsonValue value && value.TryGetValue(out bool wakes) && wakes;

    internal bool CampWagon(out int seed, out Guid wagon)
    {
        seed = 0;
        wagon = Guid.Empty;
        return root["camp"] is JsonObject camp && Int(camp, "seed", out seed) && Guid.TryParse(Text(camp, "wagon"), out wagon) && wagon != Guid.Empty;
    }

    // The wagon gathered: the camp keeps its mirrored pictures.
    internal void ClearCampWagon()
    {
        if (root["camp"] is not JsonObject camp) return;
        camp.Remove("wagon");
        if (camp["flip"] == null && camp["box"] == null) root.Remove("camp");
    }

    internal HashSet<Guid> CampFlips(out int seed)
    {
        seed = 0;
        var flips = new HashSet<Guid>();
        if (root["camp"] is not JsonObject camp || !Int(camp, "seed", out seed) || camp["flip"] is not JsonArray list) return flips;
        foreach (var node in list)
            if (node is JsonValue value && value.TryGetValue<string>(out var text) && Guid.TryParse(text, out var guid)) flips.Add(guid);
        return flips;
    }

    // ---- tactical formation (TacticalFormation): the campaign's formation book (TacticalFormationBook), "formations": {
    // "active": 0, "sets": [ { "leader": "<guid>", "places": [ { "unit": "<guid>", "x": 1, "y": -1 } ] }, {} ] }, eight sets, an
    // empty one {}; x to the leader's right and y to the front, each at most 4 cells. The first format, { "<leader>": [ places ] },
    // is read as the first sets and written in the new one with the next save. ----

    private const string FormationsName = "formations";

    internal TacticalFormationBook FormationBook()
    {
        var book = new TacticalFormationBook();
        if (root[FormationsName] is not JsonObject formations) return book;
        if (formations["sets"] is JsonArray sets)
        {
            book.Active = Int(formations, "active", out var active) ? active : 0;
            for (var i = 0; i < TacticalFormationBook.Size && i < sets.Count; i++)
                if (sets[i] is JsonObject set && Guid.TryParse(Text(set, "leader"), out var leader) && leader != Guid.Empty)
                    book.Sets[i] = new TacticalFormationBook.Set(leader, Places(set["places"] as JsonArray, leader));
            return book;
        }
        // The first format: one list of places per leader.
        var next = 0;
        foreach (var pair in formations)
        {
            if (next >= TacticalFormationBook.Size) break;
            if (!Guid.TryParse(pair.Key, out var leader) || pair.Value is not JsonArray list) continue;
            book.Sets[next++] = new TacticalFormationBook.Set(leader, Places(list, leader));
        }
        return book;
    }

    internal void SetFormationBook(TacticalFormationBook book)
    {
        if (book == null || book.IsEmpty)
        {
            root.Remove(FormationsName);
            return;
        }
        var sets = new JsonArray();
        foreach (var set in book.Sets)
        {
            if (set == null)
            {
                sets.Add(new JsonObject());
                continue;
            }
            var places = new JsonArray();
            foreach (var place in set.Places)
                places.Add(new JsonObject { ["unit"] = place.Unit.ToString("D"), ["x"] = place.X, ["y"] = place.Y });
            sets.Add(new JsonObject { ["leader"] = set.Leader.ToString("D"), ["places"] = places });
        }
        root[FormationsName] = new JsonObject { ["active"] = book.Active, ["sets"] = sets };
    }

    private static List<TacticalFormationRules.Slot> Places(JsonArray list, Guid leader)
    {
        var places = new List<TacticalFormationRules.Slot>();
        foreach (var node in list ?? new JsonArray())
        {
            if (node is not JsonObject entry || !Guid.TryParse(Text(entry, "unit"), out var unit) || unit == leader
                || !Int(entry, "x", out var x) || !Int(entry, "y", out var y)) continue;
            places.Add(new TacticalFormationRules.Slot(unit, x, y));
        }
        return places;
    }

    // The priority casts per member, "casts": { "<unit>": ["<skill>", "", "<skill>", "", ""] }: learned
    // active skills by place, the first place first, "" for an empty one. An entry from the time of three places gets empty ones.
    private const string CastsName = "casts";

    internal Dictionary<Guid, string[]> PriorityCasts()
    {
        var result = new Dictionary<Guid, string[]>();
        if (root[CastsName] is not JsonObject units) return result;
        foreach (var pair in units)
        {
            if (!Guid.TryParse(pair.Key, out var unit) || pair.Value is not JsonArray list) continue;
            var keys = new string[TacticalFormationGrid.CastSlots];
            for (var i = 0; i < keys.Length; i++)
                keys[i] = i < list.Count && list[i] is JsonValue value && value.TryGetValue<string>(out var key) && key != null ? key : "";
            result[unit] = keys;
        }
        return result;
    }

    // The casts of these members as the window left them: a member without one in casts loses the entry.
    internal void SetPriorityCasts(IEnumerable<Guid> members, IReadOnlyDictionary<Guid, string[]> casts)
    {
        if (root[CastsName] is not JsonObject units) units = new JsonObject();
        foreach (var unit in members ?? Enumerable.Empty<Guid>())
        {
            var key = unit.ToString("D");
            if (casts == null || !casts.TryGetValue(unit, out var keys))
            {
                units.Remove(key);
                continue;
            }
            var list = new JsonArray();
            for (var i = 0; i < TacticalFormationGrid.CastSlots; i++) list.Add(i < keys.Length && keys[i] != null ? keys[i] : "");
            units[key] = list;
        }
        if (units.Count == 0) root.Remove(CastsName);
        else root[CastsName] = units;
    }

    // Members who are gone (dead, dismissed) leave every set of the book, their casts, the switched off list and the sleepers; a
    // set whose leader is gone becomes empty. Returns how many entries went.
    internal int DropFormationUnits(Func<Guid, bool> present)
    {
        var removed = DropCasts(present) + DropGuids(FormationOffName, present) + DropGuids(FormationSleepName, present);
        if (root[FormationsName] == null) return removed;
        var book = FormationBook();
        var dropped = book.Drop(present);
        if (dropped > 0) SetFormationBook(book);
        return removed + dropped;
    }

    // The members whose formation the player switched off (left click on the button, like the game's own
    // options), "formationOff": [ "<unit>" ], sorted; a member not in the list has it on.
    private const string FormationOffName = "formationOff";

    internal bool IsFormationOff(Guid unit) => GuidSet(FormationOffName).Contains(unit.ToString("D"));

    internal void SetFormationOff(Guid unit, bool off) => SetGuid(FormationOffName, unit, off);

    // The members Prioritize Sleep took out of the formation (TacticalFormationSleep), "formationSleep":
    // [ "<unit>" ], sorted; each also stands in formationOff, and only these come back when they wake.
    private const string FormationSleepName = "formationSleep";

    internal bool IsFormationSleep(Guid unit) => GuidSet(FormationSleepName).Contains(unit.ToString("D"));

    internal void SetFormationSleep(Guid unit, bool asleep) => SetGuid(FormationSleepName, unit, asleep);

    internal IReadOnlyList<Guid> FormationSleepers()
    {
        var result = new List<Guid>();
        foreach (var key in GuidSet(FormationSleepName)) result.Add(Guid.Parse(key));
        return result;
    }

    private int DropGuids(string name, Func<Guid, bool> present)
    {
        var gone = 0;
        foreach (var key in GuidSet(name))
        {
            var unit = Guid.Parse(key);
            if (present(unit)) continue;
            SetGuid(name, unit, false);
            gone++;
        }
        return gone;
    }

    private int DropCasts(Func<Guid, bool> present)
    {
        if (root[CastsName] is not JsonObject units) return 0;
        var gone = new List<string>();
        foreach (var pair in units)
            if (!Guid.TryParse(pair.Key, out var unit) || !present(unit)) gone.Add(pair.Key);
        foreach (var key in gone) units.Remove(key);
        if (units.Count == 0) root.Remove(CastsName);
        return gone.Count;
    }

    private static bool Int(JsonObject node, string name, out int value)
    {
        value = 0;
        return node[name] is JsonValue json && json.TryGetValue(out value);
    }

    private static string Text(JsonObject node, string name)
        => node[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    internal string Serialize() => root.ToJsonString();

    // ---- campaign presence (CampaignPresence): the unique units this campaign has had ----
    // "pv" the version, "pr" the bits of CampaignPresenceRules.Bits in hex, "px" ids outside the table. A block without "pv"
    // comes from a Core before the field; the old marker files are looked at once then (CampaignPresenceBook.Adopt).

    internal bool HasPresence => root["pv"] != null;

    internal void StartPresence()
    {
        if (!HasPresence) root["pv"] = CampaignPresenceRules.Version;
    }

    internal bool PresenceSeen(string id)
    {
        var bit = CampaignPresenceRules.BitOf(id);
        if (bit >= 0) return (PresenceMask() & (1 << bit)) != 0;
        return id != null && root["px"] is JsonArray extra && Contains(extra, id);
    }

    // True when the id was not marked yet.
    internal bool SetPresenceSeen(string id)
    {
        if (string.IsNullOrEmpty(id) || PresenceSeen(id)) return false;
        StartPresence();
        var bit = CampaignPresenceRules.BitOf(id);
        if (bit >= 0)
        {
            root["pr"] = CampaignPresenceRules.MaskText(PresenceMask() | (1 << bit));
            return true;
        }
        if (root["px"] is not JsonArray extra) root["px"] = extra = new JsonArray();
        extra.Add(id);
        return true;
    }

    private int PresenceMask() => CampaignPresenceRules.ParseMask(Text(root, "pr"));

    private static bool Contains(JsonArray list, string id)
    {
        foreach (var node in list)
            if (node is JsonValue value && value.TryGetValue<string>(out var text) && text == id) return true;
        return false;
    }

    // ---- Apocalypse ----

    internal ApocalypseMark Apocalypse
    {
        get
        {
            var node = root["apocalypse"] as JsonObject;
            return new ApocalypseMark(Flag(node, "combat"), Flag(node, "management"));
        }
        set
        {
            if (!value.Any)
            {
                root.Remove("apocalypse");
                return;
            }
            root["apocalypse"] = new JsonObject { ["combat"] = value.Combat, ["management"] = value.Management };
        }
    }

    // ---- revive ----

    internal IReadOnlyList<TraitStack> Snapshot(Guid unit)
    {
        var result = new List<TraitStack>();
        if (Unit(unit, false)?["snapshot"] is not JsonArray list) return result;
        foreach (var entry in list)
            if (entry is JsonValue value && value.TryGetValue<string>(out var text) && TraitStack.TryParse(text, out var trait))
                result.Add(trait);
        return result;
    }

    internal void SetSnapshot(Guid unit, IReadOnlyList<TraitStack> traits)
    {
        var list = new JsonArray();
        foreach (var trait in traits) list.Add(trait.ToString());
        Unit(unit, true)["snapshot"] = list;
    }

    internal int RevivesUsed(Guid unit)
        => Unit(unit, false)?["revives"] is JsonValue value && value.TryGetValue<int>(out var count) && count > 0 ? count : 0;

    internal void SetRevivesUsed(Guid unit, int count) => Unit(unit, true)["revives"] = Math.Max(0, count);

    // The revival sickness with its remaining and total length in the game's duration units: when the effect is missing
    // after a load, for instance because the
    // campaign was played without Core for a while, Core puts it back with this remaining length.
    internal bool TryGetSickness(Guid unit, out double remaining, out double total)
    {
        remaining = total = 0;
        if (Unit(unit, false)?["sickness"] is not JsonObject sickness) return false;
        if (sickness["remaining"] is not JsonValue left || !left.TryGetValue(out remaining)) return false;
        if (sickness["total"] is not JsonValue all || !all.TryGetValue(out total)) return false;
        if (!(remaining > 0) || !(total > 0) || double.IsInfinity(remaining) || double.IsInfinity(total)) return false;
        remaining = Math.Min(remaining, total);
        return true;
    }

    internal void SetSickness(Guid unit, double remaining, double total)
    {
        if (!(remaining > 0) || !(total > 0))
        {
            ClearSickness(unit);
            return;
        }
        Unit(unit, true)["sickness"] = new JsonObject { ["remaining"] = Math.Min(remaining, total), ["total"] = total };
    }

    internal void ClearSickness(Guid unit) => Unit(unit, false)?.Remove("sickness");

    // The bleeding wound's buffer in health points (WoundRules); 0 or missing means no wound.
    internal double WoundBuffer(Guid unit)
        => Unit(unit, false)?["wound"] is JsonValue value && value.TryGetValue<double>(out var buffer)
            && buffer > 0 && !double.IsInfinity(buffer) ? buffer : 0;

    internal void SetWoundBuffer(Guid unit, double buffer)
    {
        if (!(buffer > 0) || double.IsInfinity(buffer))
        {
            var entry = Unit(unit, false);
            entry?.Remove("wound");
            entry?.Remove("woundfatal");
            return;
        }
        Unit(unit, true)["wound"] = Math.Round(buffer, 3);
    }

    // The lethal wound (WoundRules.Stage.Fatal): kept until the wound closes, which also removes it; missing means not lethal.
    internal bool IsWoundFatal(Guid unit)
        => WoundBuffer(unit) > 0 && Unit(unit, false)?["woundfatal"] is JsonValue value && value.TryGetValue<bool>(out var fatal) && fatal;

    internal void SetWoundFatal(Guid unit, bool fatal)
    {
        if (fatal && WoundBuffer(unit) > 0) Unit(unit, true)["woundfatal"] = true;
        else Unit(unit, false)?.Remove("woundfatal");
    }

    // ---- equipment on the body (CorpseKeep, step 2): bandit corpses undressed, by GUID, sorted ----

    private const string UndressedName = "undressed";

    internal bool IsUndressed(Guid unit) => GuidSet(UndressedName).Contains(unit.ToString("D"));

    internal IReadOnlyList<Guid> UndressedUnits()
    {
        var result = new List<Guid>();
        foreach (var key in GuidSet(UndressedName)) result.Add(Guid.Parse(key));
        return result;
    }

    internal void SetUndressed(Guid unit, bool undressed) => SetGuid(UndressedName, unit, undressed);

    private void SetGuid(string name, Guid unit, bool member)
    {
        var set = GuidSet(name);
        var key = unit.ToString("D");
        if (member ? !set.Add(key) : !set.Remove(key)) return;
        if (set.Count == 0)
        {
            root.Remove(name);
            return;
        }
        var array = new JsonArray();
        foreach (var entry in set) array.Add(entry);
        root[name] = array;
    }

    private SortedSet<string> GuidSet(string name)
    {
        var set = new SortedSet<string>(StringComparer.Ordinal);
        if (root[name] is not JsonArray array) return set;
        foreach (var node in array)
            if (node is JsonValue value && value.TryGetValue<string>(out var text) && Guid.TryParse(text, out var guid))
                set.Add(guid.ToString("D"));
        return set;
    }

    internal IReadOnlyList<Guid> WoundedUnits()
    {
        var result = new List<Guid>();
        foreach (var unit in Units())
            if (WoundBuffer(unit) > 0) result.Add(unit);
        return result;
    }

    // Units with any data of their own (revive, sickness, wound): for the line when a save is bound and the
    // wounded units (WoundedUnits).
    internal IReadOnlyList<Guid> Units()
    {
        var result = new List<Guid>();
        if (root["units"] is not JsonObject units) return result;
        foreach (var pair in units)
            if (Guid.TryParse(pair.Key, out var guid)) result.Add(guid);
        return result;
    }

    private JsonObject Unit(Guid unit, bool create)
    {
        if (root["units"] is not JsonObject units)
        {
            if (!create) return null;
            units = new JsonObject();
            root["units"] = units;
        }
        var key = unit.ToString("D");
        if (units[key] is JsonObject entry) return entry;
        if (!create) return null;
        entry = new JsonObject();
        units[key] = entry;
        return entry;
    }

    private static bool Flag(JsonObject node, string name)
        => node?[name] is JsonValue value && value.TryGetValue<bool>(out var flag) && flag;
}
