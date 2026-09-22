namespace World;

/// A room's stock, if it has one. Buying gives the player a fresh copy — the stock itself never runs out.
public class Shop
{
    public required string Name { get; init; }
    public List<Combat.Weapon> Weapons { get; init; } = new();
    public List<Combat.Armor> Armor { get; init; } = new();
    public Combat.Backpack? Backpack { get; init; }
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
}

public class Area
{
    public required string Id { get; init; }
    public required string Name { get; init; }

    /// "world", "town", "cave", "city"... informational for now; later used to restrict where NPCs may roam.
    public required string Type { get; init; }

    public IReadOnlyList<Room> Rooms { get; init; } = Array.Empty<Room>();
}
