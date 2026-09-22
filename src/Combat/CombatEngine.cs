namespace Combat;

public enum CombatEndReason { None, Slain, BledOut, Fled, Stabilized }

public class CombatEngine
{
    public Combatant First { get; }   // whoever used "kill" — keeps the initiative every round.
    public Combatant Second { get; }
    public int Round { get; private set; }
    public bool IsFinished { get; private set; }
    public Combatant? Winner { get; private set; }
    public CombatEndReason EndReason { get; private set; }

    /// True while someone is on the ground bleeding: nobody attacks until they are finished off, bandaged
    /// or bleed out, but rounds keep passing (the bleeding clock runs).
    public bool IsPaused => First.IsBleeding || Second.IsBleeding;

    /// Carries the attacker/defender along with the full result (including Tier), not just the
    /// formatted text — a presentation layer can react to hit intensity (color, screen shake, sound...)
    /// without re-parsing the emote, and knowing who attacked is what lets a caller award XP only for
    /// the player's own hits.
    public event Action<Combatant, Combatant, HitResult>? OnAttackResult;

    /// Plain narration lines (falling, bleeding, bandaging...), already phrased from the player's perspective.
    public event Action<string>? OnNarration;

    private readonly IRandomSource _rng;
    private readonly EmoteTable _standardEmotes;
    private readonly HashSet<Combatant> _justFell = new();
    private readonly object _gate = new(); // commands (kill/bandage) come from another thread than the round loop
    private bool _fleeRequested;
    private bool _bledOut;

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
        lock (_gate)
        {
            if (IsFinished)
                return;

            // A flee request never cuts short a round already in progress — it only stops the next one.
            if (_fleeRequested)
            {
                End(CombatEndReason.Fled, null);
                return;
            }

            Round++;
            if (IsPaused)
                return;

            Act(First, Second);
        }
    }

    public void PlaySecondTurn()
    {
        lock (_gate)
        {
            if (IsFinished)
                return;

            if (!IsPaused && !Second.IsDown)
                Act(Second, First);

            EndOfRound();

            if (First.IsDead || Second.IsDead)
                End(_bledOut ? CombatEndReason.BledOut : CombatEndReason.Slain, First.IsDead ? Second : First);
        }
    }

    /// No penalty: combat stops once the round already in progress finishes.
    public void RequestFlee() => _fleeRequested = true;

    /// `executor` kills the bleeding opponent. Returns false if there is nobody bleeding to finish off.
    public bool FinishOff(Combatant executor)
    {
        lock (_gate)
        {
            var target = BleedingOpponentOf(executor);
            if (target == null)
                return false;

            target.Kill();
            Narrate(executor.IsPlayer
                ? $"You finish off {target.Name}."
                : target.IsPlayer
                    ? $"{Cap(executor.Name)} finishes you off!"
                    : $"{Cap(executor.Name)} finishes off {target.Name}.");
            End(CombatEndReason.Slain, executor);
            return true;
        }
    }

    /// `helper` bandages the bleeding opponent: the bleeding stops, they get back some HP, and the combat stops.
    public bool Bandage(Combatant helper)
    {
        lock (_gate)
        {
            var target = BleedingOpponentOf(helper);
            if (target == null)
                return false;

            target.Stabilize();
            Narrate(helper.IsPlayer
                ? $"You bandage {target.Name}'s wounds, and the bleeding stops."
                : $"{Cap(helper.Name)} bandages {target.Name}'s wounds, and the bleeding stops.");
            End(CombatEndReason.Stabilized, null);
            return true;
        }
    }

    private Combatant? BleedingOpponentOf(Combatant actor)
    {
        if (IsFinished || actor.IsDown)
            return null;

        var opponent = ReferenceEquals(actor, First) ? Second : ReferenceEquals(actor, Second) ? First : null;
        return opponent is { IsBleeding: true } ? opponent : null;
    }

    private void Act(Combatant attacker, Combatant defender)
    {
        if (attacker.IsDown)
            return;

        var skill = attacker.TakeQueuedSkillIfReady();
        var result = skill != null
            ? skill.Execute(attacker, defender, _rng)
            : ResolveAttack(attacker, defender);

        defender.ApplyDamage(result.Damage);
        OnAttackResult?.Invoke(attacker, defender, result);

        if (defender.IsBleeding)
        {
            _justFell.Add(defender);
            Narrate(defender,
                "You collapse to the ground, bleeding and in need of bandages!",
                $"{Cap(defender.Name)} collapses to the ground, bleeding and in need of bandages!");
        }
    }

    private void EndOfRound()
    {
        foreach (var combatant in new[] { First, Second })
        {
            combatant.TickEndOfRound();

            if (!combatant.IsBleeding)
                continue;

            // The round they fell already announced it, so only later rounds repeat the reminder.
            bool justFell = _justFell.Remove(combatant);

            if (combatant.TickBleed())
            {
                _bledOut = true;
                Narrate(combatant, "You bleed out and die.", $"{Cap(combatant.Name)} bleeds out and dies.");
            }
            else if (!justFell)
            {
                Narrate(combatant,
                    "You lie on the ground, bleeding and in need of bandages.",
                    $"{Cap(combatant.Name)} lies on the ground, bleeding and in need of bandages.");
            }
        }
    }

    private void End(CombatEndReason reason, Combatant? winner)
    {
        IsFinished = true;
        EndReason = reason;
        Winner = winner;
    }

    private void Narrate(string text) => OnNarration?.Invoke(text);

    private void Narrate(Combatant subject, string ifPlayer, string ifNpc) =>
        Narrate(subject.IsPlayer ? ifPlayer : ifNpc);

    private static string Cap(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    public HitResult ResolveAttack(Combatant attacker, Combatant defender) =>
        AttackResolver.Resolve(attacker, defender, _rng, standardEmotes: _standardEmotes);
}
