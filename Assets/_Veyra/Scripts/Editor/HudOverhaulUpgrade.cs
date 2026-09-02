#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Veyra.Combat.Encounter;
using Veyra.Combat.MultiEnemy;
using Veyra.Combat.Tutorial;
using Veyra.UI;
using Veyra.UI.Battle;

namespace Veyra.Editor
{
    internal static class HudOverhaulUpgrade
    {
        private const string WorldLayerName = "HUD_WorldLayer";
        private const string SourceFolder = "Assets/Bottoni";
        private const string ArtFolder = "Assets/_Veyra/Art/Sprites/UI/HUD/UserProvided";
        private const string PrefabFolder = "Assets/_Veyra/Prefabs/UI/Battle/HUD";
        private const string ThemePath = "Assets/_Veyra/Data/UI/VeyraThemePalette.asset";

        private const string MainMenuPath = "Assets/_Veyra/Scenes/SCN_MainMenu.unity";
        private const string TutorialPath = "Assets/_Veyra/Scenes/SCN_W01_L01_Tutorial.unity";
        private const string Level02Path = "Assets/_Veyra/Scenes/SCN_W01_L02_ThornGuardian.unity";
        private const string Level03Path = "Assets/_Veyra/Scenes/SCN_W01_L03_AshWatcher.unity";
        private const string Level04Path = "Assets/_Veyra/Scenes/SCN_W01_L04_ThreefoldAssault.unity";

        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
        private static readonly Color Background = new Color32(9, 11, 19, 255);
        private static readonly Color Panel = new Color32(21, 24, 39, 246);
        private static readonly Color SecondaryPanel = new Color32(31, 36, 56, 250);
        private static readonly Color Gold = new Color32(228, 163, 41, 255);
        private static readonly Color Cyan = new Color32(72, 185, 181, 255);
        private static readonly Color Coral = new Color32(232, 90, 60, 255);
        private static readonly Color Green = new Color32(119, 200, 75, 255);
        private static readonly Color WarmWhite = new Color32(244, 240, 223, 255);
        private static readonly Color Muted = new Color32(169, 171, 192, 255);

        private sealed class HudSprites
        {
            public Sprite Primary;
            public Sprite PrimarySelected;
            public Sprite Secondary;
            public Sprite Disabled;
            public Sprite Upgrade;
            public Sprite EmptyHealth;
            public Sprite GreenHealth;
        }

        [MenuItem("Tools/Veyra/HUD/Apply Complete HUD Overhaul", priority = 230)]
        internal static void ApplyCompleteHudOverhaul()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Esegui l'upgrade HUD soltanto in Edit Mode.");
            }

            EnsureFolder(ArtFolder);
            EnsureFolder(PrefabFolder);
            HudSprites sprites = ImportProvidedSprites();
            TMP_FontAsset font = Phase02UiFactory.LoadRequiredFont();
            UpdateTheme();
            CreateReusablePrefabs(font, sprites);

            UpgradeMainMenu(sprites);
            UpgradeTutorial(font, sprites);
            UpgradeEncounter(Level02Path, font, sprites);
            UpgradeEncounter(Level03Path, font, sprites);
            UpgradeLevel04(font, sprites);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Veyra HUD] Upgrade completato: HUD compatto, barre contestuali e dialoghi ancorati applicati ai livelli 1-4.");
        }

        [MenuItem("Tools/Veyra/Validate/Complete HUD", priority = 303)]
        internal static void ValidateCompleteHud()
        {
            var errors = new List<string>();
            ValidateScene(TutorialPath, 2, 0, errors);
            ValidateScene(Level02Path, 2, 1, errors);
            ValidateScene(Level03Path, 2, 1, errors);
            ValidateScene(Level04Path, 4, 3, errors);

            string[] futureScenes = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_Veyra/Scenes" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.Contains("_L05_") || path.Contains("_L06_") ||
                               path.Contains("_L07_") || path.Contains("_L08_") ||
                               path.Contains("_L09_") || path.Contains("_L10_"))
                .ToArray();
            if (futureScenes.Length > 0)
            {
                errors.Add("Sono state trovate scene future non consentite: " + string.Join(", ", futureScenes));
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "[Veyra HUD Validation] FALLITA (" + errors.Count + "):\n- " +
                    string.Join("\n- ", errors));
            }

            Debug.Log("[Veyra HUD Validation] SUPERATA — HUD compatto, barre vita inizialmente nascoste, dialoghi contestuali e scene 1-4 conformi.");
        }

        private static HudSprites ImportProvidedSprites()
        {
            var map = new Dictionary<string, string>
            {
                { "Bottone primario non selezionato.png", "SPR_HUD_ButtonPrimary.png" },
                { "Bottoni primario selezionato.png", "SPR_HUD_ButtonPrimarySelected.png" },
                { "Bottone secondario non selezionato.png", "SPR_HUD_ButtonSecondary.png" },
                { "Bottoni disabilitati.png", "SPR_HUD_ButtonDisabled.png" },
                { "Bottone Upgrade.png", "SPR_HUD_ButtonUpgrade.png" },
                { "Barra Vita intera.png", "SPR_HUD_HealthEmpty.png" },
                { "Barra vita intera verde.png", "SPR_HUD_HealthGreen.png" },
                { "Barra vita con chip damage (rosso).png", "SPR_HUD_HealthDamageReference.png" },
                { "Barra vita con chip damage (rosso in ritardo).png", "SPR_HUD_HealthChipReference.png" },
                { "Barra vita lampeggiante 25.png", "SPR_HUD_HealthDangerReference.png" },
                { "Nemico vita.png", "SPR_HUD_EnemyHealthReference.png" },
                { "Boss vita.png", "SPR_HUD_BossHealthReserved.png" }
            };

            foreach (KeyValuePair<string, string> pair in map)
            {
                string source = SourceFolder + "/" + pair.Key;
                string destination = ArtFolder + "/" + pair.Value;
                if (!File.Exists(source))
                {
                    throw new FileNotFoundException("Risorsa HUD mancante", source);
                }

                if (!File.Exists(destination))
                {
                    if (!AssetDatabase.CopyAsset(source, destination))
                    {
                        throw new InvalidOperationException("Impossibile copiare " + source);
                    }
                }
                else
                {
                    File.Copy(source, destination, true);
                }

                AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceUpdate);
                TextureImporter importer = AssetImporter.GetAtPath(destination) as TextureImporter;
                if (importer == null) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                if (pair.Key.IndexOf("Bottone", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    importer.spriteBorder = new Vector4(20f, 20f, 20f, 20f);
                }
                importer.SaveAndReimport();
            }

            return new HudSprites
            {
                Primary = LoadSprite("SPR_HUD_ButtonPrimary.png"),
                PrimarySelected = LoadSprite("SPR_HUD_ButtonPrimarySelected.png"),
                Secondary = LoadSprite("SPR_HUD_ButtonSecondary.png"),
                Disabled = LoadSprite("SPR_HUD_ButtonDisabled.png"),
                Upgrade = LoadSprite("SPR_HUD_ButtonUpgrade.png"),
                EmptyHealth = LoadSprite("SPR_HUD_HealthEmpty.png"),
                GreenHealth = LoadSprite("SPR_HUD_HealthGreen.png")
            };
        }

        private static Sprite LoadSprite(string fileName)
        {
            string path = ArtFolder + "/" + fileName;
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
            }
            if (sprite == null) throw new InvalidOperationException("Sprite HUD non importato: " + path);
            return sprite;
        }

        private static void UpdateTheme()
        {
            VeyraThemePalette theme = AssetDatabase.LoadAssetAtPath<VeyraThemePalette>(ThemePath);
            if (theme == null)
            {
                EnsureFolder("Assets/_Veyra/Data/UI");
                theme = ScriptableObject.CreateInstance<VeyraThemePalette>();
                AssetDatabase.CreateAsset(theme, ThemePath);
            }

            theme.background = Background;
            theme.panel = Panel;
            theme.secondaryPanel = SecondaryPanel;
            theme.border = new Color32(74, 82, 122, 255);
            theme.disabled = new Color32(86, 87, 91, 255);
            theme.primaryText = WarmWhite;
            theme.secondaryText = Muted;
            theme.information = Cyan;
            theme.action = Gold;
            theme.danger = Coral;
            theme.damage = Coral;
            EditorUtility.SetDirty(theme);
        }

        private static void CreateReusablePrefabs(TMP_FontAsset font, HudSprites sprites)
        {
            CreateHealthPrefab(font, sprites);
            CreateDialoguePrefab(font);
            CreateButtonPrefab(font, sprites, true);
            CreateButtonPrefab(font, sprites, false);
            CreateCompactPanelPrefab();
        }

        private static void CreateHealthPrefab(TMP_FontAsset font, HudSprites sprites)
        {
            GameObject root = CreateUiObject("PF_WorldHealthBar", null);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(280f, 54f);
            CanvasGroup group = root.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            WorldUiFollower follower = root.AddComponent<WorldUiFollower>();
            WorldHealthBarView view = root.AddComponent<WorldHealthBarView>();

            Image background = CreateImage("Frame", rect, sprites.EmptyHealth, Color.white,
                new Vector2(0f, 0.18f), new Vector2(1f, 0.78f));
            background.preserveAspect = false;
            Image chip = CreateImage("ChipFill", rect, BuiltinSprite(), Coral,
                new Vector2(0.025f, 0.28f), new Vector2(0.975f, 0.68f));
            ConfigureFilled(chip);
            Image current = CreateImage("CurrentFill", rect, sprites.GreenHealth, Color.white,
                new Vector2(0f, 0.18f), new Vector2(1f, 0.78f));
            ConfigureFilled(current);
            Image danger = CreateImage("DangerFrame", rect, sprites.EmptyHealth, Coral,
                new Vector2(0f, 0.18f), new Vector2(1f, 0.78f));
            danger.enabled = false;
            TMP_Text value = CreateText("TXT_HealthValue", rect, "100 / 100", font, 19f,
                WarmWhite, TextAlignmentOptions.Center, new Vector2(0.05f, 0f), new Vector2(0.95f, 0.36f));

            SetSerialized(follower, "followedRect", rect);
            SetSerialized(view, "follower", follower);
            SetSerialized(view, "canvasGroup", group);
            SetSerialized(view, "currentFill", current);
            SetSerialized(view, "chipFill", chip);
            SetSerialized(view, "dangerFrame", danger);
            SetSerialized(view, "valueText", value);
            SavePrefab(root, PrefabFolder + "/PF_WorldHealthBar.prefab");
        }

        private static void CreateDialoguePrefab(TMP_FontAsset font)
        {
            GameObject root = CreateUiObject("PF_WorldDialogueBubble", null);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(390f, 150f);
            CanvasGroup group = root.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            WorldUiFollower follower = root.AddComponent<WorldUiFollower>();
            WorldDialogueBubbleView view = root.AddComponent<WorldDialogueBubbleView>();

            Image panel = root.AddComponent<Image>();
            panel.sprite = BuiltinSprite();
            panel.type = Image.Type.Sliced;
            panel.color = Panel;
            panel.raycastTarget = false;
            Outline outline = root.AddComponent<Outline>();
            outline.effectColor = new Color(Gold.r, Gold.g, Gold.b, 0.90f);
            outline.effectDistance = new Vector2(2f, -2f);

            Image tail = CreateImage("Tail", rect, BuiltinSprite(), Panel,
                new Vector2(0.46f, -0.05f), new Vector2(0.54f, 0.15f));
            tail.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            TMP_Text speaker = CreateText("TXT_Speaker", rect, "NEMICO", font, 22f, Gold,
                TextAlignmentOptions.Left, new Vector2(0.055f, 0.70f), new Vector2(0.945f, 0.93f));
            speaker.fontStyle = FontStyles.Bold;
            TMP_Text body = CreateText("TXT_Dialogue", rect, "Dialogo contestuale", font, 26f, WarmWhite,
                TextAlignmentOptions.TopLeft, new Vector2(0.055f, 0.12f), new Vector2(0.945f, 0.70f));
            body.textWrappingMode = TextWrappingModes.Normal;
            body.overflowMode = TextOverflowModes.Ellipsis;

            SetSerialized(follower, "followedRect", rect);
            SetSerialized(view, "follower", follower);
            SetSerialized(view, "canvasGroup", group);
            SetSerialized(view, "speakerText", speaker);
            SetSerialized(view, "bodyText", body);
            SavePrefab(root, PrefabFolder + "/PF_WorldDialogueBubble.prefab");
        }

        private static void CreateButtonPrefab(TMP_FontAsset font, HudSprites sprites, bool primary)
        {
            string name = primary ? "PF_CombatActionButton_Primary" : "PF_CombatActionButton_Secondary";
            GameObject root = CreateUiObject(name, null);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(260f, 82f);
            Image image = root.AddComponent<Image>();
            Button button = root.AddComponent<Button>();
            button.targetGraphic = image;
            CreateText("TXT_Label", rect, primary ? "AZIONE" : "INFORMAZIONE", font, 27f,
                WarmWhite, TextAlignmentOptions.Center, Vector2.zero, Vector2.one).fontStyle = FontStyles.Bold;
            StyleButton(button, sprites, primary ? ButtonKind.Primary : ButtonKind.Secondary);
            SavePrefab(root, PrefabFolder + "/" + name + ".prefab");
        }

        private static void CreateCompactPanelPrefab()
        {
            GameObject root = CreateUiObject("PF_CompactPanel", null);
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(460f, 250f);
            Image image = root.AddComponent<Image>();
            image.sprite = BuiltinSprite();
            image.type = Image.Type.Sliced;
            image.color = Panel;
            Outline outline = root.AddComponent<Outline>();
            outline.effectColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.62f);
            outline.effectDistance = new Vector2(2f, -2f);
            SavePrefab(root, PrefabFolder + "/PF_CompactPanel.prefab");
        }

        private static void UpgradeMainMenu(HudSprites sprites)
        {
            Scene scene = OpenScene(MainMenuPath);
            ConfigureCanvases(scene);
            PolishAllButtons(scene, sprites);
            PolishAllText(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void UpgradeTutorial(TMP_FontAsset font, HudSprites sprites)
        {
            Scene scene = OpenScene(TutorialPath);
            TutorialBattleController controller = FindSingle<TutorialBattleController>(scene);
            SerializedObject serialized = new SerializedObject(controller);
            RectTransform safeArea = GetSafeArea(scene);
            Canvas canvas = safeArea.GetComponentInParent<Canvas>();
            Camera camera = FindCamera(scene);
            RectTransform layer = RebuildWorldLayer(safeArea);
            Transform hero = GetObject<Transform>(serialized, "heroActor");
            Transform enemy = GetObject<Transform>(serialized, "enemyActor");

            WorldHealthBarView heroBar = CreateHealthInstance(layer, canvas, safeArea, camera, hero, true, "HUD_HeroHealth");
            WorldHealthBarView enemyBar = CreateHealthInstance(layer, canvas, safeArea, camera, enemy, false, "HUD_EnemyHealth");
            SetObject(serialized, "heroWorldHealthBar", heroBar);
            SetObject(serialized, "enemyWorldHealthBar", enemyBar);
            HideSingleEnemyLegacyHealth(serialized);
            CompactSingleEnemyLayout(scene, serialized, true);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            ConfigureCanvases(scene);
            PolishAllButtons(scene, sprites);
            PolishAllText(scene);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void UpgradeEncounter(string path, TMP_FontAsset font, HudSprites sprites)
        {
            Scene scene = OpenScene(path);
            EncounterBattleController controller = FindSingle<EncounterBattleController>(scene);
            SerializedObject serialized = new SerializedObject(controller);
            RectTransform safeArea = GetSafeArea(scene);
            Canvas canvas = safeArea.GetComponentInParent<Canvas>();
            Camera camera = FindCamera(scene);
            RectTransform layer = RebuildWorldLayer(safeArea);
            Transform hero = GetObject<Transform>(serialized, "heroActor");
            Transform enemy = GetObject<Transform>(serialized, "enemyActor");

            WorldHealthBarView heroBar = CreateHealthInstance(layer, canvas, safeArea, camera, hero, true, "HUD_HeroHealth");
            WorldHealthBarView enemyBar = CreateHealthInstance(layer, canvas, safeArea, camera, enemy, false, "HUD_EnemyHealth");
            WorldDialogueBubbleView enemyDialogue = CreateDialogueInstance(
                layer, canvas, safeArea, camera, enemy, "HUD_EnemyDialogue", Coral);
            SetObject(serialized, "heroWorldHealthBar", heroBar);
            SetObject(serialized, "enemyWorldHealthBar", enemyBar);
            SetObject(serialized, "enemyWorldDialogue", enemyDialogue);

            GameObject allyActor = GetObject<GameObject>(serialized, "thornGuardianAllyActor");
            if (allyActor != null)
            {
                WorldDialogueBubbleView allyDialogue = CreateDialogueInstance(
                    layer, canvas, safeArea, camera, allyActor.transform, "HUD_AllyDialogue", Cyan);
                SetObject(serialized, "allyWorldDialogue", allyDialogue);
            }

            SetInactive(serialized, "enemyDialogueRoot");
            SetInactive(serialized, "allyDialogueRoot");
            HideSingleEnemyLegacyHealth(serialized);
            CompactSingleEnemyLayout(scene, serialized, false);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            ConfigureCanvases(scene);
            PolishAllButtons(scene, sprites);
            PolishAllText(scene);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void UpgradeLevel04(TMP_FontAsset font, HudSprites sprites)
        {
            Scene scene = OpenScene(Level04Path);
            MultiEnemyBattleController controller = FindSingle<MultiEnemyBattleController>(scene);
            SerializedObject serialized = new SerializedObject(controller);
            RectTransform safeArea = GetSafeArea(scene);
            Canvas canvas = safeArea.GetComponentInParent<Canvas>();
            Camera camera = FindCamera(scene);
            RectTransform layer = RebuildWorldLayer(safeArea);
            Transform hero = GetObject<Transform>(serialized, "heroActor");
            WorldHealthBarView heroBar = CreateHealthInstance(layer, canvas, safeArea, camera, hero, true, "HUD_HeroHealth");
            SetObject(serialized, "heroWorldHealthBar", heroBar);

            SerializedProperty enemies = serialized.FindProperty("enemyViews");
            for (int index = 0; index < enemies.arraySize; index++)
            {
                SerializedProperty enemy = enemies.GetArrayElementAtIndex(index);
                Transform actor = enemy.FindPropertyRelative("actor").objectReferenceValue as Transform;
                string id = enemy.FindPropertyRelative("enemyId").stringValue;
                WorldHealthBarView bar = CreateHealthInstance(
                    layer, canvas, safeArea, camera, actor, false, "HUD_Health_" + id);
                WorldDialogueBubbleView dialogue = CreateDialogueInstance(
                    layer, canvas, safeArea, camera, actor, "HUD_Dialogue_" + id, Coral);
                enemy.FindPropertyRelative("worldHealthBar").objectReferenceValue = bar;
                enemy.FindPropertyRelative("worldDialogue").objectReferenceValue = dialogue;

                Image oldFill = enemy.FindPropertyRelative("healthFill").objectReferenceValue as Image;
                TMP_Text oldValue = enemy.FindPropertyRelative("healthText").objectReferenceValue as TMP_Text;
                if (oldFill != null) oldFill.enabled = false;
                if (oldValue != null) oldValue.enabled = false;
            }

            GameObject thorn = GetObject<GameObject>(serialized, "thornGuardianAllyActor");
            GameObject ash = GetObject<GameObject>(serialized, "ashWatcherAllyActor");
            if (thorn != null)
            {
                SetObject(serialized, "thornGuardianWorldDialogue", CreateDialogueInstance(
                    layer, canvas, safeArea, camera, thorn.transform, "HUD_ThornAllyDialogue", Cyan));
            }
            if (ash != null)
            {
                SetObject(serialized, "ashWatcherWorldDialogue", CreateDialogueInstance(
                    layer, canvas, safeArea, camera, ash.transform, "HUD_AshAllyDialogue", Cyan));
            }

            SetInactive(serialized, "dialogueRoot");
            SetInactive(serialized, "allyDialogueRoot");
            Image heroLegacy = GetObject<Image>(serialized, "heroHealthFill");
            TMP_Text heroLegacyText = GetObject<TMP_Text>(serialized, "heroHealthText");
            if (heroLegacy != null) heroLegacy.enabled = false;
            if (heroLegacyText != null) heroLegacyText.enabled = false;
            CompactLevel04Layout(scene, serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            ConfigureCanvases(scene);
            PolishAllButtons(scene, sprites);
            PolishAllText(scene);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static RectTransform RebuildWorldLayer(RectTransform safeArea)
        {
            Transform existing = safeArea.Find(WorldLayerName);
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }

            RectTransform layer = CreateUiObject(WorldLayerName, safeArea).GetComponent<RectTransform>();
            SetRect(layer, Vector2.zero, Vector2.one);
            layer.SetAsLastSibling();
            return layer;
        }

        private static WorldHealthBarView CreateHealthInstance(
            RectTransform parent,
            Canvas canvas,
            RectTransform canvasRect,
            Camera camera,
            Transform target,
            bool hero,
            string name)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/PF_WorldHealthBar.prefab");
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (instance == null) throw new InvalidOperationException("Impossibile creare " + name);
            instance.name = name;
            RectTransform rect = instance.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(hero ? 285f : 250f, hero ? 56f : 50f);

            WorldUiFollower follower = instance.GetComponent<WorldUiFollower>();
            WorldHealthBarView view = instance.GetComponent<WorldHealthBarView>();
            SetSerialized(follower, "rootCanvas", canvas);
            SetSerialized(follower, "canvasRect", canvasRect);
            SetSerialized(follower, "followedRect", rect);
            SetSerialized(follower, "target", target);
            SetSerialized(follower, "worldCamera", camera);
            SetSerialized(follower, "worldOffset", CalculateOffset(target, 0.42f));

            Image current = instance.transform.Find("CurrentFill")?.GetComponent<Image>();
            Image chip = instance.transform.Find("ChipFill")?.GetComponent<Image>();
            if (current != null)
            {
                current.color = hero ? Color.white : Coral;
                if (!hero) current.sprite = BuiltinSprite();
            }
            if (chip != null) chip.color = new Color(Coral.r, Coral.g, Coral.b, 0.95f);
            view.SetTarget(target);
            view.SetHealthSilently(1, 1);
            return view;
        }

        private static WorldDialogueBubbleView CreateDialogueInstance(
            RectTransform parent,
            Canvas canvas,
            RectTransform canvasRect,
            Camera camera,
            Transform target,
            string name,
            Color accent)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/PF_WorldDialogueBubble.prefab");
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (instance == null) throw new InvalidOperationException("Impossibile creare " + name);
            instance.name = name;
            RectTransform rect = instance.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(390f, 150f);
            WorldUiFollower follower = instance.GetComponent<WorldUiFollower>();
            WorldDialogueBubbleView view = instance.GetComponent<WorldDialogueBubbleView>();
            SetSerialized(follower, "rootCanvas", canvas);
            SetSerialized(follower, "canvasRect", canvasRect);
            SetSerialized(follower, "followedRect", rect);
            SetSerialized(follower, "target", target);
            SetSerialized(follower, "worldCamera", camera);
            SetSerialized(follower, "worldOffset", CalculateOffset(target, 1.12f));
            Outline outline = instance.GetComponent<Outline>();
            if (outline != null) outline.effectColor = new Color(accent.r, accent.g, accent.b, 0.92f);
            TMP_Text speaker = instance.transform.Find("TXT_Speaker")?.GetComponent<TMP_Text>();
            if (speaker != null) speaker.color = accent;
            view.SetTarget(target);
            view.HideImmediate();
            return view;
        }

        private static Vector3 CalculateOffset(Transform target, float extra)
        {
            if (target == null) return new Vector3(0f, 1.8f + extra, 0f);
            SpriteRenderer renderer = target.GetComponentInChildren<SpriteRenderer>(true);
            float height = renderer != null ? Mathf.Max(0.9f, renderer.bounds.max.y - target.position.y) : 1.4f;
            return new Vector3(0f, height + extra, 0f);
        }

        private static void HideSingleEnemyLegacyHealth(SerializedObject serialized)
        {
            HideSafeAreaPanel(GetObject<Image>(serialized, "heroHealthFill"));
            HideSafeAreaPanel(GetObject<Image>(serialized, "enemyHealthFill"));
        }

        private static void HideSafeAreaPanel(Component component)
        {
            if (component == null) return;
            Transform current = component.transform;
            while (current.parent != null && current.parent.name != "SafeArea")
            {
                current = current.parent;
            }
            if (current.parent != null && current.parent.name == "SafeArea")
            {
                current.gameObject.SetActive(false);
            }
            else
            {
                component.gameObject.SetActive(false);
            }
        }

        private static void CompactSingleEnemyLayout(Scene scene, SerializedObject serialized, bool tutorial)
        {
            SetTopLevelRect(GetObject<TMP_Text>(serialized, "combatMessage"), 0.30f, 0.895f, 0.70f, 0.95f);
            SetTopLevelRect(GetObject<TMP_Text>(serialized, "intentText"), 0.72f, 0.895f, 0.94f, 0.95f);
            SetTopLevelRect(GetObject<TMP_Text>(serialized, "statusText"), 0.04f, 0.895f, 0.28f, 0.95f);
            GameObject phase = GetObject<TMP_Text>(serialized, "phaseText")?.gameObject;
            if (phase != null) phase.SetActive(false);

            string[] actionNames = { "attackButton", "guardButton", "techniqueButton", "analyzeButton" };
            for (int index = 0; index < actionNames.Length; index++)
            {
                Button button = GetObject<Button>(serialized, actionNames[index]);
                if (button == null) continue;
                float left = 0.18f + index * 0.1625f;
                SetRect(button.GetComponent<RectTransform>(), new Vector2(left, 0.018f),
                    new Vector2(left + 0.145f, 0.108f));
            }

            RectTransform analyze = GetObject<GameObject>(serialized, "analyzePanel")?.GetComponent<RectTransform>();
            if (analyze != null) SetRect(analyze, new Vector2(0.66f, 0.46f), new Vector2(0.96f, 0.84f));
            RectTransform finalChoice = GetObject<GameObject>(serialized, "finalChoicePanel")?.GetComponent<RectTransform>();
            if (finalChoice != null) SetRect(finalChoice, new Vector2(0.25f, 0.18f), new Vector2(0.75f, 0.80f));
            RectTransform confirmation = GetObject<GameObject>(serialized, "confirmationPanel")?.GetComponent<RectTransform>();
            if (confirmation != null) SetRect(confirmation, new Vector2(0.32f, 0.28f), new Vector2(0.68f, 0.70f));

            if (tutorial)
            {
                RectTransform tutorialOverlay = GetObject<GameObject>(serialized, "tutorialOverlay")?.GetComponent<RectTransform>();
                RectTransform card = FindRect(scene, "TutorialCard");
                if (card != null) SetRect(card, new Vector2(0.025f, 0.57f), new Vector2(0.36f, 0.84f));
                Image blocker = GetObject<Image>(serialized, "tutorialInputBlocker");
                if (blocker != null)
                {
                    Color color = blocker.color;
                    color.a = 0.08f;
                    blocker.color = color;
                }
                if (tutorialOverlay != null) tutorialOverlay.SetAsLastSibling();
            }
        }

        private static void CompactLevel04Layout(Scene scene, SerializedObject serialized)
        {
            SetNamedRect(scene, "TXT_Level04Title", 0.35f, 0.963f, 0.65f, 0.995f);
            SetNamedRect(scene, "BTN_Target_W01_L04_BRUTE", 0.18f, 0.835f, 0.385f, 0.915f);
            SetNamedRect(scene, "BTN_Target_W01_L04_WATCHER", 0.3975f, 0.835f, 0.6025f, 0.915f);
            SetNamedRect(scene, "BTN_Target_W01_L04_MASK", 0.615f, 0.835f, 0.82f, 0.915f);
            SetNamedRect(scene, "SelectedTargetPanel", 0.02f, 0.895f, 0.25f, 0.95f);
            SetNamedRect(scene, "CombatMessagePanel", 0.29f, 0.895f, 0.71f, 0.95f);
            SetNamedRect(scene, "HeroStatusPanel", 0.75f, 0.895f, 0.98f, 0.95f);
            RectTransform heroPanel = FindRect(scene, "HeroHealthPanel");
            if (heroPanel != null) heroPanel.gameObject.SetActive(false);
            RectTransform phase = FindRect(scene, "TXT_Level04Phase");
            if (phase != null) phase.gameObject.SetActive(false);

            string[] actionNames = { "attackButton", "guardButton", "techniqueButton", "analyzeButton" };
            for (int index = 0; index < actionNames.Length; index++)
            {
                Button button = GetObject<Button>(serialized, actionNames[index]);
                if (button == null) continue;
                float left = 0.18f + index * 0.1625f;
                SetRect(button.GetComponent<RectTransform>(), new Vector2(left, 0.018f),
                    new Vector2(left + 0.145f, 0.108f));
            }
            RectTransform analyze = GetObject<GameObject>(serialized, "analyzePanel")?.GetComponent<RectTransform>();
            if (analyze != null) SetRect(analyze, new Vector2(0.64f, 0.40f), new Vector2(0.96f, 0.82f));
            RectTransform moral = GetObject<GameObject>(serialized, "moralChoicePanel")?.GetComponent<RectTransform>();
            if (moral != null) SetRect(moral, new Vector2(0.20f, 0.13f), new Vector2(0.80f, 0.84f));
        }

        private static void ConfigureCanvases(Scene scene)
        {
            foreach (CanvasScaler scaler in scene.GetRootGameObjects()
                         .SelectMany(root => root.GetComponentsInChildren<CanvasScaler>(true)))
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = ReferenceResolution;
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
                EditorUtility.SetDirty(scaler);
            }
        }

        private static void PolishAllButtons(Scene scene, HudSprites sprites)
        {
            foreach (Button button in scene.GetRootGameObjects()
                         .SelectMany(root => root.GetComponentsInChildren<Button>(true)))
            {
                ButtonKind kind = ClassifyButton(button);
                StyleButton(button, sprites, kind);
                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.color = WarmWhite;
                    label.fontStyle = FontStyles.Bold;
                    label.enableAutoSizing = true;
                    label.fontSizeMin = 19f;
                    label.fontSizeMax = 30f;
                    label.margin = new Vector4(12f, 5f, 12f, 5f);
                    EditorUtility.SetDirty(label);
                }
            }
        }

        private enum ButtonKind { Primary, Secondary, Upgrade, Danger }

        private static ButtonKind ClassifyButton(Button button)
        {
            string key = button.name.ToLowerInvariant();
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null) key += " " + label.text.ToLowerInvariant();
            if (key.Contains("upgrade") || key.Contains("potenz")) return ButtonKind.Upgrade;
            if (key.Contains("kill") || key.Contains("uccidi") || key.Contains("elimina")) return ButtonKind.Danger;
            if (key.Contains("guard") || key.Contains("analy") || key.Contains("analizza") ||
                key.Contains("settings") || key.Contains("opzioni") || key.Contains("back") ||
                key.Contains("indietro") || key.Contains("close") || key.Contains("chiudi") ||
                key.Contains("menu") || key.Contains("cancel") || key.Contains("annulla") ||
                key.Contains("pause")) return ButtonKind.Secondary;
            return ButtonKind.Primary;
        }

        private static void StyleButton(Button button, HudSprites sprites, ButtonKind kind)
        {
            Image image = button.targetGraphic as Image ?? button.GetComponent<Image>();
            if (image == null) return;
            Sprite normal = kind == ButtonKind.Secondary ? sprites.Secondary :
                kind == ButtonKind.Upgrade ? sprites.Upgrade : sprites.Primary;
            image.sprite = normal;
            image.type = Image.Type.Sliced;
            image.color = kind == ButtonKind.Danger ? new Color(1f, 0.48f, 0.40f, 1f) : Color.white;
            image.raycastTarget = true;

            if (kind == ButtonKind.Secondary)
            {
                button.transition = Selectable.Transition.ColorTint;
                ColorBlock colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
                colors.pressedColor = new Color(0.78f, 0.88f, 0.90f, 1f);
                colors.selectedColor = new Color(0.88f, 1f, 1f, 1f);
                colors.disabledColor = new Color(0.34f, 0.34f, 0.34f, 0.72f);
                colors.fadeDuration = 0.08f;
                button.colors = colors;
            }
            else
            {
                button.transition = Selectable.Transition.SpriteSwap;
                SpriteState state = button.spriteState;
                state.highlightedSprite = sprites.PrimarySelected;
                state.pressedSprite = sprites.PrimarySelected;
                state.selectedSprite = sprites.PrimarySelected;
                state.disabledSprite = sprites.Disabled;
                button.spriteState = state;
            }
            EditorUtility.SetDirty(image);
            EditorUtility.SetDirty(button);
        }

        private static void PolishAllText(Scene scene)
        {
            foreach (TMP_Text text in scene.GetRootGameObjects()
                         .SelectMany(root => root.GetComponentsInChildren<TMP_Text>(true)))
            {
                if (text.fontSize < 19f) text.fontSize = 19f;
                if (text.enableAutoSizing)
                {
                    text.fontSizeMin = Mathf.Max(17f, text.fontSizeMin);
                    text.fontSizeMax = Mathf.Max(text.fontSizeMin + 2f, text.fontSizeMax);
                }
                text.raycastTarget = false;
                EditorUtility.SetDirty(text);
            }
        }

        private static void ValidateScene(
            string path,
            int expectedHealthBars,
            int minimumDialogues,
            List<string> errors)
        {
            Scene scene = OpenScene(path);
            string label = Path.GetFileNameWithoutExtension(path);
            RectTransform layer = FindRect(scene, WorldLayerName);
            if (layer == null)
            {
                errors.Add(label + ": HUD_WorldLayer mancante.");
                return;
            }

            WorldHealthBarView[] bars = layer.GetComponentsInChildren<WorldHealthBarView>(true);
            if (bars.Length != expectedHealthBars)
            {
                errors.Add(label + ": attese " + expectedHealthBars + " barre contestuali, trovate " + bars.Length + ".");
            }
            foreach (WorldHealthBarView bar in bars)
            {
                CanvasGroup group = bar.GetComponent<CanvasGroup>();
                WorldUiFollower follower = bar.GetComponent<WorldUiFollower>();
                if (group == null || group.alpha > 0.001f)
                    errors.Add(label + ": " + bar.name + " non parte nascosta.");
                if (follower == null || follower.Target == null)
                    errors.Add(label + ": " + bar.name + " non segue un personaggio.");
            }

            WorldDialogueBubbleView[] dialogues = layer.GetComponentsInChildren<WorldDialogueBubbleView>(true);
            if (dialogues.Length < minimumDialogues)
                errors.Add(label + ": dialoghi contestuali insufficienti.");

            CanvasScaler scaler = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CanvasScaler>(true)).FirstOrDefault();
            if (scaler == null || scaler.referenceResolution != ReferenceResolution)
                errors.Add(label + ": CanvasScaler non configurato a 1920x1080.");
        }

        private static Scene OpenScene(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Scena mancante", path);
            return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }

        private static T FindSingle<T>(Scene scene) where T : Component
        {
            T[] found = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
            if (found.Length != 1)
                throw new InvalidOperationException(scene.name + ": atteso un solo " + typeof(T).Name + ", trovati " + found.Length);
            return found[0];
        }

        private static RectTransform GetSafeArea(Scene scene)
        {
            RectTransform[] found = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RectTransform>(true))
                .Where(rect => rect.name == "SafeArea").ToArray();
            if (found.Length != 1)
                throw new InvalidOperationException(scene.name + ": atteso un solo SafeArea, trovati " + found.Length);
            return found[0];
        }

        private static Camera FindCamera(Scene scene)
        {
            Camera camera = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                .OrderByDescending(candidate => candidate.CompareTag("MainCamera"))
                .FirstOrDefault();
            if (camera == null) throw new InvalidOperationException(scene.name + ": Camera mancante.");
            return camera;
        }

        private static void SetInactive(SerializedObject serialized, string propertyName)
        {
            GameObject value = GetObject<GameObject>(serialized, propertyName);
            if (value != null) value.SetActive(false);
        }

        private static T GetObject<T>(SerializedObject serialized, string propertyName) where T : UnityEngine.Object
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            return property != null ? property.objectReferenceValue as T : null;
        }

        private static void SetObject(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null) throw new InvalidOperationException("Campo serializzato mancante: " + propertyName);
            property.objectReferenceValue = value;
        }

        private static void SetSerialized(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null) throw new InvalidOperationException(target.name + ": campo mancante " + propertyName);
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetSerialized(UnityEngine.Object target, string propertyName, Vector3 value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null) throw new InvalidOperationException(target.name + ": campo mancante " + propertyName);
            property.vector3Value = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            GameObject value = new GameObject(name, typeof(RectTransform));
            if (parent != null) value.transform.SetParent(parent, false);
            return value;
        }

        private static Image CreateImage(
            string name,
            RectTransform parent,
            Sprite sprite,
            Color color,
            Vector2 anchorsMin,
            Vector2 anchorsMax)
        {
            RectTransform rect = CreateUiObject(name, parent).GetComponent<RectTransform>();
            SetRect(rect, anchorsMin, anchorsMax);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text CreateText(
            string name,
            RectTransform parent,
            string value,
            TMP_FontAsset font,
            float size,
            Color color,
            TextAlignmentOptions alignment,
            Vector2 anchorsMin,
            Vector2 anchorsMax)
        {
            RectTransform rect = CreateUiObject(name, parent).GetComponent<RectTransform>();
            SetRect(rect, anchorsMin, anchorsMax);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        private static void ConfigureFilled(Image image)
        {
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
            image.fillClockwise = true;
            image.fillAmount = 1f;
        }

        private static Sprite BuiltinSprite()
        {
            return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        }

        private static void SavePrefab(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void SetTopLevelRect(Component component, float x0, float y0, float x1, float y1)
        {
            if (component == null) return;
            RectTransform current = component.GetComponent<RectTransform>();
            while (current.parent is RectTransform parent && parent.name != "SafeArea")
            {
                current = parent;
            }
            SetRect(current, new Vector2(x0, y0), new Vector2(x1, y1));
        }

        private static void SetNamedRect(Scene scene, string name, float x0, float y0, float x1, float y1)
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

        private static void SetRect(RectTransform rect, Vector2 anchorsMin, Vector2 anchorsMax)
        {
            rect.anchorMin = anchorsMin;
            rect.anchorMax = anchorsMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void EnsureFolder(string path)
        {
            string normalized = path.Replace('\\', '/');
            string[] parts = normalized.Split('/');
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
    }
}
#endif
