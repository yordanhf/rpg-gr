namespace Combat;

public static class CombatConstants
{
    public const double BaseCritChance = 0.005; // 0.5%, same for everyone before weapon bonuses.
    public const double MaxEffectiveStat = 125; // stats/skills cap here once buffs are applied.
    public const double MaxArmorPoolTotal = 100; // Absorb and Deflect are each capped at this, summed across all equipped pieces.

    public const int MinDamage = 1;
    public const int MaxNonCritDamage = 30;
    public const int MinCritDamage = 31;
    public const int MaxCritDamage = 50;
}
