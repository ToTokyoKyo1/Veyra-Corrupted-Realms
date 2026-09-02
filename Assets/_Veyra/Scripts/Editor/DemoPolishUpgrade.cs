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
using Veyra.Combat.Encounter;
using Veyra.Combat;
using Veyra.Combat.MultiEnemy;
using Veyra.Combat.Tutorial;
using Veyra.UI.Battle;
using Veyra.UI.Settings;

namespace Veyra.Editor
{
    internal static class DemoPolishUpgrade
    {
        private const string PauseControllerName = "BattlePauseController";
        private const string PauseUiName = "BattlePauseUi";

        [MenuItem("Tools/Veyra/Demo/Apply Complete Polish", priority = 225)]
        internal static void ApplyCompletePolish()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Esegui la rifinitura completa soltanto in Edit Mode.");
            }

            TacticalLandscapeUpgrade.UpgradeAll();
            TMP_FontAsset font = Phase02UiFactory.LoadRequiredFont();
            AddPauseAndPolish<TutorialBattleNavigation>(
                "Assets/_Veyra/Scenes/SCN_W01_L01_Tutorial.unity", font);
            AddPauseAndPolish<EncounterBattleNavigation>(
                "Assets/_Veyra/Scenes/SCN_W01_L02_ThornGuardian.unity", font);
            AddPauseAndPolish<EncounterBattleNavigation>(
                "Assets/_Veyra/Scenes/SCN_W01_L03_AshWatcher.unity", font);
            AddPauseAndPolish<MultiEnemyBattleNavigation>(
                "Assets/_Veyra/Scenes/SCN_W01_L04_ThreefoldAssault.unity", font);
            PolishMainMenu();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Veyra Demo Polish] HUD, input tattico, pausa, opzioni e stile UI applicati ai livelli 1-4. Livelli 5-10 invariati.");
        }

        [MenuItem("Tools/Veyra/Validate/Demo Polish", priority = 302)]
        internal static void ValidateDemoPolish()
        {
            var errors = new List<string>();
            CombatDamageResolution guarded = CombatDamageResolver.Resolve(25, true);
            if (!guarded.BlockedByGuard || guarded.AppliedDamage != 0)
            {
                errors.Add("La Guardia centralizzata non blocca a zero il danno normale.");
            }

            ValidateBattleScene<TutorialBattleNavigation>(
                "Assets/_Veyra/Scenes/SCN_W01_L01_Tutorial.unity", errors);
            ValidateBattleScene<EncounterBattleNavigation>(
                "Assets/_Veyra/Scenes/SCN_W01_L02_ThornGuardian.unity", errors);
            ValidateBattleScene<EncounterBattleNavigation>(
                "Assets/_Veyra/Scenes/SCN_W01_L03_AshWatcher.unity", errors);
            ValidateBattleScene<MultiEnemyBattleNavigation>(
                "Assets/_Veyra/Scenes/SCN_W01_L04_ThreefoldAssault.unity", errors);

            string[] futureScenes = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_Veyra/Scenes" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => Enumerable.Range(5, 6).Any(level =>
                    path.IndexOf("_L" + level.ToString("D2"), StringComparison.OrdinalIgnoreCase) >= 0))
                .ToArray();
            if (futureScenes.Length > 0)
            {
                errors.Add("Sono presenti scene 5-10 non consentite: " + string.Join(", ", futureScenes));
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "[Veyra Demo Polish Validation] FALLITA (" + errors.Count + "):\n- " +
                    string.Join("\n- ", errors));
            }

            Debug.Log("[Veyra Demo Polish Validation] SUPERATA — Guardia a zero danni, pausa con conferma, opzioni, Analizza 1/turno, input tattico e scene 1-4 conformi.");
        }

        private static void ValidateBattleScene<TNavigation>(string path, List<string> errors)
            where TNavigation : Component
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            GameObject[] roots = scene.GetRootGameObjects();
            BattlePauseController[] pauses = roots
                .SelectMany(root => root.GetComponentsInChildren<BattlePauseController>(true)).ToArray();
            if (pauses.Length != 1)
            {
                errors.Add(scene.name + ": atteso un solo pannello di pausa, trovati " + pauses.Length + ".");
            }

            TNavigation[] navigations = roots
                .SelectMany(root => root.GetComponentsInChildren<TNavigation>(true)).ToArray();
            if (navigations.Length != 1 ||
                new SerializedObject(navigations[0]).FindProperty("pauseController")?.objectReferenceValue == null)
            {
                errors.Add(scene.name + ": navigazione non collegata alla pausa.");
            }

            if (!roots.SelectMany(root => root.GetComponentsInChildren<SettingsPanelController>(true)).Any())
            {
                errors.Add(scene.name + ": pannello Opzioni in pausa assente.");
            }

            bool hasAnalyzeLimit = roots.SelectMany(root => root.GetComponentsInChildren<TMP_Text>(true))
                .Any(text => text.text.IndexOf("1/TURNO", StringComparison.OrdinalIgnoreCase) >= 0);
            if (!hasAnalyzeLimit)
            {
                errors.Add(scene.name + ": limite gratuito di Analizza non visibile.");
            }

            CanvasScaler[] scalers = roots.SelectMany(root => root.GetComponentsInChildren<CanvasScaler>(true)).ToArray();
            if (scalers.Any(scaler => scaler.referenceResolution != new Vector2(1920f, 1080f)))
            {
                errors.Add(scene.name + ": almeno un Canvas non usa il riferimento orizzontale 1920x1080.");
            }
        }

        private static void AddPauseAndPolish<TNavigation>(string scenePath, TMP_FontAsset font)
            where TNavigation : Component
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            RemoveRoot(scene, PauseControllerName);
            RemoveEveryObjectNamed(scene, PauseUiName);

            Canvas canvas = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Canvas>(true))
                .OrderByDescending(candidate => candidate.sortingOrder)
                .First();
            Transform parent = canvas.GetComponentsInChildren<RectTransform>(true)
                                   .FirstOrDefault(rect => rect.name == "SafeArea") ??
                               canvas.transform;

            GameObject controllerObject = new GameObject(PauseControllerName);
            SceneManager.MoveGameObjectToScene(controllerObject, scene);
            BattlePauseController pause = controllerObject.AddComponent<BattlePauseController>();

            RectTransform pauseRoot = Phase02UiFactory.CreatePanel(
                PauseUiName, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.025f, 0.03f, 0.07f, 0.92f), true);
            RectTransform panel = Phase02UiFactory.CreatePanel(
                "PausePanel", pauseRoot, new Vector2(0.34f, 0.13f), new Vector2(0.66f, 0.87f),
                Vector2.zero, Vector2.zero, Phase02UiFactory.Panel, true);
            Image panelImage = panel.GetComponent<Image>();
            Sprite frame = VeyraVisualAssetSetup.LoadButtonFrame(false);
            if (frame != null)
            {
                panelImage.sprite = frame;
                panelImage.type = Image.Type.Sliced;
                panelImage.color = new Color(0.65f, 0.70f, 0.92f, 1f);
            }

            Phase02UiFactory.CreateText(
                "TXT_PauseTitle", panel, "PAUSA", 52f, Phase02UiFactory.Light,
                TextAlignmentOptions.Center, font, new Vector2(0.08f, 0.85f), new Vector2(0.92f, 0.97f),
                Vector2.zero, Vector2.zero, FontStyles.Bold);
            Button resume = CreatePauseButton(panel, font, "BTN_PauseResume", "RIPRENDI", 0.68f, pause.Resume, true);
            CreatePauseButton(panel, font, "BTN_PauseRetry", "RIPROVA", 0.51f, pause.RequestRetry);
            CreatePauseButton(panel, font, "BTN_PauseOptions", "OPZIONI", 0.34f, pause.OpenOptions);
            CreatePauseButton(panel, font, "BTN_PauseMenu", "TORNA AL MENU", 0.17f, pause.RequestMainMenu);

            RectTransform confirm = Phase02UiFactory.CreatePanel(
                "PauseConfirmation", pauseRoot, new Vector2(0.27f, 0.28f), new Vector2(0.73f, 0.72f),
                Vector2.zero, Vector2.zero, Phase02UiFactory.Panel, true);
            TMP_Text confirmText = Phase02UiFactory.CreateText(
                "TXT_PauseConfirmation", confirm, string.Empty, 34f, Phase02UiFactory.Light,
                TextAlignmentOptions.Center, font, new Vector2(0.08f, 0.42f), new Vector2(0.92f, 0.90f),
                Vector2.zero, Vector2.zero, FontStyles.Bold);
            Button confirmLeave = Phase02UiFactory.CreateButton(
                "BTN_PauseConfirmLeave", confirm, "CONFERMA", font,
                new Vector2(0.08f, 0.10f), new Vector2(0.47f, 0.34f), Vector2.zero, Vector2.zero, true);
            Button cancelLeave = Phase02UiFactory.CreateButton(
                "BTN_PauseCancelLeave", confirm, "ANNULLA", font,
                new Vector2(0.53f, 0.10f), new Vector2(0.92f, 0.34f), Vector2.zero, Vector2.zero);
            UnityEventTools.AddPersistentListener(confirmLeave.onClick, pause.ConfirmLeave);
            UnityEventTools.AddPersistentListener(cancelLeave.onClick, pause.CancelConfirmation);

            SettingsPanelController settings = CreateSettings(pauseRoot, font);
            SerializedObject pauseSerialized = new SerializedObject(pause);
            pauseSerialized.FindProperty("pauseRoot").objectReferenceValue = pauseRoot.gameObject;
            pauseSerialized.FindProperty("confirmationRoot").objectReferenceValue = confirm.gameObject;
            pauseSerialized.FindProperty("confirmationText").objectReferenceValue = confirmText;
            pauseSerialized.FindProperty("resumeButton").objectReferenceValue = resume;
            pauseSerialized.FindProperty("settingsPanel").objectReferenceValue = settings;
            pauseSerialized.ApplyModifiedPropertiesWithoutUndo();

            TNavigation navigation = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<TNavigation>(true)).Single();
            SerializedObject navigationSerialized = new SerializedObject(navigation);
            navigationSerialized.FindProperty("pauseController").objectReferenceValue = pause;
            navigationSerialized.ApplyModifiedPropertiesWithoutUndo();

            confirm.gameObject.SetActive(false);
            pauseRoot.gameObject.SetActive(false);
            PolishUi(scene);
            EditorUtility.SetDirty(pause);
            EditorUtility.SetDirty(navigation);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static SettingsPanelController CreateSettings(Transform parent, TMP_FontAsset font)
        {
            RectTransform dimmer = Phase02UiFactory.CreatePanel(
                "PauseSettingsDimmer", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0f, 0f, 0f, 0.72f), true);
            RectTransform modal = Phase02UiFactory.CreatePanel(
                "PauseSettingsModal", parent, new Vector2(0.24f, 0.10f), new Vector2(0.76f, 0.90f),
                Vector2.zero, Vector2.zero, Phase02UiFactory.Panel, true);
            SettingsPanelController settings = modal.gameObject.AddComponent<SettingsPanelController>();
            Phase02UiFactory.CreateText(
                "TXT_SettingsTitle", modal, "OPZIONI", 48f, Phase02UiFactory.Light,
                TextAlignmentOptions.Center, font, new Vector2(0.05f, 0.87f), new Vector2(0.95f, 0.98f),
                Vector2.zero, Vector2.zero, FontStyles.Bold);

            Slider master = CreateSettingSlider(modal, font, "Master", "VOLUME GENERALE", 0.70f, out TMP_Text masterValue);
            Slider music = CreateSettingSlider(modal, font, "Music", "MUSICA", 0.53f, out TMP_Text musicValue);
            Slider sfx = CreateSettingSlider(modal, font, "Sfx", "EFFETTI", 0.36f, out TMP_Text sfxValue);
            Phase02UiFactory.CreateText(
                "TXT_VibrationLabel", modal, "VIBRAZIONE / REAZIONE", 28f, Phase02UiFactory.Light,
                TextAlignmentOptions.Left, font, new Vector2(0.08f, 0.23f), new Vector2(0.68f, 0.31f),
                Vector2.zero, Vector2.zero);
            Toggle vibration = Phase02UiFactory.CreateToggle(
                "TGL_Vibration", modal, new Vector2(0.76f, 0.225f), new Vector2(0.90f, 0.31f));
            Button defaults = Phase02UiFactory.CreateButton(
                "BTN_SettingsDefaults", modal, "PREDEFINITI", font,
                new Vector2(0.07f, 0.06f), new Vector2(0.47f, 0.17f), Vector2.zero, Vector2.zero);
            Button close = Phase02UiFactory.CreateButton(
                "BTN_SettingsClose", modal, "CHIUDI", font,
                new Vector2(0.53f, 0.06f), new Vector2(0.93f, 0.17f), Vector2.zero, Vector2.zero, true);

            SerializedObject serialized = new SerializedObject(settings);
            serialized.FindProperty("dimmer").objectReferenceValue = dimmer.gameObject;
            serialized.FindProperty("modalRoot").objectReferenceValue = modal.gameObject;
            serialized.FindProperty("masterVolumeSlider").objectReferenceValue = master;
            serialized.FindProperty("musicVolumeSlider").objectReferenceValue = music;
            serialized.FindProperty("sfxVolumeSlider").objectReferenceValue = sfx;
            serialized.FindProperty("vibrationToggle").objectReferenceValue = vibration;
            serialized.FindProperty("masterValueText").objectReferenceValue = masterValue;
            serialized.FindProperty("musicValueText").objectReferenceValue = musicValue;
            serialized.FindProperty("sfxValueText").objectReferenceValue = sfxValue;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            UnityEventTools.AddPersistentListener(master.onValueChanged, settings.OnMasterVolumeChanged);
            UnityEventTools.AddPersistentListener(music.onValueChanged, settings.OnMusicVolumeChanged);
            UnityEventTools.AddPersistentListener(sfx.onValueChanged, settings.OnSfxVolumeChanged);
            UnityEventTools.AddPersistentListener(vibration.onValueChanged, settings.OnVibrationChanged);
            UnityEventTools.AddPersistentListener(defaults.onClick, settings.ResetToDefaults);
            UnityEventTools.AddPersistentListener(close.onClick, settings.Close);
            dimmer.gameObject.SetActive(false);
            modal.gameObject.SetActive(false);
            return settings;
        }

        private static Slider CreateSettingSlider(
            Transform parent, TMP_FontAsset font, string id, string label, float y, out TMP_Text value)
        {
            Phase02UiFactory.CreateText(
                "TXT_" + id + "Label", parent, label, 28f, Phase02UiFactory.Light,
                TextAlignmentOptions.Left, font, new Vector2(0.08f, y + 0.08f), new Vector2(0.72f, y + 0.15f),
                Vector2.zero, Vector2.zero);
            value = Phase02UiFactory.CreateText(
                "TXT_" + id + "Value", parent, "100%", 28f, Phase02UiFactory.Cyan,
                TextAlignmentOptions.Right, font, new Vector2(0.73f, y + 0.08f), new Vector2(0.92f, y + 0.15f),
                Vector2.zero, Vector2.zero, FontStyles.Bold);
            return Phase02UiFactory.CreateSlider(
                "SLD_" + id, parent, new Vector2(0.08f, y), new Vector2(0.92f, y + 0.07f));
        }

        private static Button CreatePauseButton(
            Transform parent, TMP_FontAsset font, string name, string label, float y,
            UnityEngine.Events.UnityAction action, bool primary = false)
        {
            Button button = Phase02UiFactory.CreateButton(
                name, parent, label, font, new Vector2(0.10f, y), new Vector2(0.90f, y + 0.12f),
                Vector2.zero, Vector2.zero, primary);
            UnityEventTools.AddPersistentListener(button.onClick, action);
            return button;
        }

        private static void PolishMainMenu()
        {
            Scene scene = EditorSceneManager.OpenScene(
                "Assets/_Veyra/Scenes/SCN_MainMenu.unity", OpenSceneMode.Single);
            PolishUi(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void PolishUi(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            foreach (Button button in roots.SelectMany(root => root.GetComponentsInChildren<Button>(true)))
            {
                Phase02UiFactory.ApplyProvidedButtonVisuals(button,
                    button.name.IndexOf("Play", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    button.name.IndexOf("Continue", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    button.name.IndexOf("Confirm", StringComparison.OrdinalIgnoreCase) >= 0);
            }

            foreach (TMP_Text text in roots.SelectMany(root => root.GetComponentsInChildren<TMP_Text>(true)))
            {
                text.enableAutoSizing = true;
                text.fontSizeMin = Mathf.Max(16f, text.fontSizeMin);
                text.overflowMode = TextOverflowModes.Truncate;
                EditorUtility.SetDirty(text);
            }

            foreach (Slider slider in roots.SelectMany(root => root.GetComponentsInChildren<Slider>(true)))
            {
                if (slider.handleRect == null) continue;
                slider.handleRect.sizeDelta = new Vector2(32f, 32f);
                slider.handleRect.localScale = Vector3.one;
                EditorUtility.SetDirty(slider.handleRect);
            }

            foreach (Button analyze in roots.SelectMany(root => root.GetComponentsInChildren<Button>(true))
                         .Where(button => button.name.IndexOf("Analyze", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                TMP_Text label = analyze.GetComponentInChildren<TMP_Text>(true);
                if (label != null && label.text.IndexOf("1/TURNO", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    label.text = "ANALIZZA\nGRATUITO · 1/TURNO";
                    label.textWrappingMode = TextWrappingModes.Normal;
                }
            }
        }

        private static void RemoveRoot(Scene scene, string name)
        {
            GameObject existing = scene.GetRootGameObjects().FirstOrDefault(root => root.name == name);
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing);
        }

        private static void RemoveEveryObjectNamed(Scene scene, string name)
        {
            GameObject[] matches = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(item => item.name == name)
                .Select(item => item.gameObject)
                .ToArray();
            foreach (GameObject match in matches) UnityEngine.Object.DestroyImmediate(match);
        }
    }
}
#endif
