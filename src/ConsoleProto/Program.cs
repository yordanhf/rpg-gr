using Combat;
using ConsoleProto;
using World;

const int CorpseFadeSeconds = 30; // how long a corpse (and its loot-rights restriction) lasts
const int RespawnTickSeconds = 600; // world heartbeat: every 10 minutes, due respawns (Combatant.RespawnTicks) go live
const int RegenTickSeconds = 30; // passive HP/MP regen: +CombatConstants.RegenAmountPerTick every 30s
const string HelpLine =
    "Commands: create, continue, look [target], north/south/east/west/up/down (n/s/e/w/u/d), kill <target>, shape [target], " +
    "bandage [target], wield <weapon|shield>, sheath [weapon], wear <armor>, remove <armor>, hands, i (or inventory), gold, xp, " +
    "score, take/get/loot <item>, list, buy <item>, sell <item>, heal, train [stat] [amount], levelup, repair [item], condition <item>, " +
    "simulate [rounds] [npc], reset, flee (or stop), listk, quit";

var rng = new SystemRandomSource();
var world = new WorldState(WorldLoader.Load(), CharacterLoader.LoadNpc);
var store = new JsonCharacterStore();

Combatant? player = null; // no character until `create`; lives in memory only, lost on quit
Room? playerRoom = null;

CombatEngine? activeEngine = null;
Combatant? activeNpc = null; // the opponent of the current (or last) fight
Direction? pendingMove = null; // a move typed mid-fight: it counts as fleeing, and happens once the fight actually ends

_ = RunRespawnTicksAsync(); // runs for the whole process, independent of any player session
_ = RunPlayerRegenAsync();

Console.WriteLine(HelpLine);
Console.WriteLine("Type 'create' for a new character, or 'continue' to resume a saved one.");

while (true)
{
    var input = LineEditor.ReadLine(() => player != null ? $"HP: {player.CurrentHp}  MP: {player.CurrentMp} > " : "> ");
    if (input == null)
    {
        SaveCharacter(); // input closed (Ctrl+Z / end of pipe): same as quit
        break;
    }

    var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length == 0)
        continue;

    switch (parts[0].ToLowerInvariant())
    {
        case "create":
            HandleCreate();
            break;

        case "continue":
            HandleContinue();
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

        case "gold":
            HandleGold();
            break;

        case "xp":
        case "experience":
            HandleExperience();
            break;

        case "heal":
            HandleHeal();
            break;

        case "score":
            HandleScore();
            break;

        case "train":
            HandleTrain(parts);
            break;

        case "levelup":
            HandleLevelUp();
            break;

        case "repair":
            HandleRepair(parts);
            break;

        case "condition":
            HandleCondition(parts);
            break;

        case "wield":
            HandleWield(parts);
            break;

        case "sheath":
            HandleSheath(parts);
            break;

        case "wear":
            HandleWear(parts);
            break;

        case "remove":
            HandleRemoveArmor(parts);
            break;

        case "hands":
            HandleHands();
            break;

        case "take":
        case "get":
        case "loot":
            HandleTake(parts);
            break;

        case "list":
            HandleListShop();
            break;

        case "buy":
            HandleBuy(parts);
            break;

        case "sell":
            HandleSell(parts);
            break;

        case "i":
        case "inventory":
            HandleInventory();
            break;

        case "simulate":
            HandleSimulate(parts);
            break;

        case "quit":
        case "exit":
            SaveCharacter();
            return;

        default:
            if (Directions.TryParse(parts[0], out var direction))
                HandleMove(direction);
            else
                Console.WriteLine("Unknown command. " + HelpLine);
            break;
    }
}

// Creating or resuming is only possible before playing: to switch characters, quit (which saves) and come back.
bool CanStartSession()
{
    if (player == null)
        return true;

    Console.WriteLine($"You are already playing {player.Name}. Type quit to save and leave first.");
    return false;
}

void HandleCreate()
{
    if (!CanStartSession())
        return;

    var created = CharacterCreation.Run(store.Exists);
    if (created == null)
    {
        Console.WriteLine("Character creation cancelled.");
        return;
    }

    EnterWorld(created, world.Map.StartRoom);
}

void HandleContinue()
{
    if (!CanStartSession())
        return;

    Console.Write("Name (empty to cancel): ");
    var name = Console.ReadLine()?.Trim();
    if (string.IsNullOrEmpty(name))
        return;

    var save = store.Load(name);
    if (save == null)
    {
        Console.WriteLine(store.Exists(name)
            ? "That character's save file couldn't be read (corrupted or from an incompatible version)."
            : "There is no saved character by that name.");
        return;
    }

    var loaded = save.ToCombatant(CharacterLoader.GetRace, CharacterLoader.GetProfession, CharacterLoader.CreateStarterWeapon());
    var room = world.Map.GetRoom(save.RoomId);
    if (room == null)
    {
        Console.WriteLine("The place where you left off no longer exists; you wake up at the start.");
        room = world.Map.StartRoom;
    }

    Console.WriteLine($"Welcome back, {loaded.Name}.");
    EnterWorld(loaded, room);
}

void EnterWorld(Combatant character, Room room)
{
    player = character;
    playerRoom = room;
    activeEngine = null;
    activeNpc = null;
    pendingMove = null;

    character.OnLevelUp += level =>
        LineEditor.Print(() => Console.WriteLine($"You have reached level {level}! You are now known as {character.Title}."));

    Console.WriteLine();
    RoomView.Write(room, world);
}

// Characters are only written when leaving the game, never mid-session.
void SaveCharacter()
{
    if (player == null || playerRoom == null)
        return;

    store.Save(CharacterSave.From(player, playerRoom.Id));
    Console.WriteLine($"{player.Name} has been saved.");

    // Only gold persists — gear left unsold at quit is simply gone (see CharacterSave's doc comment).
    bool hasGear = !player.Weapon.IsUnarmed || player.SheathedWeapons.Count > 0 || player.EquippedArmor.Count > 0
        || player.HeldArmor.Count > 0 || player.HeldItems.Count > 0 || player.Backpack != null;
    if (hasGear)
        Console.WriteLine("(Your gear isn't saved — sell it before you go next time, or it's gone.)");
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
        RoomView.Write(room, world);
        return;
    }

    var query = string.Join(' ', words);

    if (query.Equals("me", StringComparison.OrdinalIgnoreCase) || query.Equals("self", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine($"You are {player!.Name} the {player.Race.Name.ToLowerInvariant()} {player.Title}, level {player.Level}.");
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

    var corpse = world.CorpsesIn(room).FirstOrDefault(c => c.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
    if (corpse != null)
    {
        Console.WriteLine(corpse.Loot.Count == 0
            ? $"{Text.Cap(corpse.Name)}. There is nothing left to take from it."
            : corpse.CanLoot(player!.Name)
                ? $"{Text.Cap(corpse.Name)}. You could take: {string.Join(", ", corpse.Loot.Select(i => i.Name))}."
                : $"{Text.Cap(corpse.Name)}. You didn't earn the right to loot it.");
        return;
    }

    var groundItem = world.ItemsOnGround(room).FirstOrDefault(i => i.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
    if (groundItem != null)
    {
        Console.WriteLine($"{Text.Cap(groundItem.Name)}. You could take it.");
        return;
    }

    if (room.Trainer != null && (query.Equals("trainer", StringComparison.OrdinalIgnoreCase)
        || room.Trainer.Name.Contains(query, StringComparison.OrdinalIgnoreCase)))
    {
        ShowTrainingMenu(room.Trainer);
        return;
    }

    if (room.Blacksmith != null && (query.Equals("blacksmith", StringComparison.OrdinalIgnoreCase)
        || room.Blacksmith.Name.Contains(query, StringComparison.OrdinalIgnoreCase)))
    {
        ShowBlacksmithMenu(room.Blacksmith);
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
    LineEditor.Print(() => RoomView.Write(destination, world));
}

// A dead NPC leaves its corpse behind (lootable only by whoever fought it, see Combatant.LootRights)
// for CorpseFadeSeconds — a short window, unrelated to how long the NPC itself takes to respawn.
// Then the corpse fades and its remaining loot spills onto the floor for anyone.
void OnNpcDied(Combatant npc)
{
    // Flat bonus on top of the per-hit XP already awarded during the fight (roughly its MaxHp worth,
    // since you can't out-damage its total HP by much) — together landing close to 2x MaxHp total.
    player?.AddExperience(npc.MaxHp);

    var roomId = world.Remove(npc);
    if (roomId == null)
        return;

    var corpse = new Corpse
    {
        Name = $"the corpse of {npc.Name}",
        Loot = npc.Loot,
        LootRights = npc.LootRights
    };
    world.AddCorpse(roomId, corpse);
    _ = FadeCorpseAsync(roomId, corpse);

    // Not on the corpse's schedule: it waits for the world tick, however many ticks this NPC needs.
    world.ScheduleRespawn(roomId, npc.Id, npc.RespawnTicks);
}

async Task FadeCorpseAsync(string roomId, Corpse corpse)
{
    await Task.Delay(CorpseFadeSeconds * 1000);
    world.FadeCorpse(roomId, corpse);
}

// The world's heartbeat: every RespawnTickSeconds, whichever pending respawns are due go live, and
// every healer's spent capacity comes back. Runs for the whole process — independent of any session.
async Task RunRespawnTicksAsync()
{
    while (true)
    {
        await Task.Delay(RespawnTickSeconds * 1000);
        world.AdvanceRespawnTick();
        foreach (var room in world.Map.Areas.SelectMany(a => a.Rooms))
            room.Healer?.ResetCapacity();
    }
}

// Passive regen: every RegenTickSeconds, the player quietly gets CombatConstants.RegenAmountPerTick
// HP *and* MP back, on top of anything else (bandaging, the healer). Runs whether in combat or not —
// the amount is small enough that it's not a meaningful escape valve mid-fight. Paused while bleeding
// or dead (Heal() already no-ops off Alive; RecoverMp doesn't, so it's gated here too) — same as
// nobody self-bandages, nobody self-regens out of a downed state either. No narration: it just nudges
// the HP/MP prompt (LineEditor.Print with an empty write) so it stays live without spamming the log.
async Task RunPlayerRegenAsync()
{
    while (true)
    {
        await Task.Delay(RegenTickSeconds * 1000);

        if (player is { IsDown: false } p)
        {
            p.Heal(CombatConstants.RegenAmountPerTick);
            p.RecoverMp(CombatConstants.RegenAmountPerTick);
            LineEditor.Print(() => { });
        }
    }
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
    npc.GrantLootRights(player!.Name);

    // Player typed "kill" -> keeps initiative every round.
    var engine = new CombatEngine(player!, npc, rng);
    engine.OnAttackResult += (attacker, defender, result) =>
    {
        LineEditor.Print(() => EmoteHighlighter.WriteLine(result.EmoteText, result.Tier));
        // XP for every point of damage you land — a miss (0 damage) is a harmless no-op here.
        if (ReferenceEquals(attacker, player))
            player!.AddExperience(result.Damage);
    };
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

// Corpses first (loot-rights gated), then the room floor (open to anyone). Prefers stowing in the
// backpack if you have room; otherwise a free hand works fine too — no backpack required for that.
void HandleTake(string[] parts)
{
    if (!RequirePlayer() || !PlayerCanAct())
        return;

    if (parts.Length < 2)
    {
        Console.WriteLine("Take what?");
        return;
    }

    var name = string.Join(' ', parts.Skip(1));
    var room = playerRoom!;

    Corpse? sourceCorpse = null;
    Item? item = null;

    foreach (var corpse in world.CorpsesIn(room))
    {
        var match = corpse.Loot.FirstOrDefault(i => i.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
        if (match == null)
            continue;

        if (!corpse.CanLoot(player!.Name))
        {
            Console.WriteLine($"You didn't earn the right to loot {corpse.Name}.");
            return;
        }

        sourceCorpse = corpse;
        item = match;
        break;
    }

    bool fromGround = false;
    if (item == null)
    {
        item = world.ItemsOnGround(room).FirstOrDefault(i => i.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
        fromGround = item != null;
    }

    if (item == null)
    {
        Console.WriteLine("You don't see that here.");
        return;
    }

    Weapon? sheathed = null;
    bool stowedInBackpack = player!.Backpack != null && player.TryStoreInBackpack(item);
    if (!stowedInBackpack && !player.TryHoldItem(item, out sheathed))
    {
        Console.WriteLine(player.Backpack != null
            ? "Your backpack is full and your hands are too."
            : "Your hands are full.");
        return;
    }

    if (sourceCorpse != null)
        sourceCorpse.Loot.Remove(item);
    else if (fromGround)
        world.RemoveFromGround(room.Id, item);

    if (sheathed != null)
        Console.WriteLine($"You sheath your {sheathed.Name}.");

    Console.WriteLine($"You take {item.Name}.");
}

void HandleListShop()
{
    if (!RequirePlayer())
        return;

    var shop = playerRoom?.Shop;
    if (shop == null)
    {
        Console.WriteLine("There is nothing to buy here.");
        return;
    }

    Console.WriteLine($"-- {shop.Name} --");
    foreach (var weapon in shop.Weapons)
        Console.WriteLine($"  {weapon.Name} - {weapon.Value} gold");
    foreach (var armor in shop.Armor)
        Console.WriteLine($"  {armor.Name} - {armor.Value} gold");
    if (shop.Backpack != null)
        Console.WriteLine($"  {shop.Backpack.Name} - {shop.Backpack.Value} gold");
}

// Buying always auto-sheaths whatever weapon you're currently holding, same as picking something up.
void HandleBuy(string[] parts)
{
    if (!RequirePlayer())
        return;

    var shop = playerRoom?.Shop;
    if (shop == null)
    {
        Console.WriteLine("There is nothing to buy here.");
        return;
    }

    if (parts.Length < 2)
    {
        Console.WriteLine("Buy what? Try: list");
        return;
    }

    var name = string.Join(' ', parts.Skip(1));

    // A bought weapon goes straight into its own sheath — it never touches your hands, so there's
    // nothing to make room for.
    var weapon = shop.Weapons.FirstOrDefault(w => w.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
    if (weapon != null)
    {
        if (!TryPay(weapon.Value))
            return;

        var bought = CloneWeapon(weapon);
        player!.SheathedWeapons.Add(bought);
        Console.WriteLine($"You buy a {bought.Name} for {weapon.Value} gold. It's already in its sheath.");
        return;
    }

    // Unworn armor has to be held, though — refuse up front (before paying) if there'd be no room
    // for it even after sheathing whatever's wielded.
    var armor = shop.Armor.FirstOrDefault(a => a.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
    if (armor != null)
    {
        double needed = Math.Max(armor.Bulk, 1);
        double handSpaceIfSheathed = player!.FreeHandBulk + (player.Weapon.IsUnarmed ? 0 : Math.Max(player.Weapon.Bulk, 1));
        if (handSpaceIfSheathed < needed)
        {
            Console.WriteLine("Your hands are full.");
            return;
        }

        if (!TryPay(armor.Value))
            return;

        var bought = CloneArmor(armor);
        var sheathed = player.FreeHandBulk < needed ? player.SheathCurrentWeapon() : null;
        player.HeldArmor.Add(bought);
        Console.WriteLine(sheathed != null
            ? $"You sheath your {sheathed.Name} and buy a {bought.Name} for {armor.Value} gold. You are holding it."
            : $"You buy a {bought.Name} for {armor.Value} gold. You are holding it.");
        return;
    }

    // The backpack goes straight onto your back — no hand space needed either.
    if (shop.Backpack != null && shop.Backpack.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
    {
        if (player!.Backpack != null)
        {
            Console.WriteLine("You already have a backpack.");
            return;
        }

        if (!TryPay(shop.Backpack.Value))
            return;

        player.Backpack = CloneBackpack(shop.Backpack);
        Console.WriteLine($"You buy a {player.Backpack.Name} for {shop.Backpack.Value} gold. You put it on.");
        return;
    }

    Console.WriteLine("The shop doesn't sell that.");

    bool TryPay(int cost)
    {
        if (player!.SpendGold(cost))
            return true;

        Console.WriteLine($"You can't afford that ({cost} gold; you have {player.Gold}).");
        return false;
    }
}

// Only sells what's already unequipped (sheathed weapons, held-but-unworn armor, backpack items) —
// equipped gear needs to be removed/sheathed first, so you don't sell what you're actively using
// by accident. Any shop buys anything, at CombatConstants.ShopSellFraction of its Value (min 1 gold).
void HandleSell(string[] parts)
{
    if (!RequirePlayer())
        return;

    if (playerRoom?.Shop == null)
    {
        Console.WriteLine("There is nowhere to sell that here.");
        return;
    }

    if (parts.Length < 2)
    {
        Console.WriteLine("Sell what?");
        return;
    }

    var name = string.Join(' ', parts.Skip(1));

    var weapon = player!.SheathedWeapons.FirstOrDefault(w => w.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
    if (weapon != null)
    {
        player.SheathedWeapons.Remove(weapon);
        Paid(weapon.Name, weapon.Value);
        return;
    }

    var armor = player.HeldArmor.FirstOrDefault(a => a.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
    if (armor != null)
    {
        player.HeldArmor.Remove(armor);
        Paid(armor.Name, armor.Value);
        return;
    }

    var item = player.BackpackItems.FirstOrDefault(i => i.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
    if (item != null)
    {
        player.BackpackItems.Remove(item);
        Paid(item.Name, item.Value);
        return;
    }

    var held = player.HeldItems.FirstOrDefault(i => i.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
    if (held != null)
    {
        player.HeldItems.Remove(held);
        Paid(held.Name, held.Value);
        return;
    }

    bool stillWorn = player.EquippedArmor.Any(a => a.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
        || (!player.Weapon.IsUnarmed && player.Weapon.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
    Console.WriteLine(stillWorn ? "You'll need to remove or sheath that first." : "You don't have that.");

    void Paid(string itemName, int baseValue)
    {
        int price = Math.Min(CombatConstants.MaxSellPrice, Math.Max(1, (int)Math.Round(baseValue * CombatConstants.ShopSellFraction)));
        player.AddGold(price);
        Console.WriteLine($"You sell {itemName} for {price} gold.");
    }
}

static Weapon CloneWeapon(Weapon w) => new()
{
    Name = w.Name,
    Hit = w.Hit,
    Damage = w.Damage,
    CritChanceBonus = w.CritChanceBonus,
    CritPower = w.CritPower,
    CustomEmotes = w.CustomEmotes,
    Bulk = w.Bulk,
    IsUnarmed = w.IsUnarmed,
    Value = w.Value,
    MaxDurability = w.MaxDurability
};

static Armor CloneArmor(Armor a) => new()
{
    Name = a.Name,
    Absorb = a.Absorb,
    Deflect = a.Deflect,
    Slots = new HashSet<BodySlot>(a.Slots),
    Bulk = a.Bulk,
    Value = a.Value,
    MaxDurability = a.MaxDurability
};

static Backpack CloneBackpack(Backpack b) => new() { Name = b.Name, Capacity = b.Capacity, Value = b.Value };

void HandleGold()
{
    if (!RequirePlayer())
        return;

    Console.WriteLine(player!.Gold == 1 ? "You have 1 gold." : $"You have {player!.Gold} gold.");
}

void HandleExperience()
{
    if (!RequirePlayer())
        return;

    Console.WriteLine($"You have {player!.Experience} experience (level {player.Level}, {player.Title}).");
}

// One flat dose per call: CombatConstants.HealCostGold for HealAmountPerUse HP *and* the same MP —
// not "heal me to full". Call it again (and pay again) for more, as long as the healer isn't tapped
// out for this world tick (Healer.TryDispense).
void HandleHeal()
{
    if (!RequirePlayer() || !PlayerCanAct())
        return;

    var healer = playerRoom?.Healer;
    if (healer == null)
    {
        Console.WriteLine("There is no healer here.");
        return;
    }

    if (player!.CurrentHp >= player.MaxHp && player.CurrentMp >= player.MaxMp)
    {
        Console.WriteLine("You are already at full health and mana.");
        return;
    }

    if (player.Gold < CombatConstants.HealCostGold)
    {
        Console.WriteLine($"The healer charges {CombatConstants.HealCostGold} gold a treatment — you don't have enough.");
        return;
    }

    if (!healer.TryDispense(CombatConstants.HealAmountPerUse))
    {
        Console.WriteLine("The healer is worn out for now — come back after they've had time to rest.");
        return;
    }

    player.SpendGold(CombatConstants.HealCostGold);
    player.Heal(CombatConstants.HealAmountPerUse);
    player.RecoverMp(CombatConstants.HealAmountPerUse);

    Console.WriteLine($"The healer tends to you for {CombatConstants.HealCostGold} gold. " +
        $"You feel {CombatConstants.HealAmountPerUse} HP and {CombatConstants.HealAmountPerUse} MP better.");
}

void HandleScore()
{
    if (!RequirePlayer())
        return;

    var p = player!;
    Console.WriteLine($"{p.Name} the {p.Race.Name.ToLowerInvariant()} {p.Title}, level {p.Level}");
    Console.WriteLine($"Experience: {p.Experience} ({Leveling.ExperienceRequired(p.Level + 1)} needed for level {p.Level + 1})");
    Console.WriteLine($"HP: {p.CurrentHp}/{p.MaxHp}   MP: {p.CurrentMp}/{p.MaxMp}   Gold: {p.Gold}");
    Console.WriteLine("Stats:");
    Console.WriteLine($"  Strength:     {p.BaseStat(StatType.Strength):0.#}");
    Console.WriteLine($"  Constitution: {p.BaseStat(StatType.Constitution):0.#}");
    Console.WriteLine($"  Agility:      {p.BaseStat(StatType.Agility):0.#}");
    Console.WriteLine($"  Coordination: {p.BaseStat(StatType.Coordination):0.#}");
    Console.WriteLine($"  Intelligence: {p.BaseStat(StatType.Intelligence):0.#}");
    Console.WriteLine("Skills:");
    Console.WriteLine($"  Aim:          {p.BaseStat(StatType.Aim):0.#}");
    Console.WriteLine($"  Attack:       {p.BaseStat(StatType.Attack):0.#}");
    Console.WriteLine($"  Defense:      {p.BaseStat(StatType.Defense):0.#}");
    Console.WriteLine($"  Dodge:        {p.BaseStat(StatType.Dodge):0.#}");
}

// `train` alone shows the menu (also reachable via `look <trainer>`); `train <stat> [amount]`
// (amount defaults to 1) actually spends the gold, per Combat.Training's per-point pricing.
void HandleTrain(string[] parts)
{
    if (!RequirePlayer())
        return;

    var trainer = playerRoom?.Trainer;
    if (trainer == null)
    {
        Console.WriteLine("There is no trainer here.");
        return;
    }

    if (parts.Length < 2)
    {
        ShowTrainingMenu(trainer);
        return;
    }

    if (!TryParseStat(parts[1], out var stat, out var displayName))
    {
        Console.WriteLine("You can't train that. Try: train");
        return;
    }

    int amount = 1;
    if (parts.Length >= 3 && (!int.TryParse(parts[2], out amount) || amount <= 0))
    {
        Console.WriteLine("Usage: train <stat> [amount]");
        return;
    }

    if (!player!.TryTrain(stat, amount, out var error))
    {
        Console.WriteLine(error);
        return;
    }

    Console.WriteLine($"The trainer works with you on {displayName}. It's now {player.BaseStat(stat):0.#}.");
}

void HandleLevelUp()
{
    if (!RequirePlayer())
        return;

    if (playerRoom?.Trainer == null)
    {
        Console.WriteLine("There is no trainer here.");
        return;
    }

    if (!player!.TryLevelUp(out var error))
        Console.WriteLine(error);
    // On success, the OnLevelUp subscription set up in EnterWorld already announces it.
}

void ShowTrainingMenu(Trainer trainer)
{
    Console.WriteLine($"-- {trainer.Name} --");
    Console.WriteLine($"Training cap at level {player!.Level}: {player.TrainingCap} per stat/skill.");
    Console.WriteLine("Stats:");
    PrintTrainLine(StatType.Strength, "Strength");
    PrintTrainLine(StatType.Constitution, "Constitution");
    PrintTrainLine(StatType.Agility, "Agility");
    PrintTrainLine(StatType.Coordination, "Coordination");
    PrintTrainLine(StatType.Intelligence, "Intelligence");
    Console.WriteLine("Skills:");
    PrintTrainLine(StatType.Aim, "Aim");
    PrintTrainLine(StatType.Attack, "Attack");
    PrintTrainLine(StatType.Defense, "Defense");
    PrintTrainLine(StatType.Dodge, "Dodge");
    Console.WriteLine("Try: train <name> [amount]");

    void PrintTrainLine(StatType stat, string name)
    {
        int current = (int)player.BaseStat(stat);
        int cap = player.TrainingCap;
        Console.WriteLine(current >= cap
            ? $"  {name,-13} {current} (maxed for your level)"
            : $"  {name,-13} {current} -> next point costs {player.TrainingCostFor(stat, 1)} gold (cap {cap})");
    }
}

// `repair` alone shows the menu (also reachable via `look <blacksmith>`); `repair <item>` actually
// pays and mends it. Finds the item across everything the player could plausibly hand over: the
// weapon in hand (unless it's a natural one, which has no blacksmith to visit), sheathed weapons,
// worn armor, and armor held but not worn.
void HandleRepair(string[] parts)
{
    if (!RequirePlayer() || !PlayerCanAct())
        return;

    var blacksmith = playerRoom?.Blacksmith;
    if (blacksmith == null)
    {
        Console.WriteLine("There is no blacksmith here.");
        return;
    }

    if (parts.Length < 2)
    {
        ShowBlacksmithMenu(blacksmith);
        return;
    }

    var name = string.Join(' ', parts.Skip(1));
    var item = FindOwnDurable(name);
    if (item == null)
    {
        Console.WriteLine("You don't have that.");
        return;
    }

    if (item.HasBeenRepaired)
    {
        Console.WriteLine($"{item.Name} has already been mended once — the blacksmith won't touch it again.");
        return;
    }

    if (item.Durability >= item.MaxDurability)
    {
        Console.WriteLine($"{item.Name} doesn't need any repairs.");
        return;
    }

    int cost = RepairCost(item);
    if (player!.Gold < cost)
    {
        Console.WriteLine($"The blacksmith wants {cost} gold for that — you don't have enough.");
        return;
    }

    player.SpendGold(cost);
    item.Repair();
    Console.WriteLine($"The blacksmith mends {item.Name} for {cost} gold. Good as new — though they won't be able to fix it a second time.");
}

// Works anywhere, not just at the blacksmith: it's just you checking your own gear.
void HandleCondition(string[] parts)
{
    if (!RequirePlayer())
        return;

    if (parts.Length < 2)
    {
        Console.WriteLine("Check the condition of what? Try: condition sword");
        return;
    }

    var name = string.Join(' ', parts.Skip(1));
    var item = FindOwnDurable(name);
    if (item == null)
    {
        Console.WriteLine("You don't have that.");
        return;
    }

    Console.WriteLine(item.IsBroken
        ? $"{item.Name} is broken. It's useless until a blacksmith repairs it."
        : $"{item.Name} is in {DescribeItemCondition(item)}.");

    if (item.HasBeenRepaired)
        Console.WriteLine("It's already been mended once — no blacksmith will touch it again.");
}

void ShowBlacksmithMenu(Blacksmith blacksmith)
{
    Console.WriteLine($"-- {blacksmith.Name} --");

    var items = GetAllOwnDurables().ToList();
    if (items.Count == 0)
    {
        Console.WriteLine("You have nothing that could need repairs.");
        return;
    }

    foreach (var item in items)
    {
        if (item.HasBeenRepaired)
            Console.WriteLine($"  {item.Name} — already mended once, can't be repaired again.");
        else if (item.Durability >= item.MaxDurability)
            Console.WriteLine($"  {item.Name} — pristine condition, nothing to repair.");
        else
            Console.WriteLine($"  {item.Name} — {DescribeItemCondition(item)}, repair costs {RepairCost(item)} gold.");
    }
    Console.WriteLine("Try: repair <name>");
}

// Placeholder wear-and-tear flavor text/thresholds — easy to swap for something else later. Every
// tier reads naturally after "X is in ___" (HandleCondition) and before ", repair costs..." (the
// blacksmith menu), so keep new tiers ending in "condition".
static string DescribeItemCondition(IDurableItem item)
{
    double fraction = (double)item.Durability / item.MaxDurability;
    return fraction switch
    {
        >= 1.0 => "pristine condition",
        >= 0.8 => "good condition",
        >= 0.6 => "fair condition",
        >= 0.4 => "worn condition",
        >= 0.2 => "poor condition",
        _ => "terrible condition"
    };
}

// Fully mending it (all the way from 0 durability) costs RepairCostFraction of its Value; less if
// it's only partway worn, scaled by how much durability is actually missing.
static int RepairCost(IDurableItem item)
{
    int missing = item.MaxDurability - item.Durability;
    double missingFraction = (double)missing / item.MaxDurability;
    return Math.Max(1, (int)Math.Round(item.Value * CombatConstants.RepairCostFraction * missingFraction));
}

IDurableItem? FindOwnDurable(string name)
{
    if (!player!.Weapon.IsUnarmed && player.Weapon.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
        return player.Weapon;

    var weapon = player.SheathedWeapons.FirstOrDefault(w => w.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
    if (weapon != null)
        return weapon;

    var equipped = player.EquippedArmor.FirstOrDefault(a => a.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
    if (equipped != null)
        return equipped;

    return player.HeldArmor.FirstOrDefault(a => a.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
}

IEnumerable<IDurableItem> GetAllOwnDurables()
{
    if (!player!.Weapon.IsUnarmed)
        yield return player.Weapon;
    foreach (var w in player.SheathedWeapons)
        yield return w;
    foreach (var a in player.EquippedArmor)
        yield return a;
    foreach (var a in player.HeldArmor)
        yield return a;
}

// A repaired item's own name always shows the fact — per rule 8quinquies, this is the one thing about
// an item's condition that's visible just by looking at it; everything else needs `condition`.
static string DisplayName(IDurableItem item) => item.HasBeenRepaired ? $"{item.Name} (repaired)" : item.Name;

static bool TryParseStat(string text, out StatType stat, out string displayName)
{
    switch (text.ToLowerInvariant())
    {
        case "strength": case "str": stat = StatType.Strength; displayName = "Strength"; return true;
        case "constitution": case "con": stat = StatType.Constitution; displayName = "Constitution"; return true;
        case "agility": case "agi": stat = StatType.Agility; displayName = "Agility"; return true;
        case "coordination": case "coord": stat = StatType.Coordination; displayName = "Coordination"; return true;
        case "intelligence": case "int": stat = StatType.Intelligence; displayName = "Intelligence"; return true;
        case "aim": stat = StatType.Aim; displayName = "Aim"; return true;
        case "attack": stat = StatType.Attack; displayName = "Attack"; return true;
        case "defense": stat = StatType.Defense; displayName = "Defense"; return true;
        case "dodge": stat = StatType.Dodge; displayName = "Dodge"; return true;
        default: stat = default; displayName = ""; return false;
    }
}

// Works for a sheathed weapon (draws it) or a held shield (readies it) — whichever matches the name.
void HandleWield(string[] parts)
{
    if (!RequirePlayer() || !PlayerCanAct())
        return;

    if (parts.Length < 2)
    {
        Console.WriteLine("Wield what? Try: wield sword");
        return;
    }

    var name = string.Join(' ', parts.Skip(1));

    var weapon = player!.SheathedWeapons.FirstOrDefault(w => w.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
    if (weapon != null)
    {
        var previous = player.Wield(weapon);
        if (!ReferenceEquals(player.Weapon, weapon))
        {
            Console.WriteLine("Your hands are full.");
            return;
        }

        Console.WriteLine(previous != null
            ? $"You sheath your {previous.Name} and draw your {weapon.Name}."
            : $"You draw your {weapon.Name}.");
        return;
    }

    var shield = player.HeldArmor.FirstOrDefault(a => a.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
    if (shield != null)
    {
        if (!player.TryEquipArmor(shield, out var error))
        {
            Console.WriteLine(error);
            return;
        }

        Console.WriteLine($"You ready your {shield.Name}.");
        return;
    }

    Console.WriteLine("You don't have that.");
}

void HandleWear(string[] parts)
{
    if (!RequirePlayer() || !PlayerCanAct())
        return;

    if (parts.Length < 2)
    {
        Console.WriteLine("Wear what? Try: wear shield");
        return;
    }

    var name = string.Join(' ', parts.Skip(1));
    var armor = player!.HeldArmor.FirstOrDefault(a => a.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
    if (armor == null)
    {
        Console.WriteLine("You aren't holding that.");
        return;
    }

    if (!player.TryEquipArmor(armor, out var error))
    {
        Console.WriteLine(error);
        return;
    }

    Console.WriteLine($"You put on your {armor.Name}.");
}

void HandleRemoveArmor(string[] parts)
{
    if (!RequirePlayer() || !PlayerCanAct())
        return;

    if (parts.Length < 2)
    {
        Console.WriteLine("Remove what? Try: remove shield");
        return;
    }

    var name = string.Join(' ', parts.Skip(1));
    var armor = player!.EquippedArmor.FirstOrDefault(a => a.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
    if (armor == null)
    {
        Console.WriteLine("You aren't wearing that.");
        return;
    }

    player.UnequipArmor(armor);
    Console.WriteLine($"You remove your {armor.Name}.");
}

void HandleSheath(string[] parts)
{
    if (!RequirePlayer() || !PlayerCanAct())
        return;

    if (player!.Weapon.IsUnarmed)
    {
        Console.WriteLine("Your hands are already empty.");
        return;
    }

    if (parts.Length >= 2 && !player.Weapon.Name.Contains(string.Join(' ', parts.Skip(1)), StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("You aren't holding that.");
        return;
    }

    var weaponName = player.Weapon.Name;
    player.SheathCurrentWeapon();
    Console.WriteLine($"You sheath your {weaponName}.");
}

void HandleHands()
{
    if (!RequirePlayer())
        return;

    var held = new List<string>();
    if (!player!.Weapon.IsUnarmed)
        held.Add(player.Weapon.Name);
    held.AddRange(player.EquippedArmor.Where(a => a.Slots.Contains(BodySlot.Shield)).Select(a => a.Name));
    held.AddRange(player.HeldArmor.Select(a => a.Name));
    held.AddRange(player.HeldItems.Select(i => i.Name));

    Console.WriteLine(held.Count == 0
        ? "Your hands are empty."
        : $"You are holding: {string.Join(", ", held)} ({FormatBulk(player.UsedHandBulk)} of {FormatBulk(CombatConstants.HandCapacity)} hand space).");
}

// Bulk numbers are always displayed with an invariant "." decimal point, regardless of the host
// machine's locale — this is an English-only game (see CLAUDE.md), so output shouldn't vary by OS
// region settings the way plain string interpolation of a double would.
static string FormatBulk(double value) => value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

void HandleInventory()
{
    if (!RequirePlayer())
        return;

    Console.WriteLine(player!.Weapon.IsUnarmed ? "Hands: empty." : $"Hands: {DisplayName(player.Weapon)}.");

    if (player.SheathedWeapons.Count > 0)
        Console.WriteLine("Sheathed: " + string.Join(", ", player.SheathedWeapons.Select(DisplayName)));

    if (player.EquippedArmor.Count > 0)
    {
        Console.WriteLine("Wearing:");
        foreach (var armor in player.EquippedArmor)
            Console.WriteLine($"  {DisplayName(armor)} ({string.Join('/', armor.Slots)})");
    }

    if (player.HeldArmor.Count > 0 || player.HeldItems.Count > 0)
    {
        var heldNames = player.HeldArmor.Select(DisplayName).Concat(player.HeldItems.Select(i => i.Name));
        Console.WriteLine("In hand (not worn/stowed): " + string.Join(", ", heldNames));
    }

    if (player.Backpack is { } backpack)
    {
        Console.WriteLine($"{backpack.Name} ({FormatBulk(player.UsedBackpackBulk)}/{FormatBulk(backpack.Capacity)} bulk):");
        if (player.BackpackItems.Count == 0)
            Console.WriteLine("  (empty)");
        else
            foreach (var item in player.BackpackItems)
                Console.WriteLine($"  {item.Name}");
    }
    else
    {
        Console.WriteLine("You have no backpack.");
    }

    Console.WriteLine($"Gold: {player.Gold}");
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
