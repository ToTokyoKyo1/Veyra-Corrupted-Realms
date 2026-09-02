using System;
using UnityEditor;
using UnityEngine;
using Veyra.Core;
using Veyra.Progression;

namespace Veyra.Editor
{
    /// <summary>
    /// Protects the player's real PlayerPrefs while an explicit manual QA run is
    /// performed in Play Mode. Backups live in EditorPrefs so an Editor restart
    /// can still recover them. This tool is never invoked by runtime code.
    /// </summary>
    [InitializeOnLoad]
    internal static class IsolatedProgressTestSession
    {
        private const string MenuRoot = "Tools/Veyra/QA/";
        private const string BackupPrefix = "Veyra.QA.ProgressBackup.";
        private const string ActiveSuffix = ".Active";
        private const string CampaignExistsSuffix = ".Campaign.Exists";
        private const string CampaignValueSuffix = ".Campaign.Value";
        private const string HeroExistsSuffix = ".Hero.Exists";
        private const string HeroValueSuffix = ".Hero.Value";

        private static readonly string ProjectKey = BuildProjectKey();

        static IsolatedProgressTestSession()
        {
            EditorApplication.delayCall += RecoverInterruptedSessionIfNeeded;
        }

        [MenuItem(MenuRoot + "Begin Isolated Progress Session", priority = 600)]
        private static void Begin()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Esci dal Play Mode prima di iniziare il test isolato.");
            }

            if (IsActive)
            {
                throw new InvalidOperationException("Esiste già una sessione QA isolata attiva.");
            }

            BackupKey(CampaignProgressStore.ProgressKey, CampaignExistsSuffix, CampaignValueSuffix);
            BackupKey(HeroProgressStore.ProgressKey, HeroExistsSuffix, HeroValueSuffix);
            EditorPrefs.SetBool(Key(ActiveSuffix), true);

            PlayerPrefs.DeleteKey(CampaignProgressStore.ProgressKey);
            PlayerPrefs.DeleteKey(HeroProgressStore.ProgressKey);
            PlayerPrefs.Save();
            Debug.Log("[Veyra QA] Sessione progressi isolata avviata. Il salvataggio reale è protetto.");
        }

        [MenuItem(MenuRoot + "Begin Isolated Progress Session", true)]
        private static bool CanBegin()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode && !IsActive;
        }

        [MenuItem(MenuRoot + "Restore Real Progress", priority = 601)]
        private static void Restore()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Esci dal Play Mode prima di ripristinare il salvataggio.");
            }

            RestoreInternal(true);
        }

        [MenuItem(MenuRoot + "Restore Real Progress", true)]
        private static bool CanRestore()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode && IsActive;
        }

        private static bool IsActive => EditorPrefs.GetBool(Key(ActiveSuffix), false);

        private static void RecoverInterruptedSessionIfNeeded()
        {
            if (!IsActive || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            RestoreInternal(false);
            Debug.LogWarning(
                "[Veyra QA] Una sessione isolata interrotta è stata rilevata: salvataggio reale ripristinato automaticamente.");
        }

        private static void RestoreInternal(bool logSuccess)
        {
            if (!IsActive)
            {
                return;
            }

            RestoreKey(CampaignProgressStore.ProgressKey, CampaignExistsSuffix, CampaignValueSuffix);
            RestoreKey(HeroProgressStore.ProgressKey, HeroExistsSuffix, HeroValueSuffix);
            PlayerPrefs.Save();
            ClearBackup();
            if (logSuccess)
            {
                Debug.Log("[Veyra QA] Salvataggio reale ripristinato correttamente.");
            }
        }

        private static void BackupKey(string playerPrefsKey, string existsSuffix, string valueSuffix)
        {
            bool exists = PlayerPrefs.HasKey(playerPrefsKey);
            EditorPrefs.SetBool(Key(existsSuffix), exists);
            EditorPrefs.SetString(
                Key(valueSuffix),
                exists ? PlayerPrefs.GetString(playerPrefsKey, string.Empty) : string.Empty);
        }

        private static void RestoreKey(string playerPrefsKey, string existsSuffix, string valueSuffix)
        {
            if (EditorPrefs.GetBool(Key(existsSuffix), false))
            {
                PlayerPrefs.SetString(playerPrefsKey, EditorPrefs.GetString(Key(valueSuffix), string.Empty));
            }
            else
            {
                PlayerPrefs.DeleteKey(playerPrefsKey);
            }
        }

        private static void ClearBackup()
        {
            EditorPrefs.DeleteKey(Key(ActiveSuffix));
            EditorPrefs.DeleteKey(Key(CampaignExistsSuffix));
            EditorPrefs.DeleteKey(Key(CampaignValueSuffix));
            EditorPrefs.DeleteKey(Key(HeroExistsSuffix));
            EditorPrefs.DeleteKey(Key(HeroValueSuffix));
        }

        private static string Key(string suffix)
        {
            return BackupPrefix + ProjectKey + suffix;
        }

        private static string BuildProjectKey()
        {
            string path = Application.dataPath.Replace('\\', '/').ToUpperInvariant();
            unchecked
            {
                uint hash = 2166136261;
                for (int index = 0; index < path.Length; index++)
                {
                    hash = (hash ^ path[index]) * 16777619;
                }

                return hash.ToString("X8");
            }
        }
    }
}
