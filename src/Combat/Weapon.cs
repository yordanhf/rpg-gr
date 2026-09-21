namespace Combat;

public class Weapon
{
    public required string Name { get; init; }
    public required int MinDamage { get; init; }
    public required int MaxDamage { get; init; }
    public EmoteTable? CustomEmotes { get; init; }
}
