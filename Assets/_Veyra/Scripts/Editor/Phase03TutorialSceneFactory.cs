#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Veyra.Combat.Preview;
using Veyra.Combat.Tutorial;

namespace Veyra.Editor
{
    internal static class Phase03TutorialSceneFactory
    {
        private const string TutorialScenePath = "Assets/_Veyra/Scenes/SCN_W01_L01_Tutorial.unity";
        private const string MainMenuScenePath = "Assets/_Veyra/Scenes/SCN_MainMenu.unity";
        private const string SceneRootName = "SCN_W01_L01_Tutorial";
        private const string TutorialWorldRootName = "TutorialBattleRoot";
        private const string TutorialUiRootName = "TutorialUIRoot";
        private const string LegacyWorldRootName = "BattlePreviewRoot";
        private const string LegacyUiRootName = "UIRoot";

        internal static void CreateOrUpdateTutorialScene(Phase03TutorialSetupReport report)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "Il tool tutorial può essere eseguito soltanto in Edit Mode.");
            }

            PreserveDirtyScenesBeforeAuthoring();
            ValidateRequiredAssets();

            Scene scene = EditorSceneManager.OpenScene(TutorialScenePath, OpenSceneMode.Single);
            GameObject sceneRoot = FindOrCreateSceneRoot(scene, report);

            ValidateLegacyRootsAreOwned(sceneRoot.transform);
            RemoveOwnedRoot(sceneRoot.transform, TutorialWorldRootName, report);
            RemoveOwnedRoot(sceneRoot.transform, TutorialUiRootName, report);
            RemoveLegacyRoots(sceneRoot.transform, report);

            TMP_FontAsset font = Phase02UiFactory.LoadRequiredFont();
            BattleWorld world = CreateBattleWorld(sceneRoot.transform);
            BattleUi ui = CreateBattleUi(sceneRoot.transform, font);

            TutorialBattleController controller = world.root.AddComponent<TutorialBattleController>();
            TutorialBattleNavigation navigation = ui.uiRoot.AddComponent<TutorialBattleNavigation>();
            AssignControllerReferences(controller, navigation, world, ui);
            AssignNavigationReferences(navigation, controller, ui);
            AddPersistentListeners(controller, navigation, ui);

            SetInitialActiveState(world, ui);
            EnsureCamera(sceneRoot.transform, report);
            EnsureEventSystem(sceneRoot.transform, report);
            ConfigureBuildSettings(report);

            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(navigation);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, TutorialScenePath))
            {
                throw new InvalidOperationException("Impossibile salvare la scena tutorial: " + TutorialScenePath);
            }

            report.Configure(TutorialScenePath + " (tutorial combattimento persistente)");
        }

        private static void PreserveDirtyScenesBeforeAuthoring()
        {
            bool hasDirtyScene = false;
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (scene.IsValid() && scene.isLoaded && scene.isDirty)
                {
                    hasDirtyScene = true;
                    break;
                }
            }

            if (!hasDirtyScene)
            {
                return;
            }

            if (Application.isBatchMode)
            {
                throw new InvalidOperationException(
                    "Una scena aperta contiene modifiche non salvate. Salvarla prima di eseguire il tool tutorial in batch mode.");
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                throw new OperationCanceledException("Generazione tutorial annullata per preservare le scene modificate.");
            }
        }

        private static void ValidateRequiredAssets()
        {
            string[] requiredPaths =
            {
                TutorialScenePath,
                MainMenuScenePath,
                Phase01PlaceholderFactory.BackgroundSpritePath,
                Phase01PlaceholderFactory.HeroPrefabPath,
                Phase01PlaceholderFactory.EnemyPrefabPath,
                Phase02SceneFactory.BattleActionBarPrefabPath,
                Phase02PrototypeAssetFactory.FontAssetPath,
                Phase02PrototypeAssetFactory.HeroBasicProjectilePrefabPath,
                Phase02PrototypeAssetFactory.HeroTechniqueProjectilePrefabPath,
                Phase02PrototypeAssetFactory.EnemyProjectilePrefabPath,
                Phase02PrototypeAssetFactory.GuardRingPrefabPath
            };

            List<string> missing = requiredPaths
                .Where(path => AssetDatabase.LoadMainAssetAtPath(path) == null)
                .ToList();
            if (missing.Count > 0)
            {
                throw new InvalidOperationException(
                    "Asset persistenti necessari al tutorial mancanti:\n- " + string.Join("\n- ", missing));
            }
        }

        private static GameObject FindOrCreateSceneRoot(Scene scene, Phase03TutorialSetupReport report)
        {
            GameObject sceneRoot = scene.GetRootGameObjects().FirstOrDefault(root => root.name == SceneRootName);
            if (sceneRoot != null)
            {
                report.Preserve(SceneRootName);
                return sceneRoot;
            }

            sceneRoot = new GameObject(SceneRootName);
            SceneManager.MoveGameObjectToScene(sceneRoot, scene);
            report.Create(SceneRootName);
            return sceneRoot;
        }

        private static void RemoveOwnedRoot(
            Transform sceneRoot,
            string ownedRootName,
            Phase03TutorialSetupReport report)
        {
            int removedCount = 0;
            for (int index = sceneRoot.childCount - 1; index >= 0; index--)
            {
                Transform child = sceneRoot.GetChild(index);
                if (child.name != ownedRootName)
                {
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(child.gameObject);
                removedCount++;
            }

            if (removedCount > 0)
            {
                report.Configure(ownedRootName + " (root Phase 03 rigenerato: " + removedCount + ")");
            }
        }

        private static void RemoveLegacyRoots(Transform sceneRoot, Phase03TutorialSetupReport report)
        {
            Transform legacyWorld = FindDirectChild(sceneRoot, LegacyWorldRootName);
            if (legacyWorld != null)
            {
                UnityEngine.Object.DestroyImmediate(legacyWorld.gameObject);
                report.Configure(LegacyWorldRootName + " (root legacy migrato)");
            }

            Transform legacyUi = FindDirectChild(sceneRoot, LegacyUiRootName);
            if (legacyUi != null)
            {
                UnityEngine.Object.DestroyImmediate(legacyUi.gameObject);
                report.Configure(LegacyUiRootName + " (root legacy migrato)");
            }
        }

        private static void ValidateLegacyRootsAreOwned(Transform sceneRoot)
        {
            Transform legacyWorld = FindDirectChild(sceneRoot, LegacyWorldRootName);
            if (legacyWorld != null && legacyWorld.GetComponent<BattlePreviewController>() == null)
            {
                throw new InvalidOperationException(
                    "Esiste un oggetto chiamato " + LegacyWorldRootName +
                    " che non appartiene alla Phase 02. Non è stato modificato.");
            }

            Transform legacyUi = FindDirectChild(sceneRoot, LegacyUiRootName);
            if (legacyUi != null && legacyUi.GetComponent<BattlePreviewNavigation>() == null)
            {
                throw new InvalidOperationException(
                    "Esiste un oggetto chiamato " + LegacyUiRootName +
                    " che non appartiene alla Phase 02. Non è stato modificato.");
            }
        }

        private static BattleWorld CreateBattleWorld(Transform parent)
        {
            BattleWorld world = new BattleWorld();
            world.root = new GameObject(TutorialWorldRootName);
            world.root.transform.SetParent(parent, false);

            Sprite backgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Phase01PlaceholderFactory.BackgroundSpritePath);
            GameObject background = new GameObject("Background", typeof(SpriteRenderer));
            background.transform.SetParent(world.root.transform, false);
            SpriteRenderer backgroundRenderer = background.GetComponent<SpriteRenderer>();
            backgroundRenderer.sprite = backgroundSprite;
            backgroundRenderer.sortingOrder = -100;

            GameObject heroSlot = new GameObject("HeroSlot");
            heroSlot.transform.SetParent(world.root.transform, false);
            heroSlot.transform.localPosition = new Vector3(-2.25f, -4.9f, 0f);
            world.heroActor = heroSlot.transform;
            GameObject heroVisualObject = InstantiatePrefab(
                Phase01PlaceholderFactory.HeroPrefabPath,
                heroSlot.transform,
                "HeroVisual");
            world.heroVisual = RequireComponent<SpriteRenderer>(heroVisualObject, Phase01PlaceholderFactory.HeroPrefabPath);
            world.heroVisual.flipX = false;
            world.heroProjectileOrigin = CreateMarker("HeroProjectileOrigin", heroSlot.transform, new Vector3(0.75f, 1.55f, 0f));
            world.heroHitTarget = CreateMarker("HeroHitTarget", heroSlot.transform, new Vector3(0.10f, 1.45f, 0f));
            world.guardVisual = InstantiatePrefab(
                Phase02PrototypeAssetFactory.GuardRingPrefabPath,
                heroSlot.transform,
                "GuardVisual");
            world.guardVisual.transform.localPosition = new Vector3(0f, 1.45f, 0f);

            GameObject enemySlot = new GameObject("EnemySlot");
            enemySlot.transform.SetParent(world.root.transform, false);
            enemySlot.transform.localPosition = new Vector3(2.25f, -4.9f, 0f);
            world.enemyActor = enemySlot.transform;
            GameObject enemyVisualObject = InstantiatePrefab(
                Phase01PlaceholderFactory.EnemyPrefabPath,
                enemySlot.transform,
                "EnemyVisual");
            world.enemyVisual = RequireComponent<SpriteRenderer>(enemyVisualObject, Phase01PlaceholderFactory.EnemyPrefabPath);
            world.enemyVisual.flipX = true;
            world.enemyProjectileOrigin = CreateMarker("EnemyProjectileOrigin", enemySlot.transform, new Vector3(-0.75f, 1.45f, 0f));
            world.enemyHitTarget = CreateMarker("EnemyHitTarget", enemySlot.transform, new Vector3(-0.10f, 1.35f, 0f));

            GameObject effects = new GameObject("PersistentEffects");
            effects.transform.SetParent(world.root.transform, false);
            world.heroBasicProjectile = InstantiatePrefab(
                Phase02PrototypeAssetFactory.HeroBasicProjectilePrefabPath,
                effects.transform,
                "HeroBasicProjectile");
            world.heroTechniqueProjectile = InstantiatePrefab(
                Phase02PrototypeAssetFactory.HeroTechniqueProjectilePrefabPath,
                effects.transform,
                "HeroTechniqueProjectile");
            world.enemyProjectile = InstantiatePrefab(
                Phase02PrototypeAssetFactory.EnemyProjectilePrefabPath,
                effects.transform,
                "EnemyProjectile");

            return world;
        }

        private static BattleUi CreateBattleUi(Transform parent, TMP_FontAsset font)
        {
            BattleUi ui = new BattleUi();
            ui.uiRoot = new GameObject(TutorialUiRootName);
            ui.uiRoot.transform.SetParent(parent, false);
            RectTransform canvas = Phase02UiFactory.CreateCanvas(ui.uiRoot.transform);
            RectTransform safeArea = Phase02UiFactory.CreateSafeArea(canvas);

            HealthUi heroHealth = CreateHealthPanel(
                "HeroStatus",
                safeArea,
                font,
                "HERO01",
                new Vector2(0.04f, 0.845f),
                new Vector2(0.49f, 0.955f),
                Phase02UiFactory.Cyan);
            ui.heroHealthFill = heroHealth.fill;
            ui.heroHealthValue = heroHealth.value;

            HealthUi enemyHealth = CreateHealthPanel(
                "EnemyStatus",
                safeArea,
                font,
                "CREATURA CORROTTA",
                new Vector2(0.51f, 0.845f),
                new Vector2(0.96f, 0.955f),
                Phase02UiFactory.Corruption);
            ui.enemyHealthFill = enemyHealth.fill;
            ui.enemyHealthValue = enemyHealth.value;

            RectTransform statusPanel = Phase02UiFactory.CreatePanel(
                "StatusPanel",
                safeArea,
                new Vector2(0.04f, 0.745f),
                new Vector2(0.49f, 0.825f),
                Vector2.zero,
                Vector2.zero,
                new Color(0.08f, 0.25f, 0.24f, 0.95f));
            ui.statusText = Phase02UiFactory.CreateText(
                "TXT_Status",
                statusPanel,
                "STATO\nNESSUN EFFETTO",
                27f,
                Phase02UiFactory.MainText,
                TextAlignmentOptions.Center,
                font,
                Vector2.zero,
                Vector2.one,
                new Vector2(14f, 8f),
                new Vector2(-14f, -8f),
                FontStyles.Bold);

            RectTransform intentPanel = Phase02UiFactory.CreatePanel(
                "IntentPanel",
                safeArea,
                new Vector2(0.51f, 0.745f),
                new Vector2(0.96f, 0.825f),
                Vector2.zero,
                Vector2.zero,
                new Color(0.24f, 0.11f, 0.24f, 0.95f));
            ui.intentText = Phase02UiFactory.CreateText(
                "TXT_Intent",
                intentPanel,
                "INTENZIONE\nATTACCO IN ARRIVO",
                27f,
                Phase02UiFactory.MainText,
                TextAlignmentOptions.Center,
                font,
                Vector2.zero,
                Vector2.one,
                new Vector2(14f, 8f),
                new Vector2(-14f, -8f),
                FontStyles.Bold);

            RectTransform messagePanel = Phase02UiFactory.CreatePanel(
                "CombatMessage",
                safeArea,
                new Vector2(0.10f, 0.625f),
                new Vector2(0.90f, 0.70f),
                Vector2.zero,
                Vector2.zero,
                new Color(Phase02UiFactory.Background.r, Phase02UiFactory.Background.g, Phase02UiFactory.Background.b, 0.86f));
            ui.combatMessage = Phase02UiFactory.CreateText(
                "TXT_CombatMessage",
                messagePanel,
                "Impara le basi del combattimento",
                31f,
                Phase02UiFactory.Light,
                TextAlignmentOptions.Center,
                font,
                Vector2.zero,
                Vector2.one,
                new Vector2(18f, 8f),
                new Vector2(-18f, -8f),
                FontStyles.Bold);

            ui.backButton = Phase02UiFactory.CreateButton(
                "BTN_BackToMenu",
                safeArea,
                "MENU",
                font,
                new Vector2(0.04f, 0.705f),
                new Vector2(0.29f, 0.74f),
                Vector2.zero,
                Vector2.zero);

            CreateActionBar(safeArea, font, ui);
            CreateTutorialOverlay(safeArea, font, ui);
            CreateAnalyzePanel(safeArea, font, ui);
            CreateOutcomeOverlay(safeArea, font, ui);
            return ui;
        }

        private static HealthUi CreateHealthPanel(
            string name,
            RectTransform parent,
            TMP_FontAsset font,
            string label,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color fillColor)
        {
            RectTransform panel = Phase02UiFactory.CreatePanel(
                name,
                parent,
                anchorMin,
                anchorMax,
                Vector2.zero,
                Vector2.zero,
                Phase02UiFactory.Panel);
            Phase02UiFactory.CreateText(
                "TXT_" + name + "Label",
                panel,
                label,
                30f,
                Phase02UiFactory.MainText,
                TextAlignmentOptions.Left,
                font,
                new Vector2(0.06f, 0.54f),
                new Vector2(0.94f, 0.94f),
                Vector2.zero,
                Vector2.zero,
                FontStyles.Bold);

            RectTransform bar = Phase02UiFactory.CreatePanel(
                "HealthBar",
                panel,
                new Vector2(0.06f, 0.14f),
                new Vector2(0.94f, 0.48f),
                Vector2.zero,
                Vector2.zero,
                Phase02UiFactory.HighlightedPanel);
            RectTransform fillRect = Phase02UiFactory.CreatePanel(
                "Fill",
                bar,
                Vector2.zero,
                Vector2.one,
                new Vector2(4f, 4f),
                new Vector2(-4f, -4f),
                fillColor);
            Image fill = fillRect.GetComponent<Image>();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillClockwise = true;
            fill.fillAmount = 1f;

            TMP_Text value = Phase02UiFactory.CreateText(
                "TXT_HealthValue",
                bar,
                "100 / 100",
                25f,
                Phase02UiFactory.MainText,
                TextAlignmentOptions.Center,
                font,
                Vector2.zero,
                Vector2.one,
                new Vector2(8f, 2f),
                new Vector2(-8f, -2f),
                FontStyles.Bold);
            return new HealthUi(fill, value);
        }

        private static void CreateActionBar(RectTransform safeArea, TMP_FontAsset font, BattleUi ui)
        {
            GameObject actionBarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Phase02SceneFactory.BattleActionBarPrefabPath);
            ui.actionBar = PrefabUtility.InstantiatePrefab(actionBarPrefab, safeArea) as GameObject;
            if (ui.actionBar == null)
            {
                throw new InvalidOperationException(
                    "Impossibile istanziare la barra azioni: " + Phase02SceneFactory.BattleActionBarPrefabPath);
            }

            ui.actionBar.name = "ActionBar";
            RectTransform actionBarRect = ui.actionBar.GetComponent<RectTransform>();
            Phase02UiFactory.SetRect(
                actionBarRect,
                new Vector2(0.025f, 0.025f),
                new Vector2(0.975f, 0.19f),
                Vector2.zero,
                Vector2.zero);

            ui.attackButton = FindRequiredComponentInChildren<Button>(ui.actionBar.transform, "BTN_Attack");
            ui.guardButton = FindRequiredComponentInChildren<Button>(ui.actionBar.transform, "BTN_Guard");
            ui.techniqueButton = FindRequiredComponentInChildren<Button>(ui.actionBar.transform, "BTN_Technique");
            ui.analyzeButton = FindRequiredComponentInChildren<Button>(ui.actionBar.transform, "BTN_Mark");
            ui.analyzeButton.gameObject.name = "BTN_Analyze";
            TMP_Text analyzeButtonLabel = ui.analyzeButton.GetComponentInChildren<TMP_Text>(true);
            if (analyzeButtonLabel == null)
            {
                throw new InvalidOperationException("Testo del pulsante ANALIZZA mancante nel prefab della barra azioni.");
            }

            analyzeButtonLabel.text = "ANALIZZA";
            ui.techniqueButtonLabel = ui.techniqueButton.GetComponentInChildren<TMP_Text>(true);
            if (ui.techniqueButtonLabel == null)
            {
                throw new InvalidOperationException("Testo del pulsante TECNICA mancante nel prefab della barra azioni.");
            }

            ui.attackHighlight = CreateActionHighlight("AttackHighlight", ui.attackButton.transform);
            ui.guardHighlight = CreateActionHighlight("GuardHighlight", ui.guardButton.transform);
            ui.techniqueHighlight = CreateActionHighlight("TechniqueHighlight", ui.techniqueButton.transform);
            ui.analyzeHighlight = CreateActionHighlight("AnalyzeHighlight", ui.analyzeButton.transform);
        }

        private static GameObject CreateActionHighlight(string name, Transform parent)
        {
            RectTransform highlight = Phase02UiFactory.CreatePanel(
                name,
                parent,
                Vector2.zero,
                Vector2.one,
                new Vector2(-10f, -10f),
                new Vector2(10f, 10f),
                new Color(Phase02UiFactory.Gold.r, Phase02UiFactory.Gold.g, Phase02UiFactory.Gold.b, 0.42f));
            highlight.SetSiblingIndex(0);
            highlight.GetComponent<Image>().raycastTarget = false;
            return highlight.gameObject;
        }

        private static void CreateTutorialOverlay(RectTransform safeArea, TMP_FontAsset font, BattleUi ui)
        {
            RectTransform overlay = Phase02UiFactory.CreateRect("TutorialOverlay", safeArea);
            Phase02UiFactory.SetRect(overlay, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            ui.tutorialOverlay = overlay.gameObject;

            RectTransform dimmer = Phase02UiFactory.CreatePanel(
                "Dimmer",
                overlay,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                new Color(0.02f, 0.05f, 0.045f, 0.78f),
                true);
            ui.tutorialInputBlocker = dimmer.GetComponent<Image>();

            RectTransform card = Phase02UiFactory.CreatePanel(
                "TutorialCard",
                overlay,
                new Vector2(0.075f, 0.315f),
                new Vector2(0.925f, 0.625f),
                Vector2.zero,
                Vector2.zero,
                Phase02UiFactory.Panel);
            ui.tutorialStepText = Phase02UiFactory.CreateText(
                "TXT_TutorialStep",
                card,
                "PASSO 1 / 10",
                30f,
                Phase02UiFactory.Gold,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.08f, 0.76f),
                new Vector2(0.92f, 0.94f),
                Vector2.zero,
                Vector2.zero,
                FontStyles.Bold);
            ui.tutorialBodyText = Phase02UiFactory.CreateText(
                "TXT_TutorialBody",
                card,
                "Benvenuto nel combattimento.",
                38f,
                Phase02UiFactory.MainText,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.08f, 0.30f),
                new Vector2(0.92f, 0.76f),
                Vector2.zero,
                Vector2.zero,
                FontStyles.Bold);
            ui.tutorialNextButton = Phase02UiFactory.CreateButton(
                "BTN_TutorialNext",
                card,
                "AVANTI",
                font,
                new Vector2(0.18f, 0.07f),
                new Vector2(0.82f, 0.28f),
                Vector2.zero,
                Vector2.zero,
                true);
        }

        private static void CreateAnalyzePanel(RectTransform safeArea, TMP_FontAsset font, BattleUi ui)
        {
            RectTransform overlay = Phase02UiFactory.CreateRect("AnalyzePanel", safeArea);
            Phase02UiFactory.SetRect(overlay, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            ui.analyzePanel = overlay.gameObject;

            Phase02UiFactory.CreatePanel(
                "Dimmer",
                overlay,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                new Color(0.02f, 0.05f, 0.045f, 0.88f),
                true);

            RectTransform card = Phase02UiFactory.CreatePanel(
                "EnemyInfoCard",
                overlay,
                new Vector2(0.075f, 0.285f),
                new Vector2(0.925f, 0.725f),
                Vector2.zero,
                Vector2.zero,
                Phase02UiFactory.Panel);

            Phase02UiFactory.CreateText(
                "TXT_AnalyzeTitle",
                card,
                "DOSSIER NEMICO",
                36f,
                Phase02UiFactory.Gold,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.07f, 0.82f),
                new Vector2(0.93f, 0.95f),
                Vector2.zero,
                Vector2.zero,
                FontStyles.Bold);

            ui.analyzeNameText = Phase02UiFactory.CreateText(
                "TXT_EnemyInfoName",
                card,
                "NOME\nCreatura Corrotta",
                29f,
                Phase02UiFactory.MainText,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.08f, 0.63f),
                new Vector2(0.92f, 0.81f),
                Vector2.zero,
                Vector2.zero,
                FontStyles.Bold);

            ui.analyzeRaceText = Phase02UiFactory.CreateText(
                "TXT_EnemyInfoRace",
                card,
                "RAZZA\nCreatura delle Radici",
                27f,
                Phase02UiFactory.Light,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.08f, 0.39f),
                new Vector2(0.49f, 0.62f),
                Vector2.zero,
                Vector2.zero,
                FontStyles.Bold);

            ui.analyzeCorruptionText = Phase02UiFactory.CreateText(
                "TXT_EnemyInfoCorruption",
                card,
                "CORRUZIONE\n70%",
                27f,
                Phase02UiFactory.Corruption,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.51f, 0.39f),
                new Vector2(0.92f, 0.62f),
                Vector2.zero,
                Vector2.zero,
                FontStyles.Bold);

            ui.analyzeMoodText = Phase02UiFactory.CreateText(
                "TXT_EnemyInfoMood",
                card,
                "STATO ATTUALE\nArrabbiato",
                29f,
                Phase02UiFactory.MainText,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.08f, 0.19f),
                new Vector2(0.92f, 0.39f),
                Vector2.zero,
                Vector2.zero,
                FontStyles.Bold);

            ui.analyzeCloseButton = Phase02UiFactory.CreateButton(
                "BTN_CloseAnalyze",
                card,
                "CHIUDI",
                font,
                new Vector2(0.18f, 0.04f),
                new Vector2(0.82f, 0.18f),
                Vector2.zero,
                Vector2.zero,
                true);
        }

        private static void CreateOutcomeOverlay(RectTransform safeArea, TMP_FontAsset font, BattleUi ui)
        {
            RectTransform overlay = Phase02UiFactory.CreatePanel(
                "OutcomeOverlay",
                safeArea,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                new Color(Phase02UiFactory.Background.r, Phase02UiFactory.Background.g, Phase02UiFactory.Background.b, 0.92f),
                true);
            ui.outcomeOverlay = overlay.gameObject;

            RectTransform card = Phase02UiFactory.CreatePanel(
                "OutcomeCard",
                overlay,
                new Vector2(0.09f, 0.35f),
                new Vector2(0.91f, 0.65f),
                Vector2.zero,
                Vector2.zero,
                Phase02UiFactory.Panel);
            ui.outcomeText = Phase02UiFactory.CreateText(
                "TXT_Outcome",
                card,
                "VITTORIA",
                82f,
                Phase02UiFactory.Cyan,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.05f, 0.42f),
                new Vector2(0.95f, 0.88f),
                Vector2.zero,
                Vector2.zero,
                FontStyles.Bold);
            ui.outcomeMenuButton = Phase02UiFactory.CreateButton(
                "BTN_OutcomeMenu",
                card,
                "TORNA AL MENU",
                font,
                new Vector2(0.12f, 0.08f),
                new Vector2(0.88f, 0.35f),
                Vector2.zero,
                Vector2.zero,
                true);
        }

        private static void AssignControllerReferences(
            TutorialBattleController controller,
            TutorialBattleNavigation navigation,
            BattleWorld world,
            BattleUi ui)
        {
            SerializedObject serialized = new SerializedObject(controller);
            serialized.Update();

            SetInt(serialized, "heroMaxHp", 100);
            SetInt(serialized, "enemyMaxHp", 100);
            SetInt(serialized, "attackDamage", 20);
            SetInt(serialized, "techniqueDamage", 32);
            SetInt(serialized, "enemyAttackDamage", 25);
            SetInt(serialized, "techniqueCooldownTurns", 2);
            SetInt(serialized, "enemyIntelligenceLevel", 0);
            SetFloat(serialized, "resultReturnDelay", 2.5f);
            SetString(serialized, "enemyDisplayName", "Creatura Corrotta");
            SetString(serialized, "enemyRace", "Creatura delle Radici");
            SetInt(serialized, "enemyCorruptionPercent", 70);
            SetEnumIndex(serialized, "enemyMood", 2);

            SetObject(serialized, "attackButton", ui.attackButton);
            SetObject(serialized, "guardButton", ui.guardButton);
            SetObject(serialized, "techniqueButton", ui.techniqueButton);
            SetObject(serialized, "analyzeButton", ui.analyzeButton);
            SetObject(serialized, "techniqueButtonLabel", ui.techniqueButtonLabel);
            SetObject(serialized, "attackHighlight", ui.attackHighlight);
            SetObject(serialized, "guardHighlight", ui.guardHighlight);
            SetObject(serialized, "techniqueHighlight", ui.techniqueHighlight);
            SetObject(serialized, "analyzeHighlight", ui.analyzeHighlight);

            SetObject(serialized, "combatMessage", ui.combatMessage);
            SetObject(serialized, "intentText", ui.intentText);
            SetObject(serialized, "statusText", ui.statusText);
            SetObject(serialized, "heroHealthFill", ui.heroHealthFill);
            SetObject(serialized, "enemyHealthFill", ui.enemyHealthFill);
            SetObject(serialized, "heroHealthValue", ui.heroHealthValue);
            SetObject(serialized, "enemyHealthValue", ui.enemyHealthValue);

            SetObject(serialized, "heroActor", world.heroActor);
            SetObject(serialized, "enemyActor", world.enemyActor);
            SetObject(serialized, "heroVisual", world.heroVisual);
            SetObject(serialized, "enemyVisual", world.enemyVisual);
            SetObject(serialized, "heroProjectileOrigin", world.heroProjectileOrigin);
            SetObject(serialized, "heroHitTarget", world.heroHitTarget);
            SetObject(serialized, "enemyProjectileOrigin", world.enemyProjectileOrigin);
            SetObject(serialized, "enemyHitTarget", world.enemyHitTarget);

            SetObject(serialized, "heroBasicProjectile", world.heroBasicProjectile);
            SetObject(serialized, "heroTechniqueProjectile", world.heroTechniqueProjectile);
            SetObject(serialized, "enemyProjectile", world.enemyProjectile);
            SetObject(serialized, "guardVisual", world.guardVisual);

            SetObject(serialized, "tutorialOverlay", ui.tutorialOverlay);
            SetObject(serialized, "tutorialInputBlocker", ui.tutorialInputBlocker);
            SetObject(serialized, "tutorialStepText", ui.tutorialStepText);
            SetObject(serialized, "tutorialBodyText", ui.tutorialBodyText);
            SetObject(serialized, "tutorialNextButton", ui.tutorialNextButton);

            SetObject(serialized, "analyzePanel", ui.analyzePanel);
            SetObject(serialized, "analyzeNameText", ui.analyzeNameText);
            SetObject(serialized, "analyzeRaceText", ui.analyzeRaceText);
            SetObject(serialized, "analyzeCorruptionText", ui.analyzeCorruptionText);
            SetObject(serialized, "analyzeMoodText", ui.analyzeMoodText);
            SetObject(serialized, "analyzeCloseButton", ui.analyzeCloseButton);

            SetObject(serialized, "outcomeOverlay", ui.outcomeOverlay);
            SetObject(serialized, "outcomeText", ui.outcomeText);
            SetObject(serialized, "outcomeMenuButton", ui.outcomeMenuButton);
            SetObject(serialized, "navigation", navigation);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignNavigationReferences(
            TutorialBattleNavigation navigation,
            TutorialBattleController controller,
            BattleUi ui)
        {
            SerializedObject serialized = new SerializedObject(navigation);
            serialized.Update();
            SetObject(serialized, "backButton", ui.backButton);
            SetObject(serialized, "resultMenuButton", ui.outcomeMenuButton);
            SetObject(serialized, "battleController", controller);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddPersistentListeners(
            TutorialBattleController controller,
            TutorialBattleNavigation navigation,
            BattleUi ui)
        {
            UnityEventTools.AddPersistentListener(ui.attackButton.onClick, controller.PreviewAttack);
            UnityEventTools.AddPersistentListener(ui.guardButton.onClick, controller.PreviewGuard);
            UnityEventTools.AddPersistentListener(ui.techniqueButton.onClick, controller.PreviewTechnique);
            UnityEventTools.AddPersistentListener(ui.analyzeButton.onClick, controller.PreviewAnalyze);
            UnityEventTools.AddPersistentListener(ui.analyzeCloseButton.onClick, controller.CloseAnalyzePanel);
            UnityEventTools.AddPersistentListener(ui.tutorialNextButton.onClick, controller.AdvanceTutorial);
            UnityEventTools.AddPersistentListener(ui.backButton.onClick, navigation.BackToMenu);
            UnityEventTools.AddPersistentListener(ui.outcomeMenuButton.onClick, navigation.BackToMenu);
        }

        private static void SetInitialActiveState(BattleWorld world, BattleUi ui)
        {
            world.heroBasicProjectile.SetActive(false);
            world.heroTechniqueProjectile.SetActive(false);
            world.enemyProjectile.SetActive(false);
            world.guardVisual.SetActive(false);

            ui.attackButton.interactable = false;
            ui.guardButton.interactable = false;
            ui.techniqueButton.interactable = false;
            ui.analyzeButton.interactable = false;
            ui.attackHighlight.SetActive(false);
            ui.guardHighlight.SetActive(false);
            ui.techniqueHighlight.SetActive(false);
            ui.analyzeHighlight.SetActive(false);
            ui.tutorialOverlay.SetActive(true);
            ui.analyzePanel.SetActive(false);
            ui.outcomeOverlay.SetActive(false);
        }

        private static void EnsureCamera(Transform sceneRoot, Phase03TutorialSetupReport report)
        {
            Camera existing = sceneRoot.GetComponentInChildren<Camera>(true);
            if (existing != null)
            {
                report.Preserve("Main Camera");
                return;
            }

            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(sceneRoot, false);
            cameraObject.transform.localPosition = new Vector3(0f, 0f, -10f);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 9.6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Phase02UiFactory.Background;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            report.Create("Main Camera");
        }

        private static void EnsureEventSystem(Transform sceneRoot, Phase03TutorialSetupReport report)
        {
            EventSystem existing = sceneRoot.GetComponentInChildren<EventSystem>(true);
            if (existing != null)
            {
                report.Preserve("EventSystem");
                return;
            }

            Phase02UiFactory.CreateEventSystem(sceneRoot);
            report.Create("EventSystem");
        }

        private static void ConfigureBuildSettings(Phase03TutorialSetupReport report)
        {
            List<EditorBuildSettingsScene> ordered = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(MainMenuScenePath, true),
                new EditorBuildSettingsScene(TutorialScenePath, true)
            };

            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                if (existing.path != MainMenuScenePath &&
                    existing.path != TutorialScenePath &&
                    ordered.All(scene => scene.path != existing.path))
                {
                    ordered.Add(existing);
                }
            }

            EditorBuildSettings.scenes = ordered.ToArray();
            report.Configure("Build order: MainMenu, Tutorial, scene preesistenti");
        }

        private static GameObject InstantiatePrefab(string path, Transform parent, string instanceName)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                throw new InvalidOperationException("Prefab persistente mancante: " + path);
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException("Impossibile istanziare il prefab: " + path);
            }

            instance.name = instanceName;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            return instance;
        }

        private static Transform CreateMarker(string name, Transform parent, Vector3 localPosition)
        {
            GameObject marker = new GameObject(name);
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = localPosition;
            return marker.transform;
        }

        private static T RequireComponent<T>(GameObject gameObject, string sourcePath) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            if (component == null)
            {
                throw new InvalidOperationException(
                    "Il prefab " + sourcePath + " non contiene il componente " + typeof(T).Name + ".");
            }

            return component;
        }

        private static T FindRequiredComponentInChildren<T>(Transform root, string objectName) where T : Component
        {
            T component = root.GetComponentsInChildren<T>(true)
                .FirstOrDefault(candidate => candidate.gameObject.name == objectName);
            if (component == null)
            {
                throw new InvalidOperationException(
                    "Componente " + typeof(T).Name + " mancante nell'oggetto " + objectName + ".");
            }

            return component;
        }

        private static Transform FindDirectChild(Transform parent, string childName)
        {
            for (int index = 0; index < parent.childCount; index++)
            {
                Transform child = parent.GetChild(index);
                if (child.name == childName)
                {
                    return child;
                }
            }

            return null;
        }

        private static void SetObject(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = RequireProperty(serialized, propertyName);
            property.objectReferenceValue = value;
        }

        private static void SetInt(SerializedObject serialized, string propertyName, int value)
        {
            SerializedProperty property = RequireProperty(serialized, propertyName);
            property.intValue = value;
        }

        private static void SetFloat(SerializedObject serialized, string propertyName, float value)
        {
            SerializedProperty property = RequireProperty(serialized, propertyName);
            property.floatValue = value;
        }

        private static void SetString(SerializedObject serialized, string propertyName, string value)
        {
            SerializedProperty property = RequireProperty(serialized, propertyName);
            property.stringValue = value;
        }

        private static void SetEnumIndex(SerializedObject serialized, string propertyName, int value)
        {
            SerializedProperty property = RequireProperty(serialized, propertyName);
            if (property.propertyType != SerializedPropertyType.Enum)
            {
                throw new InvalidOperationException(
                    serialized.targetObject.GetType().Name + "." + propertyName + " deve essere un enum serializzato.");
            }

            if (value < 0 || value >= property.enumDisplayNames.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Indice enum non valido per " + propertyName + ".");
            }

            property.enumValueIndex = value;
        }

        private static SerializedProperty RequireProperty(SerializedObject serialized, string propertyName)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    serialized.targetObject.GetType().Name +
                    " non espone il riferimento serializzato " + propertyName + ".");
            }

            return property;
        }

        private sealed class BattleWorld
        {
            internal GameObject root;
            internal Transform heroActor;
            internal Transform enemyActor;
            internal SpriteRenderer heroVisual;
            internal SpriteRenderer enemyVisual;
            internal Transform heroProjectileOrigin;
            internal Transform heroHitTarget;
            internal Transform enemyProjectileOrigin;
            internal Transform enemyHitTarget;
            internal GameObject heroBasicProjectile;
            internal GameObject heroTechniqueProjectile;
            internal GameObject enemyProjectile;
            internal GameObject guardVisual;
        }

        private sealed class BattleUi
        {
            internal GameObject uiRoot;
            internal GameObject actionBar;
            internal Button attackButton;
            internal Button guardButton;
            internal Button techniqueButton;
            internal Button analyzeButton;
            internal TMP_Text techniqueButtonLabel;
            internal GameObject attackHighlight;
            internal GameObject guardHighlight;
            internal GameObject techniqueHighlight;
            internal GameObject analyzeHighlight;
            internal TMP_Text combatMessage;
            internal TMP_Text intentText;
            internal TMP_Text statusText;
            internal Image heroHealthFill;
            internal Image enemyHealthFill;
            internal TMP_Text heroHealthValue;
            internal TMP_Text enemyHealthValue;
            internal Button backButton;
            internal GameObject tutorialOverlay;
            internal Image tutorialInputBlocker;
            internal TMP_Text tutorialStepText;
            internal TMP_Text tutorialBodyText;
            internal Button tutorialNextButton;
            internal GameObject analyzePanel;
            internal TMP_Text analyzeNameText;
            internal TMP_Text analyzeRaceText;
            internal TMP_Text analyzeCorruptionText;
            internal TMP_Text analyzeMoodText;
            internal Button analyzeCloseButton;
            internal GameObject outcomeOverlay;
            internal TMP_Text outcomeText;
            internal Button outcomeMenuButton;
        }

        private readonly struct HealthUi
        {
            internal HealthUi(Image fill, TMP_Text value)
            {
                this.fill = fill;
                this.value = value;
            }

            internal readonly Image fill;
            internal readonly TMP_Text value;
        }
    }
}
#endif
