using DungeonSettlersDelvers.Core;
using static TestKit;

// A failed read or bind of the Core block blocks saving: the failure of Remember waits for the load of exactly its file, a failed
// bind clears the throwaway campaign, and the guard runs first.
internal static class LoadFailureSection
{
    static readonly string SaveRoot = Path.Combine(Path.GetTempPath(), "delvers-load-failure-test", "Saves");
    static readonly string FileA = Path.Combine(SaveRoot, "Clan_AutoSave_1.json");
    static readonly string FileB = Path.Combine(SaveRoot, "Clan_AutoSave_2.json");
    const string Plain = "{\n  \"EntityContainerSaveData\": {},\n  \"CampaignSaveHeader\": {\"SaveName\": \"Clan_AutoSave_1\"}\n}";

    internal static void Run()
    {
        var host = new TestDelversHost("load-failure");
        DelversHost.Bind(host);
        try
        {
            ReadFailureWaitsForItsFile();
            ReadFailureLifecycle();
            BindFailureReports();
            FailedBindWritesTheFilesBlock();
            PatchOrder();
        }
        finally
        {
            CoreSaveStore.ResetLifecycle();
            DelversHost.Unbind(host);
        }
    }

    // The load as the guard sees it: begun with the read failure of that file, both completion signals, completed with its context.
    static LoadCompletionResult Load(LoadIntegrityState state, string path)
    {
        state.Begin("campaign", CoreSaveStore.ReadFailureOf(path) != null);
        state.ObserveCompletionSignal(LoadCompletionSignal.NativeFinalizeCompleted);
        state.ObserveCompletionSignal(LoadCompletionSignal.CampaignEventCompleted);
        return state.Complete(contextReady: true);
    }

    static void ReadFailureWaitsForItsFile()
    {
        CoreSaveStore.ResetLifecycle();
        var state = new LoadIntegrityState();
        CoreSaveStore.RememberFailed(FileA, new InvalidOperationException("read"));
        Check(state.IsSaveAllowed && !state.DecideSave().Blocked && CoreSaveStore.ReadFailureOf(FileA) != null,
            "read failure: before a load begins (the autosave before loading) saving stays allowed");
        Check(Load(state, FileB) == LoadCompletionResult.Trusted && state.IsSaveAllowed,
            "read failure: a failure for file A does not block saving when file B is loaded");
        Check(Load(state, FileA.ToUpperInvariant()) == LoadCompletionResult.Untrusted
                && state.IsSaveBlocked && state.FailureSource == LoadFailureSources.CoreRead && state.DecideSave().Blocked,
            "read failure: loading file A takes the failure over, whatever the path spelling, and blocks saving");
        var other = new LoadIntegrityState();
        Check(other.Begin("campaign", readFailed: true) && !other.ReportFailure("second") && other.IsSaveBlocked,
            "read failure: the taken-over failure is the first failure of that load");
        Check(!new LoadIntegrityState().Begin("campaign", readFailed: false), "read failure: a load without one reports nothing");
    }

    static void ReadFailureLifecycle()
    {
        CoreSaveStore.ResetLifecycle();
        CoreSaveStore.RememberFailed(FileA, new InvalidOperationException("read"));
        CoreSaveStore.Remember(Plain, FileA);
        Check(CoreSaveStore.ReadFailureOf(FileA) == null, "read failure: a later successful read of the file forgets it");
        CoreSaveStore.RememberFailed(FileA, new InvalidOperationException("read"));
        CoreSaveStore.ResetLifecycle();
        Check(CoreSaveStore.ReadFailureOf(FileA) == null, "read failure: a lifecycle reset forgets it");
        CoreSaveStore.RememberFailed(null, new InvalidOperationException("read"));
        Check(CoreSaveStore.ReadFailureOf(null) == null && CoreSaveStore.ReadFailureOf("") == null,
            "read failure: a read without a path cannot belong to any load");
    }

    static void BindFailureReports()
    {
        var state = new LoadIntegrityState();
        state.Begin("campaign");
        Check(state.ReportFailure(LoadFailureSources.CoreBind) && !state.ReportFailure(LoadFailureSources.ApocalypseBind)
                && state.FailureSource == LoadFailureSources.CoreBind,
            "bind failure: the first of Core's bind, an empty GUID or the Apocalypse bind names the load's source");
        Check(state.Complete(contextReady: true) == LoadCompletionResult.Untrusted && state.DecideSave().Blocked,
            "bind failure: the load completes untrusted and saving is blocked until the restart");
        Check(new[] { LoadFailureSources.CoreRead, LoadFailureSources.CoreBind, LoadFailureSources.ApocalypseBind }.Distinct().Count() == 3,
            "bind failure: every detection source has its own name");
    }

    static void FailedBindWritesTheFilesBlock()
    {
        var throwaway = Guid.NewGuid();
        var unbound = ReadFileOfNewCampaign();
        CoreSaveStore.BeginNewCampaign(throwaway);
        CoreSaveStore.BindFailed(new InvalidOperationException("bind"));
        Check(CoreSaveStore.Current == null && OwnSaveBlockBelongsTo(unbound) && !OwnSaveBlockBelongsTo(throwaway),
            "bind failure: after the unbind an own save writes the block of the file, not the throwaway campaign's");
        var kept = ReadFileOfNewCampaign();
        CoreSaveStore.BeginNewCampaign(throwaway);
        Check(OwnSaveBlockBelongsTo(throwaway) && !OwnSaveBlockBelongsTo(kept),
            "bind failure: without the unbind an own save would carry the throwaway campaign's block");
    }

    // File A is read with the block of a campaign of its own; returns that campaign.
    static Guid ReadFileOfNewCampaign()
    {
        CoreSaveStore.ResetLifecycle();
        var campaign = Guid.NewGuid();
        CoreSaveStore.BeginNewCampaign(campaign);
        CoreSaveStore.SaveStarted();
        var written = CoreSaveStore.Decorate(FileA + ".tmp", Plain);
        CoreSaveStore.SaveEnded();
        CoreSaveStore.ResetLifecycle();
        CoreSaveStore.Remember(written, FileA);
        return campaign;
    }

    // The block an own save writes into file A is the one of that campaign.
    static bool OwnSaveBlockBelongsTo(Guid campaign)
    {
        CoreSaveStore.SaveStarted();
        var saved = CoreSaveStore.Decorate(FileA + ".tmp", Plain);
        CoreSaveStore.SaveEnded();
        return CoreSaveData.Parse(CoreSaveBlock.Extract(saved), campaign) != null;
    }

    // The guard begins the load before Core binds its block and before Apocalypse binds its marks.
    static void PatchOrder()
    {
        SourceFile guard = null;
        SourceFile store = null;
        foreach (var file in SourceTree.Current.Files(SourceFolders.Core))
        {
            if (file.Name == "LoadIntegrityGuard.cs") guard = file;
            if (file.Name == "CoreSaveStorePatches.cs") store = file;
        }
        if (guard == null || store == null) return;
        Check(guard.Code.Contains("[HarmonyPriority(Priority.First)]\n    private static void Prefix(CampaignDataContainer __instance", StringComparison.Ordinal)
                && store.Code.Contains("[HarmonyPriority(Priority.High)]\n    private static void Prefix(CampaignSaveData data)", StringComparison.Ordinal),
            "patch order: the guard's prefix on Deserialize is First, Core's block binding is High, Apocalypse stays normal");
    }
}
