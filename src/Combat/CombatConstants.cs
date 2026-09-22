namespace Combat;

public static class CombatConstants
{
    public const double BaseCritChance = 0.005; // 0.5%, same for everyone before weapon bonuses.
    public const double MaxEffectiveStat = 125; // stats/skills cap here once buffs are applied.
    // Absorb and Deflect are each hard-capped at this, summed across all equipped pieces (shield included) —
    // same soft-100/hard-125 pattern as MaxEffectiveStat. A full set should normally land around 100;
    // 125 only matters as a safety ceiling, same as stats with buffs.
    public const double MaxArmorPoolTotal = 125;

    public const int MinDamage = 1;
    public const int MaxNonCritDamage = 30;
    /// Chance that a non-crit hit rolls "weak": damage scaled by 0-80% instead of the usual 80-120%.
    public const double WeakHitChance = 0.15;

    /// Chance that a non-crit hit rolls "strong": damage scaled by 120-160% instead of the usual 80-120%.
    public const double StrongHitChance = 0.05;

    /// A hit that leaves HP at or below 0 doesn't kill outright: the target falls bleeding, unless HP ends up
    /// this far below 0 (or more), which is instant death.
    public const int InstantDeathHp = 15;

    /// How many rounds a fallen combatant lasts before bleeding out, unless bandaged or finished off first.
    public const int BleedRounds = 10;

    /// Fraction of MaxHp a bandaged combatant gets back.
    public const double BandageHealFraction = 0.10;

    public const int MinCritDamage = 31;
    public const int MaxCritDamage = 50;

    /// 100 Strength + 100 Attack + 100 weapon.Damage: the offense a fully-trained, well-armed
    /// attacker reaches without buffs. Non-crit damage is scaled against this so a character with
    /// low absolute stats can't reach the top tiers no matter how weak the defender is — only the
    /// offense/defense ratio moved the needle before, which let two equally weak fighters trade
    /// Massacre-tier hits.
    public const double ReferenceMaxOffense = 300;

    /// Every weapon/armor piece starts with this many durability points and loses 1 per hit it's
    /// involved in (landed for a weapon, taken for armor) — flat, regardless of the hit's tier.
    public const int MaxDurability = 1000;

    /// Bulk-units of hand space available for wielding a weapon (a 2-handed weapon uses all of it).
    public const double HandCapacity = 2;
}
