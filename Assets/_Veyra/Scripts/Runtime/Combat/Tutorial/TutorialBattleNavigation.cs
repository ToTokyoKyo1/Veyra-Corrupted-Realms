using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Veyra.Core;

namespace Veyra.Combat.Tutorial
{
    public sealed class TutorialBattleNavigation : MonoBehaviour
    {
        [SerializeField] private Button backButton;
        [SerializeField] private Button resultMenuButton;
        [SerializeField] private TutorialBattleController battleController;

        private bool isLoading;

        public bool IsLoading => isLoading;

        private void Update()
        {
            if (!isLoading && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                BackToMenu();
            }
        }

        public void BackToMenu()
        {
            if (!isLoading)
            {
                StartCoroutine(LoadMenu());
            }
        }

        private IEnumerator LoadMenu()
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

            battleController.CancelRunningActionForSceneChange();

            AsyncOperation operation;
            try
            {
                operation = SceneManager.LoadSceneAsync(SceneNames.MainMenu, LoadSceneMode.Single);
            }
            catch (Exception exception)
            {
                ShowLoadError(exception.Message);
                yield break;
            }

            if (operation == null)
            {
                ShowLoadError("Unity non ha avviato il caricamento del menu.");
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

            battleController.ShowExternalMessage("Ritorno al menu non riuscito: " + details);
        }
    }
}
