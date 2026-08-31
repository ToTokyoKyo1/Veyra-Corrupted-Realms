using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Veyra.Core;
using Veyra.UI.Settings;

namespace Veyra.UI.MainMenu
{
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Button startButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private SettingsPanelController settingsPanel;
        [SerializeField] private GameObject loadingOverlay;
        [SerializeField] private GameObject errorModal;
        [SerializeField] private TMP_Text errorMessage;

        private bool isLoading;

        private void Awake()
        {
            LocalSettingsStore.ApplyMasterVolume(LocalSettingsStore.Load().masterVolume);
        }

        public void StartGame()
        {
            if (!isLoading)
            {
                StartCoroutine(LoadTutorial());
            }
        }

        public void OpenSettings()
        {
            if (!isLoading)
            {
                settingsPanel.Open();
            }
        }

        public void CloseError()
        {
            errorModal.SetActive(false);
        }

        private IEnumerator LoadTutorial()
        {
            isLoading = true;
            SetMenuControlsEnabled(false);
            errorModal.SetActive(false);
            loadingOverlay.SetActive(true);

            AsyncOperation operation;
            try
            {
                operation = SceneManager.LoadSceneAsync(SceneNames.World01Level01Tutorial, LoadSceneMode.Single);
            }
            catch (Exception exception)
            {
                ShowLoadError(exception.Message);
                yield break;
            }

            if (operation == null)
            {
                ShowLoadError("Unity non ha avviato il caricamento della scena tutorial.");
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
            loadingOverlay.SetActive(false);
            SetMenuControlsEnabled(true);
            errorMessage.text = "Impossibile aprire il tutorial.\n" + details;
            errorModal.SetActive(true);
        }

        private void SetMenuControlsEnabled(bool enabled)
        {
            startButton.interactable = enabled;
            settingsButton.interactable = enabled;
        }
    }
}
