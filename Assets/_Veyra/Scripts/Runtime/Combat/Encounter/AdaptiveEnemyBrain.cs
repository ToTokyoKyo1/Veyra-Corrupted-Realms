using System;

namespace Veyra.Combat.Encounter
{
    public sealed class AdaptiveEnemyBrain
    {
        public const double MaximumCounterProbability = 0.65d;

        private readonly int seed;
        private Random random;
        private EnemyIntent? lockedIntent;

        public AdaptiveEnemyBrain(int intelligenceLevel, int seed)
        {
            IntelligenceLevel = ClampIntelligence(intelligenceLevel);
            this.seed = seed;
            random = new Random(seed);
            LastDecision = new AdaptiveDecision(EnemyIntent.Attack, LearnedPattern.None, 0d, 0d, false);
        }

        public int IntelligenceLevel { get; }

        public bool HasLockedIntent => lockedIntent.HasValue;

        public EnemyIntent? LockedIntent => lockedIntent;

        public AdaptiveDecision LastDecision { get; private set; }

        public EnemyIntent PlanAndLockIntent(EnemyMemory memory, EnemyDecisionContext context)
        {
            if (memory == null)
            {
                throw new ArgumentNullException(nameof(memory));
            }

            if (lockedIntent.HasValue)
            {
                return lockedIntent.Value;
            }

            if (context.ChargedStrikePrepared)
            {
                return Lock(new AdaptiveDecision(
                    EnemyIntent.ChargedStrike,
                    LearnedPattern.None,
                    1d,
                    0d,
                    false));
            }

            LearnedPattern pattern = DetectPattern(memory);
            double confidence = IntelligenceLevel == 0 ? 0d : memory.PatternConfidence;

            if (pattern == LearnedPattern.StrategyChanged)
            {
                confidence *= 0.35d;
            }

            EnemyIntent adaptiveCounter;
            bool hasCounter = TryGetCounter(pattern, out adaptiveCounter);
            double counterProbability = hasCounter
                ? CalculateCounterProbability(confidence)
                : 0d;

            bool useCounter = hasCounter && random.NextDouble() < counterProbability;
            EnemyIntent intent = useCounter
                ? adaptiveCounter
                : ChooseBaselineIntent(context);

            if (intent == EnemyIntent.Guard && context.EnemyGuardPrepared)
            {
                intent = context.HeroGuardPrepared ? EnemyIntent.Charge : EnemyIntent.Attack;
            }

            return Lock(new AdaptiveDecision(
                intent,
                pattern,
                confidence,
                counterProbability,
                useCounter));
        }

        public void CompleteLockedIntent()
        {
            lockedIntent = null;
        }

        public void Reset()
        {
            random = new Random(seed);
            lockedIntent = null;
            LastDecision = new AdaptiveDecision(EnemyIntent.Attack, LearnedPattern.None, 0d, 0d, false);
        }

        private EnemyIntent Lock(AdaptiveDecision decision)
        {
            LastDecision = decision;
            lockedIntent = decision.Intent;
            return decision.Intent;
        }

        private LearnedPattern DetectPattern(EnemyMemory memory)
        {
            if (IntelligenceLevel == 0 || memory.CompletedActions.Count < 2)
            {
                return LearnedPattern.None;
            }

            if (memory.HasRecentStrategyChange)
            {
                return LearnedPattern.StrategyChanged;
            }

            if (memory.TendsToUseTechniqueWhenReady)
            {
                return LearnedPattern.TechniqueRhythm;
            }

            if (memory.LastCompletedAction == EncounterAction.Attack &&
                (memory.ConsecutiveCount >= 3 || memory.GetFrequency(EncounterAction.Attack) >= 3))
            {
                return LearnedPattern.RepeatedAttack;
            }

            if (memory.LastCompletedAction == EncounterAction.Guard &&
                (memory.ConsecutiveCount >= 3 || memory.GetFrequency(EncounterAction.Guard) >= 3))
            {
                return LearnedPattern.RepeatedGuard;
            }

            return LearnedPattern.None;
        }

        private static bool TryGetCounter(LearnedPattern pattern, out EnemyIntent counter)
        {
            switch (pattern)
            {
                case LearnedPattern.RepeatedAttack:
                case LearnedPattern.TechniqueRhythm:
                    counter = EnemyIntent.Guard;
                    return true;
                case LearnedPattern.RepeatedGuard:
                    counter = EnemyIntent.Charge;
                    return true;
                default:
                    counter = EnemyIntent.Attack;
                    return false;
            }
        }

        private double CalculateCounterProbability(double confidence)
        {
            double baseProbability;
            double confidenceInfluence;

            switch (IntelligenceLevel)
            {
                case 1:
                    baseProbability = 0.12d;
                    confidenceInfluence = 0.30d;
                    break;
                case 2:
                    baseProbability = 0.25d;
                    confidenceInfluence = 0.40d;
                    break;
                case 3:
                    baseProbability = 0.35d;
                    confidenceInfluence = 0.45d;
                    break;
                default:
                    return 0d;
            }

            return Math.Min(MaximumCounterProbability, baseProbability + confidence * confidenceInfluence);
        }

        private EnemyIntent ChooseBaselineIntent(EnemyDecisionContext context)
        {
            double corruption = context.CorruptionPercent / 100d;
            double attackWeight = 0.42d + corruption * 0.30d;
            double guardWeight = 0.30d - corruption * 0.12d;
            double chargeWeight = 0.28d - corruption * 0.08d;

            if (context.EnemyHealthRatio <= 0.35d)
            {
                guardWeight += 0.18d;
            }

            if (context.HeroGuardPrepared)
            {
                attackWeight -= 0.22d;
                chargeWeight += 0.22d;
            }

            switch (context.Mood)
            {
                case EnemyMood.Arrabbiato:
                    attackWeight += 0.15d;
                    guardWeight -= 0.05d;
                    break;
                case EnemyMood.Triste:
                case EnemyMood.Spaventato:
                    guardWeight += 0.12d;
                    attackWeight -= 0.06d;
                    break;
                case EnemyMood.Guardingo:
                    guardWeight += 0.10d;
                    chargeWeight += 0.06d;
                    attackWeight -= 0.08d;
                    break;
                case EnemyMood.Rassegnato:
                    attackWeight -= 0.08d;
                    guardWeight += 0.04d;
                    break;
            }

            if (context.EnemyGuardPrepared)
            {
                guardWeight = 0d;
            }

            attackWeight = Math.Max(0.05d, attackWeight);
            guardWeight = Math.Max(0d, guardWeight);
            chargeWeight = Math.Max(0.05d, chargeWeight);

            double totalWeight = attackWeight + guardWeight + chargeWeight;
            double roll = random.NextDouble() * totalWeight;
            if (roll < attackWeight)
            {
                return EnemyIntent.Attack;
            }

            roll -= attackWeight;
            return roll < guardWeight ? EnemyIntent.Guard : EnemyIntent.Charge;
        }

        private static int ClampIntelligence(int value)
        {
            if (value < 0)
            {
                return 0;
            }

            return value > 3 ? 3 : value;
        }
    }
}
