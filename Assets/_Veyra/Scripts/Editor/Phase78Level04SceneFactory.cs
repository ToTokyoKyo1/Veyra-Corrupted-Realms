#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Veyra.Combat.Encounter;
using Veyra.Combat.MultiEnemy;
using Veyra.Core;

namespace Veyra.Editor
{
    internal static class Phase78Level04SceneFactory
    {
        private const string SceneRootName = "SCN_W01_L04_ThreefoldAssault";
        private const string BattleRootName = "MultiEnemyBattleRoot";
        private const string UiRootName = "MultiEnemyUIRoot";

        private static readonly System.Collections.Generic.IReadOnlyList<MultiEnemyProfile> Roster =
            Level04EnemyRoster.Create();

        private static readonly EnemyDefinition[] Enemies =
        {
            new EnemyDefinition(
                Roster[0],
                "Non vi lascerò il tempo di respirare.",
                "La furia non è tutta mia. La corruzione la spinge al mio posto.",
                "EnemyActor_Brute",
                new Vector3(2.15f, -4.75f, 0f),
                0.86f,
                new Color(0.76f, 0.28f, 0.25f, 1f)),
            new EnemyDefinition(
                Roster[1],
                "La fretta appartiene a chi non vede il turno successivo.",
                "Osservo ogni possibilità, ma nessuna mi libera dalla corruzione.",
                "EnemyActor_Watcher",
                new Vector3(2.65f, -1.65f, 0f),
                0.62f,
                new Color(0.48f, 0.72f, 0.88f, 1f)),
            new EnemyDefinition(
                Roster[2],
                "Credi davvero che ciò che mostro sia ciò che farò?",
                "Cambio volto per obbedire. Sotto, ricordo ancora chi ero.",
                "EnemyActor_Mask",
                new Vector3(0.80f, -2.65f, 0f),
                0.58f,
                new Color(0.77f, 0.48f, 0.88f, 1f))
        };

        internal static void CreateOrUpdateLevel04()
        {
            bool existed = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                Phase78ProgressionSceneFactory.Level04Path) != null;
            Scene scene = existed
                ? EditorSceneManager.OpenScene(
                    Phase78ProgressionSceneFactory.Level04Path,
                    OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == SceneRootName)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }

            GameObject sceneRoot = new GameObject(SceneRootName);
            SceneManager.MoveGameObjectToScene(sceneRoot, scene);
            TMP_FontAsset font = Phase02UiFactory.LoadRequiredFont();
            L4World world = CreateWorld(sceneRoot.transform);
            L4Ui ui = CreateUi(sceneRoot.transform, font);
            MultiEnemyBattleController controller = world.Root.AddComponent<MultiEnemyBattleController>();
            MultiEnemyBattleNavigation navigation = ui.Root.AddComponent<MultiEnemyBattleNavigation>();
            AssignController(controller, navigation, world, ui);
            AssignActorTargets(controller, world);
            AssignNavigation(navigation, controller, ui);
            AddListeners(controller, navigation, ui);
            SetInitialState(world, ui);
            CreateCamera(sceneRoot.transform);
            Phase02UiFactory.CreateEventSystem(sceneRoot.transform);
            Phase02UiFactory.NormalizeTextOverflow(ui.Root.transform);

            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(navigation);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, Phase78ProgressionSceneFactory.Level04Path))
            {
                throw new InvalidOperationException("Impossibile salvare il Livello 4.");
            }
        }

        private static L4World CreateWorld(Transform parent)
        {
            L4World world = new L4World();
            world.Root = new GameObject(BattleRootName);
            world.Root.transform.SetParent(parent, false);

            Sprite backgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                Phase01PlaceholderFactory.BackgroundSpritePath);
            GameObject background = new GameObject("Background", typeof(SpriteRenderer));
            background.transform.SetParent(world.Root.transform, false);
            SpriteRenderer backgroundRenderer = background.GetComponent<SpriteRenderer>();
            backgroundRenderer.sprite = backgroundSprite;
            backgroundRenderer.sortingOrder = -100;
            backgroundRenderer.color = new Color(0.48f, 0.36f, 0.62f, 1f);

            world.HeroActor = CreateActor(
                Phase01PlaceholderFactory.HeroPrefabPath,
                world.Root.transform,
                "HeroActor",
                new Vector3(-2.35f, -4.75f, 0f),
                0.90f,
                Color.white,
                4,
                out world.HeroVisual);

            world.Enemies = new EnemyWorld[Enemies.Length];
            for (int index = 0; index < Enemies.Length; index++)
            {
                EnemyDefinition definition = Enemies[index];
                EnemyWorld enemy = new EnemyWorld();
                string enemyPrefabPath = index == 0
                    ? VeyraVisualAssetSetup.KnightPrefabPath
                    : Phase01PlaceholderFactory.EnemyPrefabPath;
                enemy.Actor = CreateActor(
                    enemyPrefabPath,
                    world.Root.transform,
                    definition.ActorName,
                    definition.Position,
                    definition.Scale,
                    index == 0 ? Color.white : definition.Tint,
                    definition.Altitude == EnemyAltitude.Ground ? 4 : 3,
                    out enemy.Visual);
                enemy.GuardEffect = CreateWorldEffect(
                    Phase02PrototypeAssetFactory.GuardRingPrefabPath,
                    enemy.Actor,
                    "GuardEffect",
                    Vector3.zero,
                    0.82f,
                    definition.Tint);
                enemy.ChargeEffect = CreateWorldEffect(
                    Phase02PrototypeAssetFactory.GuardRingPrefabPath,
                    enemy.Actor,
                    "ChargeEffect",
                    Vector3.zero,
                    1.05f,
                    Phase02UiFactory.Gold);
                enemy.HitEffect = CreateWorldEffect(
                    Phase02PrototypeAssetFactory.HeroBasicProjectilePrefabPath,
                    enemy.Actor,
                    "HitEffect",
                    new Vector3(0f, 1.1f, 0f),
                    0.72f,
                    Phase02UiFactory.Light);
                world.Enemies[index] = enemy;
            }

            world.HeroGuardEffect = CreateWorldEffect(
                Phase02PrototypeAssetFactory.GuardRingPrefabPath,
                world.HeroActor,
                "HeroGuardEffect",
                Vector3.zero,
                0.94f,
                Phase02UiFactory.Cyan);
            world.HeroAttackEffect = CreateWorldEffect(
                Phase02PrototypeAssetFactory.HeroBasicProjectilePrefabPath,
                world.Root.transform,
                "HeroAttackEffect",
                new Vector3(-0.8f, -3.4f, 0f),
                0.78f,
                Phase02UiFactory.Light);
            world.HeroTechniqueEffect = CreateWorldEffect(
                Phase02PrototypeAssetFactory.HeroTechniqueProjectilePrefabPath,
                world.Root.transform,
                "HeroTechniqueEffect",
                new Vector3(-0.45f, -2.95f, 0f),
                1.02f,
                Phase02UiFactory.Cyan);

            world.ThornAllyActor = CreateActor(
                Phase01PlaceholderFactory.EnemyPrefabPath,
                world.Root.transform,
                "SavedAlly_ThornGuardian",
                new Vector3(-1.15f, -3.92f, 0f),
                0.43f,
                new Color(0.52f, 0.82f, 0.42f, 0.82f),
                1,
                out _).gameObject;
            world.AshAllyActor = CreateActor(
                Phase01PlaceholderFactory.EnemyPrefabPath,
                world.Root.transform,
                "SavedAlly_AshWatcher",
                new Vector3(-2.85f, -2.82f, 0f),
                0.40f,
                new Color(0.78f, 0.50f, 0.42f, 0.82f),
                1,
                out _).gameObject;
            world.ThornSupportEffect = CreateWorldEffect(
                Phase02PrototypeAssetFactory.HeroBasicProjectilePrefabPath,
                world.Root.transform,
                "ThornSupportEffect",
                new Vector3(-0.35f, -2.4f, 0f),
                0.86f,
                new Color(0.55f, 1f, 0.48f, 1f));
            world.AshSupportEffect = CreateWorldEffect(
                Phase02PrototypeAssetFactory.EnemyProjectilePrefabPath,
                world.Root.transform,
                "AshSupportEffect",
                new Vector3(-0.1f, -1.9f, 0f),
                0.86f,
                new Color(1f, 0.68f, 0.38f, 1f));
            return world;
        }

        private static L4Ui CreateUi(Transform parent, TMP_FontAsset font)
        {
            L4Ui ui = new L4Ui();
            RectTransform canvas = Phase02UiFactory.CreateCanvas(parent);
            RectTransform safeArea = Phase02UiFactory.CreateSafeArea(canvas);
            ui.Root = safeArea.gameObject;

            Phase02UiFactory.CreateText(
                "TXT_Level04Title",
                safeArea,
                "LIVELLO 4 · ASSALTO DEI TRE",
                35f,
                Phase02UiFactory.MainText,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.03f, 0.955f),
                new Vector2(0.97f, 0.995f),
                Vector2.zero,
                Vector2.zero,
                FontStyles.Bold);

            HealthUi heroHealth = CreateHealthPanel(
                "HeroHealthPanel",
                safeArea,
                "HERO01",
                new Vector2(0.04f, 0.905f),
                new Vector2(0.96f, 0.953f),
                font,
                Phase02UiFactory.Cyan,
                "120 / 120");
            ui.HeroHealthFill = heroHealth.Fill;
            ui.HeroHealthText = heroHealth.Value;

            ui.Enemies = new EnemyUi[Enemies.Length];
            for (int index = 0; index < Enemies.Length; index++)
            {
                float width = 0.30f;
                float xMin = 0.025f + (index * 0.325f);
                ui.Enemies[index] = CreateEnemyCard(
                    safeArea,
                    font,
                    Enemies[index],
                    new Vector2(xMin, 0.805f),
                    new Vector2(xMin + width, 0.928f));
            }

            ui.CombatMessage = Phase02UiFactory.CreateText(
                "TXT_CombatMessage",
                Phase02UiFactory.CreatePanel(
                    "CombatMessagePanel",
                    safeArea,
                    new Vector2(0.285f, 0.745f),
                    new Vector2(0.715f, 0.795f),
                    Vector2.zero,
                    Vector2.zero,
                    Phase02UiFactory.Panel),
                "Scegli un bersaglio",
                32f,
                Phase02UiFactory.Light,
                TextAlignmentOptions.Center,
                font,
                Vector2.zero,
                Vector2.one,
                new Vector2(10f, 5f),
                new Vector2(-10f, -5f),
                FontStyles.Bold);
            ui.PhaseIndicator = Phase02UiFactory.CreateText(
                "TXT_Level04Phase",
                safeArea,
                "SCEGLI UN BERSAGLIO",
                40f,
                Phase02UiFactory.Gold,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.18f, 0.12f),
                new Vector2(0.82f, 0.18f),
                Vector2.zero,
                Vector2.zero,
                FontStyles.Bold);
            ui.SelectedTargetText = CreateStatusText(
                safeArea,
                font,
                "SelectedTargetPanel",
                "BERSAGLIO · NESSUNO",
                new Vector2(0.02f, 0.745f),
                new Vector2(0.27f, 0.795f));
            ui.HeroStatusText = CreateStatusText(
                safeArea,
                font,
                "HeroStatusPanel",
                "GUARDIA",
                new Vector2(0.73f, 0.745f),
                new Vector2(0.98f, 0.795f));

            DialogueUi dialogue = CreateDialogue(
                safeArea,
                font,
                "EnemyDialogueRoot",
                new Vector2(0.18f, 0.49f),
                new Vector2(0.95f, 0.575f),
                new Color(0.18f, 0.08f, 0.20f, 0.94f));
            ui.DialogueRoot = dialogue.Root;
            ui.DialogueText = dialogue.Text;
            DialogueUi allyDialogue = CreateDialogue(
                safeArea,
                font,
                "SavedAllyDialogueRoot",
                new Vector2(0.05f, 0.395f),
                new Vector2(0.82f, 0.48f),
                new Color(0.07f, 0.20f, 0.15f, 0.94f));
            ui.AllyDialogueRoot = allyDialogue.Root;
            ui.AllyDialogueText = allyDialogue.Text;

            ui.BackButton = Phase02UiFactory.CreateButton(
                "BTN_Level04Back",
                safeArea,
                "MENU",
                font,
                new Vector2(0.02f, 0.685f),
                new Vector2(0.15f, 0.735f),
                Vector2.zero,
                Vector2.zero);

            ui.AttackButton = CreateActionButton(safeArea, font, "BTN_Level04Attack", "ATTACCO", 0);
            ui.GuardButton = CreateActionButton(safeArea, font, "BTN_Level04Guard", "GUARDIA", 1);
            ui.TechniqueButton = CreateActionButton(safeArea, font, "BTN_Level04Technique", "TECNICA", 2);
            ui.TechniqueLabel = ui.TechniqueButton.GetComponentInChildren<TMP_Text>(true);
            ui.AnalyzeButton = CreateActionButton(safeArea, font, "BTN_Level04Analyze", "ANALIZZA", 3);

            CreateAnalyzePanel(safeArea, font, ui);
            CreateMoralPanel(safeArea, font, ui);
            CreateOutcomePanel(safeArea, font, ui);
            CreateTargetTutorialPanel(safeArea, font, ui);
            return ui;
        }

        private static EnemyUi CreateEnemyCard(
            Transform parent,
            TMP_FontAsset font,
            EnemyDefinition definition,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            EnemyUi ui = new EnemyUi();
            RectTransform card = Phase02UiFactory.CreatePanel(
                "BTN_Target_" + definition.EnemyId,
                parent,
                anchorMin,
                anchorMax,
                Vector2.zero,
                Vector2.zero,
                new Color(0.12f, 0.07f, 0.16f, 0.96f),
                true);
            Button targetButton = card.gameObject.AddComponent<Button>();
            targetButton.targetGraphic = card.GetComponent<Image>();
            ui.TargetButton = targetButton;
            ui.SelectionIndicator = CreateSelectionIndicator(card, font);
            ui.NameText = Phase02UiFactory.CreateText(
                "TXT_Name",
                card,
                definition.DisplayName.ToUpperInvariant(),
                21f,
                Phase02UiFactory.MainText,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.04f, 0.78f),
                new Vector2(0.96f, 0.98f),
                Vector2.zero,
                Vector2.zero,
                FontStyles.Bold);
            HealthUi health = CreateHealthPanel(
                "Health",
                card,
                string.Empty,
                new Vector2(0.06f, 0.59f),
                new Vector2(0.94f, 0.77f),
                font,
                definition.Tint,
                definition.MaxHp + " / " + definition.MaxHp);
            ui.HealthFill = health.Fill;
            ui.HealthText = health.Value;
            ui.IntentText = Phase02UiFactory.CreateText(
                "TXT_Intent",
                card,
                "INTENZIONE\nIN OSSERVAZIONE",
                18f,
                Phase02UiFactory.Light,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.04f, 0.28f),
                new Vector2(0.96f, 0.58f),
                Vector2.zero,
                Vector2.zero,
                FontStyles.Bold);
            ui.TargetStateText = Phase02UiFactory.CreateText(
                "TXT_TargetState",
                card,
                "SELEZIONA",
                17f,
                Phase02UiFactory.Cyan,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.04f, 0.04f),
                new Vector2(0.96f, 0.25f),
                Vector2.zero,
                Vector2.zero,
                FontStyles.Bold);
            ui.InstabilityClue = Phase02UiFactory.CreatePanel(
                "IntentInstability",
                card,
                new Vector2(0.04f, 0.24f),
                new Vector2(0.96f, 0.32f),
                Vector2.zero,
                Vector2.zero,
                Phase02UiFactory.Gold).gameObject;
            Phase02UiFactory.CreateText(
                "TXT_Instability",
                ui.InstabilityClue.transform,
                "INTENZIONE INSTABILE",
                14f,
                Phase02UiFactory.Background,
                TextAlignmentOptions.Center,
                font,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                FontStyles.Bold);
            ui.IncapacitatedState = Phase02UiFactory.CreatePanel(
                "IncapacitatedState",
                card,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                new Color(0.02f, 0.02f, 0.03f, 0.86f),
                false).gameObject;
            Phase02UiFactory.CreateText(
                "TXT_Incapacitated",
                ui.IncapacitatedState.transform,
                "INCAPACITATO",
                23f,
                Phase02UiFactory.SecondaryText,
                TextAlignmentOptions.Center,
                font,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                FontStyles.Bold);
            return ui;
        }

        private static GameObject CreateSelectionIndicator(
            RectTransform card,
            TMP_FontAsset font)
        {
            RectTransform root = Phase02UiFactory.CreatePanel(
                "SelectionIndicator",
                card,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                new Color(0.16f, 0.92f, 0.86f, 0.12f)).GetComponent<RectTransform>();
            Image overlay = root.GetComponent<Image>();
            overlay.raycastTarget = false;

            CreateSelectionBorder(root, "BorderTop", new Vector2(0f, 0.975f), Vector2.one);
            CreateSelectionBorder(root, "BorderBottom", Vector2.zero, new Vector2(1f, 0.025f));
            CreateSelectionBorder(root, "BorderLeft", Vector2.zero, new Vector2(0.025f, 1f));
            CreateSelectionBorder(root, "BorderRight", new Vector2(0.975f, 0f), Vector2.one);
            TMP_Text marker = Phase02UiFactory.CreateText(
                "TXT_SelectedMarker",
                root,
                "[ BERSAGLIO ]",
                22f,
                Phase02UiFactory.Cyan,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.08f, 0.01f),
                new Vector2(0.92f, 0.18f),
                Vector2.zero,
                Vector2.zero,
                FontStyles.Bold);
            marker.raycastTarget = false;
            root.gameObject.SetActive(false);
            return root.gameObject;
        }

        private static void CreateSelectionBorder(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            RectTransform border = Phase02UiFactory.CreatePanel(
                name,
                parent,
                anchorMin,
                anchorMax,
                Vector2.zero,
                Vector2.zero,
                Phase02UiFactory.Cyan);
            border.GetComponent<Image>().raycastTarget = false;
        }

        private static void CreateAnalyzePanel(Transform parent, TMP_FontAsset font, L4Ui ui)
        {
            RectTransform overlay = Phase02UiFactory.CreatePanel(
                "AnalyzePanel",
                parent,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                new Color(0.015f, 0.025f, 0.035f, 0.985f),
                true);
            ui.AnalyzePanel = overlay.gameObject;
            ui.AnalyzeTitle = Phase02UiFactory.CreateText(
                "TXT_AnalyzeTitle",
                overlay,
                "ANALISI",
                38f,
                Phase02UiFactory.Cyan,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.06f, 0.87f),
                new Vector2(0.94f, 0.96f),
                Vector2.zero,
                Vector2.zero,
                FontStyles.Bold);
            ui.AnalyzeBody = Phase02UiFactory.CreateText(
                "TXT_AnalyzeBody",
                overlay,
                "Dati dei nemici",
                24f,
                Phase02UiFactory.MainText,
                TextAlignmentOptions.TopLeft,
                font,
                new Vector2(0.07f, 0.18f),
                new Vector2(0.93f, 0.85f),
                new Vector2(12f, 10f),
                new Vector2(-12f, -10f));
            ui.AnalyzeCloseButton = Phase02UiFactory.CreateButton(
                "BTN_CloseAnalyzeL04",
                overlay,
                "CHIUDI",
                font,
                new Vector2(0.25f, 0.06f),
                new Vector2(0.75f, 0.14f),
                Vector2.zero,
                Vector2.zero,
                true);
        }

        private static void CreateMoralPanel(Transform parent, TMP_FontAsset font, L4Ui ui)
        {
            RectTransform overlay = Phase02UiFactory.CreatePanel(
                "MoralChoicePanel",
                parent,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                new Color(0.025f, 0.035f, 0.035f, 0.992f),
                true);
            ui.MoralPanel = overlay.gameObject;
            ui.MoralFocusTitle = Phase02UiFactory.CreateText(
                "TXT_MoralFocusTitle",
                overlay,
                "DECIDI IL SUO DESTINO · DECISIONE 1/3",
                40f,
                Phase02UiFactory.Gold,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.05f, 0.89f),
                new Vector2(0.95f, 0.97f),
                Vector2.zero,
                Vector2.zero,
                FontStyles.Bold);

            RectTransform portraitRect = Phase02UiFactory.CreateRect(
                "IMG_MoralFocusPortrait",
                overlay);
            Phase02UiFactory.SetRect(
                portraitRect,
                new Vector2(0.07f, 0.64f),
                new Vector2(0.32f, 0.88f),
                Vector2.zero,
                Vector2.zero);
            ui.MoralFocusPortrait = portraitRect.gameObject.AddComponent<Image>();
            ui.MoralFocusPortrait.color = Color.white;
            ui.MoralFocusPortrait.preserveAspect = true;
            ui.MoralFocusPortrait.raycastTarget = false;

            ui.MoralFocusBody = Phase02UiFactory.CreateText(
                "TXT_MoralFocusBody",
                overlay,
                "NOME\nRAZZA · CORRUZIONE · STATO\nSALVA: POTRÀ TORNARE · UCCIDI: USCIRÀ DALLA STORIA",
                30f,
                Phase02UiFactory.MainText,
                TextAlignmentOptions.MidlineLeft,
                font,
                new Vector2(0.35f, 0.61f),
                new Vector2(0.94f, 0.88f),
                new Vector2(12f, 8f),
                new Vector2(-12f, -8f),
                FontStyles.Bold);

            ui.MoralStateTexts = new TMP_Text[3];
            ui.MoralCurrentIndicators = new TMP_Text[3];
            ui.MoralCurrentOutlines = new Outline[3];
            ui.MoralSaveButtons = new Button[3];
            ui.MoralKillButtons = new Button[3];
            for (int index = 0; index < 3; index++)
            {
                float yMax = 0.58f - (index * 0.145f);
                float yMin = yMax - 0.125f;
                RectTransform row = Phase02UiFactory.CreatePanel(
                    "MoralRow_" + (index + 1),
                    overlay,
                    new Vector2(0.06f, yMin),
                    new Vector2(0.94f, yMax),
                    Vector2.zero,
                    Vector2.zero,
                    Phase02UiFactory.Panel);
                ui.MoralCurrentOutlines[index] = row.gameObject.AddComponent<Outline>();
                ui.MoralCurrentOutlines[index].effectColor = Phase02UiFactory.Gold;
                ui.MoralCurrentOutlines[index].effectDistance = new Vector2(4f, -4f);
                ui.MoralCurrentOutlines[index].useGraphicAlpha = false;
                Phase02UiFactory.CreateText(
                    "TXT_EnemyName",
                    row,
                    Enemies[index].DisplayName.ToUpperInvariant(),
                    26f,
                    Phase02UiFactory.MainText,
                    TextAlignmentOptions.MidlineLeft,
                    font,
                    new Vector2(0.34f, 0.54f),
                    new Vector2(0.69f, 0.96f),
                    Vector2.zero,
                    Vector2.zero,
                    FontStyles.Bold);
                ui.MoralStateTexts[index] = Phase02UiFactory.CreateText(
                    "TXT_ChoiceState",
                    row,
                    "DA SCEGLIERE",
                    24f,
                    Phase02UiFactory.SecondaryText,
                    TextAlignmentOptions.MidlineRight,
                    font,
                    new Vector2(0.69f, 0.54f),
                    new Vector2(0.96f, 0.96f),
                    Vector2.zero,
                    Vector2.zero,
                    FontStyles.Bold);
                ui.MoralCurrentIndicators[index] = Phase02UiFactory.CreateText(
                    "TXT_CurrentDecision",
                    row,
                    "> IN DECISIONE",
                    22f,
                    Phase02UiFactory.Gold,
                    TextAlignmentOptions.MidlineLeft,
                    font,
                    new Vector2(0.04f, 0.54f),
                    new Vector2(0.34f, 0.96f),
                    Vector2.zero,
                    Vector2.zero,
                    FontStyles.Bold);
                ui.MoralCurrentIndicators[index].raycastTarget = false;
                ui.MoralSaveButtons[index] = Phase02UiFactory.CreateButton(
                    "BTN_MoralSave" + index,
                    row,
                    "SALVA",
                    font,
                    new Vector2(0.06f, 0.05f),
                    new Vector2(0.47f, 0.51f),
                    Vector2.zero,
                    Vector2.zero);
                ui.MoralKillButtons[index] = Phase02UiFactory.CreateButton(
                    "BTN_MoralKill" + index,
                    row,
                    "UCCIDI",
                    font,
                    new Vector2(0.53f, 0.05f),
                    new Vector2(0.94f, 0.51f),
                    Vector2.zero,
                    Vector2.zero);
            }

            ui.MoralSummary = Phase02UiFactory.CreateText(
                "TXT_MoralSummary",
                overlay,
                "Seleziona tre esiti",
                28f,
                Phase02UiFactory.Light,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.06f, 0.075f),
                new Vector2(0.94f, 0.17f),
                Vector2.zero,
                Vector2.zero);
            ui.MoralConfirmButton = Phase02UiFactory.CreateButton(
                "BTN_ConfirmAllMoralChoices",
                overlay,
                "CONFERMA DEFINITIVA",
                font,
                new Vector2(0.51f, 0.012f),
                new Vector2(0.94f, 0.070f),
                Vector2.zero,
                Vector2.zero,
                true);
            ui.MoralReviewButton = Phase02UiFactory.CreateButton(
                "BTN_ReviewMoralChoices",
                overlay,
                "RIVEDI SCELTE",
                font,
                new Vector2(0.06f, 0.012f),
                new Vector2(0.49f, 0.070f),
                Vector2.zero,
                Vector2.zero);
        }

        private static void CreateOutcomePanel(Transform parent, TMP_FontAsset font, L4Ui ui)
        {
            RectTransform overlay = Phase02UiFactory.CreatePanel(
                "OutcomePanel",
                parent,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                new Color(0.015f, 0.03f, 0.03f, 0.992f),
                true);
            ui.OutcomePanel = overlay.gameObject;
            ui.OutcomeTitle = Phase02UiFactory.CreateText(
                "TXT_OutcomeTitle",
                overlay,
                "VITTORIA",
                50f,
                Phase02UiFactory.Cyan,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.06f, 0.60f),
                new Vector2(0.94f, 0.78f),
                Vector2.zero,
                Vector2.zero,
                FontStyles.Bold);
            ui.OutcomeBody = Phase02UiFactory.CreateText(
                "TXT_OutcomeBody",
                overlay,
                "BRUTO DELLE RADICI: SALVATO / UCCISO\n" +
                "VEGLIA SOSPESA: SALVATO / UCCISO\n" +
                "MASCHERA DEL VENTO: SALVATO / UCCISO\n" +
                "CONTENUTO DISPONIBILE COMPLETATO\n" +
                "LIVELLO 5 PROSSIMAMENTE",
                29f,
                Phase02UiFactory.MainText,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.08f, 0.36f),
                new Vector2(0.92f, 0.58f),
                Vector2.zero,
                Vector2.zero,
                FontStyles.Bold);
            ui.OutcomeMenuButton = Phase02UiFactory.CreateButton(
                "BTN_OutcomeMenuL04",
                overlay,
                "MENU PRINCIPALE",
                font,
                new Vector2(0.07f, 0.18f),
                new Vector2(0.48f, 0.29f),
                Vector2.zero,
                Vector2.zero,
                true);
            ui.OutcomeRetryButton = Phase02UiFactory.CreateButton(
                "BTN_OutcomeRetryL04",
                overlay,
                "RIPROVA",
                font,
                new Vector2(0.52f, 0.18f),
                new Vector2(0.93f, 0.29f),
                Vector2.zero,
                Vector2.zero);
            ui.OutcomeRetryLabel = ui.OutcomeRetryButton.GetComponentInChildren<TMP_Text>(true);
        }

        private static void CreateTargetTutorialPanel(
            Transform parent,
            TMP_FontAsset font,
            L4Ui ui)
        {
            RectTransform overlay = Phase02UiFactory.CreatePanel(
                "TargetTutorialOverlay",
                parent,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                new Color(0.012f, 0.025f, 0.025f, 0.985f),
                true);
            ui.TargetTutorialOverlay = overlay.gameObject;
            RectTransform card = Phase02UiFactory.CreatePanel(
                "TargetTutorialCard",
                overlay,
                new Vector2(0.06f, 0.30f),
                new Vector2(0.94f, 0.70f),
                Vector2.zero,
                Vector2.zero,
                Phase02UiFactory.Panel);
            Phase02UiFactory.CreateText(
                "TXT_TargetTutorialTitle",
                card,
                "SCEGLI UN BERSAGLIO",
                50f,
                Phase02UiFactory.Gold,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.06f, 0.68f),
                new Vector2(0.94f, 0.92f),
                Vector2.zero,
                Vector2.zero,
                FontStyles.Bold);
            ui.TargetTutorialText = Phase02UiFactory.CreateText(
                "TXT_TargetTutorialBody",
                card,
                "Tocca un personaggio o la sua scheda per sceglierlo come bersaglio. " +
                "Guardia resta disponibile anche prima della scelta.",
                34f,
                Phase02UiFactory.MainText,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.08f, 0.30f),
                new Vector2(0.92f, 0.67f),
                Vector2.zero,
                Vector2.zero);
            ui.TargetTutorialContinueButton = Phase02UiFactory.CreateButton(
                "BTN_TargetTutorialContinue",
                card,
                "HO CAPITO",
                font,
                new Vector2(0.20f, 0.07f),
                new Vector2(0.80f, 0.27f),
                Vector2.zero,
                Vector2.zero,
                true);
        }

        private static void AssignController(
            MultiEnemyBattleController controller,
            MultiEnemyBattleNavigation navigation,
            L4World world,
            L4Ui ui)
        {
            SerializedObject serialized = new SerializedObject(controller);
            serialized.Update();
            serialized.FindProperty("randomSeed").intValue = 4404;
            SerializedProperty enemyViews = serialized.FindProperty("enemyViews");
            enemyViews.arraySize = 3;
            for (int index = 0; index < 3; index++)
            {
                SerializedProperty entry = enemyViews.GetArrayElementAtIndex(index);
                EnemyDefinition definition = Enemies[index];
                EnemyWorld enemyWorld = world.Enemies[index];
                EnemyUi enemyUi = ui.Enemies[index];
                SetRelativeString(entry, "enemyId", definition.EnemyId);
                SetRelativeString(entry, "displayName", definition.DisplayName);
                SetRelativeString(entry, "race", definition.Race);
                SetRelativeInt(entry, "maxHp", definition.MaxHp);
                SetRelativeInt(entry, "corruptionPercent", definition.Corruption);
                SetRelativeInt(entry, "initialMood", (int)definition.Mood);
                SetRelativeInt(entry, "intelligenceLevel", definition.Intelligence);
                SetRelativeInt(entry, "altitude", (int)definition.Altitude);
                SetRelativeInt(entry, "attackDamage", definition.Attack);
                SetRelativeInt(entry, "chargedStrikeDamage", definition.ChargedStrike);
                SetRelativeInt(entry, "assaultDamage", definition.Assault);
                SetRelativeInt(entry, "traits", (int)definition.Traits);
                SetRelativeFloat(
                    entry,
                    "aggressiveWeight",
                    (float)definition.Profile.GetTraitWeight(EnemyBehaviorTraits.Aggressive));
                SetRelativeFloat(
                    entry,
                    "patientWeight",
                    (float)definition.Profile.GetTraitWeight(EnemyBehaviorTraits.Patient));
                SetRelativeFloat(
                    entry,
                    "deceptiveWeight",
                    (float)definition.Profile.GetTraitWeight(EnemyBehaviorTraits.Deceptive));
                EnemyDeceptionSettings deception = definition.Profile.DeceptionSettings;
                SetRelativeFloat(
                    entry,
                    "bluffProbability",
                    (float)deception.BluffProbability);
                SetRelativeInt(
                    entry,
                    "minimumTurnsBetweenBluffs",
                    deception.MinimumTurnsBetweenBluffs);
                SetRelativeFloat(
                    entry,
                    "feintIntentWeight",
                    (float)deception.FeintIntentWeight);
                SetRelativeString(entry, "openingDialogue", definition.OpeningDialogue);
                SetRelativeString(
                    entry,
                    "incapacitatedDialogue",
                    definition.IncapacitatedDialogue);
                SetRelativeObject(entry, "actor", enemyWorld.Actor);
                SetRelativeObject(entry, "visual", enemyWorld.Visual);
                SetRelativeObject(entry, "targetButton", enemyUi.TargetButton);
                SetRelativeObject(entry, "nameText", enemyUi.NameText);
                SetRelativeObject(entry, "healthText", enemyUi.HealthText);
                SetRelativeObject(entry, "healthFill", enemyUi.HealthFill);
                SetRelativeObject(entry, "intentText", enemyUi.IntentText);
                SetRelativeObject(entry, "targetStateText", enemyUi.TargetStateText);
                SetRelativeObject(entry, "selectionIndicator", enemyUi.SelectionIndicator);
                SetRelativeObject(entry, "instabilityClue", enemyUi.InstabilityClue);
                SetRelativeObject(entry, "incapacitatedState", enemyUi.IncapacitatedState);
                SetRelativeObject(entry, "guardEffect", enemyWorld.GuardEffect);
                SetRelativeObject(entry, "chargeEffect", enemyWorld.ChargeEffect);
                SetRelativeObject(entry, "hitEffect", enemyWorld.HitEffect);
            }

            Set(serialized, "heroActor", world.HeroActor);
            Set(serialized, "heroVisual", world.HeroVisual);
            Set(serialized, "heroHealthFill", ui.HeroHealthFill);
            Set(serialized, "heroHealthText", ui.HeroHealthText);
            Set(serialized, "heroGuardEffect", world.HeroGuardEffect);
            Set(serialized, "heroAttackEffect", world.HeroAttackEffect);
            Set(serialized, "heroTechniqueEffect", world.HeroTechniqueEffect);
            Set(serialized, "attackButton", ui.AttackButton);
            Set(serialized, "guardButton", ui.GuardButton);
            Set(serialized, "techniqueButton", ui.TechniqueButton);
            Set(serialized, "analyzeButton", ui.AnalyzeButton);
            Set(serialized, "techniqueButtonLabel", ui.TechniqueLabel);
            Set(serialized, "combatMessageText", ui.CombatMessage);
            Set(serialized, "selectedTargetText", ui.SelectedTargetText);
            Set(serialized, "heroStatusText", ui.HeroStatusText);
            Set(serialized, "phaseIndicatorText", ui.PhaseIndicator);
            Set(serialized, "targetTutorialOverlay", ui.TargetTutorialOverlay);
            Set(serialized, "targetTutorialText", ui.TargetTutorialText);
            Set(serialized, "targetTutorialContinueButton", ui.TargetTutorialContinueButton);
            Set(serialized, "dialogueRoot", ui.DialogueRoot);
            Set(serialized, "dialogueText", ui.DialogueText);
            Set(serialized, "analyzePanel", ui.AnalyzePanel);
            Set(serialized, "analyzeTitleText", ui.AnalyzeTitle);
            Set(serialized, "analyzeBodyText", ui.AnalyzeBody);
            Set(serialized, "analyzeCloseButton", ui.AnalyzeCloseButton);
            Set(serialized, "thornGuardianAllyActor", world.ThornAllyActor);
            Set(serialized, "thornGuardianSupportEffect", world.ThornSupportEffect);
            Set(serialized, "ashWatcherAllyActor", world.AshAllyActor);
            Set(serialized, "ashWatcherSupportEffect", world.AshSupportEffect);
            Set(serialized, "allyDialogueRoot", ui.AllyDialogueRoot);
            Set(serialized, "allyDialogueText", ui.AllyDialogueText);
            Set(serialized, "moralChoicePanel", ui.MoralPanel);
            SetArray(serialized, "moralChoiceStateTexts", ui.MoralStateTexts);
            SetArray(serialized, "moralCurrentIndicators", ui.MoralCurrentIndicators);
            SetArray(serialized, "moralCurrentOutlines", ui.MoralCurrentOutlines);
            SetArray(serialized, "moralSaveButtons", ui.MoralSaveButtons);
            SetArray(serialized, "moralKillButtons", ui.MoralKillButtons);
            Set(serialized, "moralSummaryText", ui.MoralSummary);
            Set(serialized, "moralConfirmButton", ui.MoralConfirmButton);
            Set(serialized, "moralReviewButton", ui.MoralReviewButton);
            Set(serialized, "moralFocusTitleText", ui.MoralFocusTitle);
            Set(serialized, "moralFocusBodyText", ui.MoralFocusBody);
            Set(serialized, "moralFocusPortrait", ui.MoralFocusPortrait);
            Set(serialized, "outcomePanel", ui.OutcomePanel);
            Set(serialized, "outcomeTitleText", ui.OutcomeTitle);
            Set(serialized, "outcomeBodyText", ui.OutcomeBody);
            Set(serialized, "outcomeMenuButton", ui.OutcomeMenuButton);
            Set(serialized, "outcomeRetryButton", ui.OutcomeRetryButton);
            Set(serialized, "outcomeRetryButtonLabel", ui.OutcomeRetryLabel);
            Set(serialized, "navigation", navigation);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignActorTargets(
            MultiEnemyBattleController controller,
            L4World world)
        {
            for (int index = 0; index < world.Enemies.Length; index++)
            {
                EnemyWorld enemy = world.Enemies[index];
                BoxCollider2D collider = enemy.Actor.gameObject.AddComponent<BoxCollider2D>();
                Bounds bounds = enemy.Visual.bounds;
                Vector3 localCenter = enemy.Actor.InverseTransformPoint(bounds.center);
                collider.offset = new Vector2(localCenter.x, localCenter.y);
                collider.size = new Vector2(
                    Mathf.Max(0.8f, bounds.size.x / enemy.Actor.lossyScale.x),
                    Mathf.Max(1.2f, bounds.size.y / enemy.Actor.lossyScale.y));

                MultiEnemyActorTarget target =
                    enemy.Actor.gameObject.AddComponent<MultiEnemyActorTarget>();
                SerializedObject serialized = new SerializedObject(target);
                serialized.Update();
                Set(serialized, "battleController", controller);
                serialized.FindProperty("enemyIndex").intValue = index;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(collider);
                EditorUtility.SetDirty(target);
            }
        }

        private static void AssignNavigation(
            MultiEnemyBattleNavigation navigation,
            MultiEnemyBattleController controller,
            L4Ui ui)
        {
            SerializedObject serialized = new SerializedObject(navigation);
            serialized.Update();
            Set(serialized, "backButton", ui.BackButton);
            Set(serialized, "resultMenuButton", ui.OutcomeMenuButton);
            Set(serialized, "retryButton", ui.OutcomeRetryButton);
            Set(serialized, "battleController", controller);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddListeners(
            MultiEnemyBattleController controller,
            MultiEnemyBattleNavigation navigation,
            L4Ui ui)
        {
            SetListener(ui.AttackButton, controller.ChooseAttack);
            SetListener(ui.GuardButton, controller.ChooseGuard);
            SetListener(ui.TechniqueButton, controller.ChooseTechnique);
            SetListener(ui.AnalyzeButton, controller.OpenAnalyze);
            SetListener(ui.AnalyzeCloseButton, controller.CloseAnalyze);
            SetListener(ui.BackButton, navigation.BackToMenu);
            SetListener(ui.OutcomeMenuButton, navigation.BackToMenu);
            SetListener(ui.OutcomeRetryButton, controller.RetryLevel);
            SetListener(ui.TargetTutorialContinueButton, controller.CompleteMultiTargetTutorial);
            SetListener(ui.Enemies[0].TargetButton, controller.SelectBrute);
            SetListener(ui.Enemies[1].TargetButton, controller.SelectWatcher);
            SetListener(ui.Enemies[2].TargetButton, controller.SelectMask);
            SetListener(ui.MoralSaveButtons[0], controller.ChooseBruteSaved);
            SetListener(ui.MoralKillButtons[0], controller.ChooseBruteKilled);
            SetListener(ui.MoralSaveButtons[1], controller.ChooseWatcherSaved);
            SetListener(ui.MoralKillButtons[1], controller.ChooseWatcherKilled);
            SetListener(ui.MoralSaveButtons[2], controller.ChooseMaskSaved);
            SetListener(ui.MoralKillButtons[2], controller.ChooseMaskKilled);
            SetListener(ui.MoralReviewButton, controller.ReviewMoralChoices);
            SetListener(ui.MoralConfirmButton, controller.ConfirmMoralChoices);
        }

        private static void SetInitialState(L4World world, L4Ui ui)
        {
            world.HeroGuardEffect.SetActive(false);
            world.HeroAttackEffect.SetActive(false);
            world.HeroTechniqueEffect.SetActive(false);
            world.ThornAllyActor.SetActive(false);
            world.AshAllyActor.SetActive(false);
            world.ThornSupportEffect.SetActive(false);
            world.AshSupportEffect.SetActive(false);
            foreach (EnemyWorld enemy in world.Enemies)
            {
                enemy.GuardEffect.SetActive(false);
                enemy.ChargeEffect.SetActive(false);
                enemy.HitEffect.SetActive(false);
            }

            foreach (EnemyUi enemy in ui.Enemies)
            {
                enemy.SelectionIndicator.SetActive(false);
                enemy.InstabilityClue.SetActive(false);
                enemy.IncapacitatedState.SetActive(false);
            }

            ui.DialogueRoot.SetActive(false);
            ui.AllyDialogueRoot.SetActive(false);
            ui.AnalyzePanel.SetActive(false);
            ui.MoralPanel.SetActive(false);
            ui.OutcomePanel.SetActive(false);
            ui.TargetTutorialOverlay.SetActive(false);
            ui.OutcomeRetryButton.gameObject.SetActive(false);
            for (int index = 0; index < ui.MoralCurrentIndicators.Length; index++)
            {
                ui.MoralCurrentIndicators[index].gameObject.SetActive(false);
                ui.MoralCurrentOutlines[index].enabled = false;
            }
            ui.MoralConfirmButton.interactable = false;
            ui.MoralReviewButton.gameObject.SetActive(false);
        }

        private static Transform CreateActor(
            string prefabPath,
            Transform parent,
            string name,
            Vector3 position,
            float scale,
            Color tint,
            int sortingOrder,
            out SpriteRenderer visual)
        {
            GameObject slot = new GameObject(name);
            slot.transform.SetParent(parent, false);
            slot.transform.localPosition = position;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, slot.transform) as GameObject;
            if (instance == null) throw new InvalidOperationException("Prefab mancante: " + prefabPath);
            instance.name = "Visual";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localScale = Vector3.one * scale;
            visual = instance.GetComponentInChildren<SpriteRenderer>(true);
            if (visual == null) throw new InvalidOperationException(prefabPath + " non contiene SpriteRenderer.");
            foreach (SpriteRenderer renderer in instance.GetComponentsInChildren<SpriteRenderer>(true))
            {
                renderer.color *= tint;
                renderer.sortingOrder = sortingOrder;
            }

            visual.flipX = name != "HeroActor";
            return slot.transform;
        }

        private static GameObject CreateWorldEffect(
            string prefabPath,
            Transform parent,
            string name,
            Vector3 position,
            float scale,
            Color tint)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (instance == null) throw new InvalidOperationException("Effetto mancante: " + prefabPath);
            instance.name = name;
            instance.transform.localPosition = position;
            instance.transform.localScale = Vector3.one * scale;
            foreach (SpriteRenderer renderer in instance.GetComponentsInChildren<SpriteRenderer>(true))
            {
                renderer.color *= tint;
                renderer.sortingOrder = 8;
            }

            return instance;
        }

        private static HealthUi CreateHealthPanel(
            string name,
            Transform parent,
            string title,
            Vector2 anchorMin,
            Vector2 anchorMax,
            TMP_FontAsset font,
            Color color,
            string value)
        {
            RectTransform root = Phase02UiFactory.CreatePanel(
                name,
                parent,
                anchorMin,
                anchorMax,
                Vector2.zero,
                Vector2.zero,
                Phase02UiFactory.Panel);
            if (!string.IsNullOrEmpty(title))
            {
                Phase02UiFactory.CreateText(
                    "TXT_Title",
                    root,
                    title,
                    18f,
                    Phase02UiFactory.MainText,
                    TextAlignmentOptions.MidlineLeft,
                    font,
                    new Vector2(0.02f, 0f),
                    new Vector2(0.20f, 1f),
                    Vector2.zero,
                    Vector2.zero,
                    FontStyles.Bold);
            }

            float fillXMin = string.IsNullOrEmpty(title) ? 0.03f : 0.22f;
            RectTransform fillBackground = Phase02UiFactory.CreatePanel(
                "FillBackground",
                root,
                new Vector2(fillXMin, 0.18f),
                new Vector2(0.97f, 0.82f),
                Vector2.zero,
                Vector2.zero,
                Phase02UiFactory.HighlightedPanel);
            RectTransform fillRect = Phase02UiFactory.CreatePanel(
                "Fill",
                fillBackground,
                Vector2.zero,
                Vector2.one,
                new Vector2(2f, 2f),
                new Vector2(-2f, -2f),
                color);
            Image fill = fillRect.GetComponent<Image>();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 1f;
            TMP_Text valueText = Phase02UiFactory.CreateText(
                "TXT_Value",
                fillBackground,
                value,
                17f,
                Phase02UiFactory.MainText,
                TextAlignmentOptions.Center,
                font,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                FontStyles.Bold);
            return new HealthUi(fill, valueText);
        }

        private static TMP_Text CreateStatusText(
            Transform parent,
            TMP_FontAsset font,
            string name,
            string value,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            RectTransform panel = Phase02UiFactory.CreatePanel(
                name,
                parent,
                anchorMin,
                anchorMax,
                Vector2.zero,
                Vector2.zero,
                Phase02UiFactory.HighlightedPanel);
            return Phase02UiFactory.CreateText(
                "TXT_" + name,
                panel,
                value,
                18f,
                Phase02UiFactory.Light,
                TextAlignmentOptions.Center,
                font,
                Vector2.zero,
                Vector2.one,
                new Vector2(6f, 3f),
                new Vector2(-6f, -3f),
                FontStyles.Bold);
        }

        private static DialogueUi CreateDialogue(
            Transform parent,
            TMP_FontAsset font,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color color)
        {
            RectTransform root = Phase02UiFactory.CreatePanel(
                name,
                parent,
                anchorMin,
                anchorMax,
                Vector2.zero,
                Vector2.zero,
                color);
            TMP_Text text = Phase02UiFactory.CreateText(
                "TXT_Dialogue",
                root,
                "Dialogo",
                21f,
                Phase02UiFactory.MainText,
                TextAlignmentOptions.Center,
                font,
                Vector2.zero,
                Vector2.one,
                new Vector2(12f, 8f),
                new Vector2(-12f, -8f),
                FontStyles.Italic);
            return new DialogueUi(root.gameObject, text);
        }

        private static Button CreateActionButton(
            Transform parent,
            TMP_FontAsset font,
            string name,
            string label,
            int index)
        {
            float gap = 0.012f;
            float width = (0.94f - (gap * 3f)) / 4f;
            float xMin = 0.03f + index * (width + gap);
            return Phase02UiFactory.CreateButton(
                name,
                parent,
                label,
                font,
                new Vector2(xMin, 0.015f),
                new Vector2(xMin + width, 0.105f),
                Vector2.zero,
                Vector2.zero,
                index == 0);
        }

        private static void CreateCamera(Transform parent)
        {
            GameObject cameraObject = new GameObject(
                "Main Camera",
                typeof(Camera),
                typeof(AudioListener),
                typeof(Physics2DRaycaster));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(parent, false);
            cameraObject.transform.localPosition = new Vector3(0f, 0f, -10f);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 9.6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Phase02UiFactory.Background;
        }

        private static void Set(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null) throw new InvalidOperationException(propertyName + " non trovato.");
            property.objectReferenceValue = value;
        }

        private static void SetArray<T>(SerializedObject serialized, string propertyName, T[] values)
            where T : UnityEngine.Object
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            property.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            }
        }

        private static void SetRelativeObject(
            SerializedProperty parent,
            string name,
            UnityEngine.Object value)
        {
            parent.FindPropertyRelative(name).objectReferenceValue = value;
        }

        private static void SetRelativeString(SerializedProperty parent, string name, string value)
        {
            parent.FindPropertyRelative(name).stringValue = value;
        }

        private static void SetRelativeInt(SerializedProperty parent, string name, int value)
        {
            parent.FindPropertyRelative(name).intValue = value;
        }

        private static void SetRelativeFloat(SerializedProperty parent, string name, float value)
        {
            parent.FindPropertyRelative(name).floatValue = value;
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

        private sealed class EnemyDefinition
        {
            internal EnemyDefinition(
                MultiEnemyProfile profile,
                string openingDialogue,
                string incapacitatedDialogue,
                string actorName,
                Vector3 position,
                float scale,
                Color tint)
            {
                if (profile == null)
                {
                    throw new ArgumentNullException(nameof(profile));
                }

                Profile = profile;
                OpeningDialogue = openingDialogue;
                IncapacitatedDialogue = incapacitatedDialogue;
                ActorName = actorName;
                Position = position;
                Scale = scale;
                Tint = tint;
            }

            internal MultiEnemyProfile Profile { get; }
            internal string EnemyId => Profile.EnemyId;
            internal string DisplayName => Profile.DisplayName;
            internal string Race => Profile.Race;
            internal int MaxHp => Profile.MaxHp;
            internal int Corruption => Profile.CorruptionPercent;
            internal EnemyMood Mood => Profile.Mood;
            internal int Intelligence => Profile.IntelligenceLevel;
            internal EnemyAltitude Altitude => Profile.Altitude;
            internal int Attack => Profile.AttackDamage;
            internal int ChargedStrike => Profile.ChargedStrikeDamage;
            internal int Assault => Profile.AssaultDamage;
            internal EnemyBehaviorTraits Traits => Profile.Traits;
            internal string OpeningDialogue { get; }
            internal string IncapacitatedDialogue { get; }
            internal string ActorName { get; }
            internal Vector3 Position { get; }
            internal float Scale { get; }
            internal Color Tint { get; }
        }

        private sealed class L4World
        {
            internal GameObject Root;
            internal Transform HeroActor;
            internal SpriteRenderer HeroVisual;
            internal EnemyWorld[] Enemies;
            internal GameObject HeroGuardEffect;
            internal GameObject HeroAttackEffect;
            internal GameObject HeroTechniqueEffect;
            internal GameObject ThornAllyActor;
            internal GameObject AshAllyActor;
            internal GameObject ThornSupportEffect;
            internal GameObject AshSupportEffect;
        }

        private sealed class EnemyWorld
        {
            internal Transform Actor;
            internal SpriteRenderer Visual;
            internal GameObject GuardEffect;
            internal GameObject ChargeEffect;
            internal GameObject HitEffect;
        }

        private sealed class L4Ui
        {
            internal GameObject Root;
            internal Image HeroHealthFill;
            internal TMP_Text HeroHealthText;
            internal EnemyUi[] Enemies;
            internal TMP_Text CombatMessage;
            internal TMP_Text SelectedTargetText;
            internal TMP_Text HeroStatusText;
            internal TMP_Text PhaseIndicator;
            internal GameObject TargetTutorialOverlay;
            internal TMP_Text TargetTutorialText;
            internal Button TargetTutorialContinueButton;
            internal GameObject DialogueRoot;
            internal TMP_Text DialogueText;
            internal GameObject AllyDialogueRoot;
            internal TMP_Text AllyDialogueText;
            internal Button BackButton;
            internal Button AttackButton;
            internal Button GuardButton;
            internal Button TechniqueButton;
            internal TMP_Text TechniqueLabel;
            internal Button AnalyzeButton;
            internal GameObject AnalyzePanel;
            internal TMP_Text AnalyzeTitle;
            internal TMP_Text AnalyzeBody;
            internal Button AnalyzeCloseButton;
            internal GameObject MoralPanel;
            internal TMP_Text[] MoralStateTexts;
            internal TMP_Text[] MoralCurrentIndicators;
            internal Outline[] MoralCurrentOutlines;
            internal Button[] MoralSaveButtons;
            internal Button[] MoralKillButtons;
            internal TMP_Text MoralSummary;
            internal Button MoralConfirmButton;
            internal Button MoralReviewButton;
            internal TMP_Text MoralFocusTitle;
            internal TMP_Text MoralFocusBody;
            internal Image MoralFocusPortrait;
            internal GameObject OutcomePanel;
            internal TMP_Text OutcomeTitle;
            internal TMP_Text OutcomeBody;
            internal Button OutcomeMenuButton;
            internal Button OutcomeRetryButton;
            internal TMP_Text OutcomeRetryLabel;
        }

        private sealed class EnemyUi
        {
            internal Button TargetButton;
            internal TMP_Text NameText;
            internal TMP_Text HealthText;
            internal Image HealthFill;
            internal TMP_Text IntentText;
            internal TMP_Text TargetStateText;
            internal GameObject SelectionIndicator;
            internal GameObject InstabilityClue;
            internal GameObject IncapacitatedState;
        }

        private readonly struct HealthUi
        {
            internal HealthUi(Image fill, TMP_Text value)
            {
                Fill = fill;
                Value = value;
            }

            internal Image Fill { get; }
            internal TMP_Text Value { get; }
        }

        private readonly struct DialogueUi
        {
            internal DialogueUi(GameObject root, TMP_Text text)
            {
                Root = root;
                Text = text;
            }

            internal GameObject Root { get; }
            internal TMP_Text Text { get; }
        }
    }
}
#endif
