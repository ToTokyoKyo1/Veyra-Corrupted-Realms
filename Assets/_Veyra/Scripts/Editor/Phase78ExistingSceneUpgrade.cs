#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Veyra.Combat.Encounter;
using Veyra.Combat.Tutorial;
using Veyra.Core;

namespace Veyra.Editor
{
    internal static class Phase78ExistingSceneUpgrade
    {
        private const string ProgressionControlsName = "ProgressionOutcomeControls";
        private const string SavedAllyConsequencesName = "SavedAllyConsequences";
        private const string AllyDialogueRootName = "AllyDialogueRoot";
        private const string BattlePhaseName = "TXT_BattlePhase";
        private const string TutorialMoralChoiceName = "TutorialMoralChoice";

        internal static void UpgradeTutorialAndEncounters()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "L'upgrade delle scene esistenti può essere eseguito soltanto in Edit Mode.");
            }

            TMP_FontAsset font = Phase02UiFactory.LoadRequiredFont();
            UpgradeTutorial(font);
            UpgradeEncounter(Phase046EncounterSceneFactory.Level02ScenePath, false, font);
            UpgradeEncounter(Phase046EncounterSceneFactory.Level03ScenePath, true, font);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Completes the standalone Phase 03 generator with the consolidated
        /// tutorial controls and moral flow required by the current runtime.
        /// It deliberately touches only Level 1.
        /// </summary>
        internal static void UpgradeTutorialOnly()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "L'upgrade del Tutorial può essere eseguito soltanto in Edit Mode.");
            }

            UpgradeTutorial(Phase02UiFactory.LoadRequiredFont());
            AssetDatabase.SaveAssets();
        }

        private static void UpgradeTutorial(TMP_FontAsset font)
        {
            Scene scene = OpenRequiredScene(Phase046EncounterSceneFactory.TutorialScenePath);
            TutorialBattleController controller = RequireSingleComponent<TutorialBattleController>(scene);
            TutorialBattleNavigation navigation = RequireSingleComponent<TutorialBattleNavigation>(scene);

            SerializedObject controllerSerialized = new SerializedObject(controller);
            controllerSerialized.Update();
            GameObject outcomeOverlay = RequireObjectReference<GameObject>(
                controllerSerialized,
                "outcomeOverlay");
            Button menuButton = RequireObjectReference<Button>(
                controllerSerialized,
                "outcomeMenuButton");
            RectTransform outcomeCard = RequireNamedRect(outcomeOverlay.transform, "OutcomeCard");
            GameObject uiRoot = RequireNamedObject(scene, "TutorialUIRoot");
            RectTransform safeArea = RequireNamedRect(uiRoot.transform, "SafeArea");

            TMP_Text phaseText = RebuildBattlePhase(safeArea, font);
            TutorialOverlayControls tutorialControls = RebuildTutorialControls(safeArea, font);
            TutorialMoralUi moral = RebuildTutorialMoralChoice(safeArea, font);

            OutcomeControls controls = RebuildProgressionControls(
                outcomeCard,
                menuButton,
                font,
                "COMPLETA IL TUTORIAL PER OTTENERE " +
                CampaignLevelCatalog.GetByNumber(1).ExperienceReward + " XP",
                "LIVELLO 2",
                true);
            UnityEventTools.AddPersistentListener(
                controls.ContinueButton.onClick,
                navigation.ContinueToLevel02);
            UnityEventTools.AddPersistentListener(
                controls.RetryButton.onClick,
                navigation.RetryCurrentLevel);
            UnityEventTools.AddPersistentListener(
                tutorialControls.RepeatButton.onClick,
                controller.RepeatCurrentExplanation);
            UnityEventTools.AddPersistentListener(
                tutorialControls.SkipButton.onClick,
                controller.SkipTutorialExplanations);
            UnityEventTools.AddPersistentListener(moral.SaveButton.onClick, controller.ChooseSave);
            UnityEventTools.AddPersistentListener(moral.KillButton.onClick, controller.ChooseKill);
            UnityEventTools.AddPersistentListener(
                moral.ConfirmButton.onClick,
                controller.ConfirmFinalChoice);
            UnityEventTools.AddPersistentListener(
                moral.BackButton.onClick,
                controller.BackFromFinalConfirmation);

            SetObject(controllerSerialized, "phaseText", phaseText);
            SetObject(controllerSerialized, "tutorialRepeatButton", tutorialControls.RepeatButton);
            SetObject(controllerSerialized, "tutorialSkipButton", tutorialControls.SkipButton);
            SetObject(controllerSerialized, "finalChoicePanel", moral.ChoiceRoot);
            SetObject(controllerSerialized, "finalChoiceTitleText", moral.TitleText);
            SetObject(controllerSerialized, "finalChoicePortrait", moral.Portrait);
            SetObject(controllerSerialized, "finalChoiceProfileText", moral.ProfileText);
            SetObject(controllerSerialized, "finalChoiceDialogueText", moral.DialogueText);
            SetObject(controllerSerialized, "saveButton", moral.SaveButton);
            SetObject(controllerSerialized, "killButton", moral.KillButton);
            SetObject(controllerSerialized, "confirmationPanel", moral.ConfirmationRoot);
            SetObject(controllerSerialized, "confirmationText", moral.ConfirmationText);
            SetObject(controllerSerialized, "confirmationConfirmButton", moral.ConfirmButton);
            SetObject(controllerSerialized, "confirmationBackButton", moral.BackButton);
            SetObject(controllerSerialized, "outcomeProgressText", controls.ProgressText);
            SetObject(controllerSerialized, "outcomeContinueButton", controls.ContinueButton);
            SetObject(controllerSerialized, "outcomeRetryButton", controls.RetryButton);
            controllerSerialized.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject navigationSerialized = new SerializedObject(navigation);
            navigationSerialized.Update();
            SetObject(navigationSerialized, "continueLevelButton", controls.ContinueButton);
            SetObject(navigationSerialized, "retryButton", controls.RetryButton);
            navigationSerialized.ApplyModifiedPropertiesWithoutUndo();

            moral.ChoiceRoot.SetActive(false);
            moral.ConfirmationRoot.SetActive(false);
            controls.RetryButton.gameObject.SetActive(false);
            Phase02UiFactory.NormalizeTextOverflow(safeArea);

            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(navigation);
            EditorUtility.SetDirty(controls.ContinueButton);
            SaveScene(scene, Phase046EncounterSceneFactory.TutorialScenePath);
        }

        private static void UpgradeEncounter(
            string scenePath,
            bool addThornGuardianConsequences,
            TMP_FontAsset font)
        {
            Scene scene = OpenRequiredScene(scenePath);
            EncounterBattleController controller = RequireSingleComponent<EncounterBattleController>(scene);
            EncounterBattleNavigation navigation = RequireSingleComponent<EncounterBattleNavigation>(scene);

            SerializedObject controllerSerialized = new SerializedObject(controller);
            controllerSerialized.Update();
            GameObject outcomeOverlay = RequireObjectReference<GameObject>(
                controllerSerialized,
                "outcomeOverlay");
            Button menuButton = RequireObjectReference<Button>(
                controllerSerialized,
                "outcomeMenuButton");
            RectTransform outcomeCard = RequireNamedRect(outcomeOverlay.transform, "OutcomeCard");
            GameObject uiRoot = RequireNamedObject(scene, Phase046EncounterSceneFactory.UiRootName);
            RectTransform safeArea = RequireNamedRect(uiRoot.transform, "SafeArea");
            TMP_Text phaseText = RebuildBattlePhase(safeArea, font);

            OutcomeControls controls = RebuildProgressionControls(
                outcomeCard,
                menuButton,
                font,
                addThornGuardianConsequences
                    ? "SCELTA REGISTRATA  -  +" +
                      CampaignLevelCatalog.GetByNumber(3).ExperienceReward +
                      " XP AL PRIMO COMPLETAMENTO"
                    : "SCELTA REGISTRATA  -  +" +
                      CampaignLevelCatalog.GetByNumber(2).ExperienceReward +
                      " XP AL PRIMO COMPLETAMENTO",
                addThornGuardianConsequences
                    ? "VAI A EROI"
                    : "LIVELLO 3",
                false);
            if (addThornGuardianConsequences)
            {
                UnityEventTools.AddPersistentListener(
                    controls.ContinueButton.onClick,
                    navigation.GoToHeroes);
            }
            else
            {
                UnityEventTools.AddPersistentListener(
                    controls.ContinueButton.onClick,
                    navigation.ContinueCampaign);
            }
            UnityEventTools.AddPersistentListener(
                controls.RetryButton.onClick,
                navigation.RetryCurrentLevel);

            SetObject(controllerSerialized, "phaseText", phaseText);
            SetObject(controllerSerialized, "outcomeProgressText", controls.ProgressText);
            SetObject(controllerSerialized, "outcomeContinueButton", controls.ContinueButton);
            SetObject(controllerSerialized, "outcomeRetryButton", controls.RetryButton);

            GameObject battleRoot = RequireNamedObject(scene, Phase046EncounterSceneFactory.BattleRootName);
            RemoveNamedChildIfPresent(battleRoot.transform, SavedAllyConsequencesName);
            RemoveNamedChildIfPresent(uiRoot.transform, AllyDialogueRootName);

            if (addThornGuardianConsequences)
            {
                SavedAllyObjects ally = CreateThornGuardianConsequences(
                    battleRoot.transform,
                    uiRoot.transform,
                    font);
                SetObject(controllerSerialized, "thornGuardianAllyActor", ally.Actor);
                SetObject(controllerSerialized, "thornGuardianSupportEffect", ally.SupportEffect);
                SetObject(controllerSerialized, "allyDialogueRoot", ally.DialogueRoot);
                SetObject(controllerSerialized, "allyDialogueText", ally.DialogueText);
            }
            else
            {
                SetObject(controllerSerialized, "thornGuardianAllyActor", null);
                SetObject(controllerSerialized, "thornGuardianSupportEffect", null);
                SetObject(controllerSerialized, "allyDialogueRoot", null);
                SetObject(controllerSerialized, "allyDialogueText", null);
            }

            controllerSerialized.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject navigationSerialized = new SerializedObject(navigation);
            navigationSerialized.Update();
            SetObject(navigationSerialized, "continueLevelButton", controls.ContinueButton);
            SetObject(navigationSerialized, "retryButton", controls.RetryButton);
            navigationSerialized.ApplyModifiedPropertiesWithoutUndo();

            controls.ContinueButton.gameObject.SetActive(false);
            controls.ContinueButton.interactable = false;
            controls.RetryButton.gameObject.SetActive(false);
            controls.RetryButton.interactable = false;
            Phase02UiFactory.NormalizeTextOverflow(safeArea);
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(navigation);
            EditorUtility.SetDirty(controls.ContinueButton);
            SaveScene(scene, scenePath);
        }

        private static OutcomeControls RebuildProgressionControls(
            RectTransform outcomeCard,
            Button menuButton,
            TMP_FontAsset font,
            string initialProgressText,
            string continueButtonLabel,
            bool tutorialLayout)
        {
            Transform previousControls = FindDirectChild(outcomeCard, ProgressionControlsName);
            if (previousControls != null)
            {
                if (menuButton.transform.IsChildOf(previousControls))
                {
                    menuButton.transform.SetParent(outcomeCard, false);
                }

                UnityEngine.Object.DestroyImmediate(previousControls.gameObject);
            }

            RectTransform controls = Phase02UiFactory.CreateRect(
                ProgressionControlsName,
                outcomeCard);
            Phase02UiFactory.SetRect(
                controls,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);

            TMP_Text menuButtonLabel = menuButton.GetComponentInChildren<TMP_Text>(true);
            if (menuButtonLabel == null)
            {
                throw new InvalidOperationException(
                    menuButton.name + " non contiene il testo del pulsante.");
            }

            menuButtonLabel.text = "MENU PRINCIPALE";
            EditorUtility.SetDirty(menuButtonLabel);

            float progressBottom = tutorialLayout ? 0.30f : 0.205f;
            float progressTop = tutorialLayout ? 0.46f : 0.315f;
            TMP_Text progressText = Phase02UiFactory.CreateText(
                "TXT_OutcomeProgress",
                controls,
                initialProgressText,
                tutorialLayout ? 28f : 25f,
                Phase02UiFactory.Gold,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.06f, progressBottom),
                new Vector2(0.94f, progressTop),
                Vector2.zero,
                Vector2.zero,
                FontStyles.Bold);

            menuButton.transform.SetParent(controls, false);
            Phase02UiFactory.SetRect(
                menuButton.GetComponent<RectTransform>(),
                new Vector2(0.06f, 0.045f),
                new Vector2(0.48f, tutorialLayout ? 0.27f : 0.19f),
                Vector2.zero,
                Vector2.zero);

            Button continueButton = Phase02UiFactory.CreateButton(
                "BTN_OutcomeContinue",
                controls,
                continueButtonLabel,
                font,
                new Vector2(0.52f, 0.045f),
                new Vector2(0.94f, tutorialLayout ? 0.27f : 0.19f),
                Vector2.zero,
                Vector2.zero,
                true);
            continueButton.gameObject.SetActive(false);
            continueButton.interactable = false;

            Button retryButton = Phase02UiFactory.CreateButton(
                "BTN_OutcomeRetry",
                controls,
                "RIPROVA",
                font,
                new Vector2(0.52f, 0.045f),
                new Vector2(0.94f, tutorialLayout ? 0.27f : 0.19f),
                Vector2.zero,
                Vector2.zero,
                true);
            retryButton.gameObject.SetActive(false);
            retryButton.interactable = false;

            return new OutcomeControls(progressText, continueButton, retryButton);
        }

        private static TMP_Text RebuildBattlePhase(RectTransform safeArea, TMP_FontAsset font)
        {
            RemoveNamedChildIfPresent(safeArea, BattlePhaseName);
            return Phase02UiFactory.CreateText(
                BattlePhaseName,
                safeArea,
                "TUO TURNO · SCEGLI UN'AZIONE",
                40f,
                Phase02UiFactory.Gold,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.12f, 0.962f),
                new Vector2(0.88f, 0.998f),
                Vector2.zero,
                Vector2.zero,
                FontStyles.Bold);
        }

        private static TutorialOverlayControls RebuildTutorialControls(
            RectTransform safeArea,
            TMP_FontAsset font)
        {
            RectTransform card = RequireNamedRect(safeArea, "TutorialCard");
            RemoveNamedChildIfPresent(card, "BTN_TutorialRepeat");
            RemoveNamedChildIfPresent(card, "BTN_TutorialSkip");

            Button nextButton = RequireNamedRect(card, "BTN_TutorialNext").GetComponent<Button>();
            Phase02UiFactory.SetRect(
                nextButton.GetComponent<RectTransform>(),
                new Vector2(0.34f, 0.055f),
                new Vector2(0.66f, 0.27f),
                Vector2.zero,
                Vector2.zero);
            Button repeatButton = Phase02UiFactory.CreateButton(
                "BTN_TutorialRepeat",
                card,
                "RIPETI\nSPIEGAZIONE",
                font,
                new Vector2(0.04f, 0.055f),
                new Vector2(0.31f, 0.27f),
                Vector2.zero,
                Vector2.zero);
            Button skipButton = Phase02UiFactory.CreateButton(
                "BTN_TutorialSkip",
                card,
                "SALTA\nTUTORIAL",
                font,
                new Vector2(0.69f, 0.055f),
                new Vector2(0.96f, 0.27f),
                Vector2.zero,
                Vector2.zero);
            SetCompactButtonText(nextButton, 27f);
            SetCompactButtonText(repeatButton, 21f);
            SetCompactButtonText(skipButton, 21f);
            return new TutorialOverlayControls(repeatButton, skipButton);
        }

        private static TutorialMoralUi RebuildTutorialMoralChoice(
            RectTransform safeArea,
            TMP_FontAsset font)
        {
            RemoveNamedChildIfPresent(safeArea, TutorialMoralChoiceName);
            RectTransform root = Phase02UiFactory.CreateRect(TutorialMoralChoiceName, safeArea);
            Phase02UiFactory.SetRect(root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            RectTransform choice = Phase02UiFactory.CreatePanel(
                "TutorialFinalChoicePanel",
                root,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                new Color(0.015f, 0.03f, 0.03f, 0.985f),
                true);
            RectTransform choiceCard = Phase02UiFactory.CreatePanel(
                "TutorialFinalChoiceCard",
                choice,
                new Vector2(0.06f, 0.17f),
                new Vector2(0.94f, 0.83f),
                Vector2.zero,
                Vector2.zero,
                Phase02UiFactory.Panel);
            TMP_Text title = Phase02UiFactory.CreateText(
                "TXT_TutorialFinalChoiceTitle",
                choiceCard,
                "PASSO 11 / 12 · NEMICO INCAPACITATO",
                40f,
                Phase02UiFactory.Gold,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.06f, 0.82f),
                new Vector2(0.94f, 0.96f),
                Vector2.zero,
                Vector2.zero,
                FontStyles.Bold);
            TMP_Text profile = Phase02UiFactory.CreateText(
                "TXT_TutorialFinalChoiceProfile",
                choiceCard,
                "CREATURA CORROTTA\nRAZZA · CREATURA DELLE RADICI\nCORRUZIONE · 70%\nSTATO · ARRABBIATO",
                31f,
                Phase02UiFactory.MainText,
                TextAlignmentOptions.Left,
                font,
                new Vector2(0.41f, 0.50f),
                new Vector2(0.92f, 0.80f),
                Vector2.zero,
                Vector2.zero,
                FontStyles.Bold);
            RectTransform portraitFrame = Phase02UiFactory.CreatePanel(
                "TutorialFinalChoicePortraitFrame",
                choiceCard,
                new Vector2(0.08f, 0.50f),
                new Vector2(0.37f, 0.80f),
                Vector2.zero,
                Vector2.zero,
                new Color(0.035f, 0.105f, 0.10f, 1f));
            RectTransform portraitRect = Phase02UiFactory.CreateRect(
                "IMG_TutorialFinalChoicePortrait",
                portraitFrame);
            Phase02UiFactory.SetRect(
                portraitRect,
                Vector2.zero,
                Vector2.one,
                new Vector2(16f, 16f),
                new Vector2(-16f, -16f));
            Image portrait = portraitRect.gameObject.AddComponent<Image>();
            portrait.color = Color.white;
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            TMP_Text dialogue = Phase02UiFactory.CreateText(
                "TXT_TutorialFinalChoiceDialogue",
                choiceCard,
                "Un nemico sconfitto non è ancora morto. Ora devi decidere il suo destino." +
                "\n\nSALVA: resta vivo; potrà tornare o aiutarti." +
                "\nUCCIDI: esce dalla storia; non potrà aiutarti.",
                29f,
                Phase02UiFactory.Light,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.08f, 0.30f),
                new Vector2(0.92f, 0.50f),
                Vector2.zero,
                Vector2.zero);
            Button save = Phase02UiFactory.CreateButton(
                "BTN_TutorialSave",
                choiceCard,
                "SALVA",
                font,
                new Vector2(0.06f, 0.07f),
                new Vector2(0.48f, 0.27f),
                Vector2.zero,
                Vector2.zero,
                true);
            Button kill = Phase02UiFactory.CreateButton(
                "BTN_TutorialKill",
                choiceCard,
                "UCCIDI",
                font,
                new Vector2(0.52f, 0.07f),
                new Vector2(0.94f, 0.27f),
                Vector2.zero,
                Vector2.zero);

            RectTransform confirmation = Phase02UiFactory.CreatePanel(
                "TutorialConfirmationPanel",
                root,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                new Color(0.01f, 0.025f, 0.025f, 0.99f),
                true);
            RectTransform confirmationCard = Phase02UiFactory.CreatePanel(
                "TutorialConfirmationCard",
                confirmation,
                new Vector2(0.07f, 0.31f),
                new Vector2(0.93f, 0.69f),
                Vector2.zero,
                Vector2.zero,
                Phase02UiFactory.Panel);
            TMP_Text confirmationText = Phase02UiFactory.CreateText(
                "TXT_TutorialConfirmation",
                confirmationCard,
                "PASSO 12 / 12\nConfermare questa decisione?",
                35f,
                Phase02UiFactory.MainText,
                TextAlignmentOptions.Center,
                font,
                new Vector2(0.07f, 0.35f),
                new Vector2(0.93f, 0.90f),
                Vector2.zero,
                Vector2.zero,
                FontStyles.Bold);
            Button confirm = Phase02UiFactory.CreateButton(
                "BTN_TutorialConfirmChoice",
                confirmationCard,
                "CONFERMA",
                font,
                new Vector2(0.06f, 0.07f),
                new Vector2(0.52f, 0.31f),
                Vector2.zero,
                Vector2.zero,
                true);
            Button back = Phase02UiFactory.CreateButton(
                "BTN_TutorialBackChoice",
                confirmationCard,
                "INDIETRO",
                font,
                new Vector2(0.56f, 0.07f),
                new Vector2(0.94f, 0.31f),
                Vector2.zero,
                Vector2.zero);

            choice.gameObject.SetActive(false);
            confirmation.gameObject.SetActive(false);
            return new TutorialMoralUi(
                choice.gameObject,
                title,
                portrait,
                profile,
                dialogue,
                save,
                kill,
                confirmation.gameObject,
                confirmationText,
                confirm,
                back);
        }

        private static void SetCompactButtonText(Button button, float size)
        {
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.fontSize = size;
                label.fontSizeMax = size;
                label.textWrappingMode = TextWrappingModes.Normal;
            }
        }

        private static SavedAllyObjects CreateThornGuardianConsequences(
            Transform battleRoot,
            Transform uiRoot,
            TMP_FontAsset font)
        {
            GameObject consequences = new GameObject(SavedAllyConsequencesName);
            consequences.transform.SetParent(battleRoot, false);

            GameObject actor = new GameObject("ThornGuardianAllyActor");
            actor.transform.SetParent(consequences.transform, false);
            actor.transform.localPosition = new Vector3(-0.45f, -4.18f, 0f);
            actor.transform.localScale = Vector3.one * 0.46f;

            GameObject visual = InstantiateRequiredPrefab(
                Phase01PlaceholderFactory.EnemyPrefabPath,
                actor.transform,
                "ThornGuardianAllyVisual");
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            foreach (SpriteRenderer renderer in visual.GetComponentsInChildren<SpriteRenderer>(true))
            {
                renderer.flipX = false;
                renderer.color = new Color(0.55f, 0.78f, 0.36f, 0.86f);
                renderer.sortingOrder = -8;
            }

            GameObject supportEffect = InstantiateRequiredPrefab(
                Phase02PrototypeAssetFactory.MarkPulsePrefabPath,
                consequences.transform,
                "ThornGuardianSupportEffect");
            supportEffect.transform.localPosition = new Vector3(2.25f, -3.48f, 0f);
            supportEffect.transform.localRotation = Quaternion.identity;
            supportEffect.transform.localScale = Vector3.one * 0.82f;
            foreach (SpriteRenderer renderer in supportEffect.GetComponentsInChildren<SpriteRenderer>(true))
            {
                renderer.color = new Color(0.62f, 0.92f, 0.38f, 0.90f);
                renderer.sortingOrder = 16;
            }

            RectTransform safeArea = RequireNamedRect(uiRoot, "SafeArea");
            RectTransform dialoguePanel = Phase02UiFactory.CreatePanel(
                AllyDialogueRootName,
                safeArea,
                new Vector2(0.04f, 0.485f),
                new Vector2(0.49f, 0.615f),
                Vector2.zero,
                Vector2.zero,
                new Color(0.08f, 0.23f, 0.14f, 0.93f));
            TMP_Text dialogueText = Phase02UiFactory.CreateText(
                "TXT_AllyDialogue",
                dialoguePanel,
                "Custode del Rovo: non sei più solo.",
                26f,
                Phase02UiFactory.Light,
                TextAlignmentOptions.Center,
                font,
                Vector2.zero,
                Vector2.one,
                new Vector2(16f, 9f),
                new Vector2(-16f, -9f),
                FontStyles.Italic);

            actor.SetActive(false);
            supportEffect.SetActive(false);
            dialoguePanel.gameObject.SetActive(false);
            return new SavedAllyObjects(
                actor,
                supportEffect,
                dialoguePanel.gameObject,
                dialogueText);
        }

        private static GameObject InstantiateRequiredPrefab(
            string path,
            Transform parent,
            string instanceName)
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
            return instance;
        }

        private static Scene OpenRequiredScene(string scenePath)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                throw new InvalidOperationException("Scena richiesta mancante: " + scenePath);
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new InvalidOperationException("Impossibile aprire la scena: " + scenePath);
            }

            return scene;
        }

        private static T RequireSingleComponent<T>(Scene scene) where T : Component
        {
            T[] components = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
            if (components.Length != 1)
            {
                throw new InvalidOperationException(
                    "La scena " + scene.path + " deve contenere esattamente un " + typeof(T).Name +
                    ", trovati: " + components.Length + ".");
            }

            return components[0];
        }

        private static GameObject RequireNamedObject(Scene scene, string objectName)
        {
            GameObject[] matches = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(transform => transform.name == objectName)
                .Select(transform => transform.gameObject)
                .ToArray();
            if (matches.Length != 1)
            {
                throw new InvalidOperationException(
                    "La scena " + scene.path + " deve contenere esattamente un oggetto " +
                    objectName + ", trovati: " + matches.Length + ".");
            }

            return matches[0];
        }

        private static RectTransform RequireNamedRect(Transform parent, string objectName)
        {
            Transform match = parent.name == objectName ? parent : FindDescendant(parent, objectName);
            RectTransform rect = match as RectTransform;
            if (rect == null)
            {
                throw new InvalidOperationException(
                    "RectTransform persistente mancante: " + objectName + ".");
            }

            return rect;
        }

        private static Transform FindDescendant(Transform parent, string objectName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == objectName)
                {
                    return child;
                }

                Transform nested = FindDescendant(child, objectName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private static Transform FindDirectChild(Transform parent, string objectName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == objectName)
                {
                    return child;
                }
            }

            return null;
        }

        private static void RemoveNamedChildIfPresent(Transform parent, string objectName)
        {
            Transform match = FindDescendant(parent, objectName);
            if (match != null)
            {
                UnityEngine.Object.DestroyImmediate(match.gameObject);
            }
        }

        private static T RequireObjectReference<T>(SerializedObject serialized, string propertyName)
            where T : UnityEngine.Object
        {
            SerializedProperty property = RequireProperty(serialized, propertyName);
            T value = property.objectReferenceValue as T;
            if (value == null)
            {
                throw new InvalidOperationException(
                    serialized.targetObject.GetType().Name + "." + propertyName +
                    " non è assegnato nella scena.");
            }

            return value;
        }

        private static void SetObject(
            SerializedObject serialized,
            string propertyName,
            UnityEngine.Object value)
        {
            RequireProperty(serialized, propertyName).objectReferenceValue = value;
        }

        private static SerializedProperty RequireProperty(
            SerializedObject serialized,
            string propertyName)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    "Campo serializzato mancante: " + serialized.targetObject.GetType().Name +
                    "." + propertyName + ".");
            }

            return property;
        }

        private static void SaveScene(Scene scene, string scenePath)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                throw new InvalidOperationException("Impossibile salvare la scena: " + scenePath);
            }
        }

        private readonly struct OutcomeControls
        {
            internal OutcomeControls(
                TMP_Text progressText,
                Button continueButton,
                Button retryButton)
            {
                ProgressText = progressText;
                ContinueButton = continueButton;
                RetryButton = retryButton;
            }

            internal TMP_Text ProgressText { get; }
            internal Button ContinueButton { get; }
            internal Button RetryButton { get; }
        }

        private readonly struct TutorialOverlayControls
        {
            internal TutorialOverlayControls(Button repeatButton, Button skipButton)
            {
                RepeatButton = repeatButton;
                SkipButton = skipButton;
            }

            internal Button RepeatButton { get; }
            internal Button SkipButton { get; }
        }

        private readonly struct TutorialMoralUi
        {
            internal TutorialMoralUi(
                GameObject choiceRoot,
                TMP_Text titleText,
                Image portrait,
                TMP_Text profileText,
                TMP_Text dialogueText,
                Button saveButton,
                Button killButton,
                GameObject confirmationRoot,
                TMP_Text confirmationText,
                Button confirmButton,
                Button backButton)
            {
                ChoiceRoot = choiceRoot;
                TitleText = titleText;
                Portrait = portrait;
                ProfileText = profileText;
                DialogueText = dialogueText;
                SaveButton = saveButton;
                KillButton = killButton;
                ConfirmationRoot = confirmationRoot;
                ConfirmationText = confirmationText;
                ConfirmButton = confirmButton;
                BackButton = backButton;
            }

            internal GameObject ChoiceRoot { get; }
            internal TMP_Text TitleText { get; }
            internal Image Portrait { get; }
            internal TMP_Text ProfileText { get; }
            internal TMP_Text DialogueText { get; }
            internal Button SaveButton { get; }
            internal Button KillButton { get; }
            internal GameObject ConfirmationRoot { get; }
            internal TMP_Text ConfirmationText { get; }
            internal Button ConfirmButton { get; }
            internal Button BackButton { get; }
        }

        private readonly struct SavedAllyObjects
        {
            internal SavedAllyObjects(
                GameObject actor,
                GameObject supportEffect,
                GameObject dialogueRoot,
                TMP_Text dialogueText)
            {
                Actor = actor;
                SupportEffect = supportEffect;
                DialogueRoot = dialogueRoot;
                DialogueText = dialogueText;
            }

            internal GameObject Actor { get; }
            internal GameObject SupportEffect { get; }
            internal GameObject DialogueRoot { get; }
            internal TMP_Text DialogueText { get; }
        }
    }
}
#endif
