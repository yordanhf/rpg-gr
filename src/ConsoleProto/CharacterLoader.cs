using System.Text.Json;
using System.Text.Json.Serialization;
using Combat;

namespace ConsoleProto;

internal static class CharacterLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new RaceByIdConverter() }
    };

    // Race files describe the race itself, so they must not go through the id-to-race converter.
    private static readonly JsonSerializerOptions RaceOptions = new() { PropertyNameCaseInsensitive = true };

    private static string DataDir => Path.Combine(AppContext.BaseDirectory, "data");

    public static Combatant LoadPlayer() => LoadFrom(Path.Combine(DataDir, "player.json"));

    public static Combatant? LoadNpc(string id)
    {
        var path = Path.Combine(DataDir, "npcs", $"{id.ToLowerInvariant()}.json");
        return File.Exists(path) ? LoadFrom(path) : null;
    }

    public static IReadOnlyList<string> ListNpcIds()
    {
        var dir = Path.Combine(DataDir, "npcs");
        if (!Directory.Exists(dir))
            return Array.Empty<string>();

        return Directory.GetFiles(dir, "*.json")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Race LoadRace(string id)
    {
        var path = Path.Combine(DataDir, "races", $"{id.ToLowerInvariant()}.json");
        if (!File.Exists(path))
            throw new InvalidDataException($"Unknown race '{id}' (expected {path})");

        return JsonSerializer.Deserialize<Race>(File.ReadAllText(path), RaceOptions)
            ?? throw new InvalidDataException($"Could not load race from {path}");
    }

    private static Combatant LoadFrom(string path)
    {
        var json = File.ReadAllText(path);
        var combatant = JsonSerializer.Deserialize<Combatant>(json, Options)
            ?? throw new InvalidDataException($"Could not load character from {path}");
        combatant.ResetHp();
        combatant.ResetMp();
        return combatant;
    }

    /// Character JSON refers to a race by id ("race": "orc"); this resolves it to data/races/orc.json.
    private sealed class RaceByIdConverter : JsonConverter<Race>
    {
        public override Race Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            LoadRace(reader.GetString() ?? throw new JsonException("Race id must be a string."));

        public override void Write(Utf8JsonWriter writer, Race value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.Name.ToLowerInvariant());
    }
}
