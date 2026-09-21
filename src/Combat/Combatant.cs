namespace Combat;

public class Combatant
{
    public required string Name { get; init; }
    public required int MaxHp { get; init; }
    public int CurrentHp { get; private set; }

    /// Not spent by anything yet — reserved for profession skills/spells that will cost MP later.
    public int MaxMp { get; init; }
    public int CurrentMp { get; private set; }

    /// Drives emote perspective ("you" vs. third person) — independent of combat initiative,
    /// since an aggressive NPC can have initiative while the player is still "you" in the text.
    public bool IsPlayer { get; init; }

    public Race Race { get; init; } = Race.None;

    public required Weapon Weapon { get; init; }
    public List<Armor> EquippedArmor { get; init; } = new();

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

    public bool IsDead => CurrentHp <= 0;

    /// Sum of Absorb across all equipped armor, capped at CombatConstants.MaxArmorPoolTotal.
    public double TotalAbsorb => Math.Min(CombatConstants.MaxArmorPoolTotal, EquippedArmor.Sum(a => a.Absorb));

    /// Sum of Deflect across all equipped armor, capped at CombatConstants.MaxArmorPoolTotal.
    public double TotalDeflect => Math.Min(CombatConstants.MaxArmorPoolTotal, EquippedArmor.Sum(a => a.Deflect));

    private readonly Queue<Skill> _queuedSkills = new();
    private readonly Dictionary<Skill, int> _cooldowns = new();
    private readonly List<StatModifier> _modifiers = new();

    /// Call once after object initialization, since CurrentHp can't be defaulted from MaxHp in an initializer.
    public void ResetHp() => CurrentHp = MaxHp;

    public void ResetMp() => CurrentMp = MaxMp;

    public void ApplyDamage(int amount)
    {
        CurrentHp = Math.Max(0, CurrentHp - amount);
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
