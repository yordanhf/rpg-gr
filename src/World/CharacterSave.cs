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

    /// The character's innate unarmed weapon (Fists) — separate from Weapon, since Weapon might be
    /// a real one they're actively wielding when they save. Needed so SheathCurrentWeapon() has
    /// somewhere to fall back to after a `continue`; null only for saves from before this existed
    /// (they get a synthesized fallback instead, see ToCombatant).
    public WeaponSave? UnarmedWeapon { get; init; }

    public List<WeaponSave> SheathedWeapons { get; init; } = new();
    public List<ArmorSave> Armor { get; init; } = new();
    public List<ArmorSave> HeldArmor { get; init; } = new();
    public BackpackSave? Backpack { get; init; }
    public List<ItemSave> BackpackItems { get; init; } = new();

    public record WeaponSave(string Name, double Hit, double Damage, double CritChanceBonus, double CritPower,
        double Bulk, bool IsUnarmed, int Durability, int MaxDurability, bool HasBeenRepaired);

    public record ArmorSave(string Name, double Absorb, double Deflect, HashSet<BodySlot> Slots, double Bulk,
        int Durability, int MaxDurability, bool HasBeenRepaired);

    public record BackpackSave(string Name, double Capacity);

    public record ItemSave(string Name, double Bulk, int Value);

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
            Weapon = ToWeaponSave(player.Weapon),
            UnarmedWeapon = player.UnarmedWeapon is { } uw ? ToWeaponSave(uw) : null,
            SheathedWeapons = player.SheathedWeapons.Select(ToWeaponSave).ToList(),
            Armor = player.EquippedArmor.Select(ToArmorSave).ToList(),
            HeldArmor = player.HeldArmor.Select(ToArmorSave).ToList(),
            Backpack = player.Backpack is { } bp ? new BackpackSave(bp.Name, bp.Capacity) : null,
            BackpackItems = player.BackpackItems.Select(i => new ItemSave(i.Name, i.Bulk, i.Value)).ToList()
        };
    }

    private static WeaponSave ToWeaponSave(Weapon w) => new(w.Name, w.Hit, w.Damage, w.CritChanceBonus, w.CritPower,
        w.Bulk, w.IsUnarmed, w.Durability, w.MaxDurability, w.HasBeenRepaired);

    private static ArmorSave ToArmorSave(Armor a) => new(a.Name, a.Absorb, a.Deflect, a.Slots, a.Bulk,
        a.Durability, a.MaxDurability, a.HasBeenRepaired);

    private static Weapon FromWeaponSave(WeaponSave w)
    {
        var weapon = new Weapon
        {
            Name = w.Name,
            Hit = w.Hit,
            Damage = w.Damage,
            CritChanceBonus = w.CritChanceBonus,
            CritPower = w.CritPower,
            Bulk = w.Bulk,
            IsUnarmed = w.IsUnarmed,
            MaxDurability = w.MaxDurability
        };
        weapon.RestoreDurability(w.Durability, w.HasBeenRepaired);
        return weapon;
    }

    private static Armor FromArmorSave(ArmorSave a)
    {
        var armor = new Armor
        {
            Name = a.Name,
            Absorb = a.Absorb,
            Deflect = a.Deflect,
            Slots = a.Slots,
            Bulk = a.Bulk,
            MaxDurability = a.MaxDurability
        };
        armor.RestoreDurability(a.Durability, a.HasBeenRepaired);
        return armor;
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
            // Set to the unarmed fallback first so Combatant captures it (see Combatant.Weapon),
            // then overwritten below to whatever they actually had in hand when they saved.
            Weapon = FromWeaponSave(UnarmedWeapon ?? SynthesizedFists),
            SheathedWeapons = SheathedWeapons.Select(FromWeaponSave).ToList(),
            EquippedArmor = Armor.Select(FromArmorSave).ToList(),
            HeldArmor = HeldArmor.Select(FromArmorSave).ToList(),
            Backpack = Backpack is { } bp ? new Backpack { Name = bp.Name, Capacity = bp.Capacity } : null,
            BackpackItems = BackpackItems.Select(i => new Item { Name = i.Name, Bulk = i.Bulk, Value = i.Value }).ToList()
        };

        if (UnarmedWeapon == null || Weapon != UnarmedWeapon) // WeaponSave is a record: this compares values, not references
            player.Weapon = FromWeaponSave(Weapon); // the real weapon they were actually holding, if different

        player.ResetHp();
        player.ResetMp();
        player.RestoreHp(CurrentHp);
        player.RestoreMp(CurrentMp);
        player.RestoreGold(Gold);
        return player;
    }

    /// Fallback for saves from before UnarmedWeapon existed — a generic, harmless "Fists".
    private static readonly WeaponSave SynthesizedFists =
        new("Fists", 0, 0, 0, 0, 1, true, CombatConstants.MaxDurability, CombatConstants.MaxDurability, false);
}

/// Where characters are kept. The prototype uses JSON files; a server would plug in a database (EF Core) here.
public interface ICharacterStore
{
    bool Exists(string name);
    CharacterSave? Load(string name);
    void Save(CharacterSave save);
}
