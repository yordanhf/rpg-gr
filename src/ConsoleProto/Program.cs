using Combat;
using ConsoleProto;

var rng = new SystemRandomSource();
Combatant? player = null; // no character until `create`; lives in memory only, lost on quit

CombatEngine? activeEngine = null;
Combatant? activeNpc = null;
string? activeNpcId = null; // the id typed in `kill <id>`, so the same (wounded/bleeding) NPC is reused instead of respawned

Console.WriteLine($"Commands: create, kill <{string.Join('|', CharacterLoader.ListNpcIds())}>, shape [target], bandage [target], simulate [rounds] [npc], reset, flee (or stop), listk, quit");

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
            Console.WriteLine("Unknown command. Try: create, kill <npc>, shape [target], bandage [target], simulate [rounds] [npc], reset, flee, listk, quit");
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

void HandleKill(string[] parts)
{
    if (!RequirePlayer() || !PlayerCanAct())
        return;

    // A bare `kill` is enough to finish off the opponent you already left bleeding in this fight.
    var id = parts.Length >= 2
        ? parts[1].ToLowerInvariant()
        : activeEngine is { IsFinished: false } && activeNpc is { IsBleeding: true } ? activeNpcId : null;

    if (id == null)
    {
        Console.WriteLine("Kill what? Try: kill rat");
        return;
    }

    // Same NPC as last time? Reuse it (wounded, bleeding...) instead of spawning a fresh copy. Dead ones respawn.
    bool sameNpc = activeNpc != null && activeNpcId == id && !activeNpc.IsDead;

    if (activeEngine is { IsFinished: false } current)
    {
        // In combat with a fallen opponent: `kill` finishes it off. Anything else is a second fight.
        if (sameNpc && activeNpc!.IsBleeding)
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

    if (sameNpc && activeNpc!.IsBleeding)
    {
        // Not fighting it anymore: someone lying there can only be bandaged, not attacked.
        Console.WriteLine($"{Cap(activeNpc.Name)} is on the ground, bleeding. You can only bandage it: bandage {id}");
        return;
    }

    var npc = sameNpc ? activeNpc! : CharacterLoader.LoadNpc(id);
    if (npc == null)
    {
        Console.WriteLine($"No such target: {id}");
        return;
    }

    activeNpc = npc;
    activeNpcId = id;

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
        else if (activeNpc is { IsBleeding: true })
        {
            Console.WriteLine($"Bandage whom? Try: bandage {activeNpcId}");
            return;
        }
        else
        {
            Console.WriteLine("There is no one here who needs bandaging.");
            return;
        }
    }
    else
    {
        var name = parts[1];
        bool matches = activeNpc is { IsBleeding: true }
            && (activeNpcId == name.ToLowerInvariant() || activeNpc.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
        if (!matches)
        {
            Console.WriteLine("No one by that name needs bandaging.");
            return;
        }
        target = activeNpc;
    }

    if (inCombat && ReferenceEquals(target, activeNpc))
    {
        // Bandaging the opponent stops the fight; the engine narrates it.
        if (!activeEngine!.Bandage(player!))
            Console.WriteLine("There is no one here who needs bandaging.");
        return;
    }

    lock (target!)
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
        if (engine.IsFinished)
            break;
        await Task.Delay(1000);
    }

    switch (engine.EndReason)
    {
        case CombatEndReason.Slain:
            LineEditor.Print(() => Console.WriteLine(engine.Winner == player ? $"You have slain {npc.Name}!" : "You have died."));
            break;

        case CombatEndReason.BledOut:
            // The engine already narrated it; only the player's own death needs a closing line.
            if (engine.Winner == npc)
                LineEditor.Print(() => Console.WriteLine("You have died."));
            break;

        case CombatEndReason.Fled:
            LineEditor.Print(() => Console.WriteLine($"You disengage from {npc.Name}."));
            // The engine is gone, so nobody ticks a fallen NPC's bleeding anymore: do it here.
            if (npc.IsBleeding)
                await BleedOutAsync(npc);
            break;

        // Stabilized: the engine already narrated the bandaging.
    }
}

static async Task BleedOutAsync(Combatant npc)
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

        LineEditor.Print(() => Console.WriteLine(bledOut
            ? $"{Cap(npc.Name)} bleeds out and dies."
            : $"{Cap(npc.Name)} lies on the ground, bleeding and in need of bandages."));

        if (bledOut)
            return;
    }
}

static string Cap(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

static string DescribeCondition(Combatant target)
{
    if (target.IsDead)
        return target.IsPlayer ? "You are dead." : $"{target.Name} is dead.";

    if (target.IsBleeding)
        return target.IsPlayer
            ? "You are on the ground, bleeding and in need of bandages!"
            : $"{Cap(target.Name)} is on the ground, bleeding and in need of bandages!";

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
