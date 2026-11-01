using System.Runtime.InteropServices;
using Il2CppInterop.Runtime.InteropTypes;

namespace DungeonSettlersDelvers.Core;

// A two-dimensional array of the game's generated floor (DungeonPreMapData: GridFlag[,], DecorationType[,]) read and written in
// place, as the Modding Toolkit's dev room does: the array header is 32 bytes, the bounds pointer sits at 16, every bound is
// 16 bytes (length, lower bound), elements are int32 at index x * height + y (DungeonGeneratorStrategy.ExtractMapData).
internal sealed class BanditCampGrid
{
    private const int HeaderBytes = 32, BoundsPointer = 16, BoundBytes = 16, TotalLength = 24, CellBytes = 4;

    private readonly IntPtr array;
    internal readonly int Width;
    internal readonly int Height;

    internal BanditCampGrid(Il2CppObjectBase source)
    {
        array = source?.Pointer ?? IntPtr.Zero;
        if (array == IntPtr.Zero) throw new InvalidOperationException("the floor's array is missing");
        var bounds = Marshal.ReadIntPtr(array + BoundsPointer);
        if (bounds == IntPtr.Zero) throw new InvalidOperationException("the floor's array has no bounds");
        Width = checked((int)Marshal.ReadInt64(bounds));
        Height = checked((int)Marshal.ReadInt64(bounds + BoundBytes));
        var total = Marshal.ReadInt64(array + TotalLength);
        if (Width <= 0 || Height <= 0 || total != (long)Width * Height)
            throw new InvalidOperationException($"unexpected floor size {Width}x{Height} ({total})");
    }

    internal bool Contains(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    internal int this[int x, int y]
    {
        get => Contains(x, y) ? Marshal.ReadInt32(array + HeaderBytes + (x * Height + y) * CellBytes) : -1;
        set { if (Contains(x, y)) Marshal.WriteInt32(array + HeaderBytes + (x * Height + y) * CellBytes, value); }
    }
}
