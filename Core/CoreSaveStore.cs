namespace DungeonSettlersDelvers.Core;

// Core's data in the campaign save: which block belongs to which file, and which block a save
// gets (CoreSaveBlock does the text work, CoreSaveData holds the content, CoreSaveStorePatches
// connects the game).
// - Reading: every save text the game parses is remembered with its file's block.
// - Loading: the block of the file being applied becomes Current (a new, empty one without).
// - Writing: during the game's own campaign save, Current goes into the text written to
//   Saves\<name>.json.tmp. Other rewrites of a file (the game's rename of save files,
//   SaveLoadHelper.TryRematchSaveFileNames, writes through SaveFile) keep that file's block.
// - Core's own file per campaign: campaigns\<guid>.json holds the newest
//   data of that campaign, written with every save of the game. A save without block (saved
//   without Core, or older than Core's save data) takes it over, and its next save carries it again.
// Without game types, so the offline tests cover it. Any failure leaves the text unchanged.
internal static class CoreSaveStore
{
    private const string TempSuffix = ".tmp";
    private const string SaveFolder = "Saves";
    private static readonly Dictionary<string, string> blocksByPath = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Exception> readFailures = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> reported = new(StringComparer.Ordinal);
    private static int savesInProgress;
    private static string archiveRoot;

    // The data of the loaded or newly created campaign; null on the title screen.
    internal static CoreSaveData Current { get; private set; }

    // Runs inside the game's own save just before Current is written (revive snapshots).
    internal static Action BeforeOwnSave { get; set; }

    internal static void ResetLifecycle()
    {
        blocksByPath.Clear();
        readFailures.Clear();
        lock (reported) reported.Clear();
        savesInProgress = 0;
        archiveRoot = null;
        BeforeOwnSave = null;
        LegacyPresence = null;
        Current = null;
        CampaignPresenceBook.ResetLifecycle();
    }

    // Where Core keeps its own file per campaign; set by the game side before the first use.
    internal static void UseArchive(string directory)
        => archiveRoot = string.IsNullOrEmpty(directory) ? null : Path.GetFullPath(directory);

    internal static string ArchivePath(Guid campaign)
        => archiveRoot == null || campaign == Guid.Empty ? null : Path.Combine(archiveRoot, campaign.ToString("D") + ".json");

    // Test cleanup: the campaign's own file goes.
    internal static bool ForgetArchive(Guid campaign)
    {
        var path = ArchivePath(campaign);
        if (path == null || !File.Exists(path)) return false;
        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception ex)
        {
            Report("archive-forget", ex);
            return false;
        }
    }

    // A save text the game is about to parse: its block is kept for the file, and Core's own items,
    // units, research, tasks and skills get back what the save text held out of the game's part
    // (CoreSaveItems, CoreSaveUnits, CoreSaveResearch, CoreSaveTasks, CoreSaveSkills). Returns the
    // text the game should read.
    internal static string Remember(string json, string path)
    {
        var block = CoreSaveBlock.Extract(json);
        if (!string.IsNullOrEmpty(path))
        {
            var key = Normalize(path);
            readFailures.Remove(key);
            if (block == null) blocksByPath.Remove(key);
            else blocksByPath[key] = block;
        }
        var data = CoreSaveData.ParseAny(block);
        if (data == null) return json;
        // The block of another campaign (a copy, a renamed file) restores nothing into this text; BindLoaded ignores it too.
        var textCampaign = CoreSaveBlock.CampaignOf(json);
        if (!CoreSaveBlock.BlockApplies(data.Campaign, textCampaign))
        {
            DelversHost.Warning($"CORE_SAVE_DATA_IGNORED reason=other-campaign stage=read campaign={textCampaign:D} block={data.Campaign:D}");
            return json;
        }
        var text = json;
        var owned = data.OwnedItems();
        var places = data.ItemPlaces();
        if (owned.Count > 0 || places.Any)
        {
            text = CoreSaveItems.FromNative(text, owned, places, out var count);
            if (count > 0) DelversHost.Info("CORE_SAVE_ITEMS_RESTORED count=" + count);
        }
        var units = data.OwnedUnits();
        if (units.Count > 0)
        {
            text = CoreSaveUnits.FromNative(text, units, out var count);
            DelversHost.Info($"CORE_SAVE_UNITS_RESTORED kept={units.Count} restored={count}");
        }
        var research = data.Research();
        if (research.Count > 0)
        {
            text = CoreSaveResearch.FromNative(text, research, out var count);
            DelversHost.Info($"CORE_SAVE_RESEARCH_RESTORED kept={research.Count} restored={count}");
        }
        var tasks = data.Tasks();
        if (tasks.Count > 0)
        {
            text = CoreSaveTasks.FromNative(text, tasks, out var count);
            DelversHost.Info($"CORE_SAVE_TASKS_RESTORED kept={tasks.Count} restored={count}");
        }
        var skills = data.OwnedSkills();
        if (skills.Count > 0)
        {
            text = CoreSaveSkills.FromNative(text, skills, out var count);
            DelversHost.Info($"CORE_SAVE_SKILLS_RESTORED kept={skills.Count} units={count}");
        }
        return text;
    }

    // Remember failed for this file: the game reads its text unchanged, and the failure waits for
    // the load of exactly this file (LoadIntegrityGuard.Begin asks ReadFailureOf). Saving stays
    // possible until then; a later successful Remember of the file forgets the failure.
    internal static void RememberFailed(string path, Exception ex)
    {
        Report("read", ex);
        if (!string.IsNullOrEmpty(path)) readFailures[Normalize(path)] = ex;
    }

    // The failure of Remember for this file, or null.
    internal static Exception ReadFailureOf(string path)
        => !string.IsNullOrEmpty(path) && readFailures.TryGetValue(Normalize(path), out var failure) ? failure : null;

    // The game applies the save at savePath (null when unknown).
    internal static void BindLoaded(Guid campaign, string savePath)
    {
        string block = null;
        if (!string.IsNullOrEmpty(savePath)) blocksByPath.TryGetValue(Normalize(savePath), out block);
        var data = CoreSaveData.Parse(block, campaign);
        if (block != null && data == null)
            DelversHost.Warning("CORE_SAVE_DATA_IGNORED reason=damaged-or-other-campaign campaign=" + campaign.ToString("D"));
        var source = "save";
        if (data == null)
        {
            data = CoreSaveData.Parse(ReadArchive(campaign), campaign);
            source = data != null ? "archive" : "new";
        }
        Current = data ?? CoreSaveData.Create(campaign);
        DelversHost.Info($"CORE_SAVE_DATA_BOUND campaign={campaign:D} source={source} units={Current.Units().Count}");
        AdoptPresence(campaign, savePath);
        CampaignPresenceBook.Bound(Current);
    }

    // The marker files an older Core wrote for a campaign, each id with its write time (CampaignPresence.LegacyMarkers); set by
    // the game side. Read once per block without the presence field, never written.
    internal static Func<Guid, IEnumerable<(string Id, DateTime WrittenUtc)>> LegacyPresence { get; set; }

    private static void AdoptPresence(Guid campaign, string savePath)
    {
        if (Current.HasPresence) return;
        var legacy = LegacyPresence?.Invoke(campaign);
        DateTime? saved = !string.IsNullOrEmpty(savePath) && File.Exists(savePath) ? File.GetLastWriteTimeUtc(savePath) : null;
        var (adopted, skipped) = CampaignPresenceBook.Adopt(Current, saved, legacy);
        if (adopted + skipped > 0)
            DelversHost.Info($"CORE_PRESENCE_ADOPTED campaign={campaign:D} adopted={adopted} skipped={skipped} saveTime={(saved.HasValue ? "1" : "0")}");
    }

    // A new campaign is being set up: nothing of the previous one may reach its saves, even if
    // BeginNewCampaign never comes.
    internal static void Unbind() => Current = null;

    // Binding the block of the loaded file failed: Current is still the throwaway
    // campaign of the load's start, and its block must not reach the save or the campaign's file.
    internal static void BindFailed(Exception ex)
    {
        Report("load", ex);
        Unbind();
    }

    // The campaign's own file appears with its first save, not here: the game also sets up a
    // throwaway campaign with a random GUID at the start of every load, before it applies the
    // save (Delvers Frieren), and that one must leave no file behind.
    internal static void BeginNewCampaign(Guid campaign)
    {
        // A new campaign never takes over old marker files: it has had nobody yet (AdoptPresence runs only for a loaded save).
        Current = CoreSaveData.Create(campaign);
        DelversHost.Info($"CORE_SAVE_DATA_BOUND campaign={campaign:D} source=new units=0");
        CampaignPresenceBook.Bound(Current);
    }

    internal static void SaveStarted() => savesInProgress++;

    internal static void SaveEnded()
    {
        if (savesInProgress > 0) savesInProgress--;
    }

    internal static bool IsCampaignSaveTemp(string path)
    {
        if (string.IsNullOrEmpty(path) || !path.EndsWith(".json" + TempSuffix, StringComparison.OrdinalIgnoreCase)) return false;
        try { return string.Equals(Path.GetFileName(Path.GetDirectoryName(path)), SaveFolder, StringComparison.OrdinalIgnoreCase); }
        catch (ArgumentException) { return false; }
    }

    // The text the game is about to write; unchanged unless it is a campaign save.
    internal static string Decorate(string path, string contents)
    {
        if (string.IsNullOrEmpty(contents) || !IsCampaignSaveTemp(path)) return contents;
        var target = Normalize(path.Substring(0, path.Length - TempSuffix.Length));
        // Core's own effects stay out of the game's part (CoreSaveEffects), and its own items are
        // written as their native stand-in (CoreSaveItems); an own key in a unit's item quick slot or in a
        // storage rule's item list is cut out and noted in Core's block instead (CoreSaveItems.Places).
        // Its own units, the first aid research and its tasks, and Core's skills on the units go into
        // Core's block, so a game without Core never meets a key it does not know.
        var stripped = CoreSaveEffects.Strip(contents, out var removed);
        if (removed > 0) DelversHost.Info("CORE_SAVE_EFFECTS_KEPT_OUT count=" + removed);
        var owned = new Dictionary<Guid, string>();
        var places = new CoreSaveItems.Places();
        stripped = CoreSaveItems.ToNative(stripped, owned, places);
        if (places.Any) DelversHost.Info($"CORE_SAVE_ITEMS_KEPT_OUT slots={places.QuickSlots.Count} storage={places.Storage.Count}");
        if (places.Missed > 0)
            DelversHost.Warning($"CORE_SAVE_ITEMS_SHAPE_UNKNOWN missed={places.Missed} (written as stand-ins, not restored on load)");
        var units = new Dictionary<Guid, string>();
        stripped = CoreSaveUnits.ToNative(stripped, units);
        if (units.Count > 0) DelversHost.Info("CORE_SAVE_UNITS_KEPT_OUT units=" + units.Count);
        var research = new List<CoreSaveResearch.Entry>();
        stripped = CoreSaveResearch.ToNative(stripped, research);
        var tasks = new List<CoreSaveTasks.Entry>();
        stripped = CoreSaveTasks.ToNative(stripped, tasks);
        if (research.Count + tasks.Count > 0)
            DelversHost.Info($"CORE_SAVE_FIRSTAID_KEPT_OUT research={research.Count} tasks={tasks.Count}");
        var skills = new List<CoreSaveSkills.Entry>();
        stripped = CoreSaveSkills.ToNative(stripped, skills);
        if (skills.Count > 0) DelversHost.Info("CORE_SAVE_SKILLS_KEPT_OUT skills=" + skills.Count);
        string block;
        var ownSave = savesInProgress > 0 && Current != null;
        if (ownSave)
        {
            try { BeforeOwnSave?.Invoke(); }
            catch (Exception ex) { Report("before-save", ex); }
            Current.SetOwnedItems(owned);
            Current.SetItemPlaces(places);
            Current.SetOwnedUnits(units);
            Current.SetResearch(research);
            Current.SetTasks(tasks);
            Current.SetOwnedSkills(skills);
            block = Current.Serialize();
        }
        else
        {
            blocksByPath.TryGetValue(target, out block);
            var known = CoreSaveData.ParseAny(block);
            if (known == null && owned.Count == 0 && !places.Any && units.Count == 0 && research.Count == 0 && tasks.Count == 0 && skills.Count == 0)
                return stripped;
            // A rewrite of a file on disk: its block stays, with the lists of this text.
            known ??= CoreSaveData.Create(Guid.Empty);
            known.SetOwnedItems(owned);
            known.SetItemPlaces(places);
            known.SetOwnedUnits(units);
            known.SetResearch(research);
            known.SetTasks(tasks);
            known.SetOwnedSkills(skills);
            block = known.Serialize();
        }
        var result = CoreSaveBlock.Append(stripped, block);
        if (ReferenceEquals(result, stripped))
        {
            Report("append", new InvalidOperationException("the save text does not end like a JSON object"));
            return stripped;
        }
        blocksByPath[target] = block;
        if (ownSave) WriteArchive(Current.Campaign, block);
        return result;
    }

    private static string ReadArchive(Guid campaign)
    {
        var path = ArchivePath(campaign);
        if (path == null || !File.Exists(path)) return null;
        try { return File.ReadAllText(path); }
        catch (Exception ex)
        {
            Report("archive-read", ex);
            return null;
        }
    }

    // Never fails the save: the block in the save itself already holds the data.
    private static void WriteArchive(Guid campaign, string block)
    {
        var path = ArchivePath(campaign);
        if (path == null) return;
        var temp = path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(temp, block);
            File.Move(temp, path, true);
        }
        catch (Exception ex)
        {
            Report("archive-write", ex);
            try { if (File.Exists(temp)) File.Delete(temp); }
            catch (Exception) { /* A stale temporary file is replaced by the next write. */ }
        }
    }

    internal static void Report(string stage, Exception ex)
    {
        lock (reported)
            if (!reported.Add(stage)) return;
        DelversHost.Error("CORE_SAVE_DATA_FAILED stage=" + stage + ": " + ex);
    }

    private static string Normalize(string path)
    {
        try { return Path.GetFullPath(path); }
        catch (Exception) { return path; }
    }
}
