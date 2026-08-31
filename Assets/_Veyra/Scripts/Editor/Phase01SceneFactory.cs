#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Veyra.UI;

namespace Veyra.Editor
{
    internal static class Phase01SceneFactory
    {
        private const string ScenePath = "Assets/_Veyra/Scenes/SCN_BattlePrototype.unity";

        private static readonly Color BackgroundPanel = new Color32(12, 22, 41, 222);
        private static readonly Color SecondaryPanel = new Color32(30, 42, 68, 232);
        private static readonly Color Cyan = new Color32(68, 209, 223, 255);
        private static readonly Color Gold = new Color32(245, 190, 78, 255);
        private static readonly Color White = new Color32(235, 243, 248, 255);

        internal static void CreateScene(Phase01SetupReport report)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                report.Preserve(ScenePath);
                EnsureSceneInBuildSettings(report);
                return;
            }

            GameObject heroPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Phase01PlaceholderFactory.HeroPrefabPath);
            GameObject enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Phase01PlaceholderFactory.EnemyPrefabPath);
            Sprite backgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Phase01PlaceholderFactory.BackgroundSpritePath);

            if (heroPrefab == null || enemyPrefab == null || backgroundSprite == null)
            {
                throw new InvalidOperationException("Phase 01 prefabs or background sprite are unavailable.");
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject sceneRoot = new GameObject("SCN_BattlePrototype");
            CreateCamera(sceneRoot.transform);
            CreateWorld(sceneRoot.transform, heroPrefab, enemyPrefab, backgroundSprite);
            CreateUi(sceneRoot.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException($"Could not save scene: {ScenePath}");
            }

            report.Create(ScenePath);
            EnsureSceneInBuildSettings(report);
        }

        private static void CreateCamera(Transform parent)
        {
            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(parent, false);
            cameraObject.transform.localPosition = new Vector3(0f, 0f, -10f);

            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 9.6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(8, 13, 28, 255);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
        }

        private static void CreateWorld(
            Transform parent,
            GameObject heroPrefab,
            GameObject enemyPrefab,
            Sprite backgroundSprite)
        {
            GameObject worldRoot = CreateGameObject("WorldRoot", parent);

            GameObject background = new GameObject("Background", typeof(SpriteRenderer));
            background.transform.SetParent(worldRoot.transform, false);
            SpriteRenderer backgroundRenderer = background.GetComponent<SpriteRenderer>();
            backgroundRenderer.sprite = backgroundSprite;
            backgroundRenderer.sortingLayerName = "Default";
            backgroundRenderer.sortingOrder = -100;

            GameObject heroSlot = CreateGameObject("HeroSlot", worldRoot.transform);
            heroSlot.transform.localPosition = new Vector3(-2.2f, -3.8f, 0f);
            InstantiatePrefab(heroPrefab, heroSlot.transform);

            GameObject enemySlot = CreateGameObject("EnemySlot", worldRoot.transform);
            enemySlot.transform.localPosition = new Vector3(2.1f, 1.6f, 0f);
            InstantiatePrefab(enemyPrefab, enemySlot.transform);
        }

        private static void InstantiatePrefab(GameObject prefab, Transform parent)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException($"Could not instantiate prefab: {prefab.name}");
            }

            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
        }

        private static void CreateUi(Transform parent)
        {
            GameObject uiRoot = CreateGameObject("UIRoot", parent);
            GameObject canvasObject = new GameObject(
                "Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(uiRoot.transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform safeArea = CreateRectTransform("SafeArea", canvasObject.transform);
            Stretch(safeArea, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            safeArea.gameObject.AddComponent<SafeAreaFitter>();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                throw new InvalidOperationException("Unity's built-in LegacyRuntime font could not be loaded.");
            }

            CreatePanel(
                "TopHUD",
                safeArea,
                new Vector2(0.04f, 0.91f),
                new Vector2(0.96f, 0.98f),
                BackgroundPanel,
                "TXT_TopHUD",
                "VEYRA  //  WORLD 01",
                38,
                font,
                TextAnchor.MiddleCenter,
                Cyan);

            CreatePanel(
                "EnemyPanel",
                safeArea,
                new Vector2(0.04f, 0.78f),
                new Vector2(0.96f, 0.89f),
                SecondaryPanel,
                "TXT_EnemyPanel",
                "ENEMY 01\nCORRUPTION // PLACEHOLDER",
                30,
                font,
                TextAnchor.MiddleLeft,
                White);

            CreatePanel(
                "IntentPanel",
                safeArea,
                new Vector2(0.12f, 0.69f),
                new Vector2(0.88f, 0.75f),
                new Color32(68, 31, 66, 232),
                "TXT_IntentPanel",
                "INTENT // UNKNOWN",
                28,
                font,
                TextAnchor.MiddleCenter,
                Gold);

            CreatePanel(
                "HeroPanel",
                safeArea,
                new Vector2(0.04f, 0.22f),
                new Vector2(0.96f, 0.34f),
                SecondaryPanel,
                "TXT_HeroPanel",
                "HERO 01\nSTATUS // PROTOTYPE",
                30,
                font,
                TextAnchor.MiddleLeft,
                White);

            CreatePanel(
                "FocusPanel",
                safeArea,
                new Vector2(0.04f, 0.15f),
                new Vector2(0.96f, 0.20f),
                new Color32(15, 57, 74, 235),
                "TXT_FocusPanel",
                "FOCUS // NOT IMPLEMENTED",
                26,
                font,
                TextAnchor.MiddleCenter,
                Cyan);

            RectTransform actionBar = CreatePanel(
                "ActionBar",
                safeArea,
                new Vector2(0.02f, 0.02f),
                new Vector2(0.98f, 0.13f),
                BackgroundPanel,
                null,
                null,
                0,
                font,
                TextAnchor.MiddleCenter,
                White);

            CreateButton("BTN_Attack", "ATTACK", 0, actionBar, font);
            CreateButton("BTN_Guard", "GUARD", 1, actionBar, font);
            CreateButton("BTN_Technique", "TECHNIQUE", 2, actionBar, font);
            CreateButton("BTN_Mark", "MARK", 3, actionBar, font);
        }

        private static RectTransform CreatePanel(
            string name,
            RectTransform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color color,
            string textName,
            string content,
            int fontSize,
            Font font,
            TextAnchor alignment,
            Color textColor)
        {
            RectTransform panel = CreateRectTransform(name, parent);
            Stretch(panel, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            Image image = panel.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            if (!string.IsNullOrEmpty(textName))
            {
                RectTransform textRect = CreateRectTransform(textName, panel);
                Stretch(textRect, Vector2.zero, Vector2.one, new Vector2(28f, 12f), new Vector2(-28f, -12f));
                Text text = textRect.gameObject.AddComponent<Text>();
                text.font = font;
                text.text = content;
                text.fontSize = fontSize;
                text.alignment = alignment;
                text.color = textColor;
                text.raycastTarget = false;
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = 16;
                text.resizeTextMaxSize = fontSize;
            }

            return panel;
        }

        private static void CreateButton(
            string name,
            string label,
            int index,
            RectTransform parent,
            Font font)
        {
            float segment = 0.25f;
            Vector2 anchorMin = new Vector2(index * segment + 0.01f, 0.12f);
            Vector2 anchorMax = new Vector2((index + 1) * segment - 0.01f, 0.88f);
            RectTransform buttonRect = CreateRectTransform(name, parent);
            Stretch(buttonRect, anchorMin, anchorMax, Vector2.zero, Vector2.zero);

            Image image = buttonRect.gameObject.AddComponent<Image>();
            image.color = index == 0 ? new Color32(126, 49, 73, 255) : new Color32(33, 73, 91, 255);
            Button button = buttonRect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;

            RectTransform textRect = CreateRectTransform("TXT_" + name.Substring(4), buttonRect);
            Stretch(textRect, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));
            Text text = textRect.gameObject.AddComponent<Text>();
            text.font = font;
            text.text = label;
            text.fontSize = 25;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = White;
            text.raycastTarget = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 13;
            text.resizeTextMaxSize = 25;
        }

        private static GameObject CreateGameObject(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static RectTransform CreateRectTransform(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject.GetComponent<RectTransform>();
        }

        private static void Stretch(
            RectTransform rectTransform,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = offsetMin;
            rectTransform.offsetMax = offsetMax;
            rectTransform.localScale = Vector3.one;
        }

        private static void EnsureSceneInBuildSettings(Phase01SetupReport report)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            if (scenes.Any(scene => scene.path == ScenePath))
            {
                report.Preserve("Build Settings entry: " + ScenePath);
                return;
            }

            EditorBuildSettings.scenes = scenes
                .Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) })
                .ToArray();
            report.Configure("Build Settings entry: " + ScenePath);
        }
    }
}
#endif
