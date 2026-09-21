namespace Combat;

public record EmoteVariant(string SecondPerson, string ThirdPerson)
{
    // SecondPerson: "You {verb} {0}." -> {0} = target name.
    // ThirdPerson:  "{0} {verb-s} {1}." -> {0} = actor name, {1} = target name.
    public string Format(string actorName, string targetName, bool actorIsPlayer)
    {
        return actorIsPlayer
            ? string.Format(SecondPerson, targetName)
            : string.Format(ThirdPerson, actorName, targetName);
    }
}

public class EmoteTable
{
    private readonly Dictionary<HitTier, List<EmoteVariant>> _variants = new();

    public EmoteTable Add(HitTier tier, params EmoteVariant[] variants)
    {
        if (!_variants.TryGetValue(tier, out var list))
        {
            list = new List<EmoteVariant>();
            _variants[tier] = list;
        }
        list.AddRange(variants);
        return this;
    }

    public List<EmoteVariant>? TryGetVariants(HitTier tier) =>
        _variants.TryGetValue(tier, out var list) ? list : null;

    public EmoteVariant Pick(HitTier tier, IRandomSource rng)
    {
        var list = TryGetVariants(tier)
            ?? throw new InvalidOperationException($"No emotes registered for tier {tier}.");
        return list[rng.Next(0, list.Count)];
    }

    public static EmoteTable CreateStandard()
    {
        var table = new EmoteTable();

        table.Add(HitTier.Miss,
            new EmoteVariant("You miss {0} completely.", "{0} misses {1} completely."),
            new EmoteVariant("Your attack misses {0}.", "{0}'s attack misses {1}."));

        table.Add(HitTier.Graze,
            new EmoteVariant("You scratch {0}.", "{0} scratches {1}."),
            new EmoteVariant("You graze {0}.", "{0} grazes {1}."));

        table.Add(HitTier.Hit,
            new EmoteVariant("You hit {0}.", "{0} hits {1}."),
            new EmoteVariant("You strike {0}.", "{0} strikes {1}."));

        table.Add(HitTier.HitHard,
            new EmoteVariant("You hit {0} hard.", "{0} hits {1} hard."),
            new EmoteVariant("You land a hard blow on {0}.", "{0} lands a hard blow on {1}."));

        table.Add(HitTier.HitVeryHard,
            new EmoteVariant("You hit {0} very hard.", "{0} hits {1} very hard."),
            new EmoteVariant("You batter {0} with a heavy blow.", "{0} batters {1} with a heavy blow."));

        table.Add(HitTier.MassiveDamage,
            new EmoteVariant("You inflict massive damage on {0}.", "{0} inflicts massive damage on {1}."),
            new EmoteVariant("You devastate {0} with a brutal strike.", "{0} devastates {1} with a brutal strike."));

        table.Add(HitTier.Massacre,
            new EmoteVariant("You massacre {0}.", "{0} massacres {1}."),
            new EmoteVariant("You annihilate {0} with a savage blow.", "{0} annihilates {1} with a savage blow."));

        table.Add(HitTier.Critical,
            new EmoteVariant("You land a critical hit on {0}!", "{0} lands a critical hit on {1}!"),
            new EmoteVariant("Your weapon finds a deadly opening on {0}!", "{0}'s weapon finds a deadly opening on {1}!"));

        return table;
    }
}
