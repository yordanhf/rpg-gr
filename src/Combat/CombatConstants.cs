namespace Combat;

public static class CombatConstants
{
    public const double BaseCritChance = 0.005; // 0.5%, same for everyone before weapon bonuses.
    public const double MaxEffectiveStat = 125; // stats/skills cap here once buffs are applied.
    public const double MaxArmorPoolTotal = 100; // Absorb and Deflect are each capped at this, summed across all equipped pieces.

    public const int MinDamage = 1;
    public const int MaxNonCritDamage = 30;
    /// Chance that a non-crit hit rolls "weak": damage scaled by 0-80% instead of the usual 80-120%.
    public const double WeakHitChance = 0.15;

    /// Chance that a non-crit hit rolls "strong": damage scaled by 120-160% instead of the usual 80-120%.
    public const double StrongHitChance = 0.05;

    public const int MinCritDamage = 31;
    public const int MaxCritDamage = 50;

    /// 100 Strength + 100 Attack + 100 weapon.Damage: the offense a fully-trained, well-armed
    /// attacker reaches without buffs. Non-crit damage is scaled against this so a character with
    /// low absolute stats can't reach the top tiers no matter how weak the defender is — only the
    /// offense/defense ratio moved the needle before, which let two equally weak fighters trade
    /// Massacre-tier hits.
    public const double ReferenceMaxOffense = 300;
}
