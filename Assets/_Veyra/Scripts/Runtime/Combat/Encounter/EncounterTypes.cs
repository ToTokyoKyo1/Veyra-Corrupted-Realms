using System;

namespace Veyra.Combat.Encounter
{
    public enum EncounterAction
    {
        Attack,
        Guard,
        Technique,
        Analyze
    }

    public enum EnemyIntent
    {
        Attack,
        Guard,
        Charge,
        ChargedStrike
    }

    public enum EnemyMood
    {
        Felice,
        Triste,
        Arrabbiato,
        Guardingo,
        Spaventato,
        Rassegnato
    }

    public enum NarrativeOutcome
    {
        None,
        Saved,
        Killed,
        HeroDefeated
    }

    public enum LearnedPattern
    {
        None,
        RepeatedAttack,
        RepeatedGuard,
        TechniqueRhythm,
        StrategyChanged,
        FrequentAnalyze
    }

    public readonly struct EncounterActionResult
    {
        private EncounterActionResult(
            bool accepted,
            EncounterAction action,
            int damageDealt,
            bool consumesTurn,
            bool blockedByGuard,
            bool enemyGuardReducedDamage,
            bool enemyDefeated,
            string rejectionReason)
        {
            Accepted = accepted;
            Action = action;
            DamageDealt = damageDealt;
            ConsumesTurn = consumesTurn;
            BlockedByGuard = blockedByGuard;
            EnemyGuardReducedDamage = enemyGuardReducedDamage;
            EnemyDefeated = enemyDefeated;
            RejectionReason = rejectionReason ?? string.Empty;
        }

        public bool Accepted { get; }

        public EncounterAction Action { get; }

        public int DamageDealt { get; }

        public bool ConsumesTurn { get; }

        public bool BlockedByGuard { get; }

        public bool EnemyGuardReducedDamage { get; }

        public bool EnemyDefeated { get; }

        public string RejectionReason { get; }

        internal static EncounterActionResult Completed(
            EncounterAction action,
            int damageDealt,
            bool consumesTurn,
            bool enemyGuardReducedDamage,
            bool enemyDefeated)
        {
            return new EncounterActionResult(
                true,
                action,
                damageDealt,
                consumesTurn,
                false,
                enemyGuardReducedDamage,
                enemyDefeated,
                string.Empty);
        }

        internal static EncounterActionResult Rejected(
            EncounterAction action,
            bool enemyDefeated,
            string reason)
        {
            return new EncounterActionResult(
                false,
                action,
                0,
                false,
                false,
                false,
                enemyDefeated,
                reason);
        }
    }

    public readonly struct EnemyIntentResult
    {
        private EnemyIntentResult(
            bool accepted,
            EnemyIntent intent,
            int damageDealt,
            bool blockedByGuard,
            bool preparedGuard,
            bool beganCharge,
            NarrativeOutcome outcome,
            string rejectionReason)
        {
            Accepted = accepted;
            Intent = intent;
            DamageDealt = damageDealt;
            BlockedByGuard = blockedByGuard;
            PreparedGuard = preparedGuard;
            BeganCharge = beganCharge;
            Outcome = outcome;
            RejectionReason = rejectionReason ?? string.Empty;
        }

        public bool Accepted { get; }

        public EnemyIntent Intent { get; }

        public int DamageDealt { get; }

        public bool BlockedByGuard { get; }

        public bool PreparedGuard { get; }

        public bool BeganCharge { get; }

        public NarrativeOutcome Outcome { get; }

        public string RejectionReason { get; }

        internal static EnemyIntentResult Completed(
            EnemyIntent intent,
            int damageDealt,
            bool blockedByGuard,
            bool preparedGuard,
            bool beganCharge,
            NarrativeOutcome outcome)
        {
            return new EnemyIntentResult(
                true,
                intent,
                damageDealt,
                blockedByGuard,
                preparedGuard,
                beganCharge,
                outcome,
                string.Empty);
        }

        internal static EnemyIntentResult Rejected(
            EnemyIntent intent,
            NarrativeOutcome outcome,
            string reason)
        {
            return new EnemyIntentResult(
                false,
                intent,
                0,
                false,
                false,
                false,
                outcome,
                reason);
        }
    }

    public readonly struct AdaptiveDecision
    {
        public AdaptiveDecision(
            EnemyIntent intent,
            LearnedPattern pattern,
            double confidence,
            double counterProbability,
            bool usedAdaptiveCounter)
        {
            Intent = intent;
            Pattern = pattern;
            Confidence = Clamp01(confidence);
            CounterProbability = Math.Min(AdaptiveEnemyBrain.MaximumCounterProbability, Clamp01(counterProbability));
            UsedAdaptiveCounter = usedAdaptiveCounter;
        }

        public EnemyIntent Intent { get; }

        public LearnedPattern Pattern { get; }

        public double Confidence { get; }

        public double CounterProbability { get; }

        public bool UsedAdaptiveCounter { get; }

        private static double Clamp01(double value)
        {
            if (value < 0d)
            {
                return 0d;
            }

            return value > 1d ? 1d : value;
        }
    }

    public readonly struct EnemyDecisionContext
    {
        public EnemyDecisionContext(
            int heroHp,
            int heroMaxHp,
            int enemyHp,
            int enemyMaxHp,
            int corruptionPercent,
            EnemyMood mood,
            bool heroGuardPrepared,
            bool enemyGuardPrepared,
            bool chargedStrikePrepared)
        {
            if (heroMaxHp <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(heroMaxHp));
            }

            if (enemyMaxHp <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(enemyMaxHp));
            }

            HeroHp = Clamp(heroHp, 0, heroMaxHp);
            HeroMaxHp = heroMaxHp;
            EnemyHp = Clamp(enemyHp, 0, enemyMaxHp);
            EnemyMaxHp = enemyMaxHp;
            CorruptionPercent = Corruption.Clamp(corruptionPercent);
            Mood = mood;
            HeroGuardPrepared = heroGuardPrepared;
            EnemyGuardPrepared = enemyGuardPrepared;
            ChargedStrikePrepared = chargedStrikePrepared;
        }

        public int HeroHp { get; }

        public int HeroMaxHp { get; }

        public int EnemyHp { get; }

        public int EnemyMaxHp { get; }

        public int CorruptionPercent { get; }

        public EnemyMood Mood { get; }

        public bool HeroGuardPrepared { get; }

        public bool EnemyGuardPrepared { get; }

        public bool ChargedStrikePrepared { get; }

        public double HeroHealthRatio => HeroHp / (double)HeroMaxHp;

        public double EnemyHealthRatio => EnemyHp / (double)EnemyMaxHp;

        public static EnemyDecisionContext From(EncounterBattleState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            return new EnemyDecisionContext(
                state.HeroHp,
                state.HeroMaxHp,
                state.EnemyHp,
                state.EnemyMaxHp,
                state.CorruptionPercent,
                state.Mood,
                state.IsHeroGuardPrepared,
                state.IsEnemyGuardPrepared,
                state.IsChargedStrikePrepared);
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum)
            {
                return minimum;
            }

            return value > maximum ? maximum : value;
        }
    }

    public static class Corruption
    {
        public static int Clamp(int value)
        {
            if (value < 0)
            {
                return 0;
            }

            return value > 100 ? 100 : value;
        }
    }
}
