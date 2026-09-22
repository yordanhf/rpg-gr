namespace Combat;

public class Armor
{
    public required string Name { get; init; }

    /// Adds to the defender's side of the damage formula (alongside Constitution + Defense).
    public required double Absorb { get; init; }

    /// Adds to the defender's side of the accuracy formula (alongside Agility + Dodge).
    public required double Deflect { get; init; }

    /// Which body slot(s) it occupies when worn — some pieces cover more than one (e.g. mail
    /// pants that include the boots cover Legs and Feet).
    public required HashSet<BodySlot> Slots { get; init; }

    /// Space it takes in a backpack, or in hand while carried unworn (can be fractional).
    public required double Bulk { get; init; }

    public int MaxDurability { get; init; } = CombatConstants.MaxDurability;
    public int Durability { get; private set; } = CombatConstants.MaxDurability;

    /// A piece can only ever be mended once — the blacksmith won't touch it again after that.
    public bool HasBeenRepaired { get; private set; }

    public bool IsBroken => Durability <= 0;

    // Broken armor can still be worn, it's just useless.
    public double EffectiveAbsorb => IsBroken ? 0 : Absorb;
    public double EffectiveDeflect => IsBroken ? 0 : Deflect;

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

    /// Returns false (refuses) if this piece has already been repaired once before.
    public bool Repair()
    {
        if (HasBeenRepaired)
            return false;

        Durability = MaxDurability;
        HasBeenRepaired = true;
        return true;
    }
}
