using System.Text.RegularExpressions;

namespace DungeonSettlersDelvers.Core;

// How Core's texts are laid out in the game's text table (UniqueCandidateLocalization): a number and its percent sign never
// break apart (at a line's end, "%." on the next). The game's tooltip text is TextMeshPro, so the
// pair goes into <nobr>; a space between them becomes a no-break space. Every language, every Core text. Rules without the game.
internal static class CoreTextLayout
{
    private const string NoBreakSpace = " ";

    // "+20 %", "20 %", "12,5 %": a number, one space, the percent sign.
    private static readonly Regex SpacedPercent = new(@"([+\-]?\d+(?:[.,]\d+)?)[  ]%", RegexOptions.CultureInvariant);

    // "+20%", "35%": a number right before the percent sign.
    private static readonly Regex TightPercent = new(@"([+\-]?\d+(?:[.,]\d+)?)%", RegexOptions.CultureInvariant);

    internal static string KeepUnits(string text)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf('%') < 0) return text;
        var spaced = SpacedPercent.Replace(text, "<nobr>$1" + NoBreakSpace + "%</nobr>");
        return TightPercent.Replace(spaced, "<nobr>$1%</nobr>");
    }
}
