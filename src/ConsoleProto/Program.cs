using Combat;
using ConsoleProto;

var rng = new SystemRandomSource();
Combatant? player = null; // no character until `create`; lives in memory only, lost on quit

CombatEngine? activeEngine = null;
Combatant? activeNpc = null;

Console.WriteLine($"Commands: create, kill <{string.Join('|', CharacterLoader.ListNpcIds())}>, shape [target], simulate [rounds] [npc], reset, flee (or stop), listk, quit");

while (true)
{
    var input = LineEditor.ReadLine("> ");
    if (input == null)
        break;

    var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length == 0)
        continue;

    switch (parts[0].ToLowerInvariant())
    {
        case "create":
            HandleCreate();
            break;

        case "kill":
            HandleKill(parts);
            break;

        case "flee":
        case "stop":
            HandleFlee();
            break;

        case "reset":
            HandleReset();
            break;

        case "listk":
            HandleListEmotes();
            break;

        case "shape":
            HandleShape(parts);
            break;

        case "simulate":
            HandleSimulate(parts);
            break;

        case "quit":
        case "exit":
            return;

        default:
            Console.WriteLine("Unknown command. Try: create, kill <npc>, shape [target], simulate [rounds] [npc], reset, flee, listk, quit");
            break;
    }
}

void HandleCreate()
{
    if (activeEngine is { IsFinished: false })
    {
        Console.WriteLine("Finish or flee the current fight first.");
        return;
    }

    var created = CharacterCreation.Run();
    if (created == null)
    {
        Console.WriteLine("Character creation cancelled.");
        return;
    }

    player = created;
    activeEngine = null;
    activeNpc = null;
}

bool RequirePlayer()
{
    if (player != null)
        return true;

    Console.WriteLine("You have no character yet. Type: create");
    return false;
}

void HandleKill(string[] parts)
{
    if (!RequirePlayer())
        return;

    if (activeEngine is { IsFinished: false })
    {
        Console.WriteLine("You are already in combat.");
        return;
    }

    if (parts.Length < 2)
    {
        Console.WriteLine("Kill what? Try: kill rat");
        return;
    }

    var npc = CharacterLoader.LoadNpc(parts[1]);
    if (npc == null)
    {
        Console.WriteLine($"No such target: {parts[1]}");
        return;
    }

    activeNpc = npc;

    // Player typed "kill" -> keeps initiative every round.
    var engine = new CombatEngine(player!, npc, rng);
    engine.OnAttackResult += result => LineEditor.Print(() => EmoteHighlighter.WriteLine(result.EmoteText, result.Tier));
    activeEngine = engine;

    Console.WriteLine($"You attack {npc.Name}!");
    _ = RunCombatAsync(engine, player!, npc);
}

void HandleFlee()
{
    if (activeEngine is { IsFinished: false } engine)
    {
        engine.RequestFlee();
        Console.WriteLine("You attempt to disengage...");
    }
    else
    {
        Console.WriteLine("You are not in combat.");
    }
}

void HandleReset()
{
    if (!RequirePlayer())
        return;

    if (activeEngine is { IsFinished: false })
    {
        Console.WriteLine("Finish or flee the current fight first.");
        return;
    }

    player!.ResetHp();
    player.ResetMp();
    activeNpc?.ResetHp();
    activeNpc?.ResetMp();
    Console.WriteLine("HP/MP restored.");
}

void HandleShape(string[] parts)
{
    if (parts.Length >= 2)
    {
        var name = parts[1];

        // If you're actually fighting this one, show its live (wounded) condition...
        if (activeNpc != null && activeNpc.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine(DescribeCondition(activeNpc));
            return;
        }

        // ...otherwise fall back to a fresh copy, so "shape rat" works even outside combat.
        var known = CharacterLoader.LoadNpc(name);
        if (known != null)
            Console.WriteLine(DescribeCondition(known));
        else
            Console.WriteLine("You don't see that here.");
        return;
    }

    if (activeEngine is { IsFinished: false } && activeNpc != null)
        Console.WriteLine(DescribeCondition(activeNpc));
    else
        Console.WriteLine("You're not in combat.");
}

void HandleSimulate(string[] parts)
{
    if (!RequirePlayer())
        return;

    int rounds = 1000;
    if (parts.Length >= 2 && (!int.TryParse(parts[1], out rounds) || rounds <= 0))
    {
        Console.WriteLine("Usage: simulate [rounds] [npc]");
        return;
    }

    Simulation.Run(player!, rounds, parts.Length >= 3 ? parts[2] : null);
}

void HandleListEmotes()
{
    var standardEmotes = EmoteTable.CreateStandard();
    foreach (HitTier tier in Enum.GetValues<HitTier>())
    {
        var variants = standardEmotes.TryGetVariants(tier);
        if (variants == null)
            continue;

        Console.WriteLine($"-- {tier} --");
        foreach (var variant in variants)
        {
            EmoteHighlighter.WriteLine(variant.Format("You", "the rat", actorIsPlayer: true), tier);
            EmoteHighlighter.WriteLine(variant.Format("the rat", "you", actorIsPlayer: false), tier);
        }
    }
}

// Each turn takes 1 second; a full round (both combatants act) takes 2 seconds.
static async Task RunCombatAsync(CombatEngine engine, Combatant player, Combatant npc)
{
    while (!engine.IsFinished)
    {
        engine.PlayFirstTurn();
        if (engine.IsFinished)
            break;
        await Task.Delay(1000);

        engine.PlaySecondTurn();
        await Task.Delay(1000);
    }

    if (engine.Winner == player)
        LineEditor.Print(() => Console.WriteLine($"You have slain {npc.Name}!"));
    else if (engine.Winner == npc)
        LineEditor.Print(() => Console.WriteLine("You have died."));
    else
        LineEditor.Print(() => Console.WriteLine($"You disengage from {npc.Name}."));
}

static string DescribeCondition(Combatant target)
{
    if (target.IsDead)
        return target.IsPlayer ? "You are dead." : $"{target.Name} is dead.";

    // Worst to best, 6 bands of the target's HP ratio — never the raw numbers.
    string[] npcConditions =
    {
        "is in critical condition, very close to death!",
        "is near death.",
        "doesn't look so great.",
        "is in average condition.",
        "is in good condition.",
        "is in perfect condition."
    };
    string[] playerConditions =
    {
        "are in critical condition, very close to death!",
        "are near death.",
        "don't look so great.",
        "are in average condition.",
        "are in good condition.",
        "are in perfect condition."
    };

    double ratio = (double)target.CurrentHp / target.MaxHp;
    int tier = Math.Clamp((int)(ratio * npcConditions.Length), 0, npcConditions.Length - 1);

    return target.IsPlayer
        ? $"You {playerConditions[tier]}"
        : $"{target.Name} {npcConditions[tier]}";
}
