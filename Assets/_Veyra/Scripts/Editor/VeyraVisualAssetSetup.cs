#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Veyra.Combat;
using Veyra.UI;

namespace Veyra.Editor
{
    public static class VeyraVisualAssetSetup
    {
        internal const string HeroIdlePath = "Assets/_Veyra/Art/Sprites/Characters/Hero01/UserProvided/SPR_Hero01_Idle.png";
        internal const string HeroWalkPath = "Assets/_Veyra/Art/Sprites/Characters/Hero01/UserProvided/SPR_Hero01_Walk.png";
        internal const string HeroAttackPath = "Assets/_Veyra/Art/Sprites/Characters/Hero01/UserProvided/SPR_Hero01_Attack.png";
        internal const string HeroTechniquePath = "Assets/_Veyra/Art/Sprites/Characters/Hero01/UserProvided/SPR_Hero01_Technique.png";
        internal const string HeroPrefabPath = "Assets/_Veyra/Prefabs/Characters/Hero01/PF_Hero01_UserProvided.prefab";
        internal const string KnightPrefabPath = "Assets/_Veyra/Prefabs/Enemies/World01/Knight/PF_W01_Knight_UserProvided.prefab";
        internal const string IconsPath = "Assets/_Veyra/Art/Sprites/UI/UserProvided/SPR_UI_Icons32.png";
        internal const string UiAtlasPath = "Assets/_Veyra/Art/Sprites/UI/UserProvided/SPR_UI_Atlas.png";
        internal const string SelectSfxPath = "Assets/_Veyra/Audio/SFX/UI/UserProvided/select.wav";
        internal const string ThemePath = "Assets/_Veyra/Data/UI/VeyraThemePalette.asset";

        private const string HeroAnimationFolder = "Assets/_Veyra/Animations/Characters/Hero01/UserProvided";
        private const string KnightAnimationFolder = "Assets/_Veyra/Animations/Enemies/World01/Knight/UserProvided";
        private const string KnightSpriteFolder = "Assets/_Veyra/Art/Sprites/Enemies/World01/Knight/UserProvided";

        [MenuItem("Tools/Veyra/Visuals/Integrate Provided Art", priority = 600)]
        public static void IntegrateProvidedArt()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("L'integrazione grafica richiede Unity in Edit Mode.");
            }

            EnsureFolders();
            ConfigureTextures();
            ConfigureAudio();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            CreateTheme();
            RuntimeAnimatorController heroController = CreateHeroAnimations();
            RuntimeAnimatorController knightController = CreateKnightAnimations();
            CreateActorPrefab(HeroPrefabPath, "PF_Hero01_UserProvided", HeroIdlePath, heroController, 10);
            CreateActorPrefab(KnightPrefabPath, "PF_W01_Knight_UserProvided", KnightIdlePath, knightController, 20);

            AssetDatabase.SaveAssets();
            Phase03TutorialSetup.CreateFirstBattleTutorial();
            Phase78ProgressionSetup.CreateMenuHeroProgressAndLevel04();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[Veyra Visuals] Asset forniti integrati. Hero01 usa Idle/Walk; Knight e' assegnato solo al nemico terrestre compatibile del Livello 4.");
        }

        internal static Sprite LoadButtonFrame(bool primary)
        {
            string wanted = primary ? "ui_button_orange" : "ui_button_blue";
            return LoadSprites(UiAtlasPath).FirstOrDefault(sprite => sprite.name == wanted);
        }

        internal static Sprite LoadUiIconForButton(string buttonName)
        {
            int index = -1;
            if (buttonName.IndexOf("Attack", StringComparison.OrdinalIgnoreCase) >= 0) index = 0;
            else if (buttonName.IndexOf("Guard", StringComparison.OrdinalIgnoreCase) >= 0) index = 31;
            else if (buttonName.IndexOf("Technique", StringComparison.OrdinalIgnoreCase) >= 0) index = 111;
            else if (buttonName.IndexOf("Analyze", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     buttonName.IndexOf("Mark", StringComparison.OrdinalIgnoreCase) >= 0) index = 77;
            if (index < 0) return null;

            string wanted = "ui_icon_" + index.ToString("D3");
            return LoadSprites(IconsPath).FirstOrDefault(sprite => sprite.name == wanted);
        }

        private static string KnightIdlePath => KnightSpriteFolder + "/noBKG_KnightIdle_strip.png";

        private static void EnsureFolders()
        {
            string[] folders =
            {
                "Assets/_Veyra/Data/UI",
                HeroAnimationFolder,
                KnightAnimationFolder,
                "Assets/_Veyra/Prefabs/Enemies/World01/Knight"
            };
            foreach (string folder in folders) EnsureFolder(folder);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }

        private static void ConfigureTextures()
        {
            ConfigureGrid(HeroIdlePath, 10, 1, 46, 55, "hero_idle", 32f);
            ConfigureGrid(HeroWalkPath, 4, 6, 45, 58, "hero_walk", 32f);
            ConfigureGrid(HeroAttackPath, 4, 5, 160, 64, "hero_attack", 32f);
            ConfigureGrid(HeroTechniquePath, 4, 9, 126, 62, "hero_technique", 32f);
            ConfigureGrid(KnightIdlePath, 15, 1, 64, 64, "knight_idle", 32f);
            ConfigureGrid(KnightSpriteFolder + "/noBKG_KnightAttack_strip.png", 22, 1, 144, 64, "knight_attack", 32f);
            ConfigureGrid(KnightSpriteFolder + "/noBKG_KnightDeath_strip.png", 15, 1, 96, 64, "knight_death", 32f);
            ConfigureGrid(KnightSpriteFolder + "/noBKG_KnightJumpAndFall_strip.png", 15, 1, 144, 64, "knight_jump_fall", 32f);
            ConfigureGrid(KnightSpriteFolder + "/noBKG_KnightRoll_strip.png", 15, 1, 180, 64, "knight_roll", 32f);
            ConfigureGrid(KnightSpriteFolder + "/noBKG_KnightRun_strip.png", 8, 1, 96, 64, "knight_run", 32f);
            ConfigureGrid(KnightSpriteFolder + "/noBKG_KnightShield_strip.png", 7, 1, 96, 64, "knight_shield", 32f);
            ConfigureGrid(IconsPath, 9, 13, 32, 32, "ui_icon", 32f, new Vector2(0.5f, 0.5f));
            ConfigureUiAtlas();
        }

        private static void ConfigureGrid(string path, int columns, int rows, int width, int height,
            string prefix, float pixelsPerUnit, Vector2? pivotOverride = null)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Texture mancante: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            Vector2 pivot = pivotOverride ?? new Vector2(0.5f, 0f);
            List<SpriteRect> sprites = new List<SpriteRect>();
            int logicalIndex = 0;
            for (int rowFromTop = 0; rowFromTop < rows; rowFromTop++)
            {
                int y = (rows - rowFromTop - 1) * height;
                for (int column = 0; column < columns; column++)
                {
                    sprites.Add(new SpriteRect
                    {
                        name = prefix + "_" + logicalIndex.ToString("D3"),
                        rect = new Rect(column * width, y, width, height),
                        alignment = SpriteAlignment.Custom,
                        pivot = pivot,
                        border = Vector4.zero
                    });
                    logicalIndex++;
                }
            }
            importer.SaveAndReimport();
            ApplySpriteRects(path, sprites);
        }

        private static void ConfigureUiAtlas()
        {
            TextureImporter importer = AssetImporter.GetAtPath(UiAtlasPath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Atlas UI mancante: " + UiAtlasPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 32f;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            SpriteRect[] sprites =
            {
                UiSprite("ui_button_orange", 238, 363, 85, 21, new Vector4(12, 8, 12, 8)),
                UiSprite("ui_button_blue", 349, 363, 85, 21, new Vector4(12, 8, 12, 8)),
                UiSprite("ui_panel_corner", 481, 320, 64, 64, new Vector4(12, 12, 12, 12))
            };
            importer.SaveAndReimport();
            ApplySpriteRects(UiAtlasPath, sprites);
        }

        private static SpriteRect UiSprite(string name, float x, float y, float width, float height, Vector4 border)
        {
            return new SpriteRect
            {
                name = name,
                rect = new Rect(x, y, width, height),
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
                border = border
            };
        }

        private static void ApplySpriteRects(string path, IEnumerable<SpriteRect> requestedRects)
        {
            SpriteDataProviderFactories factories = new SpriteDataProviderFactories();
            factories.Init();
            ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(
                AssetImporter.GetAtPath(path));
            provider.InitSpriteEditorDataProvider();

            Dictionary<string, UnityEngine.GUID> existingIds = provider.GetSpriteRects()
                .Where(rect => !string.IsNullOrWhiteSpace(rect.name))
                .GroupBy(rect => rect.name)
                .ToDictionary(group => group.Key, group => group.First().spriteID, StringComparer.Ordinal);
            SpriteRect[] rects = requestedRects.ToArray();
            foreach (SpriteRect rect in rects)
            {
                rect.spriteID = existingIds.TryGetValue(rect.name, out UnityEngine.GUID existing)
                    ? existing
                    : UnityEngine.GUID.Generate();
            }

            provider.SetSpriteRects(rects);
            ISpriteNameFileIdDataProvider names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (names != null)
            {
                names.SetNameFileIdPairs(rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
            }
            provider.Apply();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        private static void ConfigureAudio()
        {
            string[] paths =
            {
                SelectSfxPath,
                "Assets/_Veyra/Audio/SFX/UI/UserProvided/confirmation.wav",
                "Assets/_Veyra/Audio/SFX/UI/UserProvided/save.wav",
                "Assets/_Veyra/Audio/SFX/UI/UserProvided/error.wav"
            };
            foreach (string path in paths)
            {
                AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null) continue;
                importer.forceToMono = true;
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.CompressedInMemory;
                settings.compressionFormat = AudioCompressionFormat.ADPCM;
                settings.quality = 0.72f;
                settings.preloadAudioData = true;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
        }

        private static void CreateTheme()
        {
            VeyraThemePalette theme = AssetDatabase.LoadAssetAtPath<VeyraThemePalette>(ThemePath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<VeyraThemePalette>();
                AssetDatabase.CreateAsset(theme, ThemePath);
            }
            EditorUtility.SetDirty(theme);
        }

        private static RuntimeAnimatorController CreateHeroAnimations()
        {
            AnimationClip idle = CreateClip(HeroAnimationFolder + "/ANIM_Hero01_Idle.anim",
                LoadSprites(HeroIdlePath), 10f, true);
            AnimationClip walk = CreateClip(HeroAnimationFolder + "/ANIM_Hero01_Walk.anim",
                LoadSprites(HeroWalkPath).Take(4), 9f, true);
            AnimationClip attack = CreateClip(HeroAnimationFolder + "/ANIM_Hero01_Attack.anim",
                LoadSprites(HeroAttackPath), 16f, false);
            AnimationClip technique = CreateClip(HeroAnimationFolder + "/ANIM_Hero01_Technique.anim",
                LoadSprites(HeroTechniquePath), 18f, false);
            return CreateHeroController(
                HeroAnimationFolder + "/AC_Hero01_Combat.controller",
                idle,
                walk,
                attack,
                technique);
        }

        private static RuntimeAnimatorController CreateHeroController(
            string path,
            AnimationClip idle,
            AnimationClip walk,
            AnimationClip attack,
            AnimationClip technique)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            EnsureParameter(controller, "Moving", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "Attack", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Technique", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState idleState = FindOrCreateState(machine, "Idle");
            AnimatorState walkState = FindOrCreateState(machine, "Walk");
            AnimatorState attackState = FindOrCreateState(machine, "Attack");
            AnimatorState techniqueState = FindOrCreateState(machine, "Technique");
            idleState.motion = idle;
            walkState.motion = walk;
            attackState.motion = attack;
            techniqueState.motion = technique;
            machine.defaultState = idleState;

            EnsureTransition(idleState, walkState, AnimatorConditionMode.If);
            EnsureTransition(walkState, idleState, AnimatorConditionMode.IfNot);
            EnsureTriggerTransition(machine, attackState, "Attack");
            EnsureTriggerTransition(machine, techniqueState, "Technique");
            EnsureExitTransition(attackState, idleState);
            EnsureExitTransition(techniqueState, idleState);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void EnsureParameter(
            AnimatorController controller,
            string name,
            AnimatorControllerParameterType type)
        {
            if (!controller.parameters.Any(parameter => parameter.name == name))
            {
                controller.AddParameter(name, type);
            }
        }

        private static void EnsureTriggerTransition(
            AnimatorStateMachine machine,
            AnimatorState destination,
            string trigger)
        {
            if (machine.anyStateTransitions.Any(transition =>
                    transition.destinationState == destination &&
                    transition.conditions.Any(condition => condition.parameter == trigger)))
            {
                return;
            }

            AnimatorStateTransition created = machine.AddAnyStateTransition(destination);
            created.hasExitTime = false;
            created.duration = 0.03f;
            created.canTransitionToSelf = false;
            created.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        }

        private static void EnsureExitTransition(AnimatorState source, AnimatorState destination)
        {
            if (source.transitions.Any(transition => transition.destinationState == destination)) return;
            AnimatorStateTransition created = source.AddTransition(destination);
            created.hasExitTime = true;
            created.exitTime = 0.96f;
            created.duration = 0.04f;
        }

        private static RuntimeAnimatorController CreateKnightAnimations()
        {
            AnimationClip idle = CreateClip(KnightAnimationFolder + "/ANIM_Knight_Idle.anim",
                LoadSprites(KnightIdlePath), 12f, true);
            AnimationClip attack = CreateClip(KnightAnimationFolder + "/ANIM_Knight_Attack.anim",
                LoadSprites(KnightSpriteFolder + "/noBKG_KnightAttack_strip.png"), 18f, false);
            CreateClip(KnightAnimationFolder + "/ANIM_Knight_Death.anim",
                LoadSprites(KnightSpriteFolder + "/noBKG_KnightDeath_strip.png"), 12f, false);
            CreateClip(KnightAnimationFolder + "/ANIM_Knight_JumpFall.anim",
                LoadSprites(KnightSpriteFolder + "/noBKG_KnightJumpAndFall_strip.png"), 15f, false);
            CreateClip(KnightAnimationFolder + "/ANIM_Knight_Roll.anim",
                LoadSprites(KnightSpriteFolder + "/noBKG_KnightRoll_strip.png"), 18f, false);
            CreateClip(KnightAnimationFolder + "/ANIM_Knight_Run.anim",
                LoadSprites(KnightSpriteFolder + "/noBKG_KnightRun_strip.png"), 12f, true);
            CreateClip(KnightAnimationFolder + "/ANIM_Knight_Shield.anim",
                LoadSprites(KnightSpriteFolder + "/noBKG_KnightShield_strip.png"), 12f, false);
            return CreateController(KnightAnimationFolder + "/AC_Knight_Combat.controller", idle, attack, "Attack");
        }

        private static AnimationClip CreateClip(string path, IEnumerable<Sprite> frames, float frameRate, bool loop)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip { name = System.IO.Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(clip, path);
            }
            clip.frameRate = frameRate;
            Sprite[] spriteFrames = frames.Where(frame => frame != null).ToArray();
            ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[spriteFrames.Length];
            for (int index = 0; index < spriteFrames.Length; index++)
            {
                keys[index] = new ObjectReferenceKeyframe { time = index / frameRate, value = spriteFrames[index] };
            }
            EditorCurveBinding binding = EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite");
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static RuntimeAnimatorController CreateController(string path, AnimationClip idle, AnimationClip movement, string movementName)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            if (!controller.parameters.Any(parameter => parameter.name == "Moving"))
                controller.AddParameter("Moving", AnimatorControllerParameterType.Bool);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState idleState = FindOrCreateState(machine, "Idle");
            AnimatorState moveState = FindOrCreateState(machine, movementName);
            idleState.motion = idle;
            moveState.motion = movement;
            machine.defaultState = idleState;
            EnsureTransition(idleState, moveState, AnimatorConditionMode.If);
            EnsureTransition(moveState, idleState, AnimatorConditionMode.IfNot);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static AnimatorState FindOrCreateState(AnimatorStateMachine machine, string name)
        {
            foreach (ChildAnimatorState child in machine.states)
                if (child.state.name == name) return child.state;
            return machine.AddState(name);
        }

        private static void EnsureTransition(AnimatorState source, AnimatorState destination, AnimatorConditionMode mode)
        {
            if (source.transitions.Any(transition => transition.destinationState == destination)) return;
            AnimatorStateTransition created = source.AddTransition(destination);
            created.hasExitTime = false;
            created.duration = 0.04f;
            created.AddCondition(mode, 0f, "Moving");
        }

        private static void CreateActorPrefab(string path, string prefabName, string spritePath,
            RuntimeAnimatorController controller, int sortingOrder)
        {
            Sprite first = LoadSprites(spritePath).FirstOrDefault();
            if (first == null) throw new InvalidOperationException("Nessun frame importato da " + spritePath);
            GameObject root = new GameObject(prefabName, typeof(SpriteRenderer), typeof(Animator), typeof(CombatSpriteAnimatorBridge));
            SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
            renderer.sprite = first;
            renderer.sortingOrder = sortingOrder;
            Animator animator = root.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            root.GetComponent<CombatSpriteAnimatorBridge>().Configure(animator);
            if (prefabName.IndexOf("Hero01", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                root.AddComponent<HeroCombatPresentation>();
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static Sprite[] LoadSprites(string path)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                .OrderBy(sprite => sprite.name, StringComparer.Ordinal).ToArray();
        }
    }
}
#endif
