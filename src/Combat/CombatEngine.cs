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
        First.TickEndOfRound();
        Second.TickEndOfRound();
    }

    private void Finish(Combatant winner)
    {
        IsFinished = true;
        Winner = winner;
    }

    public HitResult ResolveAttack(Combatant attacker, Combatant defender) =>
        AttackResolver.Resolve(attacker, defender, _rng, standardEmotes: _standardEmotes);
}
