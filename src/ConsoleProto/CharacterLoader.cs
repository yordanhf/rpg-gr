using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Combat;

namespace ConsoleProto;

internal static class CharacterLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new RaceByIdConverter(), new ProfessionByIdConverter(), new JsonStringEnumConverter() }
    };

    // Race/profession files describe the thing itself, so they must not go through the id converters.
    private static readonly JsonSerializerOptions RaceOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly JsonSerializerOptions ProfessionOptions = new() { PropertyNameCaseInsensitive = true };

    private static string DataDir => Path.Combine(AppContext.BaseDirectory, "data");

    /// Builds a new player from the base stats in data/player.json, with the chosen name and race.
    public static Combatant CreatePlayer(string name, string raceId)
    {
        var node = JsonNode.Parse(File.ReadAllText(Path.Combine(DataDir, "player.json")))!.AsObject();
        node["name"] = name;
        node["race"] = raceId;

        var player = node.Deserialize<Combatant>(Options)
            ?? throw new InvalidDataException("Could not create player from data/player.json");
        player.ResetHp();
        player.ResetMp();
        return player;
    }

    /// A fresh copy of the game's default unarmed weapon (Fists), read from data/player.json.
    /// Used to re-arm a returning character bare-handed — see CharacterSave's persistence note.
    public static Weapon CreateStarterWeapon()
    {
        var node = JsonNode.Parse(File.ReadAllText(Path.Combine(DataDir, "player.json")))!.AsObject();
        return node["weapon"].Deserialize<Weapon>(Options)
            ?? throw new InvalidDataException("Could not read the starter weapon from data/player.json");
    }

    /// Races a new character can pick (id = file name), alphabetical.
    public static IReadOnlyList<(string Id, Race Race)> ListPlayableRaces()
    {
        var dir = Path.Combine(DataDir, "races");
        if (!Directory.Exists(dir))
            return Array.Empty<(string, Race)>();

        return Directory.GetFiles(dir, "*.json")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .Select(id => (Id: id, Race: GetRace(id)))
            .Where(r => r.Race.Playable)
            .ToList();
    }

    public static Combatant? LoadNpc(string id)
    {
        var path = Path.Combine(DataDir, "npcs", $"{id.ToLowerInvariant()}.json");
        if (!File.Exists(path))
            return null;

        var npc = LoadFrom(path);
        npc.Id = id.ToLowerInvariant();
        return npc;
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

    public static Race GetRace(string id)
    {
        var path = Path.Combine(DataDir, "races", $"{id.ToLowerInvariant()}.json");
        if (!File.Exists(path))
            throw new InvalidDataException($"Unknown race '{id}' (expected {path})");

        var race = JsonSerializer.Deserialize<Race>(File.ReadAllText(path), RaceOptions)
            ?? throw new InvalidDataException($"Could not load race from {path}");
        race.Id = id.ToLowerInvariant();
        return race;
    }

    public static Profession GetProfession(string id)
    {
        var path = Path.Combine(DataDir, "professions", $"{id.ToLowerInvariant()}.json");
        if (!File.Exists(path))
            throw new InvalidDataException($"Unknown profession '{id}' (expected {path})");

        var profession = JsonSerializer.Deserialize<Profession>(File.ReadAllText(path), ProfessionOptions)
            ?? throw new InvalidDataException($"Could not load profession from {path}");
        profession.Id = id.ToLowerInvariant();
        return profession;
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
            GetRace(reader.GetString() ?? throw new JsonException("Race id must be a string."));

        public override void Write(Utf8JsonWriter writer, Race value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.Name.ToLowerInvariant());
    }

    /// Character JSON refers to a profession by id ("profession": "civilian"); resolves it to
    /// data/professions/civilian.json.
    private sealed class ProfessionByIdConverter : JsonConverter<Profession>
    {
        public override Profession Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            GetProfession(reader.GetString() ?? throw new JsonException("Profession id must be a string."));

        public override void Write(Utf8JsonWriter writer, Profession value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.Id);
    }
}
