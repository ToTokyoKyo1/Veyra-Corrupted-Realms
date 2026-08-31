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
using Veyra.Combat.Encounter;
using Veyra.Core;
using Veyra.UI.MainMenu;

namespace Veyra.Editor
{
    internal static class Phase046EncounterSceneFactory
    {
        internal const string MainMenuScenePath = "Assets/_Veyra/Scenes/SCN_MainMenu.unity";
        internal const string TutorialScenePath = "Assets/_Veyra/Scenes/SCN_W01_L01_Tutorial.unity";
        internal const string Level02ScenePath = "Assets/_Veyra/Scenes/SCN_W01_L02_ThornGuardian.unity";
        internal const string Level03ScenePath = "Assets/_Veyra/Scenes/SCN_W01_L03_AshWatcher.unity";
        internal const string BattleRootName = "EncounterBattleRoot";
        internal const string UiRootName = "EncounterUIRoot";
        internal const string CampaignControlsName = "CampaignControls";

        private static readonly EncounterDefinition ThornGuardian = new EncounterDefinition
        {
            ScenePath = Level02ScenePath,
            SceneRootName = SceneNames.World01Level02ThornGuardian,
            CampaignEncounter = CampaignEncounter.ThornGuardian,
            EncounterId = "world01_encounter02_thorn_guardian",
            EnemyDisplayName = "Custode del Rovo",
            EnemyRace = "Custode Silvano",
            EnemyCorruptionPercent = 58,
            InitialMood = EnemyMood.Triste,
            IntelligenceLevel = 1,
            RandomSeed = 2403,
            EnemyMaxHp = 115,
            EnemyAttackDamage = 22,
            ChargedStrikeDamage = 40,
            EnemyTint = new Color(0.64f, 0.78f, 0.42f, 1f),
            OpeningDialogue = "Fermati... non sono io a muovere queste radici.",
            AttackReactionDialogue = "Hai deciso di colpirmi... forse è l'unico modo che conosci.",
            GuardReactionDialogue = "Ti stai proteggendo. Forse non vuoi davvero uccidermi.",
            TechniqueReactionDialogue = "Sento quella forza. Può liberarmi oppure distruggermi.",
            FirstAnalyzeDialogue = "Non guardarmi così... so già cosa mi sta succedendo.",
            RepeatedAnalyzeDialogue = "Puoi continuare a studiarmi. La corruzione non se ne andrà da sola.",
            LowHpDialogue = "Non riesco a fermarmi... ma tu puoi ancora scegliere.",
            AttackPatternDialogue = "Continui a colpire. Le radici provano a chiudersi.",
            GuardPatternDialogue = "Aspetti dietro la difesa... sento la tua esitazione.",
            TechniquePatternDialogue = "La tua forza ritorna con un ritmo preciso.",
            StrategyChangedDialogue = "Hai cambiato passo.",
            DefeatedDialogue = "La corruzione tace per un istante. Ora scegli tu.",
            SavedDialogue = "Il dolore... si sta ritirando. Questa scelta era tua, non mia.",
            KilledDialogue = "Almeno... il rumore finalmente finirà.",
            Tendency = "Alterna colpi, Guardia di Corteccia e una carica ben visibile."
        };

        private static readonly EncounterDefinition AshWatcher = new EncounterDefinition
        {
            ScenePath = Level03ScenePath,
            SceneRootName = SceneNames.World01Level03AshWatcher,
            CampaignEncounter = CampaignEncounter.AshWatcher,
            EncounterId = "world01_encounter03_ash_watcher",
            EnemyDisplayName = "Vigile delle Ceneri",
            EnemyRace = "Umano Mutato",
            EnemyCorruptionPercent = 82,
            InitialMood = EnemyMood.Arrabbiato,
            IntelligenceLevel = 2,
            RandomSeed = 3503,
            EnemyMaxHp = 130,
            EnemyAttackDamage = 24,
            ChargedStrikeDamage = 44,
            EnemyTint = new Color(0.82f, 0.48f, 0.42f, 1f),
            OpeningDialogue = "Ogni gesto ti tradisce. Combatti pure: io imparerò.",
            AttackReactionDialogue = "Un altro colpo. Vediamo se manterrai il ritmo.",
            GuardReactionDialogue = "Ti nascondi dietro la difesa. Posso aspettare.",
            TechniqueReactionDialogue = "Quella forza ha una cadenza. La ricorderò.",
            FirstAnalyzeDialogue = "Mi studi? Anch'io sto studiando te.",
            RepeatedAnalyzeDialogue = "Continua pure. Ogni esitazione racconta qualcosa.",
            LowHpDialogue = "Anche sconfitto, continuo a vedere le tue scelte.",
            AttackPatternDialogue = "Attacchi sempre nello stesso modo.",
            GuardPatternDialogue = "Ti chiudi dietro la difesa. Allora aspetterò.",
            TechniquePatternDialogue = "Conosco il ritmo della tua forza.",
            StrategyChangedDialogue = "Hai cambiato cadenza... interessante.",
            DefeatedDialogue = "Ho imparato ogni tua risposta, tranne quella che sceglierai ora.",
            SavedDialogue = "Hai spezzato il ciclo che avevo imparato ad accettare.",
            KilledDialogue = "Era questa la risposta che avevi preparato.",
            Tendency = "Osserva le ultime azioni e adatta i turni futuri senza conoscere la scelta corrente."
        };

        internal static void CreateOrUpdateCampaign(Phase046CampaignSetupReport report)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Il tool campagna può essere eseguito soltanto in Edit Mode.");
            }

            PreserveDirtyScenesBeforeAuthoring();
            ValidateRequiredAssets();
            CreateOrUpdateEncounter(ThornGuardian, report);
            CreateOrUpdateEncounter(AshWatcher, report);
            CreateOrUpdateMainMenu(report);
            ConfigureBuildSettings(report);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EditorSceneManager.OpenScene(Level02ScenePath, OpenSceneMode.Single);
        }

        private static void PreserveDirtyScenesBeforeAuthoring()
        {
            bool hasDirtyScene = Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .Any(scene => scene.IsValid() && scene.isLoaded && scene.isDirty);
            if (!hasDirtyScene)
            {
                return;
            }

            if (Application.isBatchMode)
            {
                throw new InvalidOperationException(
                    "Una scena aperta contiene modifiche non salvate. Salvarla prima di generare la campagna in batch mode.");
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                throw new OperationCanceledException("Generazione annullata per preservare le scene modificate.");
            }
        }

        private static void ValidateRequiredAssets()
        {
            string[] requiredPaths =
            {
                MainMenuScenePath,
                TutorialScenePath,
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

            string[] missing = requiredPaths
                .Where(path => AssetDatabase.LoadMainAssetAtPath(path) == null)
                .ToArray();
            if (missing.Length > 0)
            {
                throw new InvalidOperationException(
                    "Asset persistenti necessari alla campagna mancanti:\n- " + string.Join("\n- ", missing));
            }
        }

        private static void CreateOrUpdateEncounter(
            EncounterDefinition definition,
            Phase046CampaignSetupReport report)
        {
            bool existed = AssetDatabase.LoadAssetAtPath<SceneAsset>(definition.ScenePath) != null;
            Scene scene = existed
                ? EditorSceneManager.OpenScene(definition.ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject sceneRoot = FindOrCreateUniqueSceneRoot(scene, definition.SceneRootName, report);
            RemoveOwnedDirectChildren(sceneRoot.transform, BattleRootName, report);
            RemoveOwnedDirectChildren(sceneRoot.transform, UiRootName, report);

            TMP_FontAsset font = Phase02UiFactory.LoadRequiredFont();
            BattleWorld world = CreateBattleWorld(sceneRoot.transform, definition);
            BattleUi ui = CreateBattleUi(sceneRoot.transform, font, definition);

            EncounterBattleController controller = world.Root.AddComponent<EncounterBattleController>();
            EncounterBattleNavigation navigation = ui.Root.AddComponent<EncounterBattleNavigation>();
            AssignControllerReferences(controller, navigation, world, ui, definition);
            AssignNavigationReferences(navigation, controller, ui);
            AddPersistentListeners(controller, navigation, ui);
            SetInitialActiveState(world, ui);
            EnsureCamera(sceneRoot.transform, report);
            EnsureEventSystem(sceneRoot.transform, report);

            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(navigation);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, definition.ScenePath))
            {
                throw new InvalidOperationException("Impossibile salvare la scena: " + definition.ScenePath);
            }

            if (existed)
            {
                report.Configure(definition.SceneRootName + " (rigenerata senza duplicati)");
            }
            else
            {
                report.Create(definition.ScenePath);
            }
        }

        private static GameObject FindOrCreateUniqueSceneRoot(
            Scene scene,
            string rootName,
            Phase046CampaignSetupReport report)
        {
            GameObject[] matches = scene.GetRootGameObjects().Where(root => root.name == rootName).ToArray();
            if (matches.Length > 1)
            {
                throw new InvalidOperationException(
                    "La scena contiene più root chiamati " + rootName + "; correggere manualmente prima di rigenerare.");
            }

            if (matches.Length == 1)
            {
                report.Preserve(rootName);
                return matches[0];
            }

            GameObject sceneRoot = new GameObject(rootName);
            SceneManager.MoveGameObjectToScene(sceneRoot, scene);
            report.Create(rootName);
            return sceneRoot;
        }

        private static void RemoveOwnedDirectChildren(
            Transform sceneRoot,
            string ownedName,
            Phase046CampaignSetupReport report)
        {
            int removed = 0;
            for (int index = sceneRoot.childCount - 1; index >= 0; index--)
            {
                Transform child = sceneRoot.GetChild(index);
                if (child.name != ownedName)
                {
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(child.gameObject);
                removed++;
            }

            if (removed > 0)
            {
                report.Configure(ownedName + " (root di proprietà rigenerato: " + removed + ")");
            }
        }

        private static BattleWorld CreateBattleWorld(Transform parent, EncounterDefinition definition)
        {
            BattleWorld world = new BattleWorld();
            world.Root = new GameObject(BattleRootName);
            world.Root.transform.SetParent(parent, false);

            Sprite backgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Phase01PlaceholderFactory.BackgroundSpritePath);
            GameObject background = new GameObject("Background", typeof(SpriteRenderer));
            background.transform.SetParent(world.Root.transform, false);
            SpriteRenderer backgroundRenderer = background.GetComponent<SpriteRenderer>();
            backgroundRenderer.sprite = backgroundSprite;
            backgroundRenderer.sortingOrder = -100;

            GameObject heroSlot = new GameObject("HeroSlot");
            heroSlot.transform.SetParent(world.Root.transform, false);
            heroSlot.transform.localPosition = new Vector3(-2.25f, -4.9f, 0f);
            world.HeroActor = heroSlot.transform;
            GameObject heroVisualObject = InstantiatePrefab(
                Phase01PlaceholderFactory.HeroPrefabPath,
                heroSlot.transform,
                "HeroVisual");
            world.HeroVisual = RequireComponent<SpriteRenderer>(
                heroVisualObject,
                Phase01PlaceholderFactory.HeroPrefabPath);
            world.HeroVisual.flipX = false;
            world.HeroProjectileOrigin = CreateMarker(
                "HeroProjectileOrigin",
                heroSlot.transform,
                new Vector3(0.75f, 1.55f, 0f));
            world.HeroHitTarget = CreateMarker(
                "HeroHitTarget",
                heroSlot.transform,
                new Vector3(0.10f, 1.45f, 0f));

            GameObject enemySlot = new GameObject("EnemySlot_" + definition.EncounterId);
            enemySlot.transform.SetParent(world.Root.transform, false);
            enemySlot.transform.localPosition = new Vector3(2.25f, -4.9f, 0f);
            world.EnemyActor = enemySlot.transform;
            GameObject enemyVisualObject = InstantiatePrefab(
                Phase01PlaceholderFactory.EnemyPrefabPath,
                enemySlot.transform,
                "EnemyVisual");
            world.EnemyVisual = RequireComponent<SpriteRenderer>(
                enemyVisualObject,
                Phase01PlaceholderFactory.EnemyPrefabPath);
            world.EnemyVisual.flipX = true;
            world.EnemyVisual.color = definition.EnemyTint;
            CreateEnemyIdentityDecoration(enemySlot.transform, world.EnemyVisual, definition);
            world.EnemyProjectileOrigin = CreateMarker(
                "EnemyProjectileOrigin",
                enemySlot.transform,
                new Vector3(-0.75f, 1.45f, 0f));
            world.EnemyHitTarget = CreateMarker(
                "EnemyHitTarget",
                enemySlot.transform,
                new Vector3(-0.10f, 1.35f, 0f));

            GameObject effects = new GameObject("PersistentEffects");
            effects.transform.SetParent(world.Root.transform, false);
            world.HeroBasicProjectile = InstantiatePrefab(
                Phase02PrototypeAssetFactory.HeroBasicProjectilePrefabPath,
                effects.transform,
                "HeroBasicProjectile");
            world.HeroTechniqueProjectile = InstantiatePrefab(
                Phase02PrototypeAssetFactory.HeroTechniqueProjectilePrefabPath,
                effects.transform,
                "HeroTechniqueProjectile");
            world.EnemyProjectile = InstantiatePrefab(
                Phase02PrototypeAssetFactory.EnemyProjectilePrefabPath,
                effects.transform,
                "EnemyProjectile");
            world.HeroGuardVisual = CreateGuardVisual(
                "HeroGuardVisual",
                heroSlot.transform,
                new Color(Phase02UiFactory.Cyan.r, Phase02UiFactory.Cyan.g, Phase02UiFactory.Cyan.b, 0.78f));
            world.EnemyGuardVisual = CreateGuardVisual(
                "EnemyGuardVisual",
                enemySlot.transform,
                new Color(definition.EnemyTint.r, definition.EnemyTint.g, definition.EnemyTint.b, 0.84f));
            world.EnemyChargeVisual = CreateGuardVisual(
                "EnemyChargeVisual",
                enemySlot.transform,
                new Color(Phase02UiFactory.Gold.r, Phase02UiFactory.Gold.g, Phase02UiFactory.Gold.b, 0.88f));
            world.EnemyChargeVisual.transform.localScale = Vector3.one * 1.18f;
            world.SavedVisual = CreateGuardVisual(
                "SavedPurificationVisual",
                enemySlot.transform,
                new Color(0.82f, 0.93f, 0.88f, 0.92f));
            world.SavedVisual.transform.localScale = Vector3.one * 1.35f;
            world.KilledVisual = CreateGuardVisual(
                "KilledFadeVisual",
                enemySlot.transform,
                new Color(0.34f, 0.30f, 0.38f, 0.70f));
            world.KilledVisual.transform.localScale = Vector3.one * 1.25f;
            return world;
        }

        private static void CreateEnemyIdentityDecoration(
            Transform enemySlot,
            SpriteRenderer source,
            EncounterDefinition definition)
        {
            GameObject decoration = new GameObject(
                definition.CampaignEncounter == CampaignEncounter.ThornGuardian
                    ? "ThornCrown_Persistent"
                    : "AshAura_Persistent",
                typeof(SpriteRenderer));
            decoration.transform.SetParent(enemySlot, false);
            SpriteRenderer renderer = decoration.GetComponent<SpriteRenderer>();
            renderer.sprite = source.sprite;
            renderer.flipX = source.flipX;

            if (definition.CampaignEncounter == CampaignEncounter.ThornGuardian)
            {
                decoration.transform.localPosition = new Vector3(0f, 2.10f, 0.02f);
                decoration.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                decoration.transform.localScale = new Vector3(0.32f, 0.16f, 1f);
                renderer.color = new Color(0.45f, 0.62f, 0.24f, 0.88f);
                renderer.sortingOrder = source.sortingOrder + 1;
            }
            else
            {
                decoration.transform.localPosition = new Vector3(0f, 1.25f, 0.04f);
                decoration.transform.localScale = new Vector3(1.18f, 1.12f, 1f);
                renderer.color = new Color(0.28f, 0.12f, 0.16f, 0.32f);
                renderer.sortingOrder = source.sortingOrder - 1;
            }
        }

        private static GameObject CreateGuardVisual(string name, Transform parent, Color tint)
        {
            GameObject visual = InstantiatePrefab(
                Phase02PrototypeAssetFactory.GuardRingPrefabPath,
                parent,
                name);
            visual.transform.localPosition = new Vector3(0f, 1.45f, 0f);
            foreach (SpriteRenderer renderer in visual.GetComponentsInChildren<SpriteRenderer>(true))
            {
                renderer.color = tint;
            }

            return visual;
        }

        private static BattleUi CreateBattleUi(
            Transform parent,
            TMP_FontAsset font,
            EncounterDefinition definition)
        {
            BattleUi ui = new BattleUi();
            ui.Root = new GameObject(UiRootName);
            ui.Root.transform.SetParent(parent, false);
            RectTransform canvas = Phase02UiFactory.CreateCanvas(ui.Root.transform);
            RectTransform safeArea = Phase02UiFactory.CreateSafeArea(canvas);

            HealthUi heroHealth = CreateHealthPanel(
                "HeroStatus",
                safeArea,
                font,
                "HERO01",
                "100 / 100",
                new Vector2(0.04f, 0.845f),
                new Vector2(0.49f, 0.955f),
                Phase02UiFactory.Cyan);
            ui.HeroHealthFill = heroHealth.Fill;
            ui.HeroHealthValue = heroHealth.Value;

            HealthUi enemyHealth = CreateHealthPanel(
                "EnemyStatus",
                safeArea,
                font,
                definition.EnemyDisplayName.ToUpperInvariant(),
                definition.EnemyMaxHp + " / " + definition.EnemyMaxHp,
                new Vector2(0.51f, 0.845f),
                new Vector2(0.96f, 0.955f),
                Phase02UiFactory.Corruption);
            ui.EnemyHealthFill = enemyHealth.Fill;
            ui.EnemyHealthValue = enemyHealth.Value;

            RectTransform statusPanel = Phase02UiFactory.CreatePanel(
                "StatusPanel",
                safeArea,
                new Vector2(0.04f, 0.745f),
                new Vector2(0.49f, 0.825f),
                Vector2.zero,
                Vector2.zero,
                new Color(0.08f, 0.25f, 0.24f, 0.95f));
            ui.StatusText = Phase02UiFactory.CreateText(
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
            ui.IntentText = Phase02UiFactory.CreateText(
                "TXT_Intent",
                intentPanel,
                "INTENZIONE\nIN OSSERVAZIONE",
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
                new Vector2(0.08f, 0.635f),
                new Vector2(0.92f, 0.705f),
                Vector2.zero,
                Vector2.zero,
                new Color(
                    Phase02UiFactory.Background.r,
                    Phase02UiFactory.Background.g,
                    Phase02UiFactory.Background.b,
                    0.88f));
            ui.CombatMessage = Phase02UiFactory.CreateText(
                "TXT_CombatMessage",
                messagePanel,
                "Leggi l'intenzione e scegli la tua azione",
                30f,
                Phase02UiFactory.Light,
                TextAlignmentOptions.Center,
                font,
                Vector2.zero,
                Vector2.one,
                new Vector2(16f, 6f),
                new Vector2(-16f, -6f),
                FontStyles.Bold);

            RectTransform dialoguePanel = Phase02UiFactory.CreatePanel(
                "EnemyDialogue",
                safeArea,
                new Vector2(0.35f, 0.485f),
                new Vector2(0.96f, 0.615f),
                Vector2.zero,
                Vector2.zero,
                new Color(0.16f, 0.09f, 0.17f, 0.93f));
            ui.EnemyDialogueRoot = dialoguePanel.gameObject;
            ui.EnemyDialogueText = Phase02UiFactory.CreateText(
                "TXT_EnemyDialogue",
                dialoguePanel,
                definition.OpeningDialogue,
                28f,
                Phase02UiFactory.MainText,
                TextAlignmentOptions.Center,
                font,
                Vector2.zero,
                Vector2.one,
                new Vector2(18f, 10f),
                new Vector2(-18f, -10f),
                FontStyles.Italic);

            ui.PredictionFeedbackText = Phase02UiFactory.CreateText(
                "TXT_PredictionFeedback",
                safeArea,
                string.Empty,
                24f,
                Phase02UiFactory.Gold,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.08f, 0.435f),
                new Vector2(0.92f, 0.48f),
                Vector2.zero,
                Vector2.zero,
                FontStyles.Bold);

            ui.BackButton = Phase02UiFactory.CreateButton(
                "BTN_BackToMenu",
                safeArea,
                "MENU",
                font,
                new Vector2(0.04f, 0.705f),
                new Vector2(0.29f, 0.74f),
                Vector2.zero,
                Vector2.zero);

            CreateActionBar(safeArea, ui);
            CreateAnalyzePanel(safeArea, font, ui, definition);
            CreateFinalChoicePanel(safeArea, font, ui, definition);
            CreateConfirmationPanel(safeArea, font, ui);
            CreateOutcomeOverlay(safeArea, font, ui);
            return ui;
        }

        private static HealthUi CreateHealthPanel(
            string name,
            RectTransform parent,
            TMP_FontAsset font,
            string label,
            string initialValue,
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
                27f,
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
                initialValue,
                24f,
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

        private static void CreateActionBar(RectTransform safeArea, BattleUi ui)
        {
            GameObject actionBarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase02SceneFactory.BattleActionBarPrefabPath);
            ui.ActionBar = PrefabUtility.InstantiatePrefab(actionBarPrefab, safeArea) as GameObject;
            if (ui.ActionBar == null)
            {
                throw new InvalidOperationException(
                    "Impossibile istanziare la barra azioni: " + Phase02SceneFactory.BattleActionBarPrefabPath);
            }

            ui.ActionBar.name = "ActionBar";
            RectTransform actionBarRect = RequireComponent<RectTransform>(
                ui.ActionBar,
                Phase02SceneFactory.BattleActionBarPrefabPath);
            Phase02UiFactory.SetRect(
                actionBarRect,
                new Vector2(0.025f, 0.025f),
                new Vector2(0.975f, 0.19f),
                Vector2.zero,
                Vector2.zero);

            ui.AttackButton = FindRequiredComponentInChildren<Button>(ui.ActionBar.transform, "BTN_Attack");
            ui.GuardButton = FindRequiredComponentInChildren<Button>(ui.ActionBar.transform, "BTN_Guard");
            ui.TechniqueButton = FindRequiredComponentInChildren<Button>(ui.ActionBar.transform, "BTN_Technique");
            ui.AnalyzeButton = FindRequiredComponentInChildren<Button>(ui.ActionBar.transform, "BTN_Mark");
            ui.AnalyzeButton.gameObject.name = "BTN_Analyze";
            TMP_Text analyzeLabel = ui.AnalyzeButton.GetComponentInChildren<TMP_Text>(true);
            if (analyzeLabel == null)
            {
                throw new InvalidOperationException("Testo del pulsante ANALIZZA mancante.");
            }

            analyzeLabel.text = "ANALIZZA";
            ui.TechniqueButtonLabel = ui.TechniqueButton.GetComponentInChildren<TMP_Text>(true);
            if (ui.TechniqueButtonLabel == null)
            {
                throw new InvalidOperationException("Testo del pulsante TECNICA mancante.");
            }
        }

        private static void CreateAnalyzePanel(
            RectTransform safeArea,
            TMP_FontAsset font,
            BattleUi ui,
            EncounterDefinition definition)
        {
            RectTransform overlay = Phase02UiFactory.CreateRect("AnalyzePanel", safeArea);
            Phase02UiFactory.SetRect(overlay, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            ui.AnalyzePanel = overlay.gameObject;

            Phase02UiFactory.CreatePanel(
                "Dimmer",
                overlay,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                new Color(0.02f, 0.05f, 0.045f, 0.90f),
                true);

            RectTransform card = Phase02UiFactory.CreatePanel(
                "EnemyInfoCard",
                overlay,
                new Vector2(0.055f, 0.19f),
                new Vector2(0.945f, 0.82f),
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
                new Vector2(0.07f, 0.87f),
                new Vector2(0.93f, 0.97f),
                Vector2.zero,
                Vector2.zero,
                FontStyles.Bold);
            ui.AnalyzeNameText = CreateAnalyzeValue(
                "TXT_EnemyInfoName",
                card,
                "NOME\n" + definition.EnemyDisplayName,
                font,
                new Vector2(0.08f, 0.73f),
                new Vector2(0.92f, 0.87f),
                Phase02UiFactory.MainText);
            ui.AnalyzeRaceText = CreateAnalyzeValue(
                "TXT_EnemyInfoRace",
                card,
                "RAZZA\n" + definition.EnemyRace,
                font,
                new Vector2(0.08f, 0.58f),
                new Vector2(0.49f, 0.73f),
                Phase02UiFactory.Light);
            ui.AnalyzeCorruptionText = CreateAnalyzeValue(
                "TXT_EnemyInfoCorruption",
                card,
                "CORRUZIONE\n" + definition.EnemyCorruptionPercent + "%",
                font,
                new Vector2(0.51f, 0.58f),
                new Vector2(0.92f, 0.73f),
                Phase02UiFactory.Corruption);
            ui.AnalyzeMoodText = CreateAnalyzeValue(
                "TXT_EnemyInfoMood",
                card,
                "STATO ATTUALE\n" + definition.InitialMood,
                font,
                new Vector2(0.08f, 0.44f),
                new Vector2(0.92f, 0.58f),
                Phase02UiFactory.MainText);
            ui.AnalyzeTendencyText = CreateAnalyzeValue(
                "TXT_EnemyInfoTendency",
                card,
                "TENDENZA\n" + definition.Tendency,
                font,
                new Vector2(0.08f, 0.25f),
                new Vector2(0.92f, 0.44f),
                Phase02UiFactory.SecondaryText);
            ui.AnalyzeIntentText = CreateAnalyzeValue(
                "TXT_EnemyInfoIntent",
                card,
                "MOSSA ANNUNCIATA\nIn osservazione",
                font,
                new Vector2(0.08f, 0.13f),
                new Vector2(0.92f, 0.25f),
                Phase02UiFactory.Gold);
            ui.AnalyzeCloseButton = Phase02UiFactory.CreateButton(
                "BTN_CloseAnalyze",
                card,
                "CHIUDI",
                font,
                new Vector2(0.18f, 0.025f),
                new Vector2(0.82f, 0.125f),
                Vector2.zero,
                Vector2.zero,
                true);
        }

        private static TMP_Text CreateAnalyzeValue(
            string name,
            RectTransform parent,
            string content,
            TMP_FontAsset font,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color color)
        {
            return Phase02UiFactory.CreateText(
                name,
                parent,
                content,
                27f,
                color,
                TextAlignmentOptions.Center,
                font,
                anchorMin,
                anchorMax,
                Vector2.zero,
                Vector2.zero,
                FontStyles.Bold);
        }

        private static void CreateFinalChoicePanel(
            RectTransform safeArea,
            TMP_FontAsset font,
            BattleUi ui,
            EncounterDefinition definition)
        {
            RectTransform overlay = Phase02UiFactory.CreatePanel(
                "FinalChoicePanel",
                safeArea,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                new Color(0.04f, 0.06f, 0.06f, 0.92f),
                true);
            ui.FinalChoicePanel = overlay.gameObject;

            RectTransform card = Phase02UiFactory.CreatePanel(
                "FinalChoiceCard",
                overlay,
                new Vector2(0.07f, 0.28f),
                new Vector2(0.93f, 0.72f),
                Vector2.zero,
                Vector2.zero,
                Phase02UiFactory.Panel);
            ui.FinalChoiceTitleText = Phase02UiFactory.CreateText(
                "TXT_FinalChoiceTitle",
                card,
                "SCELTA FINALE",
                54f,
                Phase02UiFactory.Gold,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.07f, 0.72f),
                new Vector2(0.93f, 0.94f),
                Vector2.zero,
                Vector2.zero,
                FontStyles.Bold);
            ui.FinalChoiceDialogueText = Phase02UiFactory.CreateText(
                "TXT_FinalChoiceDialogue",
                card,
                definition.DefeatedDialogue,
                31f,
                Phase02UiFactory.MainText,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.09f, 0.39f),
                new Vector2(0.91f, 0.70f),
                Vector2.zero,
                Vector2.zero,
                FontStyles.Italic);
            ui.SaveButton = Phase02UiFactory.CreateButton(
                "BTN_SaveEnemy",
                card,
                "SALVA",
                font,
                new Vector2(0.08f, 0.09f),
                new Vector2(0.47f, 0.33f),
                Vector2.zero,
                Vector2.zero,
                true);
            ui.KillButton = Phase02UiFactory.CreateButton(
                "BTN_KillEnemy",
                card,
                "UCCIDI",
                font,
                new Vector2(0.53f, 0.09f),
                new Vector2(0.92f, 0.33f),
                Vector2.zero,
                Vector2.zero,
                true);
        }

        private static void CreateConfirmationPanel(
            RectTransform safeArea,
            TMP_FontAsset font,
            BattleUi ui)
        {
            RectTransform overlay = Phase02UiFactory.CreatePanel(
                "ConfirmationPanel",
                safeArea,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                new Color(0.02f, 0.04f, 0.04f, 0.95f),
                true);
            ui.ConfirmationPanel = overlay.gameObject;

            RectTransform card = Phase02UiFactory.CreatePanel(
                "ConfirmationCard",
                overlay,
                new Vector2(0.07f, 0.32f),
                new Vector2(0.93f, 0.68f),
                Vector2.zero,
                Vector2.zero,
                Phase02UiFactory.Panel);
            ui.ConfirmationText = Phase02UiFactory.CreateText(
                "TXT_Confirmation",
                card,
                "Confermare questa scelta?",
                36f,
                Phase02UiFactory.MainText,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.08f, 0.42f),
                new Vector2(0.92f, 0.88f),
                Vector2.zero,
                Vector2.zero,
                FontStyles.Bold);
            ui.ConfirmationConfirmButton = Phase02UiFactory.CreateButton(
                "BTN_ConfirmChoice",
                card,
                "CONFERMA",
                font,
                new Vector2(0.08f, 0.08f),
                new Vector2(0.54f, 0.34f),
                Vector2.zero,
                Vector2.zero,
                true);
            ui.ConfirmationBackButton = Phase02UiFactory.CreateButton(
                "BTN_BackChoice",
                card,
                "INDIETRO",
                font,
                new Vector2(0.58f, 0.08f),
                new Vector2(0.92f, 0.34f),
                Vector2.zero,
                Vector2.zero,
                true);
        }

        private static void CreateOutcomeOverlay(
            RectTransform safeArea,
            TMP_FontAsset font,
            BattleUi ui)
        {
            RectTransform overlay = Phase02UiFactory.CreatePanel(
                "OutcomeOverlay",
                safeArea,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                new Color(
                    Phase02UiFactory.Background.r,
                    Phase02UiFactory.Background.g,
                    Phase02UiFactory.Background.b,
                    0.94f),
                true);
            ui.OutcomeOverlay = overlay.gameObject;

            RectTransform card = Phase02UiFactory.CreatePanel(
                "OutcomeCard",
                overlay,
                new Vector2(0.07f, 0.28f),
                new Vector2(0.93f, 0.72f),
                Vector2.zero,
                Vector2.zero,
                Phase02UiFactory.Panel);
            ui.OutcomeText = Phase02UiFactory.CreateText(
                "TXT_Outcome",
                card,
                "SCONFITTA",
                65f,
                Phase02UiFactory.MainText,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.05f, 0.65f),
                new Vector2(0.95f, 0.90f),
                Vector2.zero,
                Vector2.zero,
                FontStyles.Bold);
            ui.OutcomeDialogueText = Phase02UiFactory.CreateText(
                "TXT_OutcomeDialogue",
                card,
                string.Empty,
                30f,
                Phase02UiFactory.SecondaryText,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.08f, 0.32f),
                new Vector2(0.92f, 0.64f),
                Vector2.zero,
                Vector2.zero,
                FontStyles.Italic);
            ui.OutcomeMenuButton = Phase02UiFactory.CreateButton(
                "BTN_OutcomeMenu",
                card,
                "TORNA AL MENU",
                font,
                new Vector2(0.12f, 0.07f),
                new Vector2(0.88f, 0.27f),
                Vector2.zero,
                Vector2.zero,
                true);
        }

        private static void AssignControllerReferences(
            EncounterBattleController controller,
            EncounterBattleNavigation navigation,
            BattleWorld world,
            BattleUi ui,
            EncounterDefinition definition)
        {
            SerializedObject serialized = new SerializedObject(controller);
            serialized.Update();

            SetEnumByName(serialized, "campaignEncounter", definition.CampaignEncounter.ToString());
            SetString(serialized, "encounterId", definition.EncounterId);
            SetString(serialized, "enemyDisplayName", definition.EnemyDisplayName);
            SetString(serialized, "enemyRace", definition.EnemyRace);
            SetInt(serialized, "enemyCorruptionPercent", definition.EnemyCorruptionPercent);
            SetEnumByName(serialized, "enemyInitialMood", definition.InitialMood.ToString());
            SetInt(serialized, "enemyIntelligenceLevel", definition.IntelligenceLevel);
            SetInt(serialized, "enemyRandomSeed", definition.RandomSeed);

            SetInt(serialized, "heroMaxHp", 100);
            SetInt(serialized, "enemyMaxHp", definition.EnemyMaxHp);
            SetInt(serialized, "attackDamage", 20);
            SetInt(serialized, "techniqueDamage", 32);
            SetInt(serialized, "enemyAttackDamage", definition.EnemyAttackDamage);
            SetInt(serialized, "chargedStrikeDamage", definition.ChargedStrikeDamage);
            SetInt(serialized, "techniqueCooldownTurns", 2);
            SetInt(serialized, "enemyGuardReductionPercent", 65);
            SetFloat(serialized, "resultReturnDelay", 2.5f);

            SetString(serialized, "openingDialogue", definition.OpeningDialogue);
            SetString(serialized, "attackReactionDialogue", definition.AttackReactionDialogue);
            SetString(serialized, "guardReactionDialogue", definition.GuardReactionDialogue);
            SetString(serialized, "techniqueReactionDialogue", definition.TechniqueReactionDialogue);
            SetString(serialized, "firstAnalyzeDialogue", definition.FirstAnalyzeDialogue);
            SetString(serialized, "repeatedAnalyzeDialogue", definition.RepeatedAnalyzeDialogue);
            SetString(serialized, "lowHpDialogue", definition.LowHpDialogue);
            SetString(serialized, "attackPatternDialogue", definition.AttackPatternDialogue);
            SetString(serialized, "guardPatternDialogue", definition.GuardPatternDialogue);
            SetString(serialized, "techniquePatternDialogue", definition.TechniquePatternDialogue);
            SetString(serialized, "strategyChangedDialogue", definition.StrategyChangedDialogue);
            SetString(serialized, "defeatedDialogue", definition.DefeatedDialogue);
            SetString(serialized, "savedDialogue", definition.SavedDialogue);
            SetString(serialized, "killedDialogue", definition.KilledDialogue);

            SetObject(serialized, "attackButton", ui.AttackButton);
            SetObject(serialized, "guardButton", ui.GuardButton);
            SetObject(serialized, "techniqueButton", ui.TechniqueButton);
            SetObject(serialized, "analyzeButton", ui.AnalyzeButton);
            SetObject(serialized, "techniqueButtonLabel", ui.TechniqueButtonLabel);

            SetObject(serialized, "combatMessage", ui.CombatMessage);
            SetObject(serialized, "intentText", ui.IntentText);
            SetObject(serialized, "statusText", ui.StatusText);
            SetObject(serialized, "predictionFeedbackText", ui.PredictionFeedbackText);
            SetObject(serialized, "heroHealthFill", ui.HeroHealthFill);
            SetObject(serialized, "enemyHealthFill", ui.EnemyHealthFill);
            SetObject(serialized, "heroHealthValue", ui.HeroHealthValue);
            SetObject(serialized, "enemyHealthValue", ui.EnemyHealthValue);

            SetObject(serialized, "enemyDialogueRoot", ui.EnemyDialogueRoot);
            SetObject(serialized, "enemyDialogueText", ui.EnemyDialogueText);
            SetObject(serialized, "heroActor", world.HeroActor);
            SetObject(serialized, "enemyActor", world.EnemyActor);
            SetObject(serialized, "heroVisual", world.HeroVisual);
            SetObject(serialized, "enemyVisual", world.EnemyVisual);
            SetObject(serialized, "heroProjectileOrigin", world.HeroProjectileOrigin);
            SetObject(serialized, "heroHitTarget", world.HeroHitTarget);
            SetObject(serialized, "enemyProjectileOrigin", world.EnemyProjectileOrigin);
            SetObject(serialized, "enemyHitTarget", world.EnemyHitTarget);

            SetObject(serialized, "heroBasicProjectile", world.HeroBasicProjectile);
            SetObject(serialized, "heroTechniqueProjectile", world.HeroTechniqueProjectile);
            SetObject(serialized, "enemyProjectile", world.EnemyProjectile);
            SetObject(serialized, "heroGuardVisual", world.HeroGuardVisual);
            SetObject(serialized, "enemyGuardVisual", world.EnemyGuardVisual);
            SetObject(serialized, "enemyChargeVisual", world.EnemyChargeVisual);
            SetObject(serialized, "savedVisual", world.SavedVisual);
            SetObject(serialized, "killedVisual", world.KilledVisual);

            SetObject(serialized, "analyzePanel", ui.AnalyzePanel);
            SetObject(serialized, "analyzeNameText", ui.AnalyzeNameText);
            SetObject(serialized, "analyzeRaceText", ui.AnalyzeRaceText);
            SetObject(serialized, "analyzeCorruptionText", ui.AnalyzeCorruptionText);
            SetObject(serialized, "analyzeMoodText", ui.AnalyzeMoodText);
            SetObject(serialized, "analyzeTendencyText", ui.AnalyzeTendencyText);
            SetObject(serialized, "analyzeIntentText", ui.AnalyzeIntentText);
            SetObject(serialized, "analyzeCloseButton", ui.AnalyzeCloseButton);

            SetObject(serialized, "finalChoicePanel", ui.FinalChoicePanel);
            SetObject(serialized, "finalChoiceTitleText", ui.FinalChoiceTitleText);
            SetObject(serialized, "finalChoiceDialogueText", ui.FinalChoiceDialogueText);
            SetObject(serialized, "saveButton", ui.SaveButton);
            SetObject(serialized, "killButton", ui.KillButton);
            SetObject(serialized, "confirmationPanel", ui.ConfirmationPanel);
            SetObject(serialized, "confirmationText", ui.ConfirmationText);
            SetObject(serialized, "confirmationConfirmButton", ui.ConfirmationConfirmButton);
            SetObject(serialized, "confirmationBackButton", ui.ConfirmationBackButton);
            SetObject(serialized, "outcomeOverlay", ui.OutcomeOverlay);
            SetObject(serialized, "outcomeText", ui.OutcomeText);
            SetObject(serialized, "outcomeDialogueText", ui.OutcomeDialogueText);
            SetObject(serialized, "outcomeMenuButton", ui.OutcomeMenuButton);
            SetObject(serialized, "navigation", navigation);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignNavigationReferences(
            EncounterBattleNavigation navigation,
            EncounterBattleController controller,
            BattleUi ui)
        {
            SerializedObject serialized = new SerializedObject(navigation);
            serialized.Update();
            SetObject(serialized, "backButton", ui.BackButton);
            SetObject(serialized, "resultMenuButton", ui.OutcomeMenuButton);
            SetObject(serialized, "battleController", controller);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddPersistentListeners(
            EncounterBattleController controller,
            EncounterBattleNavigation navigation,
            BattleUi ui)
        {
            UnityEventTools.AddPersistentListener(ui.AttackButton.onClick, controller.ChooseAttack);
            UnityEventTools.AddPersistentListener(ui.GuardButton.onClick, controller.ChooseGuard);
            UnityEventTools.AddPersistentListener(ui.TechniqueButton.onClick, controller.ChooseTechnique);
            UnityEventTools.AddPersistentListener(ui.AnalyzeButton.onClick, controller.OpenAnalyze);
            UnityEventTools.AddPersistentListener(ui.AnalyzeCloseButton.onClick, controller.CloseAnalyze);
            UnityEventTools.AddPersistentListener(ui.SaveButton.onClick, controller.ChooseSave);
            UnityEventTools.AddPersistentListener(ui.KillButton.onClick, controller.ChooseKill);
            UnityEventTools.AddPersistentListener(
                ui.ConfirmationConfirmButton.onClick,
                controller.ConfirmFinalChoice);
            UnityEventTools.AddPersistentListener(
                ui.ConfirmationBackButton.onClick,
                controller.BackFromFinalConfirmation);
            UnityEventTools.AddPersistentListener(ui.BackButton.onClick, navigation.BackToMenu);
            UnityEventTools.AddPersistentListener(ui.OutcomeMenuButton.onClick, controller.ReturnToMenu);
        }

        private static void SetInitialActiveState(BattleWorld world, BattleUi ui)
        {
            world.HeroBasicProjectile.SetActive(false);
            world.HeroTechniqueProjectile.SetActive(false);
            world.EnemyProjectile.SetActive(false);
            world.HeroGuardVisual.SetActive(false);
            world.EnemyGuardVisual.SetActive(false);
            world.EnemyChargeVisual.SetActive(false);
            world.SavedVisual.SetActive(false);
            world.KilledVisual.SetActive(false);
            ui.AnalyzePanel.SetActive(false);
            ui.FinalChoicePanel.SetActive(false);
            ui.ConfirmationPanel.SetActive(false);
            ui.OutcomeOverlay.SetActive(false);
        }

        private static void CreateOrUpdateMainMenu(Phase046CampaignSetupReport report)
        {
            Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            MainMenuController[] controllers = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true))
                .ToArray();
            if (controllers.Length != 1)
            {
                throw new InvalidOperationException(
                    "SCN_MainMenu deve contenere esattamente un MainMenuController; trovati " + controllers.Length + ".");
            }

            MainMenuController controller = controllers[0];
            Button startButton = FindRequiredComponentInScene<Button>(scene, "BTN_Start");
            TMP_Text startLabel = startButton.GetComponentInChildren<TMP_Text>(true);
            if (startLabel == null)
            {
                throw new InvalidOperationException("BTN_Start non contiene un testo.");
            }

            RectTransform safeArea = FindRequiredComponentInScene<RectTransform>(scene, "SafeArea");
            Transform previous = FindDirectChild(safeArea, CampaignControlsName);
            if (previous != null)
            {
                UnityEngine.Object.DestroyImmediate(previous.gameObject);
                report.Configure(CampaignControlsName + " (menu rigenerato senza duplicati)");
            }

            TMP_FontAsset font = Phase02UiFactory.LoadRequiredFont();
            RectTransform controls = Phase02UiFactory.CreateRect(CampaignControlsName, safeArea);
            Phase02UiFactory.SetRect(controls, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            RectTransform statusPanel = Phase02UiFactory.CreatePanel(
                "CampaignStatusPanel",
                controls,
                new Vector2(0.08f, 0.695f),
                new Vector2(0.92f, 0.785f),
                Vector2.zero,
                Vector2.zero,
                new Color(0.08f, 0.20f, 0.18f, 0.95f));
            TMP_Text campaignStatusText = Phase02UiFactory.CreateText(
                "TXT_CampaignStatus",
                statusPanel,
                "Nuova partita: il tutorial ti aspetta.",
                25f,
                Phase02UiFactory.Light,
                TextAlignmentOptions.Center,
                font,
                Vector2.zero,
                Vector2.one,
                new Vector2(14f, 7f),
                new Vector2(-14f, -7f),
                FontStyles.Bold);

            Button replayButton = Phase02UiFactory.CreateButton(
                "BTN_ReplayTutorial",
                controls,
                "RIGIOCA TUTORIAL",
                font,
                new Vector2(0.08f, 0.045f),
                new Vector2(0.48f, 0.11f),
                Vector2.zero,
                Vector2.zero,
                true);
            Button resetButton = Phase02UiFactory.CreateButton(
                "BTN_ResetCampaign",
                controls,
                "AZZERA PROGRESSI",
                font,
                new Vector2(0.52f, 0.045f),
                new Vector2(0.92f, 0.11f),
                Vector2.zero,
                Vector2.zero,
                true);

            RectTransform resetModal = Phase02UiFactory.CreatePanel(
                "ResetProgressConfirmation",
                controls,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                new Color(0.02f, 0.04f, 0.04f, 0.95f),
                true);
            RectTransform resetCard = Phase02UiFactory.CreatePanel(
                "ResetProgressCard",
                resetModal,
                new Vector2(0.08f, 0.35f),
                new Vector2(0.92f, 0.65f),
                Vector2.zero,
                Vector2.zero,
                Phase02UiFactory.Panel);
            Phase02UiFactory.CreateText(
                "TXT_ResetProgressQuestion",
                resetCard,
                "AZZERARE I PROGRESSI?\nTutorial e scelte verranno dimenticati.",
                34f,
                Phase02UiFactory.MainText,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.08f, 0.42f),
                new Vector2(0.92f, 0.90f),
                Vector2.zero,
                Vector2.zero,
                FontStyles.Bold);
            Button confirmReset = Phase02UiFactory.CreateButton(
                "BTN_ConfirmResetCampaign",
                resetCard,
                "CONFERMA",
                font,
                new Vector2(0.08f, 0.08f),
                new Vector2(0.54f, 0.34f),
                Vector2.zero,
                Vector2.zero,
                true);
            Button cancelReset = Phase02UiFactory.CreateButton(
                "BTN_CancelResetCampaign",
                resetCard,
                "INDIETRO",
                font,
                new Vector2(0.58f, 0.08f),
                new Vector2(0.92f, 0.34f),
                Vector2.zero,
                Vector2.zero,
                true);

            SerializedObject serialized = new SerializedObject(controller);
            serialized.Update();
            SetObject(serialized, "startButtonLabel", startLabel);
            SetObject(serialized, "campaignStatusText", campaignStatusText);
            SetObject(serialized, "replayTutorialButton", replayButton);
            SetObject(serialized, "resetProgressButton", resetButton);
            SetObject(serialized, "resetProgressConfirmationModal", resetModal.gameObject);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            UnityEventTools.AddPersistentListener(replayButton.onClick, controller.ReplayTutorial);
            UnityEventTools.AddPersistentListener(resetButton.onClick, controller.OpenResetProgressConfirmation);
            UnityEventTools.AddPersistentListener(confirmReset.onClick, controller.ConfirmResetProgress);
            UnityEventTools.AddPersistentListener(cancelReset.onClick, controller.CloseResetProgressConfirmation);
            resetModal.gameObject.SetActive(false);

            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, MainMenuScenePath))
            {
                throw new InvalidOperationException("Impossibile salvare il menu aggiornato.");
            }

            report.Configure("SCN_MainMenu (Continua, rigioca tutorial, riepilogo e reset confermato)");
        }

        private static void EnsureCamera(Transform sceneRoot, Phase046CampaignSetupReport report)
        {
            Camera existing = sceneRoot.GetComponentInChildren<Camera>(true);
            if (existing != null)
            {
                report.Preserve(sceneRoot.name + "/Main Camera");
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
            report.Create(sceneRoot.name + "/Main Camera");
        }

        private static void EnsureEventSystem(Transform sceneRoot, Phase046CampaignSetupReport report)
        {
            EventSystem existing = sceneRoot.GetComponentInChildren<EventSystem>(true);
            if (existing != null)
            {
                report.Preserve(sceneRoot.name + "/EventSystem");
                return;
            }

            Phase02UiFactory.CreateEventSystem(sceneRoot);
            report.Create(sceneRoot.name + "/EventSystem");
        }

        private static void ConfigureBuildSettings(Phase046CampaignSetupReport report)
        {
            string[] campaignOrder =
            {
                MainMenuScenePath,
                TutorialScenePath,
                Level02ScenePath,
                Level03ScenePath
            };
            List<EditorBuildSettingsScene> ordered = campaignOrder
                .Select(path => new EditorBuildSettingsScene(path, true))
                .ToList();

            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                if (campaignOrder.Contains(existing.path) || ordered.Any(scene => scene.path == existing.path))
                {
                    continue;
                }

                ordered.Add(existing);
            }

            EditorBuildSettings.scenes = ordered.ToArray();
            report.Configure("Build order: MainMenu, Tutorial, Custode del Rovo, Vigile delle Ceneri, scene preesistenti");
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

        private static T FindRequiredComponentInScene<T>(Scene scene, string objectName) where T : Component
        {
            T[] matches = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .Where(candidate => candidate.gameObject.name == objectName)
                .ToArray();
            if (matches.Length != 1)
            {
                throw new InvalidOperationException(
                    "La scena " + scene.name + " deve contenere esattamente un " + objectName +
                    " con componente " + typeof(T).Name + "; trovati " + matches.Length + ".");
            }

            return matches[0];
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
            RequireProperty(serialized, propertyName).objectReferenceValue = value;
        }

        private static void SetInt(SerializedObject serialized, string propertyName, int value)
        {
            RequireProperty(serialized, propertyName).intValue = value;
        }

        private static void SetFloat(SerializedObject serialized, string propertyName, float value)
        {
            RequireProperty(serialized, propertyName).floatValue = value;
        }

        private static void SetString(SerializedObject serialized, string propertyName, string value)
        {
            RequireProperty(serialized, propertyName).stringValue = value;
        }

        private static void SetEnumByName(SerializedObject serialized, string propertyName, string valueName)
        {
            SerializedProperty property = RequireProperty(serialized, propertyName);
            if (property.propertyType != SerializedPropertyType.Enum)
            {
                throw new InvalidOperationException(
                    serialized.targetObject.GetType().Name + "." + propertyName + " deve essere un enum.");
            }

            int index = Array.IndexOf(property.enumNames, valueName);
            if (index < 0)
            {
                throw new InvalidOperationException(
                    "Valore " + valueName + " non disponibile per " +
                    serialized.targetObject.GetType().Name + "." + propertyName + ".");
            }

            property.enumValueIndex = index;
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

        private sealed class EncounterDefinition
        {
            internal string ScenePath;
            internal string SceneRootName;
            internal CampaignEncounter CampaignEncounter;
            internal string EncounterId;
            internal string EnemyDisplayName;
            internal string EnemyRace;
            internal int EnemyCorruptionPercent;
            internal EnemyMood InitialMood;
            internal int IntelligenceLevel;
            internal int RandomSeed;
            internal int EnemyMaxHp;
            internal int EnemyAttackDamage;
            internal int ChargedStrikeDamage;
            internal Color EnemyTint;
            internal string OpeningDialogue;
            internal string AttackReactionDialogue;
            internal string GuardReactionDialogue;
            internal string TechniqueReactionDialogue;
            internal string FirstAnalyzeDialogue;
            internal string RepeatedAnalyzeDialogue;
            internal string LowHpDialogue;
            internal string AttackPatternDialogue;
            internal string GuardPatternDialogue;
            internal string TechniquePatternDialogue;
            internal string StrategyChangedDialogue;
            internal string DefeatedDialogue;
            internal string SavedDialogue;
            internal string KilledDialogue;
            internal string Tendency;
        }

        private sealed class BattleWorld
        {
            internal GameObject Root;
            internal Transform HeroActor;
            internal Transform EnemyActor;
            internal SpriteRenderer HeroVisual;
            internal SpriteRenderer EnemyVisual;
            internal Transform HeroProjectileOrigin;
            internal Transform HeroHitTarget;
            internal Transform EnemyProjectileOrigin;
            internal Transform EnemyHitTarget;
            internal GameObject HeroBasicProjectile;
            internal GameObject HeroTechniqueProjectile;
            internal GameObject EnemyProjectile;
            internal GameObject HeroGuardVisual;
            internal GameObject EnemyGuardVisual;
            internal GameObject EnemyChargeVisual;
            internal GameObject SavedVisual;
            internal GameObject KilledVisual;
        }

        private sealed class BattleUi
        {
            internal GameObject Root;
            internal GameObject ActionBar;
            internal Button AttackButton;
            internal Button GuardButton;
            internal Button TechniqueButton;
            internal Button AnalyzeButton;
            internal TMP_Text TechniqueButtonLabel;
            internal Button BackButton;
            internal TMP_Text CombatMessage;
            internal TMP_Text IntentText;
            internal TMP_Text StatusText;
            internal TMP_Text PredictionFeedbackText;
            internal Image HeroHealthFill;
            internal Image EnemyHealthFill;
            internal TMP_Text HeroHealthValue;
            internal TMP_Text EnemyHealthValue;
            internal GameObject EnemyDialogueRoot;
            internal TMP_Text EnemyDialogueText;
            internal GameObject AnalyzePanel;
            internal TMP_Text AnalyzeNameText;
            internal TMP_Text AnalyzeRaceText;
            internal TMP_Text AnalyzeCorruptionText;
            internal TMP_Text AnalyzeMoodText;
            internal TMP_Text AnalyzeTendencyText;
            internal TMP_Text AnalyzeIntentText;
            internal Button AnalyzeCloseButton;
            internal GameObject FinalChoicePanel;
            internal TMP_Text FinalChoiceTitleText;
            internal TMP_Text FinalChoiceDialogueText;
            internal Button SaveButton;
            internal Button KillButton;
            internal GameObject ConfirmationPanel;
            internal TMP_Text ConfirmationText;
            internal Button ConfirmationConfirmButton;
            internal Button ConfirmationBackButton;
            internal GameObject OutcomeOverlay;
            internal TMP_Text OutcomeText;
            internal TMP_Text OutcomeDialogueText;
            internal Button OutcomeMenuButton;
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
    }
}
#endif
