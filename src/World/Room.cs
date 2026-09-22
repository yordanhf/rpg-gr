namespace World;

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
}

public class Area
{
    public required string Id { get; init; }
    public required string Name { get; init; }

    /// "world", "town", "cave", "city"... informational for now; later used to restrict where NPCs may roam.
    public required string Type { get; init; }

    public IReadOnlyList<Room> Rooms { get; init; } = Array.Empty<Room>();
}
