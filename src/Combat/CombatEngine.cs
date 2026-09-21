namespace Combat;

public class CombatEngine
{
    public Combatant First { get; }   // whoever used "kill" — keeps the initiative every round.
    public Combatant Second { get; }
    public int Round { get; private set; }
    public bool IsFinished { get; private set; }
    public Combatant? Winner { get; private set; }

    public event Action<string>? OnEmote;

    private readonly IRandomSource _rng;
    private readonly EmoteTable _standardEmotes;

    public CombatEngine(Combatant first, Combatant second, IRandomSource rng, EmoteTable? standardEmotes = null)
    {
        First = first;
        Second = second;
        _rng = rng;
        _standardEmotes = standardEmotes ?? EmoteTable.CreateStandard();
    }

    public void PlayRound()
    {
        if (IsFinished)
            return;

        Round++;
        Act(First, Second);
        if (!Second.IsDead)
            Act(Second, First);

        EndOfRound();

        if (First.IsDead || Second.IsDead)
            Finish(First.IsDead ? Second : First);
    }

    public void EndByFlee()
    {
        if (!IsFinished)
            IsFinished = true;
    }

    private void Act(Combatant attacker, Combatant defender)
    {
        if (attacker.IsDead)
            return;

        var skill = attacker.TakeQueuedSkillIfReady();
        var result = skill != null
            ? skill.Execute(attacker, defender, _rng)
            : ResolveAttack(attacker, defender);

        defender.ApplyDamage(result.Damage);
        OnEmote?.Invoke(result.EmoteText);
    }

    private void EndOfRound()
    {
        First.TickCooldowns();
        Second.TickCooldowns();
    }

    private void Finish(Combatant winner)
    {
        IsFinished = true;
        Winner = winner;
    }

    public HitResult ResolveAttack(Combatant attacker, Combatant defender)
    {
        var emotes = attacker.Weapon.CustomEmotes;

        if (_rng.NextDouble() > HitChance(attacker, defender))
        {
            var missVariant = emotes?.TryGetVariants(HitTier.Miss) != null
                ? emotes.Pick(HitTier.Miss, _rng)
                : _standardEmotes.Pick(HitTier.Miss, _rng);
            return HitResult.Miss(missVariant.Format(attacker.Name, defender.Name, attacker.IsPlayer));
        }

        bool crit = _rng.NextDouble() < CritChance(attacker, defender);
        var (min, max) = DamageRange(attacker);
        int raw = _rng.Next(min, max + 1);
        int dmg = Math.Max(0, (int)((crit ? raw * attacker.CritMultiplier : raw) - defender.Armor));

        HitTier tier = crit
            ? HitTier.Critical
            : (HitTier)(1 + Math.Min(5, (int)((raw - min) / (float)(max - min + 1) * 6)));

        var variant = emotes?.TryGetVariants(tier) != null
            ? emotes.Pick(tier, _rng)
            : _standardEmotes.Pick(tier, _rng);

        return new HitResult(dmg, tier, variant.Format(attacker.Name, defender.Name, attacker.IsPlayer));
    }

    private static double HitChance(Combatant attacker, Combatant defender) =>
        Math.Clamp(attacker.Accuracy - defender.Evasion, 0.05, 0.95);

    private static double CritChance(Combatant attacker, Combatant defender) =>
        Math.Clamp(attacker.CritChance, 0.0, 1.0);

    private static (int min, int max) DamageRange(Combatant attacker)
    {
        int baseMin = attacker.AttackPower + attacker.Weapon.MinDamage;
        int baseMax = attacker.AttackPower + attacker.Weapon.MaxDamage;
        int min = (int)(baseMin * 0.8);
        int max = (int)(baseMax * 1.2);
        return (min, Math.Max(min, max));
    }
}
