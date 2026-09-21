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
}
