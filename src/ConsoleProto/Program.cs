using Combat;
using ConsoleProto;
using World;

const int NpcRespawnSeconds = 30;
const string HelpLine =
    "Commands: create, look [target], north/south/east/west/up/down (n/s/e/w/u/d), kill <target>, shape [target], " +
    "bandage [target], simulate [rounds] [npc], reset, flee (or stop), listk, quit";

var rng = new SystemRandomSource();
var world = new WorldState(WorldLoader.Load(), CharacterLoader.LoadNpc);

Combatant? player = null; // no character until `create`; lives in memory only, lost on quit
Room? playerRoom = null;

CombatEngine? activeEngine = null;
Combatant? activeNpc = null; // the opponent of the current (or last) fight
Direction? pendingMove = null; // a move typed mid-fight: it counts as fleeing, and happens once the fight actually ends

Console.WriteLine(HelpLine);

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

        case "look":
        case "l":
            HandleLook(parts);
            break;

        case "kill":
            HandleKill(parts);
            break;

        case "bandage":
            HandleBandage(parts);
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
            if (Directions.TryParse(parts[0], out var direction))
                HandleMove(direction);
            else
                Console.WriteLine("Unknown command. " + HelpLine);
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
    playerRoom = world.Map.StartRoom;
    activeEngine = null;
    activeNpc = null;
    pendingMove = null;

    Console.WriteLine();
    RoomView.Write(playerRoom, world.NpcsIn(playerRoom));
}

bool RequirePlayer()
{
    if (player != null)
        return true;

    Console.WriteLine("You have no character yet. Type: create");
    return false;
}

bool PlayerCanAct()
{
    if (player!.IsBleeding)
    {
        Console.WriteLine("You are on the ground, bleeding. You can't do anything!");
        return false;
    }

    if (player.IsDead)
    {
        Console.WriteLine("You are dead. Type: reset");
        return false;
    }

    return true;
}

void HandleLook(string[] parts)
{
    if (!RequirePlayer())
        return;

    var room = playerRoom!;

    // "look at rat" reads like "look rat".
    var words = parts.Skip(1).Where((w, i) => !(i == 0 && w.Equals("at", StringComparison.OrdinalIgnoreCase))).ToArray();
    if (words.Length == 0)
    {
        RoomView.Write(room, world.NpcsIn(room));
        return;
    }

    var query = string.Join(' ', words);

    if (query.Equals("me", StringComparison.OrdinalIgnoreCase) || query.Equals("self", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine($"You are {player!.Name}, {(("aeiou".Contains(player.Race.Name[0], StringComparison.OrdinalIgnoreCase)) ? "an" : "a")} {player.Race.Name.ToLowerInvariant()}.");
        Console.WriteLine(DescribeCondition(player));
        return;
    }

    var npc = world.FindNpc(room, query);
    if (npc != null)
    {
        Console.WriteLine(npc.Description.Length > 0 ? npc.Description : $"You see nothing special about {npc.Name}.");
        Console.WriteLine(DescribeCondition(npc));
        return;
    }

    var detail = room.Details.FirstOrDefault(d => d.Key.Equals(query, StringComparison.OrdinalIgnoreCase));
    if (detail.Key == null)
        detail = room.Details.FirstOrDefault(d => d.Key.StartsWith(query, StringComparison.OrdinalIgnoreCase));

    Console.WriteLine(detail.Key != null ? detail.Value : "You don't see that here.");
}

void HandleMove(Direction direction)
{
    if (!RequirePlayer() || !PlayerCanAct())
        return;

    if (world.Map.GetExit(playerRoom!, direction) == null)
    {
        Console.WriteLine("You can't go that way.");
        return;
    }

    // Leaving a fight is fleeing: it takes effect when the current round is over, like `flee`.
    if (activeEngine is { IsFinished: false } engine)
    {
        pendingMove = direction;
        engine.RequestFlee();
        Console.WriteLine($"You try to slip away to the {direction.Label()}...");
        return;
    }

    MovePlayer(direction);
}

// Safe to call from the combat thread (when a fled fight finally ends), hence LineEditor.Print.
void MovePlayer(Direction direction)
{
    var destination = world.Map.GetExit(playerRoom!, direction);
    if (destination == null)
        return;

    playerRoom = destination;
    LineEditor.Print(() => RoomView.Write(destination, world.NpcsIn(destination)));
}

// A dead NPC leaves the room and comes back at its spawn point after a while.
void OnNpcDied(Combatant npc)
{
    var roomId = world.Remove(npc);
    if (roomId != null)
        _ = RespawnAsync(roomId, npc.Id);
}

async Task RespawnAsync(string roomId, string npcId)
{
    await Task.Delay(NpcRespawnSeconds * 1000);
    world.Spawn(roomId, npcId);
}

void HandleKill(string[] parts)
{
    if (!RequirePlayer() || !PlayerCanAct())
        return;

    // A bare `kill` is enough to finish off the opponent you already left bleeding in this fight.
    var query = parts.Length >= 2
        ? string.Join(' ', parts.Skip(1))
        : activeEngine is { IsFinished: false } && activeNpc is { IsBleeding: true } ? activeNpc.Id : null;

    if (query == null)
    {
        Console.WriteLine("Kill what? Try: kill rat");
        return;
    }

    if (activeEngine is { IsFinished: false } current)
    {
        // In combat with a fallen opponent: `kill` finishes it off. Anything else is a second fight.
        if (activeNpc is { IsBleeding: true } && WorldState.Matches(activeNpc, query))
        {
            if (!current.FinishOff(player!))
                Console.WriteLine("There is nobody to finish off.");
        }
        else
        {
            Console.WriteLine("You are already in combat.");
        }
        return;
    }

    var npc = world.FindNpc(playerRoom!, query);
    if (npc == null)
    {
        Console.WriteLine("You don't see that here.");
        return;
    }

    if (npc.IsBleeding)
    {
        // Not fighting it: someone lying there can only be bandaged, not attacked.
        Console.WriteLine($"{Text.Cap(npc.Name)} is on the ground, bleeding. You can only bandage it: bandage {npc.Id}");
        return;
    }

    activeNpc = npc;

    // Player typed "kill" -> keeps initiative every round.
    var engine = new CombatEngine(player!, npc, rng);
    engine.OnAttackResult += result => LineEditor.Print(() => EmoteHighlighter.WriteLine(result.EmoteText, result.Tier));
    engine.OnNarration += text => LineEditor.Print(() => Console.WriteLine(text));
    activeEngine = engine;

    Console.WriteLine($"You attack {npc.Name}!");
    _ = RunCombatAsync(engine, player!, npc);
}

// Only works while someone is bleeding. In combat with them, `bandage` alone is enough; otherwise a name is required.
void HandleBandage(string[] parts)
{
    if (!RequirePlayer() || !PlayerCanAct())
        return;

    bool inCombat = activeEngine is { IsFinished: false };
    Combatant? target;

    if (parts.Length < 2)
    {
        if (inCombat && activeNpc is { IsBleeding: true })
        {
            target = activeNpc;
        }
        else
        {
            var bleeding = world.NpcsIn(playerRoom!).FirstOrDefault(n => n.IsBleeding);
            Console.WriteLine(bleeding != null
                ? $"Bandage whom? Try: bandage {bleeding.Id}"
                : "There is no one here who needs bandaging.");
            return;
        }
    }
    else
    {
        var name = string.Join(' ', parts.Skip(1));
        target = world.NpcsIn(playerRoom!).FirstOrDefault(n => n.IsBleeding && WorldState.Matches(n, name));
        if (target == null)
        {
            Console.WriteLine("No one by that name needs bandaging.");
            return;
        }
    }

    if (inCombat && ReferenceEquals(target, activeNpc))
    {
        // Bandaging the opponent stops the fight; the engine narrates it.
        if (!activeEngine!.Bandage(player!))
            Console.WriteLine("There is no one here who needs bandaging.");
        return;
    }

    lock (target)
        target.Stabilize();
    Console.WriteLine($"You bandage {target.Name}'s wounds, and the bleeding stops.");
}

void HandleFlee()
{
    if (player is { IsDown: true })
    {
        Console.WriteLine("You are on the ground, bleeding. You can't do anything!");
        return;
    }

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
    foreach (var npc in world.NpcsIn(playerRoom!))
    {
        npc.ResetHp();
        npc.ResetMp();
    }
    Console.WriteLine("HP/MP restored.");
}

void HandleShape(string[] parts)
{
    if (!RequirePlayer())
        return;

    if (parts.Length >= 2)
    {
        var npc = world.FindNpc(playerRoom!, string.Join(' ', parts.Skip(1)));
        Console.WriteLine(npc != null ? DescribeCondition(npc) : "You don't see that here.");
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
async Task RunCombatAsync(CombatEngine engine, Combatant player, Combatant npc)
{
    while (!engine.IsFinished)
    {
        engine.PlayFirstTurn();
        if (engine.IsFinished)
            break;
        await Task.Delay(1000);

        engine.PlaySecondTurn();
        if (engine.IsFinished)
            break;
        await Task.Delay(1000);
    }

    // Only a fight that ended by fleeing carries a queued move along; any other ending cancels it.
    var moveAfterFleeing = engine.EndReason == CombatEndReason.Fled ? pendingMove : null;
    pendingMove = null;

    switch (engine.EndReason)
    {
        case CombatEndReason.Slain:
            LineEditor.Print(() => Console.WriteLine(engine.Winner == player ? $"You have slain {npc.Name}!" : "You have died."));
            if (npc.IsDead)
                OnNpcDied(npc);
            break;

        case CombatEndReason.BledOut:
            // The engine already narrated it; only the player's own death needs a closing line.
            if (engine.Winner == npc)
                LineEditor.Print(() => Console.WriteLine("You have died."));
            else
                OnNpcDied(npc);
            break;

        case CombatEndReason.Fled:
            LineEditor.Print(() => Console.WriteLine($"You disengage from {npc.Name}."));
            if (moveAfterFleeing is { } direction)
                MovePlayer(direction);
            // The engine is gone, so nobody ticks a fallen NPC's bleeding anymore: do it here.
            if (npc.IsBleeding)
                await BleedOutAsync(npc);
            break;

        // Stabilized: the engine already narrated the bandaging.
    }
}

async Task BleedOutAsync(Combatant npc)
{
    while (true)
    {
        await Task.Delay(2000);

        bool bledOut;
        lock (npc)
        {
            if (!npc.IsBleeding)
                return; // bandaged (or finished) in the meantime
            bledOut = npc.TickBleed();
        }

        // Only worth narrating if you're still in the room to see it.
        var here = playerRoom != null && world.NpcsIn(playerRoom).Contains(npc);
        if (here)
        {
            LineEditor.Print(() => Console.WriteLine(bledOut
                ? $"{Text.Cap(npc.Name)} bleeds out and dies."
                : $"{Text.Cap(npc.Name)} lies on the ground, bleeding and in need of bandages."));
        }

        if (bledOut)
        {
            OnNpcDied(npc);
            return;
        }
    }
}

static string DescribeCondition(Combatant target)
{
    if (target.IsDead)
        return target.IsPlayer ? "You are dead." : $"{Text.Cap(target.Name)} is dead.";

    if (target.IsBleeding)
        return target.IsPlayer
            ? "You are on the ground, bleeding and in need of bandages!"
            : $"{Text.Cap(target.Name)} is on the ground, bleeding and in need of bandages!";

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
        : $"{Text.Cap(target.Name)} {npcConditions[tier]}";
}
