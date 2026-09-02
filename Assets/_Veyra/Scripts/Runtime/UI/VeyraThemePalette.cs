using UnityEngine;

namespace Veyra.UI
{
    [CreateAssetMenu(fileName = "VeyraThemePalette", menuName = "Veyra/UI/Theme Palette")]
    public sealed class VeyraThemePalette : ScriptableObject
    {
        [Header("Surfaces")]
        public Color background = new Color32(9, 11, 21, 255);
        public Color panel = new Color32(20, 24, 46, 255);
        public Color secondaryPanel = new Color32(44, 53, 77, 255);
        public Color border = new Color32(64, 73, 115, 255);
        public Color disabled = new Color32(104, 111, 153, 255);

        [Header("Text")]
        public Color primaryText = new Color32(245, 255, 232, 255);
        public Color secondaryText = new Color32(163, 167, 194, 255);

        [Header("Meaning")]
        public Color information = new Color32(146, 232, 192, 255);
        public Color action = new Color32(255, 174, 112, 255);
        public Color danger = new Color32(173, 47, 69, 255);
        public Color damage = new Color32(189, 106, 98, 255);
        public Color corruption = new Color32(105, 36, 100, 255);
    }
}
