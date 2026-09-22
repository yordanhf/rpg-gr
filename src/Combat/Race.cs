namespace Combat;

/// Percentage modifiers applied to a combatant's base stats (not skills). +10 means base * 1.10, -5 means base * 0.95.
/// Each race's modifiers sum to 0 so no race is strictly better than another. Applied before flat buffs/debuffs.
public class Race
{
    /// No modifiers at all — also the fallback for a combatant with no race set.
    public static readonly Race None = new() { Name = "None" };

    /// Data id (the file name in data/races), set by the loader. Used to store a character's race.
    public string Id { get; set; } = "";

    public required string Name { get; init; }

    /// Flavor text shown at character creation. Hints at strengths and weaknesses without showing numbers.
    public string Description { get; init; } = "";

    /// False for NPC-only races (e.g. creature): hidden from character creation.
    public bool Playable { get; init; } = true;

    public double StrengthPercent { get; init; }
    public double ConstitutionPercent { get; init; }
    public double AgilityPercent { get; init; }
    public double CoordinationPercent { get; init; }
    public double IntelligencePercent { get; init; }

    /// Skills (Aim, Attack, Defense, Dodge) are trained, so races never modify them.
    public double PercentFor(StatType stat) => stat switch
    {
        StatType.Strength => StrengthPercent,
        StatType.Constitution => ConstitutionPercent,
        StatType.Agility => AgilityPercent,
        StatType.Coordination => CoordinationPercent,
        StatType.Intelligence => IntelligencePercent,
        _ => 0
    };
}
