namespace Combat;

public class Skill
{
    public required string Name { get; init; }
    public required int CooldownRounds { get; init; }
    public required Func<Combatant, Combatant, IRandomSource, HitResult> Execute { get; init; }
}
