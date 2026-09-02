using UnityEngine;

namespace Veyra.Combat.Tactical
{
    [DisallowMultipleComponent]
    public sealed class TacticalPlatformView : MonoBehaviour
    {
        [SerializeField] private int row;
        [SerializeField] private int column;
        [SerializeField] private SpriteRenderer visual;
        [SerializeField] private TacticalBattlefieldController battlefield;

        private Color normalColor;

        public int Row => row;
        public int Column => column;

        public void Configure(
            int logicalRow,
            int logicalColumn,
            TacticalBattlefieldController owner,
            SpriteRenderer platformVisual)
        {
            row = logicalRow;
            column = logicalColumn;
            battlefield = owner;
            visual = platformVisual;
            normalColor = visual != null ? visual.color : Color.white;
        }

        public void SetNormalColor(Color color)
        {
            normalColor = color;
            if (visual != null)
            {
                visual.color = color;
            }
        }

        public void ShowState(
            bool reachable,
            bool selectedTarget,
            bool attackRange,
            bool techniqueRange,
            bool threatened)
        {
            if (visual != null)
            {
                if (reachable)
                {
                    visual.color = new Color(0.36f, 0.92f, 0.55f, 1f);
                }
                else if (selectedTarget)
                {
                    visual.color = new Color(1f, 0.68f, 0.34f, 1f);
                }
                else if (attackRange)
                {
                    visual.color = new Color(1f, 0.36f, 0.35f, 1f);
                }
                else if (techniqueRange)
                {
                    visual.color = new Color(0.35f, 0.82f, 0.95f, 1f);
                }
                else if (threatened)
                {
                    visual.color = new Color(0.48f, 0.20f, 0.31f, 1f);
                }
                else
                {
                    visual.color = normalColor;
                }
            }
        }
    }
}
