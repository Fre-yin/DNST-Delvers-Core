using HarmonyLib;

namespace DungeonSettlersDelvers.Core;

// Tooltip plan, step 0: the game's own skill, affecter and item descriptions as
// they stand in the text table, before Tooltip_Base.ApplyTooltipFormatText fills {n} from the row's FloatValues and
// StringTools.FormatText turns [KEY] into a yellow <color=#E1B638><link="KEY"> term (#b(...)# only as the
// text of a missing key). So the markup of "130% × Magic Attack" is
// read, not guessed. Once a session, in the language just parsed, at most PerKind lines per kind; only texts that hold a
// placeholder or a bracketed term. CORE_TEXT_PROBE kind= key= text="...". The patch class below carries the group Text and is
// patched only while that group is on (CoreProbes); with the group off the text table is never read.
internal static class TooltipTextProbe
{
    internal const int PerKind = 25;
    private static bool done;

    // Which kind a text key is ("skill", "affecter", "item", "word") or null for one we do not log.
    internal static string KindOf(string key)
    {
        if (key == null || !key.StartsWith("TEXTKEY_", StringComparison.Ordinal)) return null;
        if (key.StartsWith("TEXTKEY_UI_ToolTip_Word_", StringComparison.Ordinal)) return "word";
        if (!key.EndsWith("_DESC", StringComparison.Ordinal) || key.Contains("DelversCore") || key.Contains("Delvers")) return null;
        if (key.StartsWith("TEXTKEY_SKILL_", StringComparison.Ordinal)) return "skill";
        if (key.StartsWith("TEXTKEY_AFFECTER_", StringComparison.Ordinal)) return "affecter";
        return key.StartsWith("TEXTKEY_ITEM_", StringComparison.Ordinal) ? "item" : null;
    }

    internal static bool HasMarkup(string text) => text != null && (text.IndexOf('[') >= 0 || text.IndexOf('{') >= 0 || text.IndexOf('#') >= 0);

    internal static string Line(string kind, string key, string text)
        => "CORE_TEXT_PROBE kind=" + kind + " key=" + key + " text=\"" + (text ?? "").Replace("\n", "\\n").Replace("\"", "'") + "\"";

    internal static void Run(TextSheet sheet)
    {
        var table = sheet?._textTable;
        if (done || table == null) return;
        done = true;
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var lines = 0;
        var e = table.GetEnumerator();
        while (e.MoveNext())
        {
            var key = e.Current.Key;
            var kind = KindOf(key);
            if (kind == null) continue;
            var text = e.Current.Value;
            if (kind != "word" && !HasMarkup(text)) continue;
            counts.TryGetValue(kind, out var n);
            if (n >= PerKind) continue;
            counts[kind] = n + 1;
            lines++;
            DelversHost.Info(Line(kind, key, text));
        }
        DelversHost.Info("CORE_TEXT_PROBE_DONE lines=" + lines);
    }
}

[ProbeGroup(CoreProbeRules.Text)]
[HarmonyPatch(typeof(TextSheet), nameof(TextSheet.ParseMatchingLanguage))]
internal static class TooltipTextProbePatch
{
    private static void Postfix(TextSheet __instance)
    {
        try { TooltipTextProbe.Run(__instance); }
        catch (Exception ex) { DelversHost.Warning("CORE_TEXT_PROBE_FAILED: " + ex.Message); }
    }
}
