using System.Collections;
using MelonLoader;

namespace DungeonSettlersDelvers.Core.MelonLoader;

internal sealed class MelonDelversHostServices : IDelversHostServices
{
    private readonly CoreMod owner;

    internal MelonDelversHostServices(CoreMod owner)
    {
        this.owner = owner;
        PackageDirectory = Path.Combine(Path.GetDirectoryName(UnityEngine.Application.dataPath),
            "Mods", "DungeonSettlersDelvers");
    }

    public string PackageDirectory { get; }
    public void Info(string message) => owner.LoggerInstance.Msg(message);
    public void Warning(string message) => owner.LoggerInstance.Warning(message);
    public void Error(string message) => owner.LoggerInstance.Error(message);
    public object StartCoroutine(IEnumerator routine) => MelonCoroutines.Start(routine);
    public void StopCoroutine(object handle) => MelonCoroutines.Stop(handle);
}
