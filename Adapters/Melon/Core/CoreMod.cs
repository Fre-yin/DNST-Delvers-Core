using HarmonyLib;
using MelonLoader;

[assembly: MelonInfo(typeof(DungeonSettlersDelvers.Core.MelonLoader.CoreMod),
    "Dungeon Settlers Delvers: Core", "0.5.1", "Fre-yin")]
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
            // Masked twice: our line, and the rethrow the loader prints without our host.
            LoggerInstance.Error(DelversLogPrivacy.Mask("Core initialization failed before it became ready: " + ex));
            throw new InvalidOperationException(DelversLogPrivacy.Mask("Core initialization failed: " + ex.Message));
        }
    }

    // After the game's animations, before drawing (the bandits' orange seeds, BanditTint).
    public override void OnLateUpdate()
    {
        if (ownsRuntime) DelversCoreRuntime.LateFrame();
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
