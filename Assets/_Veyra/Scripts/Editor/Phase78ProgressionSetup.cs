#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Veyra.Editor
{
    public static class Phase78ProgressionSetup
    {
        private const string MenuPath =
            "Tools/Veyra/Progression/Create Menu, Hero Progress and Level 04";

        [MenuItem(MenuPath, priority = 500)]
        public static void CreateMenuHeroProgressAndLevel04()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("La generazione richiede Unity in Edit Mode.");
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            try
            {
                Phase046CampaignSetupReport legacyReport = new Phase046CampaignSetupReport();
                Phase046EncounterSceneFactory.CreateOrUpdateCampaign(legacyReport);
                Phase78ProgressionSceneFactory.CreateOrUpdateMenuAndLevel04();
                Phase78ExistingSceneUpgrade.UpgradeTutorialAndEncounters();
                Phase78ProgressionSceneFactory.ConfigureBuildSettingsAndOpenLevel04();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Debug.Log(
                    "[Veyra Progression] Menu, progressione Hero01, conseguenze persistenti e Livello 4 generati senza duplicati.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError(
                    "[Veyra Progression] Generazione interrotta. Sono stati toccati soltanto i root di proprietà della progressione.");
                throw;
            }
        }
    }
}
#endif
