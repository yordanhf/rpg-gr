namespace Combat;

public enum LifeState { Alive, Bleeding, Dead }

public class Combatant
{
    /// Data id for NPCs (the file name in data/npcs), set by the loader. Used to match `kill rat`, `look rat`...
    public string Id { get; set; } = "";

    public required string Name { get; init; }

    /// Shown by `look <target>`.
    public string Description { get; init; } = "";

    /// How many 10-minute world ticks must pass after death before this NPC respawns. 1 for common
    /// animals; a rare/important NPC would use a much higher number.
    public int RespawnTicks { get; init; } = 1;

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
    public Profession Profession { get; init; } = Profession.None;

    public int Level { get; private set; } = 1;

    /// Fires with the new level each time one is gained (possibly several times in a row from one
    /// big XP award). A presentation layer uses this to announce it — Combat stays silent otherwise.
    public event Action<int>? OnLevelUp;

    public string Title => Profession.TitleFor(Level);

    private Weapon _weapon = null!;
    private Weapon? _unarmedWeapon;

    /// The weapon currently in hand. Setting it (JSON load, or mid-game via Wield/SheathCurrentWeapon)
    /// remembers the first IsUnarmed weapon it's ever given as the fallback to revert to when sheathing.
    /// A loader restoring a character who saved mid-combat with a real weapon in hand must set this
    /// to their unarmed weapon first (so it gets captured), then to the actual current one — see
    /// CharacterSave.ToCombatant, which is the only place that ordering matters.
    public required Weapon Weapon
    {
        get => _weapon;
        set
        {
            _weapon = value;
            _unarmedWeapon ??= value.IsUnarmed ? value : null;
        }
    }

    /// The fallback SheathCurrentWeapon() reverts to — null only if this combatant has never once
    /// been given an IsUnarmed weapon (shouldn't happen for a properly loaded character).
    public Weapon? UnarmedWeapon => _unarmedWeapon;

    public List<Armor> EquippedArmor { get; init; } = new();

    /// Armor pieces owned but not currently worn — physically in hand (unlike a sheathed weapon,
    /// armor has nowhere free to sit), so they cost hand space same as if you'd just picked them up.
    public List<Armor> HeldArmor { get; init; } = new();

    /// Weapons owned but not currently wielded — always available at no bulk cost ("their sheath").
    public List<Weapon> SheathedWeapons { get; init; } = new();

    /// Bought at a shop; a fresh character has none.
    public Backpack? Backpack { get; set; }
    public List<Item> BackpackItems { get; init; } = new();

    /// Loose items held in hand (not stowed in a backpack) — a pelt you just looted and haven't
    /// stored yet, say. No backpack needed to hold something this way.
    public List<Item> HeldItems { get; init; } = new();

    /// What this NPC drops on death (e.g. a pelt) — stays on its corpse, not the room floor, until
    /// the corpse fades. Irrelevant for the player.
    public List<Item> Loot { get; init; } = new();

    /// Names of whoever fought this NPC (not just the killing blow) — only they may loot its corpse
    /// while it's still fresh. Everyone can once the corpse fades and the loot hits the floor.
    public HashSet<string> LootRights { get; } = new(StringComparer.OrdinalIgnoreCase);

    public void GrantLootRights(string name) => LootRights.Add(name);

    // Base stats and skills, normally 0-100 (buffs can push the effective value up to CombatConstants.MaxEffectiveStat).
    // Mutable (not init-only) because training raises them permanently during play — go through
    // TryTrain for that; a plain assignment (e.g. loading a save) skips the gold/cap checks on purpose.
    public required double Strength { get; set; }
    public required double Constitution { get; set; }
    public required double Agility { get; set; }
    public required double Coordination { get; set; }
    public required double Intelligence { get; set; }
    public required double Aim { get; set; }
    public required double Attack { get; set; }
    public required double Defense { get; set; }
    public required double Dodge { get; set; }

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

    /// Player-defined command shortcuts (`alias <name> <command>`), console-side convenience — never
    /// looked at by anything in Combat. Irrelevant for NPCs. Case-insensitive so "EE" and "ee" are the
    /// same alias.
    public Dictionary<string, string> Aliases { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// Irrelevant for NPCs — only the player accumulates this.
    public int Experience { get; private set; }

    /// Doesn't level up by itself anymore — that only happens at a trainer, see TryLevelUp.
    public void AddExperience(int amount) => Experience += Math.Max(0, amount);

    /// Sets the experience total read back from a save file. Deliberately doesn't re-check leveling —
    /// a loaded character's Level is restored separately (RestoreLevel) as the source of truth, since
    /// re-deriving it here could disagree if the stat-average requirement changes in a future update.
    public void RestoreExperience(int amount) => Experience = Math.Max(0, amount);

    /// Sets the level read back from a save file, bypassing the XP/stat-average gate (they already
    /// earned it) and without firing OnLevelUp (nobody's listening yet at load time).
    public void RestoreLevel(int level) => Level = Math.Max(1, level);

    /// Round levels (5, 10, 15, 20) need something more than XP/stats to get past — a quest system
    /// that doesn't exist yet, so for now this is just a hard wall.
    private static bool IsMilestoneLevel(int level) => level % 5 == 0;

    /// Advances exactly one level — call it again for another, once eligible again. Only ever called
    /// from a trainer command (Combat itself doesn't enforce "must be at a trainer"; that's on the
    /// caller, since "presence in a room" isn't a concept this library knows about).
    public bool TryLevelUp(out string? error)
    {
        int nextLevel = Level + 1;

        if (IsMilestoneLevel(Level))
        {
            error = $"You need to complete a special task before advancing past level {Level} (not implemented yet).";
            return false;
        }

        if (Experience < Leveling.ExperienceRequired(nextLevel))
        {
            error = "You don't have enough experience yet.";
            return false;
        }

        double statAverage = (Strength + Constitution + Agility + Coordination + Intelligence) / 5.0;
        if (statAverage < Leveling.RequiredStatAverage(nextLevel))
        {
            error = "Your stats aren't high enough yet.";
            return false;
        }

        Level = nextLevel;
        OnLevelUp?.Invoke(Level);
        error = null;
        return true;
    }

    /// The highest a stat/skill can be trained to right now (Training.MaxTrainableValue at this level).
    public int TrainingCap => Training.MaxTrainableValue(Level);

    /// Gold cost to train `stat` up by `points`, from its current (rounded) value.
    public int TrainingCostFor(StatType stat, int points) =>
        Training.CostForRange((int)GetBaseStat(stat), (int)GetBaseStat(stat) + points);

    /// Fails (refuses, nothing changes) if this would exceed TrainingCap, or gold is short. `error`
    /// explains which.
    public bool TryTrain(StatType stat, int points, out string? error)
    {
        int current = (int)GetBaseStat(stat);
        int target = current + points;
        int cap = TrainingCap;

        if (target > cap)
        {
            error = $"You can't train that past {cap} at your level.";
            return false;
        }

        int cost = Training.CostForRange(current, target);
        if (!SpendGold(cost))
        {
            error = $"That would cost {cost} gold — you don't have enough.";
            return false;
        }

        SetBaseStat(stat, target);
        error = null;
        return true;
    }

    /// Incremental HP recovery (unlike RestoreHp, which sets an absolute value for save loading).
    /// A no-op while bleeding or dead — those need Stabilize/reviving, not a top-up.
    public void Heal(int amount)
    {
        if (State != LifeState.Alive)
            return;

        CurrentHp = Math.Min(MaxHp, CurrentHp + Math.Max(0, amount));
    }

    /// Incremental MP recovery, mirroring Heal().
    public void RecoverMp(int amount) => CurrentMp = Math.Min(MaxMp, CurrentMp + Math.Max(0, amount));

    /// Hand-bulk currently spoken for: the wielded weapon, a worn shield (it still needs a hand even
    /// though it's "equipped" — unlike the other five slots, which are strapped on and cost nothing),
    /// and anything held-but-unworn. Each held thing takes at least a full hand-slot — a pelt at 0.25
    /// bulk still occupies one whole hand, no stacking several loose items into it.
    public double UsedHandBulk =>
        (Weapon.IsUnarmed ? 0 : Math.Max(Weapon.Bulk, 1))
        + EquippedArmor.Where(a => a.Slots.Contains(BodySlot.Shield)).Sum(a => Math.Max(a.Bulk, 1))
        + HeldArmor.Sum(a => Math.Max(a.Bulk, 1))
        + HeldItems.Sum(i => Math.Max(i.Bulk, 1));

    public double FreeHandBulk => Math.Max(0, CombatConstants.HandCapacity - UsedHandBulk);

    /// Holds a loose item in a free hand, sheathing the wielded weapon first only if there wasn't
    /// already room without doing so. `sheathed` is what got sheathed, for narration (null if
    /// nothing needed to move). Returns false (refuses, nothing changes) if it still doesn't fit.
    public bool TryHoldItem(Item item, out Weapon? sheathed)
    {
        double needed = Math.Max(item.Bulk, 1);
        sheathed = FreeHandBulk < needed ? SheathCurrentWeapon() : null;

        if (FreeHandBulk < needed)
            return false;

        HeldItems.Add(item);
        return true;
    }

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

    /// The raw base value (no race %, no buffs) — what training actually reads and changes.
    public double BaseStat(StatType stat) => GetBaseStat(stat);

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

    private void SetBaseStat(StatType stat, double value)
    {
        switch (stat)
        {
            case StatType.Strength: Strength = value; break;
            case StatType.Constitution: Constitution = value; break;
            case StatType.Agility: Agility = value; break;
            case StatType.Coordination: Coordination = value; break;
            case StatType.Intelligence: Intelligence = value; break;
            case StatType.Aim: Aim = value; break;
            case StatType.Attack: Attack = value; break;
            case StatType.Defense: Defense = value; break;
            case StatType.Dodge: Dodge = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(stat));
        }
    }

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
