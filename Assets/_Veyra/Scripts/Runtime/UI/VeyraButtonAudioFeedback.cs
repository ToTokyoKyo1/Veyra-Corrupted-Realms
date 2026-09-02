using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Veyra.UI.Settings;

namespace Veyra.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button), typeof(AudioSource))]
    public sealed class VeyraButtonAudioFeedback : MonoBehaviour, IPointerDownHandler, ISubmitHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private AudioSource source;
        [SerializeField] private AudioClip clickClip;

        public void Configure(Button targetButton, AudioSource targetSource, AudioClip clip)
        {
            button = targetButton;
            source = targetSource;
            clickClip = clip;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            Play();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            Play();
        }

        private void Play()
        {
            if (button != null && button.interactable && source != null && source.isActiveAndEnabled && clickClip != null)
            {
                source.PlayOneShot(clickClip, LocalSettingsStore.GetSfxVolume());
            }
        }
    }
}
