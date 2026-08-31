#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Veyra.UI;

namespace Veyra.Editor
{
    internal static class Phase02UiFactory
    {
        internal static readonly Color Background = Html("#0B1715");
        internal static readonly Color Panel = Html("#142622");
        internal static readonly Color HighlightedPanel = Html("#1D3731");
        internal static readonly Color Cyan = Html("#59D7D0");
        internal static readonly Color Light = Html("#B9FFF0");
        internal static readonly Color Corruption = Html("#8F4AC7");
        internal static readonly Color MainText = Html("#F3F7F4");
        internal static readonly Color SecondaryText = Html("#A9BBB5");
        internal static readonly Color Error = Html("#E85C65");
        internal static readonly Color Gold = Html("#D5AE62");

        internal static TMP_FontAsset LoadRequiredFont()
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Phase02PrototypeAssetFactory.FontAssetPath);
            if (font == null || font.material == null)
            {
                throw new InvalidOperationException(
                    "Font TMP essenziale assente o incompleto: " + Phase02PrototypeAssetFactory.FontAssetPath +
                    ". Importare le TMP Essential Resources oppure rilanciare il tool Phase 02.");
            }

            return font;
        }

        internal static RectTransform CreateCanvas(Transform parent)
        {
            GameObject canvasObject = new GameObject(
                "Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(parent, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            return canvasObject.GetComponent<RectTransform>();
        }

        internal static RectTransform CreateSafeArea(RectTransform canvas)
        {
            RectTransform safeArea = CreateRect("SafeArea", canvas);
            SetRect(safeArea, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            safeArea.gameObject.AddComponent<SafeAreaFitter>();
            return safeArea;
        }

        internal static GameObject CreateEventSystem(Transform parent)
        {
            GameObject eventSystemObject = new GameObject(
                "EventSystem",
                typeof(EventSystem));
            eventSystemObject.transform.SetParent(parent, false);

            InputSystemUIInputModule module = eventSystemObject.AddComponent<InputSystemUIInputModule>();
            module.UnassignActions();

            const string actionsPath = "Assets/Settings/InputSystem_Actions.inputactions";
            InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(actionsPath);
            InputActionReference[] references = AssetDatabase.LoadAllAssetsAtPath(actionsPath)
                .OfType<InputActionReference>()
                .ToArray();
            if (actions == null || references.Length == 0)
            {
                throw new InvalidOperationException("Input System UI actions persistenti mancanti: " + actionsPath);
            }

            module.actionsAsset = actions;
            module.point = FindActionReference(references, "Point");
            module.leftClick = FindActionReference(references, "Click");
            module.rightClick = FindActionReference(references, "RightClick");
            module.middleClick = FindActionReference(references, "MiddleClick");
            module.scrollWheel = FindActionReference(references, "ScrollWheel");
            module.move = FindActionReference(references, "Navigate");
            module.submit = FindActionReference(references, "Submit");
            module.cancel = FindActionReference(references, "Cancel");
            module.trackedDevicePosition = FindActionReference(references, "TrackedDevicePosition");
            module.trackedDeviceOrientation = FindActionReference(references, "TrackedDeviceOrientation");
            return eventSystemObject;
        }

        private static InputActionReference FindActionReference(
            InputActionReference[] references,
            string actionName)
        {
            InputActionReference reference = references.FirstOrDefault(
                candidate => candidate.action != null && candidate.action.name == actionName);
            if (reference == null)
            {
                throw new InvalidOperationException("Input System UI action mancante: UI/" + actionName);
            }

            return reference;
        }

        internal static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;
            return rectTransform;
        }

        internal static RectTransform CreatePanel(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax,
            Color color,
            bool raycastTarget = false)
        {
            RectTransform rect = CreateRect(name, parent);
            SetRect(rect, anchorMin, anchorMax, offsetMin, offsetMax);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return rect;
        }

        internal static TMP_Text CreateText(
            string name,
            Transform parent,
            string content,
            float fontSize,
            Color color,
            TextAlignmentOptions alignment,
            TMP_FontAsset font,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax,
            FontStyles style = FontStyles.Normal)
        {
            RectTransform rect = CreateRect(name, parent);
            SetRect(rect, anchorMin, anchorMax, offsetMin, offsetMax);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = content;
            text.fontSize = fontSize;
            text.fontSizeMax = fontSize;
            text.fontSizeMin = Mathf.Max(16f, fontSize * 0.52f);
            text.enableAutoSizing = true;
            text.color = color;
            text.alignment = alignment;
            text.fontStyle = style;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        internal static Button CreateButton(
            string name,
            Transform parent,
            string label,
            TMP_FontAsset font,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax,
            bool primary = false)
        {
            RectTransform rect = CreateRect(name, parent);
            SetRect(rect, anchorMin, anchorMax, offsetMin, offsetMax);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = primary ? Cyan : HighlightedPanel;
            image.raycastTarget = true;

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ConfigureButtonColors(button, primary);

            TMP_Text text = CreateText(
                "TXT_" + name.Substring("BTN_".Length),
                rect,
                label,
                primary ? 54f : 44f,
                primary ? Background : MainText,
                TextAlignmentOptions.Center,
                font,
                Vector2.zero,
                Vector2.one,
                new Vector2(22f, 12f),
                new Vector2(-22f, -12f),
                FontStyles.Bold);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return button;
        }

        internal static Slider CreateSlider(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            RectTransform root = CreateRect(name, parent);
            SetRect(root, anchorMin, anchorMax, new Vector2(0f, 0f), new Vector2(0f, 0f));

            RectTransform background = CreatePanel(
                "Background",
                root,
                new Vector2(0f, 0.38f),
                new Vector2(1f, 0.62f),
                Vector2.zero,
                Vector2.zero,
                HighlightedPanel);

            RectTransform fillArea = CreateRect("Fill Area", root);
            SetRect(fillArea, new Vector2(0f, 0.34f), new Vector2(1f, 0.66f), new Vector2(12f, 0f), new Vector2(-12f, 0f));
            RectTransform fill = CreatePanel("Fill", fillArea, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Cyan);

            RectTransform handleArea = CreateRect("Handle Slide Area", root);
            SetRect(handleArea, Vector2.zero, Vector2.one, new Vector2(16f, 0f), new Vector2(-16f, 0f));
            RectTransform handle = CreatePanel(
                "Handle",
                handleArea,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(-32f, -32f),
                new Vector2(32f, 32f),
                Light,
                true);

            Slider slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            slider.wholeNumbers = false;
            background.GetComponent<Image>().raycastTarget = false;
            return slider;
        }

        internal static Toggle CreateToggle(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            RectTransform root = CreateRect(name, parent);
            SetRect(root, anchorMin, anchorMax, Vector2.zero, Vector2.zero);

            RectTransform background = CreatePanel(
                "Background",
                root,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-72f, -48f),
                new Vector2(72f, 48f),
                HighlightedPanel,
                true);
            RectTransform checkmark = CreatePanel(
                "Checkmark",
                background,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-48f, -25f),
                new Vector2(48f, 25f),
                Cyan);

            Toggle toggle = root.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = background.GetComponent<Image>();
            toggle.graphic = checkmark.GetComponent<Image>();
            toggle.isOn = true;
            return toggle;
        }

        internal static void SetRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        internal static void SavePrefabSnapshotIfMissing(GameObject source, string prefabPath, Phase02SetupReport report)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                report.Preserve(prefabPath);
                return;
            }

            if (PrefabUtility.SaveAsPrefabAsset(source, prefabPath) == null)
            {
                throw new InvalidOperationException("Impossibile salvare il prefab UI: " + prefabPath);
            }

            report.Create(prefabPath);
        }

        private static void ConfigureButtonColors(Button button, bool primary)
        {
            ColorBlock colors = button.colors;
            colors.normalColor = primary ? Cyan : HighlightedPanel;
            colors.highlightedColor = primary ? Light : new Color(0.17f, 0.35f, 0.31f, 1f);
            colors.pressedColor = primary ? new Color(0.25f, 0.67f, 0.65f, 1f) : Cyan;
            colors.selectedColor = primary ? Light : Gold;
            colors.disabledColor = new Color(0.25f, 0.32f, 0.30f, 0.55f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
        }

        private static Color Html(string value)
        {
            if (!ColorUtility.TryParseHtmlString(value, out Color color))
            {
                throw new ArgumentException("Colore HTML non valido: " + value);
            }

            return color;
        }
    }
}
#endif
