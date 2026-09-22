using Combat;

namespace World;

/// Who is where right now. The static layout lives in WorldMap; the state of each NPC (HP, bleeding...) stays in
/// Combatant, so the world only needs to know which room each one is standing in.
public class WorldState
{
    private readonly Func<string, Combatant?> _createNpc;
    private readonly Dictionary<string, List<Combatant>> _occupants = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<Corpse>> _corpses = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<Item>> _groundItems = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<PendingRespawn> _pendingRespawns = new();

    private class PendingRespawn
    {
        public required string RoomId { get; init; }
        public required string NpcId { get; init; }
        public int TicksRemaining { get; set; }
    }

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

    public IReadOnlyList<Corpse> CorpsesIn(Room room)
    {
        lock (_corpses)
            return _corpses.TryGetValue(room.Id, out var list) ? list.ToArray() : Array.Empty<Corpse>();
    }

    public IReadOnlyList<Item> ItemsOnGround(Room room)
    {
        lock (_groundItems)
            return _groundItems.TryGetValue(room.Id, out var list) ? list.ToArray() : Array.Empty<Item>();
    }

    public void AddCorpse(string roomId, Corpse corpse)
    {
        lock (_corpses)
        {
            if (!_corpses.TryGetValue(roomId, out var list))
                _corpses[roomId] = list = new List<Corpse>();
            list.Add(corpse);
        }
    }

    /// Called when a corpse fades: takes it off the room, its remaining loot spills onto the floor
    /// (open to anyone — loot rights only apply to the corpse itself).
    public void FadeCorpse(string roomId, Corpse corpse)
    {
        lock (_corpses)
        {
            if (_corpses.TryGetValue(roomId, out var list))
                list.Remove(corpse);
        }

        if (corpse.Loot.Count > 0)
            DropItems(roomId, corpse.Loot);
    }

    public void DropItems(string roomId, IEnumerable<Item> items)
    {
        lock (_groundItems)
        {
            if (!_groundItems.TryGetValue(roomId, out var list))
                _groundItems[roomId] = list = new List<Item>();
            list.AddRange(items);
        }
    }

    public void RemoveFromGround(string roomId, Item item)
    {
        lock (_groundItems)
        {
            if (_groundItems.TryGetValue(roomId, out var list))
                list.Remove(item);
        }
    }

    /// An NPC doesn't respawn the instant its timer runs out — it waits for the next world tick
    /// (every 10 minutes, see the caller), snapped to whichever tick reaches 0 first.
    public void ScheduleRespawn(string roomId, string npcId, int ticks)
    {
        lock (_pendingRespawns)
            _pendingRespawns.Add(new PendingRespawn { RoomId = roomId, NpcId = npcId, TicksRemaining = Math.Max(1, ticks) });
    }

    /// Call once per world tick: advances every pending respawn and spawns whichever are now due.
    public void AdvanceRespawnTick()
    {
        List<PendingRespawn> due;
        lock (_pendingRespawns)
        {
            foreach (var pending in _pendingRespawns)
                pending.TicksRemaining--;

            due = _pendingRespawns.Where(p => p.TicksRemaining <= 0).ToList();
            foreach (var d in due)
                _pendingRespawns.Remove(d);
        }

        foreach (var d in due)
            Spawn(d.RoomId, d.NpcId);
    }
}
