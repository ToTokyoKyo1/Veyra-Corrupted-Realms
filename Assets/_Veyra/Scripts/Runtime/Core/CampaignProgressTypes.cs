using System;
using System.Collections.Generic;

namespace Veyra.Core
{
    public enum CampaignEncounter
    {
        ThornGuardian = 2,
        AshWatcher = 3,
        ThreefoldAssault = 4
    }

    public enum EncounterResolution
    {
        None = 0,
        Saved = 1,
        Killed = 2
    }

    public enum PlayerCombatAction
    {
        Attack = 0,
        Guard = 1,
        Technique = 2,
        Analyze = 3
    }

    [Serializable]
    public sealed class CampaignLevelProgressRecord
    {
        public string levelId;
        public bool completed;
        public bool rewardClaimed;
        public int completionCount;

        public CampaignLevelProgressRecord()
        {
            levelId = string.Empty;
        }

        public CampaignLevelProgressRecord(
            string levelId,
            bool completed = false,
            bool rewardClaimed = false,
            int completionCount = 0)
        {
            this.levelId = levelId ?? string.Empty;
            this.completed = completed;
            this.rewardClaimed = rewardClaimed;
            this.completionCount = Math.Max(completed ? 1 : 0, completionCount);
        }
    }

    [Serializable]
    public sealed class CampaignMoralDecisionRecord
    {
        public string levelId;
        public string enemyId;
        public EncounterResolution resolution;

        public CampaignMoralDecisionRecord()
        {
            levelId = string.Empty;
            enemyId = string.Empty;
            resolution = EncounterResolution.None;
        }

        public CampaignMoralDecisionRecord(
            string levelId,
            string enemyId,
            EncounterResolution resolution)
        {
            this.levelId = levelId ?? string.Empty;
            this.enemyId = enemyId ?? string.Empty;
            this.resolution = resolution;
        }
    }

    [Serializable]
    public sealed class CampaignTutorialRecord
    {
        public string tutorialId;
        public bool seen;

        public CampaignTutorialRecord()
        {
            tutorialId = string.Empty;
        }

        public CampaignTutorialRecord(string tutorialId, bool seen)
        {
            this.tutorialId = tutorialId ?? string.Empty;
            this.seen = seen;
        }
    }

    [Serializable]
    public struct PlayerActionProfileData
    {
        public int attackCount;
        public int guardCount;
        public int techniqueCount;
        public int analyzeCount;
        public int totalValidActions;
        public List<PlayerCombatAction> recentActions;
    }

    /// <summary>
    /// Version-three campaign data. The four scalar fields below are retained as
    /// a compatibility bridge for existing scenes and validators; records are the
    /// canonical extensible representation written to disk.
    /// </summary>
    [Serializable]
    public struct CampaignProgressData
    {
        public int saveVersion;
        public List<CampaignLevelProgressRecord> levelRecords;
        public List<CampaignMoralDecisionRecord> moralDecisions;
        public List<CampaignTutorialRecord> tutorialRecords;
        public PlayerActionProfileData playerActionProfile;

        // Legacy v1/v2 compatibility fields. Do not remove until every saved build
        // and every authored scene has migrated to stable-id APIs.
        public int version;
        public bool tutorialCompleted;
        public bool encounter02Resolved;
        public EncounterResolution encounter02Resolution;
        public bool encounter03Resolved;
        public EncounterResolution encounter03Resolution;
        public bool level04Completed;
        public EncounterResolution level04BruteResolution;
        public EncounterResolution level04WatcherResolution;
        public EncounterResolution level04MaskResolution;

        public bool HasAnyProgress
        {
            get
            {
                if (tutorialCompleted || encounter02Resolved || encounter03Resolved || level04Completed)
                {
                    return true;
                }

                if (levelRecords != null)
                {
                    for (int index = 0; index < levelRecords.Count; index++)
                    {
                        if (levelRecords[index] != null && levelRecords[index].completed)
                        {
                            return true;
                        }
                    }
                }

                if (moralDecisions != null && moralDecisions.Count > 0)
                {
                    return true;
                }

                if (tutorialRecords != null)
                {
                    for (int index = 0; index < tutorialRecords.Count; index++)
                    {
                        if (tutorialRecords[index] != null && tutorialRecords[index].seen)
                        {
                            return true;
                        }
                    }
                }

                if (playerActionProfile.attackCount > 0 ||
                    playerActionProfile.guardCount > 0 ||
                    playerActionProfile.techniqueCount > 0 ||
                    playerActionProfile.analyzeCount > 0 ||
                    (playerActionProfile.recentActions != null &&
                     playerActionProfile.recentActions.Count > 0))
                {
                    return true;
                }

                return false;
            }
        }

        public int CompletedLevelCount
        {
            get
            {
                int completed = 0;
                for (int levelNumber = 1; levelNumber <= CampaignLevelCatalog.ImplementedLevelCount;
                     levelNumber++)
                {
                    LevelDefinition definition = CampaignLevelCatalog.GetByNumber(levelNumber);
                    if (HasCompletedRecord(definition.StableId) || HasLegacyCompletion(levelNumber))
                    {
                        completed++;
                    }
                }

                return completed;
            }
        }

        private bool HasCompletedRecord(string levelId)
        {
            if (levelRecords == null)
            {
                return false;
            }

            for (int index = 0; index < levelRecords.Count; index++)
            {
                CampaignLevelProgressRecord record = levelRecords[index];
                if (record != null && record.completed &&
                    string.Equals(record.levelId, levelId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasLegacyCompletion(int levelNumber)
        {
            switch (levelNumber)
            {
                case 1: return tutorialCompleted;
                case 2: return encounter02Resolved;
                case 3: return encounter03Resolved;
                case 4: return level04Completed;
                default: return false;
            }
        }
    }

    public readonly struct CampaignLevelProgressSnapshot
    {
        internal CampaignLevelProgressSnapshot(
            string levelId,
            bool completed,
            bool rewardClaimed,
            int completionCount)
        {
            LevelId = levelId ?? string.Empty;
            Completed = completed;
            RewardClaimed = rewardClaimed;
            CompletionCount = Math.Max(completed ? 1 : 0, completionCount);
        }

        public string LevelId { get; }
        public bool Completed { get; }
        public bool RewardClaimed { get; }
        public int CompletionCount { get; }
    }

    public readonly struct PlayerActionProfileSnapshot
    {
        private readonly PlayerCombatAction[] recentActions;

        public PlayerActionProfileSnapshot(PlayerActionProfileData data)
        {
            AttackCount = Math.Max(0, data.attackCount);
            GuardCount = Math.Max(0, data.guardCount);
            TechniqueCount = Math.Max(0, data.techniqueCount);
            AnalyzeCount = Math.Max(0, data.analyzeCount);
            TotalValidActions = AttackCount + GuardCount + TechniqueCount + AnalyzeCount;
            recentActions = data.recentActions == null
                ? Array.Empty<PlayerCombatAction>()
                : data.recentActions.ToArray();
        }

        public int AttackCount { get; }
        public int GuardCount { get; }
        public int TechniqueCount { get; }
        public int AnalyzeCount { get; }
        public int TotalValidActions { get; }
        public IReadOnlyList<PlayerCombatAction> RecentActions =>
            recentActions ?? Array.Empty<PlayerCombatAction>();
        public bool HasReliablePattern => TotalValidActions >= 3;

        public PlayerCombatAction? DominantAction
        {
            get
            {
                if (TotalValidActions == 0)
                {
                    return null;
                }

                PlayerCombatAction best = PlayerCombatAction.Attack;
                int bestCount = AttackCount;
                PlayerCombatAction[] candidates =
                {
                    PlayerCombatAction.Guard,
                    PlayerCombatAction.Technique,
                    PlayerCombatAction.Analyze
                };
                for (int index = 0; index < candidates.Length; index++)
                {
                    int count = GetCount(candidates[index]);
                    if (count > bestCount)
                    {
                        best = candidates[index];
                        bestCount = count;
                    }
                }

                return best;
            }
        }

        public int CurrentRepeatCount
        {
            get
            {
                if (recentActions == null || recentActions.Length == 0)
                {
                    return 0;
                }

                PlayerCombatAction latest = recentActions[recentActions.Length - 1];
                int count = 0;
                for (int index = recentActions.Length - 1; index >= 0; index--)
                {
                    if (recentActions[index] != latest)
                    {
                        break;
                    }

                    count++;
                }

                return count;
            }
        }

        public int GetCount(PlayerCombatAction action)
        {
            switch (action)
            {
                case PlayerCombatAction.Attack: return AttackCount;
                case PlayerCombatAction.Guard: return GuardCount;
                case PlayerCombatAction.Technique: return TechniqueCount;
                case PlayerCombatAction.Analyze: return AnalyzeCount;
                default: throw new ArgumentOutOfRangeException(nameof(action), action, null);
            }
        }

        public float GetUsageRatio(PlayerCombatAction action)
        {
            return TotalValidActions == 0 ? 0f : GetCount(action) / (float)TotalValidActions;
        }
    }
}
