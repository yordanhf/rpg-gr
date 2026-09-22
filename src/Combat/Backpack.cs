namespace Combat;

/// Bought at a shop — a character starts without one. Worn on the back: doesn't cost hand space
/// and doesn't count against its own capacity. Only one can be carried at a time.
public class Backpack
{
    public required string Name { get; init; }

    /// Total bulk it can hold (minimum 2 for the starter backpack; bigger ones may exist later).
    public required double Capacity { get; init; }

    /// Gold price at a shop.
    public int Value { get; init; }
}
