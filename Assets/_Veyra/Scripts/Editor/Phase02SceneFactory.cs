#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Veyra.Combat.Preview;
using Veyra.UI.MainMenu;
using Veyra.UI.Settings;

namespace Veyra.Editor
{
    internal static class Phase02SceneFactory
    {
        internal const string MainMenuScenePath = "Assets/_Veyra/Scenes/SCN_MainMenu.unity";
        internal const string TutorialScenePath = "Assets/_Veyra/Scenes/SCN_W01_L01_Tutorial.unity";
        internal const string MainMenuStartCardPrefabPath =
            "Assets/_Veyra/Prefabs/UI/MainMenu/PF_MainMenuStartCard_Prototype.prefab";
        internal const string SettingsModalPrefabPath =
            "Assets/_Veyra/Prefabs/UI/Settings/PF_SettingsModal_Prototype.prefab";
        internal const string BattleActionBarPrefabPath =
            "Assets/_Veyra/Prefabs/UI/Battle/PF_BattleActionBar_Prototype.prefab";

        internal static void CreateScenes(Phase02SetupReport report)
        {
            PreserveDirtySceneBeforeAuthoring();
            CreateMainMenuSceneIfMissing(report);
            CreateTutorialSceneIfMissing(report);
            ConfigureBuildSettings(report);
        }

        private static void PreserveDirtySceneBeforeAuthoring()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isDirty)
            {
                return;
            }

            if (Application.isBatchMode)
            {
                throw new InvalidOperationException(
                    "La scena attiva contiene modifiche non salvate. Salvarle prima di eseguire il tool Phase 02 in batch mode.");
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                throw new OperationCanceledException("Generazione Phase 02 annullata per preservare la scena modificata.");
            }
        }

        private static void CreateMainMenuSceneIfMissing(Phase02SetupReport report)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuScenePath) != null)
            {
                report.Preserve(MainMenuScenePath + " (scena esistente non sovrascritta)");
                return;
            }

            TMP_FontAsset font = Phase02UiFactory.LoadRequiredFont();
            Sprite backgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Phase02PrototypeAssetFactory.MenuBackgroundPath);
            Sprite heroSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Phase01PlaceholderFactory.HeroSpritePath);
            if (backgroundSprite == null || heroSprite == null)
            {
                throw new InvalidOperationException("Sprite persistenti del menu mancanti o non importati.");
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject sceneRoot = new GameObject("SCN_MainMenu");
            CreateCamera(sceneRoot.transform);

            GameObject uiRoot = new GameObject("UIRoot");
            uiRoot.transform.SetParent(sceneRoot.transform, false);
            RectTransform canvas = Phase02UiFactory.CreateCanvas(uiRoot.transform);
            RectTransform safeArea = Phase02UiFactory.CreateSafeArea(canvas);

            RectTransform backgroundLayers = Phase02UiFactory.CreatePanel(
                "BackgroundLayers", safeArea, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                Phase02UiFactory.Background);
            Image backgroundImage = backgroundLayers.GetComponent<Image>();
            backgroundImage.sprite = backgroundSprite;
            backgroundImage.preserveAspect = false;
            backgroundImage.raycastTarget = false;

            RectTransform titleArea = Phase02UiFactory.CreateRect("TitleArea", safeArea);
            Phase02UiFactory.SetRect(
                titleArea, new Vector2(0.06f, 0.755f), new Vector2(0.94f, 0.96f),
                Vector2.zero, Vector2.zero);
            CreateMenuTitle(titleArea, font);

            RectTransform heroPreview = Phase02UiFactory.CreateRect("HeroPreview", safeArea);
            Phase02UiFactory.SetRect(
                heroPreview, new Vector2(0.045f, 0.245f), new Vector2(0.32f, 0.46f),
                Vector2.zero, Vector2.zero);
            Image heroImage = heroPreview.gameObject.AddComponent<Image>();
            heroImage.sprite = heroSprite;
            heroImage.preserveAspect = true;
            heroImage.raycastTarget = false;
            heroImage.color = Color.white;

            RectTransform startCard = CreateStartCard(safeArea, font, out Button startButton, out Button settingsButton);
            RectTransform footer = Phase02UiFactory.CreateRect("Footer", safeArea);
            Phase02UiFactory.SetRect(footer, new Vector2(0.08f, 0.01f), new Vector2(0.92f, 0.065f), Vector2.zero, Vector2.zero);
            Phase02UiFactory.CreateText(
                "TXT_PrototypeVersion", footer, "Prototipo 0.1", 28f, Phase02UiFactory.SecondaryText,
                TextAlignmentOptions.Center, font, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            RectTransform dimmer = Phase02UiFactory.CreatePanel(
                "Dimmer", safeArea, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.02f, 0.05f, 0.045f, 0.82f), true);

            SettingsUi settingsUi = CreateSettingsModal(safeArea, font);
            OverlayUi overlays = CreateMenuOverlays(safeArea, font);

            SettingsPanelController settingsController = uiRoot.AddComponent<SettingsPanelController>();
            MainMenuController mainMenuController = uiRoot.AddComponent<MainMenuController>();
            AssignSettingsReferences(settingsController, dimmer.gameObject, settingsUi);
            AssignMainMenuReferences(
                mainMenuController, startButton, settingsButton, settingsController,
                overlays.loadingOverlay.gameObject, overlays.errorModal.gameObject, overlays.errorText);

            Phase02UiFactory.SavePrefabSnapshotIfMissing(
                startCard.gameObject, MainMenuStartCardPrefabPath, report);
            Phase02UiFactory.SavePrefabSnapshotIfMissing(
                settingsUi.modal.gameObject, SettingsModalPrefabPath, report);

            AddMainMenuPersistentListeners(mainMenuController, settingsController, startButton, settingsButton, settingsUi, overlays);
            Phase02UiFactory.CreateEventSystem(sceneRoot.transform);

            dimmer.gameObject.SetActive(false);
            settingsUi.modal.gameObject.SetActive(false);
            overlays.loadingOverlay.gameObject.SetActive(false);
            overlays.errorModal.gameObject.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, MainMenuScenePath))
            {
                throw new InvalidOperationException("Impossibile salvare la scena: " + MainMenuScenePath);
            }

            report.Create(MainMenuScenePath);
        }

        private static void CreateTutorialSceneIfMissing(Phase02SetupReport report)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TutorialScenePath) != null)
            {
                report.Preserve(TutorialScenePath + " (scena esistente non sovrascritta)");
                return;
            }

            TMP_FontAsset font = Phase02UiFactory.LoadRequiredFont();
            Sprite backgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Phase01PlaceholderFactory.BackgroundSpritePath);
            if (backgroundSprite == null)
            {
                throw new InvalidOperationException("Sfondo persistente della Fase 1 mancante.");
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject sceneRoot = new GameObject("SCN_W01_L01_Tutorial");
            CreateCamera(sceneRoot.transform);

            BattleWorld world = CreateBattleWorld(sceneRoot.transform, backgroundSprite);
            BattleUi ui = CreateBattleUi(sceneRoot.transform, font);

            BattlePreviewController previewController = world.root.AddComponent<BattlePreviewController>();
            BattlePreviewNavigation navigation = ui.uiRoot.AddComponent<BattlePreviewNavigation>();
            AssignBattlePreviewReferences(previewController, world, ui);
            AssignBattleNavigationReferences(navigation, ui.backButton, previewController);

            Phase02UiFactory.SavePrefabSnapshotIfMissing(
                ui.actionBar.gameObject, BattleActionBarPrefabPath, report);
            AddBattlePersistentListeners(previewController, navigation, ui);
            Phase02UiFactory.CreateEventSystem(sceneRoot.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, TutorialScenePath))
            {
                throw new InvalidOperationException("Impossibile salvare la scena: " + TutorialScenePath);
            }

            report.Create(TutorialScenePath);
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
            camera.backgroundColor = Phase02UiFactory.Background;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
        }

        private static void CreateMenuTitle(RectTransform titleArea, TMP_FontAsset font)
        {
            VerticalLayoutGroup layout = titleArea.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = 4f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            TMP_Text title = Phase02UiFactory.CreateText(
                "TXT_Title", titleArea, "VEYRA", 112f, Phase02UiFactory.MainText,
                TextAlignmentOptions.Center, font, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, FontStyles.Bold);
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 128f;

            TMP_Text subtitle = Phase02UiFactory.CreateText(
                "TXT_Subtitle", titleArea, "CORRUPTED REALMS", 42f, Phase02UiFactory.Cyan,
                TextAlignmentOptions.Center, font, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, FontStyles.Bold);
            subtitle.characterSpacing = 8f;
            subtitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 60f;

            TMP_Text phrase = Phase02UiFactory.CreateText(
                "TXT_Tagline", titleArea, "Riporta la luce nei mondi corrotti.", 34f, Phase02UiFactory.SecondaryText,
                TextAlignmentOptions.Center, font, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            phrase.gameObject.AddComponent<LayoutElement>().preferredHeight = 56f;
        }

        private static RectTransform CreateStartCard(
            RectTransform safeArea,
            TMP_FontAsset font,
            out Button startButton,
            out Button settingsButton)
        {
            RectTransform card = Phase02UiFactory.CreatePanel(
                "StartCard", safeArea,
                new Vector2(0.22f, 0.105f), new Vector2(0.955f, 0.68f),
                Vector2.zero, Vector2.zero, new Color(Phase02UiFactory.Panel.r, Phase02UiFactory.Panel.g, Phase02UiFactory.Panel.b, 0.97f));

            Phase02UiFactory.CreateText(
                "TXT_World", card, "MONDO 01", 34f, Phase02UiFactory.Gold,
                TextAlignmentOptions.Left, font,
                new Vector2(0f, 0.82f), new Vector2(1f, 0.95f), new Vector2(46f, 0f), new Vector2(-46f, 0f), FontStyles.Bold);
            Phase02UiFactory.CreateText(
                "TXT_Level", card, "Bosco delle Radici\nLivello 1 · Tutorial", 52f, Phase02UiFactory.MainText,
                TextAlignmentOptions.Left, font,
                new Vector2(0f, 0.61f), new Vector2(1f, 0.83f), new Vector2(46f, 0f), new Vector2(-46f, 0f), FontStyles.Bold);
            Phase02UiFactory.CreateText(
                "TXT_HeroName", card, "Hero01", 34f, Phase02UiFactory.Cyan,
                TextAlignmentOptions.Left, font,
                new Vector2(0f, 0.52f), new Vector2(1f, 0.61f), new Vector2(46f, 0f), new Vector2(-46f, 0f));
            Phase02UiFactory.CreateText(
                "TXT_Objective", card, "Purifica la prima creatura corrotta", 31f, Phase02UiFactory.SecondaryText,
                TextAlignmentOptions.Left, font,
                new Vector2(0f, 0.38f), new Vector2(1f, 0.52f), new Vector2(46f, 0f), new Vector2(-46f, 0f));

            RectTransform buttonStack = Phase02UiFactory.CreateRect("ButtonStack", card);
            Phase02UiFactory.SetRect(buttonStack, Vector2.zero, new Vector2(1f, 0f), new Vector2(46f, 42f), new Vector2(-46f, 384f));
            VerticalLayoutGroup layout = buttonStack.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 36f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            startButton = Phase02UiFactory.CreateButton(
                "BTN_Start", buttonStack, "INIZIA", font,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
            LayoutElement startLayout = startButton.gameObject.AddComponent<LayoutElement>();
            startLayout.minHeight = 156f;
            startLayout.preferredHeight = 162f;

            settingsButton = Phase02UiFactory.CreateButton(
                "BTN_Settings", buttonStack, "IMPOSTAZIONI", font,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            LayoutElement settingsLayout = settingsButton.gameObject.AddComponent<LayoutElement>();
            settingsLayout.minHeight = 144f;
            settingsLayout.preferredHeight = 144f;
            return card;
        }

        private static SettingsUi CreateSettingsModal(RectTransform safeArea, TMP_FontAsset font)
        {
            SettingsUi ui = new SettingsUi();
            ui.modal = Phase02UiFactory.CreatePanel(
                "SettingsModal", safeArea,
                new Vector2(0.065f, 0.11f), new Vector2(0.935f, 0.89f),
                Vector2.zero, Vector2.zero, Phase02UiFactory.Panel, true);

            Phase02UiFactory.CreateText(
                "TXT_SettingsTitle", ui.modal, "IMPOSTAZIONI", 58f, Phase02UiFactory.MainText,
                TextAlignmentOptions.Center, font,
                new Vector2(0.08f, 0.86f), new Vector2(0.92f, 0.97f), Vector2.zero, Vector2.zero, FontStyles.Bold);

            CreateSettingsSliderRow(ui.modal, font, "Volume generale", 0.70f, out ui.masterSlider, out ui.masterValueText, "Master");
            CreateSettingsSliderRow(ui.modal, font, "Volume musica", 0.54f, out ui.musicSlider, out ui.musicValueText, "Music");
            CreateSettingsSliderRow(ui.modal, font, "Volume effetti", 0.38f, out ui.sfxSlider, out ui.sfxValueText, "Sfx");

            Phase02UiFactory.CreateText(
                "TXT_VibrationLabel", ui.modal, "Vibrazione", 36f, Phase02UiFactory.MainText,
                TextAlignmentOptions.Left, font,
                new Vector2(0.08f, 0.24f), new Vector2(0.60f, 0.33f), Vector2.zero, Vector2.zero);
            ui.vibrationToggle = Phase02UiFactory.CreateToggle(
                "TGL_Vibration", ui.modal, new Vector2(0.67f, 0.235f), new Vector2(0.90f, 0.33f));

            ui.resetButton = Phase02UiFactory.CreateButton(
                "BTN_Reset", ui.modal, "RIPRISTINA PREDEFINITI", font,
                new Vector2(0.08f, 0.115f), new Vector2(0.92f, 0.205f), Vector2.zero, Vector2.zero);
            ui.closeButton = Phase02UiFactory.CreateButton(
                "BTN_CloseSettings", ui.modal, "CHIUDI", font,
                new Vector2(0.08f, 0.02f), new Vector2(0.92f, 0.10f), Vector2.zero, Vector2.zero, true);
            return ui;
        }

        private static void CreateSettingsSliderRow(
            RectTransform modal,
            TMP_FontAsset font,
            string label,
            float anchorY,
            out Slider slider,
            out TMP_Text valueText,
            string suffix)
        {
            Phase02UiFactory.CreateText(
                "TXT_" + suffix + "Label", modal, label, 34f, Phase02UiFactory.MainText,
                TextAlignmentOptions.Left, font,
                new Vector2(0.08f, anchorY + 0.075f), new Vector2(0.70f, anchorY + 0.145f), Vector2.zero, Vector2.zero);
            valueText = Phase02UiFactory.CreateText(
                "TXT_" + suffix + "Value", modal, "100%", 32f, Phase02UiFactory.Cyan,
                TextAlignmentOptions.Right, font,
                new Vector2(0.72f, anchorY + 0.075f), new Vector2(0.92f, anchorY + 0.145f), Vector2.zero, Vector2.zero);
            slider = Phase02UiFactory.CreateSlider(
                "SLD_" + suffix, modal,
                new Vector2(0.08f, anchorY), new Vector2(0.92f, anchorY + 0.075f));
        }

        private static OverlayUi CreateMenuOverlays(RectTransform safeArea, TMP_FontAsset font)
        {
            OverlayUi ui = new OverlayUi();
            ui.loadingOverlay = Phase02UiFactory.CreatePanel(
                "LoadingOverlay", safeArea, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(Phase02UiFactory.Background.r, Phase02UiFactory.Background.g, Phase02UiFactory.Background.b, 0.96f), true);
            Phase02UiFactory.CreateText(
                "TXT_Loading", ui.loadingOverlay, "LA LINFA RISALE...\nCaricamento", 48f, Phase02UiFactory.Light,
                TextAlignmentOptions.Center, font,
                new Vector2(0.08f, 0.38f), new Vector2(0.92f, 0.62f), Vector2.zero, Vector2.zero, FontStyles.Bold);

            ui.errorModal = Phase02UiFactory.CreatePanel(
                "ErrorModal", safeArea, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(Phase02UiFactory.Background.r, Phase02UiFactory.Background.g, Phase02UiFactory.Background.b, 0.94f), true);
            RectTransform errorCard = Phase02UiFactory.CreatePanel(
                "ErrorCard", ui.errorModal,
                new Vector2(0.09f, 0.34f), new Vector2(0.91f, 0.66f), Vector2.zero, Vector2.zero,
                Phase02UiFactory.Panel, true);
            Phase02UiFactory.CreateText(
                "TXT_ErrorTitle", errorCard, "ERRORE", 48f, Phase02UiFactory.Error,
                TextAlignmentOptions.Center, font,
                new Vector2(0.08f, 0.70f), new Vector2(0.92f, 0.92f), Vector2.zero, Vector2.zero, FontStyles.Bold);
            ui.errorText = Phase02UiFactory.CreateText(
                "TXT_ErrorMessage", errorCard, "Impossibile aprire il tutorial.", 30f, Phase02UiFactory.MainText,
                TextAlignmentOptions.Center, font,
                new Vector2(0.08f, 0.33f), new Vector2(0.92f, 0.69f), Vector2.zero, Vector2.zero);
            ui.errorCloseButton = Phase02UiFactory.CreateButton(
                "BTN_CloseError", errorCard, "CHIUDI", font,
                new Vector2(0.10f, 0.07f), new Vector2(0.90f, 0.30f), Vector2.zero, Vector2.zero);
            return ui;
        }

        private static BattleWorld CreateBattleWorld(Transform parent, Sprite backgroundSprite)
        {
            BattleWorld world = new BattleWorld();
            world.root = new GameObject("BattlePreviewRoot");
            world.root.transform.SetParent(parent, false);

            GameObject background = new GameObject("Background", typeof(SpriteRenderer));
            background.transform.SetParent(world.root.transform, false);
            SpriteRenderer backgroundRenderer = background.GetComponent<SpriteRenderer>();
            backgroundRenderer.sprite = backgroundSprite;
            backgroundRenderer.sortingOrder = -100;

            GameObject heroSlot = new GameObject("HeroSlot");
            heroSlot.transform.SetParent(world.root.transform, false);
            heroSlot.transform.localPosition = new Vector3(-2.55f, -3.25f, 0f);
            GameObject heroVisual = InstantiatePrefab(
                Phase02PrototypeAssetFactory.HeroCombatDotPrefabPath, heroSlot.transform, "HeroVisual");
            world.heroVisual = heroVisual.GetComponent<SpriteRenderer>();
            world.heroProjectileOrigin = CreateMarker("HeroProjectileOrigin", heroSlot.transform, new Vector3(0.70f, 0.25f, 0f));
            world.heroHitTarget = CreateMarker("HeroHitTarget", heroSlot.transform, new Vector3(0.15f, 0.05f, 0f));
            world.guardVisual = InstantiatePrefab(
                Phase02PrototypeAssetFactory.GuardRingPrefabPath, heroSlot.transform, "GuardVisual");
            world.guardVisual.transform.localPosition = Vector3.zero;
            world.guardVisual.SetActive(false);

            GameObject enemySlot = new GameObject("EnemySlot");
            enemySlot.transform.SetParent(world.root.transform, false);
            enemySlot.transform.localPosition = new Vector3(2.25f, 2.25f, 0f);
            GameObject enemyVisual = InstantiatePrefab(
                Phase02PrototypeAssetFactory.EnemyCombatDotPrefabPath, enemySlot.transform, "EnemyVisual");
            world.enemyVisual = enemyVisual.GetComponent<SpriteRenderer>();
            world.enemyProjectileOrigin = CreateMarker("EnemyProjectileOrigin", enemySlot.transform, new Vector3(-0.70f, -0.25f, 0f));
            world.enemyHitTarget = CreateMarker("EnemyHitTarget", enemySlot.transform, new Vector3(-0.15f, -0.05f, 0f));
            world.markPreview = InstantiatePrefab(
                Phase02PrototypeAssetFactory.MarkPulsePrefabPath, enemySlot.transform, "MarkPreview");
            world.markPreview.transform.localPosition = Vector3.zero;
            world.markPreview.SetActive(false);

            GameObject previewEffects = new GameObject("PreviewEffects");
            previewEffects.transform.SetParent(world.root.transform, false);
            world.heroBasicProjectile = InstantiatePrefab(
                Phase02PrototypeAssetFactory.HeroBasicProjectilePrefabPath, previewEffects.transform, "HeroBasicProjectile");
            world.heroTechniqueProjectile = InstantiatePrefab(
                Phase02PrototypeAssetFactory.HeroTechniqueProjectilePrefabPath, previewEffects.transform, "HeroTechniqueProjectile");
            world.enemyProjectile = InstantiatePrefab(
                Phase02PrototypeAssetFactory.EnemyProjectilePrefabPath, previewEffects.transform, "EnemyProjectile");
            world.heroBasicProjectile.SetActive(false);
            world.heroTechniqueProjectile.SetActive(false);
            world.enemyProjectile.SetActive(false);
            return world;
        }

        private static BattleUi CreateBattleUi(Transform parent, TMP_FontAsset font)
        {
            BattleUi ui = new BattleUi();
            ui.uiRoot = new GameObject("UIRoot");
            ui.uiRoot.transform.SetParent(parent, false);
            RectTransform canvas = Phase02UiFactory.CreateCanvas(ui.uiRoot.transform);
            RectTransform safeArea = Phase02UiFactory.CreateSafeArea(canvas);

            CreateHudPanel(
                "EnemyPanel", safeArea, font, "Creatura Corrotta\nCorruzione 100/100",
                new Vector2(0.04f, 0.84f), new Vector2(0.66f, 0.965f), Phase02UiFactory.Panel, TextAlignmentOptions.Left);
            CreateHudPanel(
                "IntentPanel", safeArea, font, "Intenzione\nAttacco in arrivo",
                new Vector2(0.40f, 0.71f), new Vector2(0.96f, 0.815f),
                new Color(0.24f, 0.11f, 0.24f, 0.95f), TextAlignmentOptions.Center);

            RectTransform combatMessagePanel = Phase02UiFactory.CreatePanel(
                "CombatMessage", safeArea,
                new Vector2(0.12f, 0.555f), new Vector2(0.88f, 0.625f), Vector2.zero, Vector2.zero,
                new Color(Phase02UiFactory.Background.r, Phase02UiFactory.Background.g, Phase02UiFactory.Background.b, 0.84f));
            ui.combatMessage = Phase02UiFactory.CreateText(
                "TXT_CombatMessage", combatMessagePanel, "Seleziona un comando", 34f, Phase02UiFactory.Light,
                TextAlignmentOptions.Center, font, Vector2.zero, Vector2.one, new Vector2(18f, 8f), new Vector2(-18f, -8f), FontStyles.Bold);

            CreateHudPanel(
                "HeroPanel", safeArea, font, "Hero01\nVita 100/100",
                new Vector2(0.04f, 0.245f), new Vector2(0.61f, 0.36f), Phase02UiFactory.Panel, TextAlignmentOptions.Left);
            CreateHudPanel(
                "FocusPanel", safeArea, font, "Focus 0/3",
                new Vector2(0.65f, 0.255f), new Vector2(0.96f, 0.35f),
                new Color(0.08f, 0.25f, 0.24f, 0.95f), TextAlignmentOptions.Center);

            ui.actionBar = Phase02UiFactory.CreatePanel(
                "ActionBar", safeArea,
                new Vector2(0.025f, 0.025f), new Vector2(0.975f, 0.19f), Vector2.zero, Vector2.zero,
                new Color(Phase02UiFactory.Panel.r, Phase02UiFactory.Panel.g, Phase02UiFactory.Panel.b, 0.98f));
            HorizontalLayoutGroup layout = ui.actionBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 28, 28);
            layout.spacing = 24f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = true;

            ui.attackButton = CreateBattleButton("BTN_Attack", "ATTACCO", ui.actionBar, font);
            ui.guardButton = CreateBattleButton("BTN_Guard", "GUARDIA", ui.actionBar, font);
            ui.techniqueButton = CreateBattleButton("BTN_Technique", "TECNICA", ui.actionBar, font);
            ui.markButton = CreateBattleButton("BTN_Mark", "MARCHIO", ui.actionBar, font);

            ui.backButton = Phase02UiFactory.CreateButton(
                "BTN_BackToMenu", safeArea, "MENU", font,
                new Vector2(0.04f, 0.71f), new Vector2(0.34f, 0.80f), Vector2.zero, Vector2.zero);
            return ui;
        }

        private static void CreateHudPanel(
            string name,
            RectTransform parent,
            TMP_FontAsset font,
            string content,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color color,
            TextAlignmentOptions alignment)
        {
            RectTransform panel = Phase02UiFactory.CreatePanel(
                name, parent, anchorMin, anchorMax, Vector2.zero, Vector2.zero, color);
            Phase02UiFactory.CreateText(
                "TXT_" + name, panel, content, 34f, Phase02UiFactory.MainText,
                alignment, font, Vector2.zero, Vector2.one, new Vector2(30f, 16f), new Vector2(-30f, -16f), FontStyles.Bold);
        }

        private static Button CreateBattleButton(string name, string label, RectTransform parent, TMP_FontAsset font)
        {
            Button button = Phase02UiFactory.CreateButton(
                name, parent, label, font, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            LayoutElement element = button.gameObject.AddComponent<LayoutElement>();
            element.minWidth = 144f;
            element.minHeight = 144f;
            element.flexibleWidth = 1f;
            element.flexibleHeight = 1f;
            return button;
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

        private static void AssignSettingsReferences(
            SettingsPanelController controller,
            GameObject dimmer,
            SettingsUi ui)
        {
            SerializedObject serialized = new SerializedObject(controller);
            SetObject(serialized, "dimmer", dimmer);
            SetObject(serialized, "modalRoot", ui.modal.gameObject);
            SetObject(serialized, "masterVolumeSlider", ui.masterSlider);
            SetObject(serialized, "musicVolumeSlider", ui.musicSlider);
            SetObject(serialized, "sfxVolumeSlider", ui.sfxSlider);
            SetObject(serialized, "vibrationToggle", ui.vibrationToggle);
            SetObject(serialized, "masterValueText", ui.masterValueText);
            SetObject(serialized, "musicValueText", ui.musicValueText);
            SetObject(serialized, "sfxValueText", ui.sfxValueText);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignMainMenuReferences(
            MainMenuController controller,
            Button startButton,
            Button settingsButton,
            SettingsPanelController settingsController,
            GameObject loadingOverlay,
            GameObject errorModal,
            TMP_Text errorText)
        {
            SerializedObject serialized = new SerializedObject(controller);
            SetObject(serialized, "startButton", startButton);
            SetObject(serialized, "settingsButton", settingsButton);
            SetObject(serialized, "settingsPanel", settingsController);
            SetObject(serialized, "loadingOverlay", loadingOverlay);
            SetObject(serialized, "errorModal", errorModal);
            SetObject(serialized, "errorMessage", errorText);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignBattlePreviewReferences(BattlePreviewController controller, BattleWorld world, BattleUi ui)
        {
            SerializedObject serialized = new SerializedObject(controller);
            SerializedProperty buttons = serialized.FindProperty("actionButtons");
            buttons.arraySize = 4;
            buttons.GetArrayElementAtIndex(0).objectReferenceValue = ui.attackButton;
            buttons.GetArrayElementAtIndex(1).objectReferenceValue = ui.guardButton;
            buttons.GetArrayElementAtIndex(2).objectReferenceValue = ui.techniqueButton;
            buttons.GetArrayElementAtIndex(3).objectReferenceValue = ui.markButton;
            SetObject(serialized, "combatMessage", ui.combatMessage);
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
            SetObject(serialized, "markPreview", world.markPreview);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignBattleNavigationReferences(
            BattlePreviewNavigation navigation,
            Button backButton,
            BattlePreviewController previewController)
        {
            SerializedObject serialized = new SerializedObject(navigation);
            SetObject(serialized, "backButton", backButton);
            SetObject(serialized, "previewController", previewController);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObject(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    serialized.targetObject.GetType().Name + " non espone il riferimento serializzato " + propertyName + ".");
            }

            property.objectReferenceValue = value;
        }

        private static void AddMainMenuPersistentListeners(
            MainMenuController mainController,
            SettingsPanelController settingsController,
            Button startButton,
            Button settingsButton,
            SettingsUi settingsUi,
            OverlayUi overlays)
        {
            UnityEventTools.AddPersistentListener(startButton.onClick, mainController.StartGame);
            UnityEventTools.AddPersistentListener(settingsButton.onClick, mainController.OpenSettings);
            UnityEventTools.AddPersistentListener(settingsUi.closeButton.onClick, settingsController.Close);
            UnityEventTools.AddPersistentListener(settingsUi.resetButton.onClick, settingsController.ResetToDefaults);
            UnityEventTools.AddPersistentListener(settingsUi.masterSlider.onValueChanged, settingsController.OnMasterVolumeChanged);
            UnityEventTools.AddPersistentListener(settingsUi.musicSlider.onValueChanged, settingsController.OnMusicVolumeChanged);
            UnityEventTools.AddPersistentListener(settingsUi.sfxSlider.onValueChanged, settingsController.OnSfxVolumeChanged);
            UnityEventTools.AddPersistentListener(settingsUi.vibrationToggle.onValueChanged, settingsController.OnVibrationChanged);
            UnityEventTools.AddPersistentListener(overlays.errorCloseButton.onClick, mainController.CloseError);
        }

        private static void AddBattlePersistentListeners(
            BattlePreviewController previewController,
            BattlePreviewNavigation navigation,
            BattleUi ui)
        {
            UnityEventTools.AddPersistentListener(ui.attackButton.onClick, previewController.PreviewAttack);
            UnityEventTools.AddPersistentListener(ui.guardButton.onClick, previewController.PreviewGuard);
            UnityEventTools.AddPersistentListener(ui.techniqueButton.onClick, previewController.PreviewTechnique);
            UnityEventTools.AddPersistentListener(ui.markButton.onClick, previewController.PreviewMark);
            UnityEventTools.AddPersistentListener(ui.backButton.onClick, navigation.BackToMenu);
        }

        private static void ConfigureBuildSettings(Phase02SetupReport report)
        {
            List<EditorBuildSettingsScene> ordered = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(MainMenuScenePath, true),
                new EditorBuildSettingsScene(TutorialScenePath, true)
            };

            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                if (existing.path != MainMenuScenePath && existing.path != TutorialScenePath &&
                    ordered.All(scene => scene.path != existing.path))
                {
                    ordered.Add(existing);
                }
            }

            AddBuildSceneIfAssetExists(ordered, "Assets/Scenes/SampleScene.unity");
            AddBuildSceneIfAssetExists(ordered, "Assets/_Veyra/Scenes/SCN_BattlePrototype.unity");
            EditorBuildSettings.scenes = ordered.ToArray();
            report.Configure("Build order: MainMenu, Tutorial Draft, scene preesistenti");
        }

        private static void AddBuildSceneIfAssetExists(List<EditorBuildSettingsScene> scenes, string path)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null && scenes.All(scene => scene.path != path))
            {
                scenes.Add(new EditorBuildSettingsScene(path, true));
            }
        }

        private sealed class SettingsUi
        {
            internal RectTransform modal;
            internal Slider masterSlider;
            internal Slider musicSlider;
            internal Slider sfxSlider;
            internal Toggle vibrationToggle;
            internal TMP_Text masterValueText;
            internal TMP_Text musicValueText;
            internal TMP_Text sfxValueText;
            internal Button resetButton;
            internal Button closeButton;
        }

        private sealed class OverlayUi
        {
            internal RectTransform loadingOverlay;
            internal RectTransform errorModal;
            internal TMP_Text errorText;
            internal Button errorCloseButton;
        }

        private sealed class BattleWorld
        {
            internal GameObject root;
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
            internal GameObject markPreview;
        }

        private sealed class BattleUi
        {
            internal GameObject uiRoot;
            internal RectTransform actionBar;
            internal TMP_Text combatMessage;
            internal Button attackButton;
            internal Button guardButton;
            internal Button techniqueButton;
            internal Button markButton;
            internal Button backButton;
        }
    }
}
#endif
