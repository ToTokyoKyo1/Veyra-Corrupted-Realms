using System;
using System.Collections.Generic;

namespace Veyra.Combat.MultiEnemy
{
    public sealed class EnemyPlanningContext
    {
        internal EnemyPlanningContext(
            MultiEnemyEnemyState enemy,
            int turnNumber,
            int heroHp,
            int heroMaxHp,
            int activeEnemyCount,
            MultiEnemyPlayerTendencies playerTendencies)
        {
            Enemy = enemy ?? throw new ArgumentNullException(nameof(enemy));
            TurnNumber = turnNumber;
            HeroHp = heroHp;
            HeroMaxHp = heroMaxHp;
            ActiveEnemyCount = activeEnemyCount;
            PlayerTendencies = playerTendencies;
        }

        public MultiEnemyEnemyState Enemy { get; }

        public int TurnNumber { get; }

        public int HeroHp { get; }

        public int HeroMaxHp { get; }

        public int ActiveEnemyCount { get; }

        public MultiEnemyPlayerTendencies PlayerTendencies { get; }

        public double HeroHealthRatio => HeroHp / (double)HeroMaxHp;

        public double EnemyHealthRatio =>
            Enemy.CurrentHp / (double)Enemy.Profile.MaxHp;
    }

    public sealed class EnemyIntentWeights
    {
        private readonly Dictionary<MultiEnemyIntent, double> values =
            new Dictionary<MultiEnemyIntent, double>();

        public void Set(MultiEnemyIntent intent, double weight)
        {
            values[intent] = Math.Max(0d, weight);
        }

        public void Add(MultiEnemyIntent intent, double addedWeight)
        {
            double current;
            values.TryGetValue(intent, out current);
            values[intent] = Math.Max(0d, current + addedWeight);
        }

        public void Multiply(MultiEnemyIntent intent, double multiplier)
        {
            double current;
            if (!values.TryGetValue(intent, out current))
            {
                return;
            }

            values[intent] = Math.Max(0d, current * Math.Max(0d, multiplier));
        }

        public void Blend(MultiEnemyIntent intent, double targetWeight, double influence)
        {
            double current = Get(intent);
            double clampedInfluence = Math.Max(0d, Math.Min(1d, influence));
            Set(intent, current + (targetWeight - current) * clampedInfluence);
        }

        public double Get(MultiEnemyIntent intent)
        {
            double value;
            return values.TryGetValue(intent, out value) ? value : 0d;
        }

        internal MultiEnemyIntent Choose(System.Random random)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            MultiEnemyIntent[] stableOrder =
            {
                MultiEnemyIntent.Attack,
                MultiEnemyIntent.Guard,
                MultiEnemyIntent.Wait,
                MultiEnemyIntent.Finta,
                MultiEnemyIntent.Charge,
                MultiEnemyIntent.Assault
            };

            double total = 0d;
            for (int index = 0; index < stableOrder.Length; index++)
            {
                total += Get(stableOrder[index]);
            }

            if (total <= 0d)
            {
                return MultiEnemyIntent.Wait;
            }

            double roll = random.NextDouble() * total;
            double accumulated = 0d;
            for (int index = 0; index < stableOrder.Length; index++)
            {
                accumulated += Get(stableOrder[index]);
                if (roll < accumulated)
                {
                    return stableOrder[index];
                }
            }

            return MultiEnemyIntent.Wait;
        }
    }

    /// <summary>
    /// Behavior traits only influence a plan before it is locked. They never receive the
    /// player's current input, so mixed enemies cannot react omnisciently to a button press.
    /// </summary>
    public interface IEnemyBehaviorTrait
    {
        EnemyBehaviorTraits Kind { get; }

        void ModifyIntentWeights(
            EnemyPlanningContext context,
            EnemyIntentWeights weights,
            double influence);
    }

    public sealed class AggressiveEnemyTrait : IEnemyBehaviorTrait
    {
        public EnemyBehaviorTraits Kind => EnemyBehaviorTraits.Aggressive;

        public void ModifyIntentWeights(
            EnemyPlanningContext context,
            EnemyIntentWeights weights,
            double influence)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (weights == null)
            {
                throw new ArgumentNullException(nameof(weights));
            }

            weights.Add(MultiEnemyIntent.Attack, 0.55d * influence);
            weights.Multiply(MultiEnemyIntent.Guard, BlendMultiplier(0.45d, influence));
            weights.Multiply(MultiEnemyIntent.Wait, BlendMultiplier(0.35d, influence));

            if (context.Enemy.ConsecutiveAttacks >= 2 &&
                context.Enemy.Profile.AssaultDamage > context.Enemy.Profile.AttackDamage)
            {
                // Assault remains a possibility, not a guaranteed punishment.
                weights.Blend(MultiEnemyIntent.Assault, 0.65d, influence);
                weights.Multiply(MultiEnemyIntent.Attack, BlendMultiplier(0.65d, influence));
            }
        }

        private static double BlendMultiplier(double fullStrengthMultiplier, double influence)
        {
            double clampedInfluence = Math.Max(0d, Math.Min(1d, influence));
            return 1d + (fullStrengthMultiplier - 1d) * clampedInfluence;
        }
    }

    public sealed class PatientEnemyTrait : IEnemyBehaviorTrait
    {
        public EnemyBehaviorTraits Kind => EnemyBehaviorTraits.Patient;

        public void ModifyIntentWeights(
            EnemyPlanningContext context,
            EnemyIntentWeights weights,
            double influence)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (weights == null)
            {
                throw new ArgumentNullException(nameof(weights));
            }

            weights.Blend(MultiEnemyIntent.Attack, 0.22d, influence);
            weights.Blend(MultiEnemyIntent.Guard, 0.16d, influence);
            weights.Blend(MultiEnemyIntent.Wait, 0.30d, influence);
            weights.Blend(
                MultiEnemyIntent.Charge,
                context.Enemy.Profile.ChargedStrikeDamage > 0 ? 0.42d : 0d,
                influence);
        }
    }

    public sealed class DeceptiveEnemyTrait : IEnemyBehaviorTrait
    {
        // Kept as public aliases for validators and existing integrations. Individual
        // profiles can choose stricter values through EnemyDeceptionSettings.
        public const double MaximumBluffProbability =
            EnemyDeceptionSettings.HardMaximumBluffProbability;
        public const int MinimumTurnsBetweenBluffs =
            EnemyDeceptionSettings.HardMinimumTurnsBetweenBluffs;
        public const string InstabilityClue = "L'intenzione mostrata sembra instabile.";

        public EnemyBehaviorTraits Kind => EnemyBehaviorTraits.Deceptive;

        public void ModifyIntentWeights(
            EnemyPlanningContext context,
            EnemyIntentWeights weights,
            double influence)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (weights == null)
            {
                throw new ArgumentNullException(nameof(weights));
            }

            weights.Blend(MultiEnemyIntent.Attack, 0.36d, influence);
            weights.Blend(MultiEnemyIntent.Guard, 0.22d, influence);
            weights.Blend(MultiEnemyIntent.Wait, 0.22d, influence);
            weights.Blend(
                MultiEnemyIntent.Finta,
                context.Enemy.Profile.DeceptionSettings.FeintIntentWeight,
                influence);
            weights.Blend(MultiEnemyIntent.Charge, 0d, influence);
            weights.Blend(MultiEnemyIntent.Assault, 0d, influence);
        }

        internal static bool CanBluff(MultiEnemyEnemyState enemy, int turnNumber)
        {
            return enemy.LastBluffTurn == int.MinValue ||
                   turnNumber - enemy.LastBluffTurn >=
                   enemy.Profile.DeceptionSettings.MinimumTurnsBetweenBluffs;
        }

        internal static double GetBluffProbability(MultiEnemyEnemyState enemy)
        {
            double probability = enemy.Profile.DeceptionSettings.BluffProbability *
                                 enemy.Profile.GetTraitWeight(EnemyBehaviorTraits.Deceptive);
            return Math.Min(MaximumBluffProbability, probability);
        }
    }

    internal sealed class EnemyBehaviorComposer
    {
        private readonly List<IEnemyBehaviorTrait> traits = new List<IEnemyBehaviorTrait>();
        private readonly MultiEnemyProfile profile;

        public EnemyBehaviorComposer(MultiEnemyProfile profile)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            if (profile.HasTrait(EnemyBehaviorTraits.Aggressive))
            {
                traits.Add(new AggressiveEnemyTrait());
            }

            if (profile.HasTrait(EnemyBehaviorTraits.Patient))
            {
                traits.Add(new PatientEnemyTrait());
            }

            if (profile.HasTrait(EnemyBehaviorTraits.Deceptive))
            {
                traits.Add(new DeceptiveEnemyTrait());
            }
        }

        public MultiEnemyIntent ChooseTrueIntent(
            EnemyPlanningContext context,
            System.Random random)
        {
            if (context.Enemy.ChargePrepared)
            {
                if (context.Enemy.Profile.HasTrait(EnemyBehaviorTraits.Patient) &&
                    context.Enemy.ChargeHoldAvailable)
                {
                    return MultiEnemyIntent.HoldCharge;
                }

                return MultiEnemyIntent.ChargedStrike;
            }

            EnemyIntentWeights weights = new EnemyIntentWeights();
            weights.Set(MultiEnemyIntent.Attack, 0.46d);
            weights.Set(MultiEnemyIntent.Guard, 0.26d);
            weights.Set(MultiEnemyIntent.Wait, 0.20d);
            weights.Set(MultiEnemyIntent.Finta, 0d);
            weights.Set(
                MultiEnemyIntent.Charge,
                context.Enemy.Profile.ChargedStrikeDamage > 0 ? 0.08d : 0d);

            for (int index = 0; index < traits.Count; index++)
            {
                IEnemyBehaviorTrait trait = traits[index];
                trait.ModifyIntentWeights(
                    context,
                    weights,
                    profile.GetTraitWeight(trait.Kind));
            }

            ApplyHistoricalPlayerTendencies(context, weights);

            return weights.Choose(random);
        }

        public MultiEnemyIntent ChooseDisplayedIntent(
            EnemyPlanningContext context,
            MultiEnemyIntent trueIntent,
            System.Random random,
            out bool isBluff,
            out string instabilityClue)
        {
            isBluff = false;
            instabilityClue = string.Empty;

            if (!context.Enemy.Profile.HasTrait(EnemyBehaviorTraits.Deceptive) ||
                !IsSafeToDisguise(trueIntent) ||
                !DeceptiveEnemyTrait.CanBluff(context.Enemy, context.TurnNumber) ||
                random.NextDouble() >= DeceptiveEnemyTrait.GetBluffProbability(context.Enemy))
            {
                return trueIntent;
            }

            MultiEnemyIntent[] bluffOptions =
            {
                MultiEnemyIntent.Attack,
                MultiEnemyIntent.Guard,
                MultiEnemyIntent.Wait,
                MultiEnemyIntent.Finta
            };

            int start = random.Next(bluffOptions.Length);
            for (int offset = 0; offset < bluffOptions.Length; offset++)
            {
                MultiEnemyIntent candidate = bluffOptions[(start + offset) % bluffOptions.Length];
                if (candidate == trueIntent)
                {
                    continue;
                }

                isBluff = true;
                instabilityClue = DeceptiveEnemyTrait.InstabilityClue;
                context.Enemy.LastBluffTurn = context.TurnNumber;
                return candidate;
            }

            return trueIntent;
        }

        private static void ApplyHistoricalPlayerTendencies(
            EnemyPlanningContext context,
            EnemyIntentWeights weights)
        {
            MultiEnemyPlayerTendencies tendencies = context.PlayerTendencies;
            if (!tendencies.HasHistory || context.Enemy.Profile.IntelligenceLevel <= 0)
            {
                return;
            }

            // Historical habits only make small probability adjustments. The current
            // input is never supplied here, so this cannot become a perfect counter.
            double intelligence = context.Enemy.Profile.IntelligenceLevel / 3d;
            double influence = tendencies.LearningConfidence * intelligence;
            double attackRatio = tendencies.GetUsageRatio(MultiEnemyHeroAction.Attack);
            double guardRatio = tendencies.GetUsageRatio(MultiEnemyHeroAction.Guard);
            double techniqueRatio = tendencies.GetUsageRatio(MultiEnemyHeroAction.Technique);
            double analyzeRatio = tendencies.GetUsageRatio(MultiEnemyHeroAction.Analyze);

            weights.Add(MultiEnemyIntent.Guard, (attackRatio * 0.12d + techniqueRatio * 0.08d) * influence);
            weights.Add(MultiEnemyIntent.Wait, guardRatio * 0.10d * influence);
            weights.Add(MultiEnemyIntent.Attack, guardRatio * 0.04d * influence);

            if (context.Enemy.Profile.HasTrait(EnemyBehaviorTraits.Deceptive))
            {
                weights.Add(MultiEnemyIntent.Finta, analyzeRatio * 0.10d * influence);
            }
        }

        private static bool IsSafeToDisguise(MultiEnemyIntent intent)
        {
            // Charge and heavy attacks stay honestly telegraphed; a bluff never creates an
            // impossible charge state or hides the Brute's Assault warning.
            return intent == MultiEnemyIntent.Attack ||
                   intent == MultiEnemyIntent.Guard ||
                   intent == MultiEnemyIntent.Wait ||
                   intent == MultiEnemyIntent.Finta;
        }
    }
}
