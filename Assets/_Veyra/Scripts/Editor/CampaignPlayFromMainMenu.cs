using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Veyra.Core;

namespace Veyra.EditorTools
{
    [InitializeOnLoad]
    public static class CampaignPlayFromMainMenu
    {
        private const string MainMenuPath = "Assets/_Veyra/Scenes/SCN_MainMenu.unity";
        private const string PlayCurrentSceneOnceKey = "Veyra.PlayCurrentSceneOnce";

        static CampaignPlayFromMainMenu()
        {
            ApplyDefaultStartScene();
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        [MenuItem("Tools/Veyra/Play From Main Menu", priority = 1)]
        public static void Play()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Veyra] Il Play Mode è già attivo o in avvio.");
                return;
            }

            SceneAsset mainMenu = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuPath);
            if (mainMenu == null)
            {
                Debug.LogError("[Veyra] Scena menu non trovata: " + MainMenuPath);
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EditorSceneManager.playModeStartScene = mainMenu;
            EditorApplication.EnterPlaymode();
        }

        [MenuItem("Tools/Veyra/Development/Play Current Scene Once", priority = 50)]
        public static void PlayCurrentSceneOnce()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Veyra] Il Play Mode è già attivo o in avvio.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            SessionState.SetBool(PlayCurrentSceneOnceKey, true);
            EditorSceneManager.playModeStartScene = null;
            EditorApplication.EnterPlaymode();
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode)
            {
                return;
            }

            if (SessionState.GetBool(PlayCurrentSceneOnceKey, false))
            {
                SessionState.EraseBool(PlayCurrentSceneOnceKey);
            }

            ApplyDefaultStartScene();
        }

        private static void ApplyDefaultStartScene()
        {
            SceneAsset mainMenu = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuPath);
            if (mainMenu == null)
            {
                Debug.LogError("[Veyra] Impossibile impostare l'avvio: scena menu non trovata: " + MainMenuPath);
                return;
            }

            EditorSceneManager.playModeStartScene = mainMenu;
        }
    }
}
