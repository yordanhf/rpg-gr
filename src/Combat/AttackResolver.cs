namespace Combat;

/// Resolves a single attack: miss check, then crit check, then damage. Shared by normal attacks
/// and skills, so skills only need to supply bonuses instead of re-implementing the formulas.
public static class AttackResolver
{
    private static readonly EmoteTable DefaultStandardEmotes = EmoteTable.CreateStandard();

    public static HitResult Resolve(
        Combatant attacker,
        Combatant defender,
        IRandomSource rng,
        double offenseAccuracyBonus = 0,
        double offenseDamageBonus = 0,
        EmoteTable? standardEmotes = null)
    {
        var standard = standardEmotes ?? DefaultStandardEmotes;
        var customEmotes = attacker.Weapon.CustomEmotes;

        double offenseAcc = attacker.EffectiveStat(StatType.Coordination) + attacker.EffectiveStat(StatType.Aim)
            + attacker.Weapon.EffectiveHit + offenseAccuracyBonus;
        double defenseAcc = defender.EffectiveStat(StatType.Agility) + defender.EffectiveStat(StatType.Dodge)
            + defender.TotalDeflect;
        double hitChance = offenseAcc / (offenseAcc + defenseAcc);

        if (rng.NextDouble() > hitChance)
        {
            var missVariant = PickVariant(customEmotes, standard, HitTier.Miss, rng);
            return HitResult.Miss(missVariant.Format(attacker.Name, defender.Name, attacker.IsPlayer));
        }

        // A landed hit wears down the attacker's weapon and every piece of the defender's armor,
        // flat -1 durability each regardless of tier (rule 8quinquies).
        attacker.Weapon.Degrade();
        foreach (var piece in defender.EquippedArmor)
            piece.Degrade();

        double critChance = Math.Clamp(CombatConstants.BaseCritChance + attacker.Weapon.EffectiveCritChanceBonus, 0, 1);
        bool crit = rng.NextDouble() < critChance;

        int damage = crit
            ? RollCriticalDamage(attacker.Weapon, rng)
            : RollNormalDamage(attacker, defender, offenseDamageBonus, rng);

        HitTier tier = crit ? HitTier.Critical : ClassifyNonCritTier(damage);
        var variant = PickVariant(customEmotes, standard, tier, rng);
        return new HitResult(damage, tier, variant.Format(attacker.Name, defender.Name, attacker.IsPlayer));
    }

    private static EmoteVariant PickVariant(EmoteTable? custom, EmoteTable standard, HitTier tier, IRandomSource rng) =>
        custom?.TryGetVariants(tier) != null ? custom.Pick(tier, rng) : standard.Pick(tier, rng);

    private static int RollNormalDamage(Combatant attacker, Combatant defender, double offenseDamageBonus, IRandomSource rng)
    {
        double offense = attacker.EffectiveStat(StatType.Strength) + attacker.EffectiveStat(StatType.Attack)
            + attacker.Weapon.EffectiveDamage + offenseDamageBonus;
        double defense = defender.EffectiveStat(StatType.Constitution) + defender.EffectiveStat(StatType.Defense)
            + defender.TotalAbsorb;

        // The attacker's own offense caps how hard they can ever hit, regardless of the defender.
        double potential = Math.Clamp(offense / CombatConstants.ReferenceMaxOffense * CombatConstants.MaxNonCritDamage,
            CombatConstants.MinDamage, CombatConstants.MaxNonCritDamage);

        // Defense then mitigates a fraction of that potential — same reference scale, so a defense
        // roughly equal to the offense reference cuts the hit about in half.
        double mitigation = defense / (defense + CombatConstants.ReferenceMaxOffense);
        double raw = potential * (1 - mitigation);

        // Always some randomness (rule 6.3): usually +-20%, but a small fraction of hits fall outside that band:
        // "weak" ones land anywhere below it (so low tiers stay possible however strong the attacker gets) and
        // "strong" ones land above it (so an occasional hit reaches a tier higher than the attacker's usual cap).
        double roll = rng.NextDouble();
        double jitter = roll < CombatConstants.WeakHitChance
            ? rng.NextDouble() * 0.8
            : roll < CombatConstants.WeakHitChance + CombatConstants.StrongHitChance
                ? 1.2 + rng.NextDouble() * 0.4
                : 0.8 + rng.NextDouble() * 0.4;
        int damage = (int)Math.Round(raw * jitter);
        return Math.Clamp(damage, CombatConstants.MinDamage, CombatConstants.MaxNonCritDamage);
    }

    private static int RollCriticalDamage(Weapon weapon, IRandomSource rng)
    {
        double critPos = Math.Clamp(weapon.EffectiveCritPower / 100.0, 0, 1);

        // Usually clusters around the weapon's own crit power, but ~10% of crits ignore it entirely
        // so even a top weapon can occasionally land a weak (31) critical hit.
        double effectivePos = rng.NextDouble() < 0.10
            ? rng.NextDouble()
            : Math.Clamp(critPos + (rng.NextDouble() - 0.5) * 0.3, 0, 1);

        int span = CombatConstants.MaxCritDamage - CombatConstants.MinCritDamage;
        return CombatConstants.MinCritDamage + (int)Math.Round(effectivePos * span);
    }

    private static HitTier ClassifyNonCritTier(int damage) => damage switch
    {
        <= 3 => HitTier.Graze,
        <= 7 => HitTier.Hit,
        <= 10 => HitTier.HitHard,
        <= 14 => HitTier.HitVeryHard,
        <= 19 => HitTier.MassiveDamage,
        _ => HitTier.Massacre
    };
}
