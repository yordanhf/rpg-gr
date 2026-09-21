namespace Combat;

public record HitResult(int Damage, HitTier Tier, string EmoteText)
{
    public static HitResult Miss(string emoteText) => new(0, HitTier.Miss, emoteText);
}
