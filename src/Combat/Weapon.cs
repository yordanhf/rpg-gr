namespace Combat;

public class Weapon
{
    public required string Name { get; init; }

    /// 0-100. Adds to the attacker's side of the accuracy formula (alongside Coordination + Aim).
    public required double Hit { get; init; }

    /// 0-100. Adds to the attacker's side of the non-crit damage formula (alongside Strength + Attack).
    public required double Damage { get; init; }

    /// Fraction added to CombatConstants.BaseCritChance, e.g. 0.001-0.005 for 0.1%-0.5%.
    public required double CritChanceBonus { get; init; }

    /// 0-100. Where in the 31-50 crit damage range this weapon usually lands.
    public required double CritPower { get; init; }

    public EmoteTable? CustomEmotes { get; init; }

    public int MaxDurability { get; init; } = CombatConstants.MaxDurability;
    public int Durability { get; private set; } = CombatConstants.MaxDurability;

    /// A weapon can only ever be mended once — the blacksmith won't touch it again after that.
    public bool HasBeenRepaired { get; private set; }

    public bool IsBroken => Durability <= 0;

    // A broken weapon can still be wielded, it's just useless in a fight.
    public double EffectiveHit => IsBroken ? 0 : Hit;
    public double EffectiveDamage => IsBroken ? 0 : Damage;
    public double EffectiveCritChanceBonus => IsBroken ? 0 : CritChanceBonus;
    public double EffectiveCritPower => IsBroken ? 0 : CritPower;

    public void Degrade()
    {
        if (Durability > 0)
            Durability--;
    }

    /// Sets durability/repair state read back from a save file.
    public void RestoreDurability(int durability, bool hasBeenRepaired)
    {
        Durability = Math.Clamp(durability, 0, MaxDurability);
        HasBeenRepaired = hasBeenRepaired;
    }

    /// Returns false (refuses) if this weapon has already been repaired once before.
    public bool Repair()
    {
        if (HasBeenRepaired)
            return false;

        Durability = MaxDurability;
        HasBeenRepaired = true;
        return true;
    }
}
