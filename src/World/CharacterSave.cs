using Combat;

namespace World;

/// What gets stored of a player character: plain data only, separate from Combatant (which carries runtime
/// state such as cooldowns and buffs). The race is stored by id and the location by room id.
public class CharacterSave
{
    /// Bumped when the format changes, so older saves can be migrated instead of failing to load.
    public int Version { get; init; } = 1;

    public required string Name { get; init; }
    public required string RaceId { get; init; }
    public required string RoomId { get; init; }

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

    public required WeaponSave Weapon { get; init; }
    public List<ArmorSave> Armor { get; init; } = new();

    public record WeaponSave(string Name, double Hit, double Damage, double CritChanceBonus, double CritPower,
        int Durability, int MaxDurability, bool HasBeenRepaired);

    public record ArmorSave(string Name, double Absorb, double Deflect,
        int Durability, int MaxDurability, bool HasBeenRepaired);

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
            RoomId = roomId,
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
            Weapon = new WeaponSave(player.Weapon.Name, player.Weapon.Hit, player.Weapon.Damage,
                player.Weapon.CritChanceBonus, player.Weapon.CritPower,
                player.Weapon.Durability, player.Weapon.MaxDurability, player.Weapon.HasBeenRepaired),
            Armor = player.EquippedArmor.Select(a => new ArmorSave(a.Name, a.Absorb, a.Deflect,
                a.Durability, a.MaxDurability, a.HasBeenRepaired)).ToList()
        };
    }

    public Combatant ToCombatant(Func<string, Race> findRace)
    {
        var player = new Combatant
        {
            Name = Name,
            IsPlayer = true,
            Race = findRace(RaceId),
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
            Weapon = new Weapon
            {
                Name = Weapon.Name,
                Hit = Weapon.Hit,
                Damage = Weapon.Damage,
                CritChanceBonus = Weapon.CritChanceBonus,
                CritPower = Weapon.CritPower,
                MaxDurability = Weapon.MaxDurability
            },
            EquippedArmor = Armor.Select(a => new Armor
            {
                Name = a.Name,
                Absorb = a.Absorb,
                Deflect = a.Deflect,
                MaxDurability = a.MaxDurability
            }).ToList()
        };

        player.Weapon.RestoreDurability(Weapon.Durability, Weapon.HasBeenRepaired);
        foreach (var (save, armor) in Armor.Zip(player.EquippedArmor))
            armor.RestoreDurability(save.Durability, save.HasBeenRepaired);

        player.ResetHp();
        player.ResetMp();
        player.RestoreHp(CurrentHp);
        player.RestoreMp(CurrentMp);
        player.RestoreGold(Gold);
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
