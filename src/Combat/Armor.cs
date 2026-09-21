namespace Combat;

public class Armor
{
    public required string Name { get; init; }

    /// Adds to the defender's side of the damage formula (alongside Constitution + Defense).
    public required double Absorb { get; init; }

    /// Adds to the defender's side of the accuracy formula (alongside Agility + Dodge).
    public required double Deflect { get; init; }
}
