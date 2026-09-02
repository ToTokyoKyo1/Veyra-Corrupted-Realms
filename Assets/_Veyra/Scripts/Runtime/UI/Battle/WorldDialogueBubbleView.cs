using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace Veyra.UI.Battle
{
    [DisallowMultipleComponent]
    public sealed class WorldDialogueBubbleView : MonoBehaviour
    {
        private const int MaximumCharactersPerPage = 126;

        private readonly Queue<DialoguePage> pages = new Queue<DialoguePage>();

        [SerializeField] private WorldUiFollower follower;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text speakerText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField, Min(0.01f)] private float fadeDuration = 0.16f;
        [SerializeField, Min(0.1f)] private float minimumVisibleSeconds = 2.5f;
        [SerializeField, Min(0.1f)] private float maximumVisibleSeconds = 6f;
        [SerializeField, Min(0f)] private float secondsPerCharacter = 0.026f;

        private Coroutine queueRoutine;
        private float requestedAlpha;

        public bool IsPresented => requestedAlpha > 0f || pages.Count > 0;
        public Transform Target => follower != null ? follower.Target : null;

        public void SetTarget(Transform target)
        {
            follower?.SetTarget(target);
        }

        public void ShowDialogue(string speaker, string dialogue)
        {
            if (string.IsNullOrWhiteSpace(dialogue))
            {
                return;
            }

            foreach (string page in SplitIntoPages(dialogue.Trim()))
            {
                pages.Enqueue(new DialoguePage(speaker, page));
            }

            if (queueRoutine == null)
            {
                queueRoutine = StartCoroutine(PresentQueue());
            }
        }

        public void HideImmediate()
        {
            pages.Clear();
            if (queueRoutine != null)
            {
                StopCoroutine(queueRoutine);
                queueRoutine = null;
            }

            requestedAlpha = 0f;
            ApplyVisibility();
        }

        private IEnumerator PresentQueue()
        {
            while (pages.Count > 0)
            {
                DialoguePage page = pages.Dequeue();
                if (speakerText != null)
                {
                    speakerText.text = string.IsNullOrWhiteSpace(page.Speaker)
                        ? "NEMICO"
                        : page.Speaker.ToUpperInvariant();
                }
                if (bodyText != null)
                {
                    bodyText.text = page.Text;
                }

                follower?.RefreshNow();
                yield return Fade(0f, 1f);
                float duration = Mathf.Clamp(
                    minimumVisibleSeconds + page.Text.Length * secondsPerCharacter,
                    minimumVisibleSeconds,
                    maximumVisibleSeconds);
                yield return new WaitForSecondsRealtime(duration);
                yield return Fade(1f, 0f);
            }

            requestedAlpha = 0f;
            ApplyVisibility();
            queueRoutine = null;
        }

        private IEnumerator Fade(float start, float end)
        {
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                requestedAlpha = Mathf.Lerp(start, end, Mathf.Clamp01(elapsed / fadeDuration));
                ApplyVisibility();
                yield return null;
            }

            requestedAlpha = end;
            ApplyVisibility();
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

        private static IEnumerable<string> SplitIntoPages(string text)
        {
            string[] words = text.Split(' ');
            var builder = new StringBuilder();
            foreach (string word in words)
            {
                if (builder.Length > 0 && builder.Length + word.Length + 1 > MaximumCharactersPerPage)
                {
                    yield return builder.ToString();
                    builder.Length = 0;
                }

                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }
                builder.Append(word);
            }

            if (builder.Length > 0)
            {
                yield return builder.ToString();
            }
        }

        private readonly struct DialoguePage
        {
            public DialoguePage(string speaker, string text)
            {
                Speaker = speaker ?? string.Empty;
                Text = text ?? string.Empty;
            }

            public string Speaker { get; }
            public string Text { get; }
        }
    }
}
