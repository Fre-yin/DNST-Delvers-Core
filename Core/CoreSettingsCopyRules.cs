using System.Security.Cryptography;
using System.Text;

namespace DungeonSettlersDelvers.Core;

// Core's switches per game copy. Every copy of the game on one PC (a Steam install, a test
// sandbox) shares the same persistentDataPath, so the one settings.json made a switch set in one copy act in every other.
// Each copy now keeps its own file under DungeonSettlersDelvers\Core\copies\<id>\settings.json; the id comes from the game's
// folder (the parent of Application.dataPath). The first time a copy has no file of its own, it takes the shared file's
// values once; the shared file is read only, never written or deleted. Campaign files stay shared, as the saves are.
// The id, for tools outside the game: the game folder as a full path, '/' as '\', no '\' at the end, lower case (invariant);
// SHA256 over its UTF-8 bytes; the first 8 hex digits, upper case. Rules without the game.
internal static class CoreSettingsCopyRules
{
    internal const string ApiVersion = "1.10.0";
    internal const string FileName = "settings.json";
    internal const string NoteName = "copy.txt";

    internal static string Normalize(string gameFolder)
    {
        if (string.IsNullOrWhiteSpace(gameFolder)) return null;
        var full = Path.GetFullPath(gameFolder.Trim()).Replace('/', '\\');
        while (full.Length > 3 && full.EndsWith("\\", StringComparison.Ordinal)) full = full.Substring(0, full.Length - 1);
        return full.ToLowerInvariant();
    }

    internal static string CopyId(string gameFolder)
    {
        var normal = Normalize(gameFolder);
        if (normal == null) return null;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normal));
        return Convert.ToHexString(hash, 0, 4);
    }

    internal static string CoreFolder(string dataRoot) => Path.Combine(dataRoot, "DungeonSettlersDelvers", "Core");

    // The file all copies shared.
    internal static string SharedPath(string dataRoot) => Path.Combine(CoreFolder(dataRoot), FileName);

    // The copy's own file; the shared one when the game folder is unknown.
    internal static string CopyPath(string dataRoot, string gameFolder)
    {
        var id = CopyId(gameFolder);
        return id == null ? SharedPath(dataRoot) : Path.Combine(CoreFolder(dataRoot), "copies", id, FileName);
    }

    // Prepares the copy's folder: copy.txt with the game folder, and the shared file's values taken once when the copy has no
    // file yet. Throws on an IO failure; the caller then keeps the defaults for this session.
    internal static (string Path, string Id, bool Migrated) Prepare(string dataRoot, string gameFolder)
    {
        var id = CopyId(gameFolder);
        var path = CopyPath(dataRoot, gameFolder);
        if (id == null) return (path, null, false);
        var folder = System.IO.Path.GetDirectoryName(path);
        Directory.CreateDirectory(folder);
        var note = System.IO.Path.Combine(folder, NoteName);
        // One line: the game folder as a full path, UTF-8 without BOM, CRLF at its end. Written once, never changed.
        if (!File.Exists(note)) File.WriteAllText(note, NoteText(gameFolder), new UTF8Encoding(false));
        var shared = SharedPath(dataRoot);
        var migrated = false;
        if (!File.Exists(path) && File.Exists(shared))
        {
            File.Copy(shared, path, false);
            migrated = true;
        }
        return (path, id, migrated);
    }

    internal static string NoteText(string gameFolder) => System.IO.Path.GetFullPath(gameFolder.Trim()) + "\r\n";

    internal static string Line(string id, string gameFolder, bool migrated)
        => "CORE_SETTINGS_COPY id=" + (id ?? "-") + " game=" + (string.IsNullOrWhiteSpace(gameFolder) ? "-" : gameFolder.Trim()) + " migrated=" + (migrated ? 1 : 0);
}
