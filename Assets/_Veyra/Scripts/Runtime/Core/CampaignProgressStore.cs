using System;
using System.Collections.Generic;
using UnityEngine;
using Veyra.Progression;

namespace Veyra.Core
{
    /// <summary>
    /// Versioned persistence for World01. Stable-id records are canonical; the
    /// scalar v1/v2 fields remain synchronized while existing scenes migrate.
    /// </summary>
    public static class CampaignProgressStore
    {
        public const string ProgressKey = "Veyra.Campaign.Progress";
        public const int CurrentVersion = 3;
        public const int PlayerActionHistoryCapacity = 20;

        public static CampaignProgressData Defaults
        {
            get
            {
                CampaignProgressData progress = new CampaignProgressData
                {
                    saveVersion = CurrentVersion,
                    version = CurrentVersion,
                    levelRecords = new List<CampaignLevelProgressRecord>(),
                    moralDecisions = new List<CampaignMoralDecisionRecord>(),
                    tutorialRecords = new List<CampaignTutorialRecord>(),
                    playerActionProfile = new PlayerActionProfileData
                    {
                        recentActions = new List<PlayerCombatAction>()
                    },
                    encounter02Resolution = EncounterResolution.None,
                    encounter03Resolution = EncounterResolution.None,
                    level04BruteResolution = EncounterResolution.None,
                    level04WatcherResolution = EncounterResolution.None,
                    level04MaskResolution = EncounterResolution.None
                };
                return Normalize(progress, out _);
            }
        }

        public static CampaignProgressData Load()
        {
            if (!PlayerPrefs.HasKey(ProgressKey))
            {
                return Defaults;
            }

            string json = PlayerPrefs.GetString(ProgressKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return Defaults;
            }

            try
            {
                CampaignProgressData loaded = JsonUtility.FromJson<CampaignProgressData>(json);
                CampaignProgressData normalized = Normalize(loaded, out bool changed);
                if (changed)
                {
                    WriteNormalized(normalized);
                }

                return normalized;
            }
            catch (ArgumentException exception)
            {
                Debug.LogWarning(
                    "[Veyra] Il salvataggio campagna non è leggibile e non è stato sovrascritto: " +
                    exception.Message);
                return Defaults;
            }
        }

        /// <summary>Pure, idempotent migration used by validators.</summary>
        public static CampaignProgressData Migrate(CampaignProgressData progress)
        {
            return Normalize(progress, out _);
        }

        [Obsolete("Completa il Tutorial registrando la decisione morale con SetTutorialResolution.")]
        public static void MarkTutorialCompleted()
        {
            CampaignProgressData progress = Load();
            if (IsLevelCompleted(CampaignContentIds.Level01Tutorial, progress))
            {
                return;
            }

            if (!TryGetEnemyResolution(
                    progress,
                    CampaignContentIds.Level01Tutorial,
                    CampaignContentIds.TutorialEnemy,
                    out EncounterResolution resolution))
            {
                Debug.LogWarning(
                    "[Veyra] Il Tutorial non può essere completato senza una decisione SALVA/UCCIDI.");
                return;
            }

            SetTutorialResolution(resolution);
        }

        /// <summary>
        /// Compatibility API: only the original first result is accepted. Replay
        /// changes must use SetEncounterResolution.
        /// </summary>
        public static void RecordEncounterResolution(
            CampaignEncounter encounter,
            EncounterResolution resolution)
        {
            ValidateFinalResolution(resolution, nameof(resolution));
            if (encounter == CampaignEncounter.ThreefoldAssault)
            {
                throw new InvalidOperationException(
                    "Il Livello 4 richiede tre decisioni: usa RecordLevel04Resolutions.");
            }

            GetEncounterIds(encounter, out string levelId, out string enemyId);
            CampaignProgressData progress = Load();
            if (IsLevelCompleted(levelId, progress))
            {
                return;
            }

            SetEnemyResolutionInData(ref progress, levelId, enemyId, resolution);
            CompleteLevelUnchecked(ref progress, levelId, true, true);
            Save(progress);
            AwardHeroReward(CampaignLevelCatalog.GetById(levelId));
        }

        public static void RecordLevel04Resolutions(
            EncounterResolution bruteResolution,
            EncounterResolution watcherResolution,
            EncounterResolution maskResolution)
        {
            ValidateFinalResolution(bruteResolution, nameof(bruteResolution));
            ValidateFinalResolution(watcherResolution, nameof(watcherResolution));
            ValidateFinalResolution(maskResolution, nameof(maskResolution));

            CampaignProgressData progress = Load();
            if (IsLevelCompleted(CampaignContentIds.Level04ThreefoldAssault, progress))
            {
                return;
            }

            SetEnemyResolutionInData(ref progress, CampaignContentIds.Level04ThreefoldAssault,
                CampaignContentIds.Level04BruteEnemy, bruteResolution);
            SetEnemyResolutionInData(ref progress, CampaignContentIds.Level04ThreefoldAssault,
                CampaignContentIds.Level04WatcherEnemy, watcherResolution);
            SetEnemyResolutionInData(ref progress, CampaignContentIds.Level04ThreefoldAssault,
                CampaignContentIds.Level04MaskEnemy, maskResolution);
            CompleteLevelUnchecked(
                ref progress, CampaignContentIds.Level04ThreefoldAssault, true, true);
            Save(progress);
            AwardHeroReward(CampaignLevelCatalog.GetByNumber(4));
        }

        public static bool SetTutorialResolution(EncounterResolution resolution)
        {
            return SetEnemyResolution(
                CampaignContentIds.Level01Tutorial,
                CampaignContentIds.TutorialEnemy,
                resolution);
        }

        public static bool SetEncounterResolution(
            CampaignEncounter encounter,
            EncounterResolution resolution)
        {
            ValidateFinalResolution(resolution, nameof(resolution));
            if (encounter == CampaignEncounter.ThreefoldAssault)
            {
                throw new InvalidOperationException(
                    "Il Livello 4 richiede un esito per ciascun nemico: usa SetEnemyResolution.");
            }

            GetEncounterIds(encounter, out string levelId, out string enemyId);
            return SetEnemyResolution(levelId, enemyId, resolution);
        }

        /// <summary>
        /// Adds or replaces a decision. Once all decisions required by the level
        /// exist, the first clear and its reward are committed atomically. A replay
        /// can replace the story result but can never claim the reward twice.
        /// </summary>
        public static bool SetEnemyResolution(
            string levelId,
            string enemyId,
            EncounterResolution resolution)
        {
            ValidateMoralTarget(levelId, enemyId, resolution);
            CampaignProgressData progress = Load();
            bool decisionChanged =
                SetEnemyResolutionInData(ref progress, levelId, enemyId, resolution);
            LevelDefinition definition = CampaignLevelCatalog.GetById(levelId);
            CampaignLevelProgressRecord levelRecord = GetOrCreateLevelRecord(ref progress, levelId);
            bool firstClear = !levelRecord.completed &&
                              HasAllRequiredMoralDecisions(progress, definition);
            bool rewardNeeded = firstClear && !levelRecord.rewardClaimed;
            if (firstClear)
            {
                CompleteLevelUnchecked(ref progress, levelId, true, true);
            }

            if (!decisionChanged && !firstClear)
            {
                return false;
            }

            Save(progress);
            if (rewardNeeded)
            {
                AwardHeroReward(definition);
            }

            return true;
        }

        public static bool TryGetEncounterResolution(
            CampaignEncounter encounter,
            out EncounterResolution resolution)
        {
            if (encounter == CampaignEncounter.ThreefoldAssault)
            {
                resolution = EncounterResolution.None;
                return false;
            }

            GetEncounterIds(encounter, out string levelId, out string enemyId);
            CampaignProgressData progress = Load();
            if (!IsLevelCompleted(levelId, progress))
            {
                resolution = EncounterResolution.None;
                return false;
            }

            return TryGetEnemyResolution(progress, levelId, enemyId, out resolution);
        }

        public static bool TryGetEnemyResolution(
            string levelId,
            string enemyId,
            out EncounterResolution resolution)
        {
            return TryGetEnemyResolution(Load(), levelId, enemyId, out resolution);
        }

        public static bool TryGetEnemyResolution(
            CampaignProgressData progress,
            string levelId,
            string enemyId,
            out EncounterResolution resolution)
        {
            progress = Migrate(progress);
            CampaignMoralDecisionRecord record = FindMoralDecision(progress, levelId, enemyId);
            if (record != null && IsFinalResolution(record.resolution))
            {
                resolution = record.resolution;
                return true;
            }

            resolution = EncounterResolution.None;
            return false;
        }

        public static bool TryCompleteLevel(
            string levelId,
            out bool firstClear,
            out string failureReason)
        {
            if (!CampaignLevelCatalog.TryGetById(levelId, out LevelDefinition definition) ||
                !definition.IsImplemented)
            {
                firstClear = false;
                failureReason = "Livello non disponibile.";
                return false;
            }

            CampaignProgressData progress = Load();
            if (!IsLevelUnlocked(definition.Number, progress))
            {
                firstClear = false;
                failureReason = "Completa prima il livello precedente.";
                return false;
            }

            if (definition.HasMoralChoice && !HasAllRequiredMoralDecisions(progress, definition))
            {
                firstClear = false;
                failureReason = "Completa e conferma tutte le decisioni morali.";
                return false;
            }

            CampaignLevelProgressRecord record = GetOrCreateLevelRecord(ref progress, levelId);
            firstClear = !record.completed;
            bool rewardNeeded = !record.rewardClaimed;
            CompleteLevelUnchecked(ref progress, levelId, true, true);
            Save(progress);
            if (rewardNeeded)
            {
                AwardHeroReward(definition);
            }

            failureReason = string.Empty;
            return true;
        }

        public static bool TryGetLevelProgress(
            string levelId,
            out CampaignLevelProgressSnapshot snapshot)
        {
            return TryGetLevelProgress(Load(), levelId, out snapshot);
        }

        public static bool TryGetLevelProgress(
            CampaignProgressData progress,
            string levelId,
            out CampaignLevelProgressSnapshot snapshot)
        {
            if (!CampaignLevelCatalog.TryGetById(levelId, out _))
            {
                snapshot = default;
                return false;
            }

            progress = Migrate(progress);
            CampaignLevelProgressRecord record = FindLevelRecord(progress, levelId);
            snapshot = record == null
                ? new CampaignLevelProgressSnapshot(levelId, false, false, 0)
                : new CampaignLevelProgressSnapshot(record.levelId, record.completed,
                    record.rewardClaimed, record.completionCount);
            return true;
        }

        public static bool IsLevelRewardClaimed(string levelId)
        {
            return IsLevelRewardClaimed(levelId, Load());
        }

        public static bool IsLevelRewardClaimed(int levelNumber)
        {
            return CampaignLevelCatalog.TryGetByNumber(levelNumber, out LevelDefinition definition) &&
                   IsLevelRewardClaimed(definition.StableId, Load());
        }

        public static bool IsLevelRewardClaimed(
            string levelId,
            CampaignProgressData progress)
        {
            progress = Migrate(progress);
            CampaignLevelProgressRecord record = FindLevelRecord(progress, levelId);
            return record != null && record.rewardClaimed;
        }

        public static bool TryClaimLevelReward(string levelId)
        {
            if (!CampaignLevelCatalog.TryGetById(levelId, out LevelDefinition definition) ||
                !definition.IsImplemented)
            {
                return false;
            }

            CampaignProgressData progress = Load();
            CampaignLevelProgressRecord record = FindLevelRecord(progress, levelId);
            if (record == null || !record.completed || record.rewardClaimed)
            {
                return false;
            }

            record.rewardClaimed = true;
            Save(progress);
            AwardHeroReward(definition);
            return true;
        }

        public static void MarkTutorialSeen(string tutorialId)
        {
            if (string.IsNullOrWhiteSpace(tutorialId))
            {
                throw new ArgumentException("Tutorial id cannot be empty.", nameof(tutorialId));
            }

            CampaignProgressData progress = Load();
            CampaignTutorialRecord record = FindTutorialRecord(progress, tutorialId);
            if (record != null && record.seen)
            {
                return;
            }

            if (record == null)
            {
                progress.tutorialRecords.Add(new CampaignTutorialRecord(tutorialId.Trim(), true));
            }
            else
            {
                record.seen = true;
            }

            Save(progress);
        }

        public static bool HasSeenTutorial(string tutorialId)
        {
            if (string.IsNullOrWhiteSpace(tutorialId))
            {
                return false;
            }

            CampaignTutorialRecord record = FindTutorialRecord(Load(), tutorialId);
            return record != null && record.seen;
        }

        public static void RecordPlayerAction(PlayerCombatAction action)
        {
            ValidatePlayerAction(action, nameof(action));
            CampaignProgressData progress = Load();
            PlayerActionProfileData profile = progress.playerActionProfile;
            profile.recentActions ??= new List<PlayerCombatAction>();

            switch (action)
            {
                case PlayerCombatAction.Attack: profile.attackCount++; break;
                case PlayerCombatAction.Guard: profile.guardCount++; break;
                case PlayerCombatAction.Technique: profile.techniqueCount++; break;
                case PlayerCombatAction.Analyze: profile.analyzeCount++; break;
            }

            profile.totalValidActions = profile.attackCount + profile.guardCount +
                                        profile.techniqueCount + profile.analyzeCount;
            profile.recentActions.Add(action);
            while (profile.recentActions.Count > PlayerActionHistoryCapacity)
            {
                profile.recentActions.RemoveAt(0);
            }

            progress.playerActionProfile = profile;
            Save(progress);
        }

        public static bool TryRecordPlayerAction(string actionName)
        {
            if (!Enum.TryParse(actionName, true, out PlayerCombatAction action) ||
                !IsValidPlayerAction(action))
            {
                return false;
            }

            RecordPlayerAction(action);
            return true;
        }

        public static PlayerActionProfileSnapshot GetPlayerActionProfile()
        {
            return new PlayerActionProfileSnapshot(Load().playerActionProfile);
        }

        public static bool CanEnemiesUsePlayerProfile(int campaignLevelNumber)
        {
            return campaignLevelNumber >= 3 && GetPlayerActionProfile().HasReliablePattern;
        }

        public static string GetNextSceneName()
        {
            return GetNextSceneName(Load());
        }

        public static string GetNextSceneName(CampaignProgressData progress)
        {
            progress = Migrate(progress);
            for (int index = 0; index < CampaignLevelCatalog.All.Count; index++)
            {
                LevelDefinition definition = CampaignLevelCatalog.All[index];
                if (!definition.IsImplemented)
                {
                    continue;
                }

                if (!IsLevelCompleted(definition.StableId, progress))
                {
                    return definition.SceneName;
                }
            }

            return SceneNames.MainMenu;
        }

        public static bool HasCompletedAllImplementedLevels(CampaignProgressData progress)
        {
            progress = Migrate(progress);
            bool foundImplementedLevel = false;
            for (int index = 0; index < CampaignLevelCatalog.All.Count; index++)
            {
                LevelDefinition definition = CampaignLevelCatalog.All[index];
                if (!definition.IsImplemented)
                {
                    continue;
                }

                foundImplementedLevel = true;
                if (!IsLevelCompleted(definition.StableId, progress))
                {
                    return false;
                }
            }

            return foundImplementedLevel;
        }

        public static bool IsEncounterUnlocked(
            CampaignEncounter encounter,
            CampaignProgressData progress)
        {
            return IsLevelUnlocked((int)encounter, progress);
        }

        public static bool IsLevelUnlocked(int levelNumber, CampaignProgressData progress)
        {
            if (!CampaignLevelCatalog.TryGetByNumber(levelNumber, out LevelDefinition definition) ||
                !definition.IsImplemented)
            {
                return false;
            }

            progress = Migrate(progress);
            return string.IsNullOrEmpty(definition.PrerequisiteLevelId) ||
                   IsLevelCompleted(definition.PrerequisiteLevelId, progress);
        }

        public static bool IsLevelCompleted(int levelNumber, CampaignProgressData progress)
        {
            return CampaignLevelCatalog.TryGetByNumber(levelNumber, out LevelDefinition definition) &&
                   IsLevelCompleted(definition.StableId, progress);
        }

        public static bool IsLevelCompleted(string levelId, CampaignProgressData progress)
        {
            progress = Migrate(progress);
            CampaignLevelProgressRecord record = FindLevelRecord(progress, levelId);
            return record != null && record.completed;
        }

        public static void Reset()
        {
            PlayerPrefs.DeleteKey(ProgressKey);
            HeroProgressStore.Reset();
            PlayerPrefs.Save();
        }

        private static void Save(CampaignProgressData progress)
        {
            WriteNormalized(Normalize(progress, out _));
        }

        private static void WriteNormalized(CampaignProgressData progress)
        {
            PlayerPrefs.SetString(ProgressKey, JsonUtility.ToJson(progress));
            PlayerPrefs.Save();
        }

        private static CampaignProgressData Normalize(
            CampaignProgressData source,
            out bool changed)
        {
            string before = JsonUtility.ToJson(source);
            int sourceVersion = Math.Max(source.saveVersion, source.version);
            CampaignProgressData progress = DeepClone(source);

            NormalizeLevelRecords(ref progress);
            NormalizeMoralDecisions(ref progress);
            NormalizeTutorialRecords(ref progress);
            NormalizePlayerActionProfile(ref progress);
            ApplyLegacyBridge(ref progress);
            EnforceCompletedPrerequisiteChain(ref progress);
            if (sourceVersion < CurrentVersion)
            {
                MarkMigratedRewardsAsClaimed(ref progress);
            }

            ApplyDerivedTutorialFlags(ref progress);
            progress.saveVersion = CurrentVersion;
            progress.version = CurrentVersion;
            SynchronizeLegacyFields(ref progress);
            string after = JsonUtility.ToJson(progress);
            changed = !string.Equals(before, after, StringComparison.Ordinal);
            return progress;
        }

        private static CampaignProgressData DeepClone(CampaignProgressData source)
        {
            CampaignProgressData clone = source;
            clone.levelRecords = new List<CampaignLevelProgressRecord>();
            if (source.levelRecords != null)
            {
                for (int index = 0; index < source.levelRecords.Count; index++)
                {
                    CampaignLevelProgressRecord record = source.levelRecords[index];
                    if (record != null)
                    {
                        clone.levelRecords.Add(new CampaignLevelProgressRecord(
                            record.levelId, record.completed, record.rewardClaimed,
                            record.completionCount));
                    }
                }
            }

            clone.moralDecisions = new List<CampaignMoralDecisionRecord>();
            if (source.moralDecisions != null)
            {
                for (int index = 0; index < source.moralDecisions.Count; index++)
                {
                    CampaignMoralDecisionRecord record = source.moralDecisions[index];
                    if (record != null)
                    {
                        clone.moralDecisions.Add(new CampaignMoralDecisionRecord(
                            record.levelId, record.enemyId, record.resolution));
                    }
                }
            }

            clone.tutorialRecords = new List<CampaignTutorialRecord>();
            if (source.tutorialRecords != null)
            {
                for (int index = 0; index < source.tutorialRecords.Count; index++)
                {
                    CampaignTutorialRecord record = source.tutorialRecords[index];
                    if (record != null)
                    {
                        clone.tutorialRecords.Add(
                            new CampaignTutorialRecord(record.tutorialId, record.seen));
                    }
                }
            }

            PlayerActionProfileData profile = source.playerActionProfile;
            profile.recentActions = source.playerActionProfile.recentActions == null
                ? new List<PlayerCombatAction>()
                : new List<PlayerCombatAction>(source.playerActionProfile.recentActions);
            clone.playerActionProfile = profile;
            return clone;
        }

        private static void NormalizeLevelRecords(ref CampaignProgressData progress)
        {
            List<CampaignLevelProgressRecord> normalized =
                new List<CampaignLevelProgressRecord>();
            Dictionary<string, CampaignLevelProgressRecord> byId =
                new Dictionary<string, CampaignLevelProgressRecord>(StringComparer.Ordinal);
            for (int index = 0; index < progress.levelRecords.Count; index++)
            {
                CampaignLevelProgressRecord record = progress.levelRecords[index];
                string id = record.levelId == null ? string.Empty : record.levelId.Trim();
                if (id.Length == 0)
                {
                    continue;
                }

                record.levelId = id;
                record.completed |= record.rewardClaimed;
                record.completionCount = Math.Max(record.completed ? 1 : 0, record.completionCount);
                if (byId.TryGetValue(id, out CampaignLevelProgressRecord existing))
                {
                    existing.completed |= record.completed;
                    existing.rewardClaimed |= record.rewardClaimed;
                    existing.completionCount = Math.Max(
                        existing.completionCount, record.completionCount);
                    continue;
                }

                byId.Add(id, record);
                normalized.Add(record);
            }

            for (int index = 0; index < CampaignLevelCatalog.All.Count; index++)
            {
                string id = CampaignLevelCatalog.All[index].StableId;
                if (!byId.ContainsKey(id))
                {
                    CampaignLevelProgressRecord record = new CampaignLevelProgressRecord(id);
                    byId.Add(id, record);
                    normalized.Add(record);
                }
            }

            progress.levelRecords = normalized;
        }

        private static void NormalizeMoralDecisions(ref CampaignProgressData progress)
        {
            List<CampaignMoralDecisionRecord> normalized =
                new List<CampaignMoralDecisionRecord>();
            Dictionary<string, CampaignMoralDecisionRecord> byKey =
                new Dictionary<string, CampaignMoralDecisionRecord>(StringComparer.Ordinal);
            for (int index = 0; index < progress.moralDecisions.Count; index++)
            {
                CampaignMoralDecisionRecord record = progress.moralDecisions[index];
                string levelId = record.levelId == null ? string.Empty : record.levelId.Trim();
                string enemyId = record.enemyId == null ? string.Empty : record.enemyId.Trim();
                if (levelId.Length == 0 || enemyId.Length == 0 ||
                    !IsFinalResolution(record.resolution))
                {
                    continue;
                }

                string key = BuildMoralKey(levelId, enemyId);
                if (byKey.TryGetValue(key, out CampaignMoralDecisionRecord existing))
                {
                    existing.resolution = record.resolution;
                    continue;
                }

                record.levelId = levelId;
                record.enemyId = enemyId;
                byKey.Add(key, record);
                normalized.Add(record);
            }

            progress.moralDecisions = normalized;
        }

        private static void NormalizeTutorialRecords(ref CampaignProgressData progress)
        {
            List<CampaignTutorialRecord> normalized = new List<CampaignTutorialRecord>();
            Dictionary<string, CampaignTutorialRecord> byId =
                new Dictionary<string, CampaignTutorialRecord>(StringComparer.Ordinal);
            for (int index = 0; index < progress.tutorialRecords.Count; index++)
            {
                CampaignTutorialRecord record = progress.tutorialRecords[index];
                string id = record.tutorialId == null ? string.Empty : record.tutorialId.Trim();
                if (id.Length == 0)
                {
                    continue;
                }

                if (byId.TryGetValue(id, out CampaignTutorialRecord existing))
                {
                    existing.seen |= record.seen;
                    continue;
                }

                record.tutorialId = id;
                byId.Add(id, record);
                normalized.Add(record);
            }

            progress.tutorialRecords = normalized;
        }

        private static void NormalizePlayerActionProfile(ref CampaignProgressData progress)
        {
            PlayerActionProfileData profile = progress.playerActionProfile;
            profile.attackCount = Math.Max(0, profile.attackCount);
            profile.guardCount = Math.Max(0, profile.guardCount);
            profile.techniqueCount = Math.Max(0, profile.techniqueCount);
            profile.analyzeCount = Math.Max(0, profile.analyzeCount);
            profile.totalValidActions = profile.attackCount + profile.guardCount +
                                        profile.techniqueCount + profile.analyzeCount;
            List<PlayerCombatAction> recent = new List<PlayerCombatAction>();
            if (profile.recentActions != null)
            {
                int start = Math.Max(0, profile.recentActions.Count - PlayerActionHistoryCapacity);
                for (int index = start; index < profile.recentActions.Count; index++)
                {
                    PlayerCombatAction action = profile.recentActions[index];
                    if (IsValidPlayerAction(action))
                    {
                        recent.Add(action);
                    }
                }
            }

            profile.recentActions = recent;
            progress.playerActionProfile = profile;
        }

        private static void ApplyLegacyBridge(ref CampaignProgressData progress)
        {
            if (progress.tutorialCompleted)
            {
                CompleteLevelUnchecked(
                    ref progress, CampaignContentIds.Level01Tutorial, true, false);
            }

            if (progress.encounter02Resolved && IsFinalResolution(progress.encounter02Resolution))
            {
                AddLegacyResolutionIfMissing(ref progress, CampaignContentIds.Level02ThornGuardian,
                    CampaignContentIds.ThornGuardianEnemy, progress.encounter02Resolution);
                CompleteLevelUnchecked(
                    ref progress, CampaignContentIds.Level02ThornGuardian, true, false);
            }

            if (progress.encounter03Resolved && IsFinalResolution(progress.encounter03Resolution))
            {
                AddLegacyResolutionIfMissing(ref progress, CampaignContentIds.Level03AshWatcher,
                    CampaignContentIds.AshWatcherEnemy, progress.encounter03Resolution);
                CompleteLevelUnchecked(
                    ref progress, CampaignContentIds.Level03AshWatcher, true, false);
            }

            if (progress.level04Completed &&
                IsFinalResolution(progress.level04BruteResolution) &&
                IsFinalResolution(progress.level04WatcherResolution) &&
                IsFinalResolution(progress.level04MaskResolution))
            {
                AddLegacyResolutionIfMissing(ref progress, CampaignContentIds.Level04ThreefoldAssault,
                    CampaignContentIds.Level04BruteEnemy, progress.level04BruteResolution);
                AddLegacyResolutionIfMissing(ref progress, CampaignContentIds.Level04ThreefoldAssault,
                    CampaignContentIds.Level04WatcherEnemy, progress.level04WatcherResolution);
                AddLegacyResolutionIfMissing(ref progress, CampaignContentIds.Level04ThreefoldAssault,
                    CampaignContentIds.Level04MaskEnemy, progress.level04MaskResolution);
                CompleteLevelUnchecked(
                    ref progress, CampaignContentIds.Level04ThreefoldAssault, true, false);
            }
        }

        private static void EnforceCompletedPrerequisiteChain(ref CampaignProgressData progress)
        {
            for (int number = CampaignLevelCatalog.ImplementedLevelCount; number >= 2; number--)
            {
                LevelDefinition definition = CampaignLevelCatalog.GetByNumber(number);
                if (!IsLevelCompletedWithoutNormalization(progress, definition.StableId))
                {
                    continue;
                }

                LevelDefinition prerequisite =
                    CampaignLevelCatalog.GetById(definition.PrerequisiteLevelId);
                CompleteLevelUnchecked(ref progress, prerequisite.StableId, true, false);
            }
        }

        private static void MarkMigratedRewardsAsClaimed(ref CampaignProgressData progress)
        {
            for (int index = 0; index < progress.levelRecords.Count; index++)
            {
                CampaignLevelProgressRecord record = progress.levelRecords[index];
                if (record.completed &&
                    CampaignLevelCatalog.TryGetById(record.levelId, out LevelDefinition definition) &&
                    definition.IsImplemented)
                {
                    record.rewardClaimed = true;
                }
            }
        }

        private static void ApplyDerivedTutorialFlags(ref CampaignProgressData progress)
        {
            if (IsLevelCompletedWithoutNormalization(progress, CampaignContentIds.Level01Tutorial))
            {
                SetTutorialSeen(ref progress, CampaignContentIds.TutorialCombatBasics);
            }

            if (FindMoralDecision(progress, CampaignContentIds.Level01Tutorial,
                    CampaignContentIds.TutorialEnemy) != null)
            {
                SetTutorialSeen(ref progress, CampaignContentIds.TutorialMoralChoice);
            }

            if (IsLevelCompletedWithoutNormalization(
                    progress, CampaignContentIds.Level04ThreefoldAssault))
            {
                SetTutorialSeen(ref progress, CampaignContentIds.TutorialMultiTarget);
            }
        }

        private static void SynchronizeLegacyFields(ref CampaignProgressData progress)
        {
            progress.tutorialCompleted = IsLevelCompletedWithoutNormalization(
                progress, CampaignContentIds.Level01Tutorial);
            progress.encounter02Resolved = IsLevelCompletedWithoutNormalization(
                progress, CampaignContentIds.Level02ThornGuardian);
            progress.encounter02Resolution = GetResolutionOrNone(progress,
                CampaignContentIds.Level02ThornGuardian, CampaignContentIds.ThornGuardianEnemy);
            progress.encounter03Resolved = IsLevelCompletedWithoutNormalization(
                progress, CampaignContentIds.Level03AshWatcher);
            progress.encounter03Resolution = GetResolutionOrNone(progress,
                CampaignContentIds.Level03AshWatcher, CampaignContentIds.AshWatcherEnemy);
            progress.level04Completed = IsLevelCompletedWithoutNormalization(
                progress, CampaignContentIds.Level04ThreefoldAssault);
            progress.level04BruteResolution = GetResolutionOrNone(progress,
                CampaignContentIds.Level04ThreefoldAssault, CampaignContentIds.Level04BruteEnemy);
            progress.level04WatcherResolution = GetResolutionOrNone(progress,
                CampaignContentIds.Level04ThreefoldAssault, CampaignContentIds.Level04WatcherEnemy);
            progress.level04MaskResolution = GetResolutionOrNone(progress,
                CampaignContentIds.Level04ThreefoldAssault, CampaignContentIds.Level04MaskEnemy);
        }

        private static void CompleteLevelUnchecked(
            ref CampaignProgressData progress,
            string levelId,
            bool claimReward,
            bool incrementCompletionCount)
        {
            CampaignLevelProgressRecord record = GetOrCreateLevelRecord(ref progress, levelId);
            bool wasCompleted = record.completed;
            record.completed = true;
            record.rewardClaimed |= claimReward;
            if (incrementCompletionCount)
            {
                record.completionCount = Math.Max(1, record.completionCount + 1);
            }
            else if (!wasCompleted)
            {
                record.completionCount = Math.Max(1, record.completionCount);
            }
        }

        private static bool SetEnemyResolutionInData(
            ref CampaignProgressData progress,
            string levelId,
            string enemyId,
            EncounterResolution resolution)
        {
            CampaignMoralDecisionRecord record = FindMoralDecision(progress, levelId, enemyId);
            if (record != null)
            {
                if (record.resolution == resolution)
                {
                    return false;
                }

                record.resolution = resolution;
                return true;
            }

            progress.moralDecisions.Add(
                new CampaignMoralDecisionRecord(levelId, enemyId, resolution));
            return true;
        }

        private static void AddLegacyResolutionIfMissing(
            ref CampaignProgressData progress,
            string levelId,
            string enemyId,
            EncounterResolution resolution)
        {
            if (FindMoralDecision(progress, levelId, enemyId) == null)
            {
                progress.moralDecisions.Add(
                    new CampaignMoralDecisionRecord(levelId, enemyId, resolution));
            }
        }

        private static void SetTutorialSeen(ref CampaignProgressData progress, string tutorialId)
        {
            CampaignTutorialRecord record = FindTutorialRecord(progress, tutorialId);
            if (record == null)
            {
                progress.tutorialRecords.Add(new CampaignTutorialRecord(tutorialId, true));
            }
            else
            {
                record.seen = true;
            }
        }

        private static bool HasAllRequiredMoralDecisions(
            CampaignProgressData progress,
            LevelDefinition definition)
        {
            for (int index = 0; index < definition.EnemyIds.Count; index++)
            {
                CampaignMoralDecisionRecord record = FindMoralDecision(
                    progress, definition.StableId, definition.EnemyIds[index]);
                if (record == null || !IsFinalResolution(record.resolution))
                {
                    return false;
                }
            }

            return true;
        }

        private static CampaignLevelProgressRecord GetOrCreateLevelRecord(
            ref CampaignProgressData progress,
            string levelId)
        {
            CampaignLevelProgressRecord record = FindLevelRecord(progress, levelId);
            if (record != null)
            {
                return record;
            }

            record = new CampaignLevelProgressRecord(levelId);
            progress.levelRecords.Add(record);
            return record;
        }

        private static CampaignLevelProgressRecord FindLevelRecord(
            CampaignProgressData progress,
            string levelId)
        {
            if (progress.levelRecords == null || string.IsNullOrWhiteSpace(levelId))
            {
                return null;
            }

            for (int index = 0; index < progress.levelRecords.Count; index++)
            {
                CampaignLevelProgressRecord record = progress.levelRecords[index];
                if (record != null && string.Equals(record.levelId, levelId, StringComparison.Ordinal))
                {
                    return record;
                }
            }

            return null;
        }

        private static CampaignMoralDecisionRecord FindMoralDecision(
            CampaignProgressData progress,
            string levelId,
            string enemyId)
        {
            if (progress.moralDecisions == null)
            {
                return null;
            }

            for (int index = 0; index < progress.moralDecisions.Count; index++)
            {
                CampaignMoralDecisionRecord record = progress.moralDecisions[index];
                if (record != null &&
                    string.Equals(record.levelId, levelId, StringComparison.Ordinal) &&
                    string.Equals(record.enemyId, enemyId, StringComparison.Ordinal))
                {
                    return record;
                }
            }

            return null;
        }

        private static CampaignTutorialRecord FindTutorialRecord(
            CampaignProgressData progress,
            string tutorialId)
        {
            if (progress.tutorialRecords == null)
            {
                return null;
            }

            for (int index = 0; index < progress.tutorialRecords.Count; index++)
            {
                CampaignTutorialRecord record = progress.tutorialRecords[index];
                if (record != null &&
                    string.Equals(record.tutorialId, tutorialId, StringComparison.Ordinal))
                {
                    return record;
                }
            }

            return null;
        }

        private static bool IsLevelCompletedWithoutNormalization(
            CampaignProgressData progress,
            string levelId)
        {
            CampaignLevelProgressRecord record = FindLevelRecord(progress, levelId);
            return record != null && record.completed;
        }

        private static EncounterResolution GetResolutionOrNone(
            CampaignProgressData progress,
            string levelId,
            string enemyId)
        {
            CampaignMoralDecisionRecord record = FindMoralDecision(progress, levelId, enemyId);
            return record != null && IsFinalResolution(record.resolution)
                ? record.resolution
                : EncounterResolution.None;
        }

        private static void ValidateMoralTarget(
            string levelId,
            string enemyId,
            EncounterResolution resolution)
        {
            ValidateFinalResolution(resolution, nameof(resolution));
            if (!CampaignLevelCatalog.TryGetById(levelId, out LevelDefinition definition) ||
                !definition.IsImplemented || !definition.HasMoralChoice)
            {
                throw new ArgumentOutOfRangeException(nameof(levelId), levelId,
                    "Livello morale non valido.");
            }

            if (!CampaignLevelCatalog.IsKnownEnemy(levelId, enemyId))
            {
                throw new ArgumentOutOfRangeException(nameof(enemyId), enemyId,
                    "Nemico non valido per il livello.");
            }
        }

        private static void GetEncounterIds(
            CampaignEncounter encounter,
            out string levelId,
            out string enemyId)
        {
            switch (encounter)
            {
                case CampaignEncounter.ThornGuardian:
                    levelId = CampaignContentIds.Level02ThornGuardian;
                    enemyId = CampaignContentIds.ThornGuardianEnemy;
                    return;
                case CampaignEncounter.AshWatcher:
                    levelId = CampaignContentIds.Level03AshWatcher;
                    enemyId = CampaignContentIds.AshWatcherEnemy;
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(encounter), encounter,
                        "Incontro a esito singolo non riconosciuto.");
            }
        }

        private static void AwardHeroReward(LevelDefinition definition)
        {
            switch (definition.Number)
            {
                case 1: HeroProgressStore.RecordFirstClear(CampaignLevel.Tutorial); break;
                case 2: HeroProgressStore.RecordFirstClear(CampaignLevel.ThornGuardian); break;
                case 3: HeroProgressStore.RecordFirstClear(CampaignLevel.AshWatcher); break;
                case 4: HeroProgressStore.RecordFirstClear(CampaignLevel.ThreefoldAssault); break;
            }
        }

        private static void ValidateFinalResolution(
            EncounterResolution resolution,
            string parameterName)
        {
            if (!IsFinalResolution(resolution))
            {
                throw new ArgumentOutOfRangeException(parameterName, resolution,
                    "La risoluzione deve essere Saved oppure Killed.");
            }
        }

        private static bool IsFinalResolution(EncounterResolution resolution)
        {
            return resolution == EncounterResolution.Saved ||
                   resolution == EncounterResolution.Killed;
        }

        private static void ValidatePlayerAction(PlayerCombatAction action, string parameterName)
        {
            if (!IsValidPlayerAction(action))
            {
                throw new ArgumentOutOfRangeException(parameterName, action,
                    "Azione non riconosciuta.");
            }
        }

        private static bool IsValidPlayerAction(PlayerCombatAction action)
        {
            return action == PlayerCombatAction.Attack ||
                   action == PlayerCombatAction.Guard ||
                   action == PlayerCombatAction.Technique ||
                   action == PlayerCombatAction.Analyze;
        }

        private static string BuildMoralKey(string levelId, string enemyId)
        {
            return levelId + "\n" + enemyId;
        }
    }
}
