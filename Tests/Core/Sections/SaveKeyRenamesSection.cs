using DungeonSettlersDelvers.Core;
using static TestKit;

// Save key renames of packs.
internal static class SaveKeyRenamesSection
{
    internal static void Run()
    {
        var saveKeyRenames = new SaveKeyRenameRegistry();
        var renameLease = saveKeyRenames.Register("example-pack", new Dictionary<string, string>
        {
            ["AFFECTER_OldTrait"] = "AFFECTER_NewTrait",
            ["UNITVISUAL_Example_OldProfile"] = "UNITVISUAL_Example_NewProfile"
        });
        var renamedSave = saveKeyRenames.Apply(
            "{\"Key\":\"AFFECTER_OldTrait\",\"Traits\":[\"AFFECTER_OldTrait\",\"AFFECTER_OldTraitMood\"],"
            + "\"ProfileKey\":\"UNITVISUAL_Example_OldProfile\",\"AFFECTER_OldTrait\":1}",
            out var renamedTokens, out var renamedPacks);
        Check(renamedSave == "{\"Key\":\"AFFECTER_NewTrait\",\"Traits\":[\"AFFECTER_NewTrait\",\"AFFECTER_OldTraitMood\"],"
                + "\"ProfileKey\":\"UNITVISUAL_Example_NewProfile\",\"AFFECTER_NewTrait\":1}"
                && renamedTokens == 4 && renamedPacks.SequenceEqual(new[] { "example-pack" }),
            "save key renames replace complete JSON strings and property names, never part of a longer key");
        const string UntouchedSave = "{\"Key\":\"AFFECTER_OtherTrait\"}";
        Check(ReferenceEquals(saveKeyRenames.Apply(UntouchedSave, out var noTokens, out var noPacks), UntouchedSave)
                && noTokens == 0 && noPacks.Count == 0,
            "a save without renamed keys is returned unchanged without copying");
        string RenameFailure(IReadOnlyDictionary<string, string> renames)
        {
            try { saveKeyRenames.Register("other-pack", renames).Dispose(); return null; }
            catch (ArgumentException ex) { return ex.Message; }
            catch (InvalidOperationException ex) { return ex.Message; }
        }
        Check(RenameFailure(new Dictionary<string, string> { ["AFFECTER Old"] = "AFFECTER_New" }) != null,
            "a save key rename with characters outside letters, digits and underscores is rejected");
        Check(RenameFailure(new Dictionary<string, string> { ["AFFECTER_Same"] = "AFFECTER_Same" }) != null,
            "a save key rename to the same key is rejected");
        var duplicateRename = RenameFailure(new Dictionary<string, string> { ["AFFECTER_OldTrait"] = "AFFECTER_Other" });
        Check(duplicateRename != null && !duplicateRename.Contains("OldTrait"),
            "another pack cannot rename an already renamed key, and the message names no key");
        Check(RenameFailure(new Dictionary<string, string> { ["AFFECTER_NewTrait"] = "AFFECTER_Third" }) != null,
            "a rename chain from an existing target is rejected");
        Check(RenameFailure(new Dictionary<string, string> { ["AFFECTER_Third"] = "AFFECTER_OldTrait" }) != null,
            "a rename chain into an existing old key is rejected");
        Check(RenameFailure(new Dictionary<string, string> { ["AFFECTER_A"] = "AFFECTER_B", ["AFFECTER_B"] = "AFFECTER_C" }) != null,
            "a rename chain within one registration is rejected");
        renameLease.Dispose();
        renameLease.Dispose();
        var afterRelease = saveKeyRenames.Apply("{\"Key\":\"AFFECTER_OldTrait\"}", out var afterTokens, out _);
        Check(saveKeyRenames.Count == 0 && afterTokens == 0 && afterRelease == "{\"Key\":\"AFFECTER_OldTrait\"}",
            "releasing the lease removes the renames, and a second release changes nothing");
    }
}
