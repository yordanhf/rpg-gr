using System.Text.Json;
using World;

namespace ConsoleProto;

/// One JSON file per character in the user's application-data folder (outside the repo and the build output,
/// so a rebuild or a clean never deletes anybody's progress).
internal sealed class JsonCharacterStore : ICharacterStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public string Directory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RpgGr", "saves");

    public bool Exists(string name) => PathFor(name) is { } path && File.Exists(path);

    /// Null both when there's no such save and when the file exists but won't parse (an incompatible
    /// or corrupted save) — either way there's nothing usable to load, and the caller already has a
    /// message for "no such character" that covers both without crashing the whole program.
    public CharacterSave? Load(string name)
    {
        var path = PathFor(name);
        if (path == null || !File.Exists(path))
            return null;

        try
        {
            return JsonSerializer.Deserialize<CharacterSave>(File.ReadAllText(path), Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void Save(CharacterSave save)
    {
        var path = PathFor(save.Name) ?? throw new InvalidOperationException($"Invalid character name '{save.Name}'.");
        System.IO.Directory.CreateDirectory(Directory);

        // Write-then-replace, so a crash mid-write can't leave a half-written save behind.
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(save, Options));
        File.Move(temp, path, overwrite: true);
    }

    // Names are letters only (see character creation), which also keeps user input from escaping the folder.
    private string? PathFor(string name) =>
        name.Length > 0 && name.All(char.IsLetter)
            ? Path.Combine(Directory, name.ToLowerInvariant() + ".json")
            : null;
}
