namespace World;

/// A room's stock, if it has one. Buying gives the player a fresh copy — the stock itself never runs out.
public class Shop
{
    public required string Name { get; init; }
    public List<Combat.Weapon> Weapons { get; init; } = new();
    public List<Combat.Armor> Armor { get; init; } = new();
    public Combat.Backpack? Backpack { get; init; }
}

/// A room with a healer. Each use is a flat CombatConstants.HealCostGold for
/// CombatConstants.HealAmountPerUse HP *and* the same amount of MP — not "heal me to full", just one
/// dose at a time. The healer only has so much in them per world tick (CombatConstants.
/// HealerCapacityPerTick, shared across everyone using this healer) — once spent, they need the next
/// tick to recover, same 10-minute heartbeat as NPC respawns (see Program.cs).
public class Healer
{
    public required string Name { get; init; }

    private int _capacityRemaining = Combat.CombatConstants.HealerCapacityPerTick;

    /// Spends `amount` of capacity if there's enough left. Returns false (refuses, nothing changes)
    /// if the healer is tapped out for this tick.
    public bool TryDispense(int amount)
    {
        if (amount > _capacityRemaining)
            return false;

        _capacityRemaining -= amount;
        return true;
    }

    /// Called once per world tick.
    public void ResetCapacity() => _capacityRemaining = Combat.CombatConstants.HealerCapacityPerTick;
}

/// A room with a trainer: raises any of the 9 base stats/skills for gold (Combat.Training), and is
/// also the only place a character can actually apply a level-up (Combatant.TryLevelUp) once they
/// qualify — leveling doesn't happen automatically during play. Trains "civilian" only for now;
/// ProfessionId is here so profession-specific trainers can reuse this same class later.
public class Trainer
{
    public required string Name { get; init; }
    public string ProfessionId { get; init; } = "civilian";
}

/// A room with a blacksmith: mends a weapon or armor piece's durability back to full for gold, priced
/// by how much is missing (see CombatConstants.RepairCostFraction). Each item can only ever be mended
/// once in its life (Weapon/Armor.HasBeenRepaired) — the blacksmith refuses a second time.
public class Blacksmith
{
    public required string Name { get; init; }
}

/// A room with somewhere to permanently discard an unwanted item (`drop <item> into trash`). No
/// capacity, no getting it back — Program.cs asks for confirmation before actually destroying anything.
public class TrashCan
{
    public required string Name { get; init; }
}

public class Room
{
    /// Global id: "<areaId>.<roomId>", e.g. "world.millford_gate". Exits reference other rooms by this id.
    public required string Id { get; init; }
    public required string AreaId { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }

    /// Direction -> destination room id (possibly in another area: that's how a town entrance works).
    public IReadOnlyDictionary<Direction, string> Exits { get; init; } = new Dictionary<Direction, string>();

    /// Things you can `look <key>` at in this room (signs, fountains, the town map...).
    public IReadOnlyDictionary<string, string> Details { get; init; } = new Dictionary<string, string>();

    /// NPC ids (data/npcs/<id>.json) that live in this room and respawn here.
    public IReadOnlyList<string> Spawns { get; init; } = Array.Empty<string>();

    /// Where a newly created character appears.
    public bool IsStart { get; init; }

    /// Set if this room has a shop; null otherwise.
    public Shop? Shop { get; init; }

    /// Set if this room has a healer; null otherwise.
    public Healer? Healer { get; init; }

    /// Set if this room has a trainer; null otherwise.
    public Trainer? Trainer { get; init; }

    /// Set if this room has a blacksmith; null otherwise.
    public Blacksmith? Blacksmith { get; init; }

    /// Set if this room has a trash can; null otherwise.
    public TrashCan? Trash { get; init; }
}

public class Area
{
    public required string Id { get; init; }
    public required string Name { get; init; }

    /// "world", "town", "cave", "city"... informational for now; later used to restrict where NPCs may roam.
    public required string Type { get; init; }

    public IReadOnlyList<Room> Rooms { get; init; } = Array.Empty<Room>();
}
