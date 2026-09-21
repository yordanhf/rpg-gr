namespace Combat;

public interface IRandomSource
{
    double NextDouble();
    int Next(int minInclusive, int maxExclusive);
}

public class SystemRandomSource : IRandomSource
{
    private readonly Random _random;

    public SystemRandomSource(int? seed = null)
    {
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    public double NextDouble() => _random.NextDouble();

    public int Next(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);
}
