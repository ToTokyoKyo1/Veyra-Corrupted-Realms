using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Veyra.Core;
using Veyra.UI.Settings;

namespace Veyra.UI.Battle
{
    [DisallowMultipleComponent]
    public sealed class BattlePauseController : MonoBehaviour
    {
        [SerializeField] private GameObject pauseRoot;
        [SerializeField] private GameObject confirmationRoot;
        [SerializeField] private TMP_Text confirmationText;
        [SerializeField] private Button resumeButton;
        [SerializeField] private SettingsPanelController settingsPanel;

        private bool retryRequested;

        public bool IsOpen => pauseRoot != null && pauseRoot.activeSelf;

        private void Awake()
        {
            if (pauseRoot != null) pauseRoot.SetActive(false);
            if (confirmationRoot != null) confirmationRoot.SetActive(false);
        }

        private void OnDisable()
        {
            Time.timeScale = 1f;
        }

        public void TogglePause()
        {
            if (settingsPanel != null && settingsPanel.IsOpen)
            {
                settingsPanel.Close();
                return;
            }

            if (IsOpen) Resume();
            else Open();
        }

        public void Open()
        {
            if (pauseRoot == null) return;
            pauseRoot.SetActive(true);
            if (confirmationRoot != null) confirmationRoot.SetActive(false);
            Time.timeScale = 0f;
            Select(resumeButton);
        }

        public void Resume()
        {
            if (confirmationRoot != null && confirmationRoot.activeSelf)
            {
                confirmationRoot.SetActive(false);
                Select(resumeButton);
                return;
            }

            if (pauseRoot != null) pauseRoot.SetActive(false);
            Time.timeScale = 1f;
        }

        public void OpenOptions()
        {
            if (settingsPanel != null) settingsPanel.Open();
        }

        public void RequestRetry()
        {
            retryRequested = true;
            ShowConfirmation("RIPROVARE IL LIVELLO?\nI PROGRESSI DELLO SCONTRO IN CORSO NON SARANNO SALVATI.");
        }

        public void RequestMainMenu()
        {
            retryRequested = false;
            ShowConfirmation("TORNARE AL MENU?\nI PROGRESSI DELLO SCONTRO IN CORSO NON SARANNO SALVATI.");
        }

        public void CancelConfirmation()
        {
            if (confirmationRoot != null) confirmationRoot.SetActive(false);
            Select(resumeButton);
        }

        public void ConfirmLeave()
        {
            Time.timeScale = 1f;
            string sceneName = retryRequested
                ? SceneManager.GetActiveScene().name
                : SceneNames.MainMenu;
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }

        private void ShowConfirmation(string message)
        {
            if (confirmationText != null) confirmationText.text = message;
            if (confirmationRoot != null) confirmationRoot.SetActive(true);
        }

        private static void Select(Selectable selectable)
        {
            if (EventSystem.current != null && selectable != null)
            {
                EventSystem.current.SetSelectedGameObject(selectable.gameObject);
            }
        }
    }
}
