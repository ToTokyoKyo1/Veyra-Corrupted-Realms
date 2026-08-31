using System;
using UnityEngine;

namespace Veyra.Core
{
    public enum CampaignEncounter
    {
        ThornGuardian = 2,
        AshWatcher = 3
    }

    public enum EncounterResolution
    {
        None = 0,
        Saved = 1,
        Killed = 2
    }

    [Serializable]
    public struct CampaignProgressData
    {
        public int version;
        public bool tutorialCompleted;
        public bool encounter02Resolved;
        public EncounterResolution encounter02Resolution;
        public bool encounter03Resolved;
        public EncounterResolution encounter03Resolution;

        public bool HasAnyProgress =>
            tutorialCompleted || encounter02Resolved || encounter03Resolved;
    }

    public static class CampaignProgressStore
    {
        public const string ProgressKey = "Veyra.Campaign.Progress";
        public const int CurrentVersion = 1;

        public static CampaignProgressData Defaults => new CampaignProgressData
        {
            version = CurrentVersion,
            tutorialCompleted = false,
            encounter02Resolved = false,
            encounter02Resolution = EncounterResolution.None,
            encounter03Resolved = false,
            encounter03Resolution = EncounterResolution.None
        };

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
                return Normalize(loaded);
            }
            catch (ArgumentException)
            {
                return Defaults;
            }
        }

        public static void MarkTutorialCompleted()
        {
            CampaignProgressData progress = Load();
            if (progress.tutorialCompleted)
            {
                return;
            }

            progress.tutorialCompleted = true;
            Save(progress);
        }

        public static void RecordEncounterResolution(
            CampaignEncounter encounter,
            EncounterResolution resolution)
        {
            if (!IsFinalResolution(resolution))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(resolution),
                    resolution,
                    "La risoluzione deve essere Saved oppure Killed.");
            }

            CampaignProgressData progress = Load();
            switch (encounter)
            {
                case CampaignEncounter.ThornGuardian:
                    progress.tutorialCompleted = true;
                    progress.encounter02Resolved = true;
                    progress.encounter02Resolution = resolution;
                    break;
                case CampaignEncounter.AshWatcher:
                    progress.encounter03Resolved = true;
                    progress.encounter03Resolution = resolution;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(encounter),
                        encounter,
                        "Incontro della campagna non riconosciuto.");
            }

            Save(progress);
        }

        public static string GetNextSceneName()
        {
            return GetNextSceneName(Load());
        }

        public static string GetNextSceneName(CampaignProgressData progress)
        {
            progress = Normalize(progress);

            if (!progress.tutorialCompleted)
            {
                return SceneNames.World01Level01Tutorial;
            }

            if (!progress.encounter02Resolved)
            {
                return SceneNames.World01Level02ThornGuardian;
            }

            return SceneNames.World01Level03AshWatcher;
        }

        public static bool IsEncounterUnlocked(
            CampaignEncounter encounter,
            CampaignProgressData progress)
        {
            progress = Normalize(progress);
            switch (encounter)
            {
                case CampaignEncounter.ThornGuardian:
                    return progress.tutorialCompleted;
                case CampaignEncounter.AshWatcher:
                    return progress.encounter02Resolved;
                default:
                    return false;
            }
        }

        public static void Reset()
        {
            PlayerPrefs.DeleteKey(ProgressKey);
            PlayerPrefs.Save();
        }

        private static void Save(CampaignProgressData progress)
        {
            CampaignProgressData normalized = Normalize(progress);
            PlayerPrefs.SetString(ProgressKey, JsonUtility.ToJson(normalized));
            PlayerPrefs.Save();
        }

        private static CampaignProgressData Normalize(CampaignProgressData progress)
        {
            progress.version = CurrentVersion;

            if (!progress.encounter02Resolved || !IsFinalResolution(progress.encounter02Resolution))
            {
                progress.encounter02Resolved = false;
                progress.encounter02Resolution = EncounterResolution.None;
            }

            if (!progress.encounter03Resolved || !IsFinalResolution(progress.encounter03Resolution))
            {
                progress.encounter03Resolved = false;
                progress.encounter03Resolution = EncounterResolution.None;
            }

            return progress;
        }

        private static bool IsFinalResolution(EncounterResolution resolution)
        {
            return resolution == EncounterResolution.Saved ||
                   resolution == EncounterResolution.Killed;
        }
    }
}
