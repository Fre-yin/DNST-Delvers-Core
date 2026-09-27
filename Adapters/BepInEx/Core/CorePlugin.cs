using global::BepInEx;
using global::BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;

namespace DungeonSettlersDelvers.Core.BepInEx;

[BepInPlugin(DelversCoreRuntime.BepInExPluginId, "Dungeon Settlers Delvers: Core", "0.3.0")]
[BepInProcess("DungeonSettlers.exe")]
public sealed class CorePlugin : BasePlugin
{
    private Harmony harmony;
    private BepInExDelversHostServices host;
    private DelversCoroutineRunner runner;
    private bool ownsRuntime;

    public override void Load()
    {
        if (ownsRuntime) return;
        try
        {
            runner = AddComponent<DelversCoroutineRunner>();
            host = new BepInExDelversHostServices(this, runner);
            DelversHost.Bind(host);
            harmony = new Harmony(DelversCoreRuntime.BepInExPluginId);
            DelversCoreRuntime.Initialize(harmony, "BepInEx");
            ownsRuntime = true;
        }
        catch (Exception ex)
        {
            DelversHost.Unbind(host);
            if (runner) UnityEngine.Object.Destroy(runner);
            host = null;
            runner = null;
            Log.LogError("Core initialization failed before it became ready: " + ex);
            throw;
        }
    }

    // The core owns persisted campaign markers and cannot be safely hot-unloaded.
    public override bool Unload() => false;
}

public sealed class DelversCoroutineRunner : MonoBehaviour
{
    public DelversCoroutineRunner(IntPtr pointer) : base(pointer) { }
}
