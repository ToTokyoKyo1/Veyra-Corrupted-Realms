using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Veyra.Core;

namespace Veyra.Combat.Encounter
{
    public sealed class EncounterBattleNavigation : MonoBehaviour
    {
        [SerializeField] private Button backButton;
        [SerializeField] private Button resultMenuButton;
        [SerializeField] private EncounterBattleController battleController;

        private bool isLoading;

        public bool IsLoading => isLoading;

        private void Update()
        {
            if (!isLoading && Keyboard.current != null &&
                Keyboard.current.escapeKey.wasPressedThisFrame)
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
            SetNavigationButtons(false);
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
        }

        private void ShowLoadError(string details)
        {
            isLoading = false;
            SetNavigationButtons(true);

            if (battleController != null)
            {
                battleController.ShowExternalMessage(
                    "Ritorno al menu non riuscito: " + details);
            }
        }
    }
}
