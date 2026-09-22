using Combat;

namespace ConsoleProto;

/// The `create` command flow: asks for a name and a race, then builds the player. Nothing is persisted.
internal static class CharacterCreation
{
    private const int MinNameLength = 2;
    private const int MaxNameLength = 16;

    /// Returns null if the user cancels (empty answer) or input ends.
    public static Combatant? Run(Func<string, bool> nameTaken)
    {
        var name = AskName(nameTaken);
        if (name == null)
            return null;

        var races = CharacterLoader.ListPlayableRaces();
        if (races.Count == 0)
        {
            Console.WriteLine("No races are available.");
            return null;
        }

        Console.WriteLine("Choose your race:");
        foreach (var (id, race) in races)
        {
            Console.WriteLine($"  {id}");
            Console.WriteLine($"    {race.Description}");
        }

        var raceId = AskRace(races);
        if (raceId == null)
            return null;

        var player = CharacterLoader.CreatePlayer(name, raceId);

        Console.WriteLine();
        Console.WriteLine("The world blurs, then sharpens around you...");
        Console.WriteLine($"You are {name} the {player.Race.Name.ToLowerInvariant()} {player.Title}. Your story begins.");
        return player;
    }

    private static string? AskName(Func<string, bool> nameTaken)
    {
        while (true)
        {
            Console.Write("Name (empty to cancel): ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
                return null;

            var name = input.Trim();
            if (name.Length < MinNameLength || name.Length > MaxNameLength || !name.All(char.IsLetter))
            {
                Console.WriteLine($"Names use only letters, {MinNameLength}-{MaxNameLength} long.");
                continue;
            }

            name = char.ToUpperInvariant(name[0]) + name[1..].ToLowerInvariant();
            if (nameTaken(name))
            {
                Console.WriteLine("A character with that name already exists.");
                continue;
            }

            return name;
        }
    }

    private static string? AskRace(IReadOnlyList<(string Id, Race Race)> races)
    {
        while (true)
        {
            Console.Write("Race (type it exactly, empty to cancel): ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
                return null;

            var match = races.FirstOrDefault(r => r.Id.Equals(input.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match.Id != null)
                return match.Id;

            Console.WriteLine("That is not one of the races listed.");
        }
    }
}
