using System.Text.RegularExpressions;
using Combat;

namespace ConsoleProto;

/// Colors the 1-2 "intensity" words inside an emote line, matched by tier. Each tier has 2 authored
/// variants with different verbs (see EmoteTable.CreateStandard), so the pattern lists every verb
/// that tier's variants actually use and colors whichever one shows up in a given line.
internal static class EmoteHighlighter
{
    private sealed record Rule(Regex Pattern, ConsoleColor? Color, (byte R, byte G, byte B)? Rgb);

    private static readonly Dictionary<HitTier, Rule> Rules = new()
    {
        [HitTier.Miss] = new Rule(new Regex(@"\bmiss(es)?\b", RegexOptions.IgnoreCase), ConsoleColor.White, null),
        [HitTier.Graze] = new Rule(new Regex(@"\b(scratch(es)?|graze(s)?)\b", RegexOptions.IgnoreCase), ConsoleColor.DarkYellow, null),
        [HitTier.Hit] = new Rule(new Regex(@"\b(hit(s)?|strike(s)?)\b", RegexOptions.IgnoreCase), ConsoleColor.Magenta, null),
        [HitTier.HitHard] = new Rule(new Regex(@"\bhard\b", RegexOptions.IgnoreCase), ConsoleColor.Red, null),
        // "Carmelita oscuro" has no standard ConsoleColor equivalent, so this one uses a true RGB ANSI escape instead.
        [HitTier.HitVeryHard] = new Rule(new Regex(@"very hard|heavy blow", RegexOptions.IgnoreCase), null, ((byte)92, (byte)64, (byte)51)),
        [HitTier.MassiveDamage] = new Rule(new Regex(@"\b(inflict|devastate)\b", RegexOptions.IgnoreCase), ConsoleColor.Cyan, null),
        [HitTier.Massacre] = new Rule(new Regex(@"\b(massacre|annihilate)\b", RegexOptions.IgnoreCase), ConsoleColor.Yellow, null),
        [HitTier.Critical] = new Rule(new Regex(@"critical hit|deadly opening", RegexOptions.IgnoreCase), ConsoleColor.Blue, null),
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
        WriteColored(text.Substring(match.Index, match.Length), rule);
        Console.WriteLine(text[(match.Index + match.Length)..]);
    }

    private static void WriteColored(string span, Rule rule)
    {
        if (rule.Rgb is { } rgb)
        {
            Console.Write($"\x1b[38;2;{rgb.R};{rgb.G};{rgb.B}m{span}\x1b[0m");
            return;
        }

        var previous = Console.ForegroundColor;
        Console.ForegroundColor = rule.Color!.Value;
        Console.Write(span);
        Console.ForegroundColor = previous;
    }
}
