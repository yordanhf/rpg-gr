namespace World;

/// The static structure of the world: every room of every area, indexed by global id.
public class WorldMap
{
    private readonly Dictionary<string, Room> _rooms = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<Area> Areas { get; }
    public Room StartRoom { get; }

    public WorldMap(IEnumerable<Area> areas)
    {
        Areas = areas.ToList();

        foreach (var room in Areas.SelectMany(a => a.Rooms))
        {
            if (!_rooms.TryAdd(room.Id, room))
                throw new InvalidDataException($"Duplicate room id '{room.Id}'.");
        }

        // A dangling exit would strand the player, so it's caught at load time instead of when someone walks into it.
        foreach (var room in _rooms.Values)
        {
            foreach (var (direction, targetId) in room.Exits)
            {
                if (!_rooms.ContainsKey(targetId))
                    throw new InvalidDataException($"Room '{room.Id}' has a {direction.Label()} exit to unknown room '{targetId}'.");
            }
        }

        var starts = _rooms.Values.Where(r => r.IsStart).ToList();
        if (starts.Count != 1)
            throw new InvalidDataException($"Exactly one room must be marked as start, found {starts.Count}.");
        StartRoom = starts[0];
    }

    public Room? GetRoom(string id) => _rooms.GetValueOrDefault(id);

    /// The room you'd end up in by leaving `from` towards `direction`, or null if there's no exit that way.
    public Room? GetExit(Room from, Direction direction) =>
        from.Exits.TryGetValue(direction, out var targetId) ? _rooms[targetId] : null;
}
