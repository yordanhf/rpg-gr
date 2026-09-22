namespace Combat;

/// Reaching a level needs BOTH enough experience and a high enough average of the 5 base stats
/// (Strength/Constitution/Agility/Coordination/Intelligence — skills don't count toward this; those
/// are trained separately and are profession-specific). The average uses raw base stats, not the
/// race-adjusted/buffed EffectiveStat, so a temporary buff can't unlock a level that then "un-levels"
/// once it wears off.
public static class Leveling
{
    /// Cumulative XP needed to BE this level (not "to go from the previous level").
    /// Level 2 = 500; each level after that is the previous threshold x1.33, per the dev's call.
    public static int ExperienceRequired(int level)
    {
        if (level <= 1)
            return 0;

        double xp = 500;
        for (int l = 3; l <= level; l++)
            xp *= 1.33;
        return (int)Math.Round(xp);
    }

    /// Average of the 5 base stats needed to reach this level: 25 at level 2, +5 for every level after.
    public static double RequiredStatAverage(int level) => level <= 1 ? 0 : 15 + 5.0 * level;
}
