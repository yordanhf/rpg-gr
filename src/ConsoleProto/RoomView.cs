using Combat;
using World;

namespace ConsoleProto;

internal static class Text
{
    public static string Cap(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}

/// How a room is shown to the player (`look`, and after moving).
internal static class RoomView
{
    public static void Write(Room room, WorldState world)
    {
        var previous = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(room.Name);
        Console.ForegroundColor = previous;

        Console.WriteLine(room.Description);

        var exits = room.Exits.Keys.OrderBy(d => d).Select(d => d.Label()).ToList();
        Console.WriteLine(exits.Count > 0 ? $"Exits: {string.Join(", ", exits)}." : "Exits: none.");

        if (room.Shop != null)
            Console.WriteLine("You can buy goods here. Try: list");

        if (room.Healer != null)
            Console.WriteLine("You can be healed here. Try: heal");

        if (room.Trainer != null)
            Console.WriteLine("You can train here, and advance a level once you qualify. Try: train, levelup");

        foreach (var npc in world.NpcsIn(room))
        {
            Console.WriteLine(npc.IsBleeding
                ? $"{Text.Cap(npc.Name)} is lying here, bleeding and in need of bandages."
                : $"{Text.Cap(npc.Name)} is here.");
        }

        foreach (var corpse in world.CorpsesIn(room))
            Console.WriteLine($"{Text.Cap(corpse.Name)} lies here.");

        foreach (var item in world.ItemsOnGround(room))
            Console.WriteLine($"{Text.Cap(item.Name)} lies on the ground.");
    }
}
