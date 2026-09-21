using Combat;

if (args.Length > 0 && args[0] == "simulate")
{
    int count = args.Length > 1 && int.TryParse(args[1], out var n) ? n : 10_000;
    RunSimulation(count);
    return;
}

await RunInteractiveCombat();

static async Task RunInteractiveCombat()
{
    var rng = new SystemRandomSource();
    var player = CreatePlayer();
    var orc = CreateOrc();
    player.ResetHp();
    orc.ResetHp();

    // Player uses "kill" -> keeps initiative every round.
    var engine = new CombatEngine(player, orc, rng);
    engine.OnEmote += Console.WriteLine;

    player.QueueSkill(CreatePowerStrike());

    while (!engine.IsFinished)
    {
        engine.PlayRound();
        Console.WriteLine($"   [Round {engine.Round}] {player.Name}: {player.CurrentHp}/{player.MaxHp} HP | {orc.Name}: {orc.CurrentHp}/{orc.MaxHp} HP");
        await Task.Delay(800);
    }

    Console.WriteLine(engine.Winner == player ? "You win!" : "You have died.");
}

static void RunSimulation(int combatCount)
{
    int wins = 0;
    long totalRounds = 0;
    var rng = new SystemRandomSource(seed: 12345);

    for (int i = 0; i < combatCount; i++)
    {
        var player = CreatePlayer();
        var orc = CreateOrc();
        player.ResetHp();
        orc.ResetHp();

        var engine = new CombatEngine(player, orc, rng);
        player.QueueSkill(CreatePowerStrike());

        while (!engine.IsFinished && engine.Round < 200)
            engine.PlayRound();

        if (engine.Winner == player)
            wins++;
        totalRounds += engine.Round;
    }

    Console.WriteLine($"Combats simulated: {combatCount}");
    Console.WriteLine($"Player win rate: {wins * 100.0 / combatCount:F1}%");
    Console.WriteLine($"Average rounds per combat: {totalRounds / (double)combatCount:F1}");
}

static Combatant CreatePlayer() => new()
{
    Name = "You",
    IsPlayer = true,
    MaxHp = 60,
    Accuracy = 0.85,
    Evasion = 0.15,
    CritChance = 0.10,
    CritMultiplier = 2.0,
    Armor = 3,
    AttackPower = 4,
    Weapon = new Weapon
    {
        Name = "Iron Sword",
        MinDamage = 4,
        MaxDamage = 9,
        CustomEmotes = new EmoteTable()
            .Add(HitTier.Critical,
                new EmoteVariant("Your blade finds a gap in {0}'s guard — a critical hit!",
                                  "{0}'s blade finds a gap in {1}'s guard — a critical hit!"))
    }
};

static Combatant CreateOrc() => new()
{
    Name = "the orc",
    IsPlayer = false,
    MaxHp = 50,
    Accuracy = 0.75,
    Evasion = 0.10,
    CritChance = 0.05,
    CritMultiplier = 1.75,
    Armor = 2,
    AttackPower = 5,
    Weapon = new Weapon
    {
        Name = "Rusty Axe",
        MinDamage = 3,
        MaxDamage = 8
    }
};

static Skill CreatePowerStrike() => new()
{
    Name = "Power Strike",
    CooldownRounds = 3,
    Execute = (attacker, defender, rng) =>
    {
        bool hit = rng.NextDouble() <= Math.Clamp(attacker.Accuracy - defender.Evasion + 0.05, 0.05, 0.95);
        if (!hit)
        {
            return HitResult.Miss(attacker.IsPlayer
                ? $"You attempt a Power Strike on {defender.Name} but miss."
                : $"{attacker.Name} attempts a Power Strike on {defender.Name} but misses.");
        }

        int baseMin = attacker.AttackPower + attacker.Weapon.MinDamage;
        int baseMax = attacker.AttackPower + attacker.Weapon.MaxDamage;
        int raw = rng.Next((int)(baseMin * 0.8), (int)(baseMax * 1.2) + 1);
        int dmg = Math.Max(0, (int)(raw * 1.5) - defender.Armor);

        string emote = attacker.IsPlayer
            ? $"You unleash a Power Strike on {defender.Name}!"
            : $"{attacker.Name} unleashes a Power Strike on {defender.Name}!";

        return new HitResult(dmg, HitTier.HitVeryHard, emote);
    }
};
