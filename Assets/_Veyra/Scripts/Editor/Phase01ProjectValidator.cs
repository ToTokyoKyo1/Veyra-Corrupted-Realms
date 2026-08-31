#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Veyra.UI;

namespace Veyra.Editor
{
    [InitializeOnLoad]
    public static class Phase01ProjectValidator
    {
        private const string ScenePath = "Assets/_Veyra/Scenes/SCN_BattlePrototype.unity";
        private const string ValidationStateKey = "Veyra.Phase01.ValidationState";
        private const string HierarchyKey = "Veyra.Phase01.Hierarchy";
        private const string DependencyHashKey = "Veyra.Phase01.DependencyHash";
        private const string FailureKey = "Veyra.Phase01.Failure";
        private const string WaitingForPlay = "WaitingForPlay";
        private const string WaitingForEdit = "WaitingForEdit";

        private static readonly string[] RequiredHierarchyPaths =
        {
            "Main Camera",
            "WorldRoot",
            "WorldRoot/Background",
            "WorldRoot/HeroSlot",
            "WorldRoot/HeroSlot/PF_Hero01_Placeholder",
            "WorldRoot/EnemySlot",
            "WorldRoot/EnemySlot/PF_W01_Enemy01_Placeholder",
            "UIRoot",
            "UIRoot/Canvas",
            "UIRoot/Canvas/SafeArea",
            "UIRoot/Canvas/SafeArea/TopHUD",
            "UIRoot/Canvas/SafeArea/EnemyPanel",
            "UIRoot/Canvas/SafeArea/IntentPanel",
            "UIRoot/Canvas/SafeArea/HeroPanel",
            "UIRoot/Canvas/SafeArea/FocusPanel",
            "UIRoot/Canvas/SafeArea/ActionBar",
            "UIRoot/Canvas/SafeArea/ActionBar/BTN_Attack",
            "UIRoot/Canvas/SafeArea/ActionBar/BTN_Guard",
            "UIRoot/Canvas/SafeArea/ActionBar/BTN_Technique",
            "UIRoot/Canvas/SafeArea/ActionBar/BTN_Mark"
        };

        private static readonly Vector2Int[] ReferenceLayoutSizes =
        {
            new Vector2Int(360, 640),
            new Vector2Int(390, 844),
            new Vector2Int(412, 915)
        };

        static Phase01ProjectValidator()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("Tools/Veyra/Phase 01/Validate Project Foundation", priority = 101)]
        public static void ValidateProjectFoundation()
        {
            List<string> errors = ValidateEditModeFoundation();
            CompleteValidation(errors, "Edit Mode validation");
        }

        [MenuItem("Tools/Veyra/Phase 01/Validate With Play Mode", priority = 102)]
        public static void ValidateWithPlayMode()
        {
            ClearSession();
            List<string> errors = ValidateEditModeFoundation();
            if (errors.Count > 0)
            {
                CompleteValidation(errors, "Pre-Play validation");
                ExitBatchMode(1);
                return;
            }

            GameObject root = GetSceneRoot();
            SessionState.SetString(HierarchyKey, BuildHierarchySignature(root));
            SessionState.SetString(DependencyHashKey, AssetDatabase.GetAssetDependencyHash(ScenePath).ToString());
            SessionState.SetString(ValidationStateKey, WaitingForPlay);
            Debug.Log("[Veyra Phase 01 Validation] Pre-Play checks passed. Entering Play Mode.");
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            string validationState = SessionState.GetString(ValidationStateKey, string.Empty);
            if (state == PlayModeStateChange.EnteredPlayMode && validationState == WaitingForPlay)
            {
                List<string> errors = ValidateLoadedScene();
                CompareHierarchy(errors);

                if (errors.Count > 0)
                {
                    SessionState.SetString(FailureKey, string.Join("\n", errors));
                }

                SessionState.SetString(ValidationStateKey, WaitingForEdit);
                EditorApplication.ExitPlaymode();
                return;
            }

            if (state == PlayModeStateChange.EnteredEditMode && validationState == WaitingForEdit)
            {
                List<string> errors = ValidateEditModeFoundation();
                CompareHierarchy(errors);

                string expectedHash = SessionState.GetString(DependencyHashKey, string.Empty);
                string currentHash = AssetDatabase.GetAssetDependencyHash(ScenePath).ToString();
                if (!string.Equals(expectedHash, currentHash, StringComparison.Ordinal))
                {
                    errors.Add("The scene dependency hash changed while entering or leaving Play Mode.");
                }

                string playModeFailure = SessionState.GetString(FailureKey, string.Empty);
                if (!string.IsNullOrEmpty(playModeFailure))
                {
                    errors.Add("Play Mode checks failed:\n" + playModeFailure);
                }

                ClearSession();
                CompleteValidation(errors, "Edit/Play/Edit validation");
                ExitBatchMode(errors.Count == 0 ? 0 : 1);
            }
        }

        private static List<string> ValidateEditModeFoundation()
        {
            List<string> errors = new List<string>();

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                errors.Add("Missing scene asset: " + ScenePath);
                return errors;
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            errors.AddRange(ValidateLoadedScene());
            ValidatePrefabs(errors);
            ValidateSpriteImporters(errors);
            ValidatePlayerSettings(errors);

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/SampleScene.unity") == null)
            {
                errors.Add("The original SampleScene is missing.");
            }

            string[] forbiddenWorlds = Enumerable.Range(2, 9).Select(index => $"World{index:00}").ToArray();
            string veyraRoot = Path.Combine(Application.dataPath, "_Veyra");
            foreach (string folder in Directory.EnumerateDirectories(veyraRoot, "*", SearchOption.AllDirectories))
            {
                string folderName = Path.GetFileName(folder);
                if (forbiddenWorlds.Contains(folderName))
                {
                    errors.Add("Out-of-scope folder found: " + folderName);
                }
            }

            return errors;
        }

        private static List<string> ValidateLoadedScene()
        {
            List<string> errors = new List<string>();
            GameObject root = GetSceneRoot();
            if (root == null)
            {
                errors.Add("The scene root SCN_BattlePrototype is missing.");
                return errors;
            }

            foreach (string path in RequiredHierarchyPaths)
            {
                if (root.transform.Find(path) == null)
                {
                    errors.Add("Missing hierarchy path: SCN_BattlePrototype/" + path);
                }
            }

            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                if (missingCount > 0)
                {
                    errors.Add($"{transform.name} has {missingCount} missing script(s).");
                }
            }

            Canvas canvas = root.GetComponentInChildren<Canvas>(true);
            CanvasScaler scaler = root.GetComponentInChildren<CanvasScaler>(true);
            SafeAreaFitter safeAreaFitter = root.GetComponentInChildren<SafeAreaFitter>(true);

            if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                errors.Add("Canvas is missing or is not Screen Space Overlay.");
            }

            if (scaler == null ||
                scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize ||
                scaler.referenceResolution != new Vector2(1080f, 1920f) ||
                scaler.screenMatchMode != CanvasScaler.ScreenMatchMode.MatchWidthOrHeight ||
                !Mathf.Approximately(scaler.matchWidthOrHeight, 0.5f))
            {
                errors.Add("CanvasScaler does not match the required 1080 x 1920 configuration.");
            }

            if (safeAreaFitter == null || safeAreaFitter.gameObject.name != "SafeArea")
            {
                errors.Add("SafeAreaFitter is missing from SafeArea.");
            }

            ValidateReferenceLayouts(root, errors);

            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                if (button.onClick.GetPersistentEventCount() != 0)
                {
                    errors.Add(button.name + " must not have a Phase 1 click handler.");
                }

                if (button.targetGraphic == null)
                {
                    errors.Add(button.name + " has a missing target Graphic reference.");
                }
            }

            foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.sprite == null)
                {
                    errors.Add(renderer.name + " has a missing Sprite reference.");
                }
            }

            foreach (Text text in root.GetComponentsInChildren<Text>(true))
            {
                if (text.font == null)
                {
                    errors.Add(text.name + " has a missing Font reference.");
                }
            }

            return errors;
        }

        private static void ValidateReferenceLayouts(GameObject root, List<string> errors)
        {
            string[] layoutPaths =
            {
                "UIRoot/Canvas/SafeArea/TopHUD",
                "UIRoot/Canvas/SafeArea/EnemyPanel",
                "UIRoot/Canvas/SafeArea/IntentPanel",
                "UIRoot/Canvas/SafeArea/HeroPanel",
                "UIRoot/Canvas/SafeArea/FocusPanel",
                "UIRoot/Canvas/SafeArea/ActionBar",
                "UIRoot/Canvas/SafeArea/ActionBar/BTN_Attack",
                "UIRoot/Canvas/SafeArea/ActionBar/BTN_Guard",
                "UIRoot/Canvas/SafeArea/ActionBar/BTN_Technique",
                "UIRoot/Canvas/SafeArea/ActionBar/BTN_Mark"
            };

            foreach (Vector2Int screenSize in ReferenceLayoutSizes)
            {
                float widthScale = screenSize.x / 1080f;
                float heightScale = screenSize.y / 1920f;
                float canvasScale = Mathf.Sqrt(widthScale * heightScale);
                Rect canvasRect = new Rect(0f, 0f, screenSize.x / canvasScale, screenSize.y / canvasScale);

                foreach (string path in layoutPaths)
                {
                    RectTransform rectTransform = root.transform.Find(path) as RectTransform;
                    if (rectTransform == null)
                    {
                        continue;
                    }

                    Rect calculatedRect = CalculateRectInCanvas(rectTransform, canvasRect);
                    if (calculatedRect.width <= 0f || calculatedRect.height <= 0f ||
                        calculatedRect.xMin < -0.01f || calculatedRect.yMin < -0.01f ||
                        calculatedRect.xMax > canvasRect.width + 0.01f ||
                        calculatedRect.yMax > canvasRect.height + 0.01f)
                    {
                        errors.Add($"{path} is outside the layout at {screenSize.x} x {screenSize.y}.");
                    }
                }
            }
        }

        private static Rect CalculateRectInCanvas(RectTransform target, Rect canvasRect)
        {
            Stack<RectTransform> chain = new Stack<RectTransform>();
            Transform current = target;
            while (current is RectTransform currentRect && currentRect.name != "Canvas")
            {
                chain.Push(currentRect);
                current = current.parent;
            }

            Rect rect = canvasRect;
            while (chain.Count > 0)
            {
                RectTransform child = chain.Pop();
                float xMin = rect.xMin + rect.width * child.anchorMin.x + child.offsetMin.x;
                float yMin = rect.yMin + rect.height * child.anchorMin.y + child.offsetMin.y;
                float xMax = rect.xMin + rect.width * child.anchorMax.x + child.offsetMax.x;
                float yMax = rect.yMin + rect.height * child.anchorMax.y + child.offsetMax.y;
                rect = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            }

            return rect;
        }

        private static void ValidatePrefabs(List<string> errors)
        {
            ValidatePrefab(Phase01PlaceholderFactory.HeroPrefabPath, "PF_Hero01_Placeholder", 10, errors);
            ValidatePrefab(Phase01PlaceholderFactory.EnemyPrefabPath, "PF_W01_Enemy01_Placeholder", 20, errors);
        }

        private static void ValidatePrefab(string path, string expectedName, int sortingOrder, List<string> errors)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                errors.Add("Missing prefab: " + path);
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (contents.name != expectedName)
                {
                    errors.Add(path + " has an unexpected root name.");
                }

                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(contents) > 0)
                {
                    errors.Add(path + " contains a missing script.");
                }

                SpriteRenderer renderer = contents.GetComponent<SpriteRenderer>();
                if (renderer == null || renderer.sprite == null || renderer.sortingOrder != sortingOrder)
                {
                    errors.Add(path + " has an invalid SpriteRenderer configuration.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void ValidateSpriteImporters(List<string> errors)
        {
            ValidateSpriteImporter(Phase01PlaceholderFactory.HeroSpritePath, 32f, new Vector2(0.5f, 0f), errors);
            ValidateSpriteImporter(Phase01PlaceholderFactory.EnemySpritePath, 32f, new Vector2(0.5f, 0f), errors);
            ValidateSpriteImporter(Phase01PlaceholderFactory.BackgroundSpritePath, 10f, new Vector2(0.5f, 0.5f), errors);
        }

        private static void ValidateSpriteImporter(
            string path,
            float pixelsPerUnit,
            Vector2 pivot,
            List<string> errors)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                errors.Add("Missing TextureImporter: " + path);
                return;
            }

            TextureImporterSettings textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);

            if (importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single ||
                !Mathf.Approximately(importer.spritePixelsPerUnit, pixelsPerUnit) ||
                importer.spritePivot != pivot ||
                textureSettings.spriteMeshType != SpriteMeshType.FullRect ||
                importer.filterMode != FilterMode.Point ||
                importer.textureCompression != TextureImporterCompression.Uncompressed ||
                importer.mipmapEnabled)
            {
                errors.Add("Invalid sprite import settings: " + path);
            }
        }

        private static void ValidatePlayerSettings(List<string> errors)
        {
            if (PlayerSettings.productName != "Veyra: Corrupted Realms" ||
                PlayerSettings.companyName != "TokyoKyo" ||
                PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android) !=
                "com.totokyokyo.veyra")
            {
                errors.Add("Product, company, or Android application identifier is incorrect.");
            }

            if (PlayerSettings.defaultInterfaceOrientation != UIOrientation.Portrait ||
                !PlayerSettings.allowedAutorotateToPortrait ||
                PlayerSettings.allowedAutorotateToPortraitUpsideDown ||
                PlayerSettings.allowedAutorotateToLandscapeLeft ||
                PlayerSettings.allowedAutorotateToLandscapeRight)
            {
                errors.Add("Portrait orientation settings are incorrect.");
            }

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                errors.Add("Android is not the active build target.");
            }
        }

        private static GameObject GetSceneRoot()
        {
            Scene scene = SceneManager.GetActiveScene();
            return scene.GetRootGameObjects().FirstOrDefault(gameObject => gameObject.name == "SCN_BattlePrototype");
        }

        private static string BuildHierarchySignature(GameObject root)
        {
            if (root == null)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            AppendHierarchy(root.transform, builder, 0);
            return builder.ToString();
        }

        private static void AppendHierarchy(Transform transform, StringBuilder builder, int depth)
        {
            builder.Append(depth).Append(':').Append(transform.name).Append('[');
            foreach (Component component in transform.GetComponents<Component>())
            {
                builder.Append(component == null ? "Missing" : component.GetType().FullName).Append(',');
            }

            builder.AppendLine("]");
            for (int index = 0; index < transform.childCount; index++)
            {
                AppendHierarchy(transform.GetChild(index), builder, depth + 1);
            }
        }

        private static void CompareHierarchy(List<string> errors)
        {
            string expected = SessionState.GetString(HierarchyKey, string.Empty);
            string actual = BuildHierarchySignature(GetSceneRoot());
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                errors.Add("The scene hierarchy changed during the Play Mode verification.");
            }
        }

        private static void CompleteValidation(IReadOnlyCollection<string> errors, string label)
        {
            if (errors.Count == 0)
            {
                Debug.Log($"[Veyra Phase 01 Validation] {label} passed with no Missing Script or Missing Reference.");
                return;
            }

            string message = $"[Veyra Phase 01 Validation] {label} failed:\n- " + string.Join("\n- ", errors);
            Debug.LogError(message);
        }

        private static void ExitBatchMode(int exitCode)
        {
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
        }

        private static void ClearSession()
        {
            SessionState.EraseString(ValidationStateKey);
            SessionState.EraseString(HierarchyKey);
            SessionState.EraseString(DependencyHashKey);
            SessionState.EraseString(FailureKey);
        }
    }
}
#endif
