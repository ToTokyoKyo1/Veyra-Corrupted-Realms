using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Veyra.UI.Settings
{
    public sealed class SettingsPanelController : MonoBehaviour
    {
        [Header("Persistent panel objects")]
        [SerializeField] private GameObject dimmer;
        [SerializeField] private GameObject modalRoot;

        [Header("Controls")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Toggle vibrationToggle;
        [SerializeField] private TMP_Text masterValueText;
        [SerializeField] private TMP_Text musicValueText;
        [SerializeField] private TMP_Text sfxValueText;

        private LocalSettingsStore.Values values;
        private bool initialized;

        public bool IsOpen => modalRoot != null && modalRoot.activeSelf;

        private void Awake()
        {
            InitializeIfNeeded();
        }

        private void Update()
        {
            if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                SaveCurrentValues();
            }
        }

        public void Open()
        {
            InitializeIfNeeded();
            values = LocalSettingsStore.Load();
            ApplyValuesToUi();
            dimmer.SetActive(true);
            modalRoot.SetActive(true);
        }

        public void Close()
        {
            if (!initialized)
            {
                return;
            }

            SaveCurrentValues();
            modalRoot.SetActive(false);
            dimmer.SetActive(false);
        }

        public void ResetToDefaults()
        {
            InitializeIfNeeded();
            values = LocalSettingsStore.Defaults;
            ApplyValuesToUi();
            LocalSettingsStore.Apply(values);
        }

        public void OnMasterVolumeChanged(float value)
        {
            values.masterVolume = Mathf.Clamp01(value);
            LocalSettingsStore.ApplyMasterVolume(values.masterVolume);
            UpdateValueLabels();
        }

        public void OnMusicVolumeChanged(float value)
        {
            values.musicVolume = Mathf.Clamp01(value);
            LocalSettingsStore.Save(values);
            UpdateValueLabels();
        }

        public void OnSfxVolumeChanged(float value)
        {
            values.sfxVolume = Mathf.Clamp01(value);
            LocalSettingsStore.Save(values);
            UpdateValueLabels();
        }

        public void OnVibrationChanged(bool value)
        {
            values.vibrationEnabled = value;
            LocalSettingsStore.Save(values);
        }

        private void InitializeIfNeeded()
        {
            if (initialized)
            {
                return;
            }

            values = LocalSettingsStore.Load();
            ApplyValuesToUi();
            LocalSettingsStore.Apply(values);
            initialized = true;
        }

        private void ApplyValuesToUi()
        {
            masterVolumeSlider.SetValueWithoutNotify(values.masterVolume);
            musicVolumeSlider.SetValueWithoutNotify(values.musicVolume);
            sfxVolumeSlider.SetValueWithoutNotify(values.sfxVolume);
            vibrationToggle.SetIsOnWithoutNotify(values.vibrationEnabled);
            UpdateValueLabels();
        }

        private void UpdateValueLabels()
        {
            masterValueText.text = Mathf.RoundToInt(values.masterVolume * 100f) + "%";
            musicValueText.text = Mathf.RoundToInt(values.musicVolume * 100f) + "%";
            sfxValueText.text = Mathf.RoundToInt(values.sfxVolume * 100f) + "%";
        }

        private void SaveCurrentValues()
        {
            LocalSettingsStore.Save(values);
        }
    }
}
