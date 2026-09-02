#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Veyra.Core;

namespace Veyra.Editor.Tests
{
    /// <summary>
    /// Pure editor validation for the campaign schema. It never reads, clears or
    /// writes PlayerPrefs, so running it cannot alter the player's real save.
    /// </summary>
    public static class CampaignCoreValidator
    {
        [MenuItem("Veyra/Validate Campaign Core")]
        public static void ValidateCampaignCore()
        {
            List<string> errors = CollectErrors();
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Campaign Core validation failed:\n- " + string.Join("\n- ", errors));
            }

            Debug.Log(
                "[Veyra Campaign Core] SUPERATA — catalogo 1-10, migrazione v2->v3, " +
                "idempotenza, replay morale e profilo azioni limitato sono conformi.");
        }

        public static List<string> CollectErrors()
        {
            List<string> errors = new List<string>();
            ValidateCatalog(errors);
            ValidateLegacyMigration(errors);
            ValidateGenericDecisionAuthority(errors);
            ValidateActionProfile(errors);
            return errors;
        }

        private static void ValidateCatalog(ICollection<string> errors)
        {
            Require(CampaignLevelCatalog.All.Count == 10,
                "Il catalogo non contiene esattamente dieci slot.", errors);
            Require(CampaignLevelCatalog.ImplementedLevelCount == 4,
                "Il catalogo non dichiara esattamente quattro livelli implementati.", errors);

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < CampaignLevelCatalog.All.Count; index++)
            {
                LevelDefinition definition = CampaignLevelCatalog.All[index];
                Require(ids.Add(definition.StableId),
                    "ID livello duplicato: " + definition.StableId, errors);
                Require(definition.Number == index + 1,
                    "Ordine/numero livello incoerente per " + definition.StableId, errors);
                Require(definition.IsImplemented == (definition.Number <= 4),
                    "Stato Implementato incoerente per il livello " + definition.Number, errors);
                if (definition.Number >= 5)
                {
                    Require(string.IsNullOrEmpty(definition.SceneName) &&
                            definition.EnemyIds.Count == 0,
                        "Un placeholder L5-L10 contiene scena o nemici speculativi.", errors);
                }
            }

            CampaignProgressData fresh = CampaignProgressStore.Defaults;
            for (int number = 5; number <= 10; number++)
            {
                Require(!CampaignProgressStore.IsLevelUnlocked(number, fresh),
                    "Il placeholder " + number + " risulta sbloccato.", errors);
            }
        }

        private static void ValidateLegacyMigration(ICollection<string> errors)
        {
            CampaignProgressData legacy = new CampaignProgressData
            {
                version = 2,
                tutorialCompleted = true,
                encounter02Resolved = true,
                encounter02Resolution = EncounterResolution.Saved,
                encounter03Resolved = true,
                encounter03Resolution = EncounterResolution.Killed,
                level04Completed = true,
                level04BruteResolution = EncounterResolution.Saved,
                level04WatcherResolution = EncounterResolution.Killed,
                level04MaskResolution = EncounterResolution.Saved
            };

            CampaignProgressData migrated = CampaignProgressStore.Migrate(legacy);
            Require(migrated.saveVersion == CampaignProgressStore.CurrentVersion &&
                    migrated.version == CampaignProgressStore.CurrentVersion,
                "La migrazione non imposta entrambe le versioni correnti.", errors);
            Require(migrated.CompletedLevelCount == 4,
                "La migrazione non conserva i quattro completamenti legacy.", errors);
            Require(CampaignProgressStore.IsLevelRewardClaimed(
                    CampaignContentIds.Level04ThreefoldAssault, migrated),
                "La migrazione non conserva la ricompensa già guadagnata.", errors);
            Require(CampaignProgressStore.TryGetEnemyResolution(migrated,
                    CampaignContentIds.Level02ThornGuardian,
                    CampaignContentIds.ThornGuardianEnemy,
                    out EncounterResolution thorn) && thorn == EncounterResolution.Saved,
                "La migrazione perde l'esito SALVATO del Livello 2.", errors);
            Require(CampaignProgressStore.TryGetEnemyResolution(migrated,
                    CampaignContentIds.Level04ThreefoldAssault,
                    CampaignContentIds.Level04WatcherEnemy,
                    out EncounterResolution watcher) && watcher == EncounterResolution.Killed,
                "La migrazione perde un esito individuale del Livello 4.", errors);

            string once = JsonUtility.ToJson(migrated);
            string twice = JsonUtility.ToJson(CampaignProgressStore.Migrate(migrated));
            Require(string.Equals(once, twice, StringComparison.Ordinal),
                "La migrazione non è idempotente.", errors);
        }

        private static void ValidateGenericDecisionAuthority(ICollection<string> errors)
        {
            CampaignProgressData data = CampaignProgressStore.Defaults;
            data.encounter02Resolved = true;
            data.encounter02Resolution = EncounterResolution.Saved;
            data.levelRecords.Add(new CampaignLevelProgressRecord(
                CampaignContentIds.Level02ThornGuardian, true, true, 1));
            data.moralDecisions.Add(new CampaignMoralDecisionRecord(
                CampaignContentIds.Level02ThornGuardian,
                CampaignContentIds.ThornGuardianEnemy,
                EncounterResolution.Killed));

            CampaignProgressData normalized = CampaignProgressStore.Migrate(data);
            Require(normalized.encounter02Resolution == EncounterResolution.Killed,
                "Un campo legacy obsoleto sovrascrive la decisione generica del replay.", errors);
        }

        private static void ValidateActionProfile(ICollection<string> errors)
        {
            CampaignProgressData data = CampaignProgressStore.Defaults;
            data.playerActionProfile = new PlayerActionProfileData
            {
                attackCount = 12,
                guardCount = 7,
                techniqueCount = 4,
                analyzeCount = 2,
                totalValidActions = -99,
                recentActions = new List<PlayerCombatAction>()
            };
            for (int index = 0; index < 25; index++)
            {
                data.playerActionProfile.recentActions.Add(PlayerCombatAction.Attack);
            }

            CampaignProgressData normalized = CampaignProgressStore.Migrate(data);
            PlayerActionProfileSnapshot snapshot =
                new PlayerActionProfileSnapshot(normalized.playerActionProfile);
            Require(snapshot.TotalValidActions == 25,
                "Il totale delle azioni non viene ricalcolato dai contatori.", errors);
            Require(snapshot.RecentActions.Count == CampaignProgressStore.PlayerActionHistoryCapacity,
                "La cronologia azioni non è limitata a venti elementi.", errors);
            Require(snapshot.DominantAction == PlayerCombatAction.Attack &&
                    snapshot.CurrentRepeatCount == CampaignProgressStore.PlayerActionHistoryCapacity,
                "Pattern dominante o ripetizione corrente errati.", errors);
        }

        private static void Require(bool condition, string message, ICollection<string> errors)
        {
            if (!condition)
            {
                errors.Add(message);
            }
        }
    }
}
#endif
