namespace Combat;

/// A buff or debuff: a temporary flat bonus/penalty to one stat or skill, lasting a number of rounds.
public class StatModifier
{
    public required StatType Stat { get; init; }
    public required double Amount { get; init; }
    public int RemainingRounds { get; set; }
}
