using Combat;

namespace World;

/// What gets stored of a player character: plain data only, separate from Combatant (which carries runtime
/// state such as cooldowns and buffs). The race is stored by id and the location by room id.
///
/// Gear is NOT persisted on purpose: weapons, armor, backpack, and any loose/held items are gone when
/// the character quits, unless sold first for gold beforehand. The engine has no server process that
/// outlives a single run (`quit` ends the whole program, taking the in-memory world — corpses, ground
/// items, everything — with it), so "leave it on the floor for next time" isn't something this
/// prototype can actually deliver; losing it is the honest, simple alternative.
public class CharacterSave
{
    /// Bumped when the format changes, so older saves can be migrated instead of failing to load.
    public int Version { get; init; } = 1;

    public required string Name { get; init; }
    public required string RaceId { get; init; }

    /// Added after some characters already existed; defaults to "civilian" (the only profession so
    /// far) so a save from before this field existed still loads instead of crashing `continue`.
    public string ProfessionId { get; init; } = "civilian";

    public required string RoomId { get; init; }

    public int Level { get; init; } = 1;

    public required int MaxHp { get; init; }
    public required int CurrentHp { get; init; }
    public required int MaxMp { get; init; }
    public required int CurrentMp { get; init; }

    public required double Strength { get; init; }
    public required double Constitution { get; init; }
    public required double Agility { get; init; }
    public required double Coordination { get; init; }
    public required double Intelligence { get; init; }
    public required double Aim { get; init; }
    public required double Attack { get; init; }
    public required double Defense { get; init; }
    public required double Dodge { get; init; }

    public int Gold { get; init; }
    public int Experience { get; init; }

    /// Player-defined command shortcuts (`alias`). Added after some characters already existed;
    /// defaults to empty so an old save without this key still loads instead of crashing `continue`
    /// (same lesson as ProfessionId below).
    public Dictionary<string, string> Aliases { get; init; } = new();

    /// A character that is down (bleeding or dead) is saved as if bandaged: alive, at a fraction of max HP,
    /// because nobody would be around to help them while the game is closed.
    public static CharacterSave From(Combatant player, string roomId)
    {
        int hp = player.IsDown
            ? Math.Max(1, (int)Math.Ceiling(player.MaxHp * CombatConstants.BandageHealFraction))
            : player.CurrentHp;

        return new CharacterSave
        {
            Name = player.Name,
            RaceId = player.Race.Id,
            ProfessionId = player.Profession.Id,
            RoomId = roomId,
            Level = player.Level,
            MaxHp = player.MaxHp,
            CurrentHp = hp,
            MaxMp = player.MaxMp,
            CurrentMp = player.CurrentMp,
            Strength = player.Strength,
            Constitution = player.Constitution,
            Agility = player.Agility,
            Coordination = player.Coordination,
            Intelligence = player.Intelligence,
            Aim = player.Aim,
            Attack = player.Attack,
            Defense = player.Defense,
            Dodge = player.Dodge,
            Gold = player.Gold,
            Experience = player.Experience,
            Aliases = new Dictionary<string, string>(player.Aliases)
        };
    }

    /// `startingWeapon` is a fresh copy of the game's default unarmed weapon (Fists) — a returning
    /// character always starts bare-handed and bare-armored, same as a brand new one.
    public Combatant ToCombatant(Func<string, Race> findRace, Func<string, Profession> findProfession, Weapon startingWeapon)
    {
        var player = new Combatant
        {
            Name = Name,
            IsPlayer = true,
            Race = findRace(RaceId),
            Profession = findProfession(ProfessionId),
            MaxHp = MaxHp,
            MaxMp = MaxMp,
            Strength = Strength,
            Constitution = Constitution,
            Agility = Agility,
            Coordination = Coordination,
            Intelligence = Intelligence,
            Aim = Aim,
            Attack = Attack,
            Defense = Defense,
            Dodge = Dodge,
            Weapon = startingWeapon,
            Aliases = new Dictionary<string, string>(Aliases, StringComparer.OrdinalIgnoreCase)
        };

        player.ResetHp();
        player.ResetMp();
        player.RestoreHp(CurrentHp);
        player.RestoreMp(CurrentMp);
        player.RestoreGold(Gold);
        player.RestoreExperience(Experience);
        player.RestoreLevel(Level);
        return player;
    }
}

/// Where characters are kept. The prototype uses JSON files; a server would plug in a database (EF Core) here.
public interface ICharacterStore
{
    bool Exists(string name);
    CharacterSave? Load(string name);
    void Save(CharacterSave save);
}
