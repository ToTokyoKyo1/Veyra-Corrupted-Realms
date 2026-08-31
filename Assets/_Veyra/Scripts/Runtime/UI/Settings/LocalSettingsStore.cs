using System;
using UnityEngine;

namespace Veyra.UI.Settings
{
    public static class LocalSettingsStore
    {
        public const string VersionKey = "Veyra.Settings.Version";
        public const string MasterVolumeKey = "Veyra.Settings.MasterVolume";
        public const string MusicVolumeKey = "Veyra.Settings.MusicVolume";
        public const string SfxVolumeKey = "Veyra.Settings.SfxVolume";
        public const string VibrationEnabledKey = "Veyra.Settings.VibrationEnabled";

        public const int CurrentVersion = 1;

        [Serializable]
        public struct Values
        {
            public int version;
            public float masterVolume;
            public float musicVolume;
            public float sfxVolume;
            public bool vibrationEnabled;
        }

        public static Values Defaults => new Values
        {
            version = CurrentVersion,
            masterVolume = 1f,
            musicVolume = 0.8f,
            sfxVolume = 0.8f,
            vibrationEnabled = true
        };

        public static Values Load()
        {
            Values defaults = Defaults;

            return new Values
            {
                version = CurrentVersion,
                masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, defaults.masterVolume)),
                musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, defaults.musicVolume)),
                sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, defaults.sfxVolume)),
                vibrationEnabled = PlayerPrefs.GetInt(VibrationEnabledKey, defaults.vibrationEnabled ? 1 : 0) == 1
            };
        }

        public static void Save(Values values)
        {
            values.version = CurrentVersion;
            values.masterVolume = Mathf.Clamp01(values.masterVolume);
            values.musicVolume = Mathf.Clamp01(values.musicVolume);
            values.sfxVolume = Mathf.Clamp01(values.sfxVolume);

            PlayerPrefs.SetInt(VersionKey, values.version);
            PlayerPrefs.SetFloat(MasterVolumeKey, values.masterVolume);
            PlayerPrefs.SetFloat(MusicVolumeKey, values.musicVolume);
            PlayerPrefs.SetFloat(SfxVolumeKey, values.sfxVolume);
            PlayerPrefs.SetInt(VibrationEnabledKey, values.vibrationEnabled ? 1 : 0);
            PlayerPrefs.Save();
            ApplyMasterVolume(values.masterVolume);
        }

        public static void ApplyMasterVolume(float volume)
        {
            AudioListener.volume = Mathf.Clamp01(volume);
        }
    }
}
