using System;
using System.Collections.Generic;
using Veyra.Combat.Encounter;
using Veyra.Core;

namespace Veyra.Combat.MultiEnemy
{
    public readonly struct HeroSkillUpgrades
    {
        public HeroSkillUpgrades(
            bool attackMastery,
            bool bastion,
            bool techniqueMastery,
            bool analyzeMastery)
        {
            AttackMastery = attackMastery;
            Bastion = bastion;
            TechniqueMastery = techniqueMastery;
            AnalyzeMastery = analyzeMastery;
        }

        public bool AttackMastery { get; }

        public bool Bastion { get; }

        public bool TechniqueMastery { get; }

        public bool AnalyzeMastery { get; }

        public static HeroSkillUpgrades None => new HeroSkillUpgrades(false, false, false, false);

        public static HeroSkillUpgrades Attack => new HeroSkillUpgrades(true, false, false, false);

        public static HeroSkillUpgrades Guard => new HeroSkillUpgrades(false, true, false, false);

        public static HeroSkillUpgrades Technique => new HeroSkillUpgrades(false, false, true, false);

        public static HeroSkillUpgrades Analyze => new HeroSkillUpgrades(false, false, false, true);
    }

    public sealed class MultiEnemyBattleRules
    {
        public MultiEnemyBattleRules(
            int heroMaxHp,
            int heroAttackDamage,
            int heroTechniqueDamage,
            int techniqueCooldownTurns = 2,
            int enemyGuardReductionPercent = 50,
            int baseTechniqueSplashPercent = 35,
            int upgradedTechniqueSplashPercent = 55,
            int attackUpgradeBonus = 8,
            int techniqueUpgradeBonus = 14,
            int exposedBonusPercent = 25)
        {
            RequirePositive(heroMaxHp, nameof(heroMaxHp));
            RequirePositive(heroAttackDamage, nameof(heroAttackDamage));
            RequirePositive(heroTechniqueDamage, nameof(heroTechniqueDamage));
            RequirePositive(techniqueCooldownTurns, nameof(techniqueCooldownTurns));
            RequirePercent(enemyGuardReductionPercent, nameof(enemyGuardReductionPercent));
            RequirePercent(baseTechniqueSplashPercent, nameof(baseTechniqueSplashPercent));
            RequirePercent(upgradedTechniqueSplashPercent, nameof(upgradedTechniqueSplashPercent));
            RequirePositive(attackUpgradeBonus, nameof(attackUpgradeBonus));
            RequirePositive(techniqueUpgradeBonus, nameof(techniqueUpgradeBonus));
            RequirePositive(exposedBonusPercent, nameof(exposedBonusPercent));

            if (upgradedTechniqueSplashPercent < baseTechniqueSplashPercent)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(upgradedTechniqueSplashPercent),
                    "Upgraded splash cannot be weaker than base splash.");
            }

            HeroMaxHp = heroMaxHp;
            HeroAttackDamage = heroAttackDamage;
            HeroTechniqueDamage = heroTechniqueDamage;
            TechniqueCooldownTurns = techniqueCooldownTurns;
            EnemyGuardReductionPercent = enemyGuardReductionPercent;
            BaseTechniqueSplashPercent = baseTechniqueSplashPercent;
            UpgradedTechniqueSplashPercent = upgradedTechniqueSplashPercent;
            AttackUpgradeBonus = attackUpgradeBonus;
            TechniqueUpgradeBonus = techniqueUpgradeBonus;
            ExposedBonusPercent = exposedBonusPercent;
        }

        public int HeroMaxHp { get; }

        public int HeroAttackDamage { get; }

        public int HeroTechniqueDamage { get; }

        public int TechniqueCooldownTurns { get; }

        public int EnemyGuardReductionPercent { get; }

        public int BaseTechniqueSplashPercent { get; }

        public int UpgradedTechniqueSplashPercent { get; }

        public int AttackUpgradeBonus { get; }

        public int TechniqueUpgradeBonus { get; }

        public int ExposedBonusPercent { get; }

        public static MultiEnemyBattleRules Level04HeroAtLevel3 =>
            new MultiEnemyBattleRules(120, 24, 38);

        private static void RequirePositive(int value, string parameterName)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Value must be greater than zero.");
            }
        }

        private static void RequirePercent(int value, string parameterName)
        {
            if (value < 1 || value > 100)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Value must be between 1 and 100.");
            }
        }
    }

    /// <summary>
    /// Per-profile influence of each combinable behavior trait. Zero disables the
    /// trait's influence while one applies its full authored behavior.
    /// </summary>
    public readonly struct EnemyTraitWeights
    {
        public EnemyTraitWeights(double aggressive, double patient, double deceptive)
        {
            ValidateWeight(aggressive, nameof(aggressive));
            ValidateWeight(patient, nameof(patient));
            ValidateWeight(deceptive, nameof(deceptive));
            Aggressive = aggressive;
            Patient = patient;
            Deceptive = deceptive;
        }

        public double Aggressive { get; }

        public double Patient { get; }

        public double Deceptive { get; }

        public double Get(EnemyBehaviorTraits trait)
        {
            switch (trait)
            {
                case EnemyBehaviorTraits.Aggressive: return Aggressive;
                case EnemyBehaviorTraits.Patient: return Patient;
                case EnemyBehaviorTraits.Deceptive: return Deceptive;
                default: return 0d;
            }
        }

        public static EnemyTraitWeights FullStrength => new EnemyTraitWeights(1d, 1d, 1d);

        private static void ValidateWeight(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d || value > 1d)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Trait weight must be between zero and one.");
            }
        }
    }

    /// <summary>
    /// Fairness limits for deceptive enemies. A bluff is authored as a visible intent
    /// that differs from the already locked true intent; it never reacts to the button
    /// pressed during the current turn.
    /// </summary>
    public readonly struct EnemyDeceptionSettings
    {
        public const double HardMaximumBluffProbability = 0.35d;
        public const int HardMinimumTurnsBetweenBluffs = 3;

        public EnemyDeceptionSettings(
            double bluffProbability,
            int minimumTurnsBetweenBluffs,
            double feintIntentWeight)
        {
            if (double.IsNaN(bluffProbability) || double.IsInfinity(bluffProbability) ||
                bluffProbability < 0d || bluffProbability > HardMaximumBluffProbability)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(bluffProbability),
                    "Bluff probability must be between zero and the fairness cap.");
            }

            if (minimumTurnsBetweenBluffs < HardMinimumTurnsBetweenBluffs)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minimumTurnsBetweenBluffs),
                    "A bluff needs at least three turns of separation.");
            }

            if (double.IsNaN(feintIntentWeight) || double.IsInfinity(feintIntentWeight) ||
                feintIntentWeight < 0d || feintIntentWeight > 1d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(feintIntentWeight),
                    "Feint intent weight must be between zero and one.");
            }

            BluffProbability = bluffProbability;
            MinimumTurnsBetweenBluffs = minimumTurnsBetweenBluffs;
            FeintIntentWeight = feintIntentWeight;
        }

        public double BluffProbability { get; }

        public int MinimumTurnsBetweenBluffs { get; }

        public double FeintIntentWeight { get; }

        public static EnemyDeceptionSettings Default =>
            new EnemyDeceptionSettings(0.30d, 3, 0.20d);
    }

    /// <summary>
    /// Compact combat-only projection of the persistent player profile. Keeping this
    /// type in the combat domain lets the pure battle model remain independent from the
    /// concrete save implementation.
    /// </summary>
    public readonly struct MultiEnemyPlayerTendencies
    {
        public MultiEnemyPlayerTendencies(
            int attackCount,
            int guardCount,
            int techniqueCount,
            int analyzeCount,
            MultiEnemyHeroAction? lastAction = null,
            int currentRepeatCount = 0)
        {
            if (attackCount < 0) throw new ArgumentOutOfRangeException(nameof(attackCount));
            if (guardCount < 0) throw new ArgumentOutOfRangeException(nameof(guardCount));
            if (techniqueCount < 0) throw new ArgumentOutOfRangeException(nameof(techniqueCount));
            if (analyzeCount < 0) throw new ArgumentOutOfRangeException(nameof(analyzeCount));
            if (currentRepeatCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(currentRepeatCount));
            }

            AttackCount = attackCount;
            GuardCount = guardCount;
            TechniqueCount = techniqueCount;
            AnalyzeCount = analyzeCount;
            LastAction = lastAction;
            CurrentRepeatCount = lastAction.HasValue ? currentRepeatCount : 0;
        }

        public int AttackCount { get; }

        public int GuardCount { get; }

        public int TechniqueCount { get; }

        public int AnalyzeCount { get; }

        public MultiEnemyHeroAction? LastAction { get; }

        public int CurrentRepeatCount { get; }

        public int TotalValidActions => AttackCount + GuardCount + TechniqueCount + AnalyzeCount;

        public bool HasHistory => TotalValidActions > 0;

        public double LearningConfidence => Math.Min(1d, TotalValidActions / 20d);

        public MultiEnemyHeroAction? DominantAction
        {
            get
            {
                if (!HasHistory)
                {
                    return null;
                }

                MultiEnemyHeroAction dominant = MultiEnemyHeroAction.Attack;
                int highest = AttackCount;
                if (GuardCount > highest)
                {
                    dominant = MultiEnemyHeroAction.Guard;
                    highest = GuardCount;
                }

                if (TechniqueCount > highest)
                {
                    dominant = MultiEnemyHeroAction.Technique;
                    highest = TechniqueCount;
                }

                if (AnalyzeCount > highest)
                {
                    dominant = MultiEnemyHeroAction.Analyze;
                }

                return dominant;
            }
        }

        public double GetUsageRatio(MultiEnemyHeroAction action)
        {
            if (TotalValidActions == 0)
            {
                return 0d;
            }

            return GetCount(action) / (double)TotalValidActions;
        }

        public int GetCount(MultiEnemyHeroAction action)
        {
            switch (action)
            {
                case MultiEnemyHeroAction.Attack: return AttackCount;
                case MultiEnemyHeroAction.Guard: return GuardCount;
                case MultiEnemyHeroAction.Technique: return TechniqueCount;
                case MultiEnemyHeroAction.Analyze: return AnalyzeCount;
                default: return 0;
            }
        }

        public MultiEnemyPlayerTendencies WithRecordedAction(MultiEnemyHeroAction action)
        {
            int attack = AttackCount;
            int guard = GuardCount;
            int technique = TechniqueCount;
            int analyze = AnalyzeCount;
            switch (action)
            {
                case MultiEnemyHeroAction.Attack: attack++; break;
                case MultiEnemyHeroAction.Guard: guard++; break;
                case MultiEnemyHeroAction.Technique: technique++; break;
                case MultiEnemyHeroAction.Analyze: analyze++; break;
                default: throw new ArgumentOutOfRangeException(nameof(action));
            }

            int repeatCount = LastAction.HasValue && LastAction.Value == action
                ? CurrentRepeatCount + 1
                : 1;
            return new MultiEnemyPlayerTendencies(
                attack,
                guard,
                technique,
                analyze,
                action,
                repeatCount);
        }

        public static MultiEnemyPlayerTendencies None =>
            new MultiEnemyPlayerTendencies(0, 0, 0, 0);
    }

    public sealed class MultiEnemyProfile
    {
        public MultiEnemyProfile(
            string enemyId,
            string displayName,
            string race,
            int maxHp,
            int corruptionPercent,
            EnemyMood mood,
            int intelligenceLevel,
            EnemyAltitude altitude,
            int attackDamage,
            int chargedStrikeDamage,
            int assaultDamage,
            EnemyBehaviorTraits traits,
            EnemyTraitWeights? traitWeights = null,
            EnemyDeceptionSettings? deceptionSettings = null)
        {
            EnemyTraitWeights resolvedTraitWeights = traitWeights ?? EnemyTraitWeights.FullStrength;
            EnemyDeceptionSettings resolvedDeceptionSettings =
                deceptionSettings ?? EnemyDeceptionSettings.Default;
            if (string.IsNullOrWhiteSpace(enemyId))
            {
                throw new ArgumentException("Enemy id cannot be empty.", nameof(enemyId));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("Display name cannot be empty.", nameof(displayName));
            }

            if (string.IsNullOrWhiteSpace(race))
            {
                throw new ArgumentException("Race cannot be empty.", nameof(race));
            }

            if (maxHp <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHp));
            }

            if (attackDamage <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(attackDamage));
            }

            if (chargedStrikeDamage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(chargedStrikeDamage));
            }

            if (assaultDamage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(assaultDamage));
            }

            if ((traits & EnemyBehaviorTraits.Patient) != 0 &&
                resolvedTraitWeights.Get(EnemyBehaviorTraits.Patient) > 0d &&
                chargedStrikeDamage <= attackDamage)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(chargedStrikeDamage),
                    "A patient enemy needs a charged strike stronger than its normal attack.");
            }

            if ((traits & EnemyBehaviorTraits.Aggressive) != 0 &&
                resolvedTraitWeights.Get(EnemyBehaviorTraits.Aggressive) > 0d &&
                assaultDamage <= attackDamage)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(assaultDamage),
                    "An aggressive enemy needs an assault stronger than its normal attack.");
            }

            EnemyId = enemyId.Trim();
            DisplayName = displayName.Trim();
            Race = race.Trim();
            MaxHp = maxHp;
            CorruptionPercent = Corruption.Clamp(corruptionPercent);
            Mood = mood;
            IntelligenceLevel = Math.Max(0, Math.Min(3, intelligenceLevel));
            Altitude = altitude;
            AttackDamage = attackDamage;
            ChargedStrikeDamage = chargedStrikeDamage;
            AssaultDamage = assaultDamage;
            Traits = traits;
            TraitWeights = resolvedTraitWeights;
            DeceptionSettings = resolvedDeceptionSettings;
        }

        public string EnemyId { get; }

        public string DisplayName { get; }

        public string Race { get; }

        public int MaxHp { get; }

        public int CorruptionPercent { get; }

        public EnemyMood Mood { get; }

        public int IntelligenceLevel { get; }

        public EnemyAltitude Altitude { get; }

        public int AttackDamage { get; }

        public int ChargedStrikeDamage { get; }

        public int AssaultDamage { get; }

        public EnemyBehaviorTraits Traits { get; }

        public EnemyTraitWeights TraitWeights { get; }

        public EnemyDeceptionSettings DeceptionSettings { get; }

        public bool HasTrait(EnemyBehaviorTraits trait)
        {
            return (Traits & trait) != 0 && TraitWeights.Get(trait) > 0d;
        }

        public double GetTraitWeight(EnemyBehaviorTraits trait)
        {
            return HasTrait(trait) ? TraitWeights.Get(trait) : 0d;
        }
    }

    public sealed class MultiEnemyEnemyState
    {
        internal MultiEnemyEnemyState(MultiEnemyProfile profile)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            Reset();
        }

        public MultiEnemyProfile Profile { get; }

        public int CurrentHp { get; internal set; }

        public bool GuardPrepared { get; internal set; }

        public bool ChargePrepared { get; internal set; }

        public bool ChargeHoldAvailable { get; internal set; }

        public int ConsecutiveAttacks { get; internal set; }

        public int LastBluffTurn { get; internal set; }

        public bool Exposed { get; internal set; }

        public EnemyMoralOutcome MoralOutcome { get; internal set; }

        public bool IsIncapacitated => CurrentHp <= 0;

        internal void Reset()
        {
            CurrentHp = Profile.MaxHp;
            GuardPrepared = false;
            ChargePrepared = false;
            ChargeHoldAvailable = false;
            ConsecutiveAttacks = 0;
            LastBluffTurn = int.MinValue;
            Exposed = false;
            MoralOutcome = EnemyMoralOutcome.None;
        }
    }

    public static class Level04EnemyRoster
    {
        public const string BruteId = CampaignContentIds.Level04BruteEnemy;
        public const string WatcherId = CampaignContentIds.Level04WatcherEnemy;
        public const string MaskId = CampaignContentIds.Level04MaskEnemy;

        public static IReadOnlyList<MultiEnemyProfile> Create()
        {
            return new List<MultiEnemyProfile>
            {
                new MultiEnemyProfile(
                    BruteId,
                    "Bruto delle Radici",
                    "Umano Corrotto",
                    50,
                    74,
                    EnemyMood.Arrabbiato,
                    1,
                    EnemyAltitude.Ground,
                    10,
                    0,
                    14,
                    EnemyBehaviorTraits.Aggressive,
                    new EnemyTraitWeights(1d, 0d, 0d)),
                new MultiEnemyProfile(
                    WatcherId,
                    "Veglia Sospesa",
                    "Spirito Corrotto",
                    45,
                    61,
                    EnemyMood.Guardingo,
                    2,
                    EnemyAltitude.Flying,
                    6,
                    18,
                    0,
                    EnemyBehaviorTraits.Patient,
                    new EnemyTraitWeights(0d, 1d, 0d)),
                new MultiEnemyProfile(
                    MaskId,
                    "Maschera del Vento",
                    "Creatura Mutaforma",
                    38,
                    69,
                    EnemyMood.Felice,
                    3,
                    EnemyAltitude.Flying,
                    8,
                    0,
                    0,
                    EnemyBehaviorTraits.Deceptive,
                    new EnemyTraitWeights(0d, 0d, 1d),
                    EnemyDeceptionSettings.Default)
            }.AsReadOnly();
        }
    }
}
