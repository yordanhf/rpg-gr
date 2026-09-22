using Combat;

namespace World;

/// Who is where right now. The static layout lives in WorldMap; the state of each NPC (HP, bleeding...) stays in
/// Combatant, so the world only needs to know which room each one is standing in.
public class WorldState
{
    private readonly Func<string, Combatant?> _createNpc;
    private readonly Dictionary<string, List<Combatant>> _occupants = new(StringComparer.OrdinalIgnoreCase);

    public WorldMap Map { get; }

    /// `createNpc` builds a fresh NPC from its id (the console loads data/npcs/<id>.json).
    public WorldState(WorldMap map, Func<string, Combatant?> createNpc)
    {
        Map = map;
        _createNpc = createNpc;

        foreach (var room in map.Areas.SelectMany(a => a.Rooms))
        {
            foreach (var npcId in room.Spawns)
                Spawn(room.Id, npcId);
        }
    }

    /// A snapshot: NPCs die and respawn from background threads while commands read the room.
    public IReadOnlyList<Combatant> NpcsIn(Room room)
    {
        lock (_occupants)
            return _occupants.TryGetValue(room.Id, out var list) ? list.ToArray() : Array.Empty<Combatant>();
    }

    /// "rat" matches an NPC with id "rat" and the name "a rat".
    public static bool Matches(Combatant npc, string query) =>
        npc.Id.Equals(query, StringComparison.OrdinalIgnoreCase)
        || npc.Name.Contains(query, StringComparison.OrdinalIgnoreCase);

    public Combatant? FindNpc(Room room, string query) => NpcsIn(room).FirstOrDefault(n => Matches(n, query));

    public Combatant? Spawn(string roomId, string npcId)
    {
        var npc = _createNpc(npcId);
        if (npc == null)
            return null;

        lock (_occupants)
        {
            if (!_occupants.TryGetValue(roomId, out var list))
                _occupants[roomId] = list = new List<Combatant>();
            list.Add(npc);
        }
        return npc;
    }

    /// Takes the NPC out of the world (it died). Returns the room it was in, or null if it wasn't anywhere.
    public string? Remove(Combatant npc)
    {
        lock (_occupants)
        {
            foreach (var (roomId, list) in _occupants)
            {
                if (list.Remove(npc))
                    return roomId;
            }
        }
        return null;
    }
}
