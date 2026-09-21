using Combat;

namespace ConsoleProto;

/// The `create` command flow: asks for a name and a race, then builds the player. Nothing is persisted.
internal static class CharacterCreation
{
    private const int MinNameLength = 2;
    private const int MaxNameLength = 16;

    /// Returns null if the user cancels (empty answer) or input ends.
    public static Combatant? Run()
    {
        var name = AskName();
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
        var article = "aeiou".Contains(player.Race.Name[0], StringComparison.OrdinalIgnoreCase) ? "an" : "a";

        Console.WriteLine();
        Console.WriteLine("The world blurs, then sharpens around you...");
        Console.WriteLine($"You are {name}, {article} {player.Race.Name.ToLowerInvariant()}. Your story begins.");
        return player;
    }

    private static string? AskName()
    {
        while (true)
        {
            Console.Write("Name (empty to cancel): ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
                return null;

            var name = input.Trim();
            if (name.Length >= MinNameLength && name.Length <= MaxNameLength && name.All(char.IsLetter))
                return char.ToUpperInvariant(name[0]) + name[1..].ToLowerInvariant();

            Console.WriteLine($"Names use only letters, {MinNameLength}-{MaxNameLength} long.");
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
