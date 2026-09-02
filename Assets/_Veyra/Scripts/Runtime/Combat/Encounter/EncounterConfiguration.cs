using System;

namespace Veyra.Combat.Encounter
{
    [Serializable]
    public sealed class AdaptiveEnemyTuning
    {
        public const double AbsoluteMaximumCounterProbability = 0.65d;

        private static readonly double[] DefaultCounterBaseProbabilities =
        {
            0d,
            0.12d,
            0.25d,
            0.35d
        };

        private static readonly double[] DefaultCounterConfidenceWeights =
        {
            0d,
            0.30d,
            0.40d,
            0.45d
        };

        public int MinimumObservedActions = 2;
        public int RepeatedActionConsecutiveThreshold = 3;
        public int RepeatedActionFrequencyThreshold = 3;
        public double TechniqueRhythmThreshold = 0.60d;
        public int AnalyzePatternMinimumCount = 3;
        public double AnalyzePatternFrequencyThreshold = 0.40d;
        public double AnalyzeResponseProbabilityMultiplier = 0.65d;
        public int StrategyChangeMinimumHistory = 4;
        public int StrategyChangePrecedingRunThreshold = 3;
        public int StrategyChangeDifferentEarlierActionsThreshold = 2;
        public double RepetitionConfidenceBonus = 0.18d;
        public double TechniqueRhythmConfidenceBonus = 0.12d;
        public double StrategyChangeConfidenceMultiplier = 0.35d;
        public int MinimumIntelligenceForVisibleLearningFeedback = 2;
        public double[] CounterBaseProbabilitiesByIntelligence =
            (double[])DefaultCounterBaseProbabilities.Clone();
        public double[] CounterConfidenceWeightsByIntelligence =
            (double[])DefaultCounterConfidenceWeights.Clone();
        public double MaximumCounterProbability = AbsoluteMaximumCounterProbability;

        public static AdaptiveEnemyTuning Default => new AdaptiveEnemyTuning();

        internal static AdaptiveEnemyTuning Normalize(AdaptiveEnemyTuning source)
        {
            source = source ?? Default;
            return new AdaptiveEnemyTuning
            {
                MinimumObservedActions = Math.Max(1, source.MinimumObservedActions),
                RepeatedActionConsecutiveThreshold = Math.Max(
                    1,
                    source.RepeatedActionConsecutiveThreshold),
                RepeatedActionFrequencyThreshold = Math.Max(
                    1,
                    source.RepeatedActionFrequencyThreshold),
                TechniqueRhythmThreshold = Clamp01(source.TechniqueRhythmThreshold),
                AnalyzePatternMinimumCount = Math.Max(
                    1,
                    source.AnalyzePatternMinimumCount),
                AnalyzePatternFrequencyThreshold = Clamp01(
                    source.AnalyzePatternFrequencyThreshold),
                AnalyzeResponseProbabilityMultiplier = Clamp01(
                    source.AnalyzeResponseProbabilityMultiplier),
                StrategyChangeMinimumHistory = Math.Max(
                    2,
                    source.StrategyChangeMinimumHistory),
                StrategyChangePrecedingRunThreshold = Math.Max(
                    1,
                    source.StrategyChangePrecedingRunThreshold),
                StrategyChangeDifferentEarlierActionsThreshold = Math.Max(
                    1,
                    source.StrategyChangeDifferentEarlierActionsThreshold),
                RepetitionConfidenceBonus = Clamp01(source.RepetitionConfidenceBonus),
                TechniqueRhythmConfidenceBonus = Clamp01(
                    source.TechniqueRhythmConfidenceBonus),
                StrategyChangeConfidenceMultiplier = Clamp01(
                    source.StrategyChangeConfidenceMultiplier),
                MinimumIntelligenceForVisibleLearningFeedback = Math.Max(
                    0,
                    Math.Min(3, source.MinimumIntelligenceForVisibleLearningFeedback)),
                CounterBaseProbabilitiesByIntelligence = NormalizeIntelligenceValues(
                    source.CounterBaseProbabilitiesByIntelligence,
                    DefaultCounterBaseProbabilities),
                CounterConfidenceWeightsByIntelligence = NormalizeIntelligenceValues(
                    source.CounterConfidenceWeightsByIntelligence,
                    DefaultCounterConfidenceWeights),
                MaximumCounterProbability = Math.Min(
                    AbsoluteMaximumCounterProbability,
                    Clamp01(source.MaximumCounterProbability))
            };
        }

        internal double GetCounterBaseProbability(int intelligenceLevel)
        {
            return CounterBaseProbabilitiesByIntelligence[ClampIntelligence(intelligenceLevel)];
        }

        internal double GetCounterConfidenceWeight(int intelligenceLevel)
        {
            return CounterConfidenceWeightsByIntelligence[ClampIntelligence(intelligenceLevel)];
        }

        private static double[] NormalizeIntelligenceValues(
            double[] source,
            double[] defaults)
        {
            double[] normalized = new double[4];
            normalized[0] = 0d;
            for (int index = 1; index < normalized.Length; index++)
            {
                double value = source != null && index < source.Length
                    ? source[index]
                    : defaults[index];
                normalized[index] = Clamp01(value);
            }

            return normalized;
        }

        private static int ClampIntelligence(int value)
        {
            return Math.Max(0, Math.Min(3, value));
        }

        private static double Clamp01(double value)
        {
            return Math.Max(0d, Math.Min(1d, value));
        }
    }

    public sealed class EnemyProfile
    {
        public EnemyProfile(
            string encounterId,
            string displayName,
            string race,
            int corruptionPercent,
            EnemyMood initialMood,
            int intelligenceLevel)
        {
            if (string.IsNullOrWhiteSpace(encounterId))
            {
                throw new ArgumentException("Encounter id cannot be empty.", nameof(encounterId));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("Enemy display name cannot be empty.", nameof(displayName));
            }

            if (string.IsNullOrWhiteSpace(race))
            {
                throw new ArgumentException("Enemy race cannot be empty.", nameof(race));
            }

            EncounterId = encounterId.Trim();
            DisplayName = displayName.Trim();
            Race = race.Trim();
            CorruptionPercent = Corruption.Clamp(corruptionPercent);
            InitialMood = initialMood;
            IntelligenceLevel = ClampIntelligence(intelligenceLevel);
        }

        public string EncounterId { get; }

        public string DisplayName { get; }

        public string Race { get; }

        public int CorruptionPercent { get; }

        public EnemyMood InitialMood { get; }

        public int IntelligenceLevel { get; }

        private static int ClampIntelligence(int value)
        {
            if (value < 0)
            {
                return 0;
            }

            return value > 3 ? 3 : value;
        }
    }

    public sealed class EncounterRules
    {
        public EncounterRules(
            int heroMaxHp = 100,
            int enemyMaxHp = 115,
            int attackDamage = 20,
            int techniqueDamage = 32,
            int enemyAttackDamage = 16,
            int chargedStrikeDamage = 32,
            int techniqueCooldownTurns = 2,
            int enemyGuardReductionPercent = 60,
            bool analyzeAppliesExposed = false,
            int exposedDamagePercent = 125)
        {
            RequirePositive(heroMaxHp, nameof(heroMaxHp));
            RequirePositive(enemyMaxHp, nameof(enemyMaxHp));
            RequirePositive(attackDamage, nameof(attackDamage));
            RequirePositive(techniqueDamage, nameof(techniqueDamage));
            RequirePositive(enemyAttackDamage, nameof(enemyAttackDamage));
            RequirePositive(chargedStrikeDamage, nameof(chargedStrikeDamage));

            if (techniqueDamage <= attackDamage)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(techniqueDamage),
                    "Technique damage must be greater than attack damage.");
            }

            if (chargedStrikeDamage <= enemyAttackDamage)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(chargedStrikeDamage),
                    "Charged strike damage must be greater than enemy attack damage.");
            }

            if (techniqueCooldownTurns <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(techniqueCooldownTurns));
            }

            if (enemyGuardReductionPercent < 1 || enemyGuardReductionPercent > 99)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(enemyGuardReductionPercent),
                    "Enemy guard reduction must be between 1 and 99 percent.");
            }

            if (exposedDamagePercent < 100)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(exposedDamagePercent),
                    "Exposed damage must be at least 100 percent.");
            }

            HeroMaxHp = heroMaxHp;
            EnemyMaxHp = enemyMaxHp;
            AttackDamage = attackDamage;
            TechniqueDamage = techniqueDamage;
            EnemyAttackDamage = enemyAttackDamage;
            ChargedStrikeDamage = chargedStrikeDamage;
            TechniqueCooldownTurns = techniqueCooldownTurns;
            EnemyGuardReductionPercent = enemyGuardReductionPercent;
            AnalyzeAppliesExposed = analyzeAppliesExposed;
            ExposedDamagePercent = exposedDamagePercent;
        }

        public int HeroMaxHp { get; }

        public int EnemyMaxHp { get; }

        public int AttackDamage { get; }

        public int TechniqueDamage { get; }

        public int EnemyAttackDamage { get; }

        public int ChargedStrikeDamage { get; }

        public int TechniqueCooldownTurns { get; }

        public int EnemyGuardReductionPercent { get; }

        public bool AnalyzeAppliesExposed { get; }

        public int ExposedDamagePercent { get; }

        private static void RequirePositive(int value, string parameterName)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Value must be greater than zero.");
            }
        }
    }
}
