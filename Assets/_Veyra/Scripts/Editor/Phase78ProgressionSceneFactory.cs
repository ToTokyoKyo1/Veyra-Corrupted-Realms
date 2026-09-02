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
using Veyra.UI.MainMenu;
using Veyra.UI.Settings;

namespace Veyra.Editor
{
    internal static class Phase78ProgressionSceneFactory
    {
        internal const string MainMenuPath = "Assets/_Veyra/Scenes/SCN_MainMenu.unity";
        internal const string TutorialPath = "Assets/_Veyra/Scenes/SCN_W01_L01_Tutorial.unity";
        internal const string Level02Path = "Assets/_Veyra/Scenes/SCN_W01_L02_ThornGuardian.unity";
        internal const string Level03Path = "Assets/_Veyra/Scenes/SCN_W01_L03_AshWatcher.unity";
        internal const string Level04Path = "Assets/_Veyra/Scenes/SCN_W01_L04_ThreefoldAssault.unity";

        private const string MenuRootName = "ProgressionMenuRoot";

        internal static void CreateOrUpdateMenuAndLevel04()
        {
            CreateOrUpdateMainMenu();
            Phase78Level04SceneFactory.CreateOrUpdateLevel04();
        }

        internal static void ConfigureBuildSettingsAndOpenLevel04()
        {
            string[] requiredOrder =
            {
                MainMenuPath,
                TutorialPath,
                Level02Path,
                Level03Path,
                Level04Path
            };
            List<EditorBuildSettingsScene> ordered = requiredOrder
                .Select(path => new EditorBuildSettingsScene(path, true))
                .ToList();
            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                if (!requiredOrder.Contains(existing.path) &&
                    ordered.All(scene => scene.path != existing.path))
                {
                    ordered.Add(new EditorBuildSettingsScene(existing.path, false));
                }
            }

            EditorBuildSettings.scenes = ordered.ToArray();
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(Level04Path, OpenSceneMode.Single);
        }

        private static void CreateOrUpdateMainMenu()
        {
            Scene scene = EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);
            MainMenuController controller = RequireSingleInScene<MainMenuController>(scene);
            RectTransform safeArea = RequireNamedInScene<RectTransform>(scene, "SafeArea");
            Button playButton = RequireNamedInScene<Button>(scene, "BTN_Start");
            Button optionsButton = FindNamedInScene<Button>(scene, "BTN_Settings") ??
                                   FindNamedInScene<Button>(scene, "BTN_Options");
            if (optionsButton == null)
            {
                throw new InvalidOperationException("Il pulsante Opzioni esistente non è stato trovato.");
            }

            playButton.transform.SetParent(safeArea, false);
            optionsButton.transform.SetParent(safeArea, false);
            RemoveDirectChild(safeArea, MenuRootName);
            RemoveDirectChild(safeArea, Phase046EncounterSceneFactory.CampaignControlsName);

            TMP_FontAsset font = Phase02UiFactory.LoadRequiredFont();
            RectTransform root = Phase02UiFactory.CreateRect(MenuRootName, safeArea);
            Phase02UiFactory.SetRect(root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            RectTransform mainPanel = Phase02UiFactory.CreatePanel(
                "MainNavigationPanel",
                root,
                new Vector2(0.035f, 0.035f),
                new Vector2(0.965f, 0.965f),
                Vector2.zero,
                Vector2.zero,
                new Color(0.035f, 0.075f, 0.068f, 1f),
                true);
            Phase02UiFactory.CreateText(
                "TXT_ProgressionTitle",
                mainPanel,
                "VEYRA\nCORRUPTED REALMS",
                54f,
                Phase02UiFactory.MainText,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.08f, 0.79f),
                new Vector2(0.92f, 0.96f),
                Vector2.zero,
                Vector2.zero,
                FontStyles.Bold);
            TMP_Text campaignStatus = Phase02UiFactory.CreateText(
                "TXT_CampaignStatus",
                mainPanel,
                "0/10 COMPLETATI · PROSSIMO: TUTORIAL",
                30f,
                Phase02UiFactory.Light,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.08f, 0.69f),
                new Vector2(0.92f, 0.78f),
                Vector2.zero,
                Vector2.zero,
                FontStyles.Bold);

            playButton.transform.SetParent(mainPanel, false);
            optionsButton.transform.SetParent(mainPanel, false);
            playButton.name = "BTN_Start";
            optionsButton.name = "BTN_Options";
            SetButtonRect(playButton, new Vector2(0.12f, 0.52f), new Vector2(0.88f, 0.63f));
            SetButtonRect(optionsButton, new Vector2(0.12f, 0.16f), new Vector2(0.88f, 0.25f));
            TMP_Text playLabel = SetButtonLabel(playButton, "GIOCA · TUTORIAL");
            SetButtonLabel(optionsButton, "OPZIONI");

            Button levelsButton = Phase02UiFactory.CreateButton(
                "BTN_Levels",
                mainPanel,
                "LIVELLI",
                font,
                new Vector2(0.12f, 0.40f),
                new Vector2(0.88f, 0.49f),
                Vector2.zero,
                Vector2.zero);
            Button heroesButton = Phase02UiFactory.CreateButton(
                "BTN_Heroes",
                mainPanel,
                "EROI",
                font,
                new Vector2(0.12f, 0.28f),
                new Vector2(0.88f, 0.37f),
                Vector2.zero,
                Vector2.zero);
            RectTransform badge = Phase02UiFactory.CreatePanel(
                "HeroUpgradeBadge",
                heroesButton.transform,
                new Vector2(0.57f, 0.57f),
                new Vector2(0.98f, 0.96f),
                Vector2.zero,
                Vector2.zero,
                Phase02UiFactory.Gold);
            Phase02UiFactory.CreateText(
                "TXT_HeroUpgradeBadge",
                badge,
                "POTENZIAMENTO DISPONIBILE",
                18f,
                Phase02UiFactory.Background,
                TextAlignmentOptions.Center,
                font,
                Vector2.zero,
                Vector2.one,
                new Vector2(4f, 2f),
                new Vector2(-4f, -2f),
                FontStyles.Bold);

            MenuLevelsUi levels = CreateLevelsPanel(root, font);
            RectTransform settingsModal = RequireNamedInScene<RectTransform>(scene, "SettingsModal");
            Button resetButton = CreateSettingsCampaignReset(settingsModal, font);
            MenuHeroUi hero = CreateHeroPanel(root, font);
            MenuUpgradeUi upgrades = CreateUpgradePanels(root, font);
            ResetUi reset = CreateResetModal(root, font);

            SerializedObject serialized = new SerializedObject(controller);
            serialized.Update();
            SetObject(serialized, "mainNavigationPanel", mainPanel.gameObject);
            SetObject(serialized, "startButton", playButton);
            SetObject(serialized, "startButtonLabel", playLabel);
            SetObject(serialized, "levelsButton", levelsButton);
            SetObject(serialized, "heroesButton", heroesButton);
            SetObject(serialized, "settingsButton", optionsButton);
            SetObject(serialized, "heroUpgradeBadge", badge.gameObject);
            SetObject(serialized, "campaignStatusText", campaignStatus);
            SetObject(serialized, "levelsPanel", levels.Root);
            SetObject(serialized, "completedLevelsText", levels.CompletedText);
            SetObjectArray(serialized, "levelButtons", levels.Buttons.Cast<UnityEngine.Object>().ToArray());
            SetObjectArray(serialized, "levelButtonLabels", levels.Labels.Cast<UnityEngine.Object>().ToArray());
            SetObject(serialized, "levelsBackButton", levels.BackButton);
            SetObject(serialized, "heroesPanel", hero.Root);
            SetObject(serialized, "heroNameText", hero.NameText);
            SetObject(serialized, "heroLevelText", hero.LevelText);
            SetObject(serialized, "heroExperienceText", hero.ExperienceText);
            SetObject(serialized, "heroExperienceFill", hero.ExperienceFill);
            SetObject(serialized, "heroStatsText", hero.StatsText);
            SetObject(serialized, "heroUpgradesText", hero.UpgradesText);
            SetObject(serialized, "heroPointsText", hero.PointsText);
            SetObject(serialized, "heroUpgradeButton", hero.UpgradeButton);
            SetObject(serialized, "heroesBackButton", hero.BackButton);
            SetObject(serialized, "upgradeSelectionPanel", upgrades.SelectionRoot);
            SetObject(serialized, "upgradeAttackButton", upgrades.AttackButton);
            SetObject(serialized, "upgradeGuardButton", upgrades.GuardButton);
            SetObject(serialized, "upgradeTechniqueButton", upgrades.TechniqueButton);
            SetObject(serialized, "upgradeAnalyzeButton", upgrades.AnalyzeButton);
            SetObject(serialized, "upgradeSelectionBackButton", upgrades.SelectionBackButton);
            SetObject(serialized, "upgradeConfirmationPanel", upgrades.ConfirmationRoot);
            SetObject(serialized, "upgradeConfirmationTitle", upgrades.ConfirmationTitle);
            SetObject(serialized, "upgradeConfirmationDescription", upgrades.ConfirmationDescription);
            SetObject(serialized, "upgradeBeforeAfterText", upgrades.BeforeAfterText);
            SetObject(serialized, "confirmUpgradeButton", upgrades.ConfirmButton);
            SetObject(serialized, "cancelUpgradeButton", upgrades.CancelButton);
            SetObject(serialized, "replayTutorialButton", null);
            SetObject(serialized, "resetProgressButton", resetButton);
            SetObject(serialized, "resetProgressConfirmationModal", reset.Root);
            SetObject(serialized, "resetProgressConfirmButton", reset.ConfirmButton);
            SetObject(serialized, "resetProgressCancelButton", reset.CancelButton);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            ConfigureMenuListeners(
                controller,
                playButton,
                levelsButton,
                heroesButton,
                optionsButton,
                resetButton,
                levels,
                hero,
                upgrades,
                reset);

            mainPanel.gameObject.SetActive(true);
            levels.Root.SetActive(false);
            hero.Root.SetActive(false);
            upgrades.SelectionRoot.SetActive(false);
            upgrades.ConfirmationRoot.SetActive(false);
            reset.Root.SetActive(false);
            badge.gameObject.SetActive(false);

            DeactivateLegacySafeAreaChildren(
                safeArea,
                root.gameObject,
                controller);

            SerializedProperty settingsPanel = serialized.FindProperty("settingsPanel");
            if (settingsPanel != null &&
                settingsPanel.objectReferenceValue is SettingsPanelController settingsController)
            {
                MoveSettingsOverlaysToFront(settingsController);
            }
            MoveControllerOverlayToFront(serialized, "loadingOverlay");
            MoveControllerOverlayToFront(serialized, "errorModal");
            Phase02UiFactory.NormalizeTextOverflow(safeArea);

            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, MainMenuPath))
            {
                throw new InvalidOperationException("Impossibile salvare SCN_MainMenu.");
            }
        }

        private static MenuLevelsUi CreateLevelsPanel(Transform parent, TMP_FontAsset font)
        {
            MenuLevelsUi ui = new MenuLevelsUi();
            RectTransform root = Phase02UiFactory.CreatePanel(
                "LevelsPanel",
                parent,
                new Vector2(0.035f, 0.035f),
                new Vector2(0.965f, 0.965f),
                Vector2.zero,
                Vector2.zero,
                new Color(0.035f, 0.075f, 0.068f, 0.995f),
                true);
            ui.Root = root.gameObject;
            Phase02UiFactory.CreateText(
                "TXT_LevelsTitle",
                root,
                "LIVELLI",
                48f,
                Phase02UiFactory.MainText,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.08f, 0.90f),
                new Vector2(0.92f, 0.98f),
                Vector2.zero,
                Vector2.zero,
                FontStyles.Bold);
            ui.CompletedText = Phase02UiFactory.CreateText(
                "TXT_CompletedLevels",
                root,
                "0/10 COMPLETATI",
                30f,
                Phase02UiFactory.Light,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.10f, 0.84f),
                new Vector2(0.90f, 0.90f),
                Vector2.zero,
                Vector2.zero,
                FontStyles.Bold);

            ui.Buttons = new Button[10];
            ui.Labels = new TMP_Text[10];

            RectTransform viewport = Phase02UiFactory.CreatePanel(
                "LevelScrollViewport",
                root,
                new Vector2(0.055f, 0.145f),
                new Vector2(0.945f, 0.83f),
                Vector2.zero,
                Vector2.zero,
                new Color(0.02f, 0.055f, 0.05f, 1f),
                true);
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = Phase02UiFactory.CreateRect("LevelScrollContent", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(12f, 0f);
            content.offsetMax = new Vector2(-12f, 0f);
            content.sizeDelta = new Vector2(0f, 0f);

            VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.inertia = true;
            scroll.scrollSensitivity = 58f;

            for (int index = 0; index < 10; index++)
            {
                Button button = Phase02UiFactory.CreateButton(
                    "BTN_Level" + (index + 1).ToString("00"),
                    content,
                    (index + 1) + " · LIVELLO\nBLOCCATO\nSeleziona per i dettagli",
                    font,
                    Vector2.zero,
                    Vector2.one,
                    Vector2.zero,
                    Vector2.zero,
                    index < 4);
                button.interactable = index < 4;
                LayoutElement cardLayout = button.gameObject.AddComponent<LayoutElement>();
                cardLayout.minHeight = 360f;
                cardLayout.preferredHeight = 400f;
                cardLayout.flexibleWidth = 1f;
                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                label.fontSize = 32f;
                label.fontSizeMax = 32f;
                label.fontSizeMin = 30f;
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.margin = new Vector4(28f, 18f, 28f, 18f);
                label.textWrappingMode = TextWrappingModes.Normal;
                label.overflowMode = TextOverflowModes.Overflow;
                ui.Buttons[index] = button;
                ui.Labels[index] = label;
            }

            ui.BackButton = Phase02UiFactory.CreateButton(
                "BTN_LevelsBack",
                root,
                "INDIETRO",
                font,
                new Vector2(0.22f, 0.035f),
                new Vector2(0.78f, 0.12f),
                Vector2.zero,
                Vector2.zero);
            return ui;
        }

        private static MenuHeroUi CreateHeroPanel(Transform parent, TMP_FontAsset font)
        {
            MenuHeroUi ui = new MenuHeroUi();
            RectTransform root = Phase02UiFactory.CreatePanel(
                "HeroesPanel",
                parent,
                new Vector2(0.035f, 0.035f),
                new Vector2(0.965f, 0.965f),
                Vector2.zero,
                Vector2.zero,
                new Color(0.035f, 0.075f, 0.068f, 0.995f),
                true);
            ui.Root = root.gameObject;
            ui.NameText = Phase02UiFactory.CreateText(
                "TXT_Hero01Name",
                root,
                "HERO01",
                52f,
                Phase02UiFactory.MainText,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.08f, 0.88f),
                new Vector2(0.92f, 0.97f),
                Vector2.zero,
                Vector2.zero,
                FontStyles.Bold);
            ui.LevelText = Phase02UiFactory.CreateText(
                "TXT_Hero01Level",
                root,
                "LIVELLO 1",
                30f,
                Phase02UiFactory.Gold,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.08f, 0.82f),
                new Vector2(0.92f, 0.88f),
                Vector2.zero,
                Vector2.zero,
                FontStyles.Bold);
            ui.ExperienceText = Phase02UiFactory.CreateText(
                "TXT_Hero01Experience",
                root,
                "XP 0 / 100",
                24f,
                Phase02UiFactory.Light,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.10f, 0.75f),
                new Vector2(0.90f, 0.81f),
                Vector2.zero,
                Vector2.zero);
            ui.ExperienceFill = CreateFillBar(
                "HeroExperienceBar",
                root,
                new Vector2(0.12f, 0.72f),
                new Vector2(0.88f, 0.75f),
                Phase02UiFactory.Gold);

            ui.StatsText = Phase02UiFactory.CreateText(
                "TXT_Hero01Stats",
                root,
                "HP 100\nATTACCO 20\nTECNICA 32\nCOOLDOWN 2",
                29f,
                Phase02UiFactory.MainText,
                TextAlignmentOptions.TopLeft,
                font,
                new Vector2(0.08f, 0.46f),
                new Vector2(0.47f, 0.68f),
                new Vector2(18f, 12f),
                new Vector2(-8f, -8f),
                FontStyles.Bold);
            ui.UpgradesText = Phase02UiFactory.CreateText(
                "TXT_Hero01Upgrades",
                root,
                "POTENZIAMENTI\nNESSUNO",
                25f,
                Phase02UiFactory.Light,
                TextAlignmentOptions.TopLeft,
                font,
                new Vector2(0.50f, 0.46f),
                new Vector2(0.92f, 0.68f),
                new Vector2(8f, 12f),
                new Vector2(-18f, -8f));
            ui.PointsText = Phase02UiFactory.CreateText(
                "TXT_Hero01Points",
                root,
                "PUNTI IMPORTANTI: 0",
                25f,
                Phase02UiFactory.Gold,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.08f, 0.34f),
                new Vector2(0.92f, 0.45f),
                Vector2.zero,
                Vector2.zero,
                FontStyles.Bold);
            ui.UpgradeButton = Phase02UiFactory.CreateButton(
                "BTN_HeroUpgrade",
                root,
                "POTENZIA",
                font,
                new Vector2(0.14f, 0.24f),
                new Vector2(0.86f, 0.33f),
                Vector2.zero,
                Vector2.zero,
                true);
            Phase02UiFactory.CreateText(
                "TXT_OtherHeroesUnavailable",
                root,
                "ALTRI EROI NON ANCORA DISPONIBILI",
                22f,
                Phase02UiFactory.SecondaryText,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.08f, 0.15f),
                new Vector2(0.92f, 0.22f),
                Vector2.zero,
                Vector2.zero);
            ui.BackButton = Phase02UiFactory.CreateButton(
                "BTN_HeroesBack",
                root,
                "INDIETRO",
                font,
                new Vector2(0.27f, 0.045f),
                new Vector2(0.73f, 0.105f),
                Vector2.zero,
                Vector2.zero);
            return ui;
        }

        private static MenuUpgradeUi CreateUpgradePanels(Transform parent, TMP_FontAsset font)
        {
            MenuUpgradeUi ui = new MenuUpgradeUi();
            RectTransform selection = Phase02UiFactory.CreatePanel(
                "UpgradeSelectionPanel",
                parent,
                new Vector2(0.035f, 0.035f),
                new Vector2(0.965f, 0.965f),
                Vector2.zero,
                Vector2.zero,
                new Color(0.035f, 0.075f, 0.068f, 0.998f),
                true);
            ui.SelectionRoot = selection.gameObject;
            Phase02UiFactory.CreateText(
                "TXT_UpgradeTitle",
                selection,
                "POTENZIAMENTO IMPORTANTE",
                42f,
                Phase02UiFactory.Gold,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.06f, 0.88f),
                new Vector2(0.94f, 0.97f),
                Vector2.zero,
                Vector2.zero,
                FontStyles.Bold);
            Phase02UiFactory.CreateText(
                "TXT_UpgradeSubtitle",
                selection,
                "Scegli una sola abilità. La scelta è permanente fino al reset.",
                22f,
                Phase02UiFactory.SecondaryText,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.08f, 0.81f),
                new Vector2(0.92f, 0.87f),
                Vector2.zero,
                Vector2.zero);
            ui.AttackButton = CreateUpgradeButton(selection, font, "BTN_UpgradeAttack", "ATTACCO · COLPO RINFORZATO\n+8 DANNI", 0.66f, 0.79f);
            ui.GuardButton = CreateUpgradeButton(selection, font, "BTN_UpgradeGuard", "GUARDIA · BASTIONE\nPARA TUTTA LA FASE", 0.51f, 0.64f);
            ui.TechniqueButton = CreateUpgradeButton(selection, font, "BTN_UpgradeTechnique", "TECNICA · EVOLUTA\n+14 DANNI · AREA 55%", 0.36f, 0.49f);
            ui.AnalyzeButton = CreateUpgradeButton(selection, font, "BTN_UpgradeAnalyze", "ANALIZZA · VISTA DELLA CORRUZIONE\nBLUFF + ESPOSTO", 0.21f, 0.34f);
            ui.SelectionBackButton = Phase02UiFactory.CreateButton(
                "BTN_UpgradeSelectionBack",
                selection,
                "INDIETRO",
                font,
                new Vector2(0.27f, 0.06f),
                new Vector2(0.73f, 0.13f),
                Vector2.zero,
                Vector2.zero);

            RectTransform confirmation = Phase02UiFactory.CreatePanel(
                "UpgradeConfirmationPanel",
                parent,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                new Color(0.015f, 0.03f, 0.03f, 0.985f),
                true);
            ui.ConfirmationRoot = confirmation.gameObject;
            RectTransform card = Phase02UiFactory.CreatePanel(
                "UpgradeConfirmationCard",
                confirmation,
                new Vector2(0.07f, 0.23f),
                new Vector2(0.93f, 0.77f),
                Vector2.zero,
                Vector2.zero,
                Phase02UiFactory.Panel);
            ui.ConfirmationTitle = Phase02UiFactory.CreateText(
                "TXT_UpgradeConfirmationTitle",
                card,
                "CONFERMA",
                38f,
                Phase02UiFactory.Gold,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.07f, 0.78f),
                new Vector2(0.93f, 0.94f),
                Vector2.zero,
                Vector2.zero,
                FontStyles.Bold);
            ui.ConfirmationDescription = Phase02UiFactory.CreateText(
                "TXT_UpgradeConfirmationDescription",
                card,
                "Descrizione completa",
                25f,
                Phase02UiFactory.MainText,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.08f, 0.52f),
                new Vector2(0.92f, 0.77f),
                Vector2.zero,
                Vector2.zero);
            ui.BeforeAfterText = Phase02UiFactory.CreateText(
                "TXT_UpgradeBeforeAfter",
                card,
                "PRIMA\nDOPO",
                25f,
                Phase02UiFactory.Light,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.08f, 0.27f),
                new Vector2(0.92f, 0.50f),
                Vector2.zero,
                Vector2.zero,
                FontStyles.Bold);
            ui.ConfirmButton = Phase02UiFactory.CreateButton(
                "BTN_ConfirmUpgrade",
                card,
                "CONFERMA",
                font,
                new Vector2(0.08f, 0.07f),
                new Vector2(0.52f, 0.23f),
                Vector2.zero,
                Vector2.zero,
                true);
            ui.CancelButton = Phase02UiFactory.CreateButton(
                "BTN_CancelUpgrade",
                card,
                "INDIETRO",
                font,
                new Vector2(0.56f, 0.07f),
                new Vector2(0.92f, 0.23f),
                Vector2.zero,
                Vector2.zero);
            SetCompactButtonLabel(ui.ConfirmButton, 27f);
            SetCompactButtonLabel(ui.CancelButton, 30f);
            return ui;
        }

        private static ResetUi CreateResetModal(Transform parent, TMP_FontAsset font)
        {
            ResetUi ui = new ResetUi();
            RectTransform modal = Phase02UiFactory.CreatePanel(
                "ResetProgressConfirmation",
                parent,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                new Color(0.015f, 0.03f, 0.03f, 0.985f),
                true);
            ui.Root = modal.gameObject;
            RectTransform card = Phase02UiFactory.CreatePanel(
                "ResetProgressCard",
                modal,
                new Vector2(0.08f, 0.33f),
                new Vector2(0.92f, 0.67f),
                Vector2.zero,
                Vector2.zero,
                Phase02UiFactory.Panel);
            Phase02UiFactory.CreateText(
                "TXT_ResetProgressQuestion",
                card,
                "AZZERARE TUTTA LA CAMPAGNA?\nLivelli, XP, potenziamento ed esiti verranno cancellati.\nLe opzioni resteranno invariate.",
                29f,
                Phase02UiFactory.MainText,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.07f, 0.36f),
                new Vector2(0.93f, 0.92f),
                Vector2.zero,
                Vector2.zero,
                FontStyles.Bold);
            ui.ConfirmButton = Phase02UiFactory.CreateButton(
                "BTN_ConfirmResetCampaign",
                card,
                "CONFERMA AZZERAMENTO",
                font,
                new Vector2(0.07f, 0.08f),
                new Vector2(0.52f, 0.30f),
                Vector2.zero,
                Vector2.zero,
                true);
            ui.CancelButton = Phase02UiFactory.CreateButton(
                "BTN_CancelResetCampaign",
                card,
                "ANNULLA",
                font,
                new Vector2(0.56f, 0.08f),
                new Vector2(0.93f, 0.30f),
                Vector2.zero,
                Vector2.zero);
            return ui;
        }

        private static void ConfigureMenuListeners(
            MainMenuController controller,
            Button play,
            Button levelsButton,
            Button heroesButton,
            Button options,
            Button resetButton,
            MenuLevelsUi levels,
            MenuHeroUi hero,
            MenuUpgradeUi upgrades,
            ResetUi reset)
        {
            SetListener(play, controller.StartGame);
            SetListener(levelsButton, controller.OpenLevels);
            SetListener(heroesButton, controller.OpenHeroes);
            SetListener(options, controller.OpenSettings);
            SetListener(resetButton, controller.OpenResetProgressConfirmation);
            SetListener(levels.BackButton, controller.ShowMainPanel);
            SetListener(hero.BackButton, controller.ShowMainPanel);
            SetListener(hero.UpgradeButton, controller.OpenUpgradeSelection);
            SetListener(upgrades.SelectionBackButton, controller.CloseUpgradeSelection);
            SetListener(upgrades.AttackButton, controller.SelectAttackUpgrade);
            SetListener(upgrades.GuardButton, controller.SelectGuardUpgrade);
            SetListener(upgrades.TechniqueButton, controller.SelectTechniqueUpgrade);
            SetListener(upgrades.AnalyzeButton, controller.SelectAnalyzeUpgrade);
            SetListener(upgrades.ConfirmButton, controller.ConfirmUpgrade);
            SetListener(upgrades.CancelButton, controller.CancelUpgradeConfirmation);
            SetListener(reset.ConfirmButton, controller.ConfirmResetProgress);
            SetListener(reset.CancelButton, controller.CloseResetProgressConfirmation);
            SetListener(levels.Buttons[0], controller.OpenLevel01);
            SetListener(levels.Buttons[1], controller.OpenLevel02);
            SetListener(levels.Buttons[2], controller.OpenLevel03);
            SetListener(levels.Buttons[3], controller.OpenLevel04);
            for (int index = 4; index < levels.Buttons.Length; index++)
            {
                SetListener(levels.Buttons[index], controller.ShowComingSoonLevel);
            }
        }

        private static Button CreateUpgradeButton(
            Transform parent,
            TMP_FontAsset font,
            string name,
            string label,
            float yMin,
            float yMax)
        {
            Button button = Phase02UiFactory.CreateButton(
                name,
                parent,
                label,
                font,
                new Vector2(0.08f, yMin),
                new Vector2(0.92f, yMax),
                Vector2.zero,
                Vector2.zero);
            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            text.fontSize = 29f;
            text.textWrappingMode = TextWrappingModes.Normal;
            return button;
        }

        private static Image CreateFillBar(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color fillColor)
        {
            RectTransform background = Phase02UiFactory.CreatePanel(
                name,
                parent,
                anchorMin,
                anchorMax,
                Vector2.zero,
                Vector2.zero,
                Phase02UiFactory.HighlightedPanel);
            RectTransform fill = Phase02UiFactory.CreatePanel(
                "Fill",
                background,
                Vector2.zero,
                Vector2.one,
                new Vector2(3f, 3f),
                new Vector2(-3f, -3f),
                fillColor);
            Image image = fill.GetComponent<Image>();
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = 0;
            image.fillAmount = 0f;
            return image;
        }

        private static Button CreateSettingsCampaignReset(
            RectTransform settingsModal,
            TMP_FontAsset font)
        {
            RemoveDirectChild(settingsModal, "BTN_ResetCampaign");

            Button defaultsButton = settingsModal
                .GetComponentsInChildren<Button>(true)
                .FirstOrDefault(button => button.name == "BTN_Reset");
            Button closeButton = settingsModal
                .GetComponentsInChildren<Button>(true)
                .FirstOrDefault(button => button.name == "BTN_CloseSettings");
            RectTransform vibrationLabel = settingsModal
                .GetComponentsInChildren<RectTransform>(true)
                .FirstOrDefault(rect => rect.name == "TXT_VibrationLabel");
            RectTransform vibrationToggle = settingsModal
                .GetComponentsInChildren<RectTransform>(true)
                .FirstOrDefault(rect => rect.name == "TGL_Vibration");

            if (vibrationLabel != null)
            {
                Phase02UiFactory.SetRect(
                    vibrationLabel,
                    new Vector2(0.08f, 0.285f),
                    new Vector2(0.60f, 0.36f),
                    Vector2.zero,
                    Vector2.zero);
            }
            if (vibrationToggle != null)
            {
                Phase02UiFactory.SetRect(
                    vibrationToggle,
                    new Vector2(0.67f, 0.28f),
                    new Vector2(0.90f, 0.36f),
                    Vector2.zero,
                    Vector2.zero);
            }
            if (defaultsButton != null)
            {
                SetButtonRect(defaultsButton, new Vector2(0.08f, 0.185f), new Vector2(0.92f, 0.265f));
                SetCompactButtonLabel(defaultsButton, 31f);
            }
            if (closeButton != null)
            {
                SetButtonRect(closeButton, new Vector2(0.08f, 0.015f), new Vector2(0.92f, 0.085f));
                SetCompactButtonLabel(closeButton, 32f);
            }

            Button reset = Phase02UiFactory.CreateButton(
                "BTN_ResetCampaign",
                settingsModal,
                "AZZERA CAMPAGNA",
                font,
                new Vector2(0.08f, 0.095f),
                new Vector2(0.92f, 0.175f),
                Vector2.zero,
                Vector2.zero);
            reset.targetGraphic.color = new Color(0.34f, 0.10f, 0.12f, 1f);
            SetCompactButtonLabel(reset, 31f);
            return reset;
        }

        private static void SetCompactButtonLabel(Button button, float size)
        {
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label == null)
            {
                return;
            }

            label.fontSize = size;
            label.fontSizeMax = size;
            label.fontSizeMin = Mathf.Max(22f, size * 0.72f);
            label.textWrappingMode = TextWrappingModes.Normal;
        }

        private static void DeactivateLegacySafeAreaChildren(
            RectTransform safeArea,
            GameObject progressionRoot,
            MainMenuController controller)
        {
            HashSet<GameObject> keep = new HashSet<GameObject> { progressionRoot };
            SerializedObject menuSerialized = new SerializedObject(controller);
            menuSerialized.Update();
            AddReferencedDirectChild(keep, safeArea, menuSerialized.FindProperty("loadingOverlay"));
            AddReferencedDirectChild(keep, safeArea, menuSerialized.FindProperty("errorModal"));

            SerializedProperty settingsProperty = menuSerialized.FindProperty("settingsPanel");
            if (settingsProperty != null &&
                settingsProperty.objectReferenceValue is SettingsPanelController settings)
            {
                SerializedObject settingsSerialized = new SerializedObject(settings);
                settingsSerialized.Update();
                AddReferencedDirectChild(keep, safeArea, settingsSerialized.FindProperty("dimmer"));
                AddReferencedDirectChild(keep, safeArea, settingsSerialized.FindProperty("modalRoot"));
            }

            for (int index = 0; index < safeArea.childCount; index++)
            {
                GameObject child = safeArea.GetChild(index).gameObject;
                if (!keep.Contains(child))
                {
                    child.SetActive(false);
                    EditorUtility.SetDirty(child);
                }
            }
        }

        private static void AddReferencedDirectChild(
            ISet<GameObject> keep,
            Transform safeArea,
            SerializedProperty property)
        {
            if (property == null || !(property.objectReferenceValue is GameObject referenced))
            {
                return;
            }

            Transform current = referenced.transform;
            while (current.parent != null && current.parent != safeArea)
            {
                current = current.parent;
            }

            if (current.parent == safeArea)
            {
                keep.Add(current.gameObject);
            }
        }

        private static void SetListener(Button button, UnityEngine.Events.UnityAction action)
        {
            while (button.onClick.GetPersistentEventCount() > 0)
            {
                UnityEventTools.RemovePersistentListener(button.onClick, 0);
            }

            UnityEventTools.AddPersistentListener(button.onClick, action);
            EditorUtility.SetDirty(button);
        }

        private static TMP_Text SetButtonLabel(Button button, string value)
        {
            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text == null)
            {
                throw new InvalidOperationException(button.name + " non contiene TMP_Text.");
            }

            text.text = value;
            text.overflowMode = TextOverflowModes.Truncate;
            EditorUtility.SetDirty(text);
            return text;
        }

        private static void SetButtonRect(Button button, Vector2 anchorMin, Vector2 anchorMax)
        {
            RectTransform rect = button.GetComponent<RectTransform>();
            Phase02UiFactory.SetRect(rect, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            rect.localScale = Vector3.one;
        }

        private static void MoveControllerOverlayToFront(SerializedObject serialized, string propertyName)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null && property.objectReferenceValue is GameObject gameObject)
            {
                gameObject.transform.SetAsLastSibling();
            }
        }

        private static void MoveSettingsOverlaysToFront(SettingsPanelController settingsController)
        {
            SerializedObject settingsSerialized = new SerializedObject(settingsController);
            settingsSerialized.Update();
            MoveReferencedObjectToFront(settingsSerialized.FindProperty("dimmer"));
            MoveReferencedObjectToFront(settingsSerialized.FindProperty("modalRoot"));
        }

        private static void MoveReferencedObjectToFront(SerializedProperty property)
        {
            if (property != null && property.objectReferenceValue is GameObject gameObject)
            {
                gameObject.transform.SetAsLastSibling();
                EditorUtility.SetDirty(gameObject);
            }
        }

        private static void RemoveDirectChild(Transform parent, string childName)
        {
            for (int index = parent.childCount - 1; index >= 0; index--)
            {
                Transform child = parent.GetChild(index);
                if (child.name == childName)
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        private static T RequireSingleInScene<T>(Scene scene) where T : Component
        {
            T[] matches = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
            if (matches.Length != 1)
            {
                throw new InvalidOperationException(
                    scene.name + " richiede esattamente un " + typeof(T).Name + ".");
            }

            return matches[0];
        }

        private static T RequireNamedInScene<T>(Scene scene, string name) where T : Component
        {
            T component = FindNamedInScene<T>(scene, name);
            if (component == null)
            {
                throw new InvalidOperationException(scene.name + ": oggetto mancante " + name + ".");
            }

            return component;
        }

        private static T FindNamedInScene<T>(Scene scene, string name) where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .FirstOrDefault(component => component.gameObject.name == name);
        }

        internal static void SetObject(SerializedObject serialized, string name, UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property == null)
            {
                throw new InvalidOperationException(
                    serialized.targetObject.GetType().Name + "." + name + " non trovato.");
            }

            property.objectReferenceValue = value;
        }

        internal static void SetObjectArray(
            SerializedObject serialized,
            string name,
            UnityEngine.Object[] values)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property == null || !property.isArray)
            {
                throw new InvalidOperationException(name + " non è un array serializzato.");
            }

            property.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            }
        }

        private sealed class MenuLevelsUi
        {
            internal GameObject Root;
            internal TMP_Text CompletedText;
            internal Button[] Buttons;
            internal TMP_Text[] Labels;
            internal Button BackButton;
        }

        private sealed class MenuHeroUi
        {
            internal GameObject Root;
            internal TMP_Text NameText;
            internal TMP_Text LevelText;
            internal TMP_Text ExperienceText;
            internal Image ExperienceFill;
            internal TMP_Text StatsText;
            internal TMP_Text UpgradesText;
            internal TMP_Text PointsText;
            internal Button UpgradeButton;
            internal Button BackButton;
        }

        private sealed class MenuUpgradeUi
        {
            internal GameObject SelectionRoot;
            internal Button AttackButton;
            internal Button GuardButton;
            internal Button TechniqueButton;
            internal Button AnalyzeButton;
            internal Button SelectionBackButton;
            internal GameObject ConfirmationRoot;
            internal TMP_Text ConfirmationTitle;
            internal TMP_Text ConfirmationDescription;
            internal TMP_Text BeforeAfterText;
            internal Button ConfirmButton;
            internal Button CancelButton;
        }

        private sealed class ResetUi
        {
            internal GameObject Root;
            internal Button ConfirmButton;
            internal Button CancelButton;
        }
    }
}
#endif
