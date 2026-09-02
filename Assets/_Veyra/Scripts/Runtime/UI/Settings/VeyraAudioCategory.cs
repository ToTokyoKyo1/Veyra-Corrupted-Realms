using UnityEngine;

namespace Veyra.UI.Settings
{
    public enum VeyraAudioBus
    {
        Music,
        Effects
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class VeyraAudioCategory : MonoBehaviour
    {
        [SerializeField] private AudioSource source;
        [SerializeField] private VeyraAudioBus bus = VeyraAudioBus.Effects;
        [SerializeField, Range(0f, 1f)] private float baseVolume = 1f;

        private void Awake()
        {
            if (source == null) source = GetComponent<AudioSource>();
            Apply();
        }

        private void OnEnable()
        {
            Apply();
        }

        public void Apply()
        {
            if (source == null) return;
            float categoryVolume = bus == VeyraAudioBus.Music
                ? LocalSettingsStore.GetMusicVolume()
                : LocalSettingsStore.GetSfxVolume();
            source.volume = Mathf.Clamp01(baseVolume * categoryVolume);
        }
    }
}
