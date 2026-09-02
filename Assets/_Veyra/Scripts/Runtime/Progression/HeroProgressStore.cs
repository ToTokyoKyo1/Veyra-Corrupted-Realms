using System;
using UnityEngine;
using Veyra.Core;

namespace Veyra.Progression
{
    public static class HeroProgressStore
    {
        public const string ProgressKey = "Veyra.Hero01.Progress";
        public const int CurrentVersion = 2;

        public static HeroProgressData Defaults => new HeroProgressData
        {
            version = CurrentVersion,
            totalExperience = 0,
            tutorialRewardClaimed = false,
            encounter02RewardClaimed = false,
            encounter03RewardClaimed = false,
            level04RewardClaimed = false,
            selectedMajorUpgradesMask = 0,
            attackUpgradeRank = 0,
            guardUpgradeRank = 0,
            techniqueUpgradeRank = 0,
            analyzeUpgradeRank = 0,
            unspentMajorUpgradePoints = 0,
            awardedMajorUpgradeMilestones = 0
        };

        public static HeroProgressData Load()
        {
            HeroProgressData progress = Defaults;
            bool shouldSave = false;

            if (PlayerPrefs.HasKey(ProgressKey))
            {
                string json = PlayerPrefs.GetString(ProgressKey, string.Empty);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    try
                    {
                        progress = JsonUtility.FromJson<HeroProgressData>(json);
                    }
                    catch (ArgumentException)
                    {
                        progress = Defaults;
                        shouldSave = true;
                    }
                }
                else
                {
                    shouldSave = true;
                }
            }

            progress = Normalize(progress, ref shouldSave);
            CampaignProgressData campaign = CampaignProgressStore.Load();
            ReconcileCompletedCampaignRewards(ref progress, campaign, ref shouldSave);
            AwardReachedMajorUpgradeMilestones(ref progress, ref shouldSave);

            if (shouldSave)
            {
                Save(progress);
            }

            return progress;
        }

        public static HeroProgressSnapshot GetSnapshot()
        {
            return new HeroProgressSnapshot(Load());
        }

        public static HeroCombatStats GetCombatStats()
        {
            return HeroProgressionRules.GetCombatStats(Load());
        }

        public static bool RecordFirstClear(CampaignLevel level)
        {
            HeroProgressData progress = Load();
            if (IsRewardClaimed(progress, level))
            {
                return false;
            }

            SetRewardClaimed(ref progress, level);
            RecalculateExperience(ref progress);
            bool changed = true;
            AwardReachedMajorUpgradeMilestones(ref progress, ref changed);
            Save(progress);
            return true;
        }

        public static bool TryChooseMajorUpgrade(
            HeroMajorUpgrade upgrade,
            out HeroProgressData updatedProgress,
            out string failureReason)
        {
            HeroProgressData progress = Load();
            if (!HeroProgressionRules.TrySpendMajorUpgradePoint(
                    ref progress,
                    upgrade,
                    out failureReason))
            {
                updatedProgress = progress;
                return false;
            }

            Save(progress);
            updatedProgress = progress;
            return true;
        }

        public static bool TryChooseMajorUpgrade(HeroMajorUpgrade upgrade, out string failureReason)
        {
            return TryChooseMajorUpgrade(upgrade, out _, out failureReason);
        }

        public static void Reset()
        {
            PlayerPrefs.DeleteKey(ProgressKey);
            PlayerPrefs.Save();
        }

        private static HeroProgressData Normalize(HeroProgressData progress, ref bool changed)
        {
            int normalizedMask = HeroProgressionRules.NormalizeUpgradeMask(
                progress.selectedMajorUpgradesMask);
            if (normalizedMask != progress.selectedMajorUpgradesMask)
            {
                progress.selectedMajorUpgradesMask = normalizedMask;
                changed = true;
            }

            bool legacyUpgradeData = progress.version < 2;
            if (legacyUpgradeData)
            {
                MigrateLegacyUpgradeRanks(ref progress, ref changed);
            }

            NormalizeUpgradeRanks(ref progress, ref changed);

            if (progress.version != CurrentVersion)
            {
                progress.version = CurrentVersion;
                changed = true;
            }

            int normalizedUnspent = Math.Max(
                0,
                Math.Min(
                    HeroProgressionRules.MaximumMajorUpgradePoints,
                    progress.unspentMajorUpgradePoints));
            if (progress.unspentMajorUpgradePoints != normalizedUnspent)
            {
                progress.unspentMajorUpgradePoints = normalizedUnspent;
                changed = true;
            }

            int normalizedMilestones = Math.Max(
                0,
                Math.Min(
                    HeroProgressionRules.MaximumMajorUpgradePoints,
                    progress.awardedMajorUpgradeMilestones));
            if (progress.awardedMajorUpgradeMilestones != normalizedMilestones)
            {
                progress.awardedMajorUpgradeMilestones = normalizedMilestones;
                changed = true;
            }

            int storedExperience = progress.totalExperience;
            RecalculateExperience(ref progress);
            if (progress.totalExperience != storedExperience)
            {
                changed = true;
            }

            return progress;
        }

        private static void MigrateLegacyUpgradeRanks(
            ref HeroProgressData progress,
            ref bool changed)
        {
            HeroMajorUpgrade[] upgrades =
            {
                HeroMajorUpgrade.Attack,
                HeroMajorUpgrade.GuardBastion,
                HeroMajorUpgrade.Technique,
                HeroMajorUpgrade.Analyze
            };

            for (int index = 0; index < upgrades.Length; index++)
            {
                HeroMajorUpgrade upgrade = upgrades[index];
                if ((progress.selectedMajorUpgradesMask & (int)upgrade) == 0)
                {
                    continue;
                }

                progress.SetUpgradeRank(
                    upgrade,
                    Math.Max(1, progress.GetUpgradeRank(upgrade)));
                changed = true;
            }
        }

        private static void NormalizeUpgradeRanks(
            ref HeroProgressData progress,
            ref bool changed)
        {
            HeroMajorUpgrade[] upgrades =
            {
                HeroMajorUpgrade.Attack,
                HeroMajorUpgrade.GuardBastion,
                HeroMajorUpgrade.Technique,
                HeroMajorUpgrade.Analyze
            };

            int expectedMask = 0;
            int remainingRanks = HeroProgressionRules.MaximumMajorUpgradePoints;
            for (int index = 0; index < upgrades.Length; index++)
            {
                HeroMajorUpgrade upgrade = upgrades[index];
                int storedRank = progress.GetStoredUpgradeRank(upgrade);
                int normalizedRank = Math.Min(
                    remainingRanks,
                    HeroProgressionRules.NormalizeUpgradeRank(progress.GetUpgradeRank(upgrade)));
                remainingRanks -= normalizedRank;
                if (normalizedRank > 0)
                {
                    expectedMask |= (int)upgrade;
                }

                if (storedRank != normalizedRank)
                {
                    changed = true;
                }

                progress.SetUpgradeRank(upgrade, normalizedRank);
            }

            if (progress.selectedMajorUpgradesMask != expectedMask)
            {
                progress.selectedMajorUpgradesMask = expectedMask;
                changed = true;
            }
        }

        private static void ReconcileCompletedCampaignRewards(
            ref HeroProgressData progress,
            CampaignProgressData campaign,
            ref bool changed)
        {
            bool tutorialCompleted = campaign.tutorialCompleted ||
                                     campaign.encounter02Resolved ||
                                     campaign.encounter03Resolved ||
                                     campaign.level04Completed;
            bool level02Completed = campaign.encounter02Resolved ||
                                    campaign.encounter03Resolved ||
                                    campaign.level04Completed;
            bool level03Completed = campaign.encounter03Resolved || campaign.level04Completed;

            changed |= ClaimIfCompleted(
                ref progress.tutorialRewardClaimed,
                tutorialCompleted);
            changed |= ClaimIfCompleted(
                ref progress.encounter02RewardClaimed,
                level02Completed);
            changed |= ClaimIfCompleted(
                ref progress.encounter03RewardClaimed,
                level03Completed);
            changed |= ClaimIfCompleted(
                ref progress.level04RewardClaimed,
                campaign.level04Completed);

            int storedExperience = progress.totalExperience;
            RecalculateExperience(ref progress);
            changed |= storedExperience != progress.totalExperience;
        }

        private static bool ClaimIfCompleted(ref bool claimed, bool completed)
        {
            if (!completed || claimed)
            {
                return false;
            }

            claimed = true;
            return true;
        }

        private static void AwardReachedMajorUpgradeMilestones(
            ref HeroProgressData progress,
            ref bool changed)
        {
            int selectedCount = HeroProgressionRules.CountSelectedUpgradeRanks(progress);
            int level = HeroProgressionRules.GetLevelForExperience(progress.totalExperience);
            int eligibleMilestones = HeroProgressionRules.GetEligibleMajorUpgradePoints(level);
            int normalizedAwarded = Math.Min(
                HeroProgressionRules.MaximumMajorUpgradePoints,
                Math.Max(
                    selectedCount,
                    Math.Max(progress.awardedMajorUpgradeMilestones, eligibleMilestones)));
            if (progress.awardedMajorUpgradeMilestones != normalizedAwarded)
            {
                progress.awardedMajorUpgradeMilestones = normalizedAwarded;
                changed = true;
            }

            int expectedUnspent = Math.Max(0, normalizedAwarded - selectedCount);
            if (progress.unspentMajorUpgradePoints != expectedUnspent)
            {
                progress.unspentMajorUpgradePoints = expectedUnspent;
                changed = true;
            }
        }

        private static bool IsRewardClaimed(HeroProgressData progress, CampaignLevel level)
        {
            switch (level)
            {
                case CampaignLevel.Tutorial:
                    return progress.tutorialRewardClaimed;
                case CampaignLevel.ThornGuardian:
                    return progress.encounter02RewardClaimed;
                case CampaignLevel.AshWatcher:
                    return progress.encounter03RewardClaimed;
                case CampaignLevel.ThreefoldAssault:
                    return progress.level04RewardClaimed;
                default:
                    throw new ArgumentOutOfRangeException(nameof(level), level, "Livello non riconosciuto.");
            }
        }

        private static void SetRewardClaimed(ref HeroProgressData progress, CampaignLevel level)
        {
            switch (level)
            {
                case CampaignLevel.Tutorial:
                    progress.tutorialRewardClaimed = true;
                    return;
                case CampaignLevel.ThornGuardian:
                    progress.encounter02RewardClaimed = true;
                    return;
                case CampaignLevel.AshWatcher:
                    progress.encounter03RewardClaimed = true;
                    return;
                case CampaignLevel.ThreefoldAssault:
                    progress.level04RewardClaimed = true;
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(level), level, "Livello non riconosciuto.");
            }
        }

        private static void RecalculateExperience(ref HeroProgressData progress)
        {
            int total = 0;
            if (progress.tutorialRewardClaimed)
            {
                total += HeroProgressionRules.TutorialExperience;
            }

            if (progress.encounter02RewardClaimed)
            {
                total += HeroProgressionRules.ThornGuardianExperience;
            }

            if (progress.encounter03RewardClaimed)
            {
                total += HeroProgressionRules.AshWatcherExperience;
            }

            if (progress.level04RewardClaimed)
            {
                total += HeroProgressionRules.ThreefoldAssaultExperience;
            }

            progress.totalExperience = total;
        }

        private static void Save(HeroProgressData progress)
        {
            progress.version = CurrentVersion;
            PlayerPrefs.SetString(ProgressKey, JsonUtility.ToJson(progress));
            PlayerPrefs.Save();
        }
    }
}
