using Combat;

namespace World;

/// What's left in a room for a while after an NPC dies. Holds its loot until it fades (see
/// WorldState) — only someone in Combatant.LootRights may take from it before then.
public class Corpse
{
    public required string Name { get; init; }
    public required List<Item> Loot { get; init; }
    public required HashSet<string> LootRights { get; init; }

    public bool CanLoot(string playerName) => LootRights.Contains(playerName);
}
