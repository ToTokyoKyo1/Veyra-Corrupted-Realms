using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Veyra.Core;
using Veyra.UI.Battle;

namespace Veyra.Combat.MultiEnemy
{
    public sealed class MultiEnemyBattleNavigation : MonoBehaviour
    {
        [SerializeField] private Button backButton;
        [SerializeField] private Button resultMenuButton;
        [SerializeField] private Button retryButton;
        [SerializeField] private MultiEnemyBattleController battleController;
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
                !battleController.ShowingVictoryOutcome)
            {
                pauseController.Open();
                pauseController.RequestMainMenu();
                return;
            }

            BeginLoad(SceneNames.MainMenu);
        }

        public void RetryCurrentLevel()
        {
            string currentSceneName = SceneManager.GetActiveScene().name;
            if (string.IsNullOrWhiteSpace(currentSceneName))
            {
                ShowLoadError("La scena corrente non ha un nome valido.");
                return;
            }

            BeginLoad(currentSceneName);
        }

        public void OpenLevelSelection()
        {
            MainMenuEntryRequest.Request(MainMenuEntryPoint.Levels);
            BeginLoad(SceneNames.MainMenu);
        }

        private void BeginLoad(string sceneName)
        {
            if (isLoading)
            {
                return;
            }

            isLoading = true;
            StartCoroutine(LoadScene(sceneName));
        }

        private IEnumerator LoadScene(string sceneName)
        {
            if (backButton != null) backButton.interactable = false;
            if (resultMenuButton != null) resultMenuButton.interactable = false;
            if (retryButton != null) retryButton.interactable = false;

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
                ShowLoadError("Unity non ha avviato il caricamento della scena.");
                yield break;
            }

            if (battleController != null) battleController.CancelForSceneChange();

            while (!operation.isDone) yield return null;
        }

        private void ShowLoadError(string details)
        {
            isLoading = false;
            if (backButton != null) backButton.interactable = true;
            if (resultMenuButton != null) resultMenuButton.interactable = true;
            if (retryButton != null) retryButton.interactable = true;
            if (battleController != null)
            {
                battleController.ShowExternalMessage("Caricamento scena non riuscito: " + details);
            }
        }
    }
}
