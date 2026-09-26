using System.Collections;
using global::BepInEx.Unity.IL2CPP.Utils.Collections;
using UnityEngine;

namespace DungeonSettlersDelvers.Core.BepInEx;

internal sealed class BepInExDelversHostServices : IDelversHostServices
{
    private readonly CorePlugin owner;
    private readonly DelversCoroutineRunner runner;

    internal BepInExDelversHostServices(CorePlugin owner, DelversCoroutineRunner runner)
    {
        this.owner = owner;
        this.runner = runner;
        PackageDirectory = Path.GetDirectoryName(typeof(CorePlugin).Assembly.Location);
    }

    public string PackageDirectory { get; }
    public void Info(string message) => owner.Log.LogInfo(message);
    public void Warning(string message) => owner.Log.LogWarning(message);
    public void Error(string message) => owner.Log.LogError(message);
    public object StartCoroutine(IEnumerator routine) => runner.StartCoroutine(routine.WrapToIl2Cpp());
    public void StopCoroutine(object handle)
    {
        if (runner && handle is Coroutine coroutine) runner.StopCoroutine(coroutine);
    }
}
