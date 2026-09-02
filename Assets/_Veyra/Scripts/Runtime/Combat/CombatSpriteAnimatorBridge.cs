using UnityEngine;

namespace Veyra.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class CombatSpriteAnimatorBridge : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private string movingParameter = "Moving";
        [SerializeField, Min(0.0001f)] private float movementThreshold = 0.0025f;
        [SerializeField, Min(0.01f)] private float settleDelay = 0.12f;

        private Vector3 previousPosition;
        private float stillTime;
        private int movingHash;

        public void Configure(Animator targetAnimator)
        {
            animator = targetAnimator;
        }

        private void Awake()
        {
            previousPosition = transform.position;
            movingHash = Animator.StringToHash(movingParameter);
        }

        private void Update()
        {
            if (animator == null)
            {
                return;
            }

            bool moved = (transform.position - previousPosition).sqrMagnitude >=
                         movementThreshold * movementThreshold;
            previousPosition = transform.position;
            stillTime = moved ? 0f : stillTime + Time.deltaTime;
            animator.SetBool(movingHash, moved || stillTime < settleDelay);
        }
    }
}
