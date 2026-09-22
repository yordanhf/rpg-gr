namespace World;

public enum Direction { North, South, East, West, Up, Down }

public static class Directions
{
    private static readonly Dictionary<string, Direction> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["north"] = Direction.North, ["n"] = Direction.North,
        ["south"] = Direction.South, ["s"] = Direction.South,
        ["east"] = Direction.East, ["e"] = Direction.East,
        ["west"] = Direction.West, ["w"] = Direction.West,
        ["up"] = Direction.Up, ["u"] = Direction.Up,
        ["down"] = Direction.Down, ["d"] = Direction.Down,
    };

    /// Accepts the full name or the one-letter abbreviation, case-insensitive.
    public static bool TryParse(string text, out Direction direction) => Aliases.TryGetValue(text.Trim(), out direction);

    public static Direction Opposite(this Direction direction) => direction switch
    {
        Direction.North => Direction.South,
        Direction.South => Direction.North,
        Direction.East => Direction.West,
        Direction.West => Direction.East,
        Direction.Up => Direction.Down,
        _ => Direction.Up
    };

    public static string Label(this Direction direction) => direction.ToString().ToLowerInvariant();
}
