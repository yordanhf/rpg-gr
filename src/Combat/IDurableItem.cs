namespace Combat;

/// Weapon and Armor both carry durability/repair state with identical shapes; this lets the console
/// (repair, condition) handle either kind of gear without duplicating the same logic twice.
public interface IDurableItem
{
    string Name { get; }
    int Value { get; }
    int MaxDurability { get; }
    int Durability { get; }
    bool HasBeenRepaired { get; }
    bool IsBroken { get; }
    bool Repair();
}
