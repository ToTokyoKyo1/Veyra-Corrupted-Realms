using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Veyra.Core;
using Veyra.UI.Battle;

namespace Veyra.Combat.Tutorial
{
    public sealed class TutorialBattleNavigation : MonoBehaviour
    {
        [SerializeField] private Button backButton;
        [SerializeField] private Button resultMenuButton;
        [SerializeField] private Button continueLevelButton;
        [SerializeField] private Button retryButton;
        [SerializeField] private TutorialBattleController battleController;
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
            if (!isLoading && pauseController != null &&
                battleController != null && battleController.Outcome == BattleOutcome.Ongoing)
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

        public void ContinueToLevel02()
        {
            if (!isLoading)
            {
                LevelDefinition tutorial =
                    CampaignLevelCatalog.GetById(CampaignContentIds.Level01Tutorial);
                LevelDefinition level02 = CampaignLevelCatalog.GetById(tutorial.NextLevelId);
                StartCoroutine(LoadScene(level02.SceneName));
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
            if (backButton != null)
            {
                backButton.interactable = false;
            }

            if (resultMenuButton != null)
            {
                resultMenuButton.interactable = false;
            }

            if (continueLevelButton != null)
            {
                continueLevelButton.interactable = false;
            }

            if (retryButton != null)
            {
                retryButton.interactable = false;
            }

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

        private void ShowLoadError(string details)
        {
            isLoading = false;
            if (backButton != null)
            {
                backButton.interactable = true;
            }

            if (resultMenuButton != null)
            {
                resultMenuButton.interactable = true;
            }

            if (continueLevelButton != null)
            {
                continueLevelButton.interactable = true;
            }

            if (retryButton != null)
            {
                retryButton.interactable = true;
            }

            battleController.ShowExternalMessage("Caricamento scena non riuscito: " + details);
        }
    }
}
