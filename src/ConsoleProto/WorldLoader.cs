using System.Text.Json;
using System.Text.Json.Serialization;
using World;

namespace ConsoleProto;

/// Reads every data/areas/*.json (one file per area, rooms inside) into a WorldMap.
internal static class WorldLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };
    private static string AreasDir => Path.Combine(AppContext.BaseDirectory, "data", "areas");

    public static WorldMap Load()
    {
        var areas = Directory.GetFiles(AreasDir, "*.json")
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .Select(file =>
            {
                var dto = JsonSerializer.Deserialize<AreaFile>(File.ReadAllText(file), Options)
                    ?? throw new InvalidDataException($"Could not load area from {file}");
                return ToArea(dto, file);
            })
            .ToList();

        return new WorldMap(areas);
    }

    private static Area ToArea(AreaFile file, string path) => new()
    {
        Id = file.Id,
        Name = file.Name,
        Type = file.Type,
        Rooms = (file.Rooms ?? new()).Select(r => new Room
        {
            Id = $"{file.Id}.{r.Id}",
            AreaId = file.Id,
            Name = r.Name,
            Description = r.Description,
            Exits = ParseExits(file.Id, r, path),
            Details = r.Details ?? new(),
            Spawns = r.Spawns ?? new(),
            IsStart = r.Start,
            Shop = r.Shop,
            Healer = r.Healer,
            Trainer = r.Trainer
        }).ToList()
    };

    // An exit target without a dot is a room of the same area; "area.room" reaches into another area.
    private static Dictionary<Direction, string> ParseExits(string areaId, RoomFile room, string path)
    {
        var exits = new Dictionary<Direction, string>();
        foreach (var (name, target) in room.Exits ?? new())
        {
            if (!Directions.TryParse(name, out var direction))
                throw new InvalidDataException($"Unknown exit direction '{name}' in room '{room.Id}' ({path}).");

            exits[direction] = target.Contains('.') ? target : $"{areaId}.{target}";
        }
        return exits;
    }

    private record AreaFile(string Id, string Name, string Type, List<RoomFile>? Rooms);

    private record RoomFile(
        string Id,
        string Name,
        string Description,
        Dictionary<string, string>? Exits,
        Dictionary<string, string>? Details,
        List<string>? Spawns,
        bool Start,
        Shop? Shop,
        Healer? Healer,
        Trainer? Trainer);
}
