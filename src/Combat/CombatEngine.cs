namespace Combat;

public class CombatEngine
{
    public Combatant First { get; }   // whoever used "kill" — keeps the initiative every round.
    public Combatant Second { get; }
    public int Round { get; private set; }
    public bool IsFinished { get; private set; }
    public Combatant? Winner { get; private set; }

    /// Carries the full result (including Tier), not just the formatted text, so a presentation
    /// layer can react to hit intensity (color, screen shake, sound...) without re-parsing the emote.
    public event Action<HitResult>? OnAttackResult;

    private readonly IRandomSource _rng;
    private readonly EmoteTable _standardEmotes;
    private bool _fleeRequested;

    public CombatEngine(Combatant first, Combatant second, IRandomSource rng, EmoteTable? standardEmotes = null)
    {
        First = first;
        Second = second;
        _rng = rng;
        _standardEmotes = standardEmotes ?? EmoteTable.CreateStandard();
    }

    /// Runs both turns of a round back to back, with no pacing between them.
    /// Use PlayFirstTurn/PlaySecondTurn instead when the caller needs to space turns out in real time
    /// (each turn is 1 second, so a full round is 2 seconds — that pacing belongs to the caller, e.g.
    /// Unity's timer/coroutine or the console prototype's Task.Delay, not to this engine).
    public void PlayRound()
    {
        PlayFirstTurn();
        PlaySecondTurn();
    }

    public void PlayFirstTurn()
    {
        if (IsFinished)
            return;

        // A flee request never cuts short a round already in progress — it only stops the next one.
        if (_fleeRequested)
        {
            IsFinished = true;
            return;
        }

        Round++;
        Act(First, Second);
    }

    public void PlaySecondTurn()
    {
        if (IsFinished)
            return;

        if (!Second.IsDead)
            Act(Second, First);

        EndOfRound();

        if (First.IsDead || Second.IsDead)
            Finish(First.IsDead ? Second : First);
    }

    /// No penalty: combat stops once the round already in progress finishes.
    public void RequestFlee() => _fleeRequested = true;

    private void Act(Combatant attacker, Combatant defender)
    {
        if (attacker.IsDead)
            return;

        var skill = attacker.TakeQueuedSkillIfReady();
        var result = skill != null
            ? skill.Execute(attacker, defender, _rng)
            : ResolveAttack(attacker, defender);

        defender.ApplyDamage(result.Damage);
        OnAttackResult?.Invoke(result);
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
