namespace DungeonSettlersDelvers.Core;

// The bandit camp's room files, embedded in Core's DLL (Core/Rooms): the camp as the author drew it, and its mirrored form once she
// has approved the file the Modding Toolkit's room editor writes from it. Each is read once
// (BanditCampFile); a file that is missing or refused stays null, and without the mirrored file every camp is the normal one.
internal static class BanditCampRoomFiles
{
    internal const string Folder = "Rooms";
    internal const string NormalFile = "Bandit-Forestcamp.json";
    internal const string MirroredFile = "Bandit-Forestcamp-gespiegelt.json";
    private const string ResourcePrefix = "DungeonSettlersDelvers.Core.Rooms.";

    private static readonly Dictionary<string, (BanditCampRoom Room, string Problem)> read = new(StringComparer.Ordinal);

    internal static string FileOf(int variant) => variant == BanditCampChoice.Mirrored ? MirroredFile : NormalFile;

    // The mirrored form may be chosen only when its file is there and readable.
    internal static bool MirroredAvailable => Room(BanditCampChoice.Mirrored, out _) != null;

    // The room of the variant, or null with the reason.
    internal static BanditCampRoom Room(int variant, out string problem)
    {
        var file = FileOf(variant);
        if (!read.TryGetValue(file, out var entry))
        {
            var text = Text(file);
            if (text == null) entry = (null, "missing " + file);
            else entry = BanditCampFile.TryRead(text, out var room, out var refused) ? (room, null) : (null, file + ": " + refused);
            read[file] = entry;
        }
        problem = entry.Problem;
        return entry.Room;
    }

    private static string Text(string file)
    {
        using var stream = typeof(BanditCampRoomFiles).Assembly.GetManifestResourceStream(ResourcePrefix + file);
        if (stream == null) return null;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
