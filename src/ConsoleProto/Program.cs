using Combat;
using ConsoleProto;

var rng = new SystemRandomSource();
var player = CharacterLoader.LoadPlayer();

CombatEngine? activeEngine = null;
Combatant? activeNpc = null;

Console.WriteLine("Commands: kill <rat|deer|beast>, reset, flee (or stop), quit");

while (true)
{
    Console.Write("> ");
    var input = Console.ReadLine();
    if (input == null)
        break;

    var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length == 0)
        continue;

    switch (parts[0].ToLowerInvariant())
    {
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

        case "quit":
        case "exit":
            return;

        default:
            Console.WriteLine("Unknown command. Try: kill <rat|deer|beast>, reset, flee, quit");
            break;
    }
}

void HandleKill(string[] parts)
{
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
    var engine = new CombatEngine(player, npc, rng);
    engine.OnEmote += Console.WriteLine;
    activeEngine = engine;

    Console.WriteLine($"You attack {npc.Name}!");
    _ = RunCombatAsync(engine, player, npc);
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
    if (activeEngine is { IsFinished: false })
    {
        Console.WriteLine("Finish or flee the current fight first.");
        return;
    }

    player.ResetHp();
    player.ResetMp();
    activeNpc?.ResetHp();
    activeNpc?.ResetMp();
    Console.WriteLine("HP/MP restored.");
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

        Console.WriteLine($"   [Round {engine.Round}] {player.Name}: {player.CurrentHp}/{player.MaxHp} HP | {npc.Name}: {npc.CurrentHp}/{npc.MaxHp} HP");
    }

    if (engine.Winner == player)
        Console.WriteLine($"You have slain {npc.Name}!");
    else if (engine.Winner == npc)
        Console.WriteLine("You have died.");
    else
        Console.WriteLine($"You disengage from {npc.Name}.");
}
