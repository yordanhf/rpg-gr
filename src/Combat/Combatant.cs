namespace Combat;

public enum LifeState { Alive, Bleeding, Dead }

public class Combatant
{
    /// Data id for NPCs (the file name in data/npcs), set by the loader. Used to match `kill rat`, `look rat`...
    public string Id { get; set; } = "";

    public required string Name { get; init; }

    /// Shown by `look <target>`.
    public string Description { get; init; } = "";
    public required int MaxHp { get; init; }
    /// Can go below 0: 0 or less means bleeding (or dead if far enough below, see CombatConstants.InstantDeathHp).
    public int CurrentHp { get; private set; }

    /// Not spent by anything yet — reserved for profession skills/spells that will cost MP later.
    public int MaxMp { get; init; }
    public int CurrentMp { get; private set; }

    /// Drives emote perspective ("you" vs. third person) — independent of combat initiative,
    /// since an aggressive NPC can have initiative while the player is still "you" in the text.
    public bool IsPlayer { get; init; }

    public Race Race { get; init; } = Race.None;

    private Weapon _weapon = null!;
    private Weapon? _unarmedWeapon;

    /// The weapon currently in hand. Setting it (JSON load, or mid-game via Wield/SheathCurrentWeapon)
    /// remembers the first IsUnarmed weapon it's ever given as the fallback to revert to when sheathing.
    public required Weapon Weapon
    {
        get => _weapon;
        set
        {
            _weapon = value;
            _unarmedWeapon ??= value.IsUnarmed ? value : null;
        }
    }

    public List<Armor> EquippedArmor { get; init; } = new();

    /// Armor pieces owned but not currently worn — physically in hand (unlike a sheathed weapon,
    /// armor has nowhere free to sit), so they cost hand space same as if you'd just picked them up.
    public List<Armor> HeldArmor { get; init; } = new();

    /// Weapons owned but not currently wielded — always available at no bulk cost ("their sheath").
    public List<Weapon> SheathedWeapons { get; init; } = new();

    /// Bought at a shop; a fresh character has none.
    public Backpack? Backpack { get; set; }
    public List<Item> BackpackItems { get; init; } = new();

    /// What this NPC drops on death (e.g. a pelt) — stays on its corpse, not the room floor, until
    /// the corpse fades. Irrelevant for the player.
    public List<Item> Loot { get; init; } = new();

    /// Names of whoever fought this NPC (not just the killing blow) — only they may loot its corpse
    /// while it's still fresh. Everyone can once the corpse fades and the loot hits the floor.
    public HashSet<string> LootRights { get; } = new(StringComparer.OrdinalIgnoreCase);

    public void GrantLootRights(string name) => LootRights.Add(name);

    // Base stats and skills, normally 0-100 (buffs can push the effective value up to CombatConstants.MaxEffectiveStat).
    public required double Strength { get; init; }
    public required double Constitution { get; init; }
    public required double Agility { get; init; }
    public required double Coordination { get; init; }
    public required double Intelligence { get; init; }
    public required double Aim { get; init; }
    public required double Attack { get; init; }
    public required double Defense { get; init; }
    public required double Dodge { get; init; }

    public LifeState State { get; private set; } = LifeState.Alive;
    public int BleedRoundsLeft { get; private set; }

    public bool IsDead => State == LifeState.Dead;
    public bool IsBleeding => State == LifeState.Bleeding;

    /// On the ground (bleeding or dead): can't attack or act.
    public bool IsDown => State != LifeState.Alive;

    /// Sum of Absorb across all equipped armor (broken pieces contribute 0), capped at CombatConstants.MaxArmorPoolTotal.
    public double TotalAbsorb => Math.Min(CombatConstants.MaxArmorPoolTotal, EquippedArmor.Sum(a => a.EffectiveAbsorb));

    /// Sum of Deflect across all equipped armor (broken pieces contribute 0), capped at CombatConstants.MaxArmorPoolTotal.
    public double TotalDeflect => Math.Min(CombatConstants.MaxArmorPoolTotal, EquippedArmor.Sum(a => a.EffectiveDeflect));

    public int Gold { get; private set; }

    public void AddGold(int amount) => Gold += Math.Max(0, amount);

    /// Returns false (refuses) if there isn't enough gold to spend.
    public bool SpendGold(int amount)
    {
        if (amount < 0 || amount > Gold)
            return false;

        Gold -= amount;
        return true;
    }

    /// Sets the gold balance read back from a save file.
    public void RestoreGold(int amount) => Gold = Math.Max(0, amount);

    /// Hand-bulk currently spoken for: the wielded weapon, a worn shield (it still needs a hand even
    /// though it's "equipped" — unlike the other five slots, which are strapped on and cost nothing),
    /// and anything held-but-unworn.
    public double UsedHandBulk =>
        (Weapon.IsUnarmed ? 0 : Weapon.Bulk)
        + EquippedArmor.Where(a => a.Slots.Contains(BodySlot.Shield)).Sum(a => a.Bulk)
        + HeldArmor.Sum(a => a.Bulk);

    public double FreeHandBulk => Math.Max(0, CombatConstants.HandCapacity - UsedHandBulk);

    /// Fails if a slot the piece needs is already covered by something else worn, or (shields only)
    /// there isn't a free hand for it. `error` explains which.
    public bool TryEquipArmor(Armor armor, out string? error)
    {
        var conflict = EquippedArmor.FirstOrDefault(a => a.Slots.Overlaps(armor.Slots));
        if (conflict != null)
        {
            error = $"You need to remove your {conflict.Name} first.";
            return false;
        }

        if (armor.Slots.Contains(BodySlot.Shield))
        {
            // Moving it from held-but-unworn to worn-as-a-shield doesn't add new hand load — it's
            // already counted once (in HeldArmor); don't count it twice against its own move.
            double handBulkWithoutThis = UsedHandBulk - (HeldArmor.Contains(armor) ? armor.Bulk : 0);
            if (handBulkWithoutThis + armor.Bulk > CombatConstants.HandCapacity)
            {
                error = "Your hands are full.";
                return false;
            }
        }

        HeldArmor.Remove(armor);
        EquippedArmor.Add(armor);
        error = null;
        return true;
    }

    /// Unequips a worn piece — it ends up held in your hand, not stored away.
    public void UnequipArmor(Armor armor)
    {
        if (!EquippedArmor.Remove(armor))
            return;

        HeldArmor.Add(armor);
    }

    /// Draws a weapon from its sheath into your hands, sheathing whatever was wielded before (if
    /// anything). Returns null if the weapon isn't actually in your sheaths, or there isn't enough
    /// free hand space for it even after sheathing the current one.
    public Weapon? Wield(Weapon weapon)
    {
        if (!SheathedWeapons.Contains(weapon))
            return null;

        var previous = SheathCurrentWeapon();
        if (weapon.Bulk > FreeHandBulk)
        {
            // Doesn't fit (e.g. a shield is using a hand) — put it back and undo the auto-sheath.
            if (previous != null)
                Wield(previous);
            return null;
        }

        SheathedWeapons.Remove(weapon);
        Weapon = weapon;
        return previous;
    }

    /// Sheaths whatever's currently wielded and falls back to bare hands. No-op (returns null) if
    /// you're already unarmed. Also how the game auto-frees your hands when you acquire a new item.
    public Weapon? SheathCurrentWeapon()
    {
        if (Weapon.IsUnarmed || _unarmedWeapon == null)
            return null;

        var sheathed = Weapon;
        SheathedWeapons.Add(sheathed);
        Weapon = _unarmedWeapon;
        return sheathed;
    }

    public double UsedBackpackBulk => BackpackItems.Sum(i => i.Bulk);
    public double FreeBackpackBulk => Backpack == null ? 0 : Math.Max(0, Backpack.Capacity - UsedBackpackBulk);

    /// Fails if there's no backpack, or not enough room left in it.
    public bool TryStoreInBackpack(Item item)
    {
        if (Backpack == null || item.Bulk > FreeBackpackBulk)
            return false;

        BackpackItems.Add(item);
        return true;
    }

    private readonly Queue<Skill> _queuedSkills = new();
    private readonly Dictionary<Skill, int> _cooldowns = new();
    private readonly List<StatModifier> _modifiers = new();

    /// Call once after object initialization, since CurrentHp can't be defaulted from MaxHp in an initializer.
    public void ResetHp()
    {
        CurrentHp = MaxHp;
        State = LifeState.Alive;
        BleedRoundsLeft = 0;
    }

    public void ResetMp() => CurrentMp = MaxMp;

    /// Puts HP back to a stored value (loading a saved character). Always leaves the combatant alive.
    public void RestoreHp(int hp)
    {
        CurrentHp = Math.Clamp(hp, 1, MaxHp);
        State = LifeState.Alive;
        BleedRoundsLeft = 0;
    }

    public void RestoreMp(int mp) => CurrentMp = Math.Clamp(mp, 0, MaxMp);

    public void ApplyDamage(int amount)
    {
        CurrentHp -= amount;

        if (CurrentHp <= -CombatConstants.InstantDeathHp)
        {
            State = LifeState.Dead;
        }
        else if (CurrentHp <= 0)
        {
            State = LifeState.Bleeding;
            BleedRoundsLeft = CombatConstants.BleedRounds;
        }
    }

    /// Finishes off a bleeding combatant.
    public void Kill() => State = LifeState.Dead;

    /// Bandages a bleeding combatant: stops the bleeding and leaves them at a fraction of MaxHp, able to fight again.
    public void Stabilize()
    {
        if (State != LifeState.Bleeding)
            return;

        CurrentHp = Math.Max(1, (int)Math.Ceiling(MaxHp * CombatConstants.BandageHealFraction));
        State = LifeState.Alive;
        BleedRoundsLeft = 0;
    }

    /// One round of bleeding. Returns true if this combatant just bled out (and is now dead).
    public bool TickBleed()
    {
        if (State != LifeState.Bleeding)
            return false;

        BleedRoundsLeft--;
        if (BleedRoundsLeft > 0)
            return false;

        State = LifeState.Dead;
        return true;
    }

    public double EffectiveStat(StatType stat)
    {
        double baseValue = GetBaseStat(stat) * (1 + Race.PercentFor(stat) / 100);
        double modifierSum = _modifiers.Where(m => m.Stat == stat).Sum(m => m.Amount);
        return Math.Clamp(baseValue + modifierSum, 0, CombatConstants.MaxEffectiveStat);
    }

    private double GetBaseStat(StatType stat) => stat switch
    {
        StatType.Strength => Strength,
        StatType.Constitution => Constitution,
        StatType.Agility => Agility,
        StatType.Coordination => Coordination,
        StatType.Intelligence => Intelligence,
        StatType.Aim => Aim,
        StatType.Attack => Attack,
        StatType.Defense => Defense,
        StatType.Dodge => Dodge,
        _ => throw new ArgumentOutOfRangeException(nameof(stat))
    };

    public void AddModifier(StatModifier modifier) => _modifiers.Add(modifier);

    public bool QueueSkill(Skill skill)
    {
        if (IsOnCooldown(skill))
            return false;

        _queuedSkills.Enqueue(skill);
        return true;
    }

    public bool IsOnCooldown(Skill skill) => _cooldowns.TryGetValue(skill, out var remaining) && remaining > 0;

    public Skill? TakeQueuedSkillIfReady()
    {
        while (_queuedSkills.Count > 0)
        {
            var skill = _queuedSkills.Dequeue();
            if (IsOnCooldown(skill))
                continue;

            _cooldowns[skill] = skill.CooldownRounds;
            return skill;
        }
        return null;
    }

    /// Cooldowns and buff/debuff durations both tick down once per round (rule 8).
    public void TickEndOfRound()
    {
        foreach (var skill in _cooldowns.Keys.ToList())
        {
            if (_cooldowns[skill] > 0)
                _cooldowns[skill]--;
        }

        for (int i = _modifiers.Count - 1; i >= 0; i--)
        {
            _modifiers[i].RemainingRounds--;
            if (_modifiers[i].RemainingRounds <= 0)
                _modifiers.RemoveAt(i);
        }
    }
}
