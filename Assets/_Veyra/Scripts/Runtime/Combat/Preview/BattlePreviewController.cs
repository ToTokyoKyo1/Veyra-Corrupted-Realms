using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Veyra.Combat.Preview
{
    public sealed class BattlePreviewController : MonoBehaviour
    {
        [Header("Action controls")]
        [SerializeField] private Button[] actionButtons;
        [SerializeField] private TMP_Text combatMessage;

        [Header("Characters")]
        [SerializeField] private SpriteRenderer heroVisual;
        [SerializeField] private SpriteRenderer enemyVisual;
        [SerializeField] private Transform heroProjectileOrigin;
        [SerializeField] private Transform heroHitTarget;
        [SerializeField] private Transform enemyProjectileOrigin;
        [SerializeField] private Transform enemyHitTarget;

        [Header("Persistent preview effects")]
        [SerializeField] private GameObject heroBasicProjectile;
        [SerializeField] private GameObject heroTechniqueProjectile;
        [SerializeField] private GameObject enemyProjectile;
        [SerializeField] private GameObject guardVisual;
        [SerializeField] private GameObject markPreview;

        private const string IdleMessage = "Seleziona un comando";

        private bool previewRunning;
        private Color heroBaseColor;
        private Color enemyBaseColor;
        private Vector3 basicBaseScale;
        private Vector3 techniqueBaseScale;
        private Vector3 enemyProjectileBaseScale;
        private Vector3 guardBaseScale;
        private Vector3 markBaseScale;

        public bool IsPreviewRunning => previewRunning;

        private void Awake()
        {
            heroBaseColor = heroVisual.color;
            enemyBaseColor = enemyVisual.color;
            basicBaseScale = heroBasicProjectile.transform.localScale;
            techniqueBaseScale = heroTechniqueProjectile.transform.localScale;
            enemyProjectileBaseScale = enemyProjectile.transform.localScale;
            guardBaseScale = guardVisual.transform.localScale;
            markBaseScale = markPreview.transform.localScale;
            ResetPreviewObjects();
        }

        public void PreviewAttack()
        {
            BeginPreview(AttackSequence());
        }

        public void PreviewGuard()
        {
            BeginPreview(GuardSequence());
        }

        public void PreviewTechnique()
        {
            BeginPreview(TechniqueSequence());
        }

        public void PreviewMark()
        {
            BeginPreview(MarkSequence());
        }

        public void CancelPreviewAndReset()
        {
            StopAllCoroutines();
            previewRunning = false;
            ResetPreviewObjects();
            SetActionButtonsEnabled(true);
        }

        public void ShowExternalMessage(string message)
        {
            combatMessage.text = message;
        }

        private void BeginPreview(IEnumerator sequence)
        {
            if (previewRunning)
            {
                return;
            }

            StartCoroutine(RunPreview(sequence));
        }

        private IEnumerator RunPreview(IEnumerator sequence)
        {
            previewRunning = true;
            SetActionButtonsEnabled(false);
            ResetPreviewObjects();

            yield return sequence;

            ResetPreviewObjects();
            previewRunning = false;
            SetActionButtonsEnabled(true);
        }

        private IEnumerator AttackSequence()
        {
            combatMessage.text = "Anteprima Attacco";
            yield return MoveEffect(heroBasicProjectile, heroProjectileOrigin.position, enemyHitTarget.position, 0.34f);
            yield return Flash(enemyVisual, Color.white, 0.18f);
            yield return MoveEffect(enemyProjectile, enemyProjectileOrigin.position, heroHitTarget.position, 0.32f);
            yield return Flash(heroVisual, new Color(1f, 0.72f, 0.72f, 1f), 0.14f);
        }

        private IEnumerator GuardSequence()
        {
            combatMessage.text = "Anteprima Guardia · Colpo bloccato";
            guardVisual.SetActive(true);
            yield return PulseScale(guardVisual, guardBaseScale, 1.25f, 0.16f);
            yield return MoveEffect(enemyProjectile, enemyProjectileOrigin.position, heroHitTarget.position, 0.36f);
            yield return PulseScale(guardVisual, guardBaseScale, 1.5f, 0.22f);
        }

        private IEnumerator TechniqueSequence()
        {
            combatMessage.text = "Anteprima Tecnica";
            heroTechniqueProjectile.transform.localScale = techniqueBaseScale * 1.25f;
            yield return MoveEffect(heroTechniqueProjectile, heroProjectileOrigin.position, enemyHitTarget.position, 0.56f);
            yield return Flash(enemyVisual, new Color(0.73f, 1f, 0.94f, 1f), 0.30f);
        }

        private IEnumerator MarkSequence()
        {
            combatMessage.text = "Anteprima Marchio";
            markPreview.SetActive(true);
            yield return PulseScale(markPreview, markBaseScale, 1.65f, 0.46f);
            yield return PulseScale(markPreview, markBaseScale, 1.25f, 0.26f);
        }

        private static IEnumerator MoveEffect(GameObject effect, Vector3 start, Vector3 end, float duration)
        {
            Transform effectTransform = effect.transform;
            effectTransform.position = start;
            effect.SetActive(true);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                effectTransform.position = Vector3.Lerp(start, end, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            effectTransform.position = end;
            yield return new WaitForSecondsRealtime(0.06f);
            effect.SetActive(false);
        }

        private static IEnumerator Flash(SpriteRenderer target, Color flashColor, float duration)
        {
            Color original = target.color;
            target.color = flashColor;
            yield return new WaitForSecondsRealtime(duration);
            target.color = original;
        }

        private static IEnumerator PulseScale(GameObject effect, Vector3 baseScale, float multiplier, float duration)
        {
            effect.SetActive(true);
            float halfDuration = duration * 0.5f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(elapsed / halfDuration);
                float factor = elapsed <= halfDuration
                    ? Mathf.Lerp(1f, multiplier, normalized)
                    : Mathf.Lerp(multiplier, 1f, normalized - 1f);
                effect.transform.localScale = baseScale * factor;
                yield return null;
            }

            effect.transform.localScale = baseScale;
        }

        private void ResetPreviewObjects()
        {
            heroVisual.color = heroBaseColor;
            enemyVisual.color = enemyBaseColor;
            heroBasicProjectile.transform.localScale = basicBaseScale;
            heroTechniqueProjectile.transform.localScale = techniqueBaseScale;
            enemyProjectile.transform.localScale = enemyProjectileBaseScale;
            guardVisual.transform.localScale = guardBaseScale;
            markPreview.transform.localScale = markBaseScale;

            heroBasicProjectile.transform.position = heroProjectileOrigin.position;
            heroTechniqueProjectile.transform.position = heroProjectileOrigin.position;
            enemyProjectile.transform.position = enemyProjectileOrigin.position;

            heroBasicProjectile.SetActive(false);
            heroTechniqueProjectile.SetActive(false);
            enemyProjectile.SetActive(false);
            guardVisual.SetActive(false);
            markPreview.SetActive(false);
            combatMessage.text = IdleMessage;
        }

        private void SetActionButtonsEnabled(bool enabled)
        {
            foreach (Button button in actionButtons)
            {
                button.interactable = enabled;
            }
        }
    }
}
