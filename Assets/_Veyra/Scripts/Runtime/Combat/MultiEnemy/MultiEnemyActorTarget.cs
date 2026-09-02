using UnityEngine;
using UnityEngine.EventSystems;

namespace Veyra.Combat.MultiEnemy
{
    /// <summary>
    /// Persistent scene bridge that lets the authored enemy actor select the same
    /// target as its HUD card. The scene factory must also author a Collider2D.
    /// </summary>
    public sealed class MultiEnemyActorTarget : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private MultiEnemyBattleController battleController;
        [SerializeField] private int enemyIndex;

        public int EnemyIndex => enemyIndex;

        private void OnMouseUpAsButton()
        {
            SelectTarget();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || eventData.button == PointerEventData.InputButton.Left)
            {
                SelectTarget();
            }
        }

        private void SelectTarget()
        {
            if (battleController == null || !isActiveAndEnabled)
            {
                return;
            }

            battleController.SelectTargetByIndex(enemyIndex);
        }
    }
}
