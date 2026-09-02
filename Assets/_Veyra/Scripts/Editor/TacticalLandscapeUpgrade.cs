#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Veyra.Combat;
using Veyra.Combat.Encounter;
using Veyra.Combat.MultiEnemy;
using Veyra.Combat.Tactical;
using Veyra.Combat.Tutorial;
using Veyra.Core;
using Veyra.UI.MainMenu;

namespace Veyra.Editor
{
    internal static class TacticalLandscapeUpgrade
    {
        private const string ArenaRootName = "TacticalArenaRoot";
        private const string CommandRootName = "TacticalLandscapeCommands";
        private const string PlatformSpritePath =
            "Assets/_Veyra/Art/Sprites/UI/Battle/Tactical/SPR_TacticalPlatform.png";
        private static readonly Vector2 LandscapeResolution = new Vector2(1920f, 1080f);

        [MenuItem("Tools/Veyra/Campaign/Upgrade Existing Levels To Tactical Landscape", priority = 220)]
        internal static void UpgradeAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Esegui l'upgrade tattico soltanto in Edit Mode.");
            }

            TMP_FontAsset font = Phase02UiFactory.LoadRequiredFont();
            UpgradeMainMenu();
            UpgradeSingleEnemyScene(
                Phase046EncounterSceneFactory.TutorialScenePath,
                true,
                font);
            UpgradeSingleEnemyScene(
                Phase046EncounterSceneFactory.Level02ScenePath,
                false,
                font);
            UpgradeSingleEnemyScene(
                Phase046EncounterSceneFactory.Level03ScenePath,
                false,
                font);
            UpgradeLevel04(font);
            AssetDatabase.SaveAssets();
            Debug.Log("[Veyra Tactical] Upgrade completato: menu e livelli 1-4 sono in 16:9 con arena 4x6. Nessun livello 5-10 creato.");
        }

        [MenuItem("Tools/Veyra/Validate/Campaign Progression", priority = 300)]
        internal static void ValidateCampaignProgression()
        {
            Phase78ProgressionValidator.ValidateProgressionAndLevel04();
        }

        [MenuItem("Tools/Veyra/Validate/Tactical Battlefield", priority = 301)]
        internal static void ValidateTacticalBattlefield()
        {
            var errors = new List<string>();
            ValidateLandscapeScene(SceneNames.MainMenu, false, 0, errors);
            ValidateLandscapeScene(SceneNames.World01Level01Tutorial, true, 2, errors);
            ValidateLandscapeScene(SceneNames.World01Level02ThornGuardian, true, 2, errors);
            ValidateLandscapeScene(SceneNames.World01Level03AshWatcher, true, 2, errors);
            ValidateLandscapeScene(SceneNames.World01Level04ThreefoldAssault, true, 4, errors);

            string[] forbidden = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_Veyra/Scenes" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.IndexOf("_L0", StringComparison.OrdinalIgnoreCase) >= 0)
                .Where(path => path.IndexOf("_L05", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               path.IndexOf("_L06", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               path.IndexOf("_L07", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               path.IndexOf("_L08", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               path.IndexOf("_L09", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               path.IndexOf("_L10", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToArray();
            if (forbidden.Length > 0)
            {
                errors.Add("Sono state trovate scene future non consentite: " + string.Join(", ", forbidden));
            }

            if (errors.Count == 0)
            {
                Debug.Log("[Veyra Tactical Validation] SUPERATA — 16:9, cinque Canvas, quattro arene 4x6, movimento e riferimenti unità conformi.");
                return;
            }

            throw new InvalidOperationException(
                "[Veyra Tactical Validation] FALLITA (" + errors.Count + "):\n- " +
                string.Join("\n- ", errors));
        }

        private static void UpgradeMainMenu()
        {
            Scene scene = OpenScene("Assets/_Veyra/Scenes/SCN_MainMenu.unity");
            SetLandscapeCanvas(scene);
            MainMenuController controller = FindSingle<MainMenuController>(scene);
            SerializedObject serialized = new SerializedObject(controller);
            serialized.Update();

            SetButtonRect(serialized, "startButton", 0.055f, 0.655f, 0.34f, 0.755f);
            SetButtonRect(serialized, "levelsButton", 0.055f, 0.525f, 0.34f, 0.625f);
            SetButtonRect(serialized, "heroesButton", 0.055f, 0.395f, 0.34f, 0.495f);
            SetButtonRect(serialized, "settingsButton", 0.055f, 0.265f, 0.34f, 0.365f);

            TMP_Text campaignStatus = serialized.FindProperty("campaignStatusText")?.objectReferenceValue as TMP_Text;
            if (campaignStatus != null)
            {
                SetRect(campaignStatus.rectTransform, new Vector2(0.46f, 0.56f), new Vector2(0.94f, 0.78f));
                campaignStatus.alignment = TextAlignmentOptions.Center;
            }

            SerializedProperty buttons = serialized.FindProperty("levelButtons");
            RectTransform levelContent = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RectTransform>(true))
                .FirstOrDefault(rect => rect.name == "LevelScrollContent");
            for (int index = 0; buttons != null && index < buttons.arraySize && index < 10; index++)
            {
                Button button = buttons.GetArrayElementAtIndex(index).objectReferenceValue as Button;
                if (button == null) continue;

                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.color = new Color(0.92f, 0.96f, 0.94f, 1f);
                    label.alignment = TextAlignmentOptions.Center;
                    label.enableAutoSizing = true;
                    label.fontSizeMin = 17f;
                    label.fontSizeMax = 25f;
                    label.margin = new Vector4(12f, 10f, 12f, 10f);
                    EditorUtility.SetDirty(label);
                }
            }

            if (levelContent != null)
            {
                RectTransform oldViewport = levelContent.parent as RectTransform;
                GameObject levelsPanel = serialized.FindProperty("levelsPanel")?.objectReferenceValue as GameObject;
                if (levelsPanel != null)
                {
                    levelContent.SetParent(levelsPanel.transform, false);
                }

                if (oldViewport != null)
                {
                    oldViewport.gameObject.SetActive(false);
                }

                VerticalLayoutGroup vertical = levelContent.GetComponent<VerticalLayoutGroup>();
                if (vertical != null)
                {
                    UnityEngine.Object.DestroyImmediate(vertical);
                }

                ContentSizeFitter fitter = levelContent.GetComponent<ContentSizeFitter>();
                if (fitter != null)
                {
                    UnityEngine.Object.DestroyImmediate(fitter);
                }

                GridLayoutGroup grid = levelContent.GetComponent<GridLayoutGroup>();
                if (grid == null)
                {
                    grid = levelContent.gameObject.AddComponent<GridLayoutGroup>();
                }

                grid.padding = new RectOffset(18, 18, 18, 18);
                grid.cellSize = new Vector2(300f, 210f);
                grid.spacing = new Vector2(24f, 24f);
                grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
                grid.startAxis = GridLayoutGroup.Axis.Horizontal;
                grid.childAlignment = TextAnchor.MiddleCenter;
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 5;

                SetRect(levelContent, new Vector2(0.06f, 0.17f), new Vector2(0.94f, 0.78f));
                levelContent.pivot = new Vector2(0.5f, 0.5f);
                EditorUtility.SetDirty(grid);
                EditorUtility.SetDirty(levelContent);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void UpgradeSingleEnemyScene(string scenePath, bool tutorial, TMP_FontAsset font)
        {
            Scene scene = OpenScene(scenePath);
            SetLandscapeCanvas(scene);

            Component controller = tutorial
                ? (Component)FindSingle<TutorialBattleController>(scene)
                : FindSingle<EncounterBattleController>(scene);
            SerializedObject controllerSerialized = new SerializedObject(controller);
            controllerSerialized.Update();
            Transform heroActor = GetObject<Transform>(controllerSerialized, "heroActor");
            Transform enemyActor = GetObject<Transform>(controllerSerialized, "enemyActor");
            SpriteRenderer heroVisual = GetObject<SpriteRenderer>(controllerSerialized, "heroVisual");
            SpriteRenderer enemyVisual = GetObject<SpriteRenderer>(controllerSerialized, "enemyVisual");
            EnsureHeroCombatPresentation(heroActor);

            TacticalBattlefieldController board = RebuildArena(
                scene,
                new[]
                {
                    new UnitSpec("Hero01", heroActor, heroVisual, 1, 1, true, false, false, 1.70f),
                    new UnitSpec("Enemy", enemyActor, enemyVisual, 1, 3, false, false, true, 1.75f)
                });
            CommandUi commands = RebuildCommands(scene, font, controller, tutorial);
            AssignBoard(board, commands, controller, tutorial);
            SetObject(controllerSerialized, "battlefield", board);
            controllerSerialized.ApplyModifiedPropertiesWithoutUndo();
            ArrangeActionButtons(controllerSerialized, commands);
            CompactSingleEnemyHud(scene, controllerSerialized);
            if (tutorial)
            {
                CompactTutorialCards(scene, controllerSerialized);
            }
            EditorUtility.SetDirty(controller);
            EditorSceneManager.SaveScene(scene);
        }

        private static void UpgradeLevel04(TMP_FontAsset font)
        {
            const string path = "Assets/_Veyra/Scenes/SCN_W01_L04_ThreefoldAssault.unity";
            Scene scene = OpenScene(path);
            SetLandscapeCanvas(scene);
            MultiEnemyBattleController controller = FindSingle<MultiEnemyBattleController>(scene);
            SerializedObject serialized = new SerializedObject(controller);
            serialized.Update();
            Transform heroActor = GetObject<Transform>(serialized, "heroActor");
            SpriteRenderer heroVisual = GetObject<SpriteRenderer>(serialized, "heroVisual");
            EnsureHeroCombatPresentation(heroActor);
            SerializedProperty enemyViews = serialized.FindProperty("enemyViews");
            var specs = new List<UnitSpec>
            {
                new UnitSpec("Hero01", heroActor, heroVisual, 2, 1, true, false, false, 1.70f)
            };
            int[] rows = { 2, 0, 3 };
            int[] columns = { 3, 4, 4 };
            for (int index = 0; index < enemyViews.arraySize; index++)
            {
                SerializedProperty view = enemyViews.GetArrayElementAtIndex(index);
                specs.Add(new UnitSpec(
                    view.FindPropertyRelative("enemyId").stringValue,
                    view.FindPropertyRelative("actor").objectReferenceValue as Transform,
                    view.FindPropertyRelative("visual").objectReferenceValue as SpriteRenderer,
                    rows[index], columns[index], false, index > 0, true,
                    index == 0 ? 2.05f : 1.72f));
            }

            TacticalBattlefieldController board = RebuildArena(scene, specs.ToArray());
            CommandUi commands = RebuildCommands(scene, font, controller, false);
            AssignBoard(board, commands, controller, false);
            SetObject(serialized, "battlefield", board);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            ArrangeActionButtons(serialized, commands);
            CompactLevel04Hud(scene);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.SaveScene(scene);
        }

        private static TacticalBattlefieldController RebuildArena(Scene scene, UnitSpec[] units)
        {
            RemoveRoot(scene, ArenaRootName);
            GameObject root = new GameObject(ArenaRootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            TacticalBattlefieldController board = root.AddComponent<TacticalBattlefieldController>();
            Sprite platformSprite = EnsurePlatformSprite();
            var views = new List<TacticalPlatformView>();

            for (int row = 0; row < TacticalBattlefieldController.RowCount; row++)
            {
                for (int column = 0; column < TacticalBattlefieldController.ColumnCount; column++)
                {
                    GameObject node = new GameObject(
                        "Platform_R" + row + "_C" + column,
                        typeof(SpriteRenderer),
                        typeof(BoxCollider2D),
                        typeof(TacticalPlatformView));
                    node.transform.SetParent(root.transform, false);
                    node.transform.position = PlatformPosition(row, column);
                    node.transform.localScale = new Vector3(0.78f, 0.50f, 1f);
                    SpriteRenderer renderer = node.GetComponent<SpriteRenderer>();
                    renderer.sprite = platformSprite;
                    renderer.color = new Color(0.27f, 0.68f, 0.66f, 0.92f);
                    renderer.sortingOrder = 2 + row * 3;
                    GameObject innerObject = new GameObject("PlatformInner", typeof(SpriteRenderer));
                    innerObject.transform.SetParent(node.transform, false);
                    innerObject.transform.localScale = new Vector3(0.88f, 0.74f, 1f);
                    SpriteRenderer inner = innerObject.GetComponent<SpriteRenderer>();
                    inner.sprite = platformSprite;
                    inner.color = new Color(0.10f, 0.19f, 0.27f, 1f);
                    inner.sortingOrder = renderer.sortingOrder + 1;
                    BoxCollider2D collider = node.GetComponent<BoxCollider2D>();
                    collider.size = Vector2.one;
                    TacticalPlatformView view = node.GetComponent<TacticalPlatformView>();
                    view.Configure(row, column, board, inner);
                    views.Add(view);
                }
            }

            SerializedObject boardSerialized = new SerializedObject(board);
            boardSerialized.Update();
            SetArray(boardSerialized.FindProperty("platforms"), views.Cast<UnityEngine.Object>().ToArray());
            SerializedProperty unitArray = boardSerialized.FindProperty("units");
            unitArray.arraySize = units.Length;
            for (int index = 0; index < units.Length; index++)
            {
                UnitSpec spec = units[index];
                SerializedProperty item = unitArray.GetArrayElementAtIndex(index);
                item.FindPropertyRelative("unitId").stringValue = spec.Id;
                item.FindPropertyRelative("actor").objectReferenceValue = spec.Actor;
                item.FindPropertyRelative("visual").objectReferenceValue = spec.Visual;
                item.FindPropertyRelative("startRow").intValue = spec.Row;
                item.FindPropertyRelative("startColumn").intValue = spec.Column;
                item.FindPropertyRelative("isHero").boolValue = spec.IsHero;
                item.FindPropertyRelative("isFlying").boolValue = spec.IsFlying;
                item.FindPropertyRelative("sourceSpriteFacesRight").boolValue = spec.SourceFacesRight;
                item.FindPropertyRelative("targetVisualHeight").floatValue = spec.Height;
                item.FindPropertyRelative("flyingHeight").floatValue = spec.IsFlying ? 0.62f : 0f;
                SpriteRenderer shadow = CreateShadow(root.transform, spec, platformSprite);
                item.FindPropertyRelative("shadow").objectReferenceValue = shadow;
                ApplyPersistentUnitPresentation(spec, shadow);
            }

            boardSerialized.ApplyModifiedPropertiesWithoutUndo();
            Camera camera = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)).FirstOrDefault();
            if (camera != null)
            {
                camera.orthographic = true;
                camera.orthographicSize = 3.90f;
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.backgroundColor = new Color(0.025f, 0.035f, 0.08f, 1f);
                EditorUtility.SetDirty(camera);
                boardSerialized.Update();
                SetObject(boardSerialized, "worldCamera", camera);
                boardSerialized.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(board);
            return board;
        }

        private static void EnsureHeroCombatPresentation(Transform heroActor)
        {
            if (heroActor != null && heroActor.GetComponent<HeroCombatPresentation>() == null)
            {
                heroActor.gameObject.AddComponent<HeroCombatPresentation>();
                EditorUtility.SetDirty(heroActor.gameObject);
            }
        }

        private static CommandUi RebuildCommands(Scene scene, TMP_FontAsset font, Component controller, bool tutorial)
        {
            RectTransform safeArea = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RectTransform>(true))
                .First(rect => rect.name == "SafeArea");
            Transform old = safeArea.Find(CommandRootName);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            RectTransform root = Phase02UiFactory.CreateRect(CommandRootName, safeArea);
            Phase02UiFactory.SetRect(root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Button move = Phase02UiFactory.CreateButton(
                "BTN_Move", root, "MUOVI", font,
                new Vector2(0.01f, 0.015f), new Vector2(0.155f, 0.115f),
                new Vector2(6f, 4f), new Vector2(-6f, -4f));
            Button end = Phase02UiFactory.CreateButton(
                "BTN_EndTurn", root, "FINE TURNO", font,
                new Vector2(0.845f, 0.015f), new Vector2(0.99f, 0.115f),
                new Vector2(6f, 4f), new Vector2(-6f, -4f));
            TMP_Text feedback = Phase02UiFactory.CreateText(
                "TXT_TacticalFeedback", root,
                tutorial ? "TUTORIAL · PREMI MUOVI E SCEGLI UNA PEDANA VERDE" : "TUO TURNO",
                28f, Phase02UiFactory.Cyan, TextAlignmentOptions.Center, font,
                new Vector2(0.22f, 0.125f), new Vector2(0.78f, 0.18f),
                Vector2.zero, Vector2.zero, FontStyles.Bold);
            return new CommandUi(move, end, feedback);
        }

        private static void AssignBoard(
            TacticalBattlefieldController board,
            CommandUi commands,
            Component controller,
            bool tutorial)
        {
            SerializedObject serialized = new SerializedObject(board);
            serialized.Update();
            SetObject(serialized, "moveButton", commands.Move);
            SetObject(serialized, "endTurnButton", commands.EndTurn);
            SetObject(serialized, "feedbackText", commands.Feedback);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            if (tutorial)
            {
                TutorialBattleController typed = (TutorialBattleController)controller;
                UnityEventTools.AddPersistentListener(commands.Move.onClick, typed.BeginTacticalMove);
                UnityEventTools.AddPersistentListener(commands.EndTurn.onClick, board.RequestEndTurn);
                UnityEventTools.AddPersistentListener(board.EndTurnRequested, typed.EndTacticalTurn);
            }
            else if (controller is EncounterBattleController encounter)
            {
                UnityEventTools.AddPersistentListener(commands.Move.onClick, encounter.BeginTacticalMove);
                UnityEventTools.AddPersistentListener(commands.EndTurn.onClick, board.RequestEndTurn);
                UnityEventTools.AddPersistentListener(board.EndTurnRequested, encounter.EndTacticalTurn);
            }
            else
            {
                MultiEnemyBattleController multi = (MultiEnemyBattleController)controller;
                UnityEventTools.AddPersistentListener(commands.Move.onClick, multi.BeginTacticalMove);
                UnityEventTools.AddPersistentListener(commands.EndTurn.onClick, board.RequestEndTurn);
                UnityEventTools.AddPersistentListener(board.EndTurnRequested, multi.EndTacticalTurn);
            }
        }

        private static void ArrangeActionButtons(SerializedObject controller, CommandUi commands)
        {
            string[] names = { "attackButton", "guardButton", "techniqueButton", "analyzeButton" };
            for (int index = 0; index < names.Length; index++)
            {
                Button button = GetObject<Button>(controller, names[index]);
                if (button == null) continue;
                float left = 0.17f + index * 0.165f;
                SetRect(button.GetComponent<RectTransform>(),
                    new Vector2(left, 0.015f), new Vector2(left + 0.145f, 0.115f));
            }
        }

        private static void CompactSingleEnemyHud(Scene scene, SerializedObject controller)
        {
            RectTransform heroHealth = GetSafeAreaChild(
                GetObject<Image>(controller, "heroHealthFill")?.rectTransform);
            RectTransform enemyHealth = GetSafeAreaChild(
                GetObject<Image>(controller, "enemyHealthFill")?.rectTransform);
            RectTransform message = GetSafeAreaChild(
                GetObject<TMP_Text>(controller, "combatMessage")?.rectTransform);
            RectTransform phase = GetSafeAreaChild(
                GetObject<TMP_Text>(controller, "phaseText")?.rectTransform);
            RectTransform intent = GetSafeAreaChild(
                GetObject<TMP_Text>(controller, "intentText")?.rectTransform);
            RectTransform status = GetSafeAreaChild(
                GetObject<TMP_Text>(controller, "statusText")?.rectTransform);

            if (heroHealth != null) SetRect(heroHealth, new Vector2(0.02f, 0.915f), new Vector2(0.49f, 0.965f));
            if (enemyHealth != null) SetRect(enemyHealth, new Vector2(0.51f, 0.915f), new Vector2(0.98f, 0.965f));
            if (message != null) SetRect(message, new Vector2(0.27f, 0.845f), new Vector2(0.73f, 0.90f));
            if (phase != null) SetRect(phase, new Vector2(0.02f, 0.845f), new Vector2(0.255f, 0.90f));
            if (intent != null) SetRect(intent, new Vector2(0.745f, 0.845f), new Vector2(0.98f, 0.90f));
            if (status != null && status != intent)
            {
                SetRect(status, new Vector2(0.75f, 0.79f), new Vector2(0.98f, 0.84f));
            }

            if (phase != null) phase.gameObject.SetActive(false);

            RectTransform title = FindRect(scene, "TXT_LevelTitle") ??
                                  FindRect(scene, "TXT_TutorialTitle") ??
                                  FindRect(scene, "TXT_EncounterTitle");
            if (title != null) SetRect(title, new Vector2(0.20f, 0.968f), new Vector2(0.80f, 0.998f));
        }

        private static void CompactLevel04Hud(Scene scene)
        {
            SetNamedRect(scene, "TXT_Level04Title", 0.20f, 0.970f, 0.80f, 0.998f);
            SetNamedRect(scene, "HeroHealthPanel", 0.10f, 0.932f, 0.90f, 0.968f);
            SetNamedRect(scene, "BTN_Target_W01_L04_BRUTE", 0.02f, 0.805f, 0.325f, 0.928f);
            SetNamedRect(scene, "BTN_Target_W01_L04_WATCHER", 0.3475f, 0.805f, 0.6525f, 0.928f);
            SetNamedRect(scene, "BTN_Target_W01_L04_MASK", 0.675f, 0.805f, 0.98f, 0.928f);
            SetNamedRect(scene, "SelectedTargetPanel", 0.02f, 0.745f, 0.27f, 0.795f);
            SetNamedRect(scene, "CombatMessagePanel", 0.285f, 0.745f, 0.715f, 0.795f);
            SetNamedRect(scene, "HeroStatusPanel", 0.73f, 0.745f, 0.98f, 0.795f);
            SetNamedRect(scene, "BTN_Level04Back", 0.02f, 0.685f, 0.15f, 0.735f);
            RectTransform phase = FindRect(scene, "TXT_Level04Phase");
            if (phase != null) phase.gameObject.SetActive(false);
            SetNamedRect(scene, "EnemyDialogueRoot", 0.20f, 0.66f, 0.95f, 0.735f);
            SetNamedRect(scene, "SavedAllyDialogueRoot", 0.05f, 0.58f, 0.80f, 0.65f);
        }

        private static void CompactTutorialCards(Scene scene, SerializedObject controller)
        {
            SetNamedRect(scene, "TutorialCard", 0.03f, 0.53f, 0.48f, 0.80f);
            SetNamedRect(scene, "EnemyInfoCard", 0.52f, 0.43f, 0.97f, 0.78f);
            Image inputBlocker = GetObject<Image>(controller, "tutorialInputBlocker");
            if (inputBlocker != null)
            {
                inputBlocker.color = new Color(0f, 0f, 0f, 0.12f);
                EditorUtility.SetDirty(inputBlocker);
            }

            TMP_Text body = GetObject<TMP_Text>(controller, "tutorialBodyText");
            TMP_Text step = GetObject<TMP_Text>(controller, "tutorialStepText");
            if (body != null)
            {
                body.fontSizeMin = 24f;
                body.fontSizeMax = 34f;
                EditorUtility.SetDirty(body);
            }
            if (step != null)
            {
                step.fontSizeMin = 22f;
                step.fontSizeMax = 30f;
                EditorUtility.SetDirty(step);
            }
        }

        private static void SetNamedRect(
            Scene scene,
            string name,
            float x0,
            float y0,
            float x1,
            float y1)
        {
            RectTransform rect = FindRect(scene, name);
            if (rect != null) SetRect(rect, new Vector2(x0, y0), new Vector2(x1, y1));
        }

        private static RectTransform FindRect(Scene scene, string name)
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RectTransform>(true))
                .FirstOrDefault(rect => rect.name == name);
        }

        private static RectTransform GetSafeAreaChild(RectTransform rect)
        {
            if (rect == null) return null;
            RectTransform current = rect;
            while (current.parent is RectTransform parent && parent.name != "SafeArea")
            {
                current = parent;
            }

            return current;
        }

        private static void SetLandscapeCanvas(Scene scene)
        {
            CanvasScaler[] scalers = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CanvasScaler>(true)).ToArray();
            foreach (CanvasScaler scaler in scalers)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = LandscapeResolution;
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
                EditorUtility.SetDirty(scaler);
            }
        }

        private static void ValidateLandscapeScene(
            string sceneName,
            bool requiresArena,
            int expectedUnits,
            List<string> errors)
        {
            string path = "Assets/_Veyra/Scenes/" + sceneName + ".unity";
            Scene scene = OpenScene(path);
            CanvasScaler[] scalers = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CanvasScaler>(true)).ToArray();
            if (scalers.Length == 0 || scalers.Any(s => s.referenceResolution != LandscapeResolution))
            {
                errors.Add(sceneName + ": Canvas non configurato a 1920x1080.");
            }

            if (!requiresArena) return;
            TacticalBattlefieldController[] boards = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<TacticalBattlefieldController>(true)).ToArray();
            TacticalPlatformView[] nodes = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<TacticalPlatformView>(true)).ToArray();
            if (boards.Length != 1) errors.Add(sceneName + ": deve esistere una sola arena tattica.");
            if (nodes.Length != 24 || nodes.Select(n => n.Row + ":" + n.Column).Distinct().Count() != 24)
            {
                errors.Add(sceneName + ": l'arena non contiene 24 coordinate uniche (4x6).");
            }

            if (boards.Length == 1)
            {
                SerializedObject serialized = new SerializedObject(boards[0]);
                if (serialized.FindProperty("units").arraySize != expectedUnits)
                {
                    errors.Add(sceneName + ": numero di unità tattiche inatteso.");
                }

                if (GetObject<Button>(serialized, "moveButton") == null ||
                    GetObject<Button>(serialized, "endTurnButton") == null)
                {
                    errors.Add(sceneName + ": comandi MUOVI/FINE TURNO non collegati.");
                }

                SerializedProperty units = serialized.FindProperty("units");
                for (int index = 0; index < units.arraySize; index++)
                {
                    SerializedProperty unit = units.GetArrayElementAtIndex(index);
                    if (!unit.FindPropertyRelative("isHero").boolValue) continue;
                    Transform actor = unit.FindPropertyRelative("actor").objectReferenceValue as Transform;
                    if (actor == null || actor.GetComponent<HeroCombatPresentation>() == null)
                    {
                        errors.Add(sceneName + ": HeroCombatPresentation non collegata all'eroe.");
                    }
                }
            }
        }

        private static SpriteRenderer CreateShadow(Transform parent, UnitSpec spec, Sprite sprite)
        {
            GameObject shadowObject = new GameObject("Shadow_" + spec.Id, typeof(SpriteRenderer));
            shadowObject.transform.SetParent(parent, false);
            SpriteRenderer shadow = shadowObject.GetComponent<SpriteRenderer>();
            shadow.sprite = sprite;
            shadow.color = new Color(0f, 0f, 0f, 0.42f);
            shadow.sortingOrder = 16 + spec.Row * 3;
            shadow.transform.localScale = new Vector3(spec.IsFlying ? 0.95f : 1.15f, 0.30f, 1f);
            shadow.transform.position = PlatformPosition(spec.Row, spec.Column) + new Vector3(0f, 0.10f, 0f);
            return shadow;
        }

        private static Sprite EnsurePlatformSprite()
        {
            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(PlatformSpritePath);
            if (existing != null)
            {
                return existing;
            }

            EnsureAssetFolder("Assets/_Veyra/Art/Sprites/UI/Battle/Tactical");
            const int width = 128;
            const int height = 64;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = (x + 0.5f - width * 0.5f) / (width * 0.48f);
                    float ny = (y + 0.5f - height * 0.5f) / (height * 0.42f);
                    pixels[y * width + x] = nx * nx + ny * ny <= 1f
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(0, 0, 0, 0);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            File.WriteAllBytes(PlatformSpritePath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(PlatformSpritePath, ImportAssetOptions.ForceSynchronousImport);

            TextureImporter importer = AssetImporter.GetAtPath(PlatformSpritePath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException("Importer pedana non disponibile: " + PlatformSpritePath);
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 64f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(PlatformSpritePath);
        }

        private static void EnsureAssetFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }

                current = next;
            }
        }

        private static void ApplyPersistentUnitPresentation(UnitSpec spec, SpriteRenderer shadow)
        {
            if (spec.Actor == null) return;
            Vector3 platform = PlatformPosition(spec.Row, spec.Column);
            spec.Actor.position = platform + new Vector3(0f, 0.33f + (spec.IsFlying ? 0.62f : 0f), 0f);
            if (spec.Visual != null && spec.Visual.sprite != null)
            {
                float height = spec.Visual.sprite.bounds.size.y;
                if (height > 0.001f)
                {
                    float scale = spec.Height / height;
                    spec.Visual.transform.localScale = new Vector3(scale, scale, 1f);
                }

                bool shouldFaceRight = spec.IsHero;
                spec.Visual.flipX = spec.SourceFacesRight != shouldFaceRight;
                spec.Visual.sortingOrder = 20 + spec.Row * 3;
                BoxCollider2D selectionCollider = spec.Visual.GetComponent<BoxCollider2D>();
                if (selectionCollider == null)
                {
                    selectionCollider = spec.Visual.gameObject.AddComponent<BoxCollider2D>();
                }
                selectionCollider.size = spec.Visual.sprite.bounds.size;
                selectionCollider.offset = spec.Visual.sprite.bounds.center;
                selectionCollider.isTrigger = true;
                EditorUtility.SetDirty(selectionCollider);
                EditorUtility.SetDirty(spec.Visual);
            }

            EditorUtility.SetDirty(spec.Actor);
            EditorUtility.SetDirty(shadow);
        }

        private static Vector3 PlatformPosition(int row, int column)
        {
            float x = (column - 2.5f) * 1.55f + (row - 1.5f) * 0.28f;
            float y = -1.25f + (row - 1.5f) * 0.88f;
            return new Vector3(x, y, 0f);
        }

        private static Scene OpenScene(string path)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            if (!scene.IsValid()) throw new InvalidOperationException("Scena non trovata: " + path);
            return scene;
        }

        private static T FindSingle<T>(Scene scene) where T : Component
        {
            T[] found = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
            if (found.Length != 1) throw new InvalidOperationException(scene.name + ": atteso un solo " + typeof(T).Name);
            return found[0];
        }

        private static void RemoveRoot(Scene scene, string name)
        {
            GameObject existing = scene.GetRootGameObjects().FirstOrDefault(root => root.name == name);
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing);
        }

        private static T GetObject<T>(SerializedObject serialized, string propertyName) where T : UnityEngine.Object
        {
            return serialized.FindProperty(propertyName)?.objectReferenceValue as T;
        }

        private static void SetObject(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null) throw new InvalidOperationException("Proprietà serializzata mancante: " + propertyName);
            property.objectReferenceValue = value;
        }

        private static void SetArray(SerializedProperty property, UnityEngine.Object[] values)
        {
            property.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            }
        }

        private static void SetButtonRect(SerializedObject serialized, string property, float x0, float y0, float x1, float y1)
        {
            Button button = GetObject<Button>(serialized, property);
            if (button != null) SetRect(button.GetComponent<RectTransform>(), new Vector2(x0, y0), new Vector2(x1, y1));
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = new Vector2(8f, 8f);
            rect.offsetMax = new Vector2(-8f, -8f);
            rect.localScale = Vector3.one;
            EditorUtility.SetDirty(rect);
        }

        private readonly struct UnitSpec
        {
            public readonly string Id;
            public readonly Transform Actor;
            public readonly SpriteRenderer Visual;
            public readonly int Row;
            public readonly int Column;
            public readonly bool IsHero;
            public readonly bool IsFlying;
            public readonly bool SourceFacesRight;
            public readonly float Height;

            public UnitSpec(string id, Transform actor, SpriteRenderer visual, int row, int column,
                bool isHero, bool isFlying, bool sourceFacesRight, float height)
            {
                Id = id;
                Actor = actor;
                Visual = visual;
                Row = row;
                Column = column;
                IsHero = isHero;
                IsFlying = isFlying;
                SourceFacesRight = sourceFacesRight;
                Height = height;
            }
        }

        private readonly struct CommandUi
        {
            public readonly Button Move;
            public readonly Button EndTurn;
            public readonly TMP_Text Feedback;
            public CommandUi(Button move, Button endTurn, TMP_Text feedback)
            {
                Move = move;
                EndTurn = endTurn;
                Feedback = feedback;
            }
        }
    }
}
#endif
