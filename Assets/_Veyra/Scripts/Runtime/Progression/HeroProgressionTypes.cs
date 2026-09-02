using System;
using Veyra.Core;

namespace Veyra.Progression
{
    public enum CampaignLevel
    {
        Tutorial = 1,
        ThornGuardian = 2,
        AshWatcher = 3,
        ThreefoldAssault = 4
    }

    public enum HeroMajorUpgrade
    {
        None = 0,
        Attack = 1,
        GuardBastion = 2,
        Guard = GuardBastion,
        Technique = 4,
        Analyze = 8
    }

    [Serializable]
    public struct HeroProgressData
    {
        public int version;
        public int totalExperience;
        public bool tutorialRewardClaimed;
        public bool encounter02RewardClaimed;
        public bool encounter03RewardClaimed;
        public bool level04RewardClaimed;
        public int selectedMajorUpgradesMask;
        public int attackUpgradeRank;
        public int guardUpgradeRank;
        public int techniqueUpgradeRank;
        public int analyzeUpgradeRank;
        public int unspentMajorUpgradePoints;
        public int awardedMajorUpgradeMilestones;

        public bool HasUpgrade(HeroMajorUpgrade upgrade)
        {
            return GetUpgradeRank(upgrade) > 0;
        }

        public int GetUpgradeRank(HeroMajorUpgrade upgrade)
        {
            if (!HeroProgressionRules.IsSelectableUpgrade(upgrade))
            {
                return 0;
            }

            int storedRank = GetStoredUpgradeRank(upgrade);
            if (storedRank == int.MinValue)
            {
                return 0;
            }

            // The mask is retained as a version-1 compatibility bridge. A legacy bit
            // therefore represents rank one until Normalize migrates it explicitly.
            if (storedRank <= 0 && version < 2 &&
                (selectedMajorUpgradesMask & (int)upgrade) != 0)
            {
                storedRank = 1;
            }

            return HeroProgressionRules.NormalizeUpgradeRank(storedRank);
        }

        internal int GetStoredUpgradeRank(HeroMajorUpgrade upgrade)
        {
            switch (upgrade)
            {
                case HeroMajorUpgrade.Attack:
                    return attackUpgradeRank;
                case HeroMajorUpgrade.GuardBastion:
                    return guardUpgradeRank;
                case HeroMajorUpgrade.Technique:
                    return techniqueUpgradeRank;
                case HeroMajorUpgrade.Analyze:
                    return analyzeUpgradeRank;
                default:
                    return int.MinValue;
            }
        }

        internal void SetUpgradeRank(HeroMajorUpgrade upgrade, int rank)
        {
            int normalizedRank = HeroProgressionRules.NormalizeUpgradeRank(rank);
            switch (upgrade)
            {
                case HeroMajorUpgrade.Attack:
                    attackUpgradeRank = normalizedRank;
                    break;
                case HeroMajorUpgrade.GuardBastion:
                    guardUpgradeRank = normalizedRank;
                    break;
                case HeroMajorUpgrade.Technique:
                    techniqueUpgradeRank = normalizedRank;
                    break;
                case HeroMajorUpgrade.Analyze:
                    analyzeUpgradeRank = normalizedRank;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(upgrade), upgrade, null);
            }

            if (normalizedRank > 0)
            {
                selectedMajorUpgradesMask |= (int)upgrade;
            }
            else
            {
                selectedMajorUpgradesMask &= ~(int)upgrade;
            }
        }
    }

    public readonly struct HeroCombatStats
    {
        internal HeroCombatStats(
            int level,
            int maxHp,
            int attackDamage,
            int techniqueDamage,
            int techniqueSplashPercent,
            bool guardBlocksAllDirectEnemyActions,
            bool analyzeRevealsAllEnemyIntents,
            bool analyzeAppliesExposed)
        {
            Level = level;
            MaxHp = maxHp;
            AttackDamage = attackDamage;
            TechniqueDamage = techniqueDamage;
            TechniqueSplashPercent = techniqueSplashPercent;
            GuardBlocksAllDirectEnemyActions = guardBlocksAllDirectEnemyActions;
            AnalyzeRevealsAllEnemyIntents = analyzeRevealsAllEnemyIntents;
            AnalyzeAppliesExposed = analyzeAppliesExposed;
        }

        public int Level { get; }

        public int MaxHp { get; }

        public int AttackDamage { get; }

        public int TechniqueDamage { get; }

        public int TechniqueSplashPercent { get; }

        public bool GuardBlocksAllDirectEnemyActions { get; }

        public bool AnalyzeRevealsAllEnemyIntents { get; }

        public bool AnalyzeAppliesExposed { get; }

        public int ExposedDamagePercent => AnalyzeAppliesExposed
            ? HeroProgressionRules.ExposedDamagePercent
            : 100;
    }

    public readonly struct HeroProgressSnapshot
    {
        internal HeroProgressSnapshot(HeroProgressData data)
        {
            Data = data;
            Level = HeroProgressionRules.GetLevelForExperience(data.totalExperience);
            CombatStats = HeroProgressionRules.GetCombatStats(data);
        }

        public HeroProgressData Data { get; }

        public int TotalExperience => Data.totalExperience;

        public int Level { get; }

        public int UnspentMajorUpgradePoints => Data.unspentMajorUpgradePoints;

        public bool HasPendingMajorUpgrade => UnspentMajorUpgradePoints > 0;

        public int CurrentLevelExperienceThreshold =>
            HeroProgressionRules.GetExperienceThresholdForLevel(Level);

        public int NextLevelExperienceThreshold =>
            HeroProgressionRules.GetNextLevelExperienceThreshold(Level);

        public int ExperienceIntoCurrentLevel =>
            Math.Max(0, TotalExperience - CurrentLevelExperienceThreshold);

        public int ExperienceNeededForNextLevel => NextLevelExperienceThreshold < 0
            ? 0
            : Math.Max(0, NextLevelExperienceThreshold - TotalExperience);

        public bool IsAtCurrentContentLevelCap => NextLevelExperienceThreshold < 0;

        public HeroCombatStats CombatStats { get; }

        public bool HasUpgrade(HeroMajorUpgrade upgrade)
        {
            return Data.HasUpgrade(upgrade);
        }

        public int GetUpgradeRank(HeroMajorUpgrade upgrade)
        {
            return Data.GetUpgradeRank(upgrade);
        }
    }

    public static class HeroProgressionRules
    {
        // Compatibility accessors for the legacy hero save. Campaign rewards are
        // authored once in CampaignLevelCatalog and mirrored here for old callers.
        public static int TutorialExperience => CampaignLevelCatalog.GetByNumber(1).ExperienceReward;
        public static int ThornGuardianExperience => CampaignLevelCatalog.GetByNumber(2).ExperienceReward;
        public static int AshWatcherExperience => CampaignLevelCatalog.GetByNumber(3).ExperienceReward;
        public static int ThreefoldAssaultExperience => CampaignLevelCatalog.GetByNumber(4).ExperienceReward;

        public const int Level02ExperienceThreshold = 100;
        public const int Level03ExperienceThreshold = 300;
        public const int Level04ExperienceThreshold = 500;
        public const int CurrentMaximumLevel = 4;
        public const int MajorUpgradeLevelInterval = 3;
        public const int MaximumMajorUpgradeRank = 3;
        public const int MaximumMajorUpgradePoints = 3;

        public const int BaseMaxHp = 100;
        public const int BaseAttackDamage = 20;
        public const int BaseTechniqueDamage = 32;
        public const int MaxHpPerLevel = 10;
        public const int AttackDamagePerLevel = 2;
        public const int TechniqueDamagePerLevel = 3;

        public const int AttackUpgradeBonus = 8;
        public const int TechniqueUpgradeBonus = 14;
        public const int BaseTechniqueSplashPercent = 35;
        public const int UpgradedTechniqueSplashPercent = 55;
        public const int ExposedDamagePercent = 125;

        private const int SelectableUpgradeMask =
            (int)HeroMajorUpgrade.Attack |
            (int)HeroMajorUpgrade.GuardBastion |
            (int)HeroMajorUpgrade.Technique |
            (int)HeroMajorUpgrade.Analyze;

        public static int GetExperienceReward(CampaignLevel level)
        {
            int levelNumber = (int)level;
            if (!CampaignLevelCatalog.TryGetByNumber(levelNumber, out LevelDefinition definition) ||
                !definition.IsImplemented)
            {
                throw new ArgumentOutOfRangeException(nameof(level), level, "Livello non riconosciuto.");
            }

            return definition.ExperienceReward;
        }

        public static int GetLevelForExperience(int experience)
        {
            int normalizedExperience = Math.Max(0, experience);
            if (normalizedExperience >= Level04ExperienceThreshold)
            {
                return 4;
            }

            if (normalizedExperience >= Level03ExperienceThreshold)
            {
                return 3;
            }

            return normalizedExperience >= Level02ExperienceThreshold ? 2 : 1;
        }

        public static int GetExperienceThresholdForLevel(int level)
        {
            switch (level)
            {
                case 1:
                    return 0;
                case 2:
                    return Level02ExperienceThreshold;
                case 3:
                    return Level03ExperienceThreshold;
                case 4:
                    return Level04ExperienceThreshold;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(level),
                        level,
                        "Sono definiti soltanto i livelli di Hero01 attualmente giocabili (1-4).");
            }
        }

        public static int GetNextLevelExperienceThreshold(int currentLevel)
        {
            switch (currentLevel)
            {
                case 1:
                    return Level02ExperienceThreshold;
                case 2:
                    return Level03ExperienceThreshold;
                case 3:
                    return Level04ExperienceThreshold;
                case 4:
                    return -1;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(currentLevel),
                        currentLevel,
                        "Sono definiti soltanto i livelli di Hero01 attualmente giocabili (1-4).");
            }
        }

        public static int GetEligibleMajorUpgradePoints(int level)
        {
            return Math.Min(
                MaximumMajorUpgradePoints,
                Math.Max(0, level) / MajorUpgradeLevelInterval);
        }

        public static HeroCombatStats GetCombatStats(HeroProgressData progress)
        {
            int level = GetLevelForExperience(progress.totalExperience);
            int gainedLevels = Math.Max(0, level - 1);
            bool attackUpgrade = progress.HasUpgrade(HeroMajorUpgrade.Attack);
            bool guardUpgrade = progress.HasUpgrade(HeroMajorUpgrade.GuardBastion);
            bool techniqueUpgrade = progress.HasUpgrade(HeroMajorUpgrade.Technique);
            bool analyzeUpgrade = progress.HasUpgrade(HeroMajorUpgrade.Analyze);

            return new HeroCombatStats(
                level,
                BaseMaxHp + (gainedLevels * MaxHpPerLevel),
                BaseAttackDamage + (gainedLevels * AttackDamagePerLevel) +
                (attackUpgrade ? AttackUpgradeBonus : 0),
                BaseTechniqueDamage + (gainedLevels * TechniqueDamagePerLevel) +
                (techniqueUpgrade ? TechniqueUpgradeBonus : 0),
                techniqueUpgrade ? UpgradedTechniqueSplashPercent : BaseTechniqueSplashPercent,
                guardUpgrade,
                analyzeUpgrade,
                analyzeUpgrade);
        }

        public static bool IsSelectableUpgrade(HeroMajorUpgrade upgrade)
        {
            int rawValue = (int)upgrade;
            return rawValue != 0 &&
                   (rawValue & (rawValue - 1)) == 0 &&
                   (rawValue & SelectableUpgradeMask) != 0;
        }

        public static bool TrySpendMajorUpgradePoint(
            ref HeroProgressData progress,
            HeroMajorUpgrade upgrade,
            out string failureReason)
        {
            if (!IsSelectableUpgrade(upgrade))
            {
                failureReason = "Potenziamento non valido.";
                return false;
            }

            if (progress.unspentMajorUpgradePoints <= 0)
            {
                failureReason = "Non ci sono punti potenziamento disponibili.";
                return false;
            }

            int currentRank = progress.GetUpgradeRank(upgrade);
            if (currentRank >= MaximumMajorUpgradeRank)
            {
                failureReason = "Questo potenziamento ha già raggiunto il grado massimo.";
                return false;
            }

            if (CountSelectedUpgradeRanks(progress) >= MaximumMajorUpgradePoints)
            {
                failureReason = "Sono già stati assegnati tutti i gradi importanti disponibili.";
                return false;
            }

            progress.SetUpgradeRank(upgrade, currentRank + 1);
            progress.unspentMajorUpgradePoints--;
            failureReason = string.Empty;
            return true;
        }

        internal static int NormalizeUpgradeMask(int mask)
        {
            return mask & SelectableUpgradeMask;
        }

        internal static int CountSelectedUpgrades(int mask)
        {
            int value = NormalizeUpgradeMask(mask);
            int count = 0;
            while (value != 0)
            {
                count += value & 1;
                value >>= 1;
            }

            return count;
        }

        internal static int CountSelectedUpgradeRanks(HeroProgressData progress)
        {
            return progress.GetUpgradeRank(HeroMajorUpgrade.Attack) +
                   progress.GetUpgradeRank(HeroMajorUpgrade.GuardBastion) +
                   progress.GetUpgradeRank(HeroMajorUpgrade.Technique) +
                   progress.GetUpgradeRank(HeroMajorUpgrade.Analyze);
        }

        internal static int NormalizeUpgradeRank(int rank)
        {
            return Math.Max(0, Math.Min(MaximumMajorUpgradeRank, rank));
        }

        public static string GetUpgradeDisplayName(HeroMajorUpgrade upgrade)
        {
            switch (upgrade)
            {
                case HeroMajorUpgrade.Attack:
                    return "ATTACCO";
                case HeroMajorUpgrade.GuardBastion:
                    return "GUARDIA · BASTIONE";
                case HeroMajorUpgrade.Technique:
                    return "TECNICA";
                case HeroMajorUpgrade.Analyze:
                    return "ANALIZZA";
                default:
                    return string.Empty;
            }
        }

        public static string GetUpgradeDescription(HeroMajorUpgrade upgrade)
        {
            switch (upgrade)
            {
                case HeroMajorUpgrade.Attack:
                    return "+8 danni all'attacco base.";
                case HeroMajorUpgrade.GuardBastion:
                    return "Bastione para tutte le azioni nemiche dirette della fase corrente.";
                case HeroMajorUpgrade.Technique:
                    return "+14 danni e danno ad area aumentato dal 35% al 55%.";
                case HeroMajorUpgrade.Analyze:
                    return "Rivela bluff e intenzioni; il prossimo colpo infligge il 25% in più.";
                default:
                    return string.Empty;
            }
        }
    }
}
