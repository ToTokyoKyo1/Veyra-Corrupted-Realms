#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Veyra.Combat.Preview;
using Veyra.UI;
using Veyra.UI.MainMenu;
using Veyra.UI.Settings;

namespace Veyra.Editor
{
    [InitializeOnLoad]
    public static class Phase02Validator
    {
        private const string StateKey = "Veyra.Phase02.Validation.State";
        private const string FailureKey = "Veyra.Phase02.Validation.Failure";
        private const string MainSignatureKey = "Veyra.Phase02.Validation.MainSignature";
        private const string TutorialSignatureKey = "Veyra.Phase02.Validation.TutorialSignature";
        private const string MainHashKey = "Veyra.Phase02.Validation.MainHash";
        private const string TutorialHashKey = "Veyra.Phase02.Validation.TutorialHash";
        private const string SettingsSnapshotKey = "Veyra.Phase02.Validation.SettingsSnapshot";
        private const string SettingsPresenceKey = "Veyra.Phase02.Validation.SettingsPresence";
        private const string RestartSnapshotPath = "Library/VeyraPhase02SettingsRestartSnapshot.json";

        private const string WaitingForPlay = "WaitingForPlay";
        private const string MainDelay = "MainDelay";
        private const string SettingsReopen = "SettingsReopen";
        private const string WaitTutorial = "WaitTutorial";
        private const string TutorialDelay = "TutorialDelay";
        private const string WaitAttack = "WaitAttack";
        private const string WaitGuard = "WaitGuard";
        private const string WaitTechnique = "WaitTechnique";
        private const string WaitMark = "WaitMark";
        private const string WaitMainReturn = "WaitMainReturn";
        private const string MainReturnDelay = "MainReturnDelay";
        private const string WaitingForEdit = "WaitingForEdit";

        private static readonly string[] MainHierarchy =
        {
            "Main Camera",
            "UIRoot",
            "UIRoot/Canvas",
            "UIRoot/Canvas/SafeArea",
            "UIRoot/Canvas/SafeArea/BackgroundLayers",
            "UIRoot/Canvas/SafeArea/TitleArea",
            "UIRoot/Canvas/SafeArea/HeroPreview",
            "UIRoot/Canvas/SafeArea/StartCard",
            "UIRoot/Canvas/SafeArea/Footer",
            "UIRoot/Canvas/SafeArea/Dimmer",
            "UIRoot/Canvas/SafeArea/SettingsModal",
            "UIRoot/Canvas/SafeArea/LoadingOverlay",
            "UIRoot/Canvas/SafeArea/ErrorModal",
            "EventSystem"
        };

        private static readonly string[] TutorialHierarchy =
        {
            "Main Camera",
            "BattlePreviewRoot",
            "BattlePreviewRoot/Background",
            "BattlePreviewRoot/HeroSlot",
            "BattlePreviewRoot/HeroSlot/HeroVisual",
            "BattlePreviewRoot/HeroSlot/HeroProjectileOrigin",
            "BattlePreviewRoot/HeroSlot/HeroHitTarget",
            "BattlePreviewRoot/HeroSlot/GuardVisual",
            "BattlePreviewRoot/EnemySlot",
            "BattlePreviewRoot/EnemySlot/EnemyVisual",
            "BattlePreviewRoot/EnemySlot/EnemyProjectileOrigin",
            "BattlePreviewRoot/EnemySlot/EnemyHitTarget",
            "BattlePreviewRoot/EnemySlot/MarkPreview",
            "BattlePreviewRoot/PreviewEffects",
            "BattlePreviewRoot/PreviewEffects/HeroBasicProjectile",
            "BattlePreviewRoot/PreviewEffects/HeroTechniqueProjectile",
            "BattlePreviewRoot/PreviewEffects/EnemyProjectile",
            "UIRoot",
            "UIRoot/Canvas",
            "UIRoot/Canvas/SafeArea",
            "UIRoot/Canvas/SafeArea/EnemyPanel",
            "UIRoot/Canvas/SafeArea/IntentPanel",
            "UIRoot/Canvas/SafeArea/CombatMessage",
            "UIRoot/Canvas/SafeArea/HeroPanel",
            "UIRoot/Canvas/SafeArea/FocusPanel",
            "UIRoot/Canvas/SafeArea/ActionBar",
            "UIRoot/Canvas/SafeArea/BTN_BackToMenu",
            "EventSystem"
        };

        private static readonly Vector2Int[] ReferenceLayoutSizes =
        {
            new Vector2Int(360, 640),
            new Vector2Int(390, 844),
            new Vector2Int(412, 915)
        };

        private static int frameCounter;
        private static double stepStartedAt;

        static Phase02Validator()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        [MenuItem("Tools/Veyra/Phase 02/Validate Phase 02", priority = 201)]
        public static void ValidatePhase02()
        {
            if (!PrepareToOpenValidationScenes())
            {
                return;
            }

            List<string> errors = ValidateEditModePhase02();
            CompleteValidation(errors, "validazione Edit Mode");
            ExitBatchMode(errors.Count == 0 ? 0 : 1);
        }

        [MenuItem("Tools/Veyra/Phase 02/Validate Phase 02 With Play Mode", priority = 202)]
        public static void ValidatePhase02WithPlayMode()
        {
            if (!PrepareToOpenValidationScenes())
            {
                ExitBatchMode(1);
                return;
            }

            ClearSession();
            List<string> errors = ValidateEditModePhase02();
            if (errors.Count > 0)
            {
                CompleteValidation(errors, "validazione pre-Play");
                ExitBatchMode(1);
                return;
            }

            StoreSceneSnapshot(
                Phase02SceneFactory.MainMenuScenePath,
                "SCN_MainMenu",
                MainSignatureKey,
                MainHashKey);
            StoreSceneSnapshot(
                Phase02SceneFactory.TutorialScenePath,
                "SCN_W01_L01_Tutorial",
                TutorialSignatureKey,
                TutorialHashKey);
            StoreSettingsSnapshot();

            EditorSceneManager.OpenScene(Phase02SceneFactory.MainMenuScenePath, OpenSceneMode.Single);
            SessionState.SetString(StateKey, WaitingForPlay);
            Debug.Log("[Veyra Phase 02 Validation] Controlli pre-Play superati. Avvio del flusso Menu → Tutorial Draft → Menu.");
            EditorApplication.EnterPlaymode();
        }

        public static void WriteSettingsRestartProbe()
        {
            RestartSettingsSnapshot snapshot = new RestartSettingsSnapshot
            {
                values = LocalSettingsStore.Load(),
                presence = GetSettingsPresenceMask()
            };
            File.WriteAllText(Path.GetFullPath(RestartSnapshotPath), JsonUtility.ToJson(snapshot));

            LocalSettingsStore.Save(new LocalSettingsStore.Values
            {
                version = LocalSettingsStore.CurrentVersion,
                masterVolume = 0.29f,
                musicVolume = 0.43f,
                sfxVolume = 0.71f,
                vibrationEnabled = false
            });
            Debug.Log("[Veyra Phase 02 Validation] Probe PlayerPrefs scritto; chiusura Unity per la verifica dopo riavvio.");
            ExitBatchMode(0);
        }

        public static void VerifySettingsRestartProbeAndRestore()
        {
            string absolutePath = Path.GetFullPath(RestartSnapshotPath);
            if (!File.Exists(absolutePath))
            {
                Debug.LogError("[Veyra Phase 02 Validation] Snapshot del probe PlayerPrefs mancante.");
                ExitBatchMode(1);
                return;
            }

            RestartSettingsSnapshot snapshot = JsonUtility.FromJson<RestartSettingsSnapshot>(File.ReadAllText(absolutePath));
            try
            {
                LocalSettingsStore.Values loaded = LocalSettingsStore.Load();
                if (!Mathf.Approximately(loaded.masterVolume, 0.29f) ||
                    !Mathf.Approximately(loaded.musicVolume, 0.43f) ||
                    !Mathf.Approximately(loaded.sfxVolume, 0.71f) ||
                    loaded.vibrationEnabled)
                {
                    throw new InvalidOperationException("PlayerPrefs non ha mantenuto i valori nel nuovo processo Unity.");
                }

                RestoreSettings(snapshot.values, snapshot.presence);
                File.Delete(absolutePath);
                Debug.Log("[Veyra Phase 02 Validation] Persistenza PlayerPrefs dopo riavvio superata; valori originali ripristinati.");
                ExitBatchMode(0);
            }
            catch (Exception exception)
            {
                RestoreSettings(snapshot.values, snapshot.presence);
                File.Delete(absolutePath);
                Debug.LogError("[Veyra Phase 02 Validation] Verifica PlayerPrefs dopo riavvio fallita: " + exception.Message);
                ExitBatchMode(1);
            }
        }

        private static bool PrepareToOpenValidationScenes()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isDirty)
            {
                return true;
            }

            if (Application.isBatchMode)
            {
                Debug.LogError("[Veyra Phase 02 Validation] La scena attiva ha modifiche non salvate.");
                return false;
            }

            return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        }

        private static List<string> ValidateEditModePhase02()
        {
            List<string> errors = new List<string>();

            Phase02MenuBattleSetup.VerifyPhase01Prerequisites();
            ValidateAssets(errors);
            ValidateBuildSettings(errors);

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Phase02SceneFactory.MainMenuScenePath) != null)
            {
                EditorSceneManager.OpenScene(Phase02SceneFactory.MainMenuScenePath, OpenSceneMode.Single);
                ValidateMainMenuScene(errors);
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Phase02SceneFactory.TutorialScenePath) != null)
            {
                EditorSceneManager.OpenScene(Phase02SceneFactory.TutorialScenePath, OpenSceneMode.Single);
                ValidateTutorialScene(errors);
            }

            ValidateRuntimeSource(errors);
            return errors;
        }

        private static void ValidateAssets(List<string> errors)
        {
            string[] scenes =
            {
                Phase02SceneFactory.MainMenuScenePath,
                Phase02SceneFactory.TutorialScenePath
            };
            foreach (string path in scenes)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                {
                    errors.Add("Scena mancante: " + path);
                }
            }

            string[] prefabs = Phase02PrototypeAssetFactory.RequiredPrefabPaths
                .Concat(new[]
                {
                    Phase02SceneFactory.MainMenuStartCardPrefabPath,
                    Phase02SceneFactory.SettingsModalPrefabPath,
                    Phase02SceneFactory.BattleActionBarPrefabPath
                })
                .ToArray();
            foreach (string path in prefabs)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    errors.Add("Prefab mancante: " + path);
                    continue;
                }

                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    ValidateMissingScripts(contents, path, errors);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            foreach (string path in Phase02PrototypeAssetFactory.RequiredSpritePaths)
            {
                ValidateSpriteImporter(path, errors);
            }

            if (AssetDatabase.LoadAssetAtPath<Sprite>(Phase02PrototypeAssetFactory.MenuBackgroundPath) == null)
            {
                errors.Add("Sprite provvisorio del menu mancante: " + Phase02PrototypeAssetFactory.MenuBackgroundPath);
            }

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Phase02PrototypeAssetFactory.FontAssetPath);
            if (font == null || font.material == null || font.atlasTextures == null || font.atlasTextures.Length == 0 || font.atlasTextures[0] == null)
            {
                errors.Add("Font TMP persistente mancante o incompleto: " + Phase02PrototypeAssetFactory.FontAssetPath);
            }
        }

        private static void ValidateBuildSettings(List<string> errors)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            if (scenes.Length < 2 || scenes[0].path != Phase02SceneFactory.MainMenuScenePath || !scenes[0].enabled)
            {
                errors.Add("SCN_MainMenu deve essere la prima scena abilitata nel profilo di build.");
            }

            if (scenes.Length < 2 || scenes[1].path != Phase02SceneFactory.TutorialScenePath || !scenes[1].enabled)
            {
                errors.Add("SCN_W01_L01_Tutorial deve essere la seconda scena abilitata nel profilo di build.");
            }

            if (scenes.All(scene => scene.path != "Assets/Scenes/SampleScene.unity"))
            {
                errors.Add("SampleScene non è stata conservata nel profilo di build.");
            }

            if (scenes.All(scene => scene.path != "Assets/_Veyra/Scenes/SCN_BattlePrototype.unity"))
            {
                errors.Add("SCN_BattlePrototype non è stata conservata nel profilo di build.");
            }
        }

        private static void ValidateMainMenuScene(List<string> errors)
        {
            GameObject root = GetRoot("SCN_MainMenu");
            if (root == null)
            {
                errors.Add("Root SCN_MainMenu mancante.");
                return;
            }

            ValidateHierarchy(root, MainHierarchy, errors);
            ValidateCommonSceneRequirements(root, errors);
            ValidateSerializedReferences(
                root.GetComponentInChildren<MainMenuController>(true),
                new[] { "startButton", "settingsButton", "settingsPanel", "loadingOverlay", "errorModal", "errorMessage" },
                errors);
            ValidateSerializedReferences(
                root.GetComponentInChildren<SettingsPanelController>(true),
                new[]
                {
                    "dimmer", "modalRoot", "masterVolumeSlider", "musicVolumeSlider", "sfxVolumeSlider",
                    "vibrationToggle", "masterValueText", "musicValueText", "sfxValueText"
                },
                errors);

            string[] hiddenPaths =
            {
                "UIRoot/Canvas/SafeArea/Dimmer",
                "UIRoot/Canvas/SafeArea/SettingsModal",
                "UIRoot/Canvas/SafeArea/LoadingOverlay",
                "UIRoot/Canvas/SafeArea/ErrorModal"
            };
            foreach (string path in hiddenPaths)
            {
                Transform hidden = root.transform.Find(path);
                if (hidden != null && hidden.gameObject.activeSelf)
                {
                    errors.Add(path + " deve essere inizialmente inattivo e non bloccare i raycast.");
                }
            }

            ValidateButtonListener(root, "UIRoot/Canvas/SafeArea/StartCard/ButtonStack/BTN_Start", errors);
            ValidateButtonListener(root, "UIRoot/Canvas/SafeArea/StartCard/ButtonStack/BTN_Settings", errors);
            ValidateButtonListener(root, "UIRoot/Canvas/SafeArea/SettingsModal/BTN_Reset", errors);
            ValidateButtonListener(root, "UIRoot/Canvas/SafeArea/SettingsModal/BTN_CloseSettings", errors);

            ValidateTouchTarget(root, "UIRoot/Canvas/SafeArea/StartCard/ButtonStack/BTN_Start", 156f, errors);
            ValidateTouchTarget(root, "UIRoot/Canvas/SafeArea/StartCard/ButtonStack/BTN_Settings", 144f, errors);
            ValidateLayouts(
                root,
                new[]
                {
                    "UIRoot/Canvas/SafeArea/TitleArea",
                    "UIRoot/Canvas/SafeArea/HeroPreview",
                    "UIRoot/Canvas/SafeArea/StartCard",
                    "UIRoot/Canvas/SafeArea/Footer",
                    "UIRoot/Canvas/SafeArea/SettingsModal"
                },
                errors);
        }

        private static void ValidateTutorialScene(List<string> errors)
        {
            GameObject root = GetRoot("SCN_W01_L01_Tutorial");
            if (root == null)
            {
                errors.Add("Root SCN_W01_L01_Tutorial mancante.");
                return;
            }

            ValidateHierarchy(root, TutorialHierarchy, errors);
            ValidateCommonSceneRequirements(root, errors);
            ValidateSerializedReferences(
                root.GetComponentInChildren<BattlePreviewController>(true),
                new[]
                {
                    "combatMessage", "heroVisual", "enemyVisual", "heroProjectileOrigin", "heroHitTarget",
                    "enemyProjectileOrigin", "enemyHitTarget", "heroBasicProjectile", "heroTechniqueProjectile",
                    "enemyProjectile", "guardVisual", "markPreview"
                },
                errors,
                "actionButtons",
                4);
            ValidateSerializedReferences(
                root.GetComponentInChildren<BattlePreviewNavigation>(true),
                new[] { "backButton", "previewController" },
                errors);

            string[] buttons = { "BTN_Attack", "BTN_Guard", "BTN_Technique", "BTN_Mark" };
            foreach (string buttonName in buttons)
            {
                string path = "UIRoot/Canvas/SafeArea/ActionBar/" + buttonName;
                ValidateButtonListener(root, path, errors);
                ValidateTouchTarget(root, path, 144f, errors);
            }

            ValidateButtonListener(root, "UIRoot/Canvas/SafeArea/BTN_BackToMenu", errors);

            string[] inactiveEffects =
            {
                "BattlePreviewRoot/HeroSlot/GuardVisual",
                "BattlePreviewRoot/EnemySlot/MarkPreview",
                "BattlePreviewRoot/PreviewEffects/HeroBasicProjectile",
                "BattlePreviewRoot/PreviewEffects/HeroTechniqueProjectile",
                "BattlePreviewRoot/PreviewEffects/EnemyProjectile"
            };
            foreach (string path in inactiveEffects)
            {
                Transform effect = root.transform.Find(path);
                if (effect != null && effect.gameObject.activeSelf)
                {
                    errors.Add("L'effetto persistente deve essere inizialmente inattivo: " + path);
                }
            }

            ValidateLayouts(
                root,
                new[]
                {
                    "UIRoot/Canvas/SafeArea/EnemyPanel",
                    "UIRoot/Canvas/SafeArea/IntentPanel",
                    "UIRoot/Canvas/SafeArea/CombatMessage",
                    "UIRoot/Canvas/SafeArea/HeroPanel",
                    "UIRoot/Canvas/SafeArea/FocusPanel",
                    "UIRoot/Canvas/SafeArea/ActionBar",
                    "UIRoot/Canvas/SafeArea/BTN_BackToMenu"
                },
                errors);
        }

        private static void ValidateCommonSceneRequirements(GameObject root, List<string> errors)
        {
            ValidateMissingScripts(root, root.name, errors);

            EventSystem[] eventSystems = root.GetComponentsInChildren<EventSystem>(true);
            InputSystemUIInputModule[] inputModules = root.GetComponentsInChildren<InputSystemUIInputModule>(true);
            StandaloneInputModule[] legacyModules = root.GetComponentsInChildren<StandaloneInputModule>(true);
            if (eventSystems.Length != 1)
            {
                errors.Add(root.name + " deve contenere esattamente un EventSystem.");
            }

            if (inputModules.Length != 1 || legacyModules.Length != 0)
            {
                errors.Add(root.name + " deve usare un solo InputSystemUIInputModule e nessun modulo legacy.");
            }
            else if (inputModules[0].actionsAsset == null ||
                     inputModules[0].point == null ||
                     inputModules[0].leftClick == null ||
                     inputModules[0].move == null ||
                     inputModules[0].submit == null ||
                     inputModules[0].cancel == null)
            {
                errors.Add(root.name + " ha riferimenti Input System UI mancanti.");
            }

            Canvas[] canvases = root.GetComponentsInChildren<Canvas>(true);
            CanvasScaler[] scalers = root.GetComponentsInChildren<CanvasScaler>(true);
            if (canvases.Length != 1 || canvases[0].renderMode != RenderMode.ScreenSpaceOverlay)
            {
                errors.Add(root.name + " deve contenere un solo Canvas Screen Space Overlay.");
            }

            if (scalers.Length != 1 ||
                scalers[0].uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize ||
                scalers[0].referenceResolution != new Vector2(1080f, 1920f) ||
                scalers[0].screenMatchMode != CanvasScaler.ScreenMatchMode.MatchWidthOrHeight ||
                !Mathf.Approximately(scalers[0].matchWidthOrHeight, 0.5f))
            {
                errors.Add(root.name + " ha un CanvasScaler non conforme a 1080 × 1920, Match 0.5.");
            }

            SafeAreaFitter[] safeAreas = root.GetComponentsInChildren<SafeAreaFitter>(true);
            if (safeAreas.Length != 1 || safeAreas[0].gameObject.name != "SafeArea")
            {
                errors.Add(root.name + " deve contenere un solo SafeAreaFitter su SafeArea.");
            }

            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.font == null || text.fontSharedMaterial == null)
                {
                    errors.Add(root.name + "/" + text.name + " ha un riferimento TMP mancante.");
                }
            }

            foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.sprite == null)
                {
                    errors.Add(root.name + "/" + renderer.name + " ha uno Sprite mancante.");
                }
            }
        }

        private static void ValidateHierarchy(GameObject root, IEnumerable<string> hierarchy, List<string> errors)
        {
            foreach (string path in hierarchy)
            {
                if (root.transform.Find(path) == null)
                {
                    errors.Add("Gerarchia mancante: " + root.name + "/" + path);
                }
            }
        }

        private static void ValidateMissingScripts(GameObject root, string context, List<string> errors)
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                if (count > 0)
                {
                    errors.Add(context + "/" + transform.name + " contiene " + count + " Missing Script.");
                }
            }
        }

        private static void ValidateSerializedReferences(
            MonoBehaviour component,
            IEnumerable<string> propertyNames,
            List<string> errors,
            string arrayPropertyName = null,
            int expectedArraySize = 0)
        {
            if (component == null)
            {
                errors.Add("Controller richiesto mancante.");
                return;
            }

            SerializedObject serialized = new SerializedObject(component);
            foreach (string name in propertyNames)
            {
                SerializedProperty property = serialized.FindProperty(name);
                if (property == null || property.objectReferenceValue == null)
                {
                    errors.Add(component.GetType().Name + "." + name + " non è assegnato.");
                }
            }

            if (!string.IsNullOrEmpty(arrayPropertyName))
            {
                SerializedProperty array = serialized.FindProperty(arrayPropertyName);
                if (array == null || array.arraySize != expectedArraySize)
                {
                    errors.Add(component.GetType().Name + "." + arrayPropertyName + " deve contenere " + expectedArraySize + " riferimenti.");
                }
                else
                {
                    for (int index = 0; index < array.arraySize; index++)
                    {
                        if (array.GetArrayElementAtIndex(index).objectReferenceValue == null)
                        {
                            errors.Add(component.GetType().Name + "." + arrayPropertyName + " contiene un riferimento mancante.");
                        }
                    }
                }
            }
        }

        private static void ValidateButtonListener(GameObject root, string path, List<string> errors)
        {
            Transform transform = root.transform.Find(path);
            Button button = transform == null ? null : transform.GetComponent<Button>();
            if (button == null || button.targetGraphic == null)
            {
                errors.Add("Pulsante mancante o senza Target Graphic: " + path);
                return;
            }

            if (button.onClick.GetPersistentEventCount() != 1)
            {
                errors.Add(path + " deve avere esattamente un listener persistente.");
            }
        }

        private static void ValidateTouchTarget(GameObject root, string path, float minimumHeight, List<string> errors)
        {
            Transform transform = root.transform.Find(path);
            LayoutElement layout = transform == null ? null : transform.GetComponent<LayoutElement>();
            if (layout == null || layout.minHeight < minimumHeight - 0.01f)
            {
                errors.Add(path + " non dichiara un target touch minimo di " + minimumHeight + " px.");
            }
        }

        private static void ValidateSpriteImporter(string path, List<string> errors)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                errors.Add("TextureImporter mancante: " + path);
                return;
            }

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single ||
                !Mathf.Approximately(importer.spritePixelsPerUnit, 32f) ||
                importer.spritePivot != new Vector2(0.5f, 0.5f) ||
                importer.filterMode != FilterMode.Point ||
                importer.textureCompression != TextureImporterCompression.Uncompressed ||
                importer.mipmapEnabled ||
                settings.spriteMeshType != SpriteMeshType.FullRect)
            {
                errors.Add("Impostazioni import sprite non valide: " + path);
            }
        }

        private static void ValidateLayouts(GameObject root, IEnumerable<string> paths, List<string> errors)
        {
            foreach (Vector2Int screenSize in ReferenceLayoutSizes)
            {
                float widthScale = screenSize.x / 1080f;
                float heightScale = screenSize.y / 1920f;
                float canvasScale = Mathf.Sqrt(widthScale * heightScale);
                Rect canvasRect = new Rect(0f, 0f, screenSize.x / canvasScale, screenSize.y / canvasScale);

                foreach (string path in paths)
                {
                    RectTransform rect = root.transform.Find(path) as RectTransform;
                    if (rect == null)
                    {
                        continue;
                    }

                    Rect calculated = CalculateRectInCanvas(rect, canvasRect);
                    if (calculated.width <= 0f || calculated.height <= 0f ||
                        calculated.xMin < -0.01f || calculated.yMin < -0.01f ||
                        calculated.xMax > canvasRect.width + 0.01f ||
                        calculated.yMax > canvasRect.height + 0.01f)
                    {
                        errors.Add(path + " esce dal layout a " + screenSize.x + " × " + screenSize.y + ".");
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

        private static void ValidateRuntimeSource(List<string> errors)
        {
            string[] sourcePaths =
            {
                "Assets/_Veyra/Scripts/Runtime/UI/MainMenu/MainMenuController.cs",
                "Assets/_Veyra/Scripts/Runtime/UI/Settings/LocalSettingsStore.cs",
                "Assets/_Veyra/Scripts/Runtime/UI/Settings/SettingsPanelController.cs",
                "Assets/_Veyra/Scripts/Runtime/Combat/Preview/BattlePreviewController.cs",
                "Assets/_Veyra/Scripts/Runtime/Combat/Preview/BattlePreviewNavigation.cs"
            };
            string[] forbidden =
            {
                "GameObject.Find", "FindObjectOfType", "Resources.Load", "new GameObject", ".AddComponent<", "DontDestroyOnLoad"
            };

            foreach (string path in sourcePaths)
            {
                if (!File.Exists(path))
                {
                    errors.Add("Script runtime mancante: " + path);
                    continue;
                }

                string source = File.ReadAllText(path);
                foreach (string token in forbidden)
                {
                    if (source.Contains(token))
                    {
                        errors.Add(path + " contiene authoring o lookup runtime vietato: " + token);
                    }
                }
            }
        }

        private static void StoreSceneSnapshot(string path, string rootName, string signatureKey, string hashKey)
        {
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            SessionState.SetString(signatureKey, BuildHierarchySignature(GetRoot(rootName)));
            SessionState.SetString(hashKey, AssetDatabase.GetAssetDependencyHash(path).ToString());
        }

        private static void CompareSceneSnapshot(
            string path,
            string rootName,
            string signatureKey,
            string hashKey,
            List<string> errors)
        {
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            string signature = BuildHierarchySignature(GetRoot(rootName));
            if (!string.Equals(SessionState.GetString(signatureKey, string.Empty), signature, StringComparison.Ordinal))
            {
                errors.Add(rootName + " ha cambiato gerarchia entrando o uscendo dal Play Mode.");
            }

            string hash = AssetDatabase.GetAssetDependencyHash(path).ToString();
            if (!string.Equals(SessionState.GetString(hashKey, string.Empty), hash, StringComparison.Ordinal))
            {
                errors.Add(rootName + " ha cambiato dipendenze entrando o uscendo dal Play Mode.");
            }
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
            builder.Append(depth).Append(':').Append(transform.name).Append(':').Append(transform.gameObject.activeSelf).Append('[');
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

        private static GameObject GetRoot(string name)
        {
            return SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(root => root.name == name);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            string state = SessionState.GetString(StateKey, string.Empty);
            if (change == PlayModeStateChange.EnteredPlayMode && state == WaitingForPlay)
            {
                frameCounter = 0;
                stepStartedAt = EditorApplication.timeSinceStartup;
                SessionState.SetString(StateKey, MainDelay);
                return;
            }

            if (change == PlayModeStateChange.EnteredEditMode && state == WaitingForEdit)
            {
                List<string> errors = ValidateEditModePhase02();
                CompareSceneSnapshot(
                    Phase02SceneFactory.MainMenuScenePath, "SCN_MainMenu", MainSignatureKey, MainHashKey, errors);
                CompareSceneSnapshot(
                    Phase02SceneFactory.TutorialScenePath, "SCN_W01_L01_Tutorial", TutorialSignatureKey, TutorialHashKey, errors);

                string playFailure = SessionState.GetString(FailureKey, string.Empty);
                if (!string.IsNullOrEmpty(playFailure))
                {
                    errors.Add("Controlli Play Mode falliti: " + playFailure);
                }

                ClearSession();
                CompleteValidation(errors, "validazione Edit/Play/Edit e flusso completo");
                ExitBatchMode(errors.Count == 0 ? 0 : 1);
            }
        }

        private static void OnEditorUpdate()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            string state = SessionState.GetString(StateKey, string.Empty);
            try
            {
                switch (state)
                {
                    case MainDelay:
                        if (++frameCounter >= 5)
                        {
                            TestSettingsOpenAndSave();
                        }
                        break;
                    case SettingsReopen:
                        TestSettingsPersistenceAndStart();
                        break;
                    case WaitTutorial:
                        WaitForTutorialScene();
                        break;
                    case TutorialDelay:
                        if (++frameCounter >= 5)
                        {
                            BeginBattleAction("BTN_Attack", "BattlePreviewRoot/PreviewEffects/HeroBasicProjectile", WaitAttack);
                        }
                        break;
                    case WaitAttack:
                        WaitForBattleActionToComplete("BTN_Guard", "BattlePreviewRoot/HeroSlot/GuardVisual", WaitGuard);
                        break;
                    case WaitGuard:
                        WaitForBattleActionToComplete("BTN_Technique", "BattlePreviewRoot/PreviewEffects/HeroTechniqueProjectile", WaitTechnique);
                        break;
                    case WaitTechnique:
                        WaitForBattleActionToComplete("BTN_Mark", "BattlePreviewRoot/EnemySlot/MarkPreview", WaitMark);
                        break;
                    case WaitMark:
                        WaitForMarkAndReturn();
                        break;
                    case WaitMainReturn:
                        WaitForMainSceneReturn();
                        break;
                    case MainReturnDelay:
                        if (++frameCounter >= 5)
                        {
                            TestSettingsAfterSceneRoundTripAndExit();
                        }
                        break;
                }
            }
            catch (Exception exception)
            {
                FailPlayValidation(exception.Message);
            }
        }

        private static void TestSettingsOpenAndSave()
        {
            GameObject root = RequirePlayRoot("SCN_MainMenu");
            Button settingsButton = RequireComponent<Button>(root, "UIRoot/Canvas/SafeArea/StartCard/ButtonStack/BTN_Settings");
            settingsButton.onClick.Invoke();

            GameObject modal = RequireTransform(root, "UIRoot/Canvas/SafeArea/SettingsModal").gameObject;
            GameObject dimmer = RequireTransform(root, "UIRoot/Canvas/SafeArea/Dimmer").gameObject;
            if (!modal.activeSelf || !dimmer.activeSelf)
            {
                throw new InvalidOperationException("Il pannello Impostazioni o il Dimmer non si attiva.");
            }

            RequireComponent<Slider>(root, "UIRoot/Canvas/SafeArea/SettingsModal/SLD_Master").value = 0.42f;
            RequireComponent<Slider>(root, "UIRoot/Canvas/SafeArea/SettingsModal/SLD_Music").value = 0.37f;
            RequireComponent<Slider>(root, "UIRoot/Canvas/SafeArea/SettingsModal/SLD_Sfx").value = 0.61f;
            RequireComponent<Toggle>(root, "UIRoot/Canvas/SafeArea/SettingsModal/TGL_Vibration").isOn = false;
            RequireComponent<Button>(root, "UIRoot/Canvas/SafeArea/SettingsModal/BTN_CloseSettings").onClick.Invoke();

            if (modal.activeSelf || dimmer.activeSelf || !Mathf.Approximately(AudioListener.volume, 0.42f))
            {
                throw new InvalidOperationException("La chiusura Impostazioni non ripristina correttamente menu, raycast o volume generale.");
            }

            settingsButton.onClick.Invoke();
            SessionState.SetString(StateKey, SettingsReopen);
        }

        private static void TestSettingsPersistenceAndStart()
        {
            GameObject root = RequirePlayRoot("SCN_MainMenu");
            AssertSlider(root, "UIRoot/Canvas/SafeArea/SettingsModal/SLD_Master", 0.42f);
            AssertSlider(root, "UIRoot/Canvas/SafeArea/SettingsModal/SLD_Music", 0.37f);
            AssertSlider(root, "UIRoot/Canvas/SafeArea/SettingsModal/SLD_Sfx", 0.61f);
            if (RequireComponent<Toggle>(root, "UIRoot/Canvas/SafeArea/SettingsModal/TGL_Vibration").isOn)
            {
                throw new InvalidOperationException("Il valore Vibrazione non viene ricaricato.");
            }

            RequireComponent<Button>(root, "UIRoot/Canvas/SafeArea/SettingsModal/BTN_CloseSettings").onClick.Invoke();
            Button startButton = RequireComponent<Button>(root, "UIRoot/Canvas/SafeArea/StartCard/ButtonStack/BTN_Start");
            startButton.onClick.Invoke();
            startButton.onClick.Invoke();

            if (!RequireTransform(root, "UIRoot/Canvas/SafeArea/LoadingOverlay").gameObject.activeSelf || startButton.interactable)
            {
                throw new InvalidOperationException("INIZIA non mostra subito LoadingOverlay o non blocca il doppio tocco.");
            }

            stepStartedAt = EditorApplication.timeSinceStartup;
            SessionState.SetString(StateKey, WaitTutorial);
        }

        private static void WaitForTutorialScene()
        {
            if (SceneManager.GetActiveScene().name == "SCN_W01_L01_Tutorial")
            {
                frameCounter = 0;
                SessionState.SetString(StateKey, TutorialDelay);
                return;
            }

            EnsureStepNotTimedOut("caricamento scena tutorial", 12d);
        }

        private static void BeginBattleAction(string buttonName, string effectPath, string waitState)
        {
            GameObject root = RequirePlayRoot("SCN_W01_L01_Tutorial");
            Button button = RequireComponent<Button>(root, "UIRoot/Canvas/SafeArea/ActionBar/" + buttonName);
            BattlePreviewController controller = root.GetComponentInChildren<BattlePreviewController>(true);
            button.onClick.Invoke();
            button.onClick.Invoke();

            if (controller == null || !controller.IsPreviewRunning || !RequireTransform(root, effectPath).gameObject.activeSelf)
            {
                throw new InvalidOperationException(buttonName + " non avvia il proprio effetto persistente o accetta una sovrapposizione.");
            }

            foreach (Button actionButton in GetActionButtons(root))
            {
                if (actionButton.interactable)
                {
                    throw new InvalidOperationException("I comandi non vengono disabilitati durante " + buttonName + ".");
                }
            }

            stepStartedAt = EditorApplication.timeSinceStartup;
            SessionState.SetString(StateKey, waitState);
        }

        private static void WaitForBattleActionToComplete(string nextButton, string nextEffectPath, string nextWaitState)
        {
            GameObject root = RequirePlayRoot("SCN_W01_L01_Tutorial");
            BattlePreviewController controller = root.GetComponentInChildren<BattlePreviewController>(true);
            if (controller != null && controller.IsPreviewRunning)
            {
                EnsureStepNotTimedOut("completamento anteprima precedente", 5d);
                return;
            }

            AssertPreviewReset(root);
            BeginBattleAction(nextButton, nextEffectPath, nextWaitState);
        }

        private static void WaitForMarkAndReturn()
        {
            GameObject root = RequirePlayRoot("SCN_W01_L01_Tutorial");
            BattlePreviewController controller = root.GetComponentInChildren<BattlePreviewController>(true);
            if (controller != null && controller.IsPreviewRunning)
            {
                EnsureStepNotTimedOut("completamento anteprima Marchio", 5d);
                return;
            }

            AssertPreviewReset(root);
            Button backButton = RequireComponent<Button>(root, "UIRoot/Canvas/SafeArea/BTN_BackToMenu");
            backButton.onClick.Invoke();
            backButton.onClick.Invoke();
            if (backButton.interactable)
            {
                throw new InvalidOperationException("BTN_BackToMenu non blocca il doppio tocco.");
            }

            stepStartedAt = EditorApplication.timeSinceStartup;
            SessionState.SetString(StateKey, WaitMainReturn);
        }

        private static void WaitForMainSceneReturn()
        {
            if (SceneManager.GetActiveScene().name == "SCN_MainMenu")
            {
                frameCounter = 0;
                SessionState.SetString(StateKey, MainReturnDelay);
                return;
            }

            EnsureStepNotTimedOut("ritorno al menu", 12d);
        }

        private static void TestSettingsAfterSceneRoundTripAndExit()
        {
            GameObject root = RequirePlayRoot("SCN_MainMenu");
            RequireComponent<Button>(root, "UIRoot/Canvas/SafeArea/StartCard/ButtonStack/BTN_Settings").onClick.Invoke();
            AssertSlider(root, "UIRoot/Canvas/SafeArea/SettingsModal/SLD_Master", 0.42f);
            AssertSlider(root, "UIRoot/Canvas/SafeArea/SettingsModal/SLD_Music", 0.37f);
            AssertSlider(root, "UIRoot/Canvas/SafeArea/SettingsModal/SLD_Sfx", 0.61f);
            if (RequireComponent<Toggle>(root, "UIRoot/Canvas/SafeArea/SettingsModal/TGL_Vibration").isOn)
            {
                throw new InvalidOperationException("Le impostazioni non persistono dopo il cambio scena completo.");
            }

            RestoreSettingsSnapshot();
            SessionState.SetString(StateKey, WaitingForEdit);
            EditorApplication.ExitPlaymode();
        }

        private static void AssertPreviewReset(GameObject root)
        {
            string[] paths =
            {
                "BattlePreviewRoot/HeroSlot/GuardVisual",
                "BattlePreviewRoot/EnemySlot/MarkPreview",
                "BattlePreviewRoot/PreviewEffects/HeroBasicProjectile",
                "BattlePreviewRoot/PreviewEffects/HeroTechniqueProjectile",
                "BattlePreviewRoot/PreviewEffects/EnemyProjectile"
            };
            if (paths.Any(path => RequireTransform(root, path).gameObject.activeSelf) ||
                GetActionButtons(root).Any(button => !button.interactable))
            {
                throw new InvalidOperationException("L'anteprima non ripristina effetti e pulsanti al termine.");
            }
        }

        private static IEnumerable<Button> GetActionButtons(GameObject root)
        {
            string basePath = "UIRoot/Canvas/SafeArea/ActionBar/";
            yield return RequireComponent<Button>(root, basePath + "BTN_Attack");
            yield return RequireComponent<Button>(root, basePath + "BTN_Guard");
            yield return RequireComponent<Button>(root, basePath + "BTN_Technique");
            yield return RequireComponent<Button>(root, basePath + "BTN_Mark");
        }

        private static void AssertSlider(GameObject root, string path, float expected)
        {
            float actual = RequireComponent<Slider>(root, path).value;
            if (!Mathf.Approximately(actual, expected))
            {
                throw new InvalidOperationException(path + " non ha mantenuto il valore " + expected + ".");
            }
        }

        private static GameObject RequirePlayRoot(string name)
        {
            GameObject root = GetRoot(name);
            if (root == null)
            {
                throw new InvalidOperationException("Scena attiva senza root " + name + ".");
            }

            return root;
        }

        private static Transform RequireTransform(GameObject root, string path)
        {
            Transform transform = root.transform.Find(path);
            if (transform == null)
            {
                throw new InvalidOperationException("Oggetto Play Mode mancante: " + path);
            }

            return transform;
        }

        private static T RequireComponent<T>(GameObject root, string path) where T : Component
        {
            T component = RequireTransform(root, path).GetComponent<T>();
            if (component == null)
            {
                throw new InvalidOperationException(path + " non contiene " + typeof(T).Name + ".");
            }

            return component;
        }

        private static void EnsureStepNotTimedOut(string label, double seconds)
        {
            if (EditorApplication.timeSinceStartup - stepStartedAt > seconds)
            {
                throw new TimeoutException("Timeout durante " + label + ".");
            }
        }

        private static void StoreSettingsSnapshot()
        {
            SessionState.SetString(SettingsSnapshotKey, JsonUtility.ToJson(LocalSettingsStore.Load()));
            SessionState.SetInt(SettingsPresenceKey, GetSettingsPresenceMask());
        }

        private static void RestoreSettingsSnapshot()
        {
            string json = SessionState.GetString(SettingsSnapshotKey, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                LocalSettingsStore.Values values = JsonUtility.FromJson<LocalSettingsStore.Values>(json);
                RestoreSettings(values, SessionState.GetInt(SettingsPresenceKey, 0));
            }
        }

        private static int GetSettingsPresenceMask()
        {
            int presence = 0;
            string[] keys = SettingsKeys();
            for (int index = 0; index < keys.Length; index++)
            {
                if (PlayerPrefs.HasKey(keys[index]))
                {
                    presence |= 1 << index;
                }
            }

            return presence;
        }

        private static void RestoreSettings(LocalSettingsStore.Values values, int presence)
        {
            LocalSettingsStore.Save(values);
            string[] keys = SettingsKeys();
            for (int index = 0; index < keys.Length; index++)
            {
                if ((presence & (1 << index)) == 0)
                {
                    PlayerPrefs.DeleteKey(keys[index]);
                }
            }

            PlayerPrefs.Save();
        }

        private static string[] SettingsKeys()
        {
            return new[]
            {
                LocalSettingsStore.VersionKey,
                LocalSettingsStore.MasterVolumeKey,
                LocalSettingsStore.MusicVolumeKey,
                LocalSettingsStore.SfxVolumeKey,
                LocalSettingsStore.VibrationEnabledKey
            };
        }

        private static void FailPlayValidation(string message)
        {
            RestoreSettingsSnapshot();
            SessionState.SetString(FailureKey, message);
            SessionState.SetString(StateKey, WaitingForEdit);
            EditorApplication.ExitPlaymode();
        }

        private static void CompleteValidation(IReadOnlyCollection<string> errors, string label)
        {
            if (errors.Count == 0)
            {
                Debug.Log(
                    "[Veyra Phase 02 Validation] " + label +
                    " superata: nessun Missing Script, Missing Reference o errore di layout alle tre risoluzioni.");
                return;
            }

            Debug.LogError(
                "[Veyra Phase 02 Validation] " + label + " fallita:\n- " +
                string.Join("\n- ", errors));
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
            SessionState.EraseString(StateKey);
            SessionState.EraseString(FailureKey);
            SessionState.EraseString(MainSignatureKey);
            SessionState.EraseString(TutorialSignatureKey);
            SessionState.EraseString(MainHashKey);
            SessionState.EraseString(TutorialHashKey);
            SessionState.EraseString(SettingsSnapshotKey);
            SessionState.EraseInt(SettingsPresenceKey);
        }

        [Serializable]
        private sealed class RestartSettingsSnapshot
        {
            public LocalSettingsStore.Values values;
            public int presence;
        }
    }
}
#endif
