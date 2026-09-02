using UnityEngine;

namespace Veyra.UI.Battle
{
    [DisallowMultipleComponent]
    public sealed class WorldUiFollower : MonoBehaviour
    {
        [SerializeField] private Canvas rootCanvas;
        [SerializeField] private RectTransform canvasRect;
        [SerializeField] private RectTransform followedRect;
        [SerializeField] private Transform target;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.8f, 0f);
        [SerializeField] private Vector2 screenPadding = new Vector2(24f, 18f);

        public Transform Target => target;
        public bool IsTargetVisible { get; private set; }

        public void SetTarget(Transform value)
        {
            target = value;
            RefreshNow();
        }

        public void RefreshNow()
        {
            if (target == null || worldCamera == null || canvasRect == null || followedRect == null)
            {
                IsTargetVisible = false;
                return;
            }

            Vector3 viewport = worldCamera.WorldToViewportPoint(target.position + worldOffset);
            IsTargetVisible = viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f &&
                              viewport.y >= 0f && viewport.y <= 1f;
            if (!IsTargetVisible)
            {
                return;
            }

            Vector2 screenPoint = worldCamera.WorldToScreenPoint(target.position + worldOffset);
            Camera eventCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? rootCanvas.worldCamera
                : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    screenPoint,
                    eventCamera,
                    out Vector2 localPoint))
            {
                IsTargetVisible = false;
                return;
            }

            Rect bounds = canvasRect.rect;
            Vector2 halfSize = Vector2.Scale(followedRect.rect.size, followedRect.localScale) * 0.5f;
            localPoint.x = Mathf.Clamp(
                localPoint.x,
                bounds.xMin + halfSize.x + screenPadding.x,
                bounds.xMax - halfSize.x - screenPadding.x);
            localPoint.y = Mathf.Clamp(
                localPoint.y,
                bounds.yMin + halfSize.y + screenPadding.y,
                bounds.yMax - halfSize.y - screenPadding.y);
            followedRect.anchoredPosition = localPoint;
        }

        private void LateUpdate()
        {
            RefreshNow();
        }
    }
}
