using HarmonyLib;
using MelonLoader;

[assembly: MelonInfo(typeof(DungeonSettlersDelvers.Core.MelonLoader.CoreMod),
    "Dungeon Settlers Delvers: Core", "0.3.0", "Danny")]
[assembly: MelonGame(null, "DungeonSettlers")]
[assembly: HarmonyDontPatchAll]

namespace DungeonSettlersDelvers.Core.MelonLoader;

public sealed class CoreMod : MelonMod
{
    private MelonDelversHostServices host;
    private bool ownsRuntime;

    public override void OnInitializeMelon()
    {
        if (ownsRuntime) return;
        host = new MelonDelversHostServices(this);
        try
        {
            DelversHost.Bind(host);
            DelversCoreRuntime.Initialize(HarmonyInstance, "Melon");
            ownsRuntime = true;
        }
        catch (Exception ex)
        {
            DelversHost.Unbind(host);
            host = null;
            LoggerInstance.Error("Core initialization failed before it became ready: " + ex);
            throw;
        }
    }

    public override void OnDeinitializeMelon()
    {
        if (ownsRuntime)
        {
            HarmonyInstance.UnpatchSelf();
            DelversCoreRuntime.Shutdown("Melon");
        }
        DelversHost.Unbind(host);
        host = null;
        ownsRuntime = false;
    }
}
