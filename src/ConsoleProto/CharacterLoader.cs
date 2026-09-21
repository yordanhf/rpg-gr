using System.Text.Json;
using Combat;

namespace ConsoleProto;

internal static class CharacterLoader
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };
    private static string DataDir => Path.Combine(AppContext.BaseDirectory, "data");

    public static Combatant LoadPlayer() => LoadFrom(Path.Combine(DataDir, "player.json"));

    public static Combatant? LoadNpc(string id)
    {
        var path = Path.Combine(DataDir, "npcs", $"{id.ToLowerInvariant()}.json");
        return File.Exists(path) ? LoadFrom(path) : null;
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
}
