using System;

namespace Veyra.Combat.Encounter
{
    public sealed class AdaptiveEnemyBrain
    {
        public const double MaximumCounterProbability =
            AdaptiveEnemyTuning.AbsoluteMaximumCounterProbability;

        private readonly int seed;
        private readonly AdaptiveEnemyTuning tuning;
        private Random random;
        private EnemyIntent? lockedIntent;

        public AdaptiveEnemyBrain(int intelligenceLevel, int seed)
            : this(intelligenceLevel, seed, null)
        {
        }

        public AdaptiveEnemyBrain(
            int intelligenceLevel,
            int seed,
            AdaptiveEnemyTuning tuning)
        {
            IntelligenceLevel = ClampIntelligence(intelligenceLevel);
            this.seed = seed;
            this.tuning = AdaptiveEnemyTuning.Normalize(tuning);
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
            double confidence = IntelligenceLevel == 0
                ? 0d
                : pattern == LearnedPattern.FrequentAnalyze
                    ? memory.GetAnalyzePatternConfidence(tuning)
                    : memory.GetPatternConfidence(tuning);

            if (pattern == LearnedPattern.StrategyChanged)
            {
                confidence *= tuning.StrategyChangeConfidenceMultiplier;
            }

            EnemyIntent adaptiveCounter;
            bool hasCounter = TryGetCounter(pattern, out adaptiveCounter);
            double counterProbability = hasCounter
                ? CalculateCounterProbability(confidence)
                : 0d;

            if (pattern == LearnedPattern.FrequentAnalyze)
            {
                counterProbability *= tuning.AnalyzeResponseProbabilityMultiplier;
            }

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
            if (IntelligenceLevel == 0)
            {
                return LearnedPattern.None;
            }

            if (memory.CompletedActions.Count >= tuning.MinimumObservedActions)
            {
                if (memory.HasRecentStrategyChangeFor(tuning))
                {
                    return LearnedPattern.StrategyChanged;
                }

                if (memory.TendsToUseTechniqueWhenReadyFor(tuning))
                {
                    return LearnedPattern.TechniqueRhythm;
                }

                if (memory.HasRepeatedActionPattern(EncounterAction.Attack, tuning))
                {
                    return LearnedPattern.RepeatedAttack;
                }

                if (memory.HasRepeatedActionPattern(EncounterAction.Guard, tuning))
                {
                    return LearnedPattern.RepeatedGuard;
                }
            }

            if (memory.HasFrequentAnalyzePatternFor(tuning))
            {
                return LearnedPattern.FrequentAnalyze;
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
                case LearnedPattern.FrequentAnalyze:
                    counter = EnemyIntent.Guard;
                    return true;
                default:
                    counter = EnemyIntent.Attack;
                    return false;
            }
        }

        private double CalculateCounterProbability(double confidence)
        {
            if (IntelligenceLevel == 0)
            {
                return 0d;
            }

            double baseProbability = tuning.GetCounterBaseProbability(IntelligenceLevel);
            double confidenceWeight = tuning.GetCounterConfidenceWeight(IntelligenceLevel);
            return Math.Min(
                tuning.MaximumCounterProbability,
                baseProbability + confidence * confidenceWeight);
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
