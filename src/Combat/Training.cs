namespace Combat;

/// Gold cost to train a stat or skill up by hand, point by point, at a trainer. Every point costs
/// more once the value crosses into the next bracket of 10 — see CLAUDE.md for the numbers, they're
/// a first pass the dev already flagged as likely too steep at the high end.
public static class Training
{
    private const int BracketSize = 10;
    private const double BaseCostPerPoint = 10;
    private const double GrowthPerBracket = 1.33;

    /// Gold cost of buying the Nth point of a stat/skill (1-indexed: the 1st point takes it from 0 to
    /// 1, the 11th from 10 to 11...). Depends only on which bracket of 10 that point falls in.
    public static int CostForPoint(int pointNumber)
    {
        int bracket = (pointNumber - 1) / BracketSize;
        return (int)Math.Round(BaseCostPerPoint * Math.Pow(GrowthPerBracket, bracket));
    }

    /// Total gold to train from `from` up to `to` (both whole numbers, `to` > `from`).
    public static int CostForRange(int from, int to)
    {
        int total = 0;
        for (int point = from + 1; point <= to; point++)
            total += CostForPoint(point);
        return total;
    }

    /// The highest a stat/skill can be trained to at this level — 3 levels' worth of headroom above
    /// what the current level actually requires (Leveling.RequiredStatAverage, evaluated 3 levels up).
    /// E.g. at level 1 that's 30 + 5*1 = 35.
    public static int MaxTrainableValue(int level) => 30 + 5 * level;
}
