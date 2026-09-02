using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Veyra.Core;
using Veyra.UI.Battle;

namespace Veyra.Combat.Encounter
{
    public sealed class EncounterBattleNavigation : MonoBehaviour
    {
        [SerializeField] private Button backButton;
        [SerializeField] private Button resultMenuButton;
        [SerializeField] private Button continueLevelButton;
        [SerializeField] private Button retryButton;
        [SerializeField] private EncounterBattleController battleController;
        [SerializeField] private BattlePauseController pauseController;

        private bool isLoading;

        public bool IsLoading => isLoading;

        private void Update()
        {
            bool pausePressed = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                                (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
            if (!isLoading && pausePressed)
            {
                if (pauseController != null) pauseController.TogglePause();
            }
        }

        public void BackToMenu()
        {
            if (!isLoading && pauseController != null && battleController != null &&
                battleController.Resolution == NarrativeOutcome.None)
            {
                pauseController.Open();
                pauseController.RequestMainMenu();
                return;
            }

            if (!isLoading)
            {
                StartCoroutine(LoadScene(SceneNames.MainMenu));
            }
        }

        public void ContinueCampaign()
        {
            if (isLoading)
            {
                return;
            }

            string activeSceneName = SceneManager.GetActiveScene().name;
            LevelDefinition currentLevel = null;
            for (int index = 0; index < CampaignLevelCatalog.All.Count; index++)
            {
                LevelDefinition candidate = CampaignLevelCatalog.All[index];
                if (candidate.SceneName == activeSceneName)
                {
                    currentLevel = candidate;
                    break;
                }
            }

            if (currentLevel == null || string.IsNullOrEmpty(currentLevel.NextLevelId) ||
                !CampaignLevelCatalog.TryGetById(currentLevel.NextLevelId, out LevelDefinition nextLevel) ||
                !nextLevel.IsImplemented)
            {
                ShowLoadError("Non esiste un livello successivo disponibile.");
                return;
            }

            StartCoroutine(LoadScene(nextLevel.SceneName));
        }

        public void GoToHeroes()
        {
            if (!isLoading)
            {
                MainMenuEntryRequest.Request(MainMenuEntryPoint.Heroes);
                StartCoroutine(LoadScene(SceneNames.MainMenu));
            }
        }

        public void RetryCurrentLevel()
        {
            if (!isLoading)
            {
                StartCoroutine(LoadScene(SceneManager.GetActiveScene().name));
            }
        }

        private IEnumerator LoadScene(string sceneName)
        {
            isLoading = true;
            SetNavigationButtons(false);
            battleController.CancelRunningActionForSceneChange();

            AsyncOperation operation;
            try
            {
                operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            }
            catch (Exception exception)
            {
                ShowLoadError(exception.Message);
                yield break;
            }

            if (operation == null)
            {
                ShowLoadError("Unity non ha avviato il caricamento di " + sceneName + ".");
                yield break;
            }

            while (!operation.isDone)
            {
                yield return null;
            }
        }

        private void SetNavigationButtons(bool interactable)
        {
            if (backButton != null)
            {
                backButton.interactable = interactable;
            }

            if (resultMenuButton != null)
            {
                resultMenuButton.interactable = interactable;
            }

            if (continueLevelButton != null)
            {
                continueLevelButton.interactable = interactable;
            }

            if (retryButton != null)
            {
                retryButton.interactable = interactable;
            }
        }

        private void ShowLoadError(string details)
        {
            isLoading = false;
            MainMenuEntryRequest.Request(MainMenuEntryPoint.Main);
            SetNavigationButtons(true);

            if (battleController != null)
            {
                battleController.ShowExternalMessage(
                    "Caricamento scena non riuscito: " + details);
            }
        }
    }
}
