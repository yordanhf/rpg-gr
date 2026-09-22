namespace Combat;

/// A title that applies for levels [MinLevel, MaxLevel] (MaxLevel null = open-ended, e.g. "20 and up").
public record TitleBracket(int MinLevel, int? MaxLevel, string Title);

/// A profession's own skills aren't implemented yet (reserved for later) — for now this only drives
/// the character's title. Titles are the same for every race for now; later they may split by
/// "race type" (e.g. orc-kin vs. elf/human/dwarf-kin) so different families of races don't share
/// the same title ladder.
public class Profession
{
    /// No titles at all — the fallback for a combatant with no profession set.
    public static readonly Profession None = new() { Name = "None", Titles = new() };

    /// Data id (the file name in data/professions), set by the loader.
    public string Id { get; set; } = "";

    public required string Name { get; init; }

    public required List<TitleBracket> Titles { get; init; }

    /// The matching bracket's title, or this profession's plain Name if none matches (e.g. above the
    /// highest defined bracket — those are meant to be personalized per character later, not looked up).
    public string TitleFor(int level) =>
        Titles.FirstOrDefault(t => level >= t.MinLevel && (t.MaxLevel == null || level <= t.MaxLevel))?.Title ?? Name;
}
