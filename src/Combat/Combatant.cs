namespace Combat;

public class Combatant
{
    public required string Name { get; init; }
    public required int MaxHp { get; init; }
    public int CurrentHp { get; private set; }

    /// Base chance to land a hit, before subtracting the defender's evasion.
    public required double Accuracy { get; init; }
    public required double Evasion { get; init; }
    public required double CritChance { get; init; }
    public required double CritMultiplier { get; init; }
    public required int Armor { get; init; }
    public required int AttackPower { get; init; }
    public required Weapon Weapon { get; init; }

    /// Drives emote perspective ("you" vs. third person) — independent of combat initiative,
    /// since an aggressive NPC can have initiative while the player is still "you" in the text.
    public bool IsPlayer { get; init; }

    public bool IsDead => CurrentHp <= 0;

    private readonly Queue<Skill> _queuedSkills = new();
    private readonly Dictionary<Skill, int> _cooldowns = new();

    /// Call once after object initialization, since CurrentHp can't be defaulted from MaxHp in an initializer.
    public void ResetHp() => CurrentHp = MaxHp;

    public void ApplyDamage(int amount)
    {
        CurrentHp = Math.Max(0, CurrentHp - amount);
    }

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

    public void TickCooldowns()
    {
        foreach (var skill in _cooldowns.Keys.ToList())
        {
            if (_cooldowns[skill] > 0)
                _cooldowns[skill]--;
        }
    }
}
