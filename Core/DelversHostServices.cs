using System.Collections;

namespace DungeonSettlersDelvers.Core;

public interface IDelversHostServices
{
    string PackageDirectory { get; }
    void Info(string message);
    void Warning(string message);
    void Error(string message);
    object StartCoroutine(IEnumerator routine);
    void StopCoroutine(object handle);
}

// Bound once by the Core loader adapter. Frieren uses the same services through
// Core, so loader APIs never enter either gameplay assembly.
public static class DelversHost
{
    private static readonly object Gate = new();
    private static IDelversHostServices current;

    public static bool IsBound { get { lock (Gate) return current != null; } }
    public static string PackageDirectory { get { lock (Gate) return current?.PackageDirectory; } }

    public static void Bind(IDelversHostServices host)
    {
        if (host == null) throw new ArgumentNullException(nameof(host));
        lock (Gate)
        {
            if (current != null && !ReferenceEquals(current, host))
                throw new InvalidOperationException("Dungeon Settlers Delvers Core host services are already bound to another loader adapter.");
            current = host;
        }
    }

    public static void Unbind(IDelversHostServices host)
    {
        lock (Gate)
            if (ReferenceEquals(current, host)) current = null;
    }

    public static void Info(string message) => GetCurrent()?.Info(message);
    public static void Warning(string message) => GetCurrent()?.Warning(message);
    public static void Error(string message) => GetCurrent()?.Error(message);
    public static object StartCoroutine(IEnumerator routine) => GetCurrent()?.StartCoroutine(routine);
    public static void StopCoroutine(object handle) => GetCurrent()?.StopCoroutine(handle);

    private static IDelversHostServices GetCurrent()
    {
        lock (Gate) return current;
    }
}
