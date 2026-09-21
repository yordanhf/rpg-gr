using System.Text.RegularExpressions;
using Combat;

namespace ConsoleProto;

/// Colors the 1-2 "intensity" words inside an emote line, matched by tier. Each tier has 2 authored
/// variants with different verbs (see EmoteTable.CreateStandard), so the pattern lists every verb
/// that tier's variants actually use — including its third-person "-s" form — and colors whichever
/// one shows up in a given line.
internal static class EmoteHighlighter
{
    private sealed record Rule(Regex Pattern, ConsoleColor Color);

    private static readonly Dictionary<HitTier, Rule> Rules = new()
    {
        [HitTier.Miss] = new Rule(new Regex(@"\bmiss(es)?\b", RegexOptions.IgnoreCase), ConsoleColor.White),
        [HitTier.Graze] = new Rule(new Regex(@"\b(scratch(es)?|graze(s)?)\b", RegexOptions.IgnoreCase), ConsoleColor.DarkYellow),
        [HitTier.Hit] = new Rule(new Regex(@"\b(hit(s)?|strike(s)?)\b", RegexOptions.IgnoreCase), ConsoleColor.Magenta),
        [HitTier.HitHard] = new Rule(new Regex(@"\bhard\b", RegexOptions.IgnoreCase), ConsoleColor.Red),
        [HitTier.HitVeryHard] = new Rule(new Regex(@"very hard|heavy blow", RegexOptions.IgnoreCase), ConsoleColor.DarkRed),
        [HitTier.MassiveDamage] = new Rule(new Regex(@"\b(inflict(s)?|devastate(s)?)\b", RegexOptions.IgnoreCase), ConsoleColor.Cyan),
        [HitTier.Massacre] = new Rule(new Regex(@"\b(massacre(s)?|annihilate(s)?)\b", RegexOptions.IgnoreCase), ConsoleColor.Yellow),
        [HitTier.Critical] = new Rule(new Regex(@"critical hit|deadly opening", RegexOptions.IgnoreCase), ConsoleColor.Blue),
    };

    public static void WriteLine(string text, HitTier tier)
    {
        if (!Rules.TryGetValue(tier, out var rule))
        {
            Console.WriteLine(text);
            return;
        }

        var match = rule.Pattern.Match(text);
        if (!match.Success)
        {
            Console.WriteLine(text);
            return;
        }

        Console.Write(text[..match.Index]);

        var previous = Console.ForegroundColor;
        Console.ForegroundColor = rule.Color;
        Console.Write(text.Substring(match.Index, match.Length));
        Console.ForegroundColor = previous;

        Console.WriteLine(text[(match.Index + match.Length)..]);
    }
}
