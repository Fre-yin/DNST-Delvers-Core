using System.Collections.Concurrent;
using Il2CppInterop.Runtime;

namespace DungeonSettlersDelvers.Core;

// Adds an IL2CPP value type (a struct such as AffecterHolder) to a native List<T>.
// Il2CppInterop 1.5.3 under BepInEx 6 hands List<T>.Add the boxed object instead of the
// struct data, so the list receives the object header and a garbage string pointer; reading
// or saving that entry later can crash the game. MelonLoader's Il2CppInterop is not affected.
// Calling the native Add with the unboxed data avoids the wrapper and behaves the same under
// both loaders. Found by the in-game self-test on 29.09.2026. Core API 1.4.0.
public static class DelversNativeList
{
    private static readonly ConcurrentDictionary<IntPtr, IntPtr> AddMethods = new();

    public static unsafe void AddValue<T>(Il2CppSystem.Collections.Generic.List<T> list, T item)
        where T : Il2CppSystem.ValueType
    {
        if (list == null) throw new ArgumentNullException(nameof(list));
        if (item == null) throw new ArgumentNullException(nameof(item));
        var listPtr = IL2CPP.Il2CppObjectBaseToPtrNotNull(list);
        var add = AddMethods.GetOrAdd(IL2CPP.il2cpp_object_get_class(listPtr), FindAdd);
        var args = stackalloc IntPtr[1];
        args[0] = IL2CPP.il2cpp_object_unbox(IL2CPP.Il2CppObjectBaseToPtrNotNull(item));
        var exception = IntPtr.Zero;
        IL2CPP.il2cpp_runtime_invoke(add, listPtr, (void**)args, ref exception);
        GC.KeepAlive(item);
        if (exception != IntPtr.Zero) throw new Il2CppException(exception);
    }

    private static IntPtr FindAdd(IntPtr listClass)
    {
        var method = IL2CPP.il2cpp_class_get_method_from_name(listClass, "Add", 1);
        return method != IntPtr.Zero ? method : throw new MissingMethodException("List<T>.Add");
    }
}
