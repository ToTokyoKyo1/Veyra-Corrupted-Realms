using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Veyra.UI.Battle
{
    [DisallowMultipleComponent]
    public sealed class WorldHealthBarView : MonoBehaviour
    {
        [SerializeField] private WorldUiFollower follower;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image currentFill;
        [SerializeField] private Image chipFill;
        [SerializeField] private Image dangerFrame;
        [SerializeField] private TMP_Text valueText;
        [SerializeField, Min(0f)] private float chipDelay = 0.24f;
        [SerializeField, Min(0.01f)] private float chipDuration = 0.34f;
        [SerializeField, Min(0f)] private float visibleDuration = 1.55f;
        [SerializeField, Min(0.01f)] private float fadeDuration = 0.25f;

        private Coroutine presentationRoutine;
        private float requestedAlpha;

        public bool IsPresented => requestedAlpha > 0f;
        public Transform Target => follower != null ? follower.Target : null;

        public void SetTarget(Transform target)
        {
            if (follower != null)
            {
                follower.SetTarget(target);
            }
        }

        public void SetHealthSilently(int current, int maximum)
        {
            if (presentationRoutine != null)
            {
                StopCoroutine(presentationRoutine);
                presentationRoutine = null;
            }

            float ratio = GetRatio(current, maximum);
            SetFill(currentFill, ratio);
            SetFill(chipFill, ratio);
            SetValue(current, maximum);
            SetDanger(ratio <= 0.25f && current > 0);
            requestedAlpha = 0f;
            ApplyVisibility();
        }

        public void ShowDamage(int previous, int current, int maximum)
        {
            if (maximum <= 0 || current >= previous)
            {
                return;
            }

            previous = Mathf.Clamp(previous, 0, maximum);
            current = Mathf.Clamp(current, 0, maximum);
            if (presentationRoutine != null)
            {
                StopCoroutine(presentationRoutine);
            }

            presentationRoutine = StartCoroutine(PresentDamage(previous, current, maximum));
        }

        public void HideImmediate()
        {
            if (presentationRoutine != null)
            {
                StopCoroutine(presentationRoutine);
                presentationRoutine = null;
            }

            requestedAlpha = 0f;
            ApplyVisibility();
        }

        private IEnumerator PresentDamage(int previous, int current, int maximum)
        {
            float previousRatio = GetRatio(previous, maximum);
            float currentRatio = GetRatio(current, maximum);
            SetFill(chipFill, previousRatio);
            SetFill(currentFill, currentRatio);
            SetValue(current, maximum);
            SetDanger(currentRatio <= 0.25f && current > 0);
            requestedAlpha = 1f;
            follower?.RefreshNow();
            ApplyVisibility();

            if (chipDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(chipDelay);
            }

            float elapsed = 0f;
            while (elapsed < chipDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / chipDuration);
                SetFill(chipFill, Mathf.Lerp(previousRatio, currentRatio, progress));
                if (dangerFrame != null && dangerFrame.enabled)
                {
                    Color danger = dangerFrame.color;
                    danger.a = 0.55f + Mathf.PingPong(elapsed * 2.8f, 0.40f);
                    dangerFrame.color = danger;
                }
                yield return null;
            }

            SetFill(chipFill, currentRatio);
            if (visibleDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(visibleDuration);
            }

            elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                requestedAlpha = 1f - Mathf.Clamp01(elapsed / fadeDuration);
                ApplyVisibility();
                yield return null;
            }

            requestedAlpha = 0f;
            ApplyVisibility();
            presentationRoutine = null;
        }

        private void LateUpdate()
        {
            ApplyVisibility();
        }

        private void ApplyVisibility()
        {
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = follower == null || follower.IsTargetVisible ? requestedAlpha : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        private void SetDanger(bool value)
        {
            if (dangerFrame != null)
            {
                dangerFrame.enabled = value;
                Color color = dangerFrame.color;
                color.a = value ? 0.78f : 0f;
                dangerFrame.color = color;
            }
        }

        private void SetValue(int current, int maximum)
        {
            if (valueText != null)
            {
                valueText.text = current + " / " + maximum;
            }
        }

        private static void SetFill(Image image, float value)
        {
            if (image != null)
            {
                image.fillAmount = Mathf.Clamp01(value);
            }
        }

        private static float GetRatio(int current, int maximum)
        {
            return maximum <= 0 ? 0f : Mathf.Clamp01(current / (float)maximum);
        }
    }
}
