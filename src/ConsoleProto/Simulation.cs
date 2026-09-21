using Combat;

namespace ConsoleProto;

/// Diagnostic only: resolves N rounds (one attack each side per round) between the player and an NPC,
/// ignoring HP entirely — nobody dies, so every round has both attacks. No engine, no delays.
internal static class Simulation
{
    private static readonly string[] NpcIds = { "rat", "deer", "beast" };
    private static readonly HitTier[] Tiers = Enum.GetValues<HitTier>();

    public static void Run(Combatant player, int rounds)
    {
        foreach (var id in NpcIds)
        {
            var npc = CharacterLoader.LoadNpc(id);
            if (npc == null)
                continue;

            var rng = new SystemRandomSource();
            var playerStats = new SideStats();
            var npcStats = new SideStats();

            for (int i = 0; i < rounds; i++)
            {
                playerStats.Add(AttackResolver.Resolve(player, npc, rng));
                npcStats.Add(AttackResolver.Resolve(npc, player, rng));
            }

            Print(npc.Name, rounds, playerStats, npcStats);
        }
    }

    private static void Print(string npcName, int rounds, SideStats player, SideStats npc)
    {
        Console.WriteLine($"=== Player vs {npcName} ({rounds} rounds, HP ignored) ===");
        Console.WriteLine($"{"",-16}{"Player",10}{npcName,10}");

        foreach (var tier in Tiers)
            Console.WriteLine($"{tier,-16}{player.Counts[tier],10}{npc.Counts[tier],10}");

        Console.WriteLine($"{"Hit rate",-16}{player.HitRate,10:P1}{npc.HitRate,10:P1}");
        Console.WriteLine($"{"Avg dmg/hit",-16}{player.AvgDamagePerHit,10:F1}{npc.AvgDamagePerHit,10:F1}");
        Console.WriteLine($"{"Avg dmg/round",-16}{player.AvgDamagePerRound,10:F1}{npc.AvgDamagePerRound,10:F1}");
        Console.WriteLine();
    }

    private class SideStats
    {
        public Dictionary<HitTier, int> Counts { get; } = Tiers.ToDictionary(t => t, _ => 0);
        private long _totalDamage;
        private int _attacks;

        public void Add(HitResult result)
        {
            Counts[result.Tier]++;
            _totalDamage += result.Damage;
            _attacks++;
        }

        private int Hits => _attacks - Counts[HitTier.Miss];
        public double HitRate => _attacks == 0 ? 0 : (double)Hits / _attacks;
        public double AvgDamagePerHit => Hits == 0 ? 0 : (double)_totalDamage / Hits;
        public double AvgDamagePerRound => _attacks == 0 ? 0 : (double)_totalDamage / _attacks;
    }
}
