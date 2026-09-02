#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Veyra.Combat;
using Veyra.UI;

namespace Veyra.Editor
{
    public static class VeyraVisualAssetValidator
    {
        private static readonly string[] BattleScenes =
        {
            Phase02SceneFactory.TutorialScenePath,
            Phase046EncounterSceneFactory.Level02ScenePath,
            Phase046EncounterSceneFactory.Level03ScenePath,
            Phase78ProgressionSceneFactory.Level04Path
        };

        private static readonly string[] RequiredClips =
        {
            "Assets/_Veyra/Animations/Characters/Hero01/UserProvided/ANIM_Hero01_Idle.anim",
            "Assets/_Veyra/Animations/Characters/Hero01/UserProvided/ANIM_Hero01_Walk.anim",
            "Assets/_Veyra/Animations/Enemies/World01/Knight/UserProvided/ANIM_Knight_Idle.anim",
            "Assets/_Veyra/Animations/Enemies/World01/Knight/UserProvided/ANIM_Knight_Attack.anim",
            "Assets/_Veyra/Animations/Enemies/World01/Knight/UserProvided/ANIM_Knight_Death.anim",
            "Assets/_Veyra/Animations/Enemies/World01/Knight/UserProvided/ANIM_Knight_JumpFall.anim",
            "Assets/_Veyra/Animations/Enemies/World01/Knight/UserProvided/ANIM_Knight_Roll.anim",
            "Assets/_Veyra/Animations/Enemies/World01/Knight/UserProvided/ANIM_Knight_Run.anim",
            "Assets/_Veyra/Animations/Enemies/World01/Knight/UserProvided/ANIM_Knight_Shield.anim"
        };

        [MenuItem("Tools/Veyra/QA/Validate Visual Integration", priority = 610)]
        public static void ValidateVisualIntegration()
        {
            List<string> errors = new List<string>();
            ValidateTexture(VeyraVisualAssetSetup.HeroIdlePath, 10, errors);
            ValidateTexture(VeyraVisualAssetSetup.HeroWalkPath, 24, errors);
            ValidateTexture(VeyraVisualAssetSetup.IconsPath, 117, errors);
            ValidateTexture(VeyraVisualAssetSetup.UiAtlasPath, 3, errors);
            ValidatePrefab(VeyraVisualAssetSetup.HeroPrefabPath, errors);
            ValidatePrefab(VeyraVisualAssetSetup.KnightPrefabPath, errors);
            ValidateTheme(errors);
            ValidateAudio(errors);
            ValidateClips(errors);
            ValidateSceneReferences(errors);

            if (errors.Count == 0)
            {
                Debug.Log("[Veyra Visual Validation] PASS - asset, importazioni, prefab, animazioni, audio e riferimenti scene sono coerenti.");
                return;
            }

            foreach (string error in errors)
            {
                Debug.LogError("[Veyra Visual Validation] " + error);
            }
            throw new InvalidOperationException("Veyra visual validation failed with " + errors.Count + " error(s).");
        }

        private static void ValidateTexture(string path, int spriteCount, List<string> errors)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                errors.Add("Texture mancante: " + path);
                return;
            }
            if (importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Multiple ||
                importer.filterMode != FilterMode.Point ||
                importer.wrapMode != TextureWrapMode.Clamp ||
                importer.mipmapEnabled ||
                importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                errors.Add("Importer pixel-art non coerente: " + path);
            }
            int actual = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Count();
            if (actual != spriteCount)
            {
                errors.Add(path + " contiene " + actual + " sprite invece di " + spriteCount + ".");
            }
        }

        private static void ValidatePrefab(string path, List<string> errors)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                errors.Add("Prefab mancante: " + path);
                return;
            }
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                SpriteRenderer renderer = contents.GetComponent<SpriteRenderer>();
                Animator animator = contents.GetComponent<Animator>();
                CombatSpriteAnimatorBridge bridge = contents.GetComponent<CombatSpriteAnimatorBridge>();
                if (renderer == null || renderer.sprite == null || animator == null ||
                    animator.runtimeAnimatorController == null || bridge == null)
                {
                    errors.Add("Prefab attore incompleto: " + path);
                }
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(contents) > 0)
                {
                    errors.Add("Prefab con script mancante: " + path);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void ValidateTheme(List<string> errors)
        {
            VeyraThemePalette theme = AssetDatabase.LoadAssetAtPath<VeyraThemePalette>(VeyraVisualAssetSetup.ThemePath);
            if (theme == null)
            {
                errors.Add("Theme palette mancante.");
                return;
            }
            CheckColor("background", theme.background, new Color32(9, 11, 21, 255), errors);
            CheckColor("panel", theme.panel, new Color32(20, 24, 46, 255), errors);
            CheckColor("secondaryPanel", theme.secondaryPanel, new Color32(44, 53, 77, 255), errors);
            CheckColor("primaryText", theme.primaryText, new Color32(245, 255, 232, 255), errors);
            CheckColor("secondaryText", theme.secondaryText, new Color32(163, 167, 194, 255), errors);
            CheckColor("information", theme.information, new Color32(146, 232, 192, 255), errors);
            CheckColor("action", theme.action, new Color32(255, 174, 112, 255), errors);
            CheckColor("danger", theme.danger, new Color32(173, 47, 69, 255), errors);
            CheckColor("corruption", theme.corruption, new Color32(105, 36, 100, 255), errors);
        }

        private static void CheckColor(string name, Color actual, Color32 expected, List<string> errors)
        {
            Color32 value = actual;
            if (value.r != expected.r || value.g != expected.g || value.b != expected.b || value.a != expected.a)
            {
                errors.Add("Colore theme non coerente: " + name + ".");
            }
        }

        private static void ValidateAudio(List<string> errors)
        {
            AudioImporter importer = AssetImporter.GetAtPath(VeyraVisualAssetSetup.SelectSfxPath) as AudioImporter;
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(VeyraVisualAssetSetup.SelectSfxPath);
            if (importer == null || clip == null)
            {
                errors.Add("SFX UI select mancante.");
                return;
            }
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            if (!importer.forceToMono || settings.loadType != AudioClipLoadType.CompressedInMemory ||
                settings.compressionFormat != AudioCompressionFormat.ADPCM || !settings.preloadAudioData)
            {
                errors.Add("Importer audio UI non coerente.");
            }
        }

        private static void ValidateClips(List<string> errors)
        {
            foreach (string path in RequiredClips)
            {
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null || clip.empty)
                {
                    errors.Add("AnimationClip mancante o vuota: " + path);
                }
            }
        }

        private static void ValidateSceneReferences(List<string> errors)
        {
            ValidateDependencies(Phase02SceneFactory.MainMenuScenePath,
                new[] { VeyraVisualAssetSetup.UiAtlasPath, VeyraVisualAssetSetup.SelectSfxPath }, errors);
            foreach (string scene in BattleScenes)
            {
                ValidateDependencies(scene, new[]
                {
                    VeyraVisualAssetSetup.HeroPrefabPath,
                    VeyraVisualAssetSetup.IconsPath,
                    VeyraVisualAssetSetup.UiAtlasPath,
                    VeyraVisualAssetSetup.SelectSfxPath
                }, errors);
            }
            ValidateDependencies(Phase78ProgressionSceneFactory.Level04Path,
                new[] { VeyraVisualAssetSetup.KnightPrefabPath }, errors);
        }

        private static void ValidateDependencies(string scene, IEnumerable<string> required, List<string> errors)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scene) == null)
            {
                errors.Add("Scena mancante: " + scene);
                return;
            }
            HashSet<string> dependencies = new HashSet<string>(AssetDatabase.GetDependencies(scene, true), StringComparer.Ordinal);
            foreach (string path in required)
            {
                if (!dependencies.Contains(path)) errors.Add(scene + " non referenzia " + path + ".");
            }
        }
    }
}
#endif
