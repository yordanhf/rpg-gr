namespace Combat;

/// A generic carryable that isn't a weapon or armor — loot like pelts, fangs, or other trade goods.
public class Item
{
    public required string Name { get; init; }

    /// Space it takes in a backpack (or in hand while carried) — can be fractional (e.g. 0.5, 0.25).
    public required double Bulk { get; init; }

    /// Base gold value, used by shops later.
    public int Value { get; init; }
}
