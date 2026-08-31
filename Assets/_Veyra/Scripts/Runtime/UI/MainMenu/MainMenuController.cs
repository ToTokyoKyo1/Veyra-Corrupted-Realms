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
        [SerializeField] private TMP_Text startButtonLabel;
        [SerializeField] private TMP_Text campaignStatusText;
        [SerializeField] private Button replayTutorialButton;
        [SerializeField] private Button resetProgressButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private SettingsPanelController settingsPanel;
        [SerializeField] private GameObject resetProgressConfirmationModal;
        [SerializeField] private GameObject loadingOverlay;
        [SerializeField] private GameObject errorModal;
        [SerializeField] private TMP_Text errorMessage;

        private bool isLoading;

        private void Awake()
        {
            LocalSettingsStore.ApplyMasterVolume(LocalSettingsStore.Load().masterVolume);
            if (resetProgressConfirmationModal != null)
            {
                resetProgressConfirmationModal.SetActive(false);
            }

            RefreshCampaignState();
        }

        public void StartGame()
        {
            if (!isLoading)
            {
                StartCoroutine(LoadCampaignScene(CampaignProgressStore.GetNextSceneName()));
            }
        }

        public void ReplayTutorial()
        {
            if (!isLoading)
            {
                StartCoroutine(LoadCampaignScene(SceneNames.World01Level01Tutorial));
            }
        }

        public void OpenResetProgressConfirmation()
        {
            if (!isLoading && resetProgressConfirmationModal != null)
            {
                resetProgressConfirmationModal.SetActive(true);
                SetMenuControlsEnabled(false);
            }
        }

        public void CloseResetProgressConfirmation()
        {
            if (resetProgressConfirmationModal != null)
            {
                resetProgressConfirmationModal.SetActive(false);
            }

            if (!isLoading)
            {
                SetMenuControlsEnabled(true);
            }
        }

        public void ConfirmResetProgress()
        {
            if (isLoading || resetProgressConfirmationModal == null ||
                !resetProgressConfirmationModal.activeSelf)
            {
                return;
            }

            CampaignProgressStore.Reset();
            resetProgressConfirmationModal.SetActive(false);
            RefreshCampaignState();
            SetMenuControlsEnabled(true);
        }

        public void RefreshCampaignState()
        {
            CampaignProgressData progress = CampaignProgressStore.Load();

            if (startButtonLabel != null)
            {
                startButtonLabel.text = GetPrimaryActionLabel(progress);
            }

            if (campaignStatusText != null)
            {
                campaignStatusText.text = GetCampaignStatus(progress);
            }

            if (replayTutorialButton != null)
            {
                replayTutorialButton.gameObject.SetActive(progress.tutorialCompleted);
            }

            if (resetProgressButton != null)
            {
                resetProgressButton.gameObject.SetActive(progress.HasAnyProgress);
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

        private IEnumerator LoadCampaignScene(string sceneName)
        {
            isLoading = true;
            SetMenuControlsEnabled(false);
            if (resetProgressConfirmationModal != null)
            {
                resetProgressConfirmationModal.SetActive(false);
            }

            errorModal.SetActive(false);
            loadingOverlay.SetActive(true);

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
                ShowLoadError("Unity non ha avviato il caricamento della scena richiesta.");
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
            errorMessage.text = "Impossibile aprire la scena.\n" + details;
            errorModal.SetActive(true);
        }

        private void SetMenuControlsEnabled(bool enabled)
        {
            startButton.interactable = enabled;
            settingsButton.interactable = enabled;

            if (replayTutorialButton != null)
            {
                replayTutorialButton.interactable = enabled;
            }

            if (resetProgressButton != null)
            {
                resetProgressButton.interactable = enabled;
            }
        }

        private static string GetPrimaryActionLabel(CampaignProgressData progress)
        {
            if (!progress.tutorialCompleted)
            {
                return "INIZIA";
            }

            if (!progress.encounter02Resolved || !progress.encounter03Resolved)
            {
                return "CONTINUA";
            }

            return "RIGIOCA LIVELLO 3";
        }

        private static string GetCampaignStatus(CampaignProgressData progress)
        {
            if (!progress.tutorialCompleted)
            {
                return "PROSSIMO SCONTRO\nTUTORIAL";
            }

            if (!progress.encounter02Resolved)
            {
                return "PROSSIMO SCONTRO\nCUSTODE DEL ROVO";
            }

            string thornResult = GetResolutionDisplayName(progress.encounter02Resolution);
            if (!progress.encounter03Resolved)
            {
                return "CUSTODE: " + thornResult + "\nPROSSIMO: VIGILE DELLE CENERI";
            }

            return "PRIMO BLOCCO COMPLETATO\nCUSTODE: " + thornResult +
                   "  •  VIGILE: " + GetResolutionDisplayName(progress.encounter03Resolution);
        }

        private static string GetResolutionDisplayName(EncounterResolution resolution)
        {
            switch (resolution)
            {
                case EncounterResolution.Saved:
                    return "SALVATO";
                case EncounterResolution.Killed:
                    return "UCCISO";
                default:
                    return "NON RISOLTO";
            }
        }
    }
}
