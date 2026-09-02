using System.Collections;
using UnityEngine;

namespace Veyra.Combat
{
    /// <summary>
    /// Temporary close-combat presentation for Hero01. It deliberately contains no final
    /// attack art: sprite clips and impact VFX can be assigned later without changing combat rules.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeroCombatPresentation : MonoBehaviour
    {
        [SerializeField] private Transform actor;
        [SerializeField] private Animator animator;
        [SerializeField] private string basicAttackTrigger = "Attack";
        [SerializeField] private string techniqueTrigger = "Technique";
        [SerializeField, Min(0.05f)] private float preparationDuration = 0.10f;
        [SerializeField, Min(0.05f)] private float lungeDuration = 0.14f;
        [SerializeField, Min(0.05f)] private float returnDuration = 0.16f;
        [SerializeField, Min(0.1f)] private float maximumLungeDistance = 0.72f;

        public static HeroCombatPresentation Ensure(Transform heroActor)
        {
            if (heroActor == null) return null;
            HeroCombatPresentation presentation =
                heroActor.GetComponent<HeroCombatPresentation>();
            if (presentation == null)
            {
                Debug.LogError(
                    "[Veyra] HeroCombatPresentation mancante: applica l'integrazione grafica dalle Tools.",
                    heroActor);
                return null;
            }

            presentation.actor = heroActor;
            if (presentation.animator == null)
            {
                presentation.animator = heroActor.GetComponentInChildren<Animator>();
            }

            return presentation;
        }

        public IEnumerator PlayMelee(Transform target, bool technique)
        {
            if (actor == null || target == null) yield break;

            Vector3 origin = actor.position;
            Vector3 originalScale = actor.localScale;
            Vector3 direction = target.position - origin;
            direction.z = 0f;
            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.right;
            direction.Normalize();

            TriggerIfAvailable(technique ? techniqueTrigger : basicAttackTrigger);
            yield return AnimateScale(originalScale, originalScale * 0.94f, preparationDuration);

            float availableDistance = Vector3.Distance(origin, target.position);
            float lungeDistance = Mathf.Min(maximumLungeDistance, availableDistance * 0.35f);
            Vector3 impactPosition = origin + direction * lungeDistance;
            yield return AnimatePosition(origin, impactPosition, lungeDuration);
            yield return new WaitForSecondsRealtime(technique ? 0.10f : 0.05f);
            yield return AnimatePosition(actor.position, origin, returnDuration);
            actor.position = origin;
            actor.localScale = originalScale;
        }

        private IEnumerator AnimatePosition(Vector3 from, Vector3 to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                actor.position = Vector3.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            actor.position = to;
        }

        private IEnumerator AnimateScale(Vector3 from, Vector3 to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                actor.localScale = Vector3.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            actor.localScale = to;
        }

        private void TriggerIfAvailable(string parameterName)
        {
            if (animator == null || string.IsNullOrWhiteSpace(parameterName)) return;
            int hash = Animator.StringToHash(parameterName);
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash == hash && parameter.type == AnimatorControllerParameterType.Trigger)
                {
                    animator.SetTrigger(hash);
                    return;
                }
            }
        }
    }
}
