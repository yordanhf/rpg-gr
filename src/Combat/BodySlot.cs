namespace Combat;

/// Where a piece of armor is worn. A single piece can cover more than one slot at once
/// (e.g. mail pants that include the boots would cover Legs and Feet).
/// Shield is special: unlike the other five, it still costs hand space even while equipped
/// (see Combatant.UsedHandBulk) — you have to actively hold it up, not just strap it on.
public enum BodySlot { Head, Torso, Arms, Legs, Feet, Shield }
