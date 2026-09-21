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

    Console.WriteLine("Type 'flee' and press Enter at any time to disengage.");
    _ = Task.Run(() =>
    {
        while (!engine.IsFinished)
        {
            if (string.Equals(Console.ReadLine()?.Trim(), "flee", StringComparison.OrdinalIgnoreCase))
            {
                engine.RequestFlee();
                return;
            }
        }
    });

    // Each turn takes 1 second; a full round (both combatants act) takes 2 seconds.
    while (!engine.IsFinished)
    {
        engine.PlayFirstTurn();
        if (engine.IsFinished)
            break;
        await Task.Delay(1000);

        engine.PlaySecondTurn();
        await Task.Delay(1000);

        Console.WriteLine($"   [Round {engine.Round}] {player.Name}: {player.CurrentHp}/{player.MaxHp} HP | {orc.Name}: {orc.CurrentHp}/{orc.MaxHp} HP");
    }

    if (engine.Winner == player)
        Console.WriteLine("You win!");
    else if (engine.Winner == orc)
        Console.WriteLine("You have died.");
    else
        Console.WriteLine("You flee from the fight.");
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
    Strength = 60,
    Constitution = 55,
    Agility = 50,
    Coordination = 65,
    Aim = 60,
    Attack = 55,
    Defense = 45,
    Dodge = 40,
    Weapon = new Weapon
    {
        Name = "Iron Sword",
        Hit = 70,
        Damage = 55,
        CritChanceBonus = 0.003,
        CritPower = 40,
        CustomEmotes = new EmoteTable()
            .Add(HitTier.Critical,
                new EmoteVariant("Your blade finds a gap in {0}'s guard — a critical hit!",
                                  "{0}'s blade finds a gap in {1}'s guard — a critical hit!"))
    },
    EquippedArmor = new()
    {
        new Armor { Name = "Leather Chestplate", Absorb = 25, Deflect = 10 },
        new Armor { Name = "Leather Boots", Absorb = 10, Deflect = 15 }
    }
};

static Combatant CreateOrc() => new()
{
    Name = "the orc",
    IsPlayer = false,
    MaxHp = 50,
    Strength = 65,
    Constitution = 60,
    Agility = 40,
    Coordination = 45,
    Aim = 40,
    Attack = 60,
    Defense = 35,
    Dodge = 25,
    Weapon = new Weapon
    {
        Name = "Rusty Axe",
        Hit = 55,
        Damage = 50,
        CritChanceBonus = 0.001,
        CritPower = 25
    },
    EquippedArmor = new()
    {
        new Armor { Name = "Hide Vest", Absorb = 15, Deflect = 5 }
    }
};

static Skill CreatePowerStrike() => new()
{
    Name = "Power Strike",
    CooldownRounds = 3,
    Execute = (attacker, defender, rng) =>
    {
        var result = AttackResolver.Resolve(attacker, defender, rng, offenseAccuracyBonus: 5, offenseDamageBonus: 20);
        string prefix = attacker.IsPlayer ? "You unleash a Power Strike! " : $"{attacker.Name} unleashes a Power Strike! ";
        return result with { EmoteText = prefix + result.EmoteText };
    }
};
