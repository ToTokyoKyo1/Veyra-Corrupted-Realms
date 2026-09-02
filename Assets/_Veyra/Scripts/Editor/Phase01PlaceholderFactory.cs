#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Veyra.Editor
{
    internal static class Phase01PlaceholderFactory
    {
        internal const string HeroSpritePath =
            "Assets/_Veyra/Art/Sprites/Characters/Hero01/SPR_Hero01_Placeholder.png";
        internal const string EnemySpritePath =
            "Assets/_Veyra/Art/Sprites/Enemies/World01/Enemy01/SPR_W01_Enemy01_Placeholder.png";
        internal const string BackgroundSpritePath =
            "Assets/_Veyra/Art/Sprites/Environment/World01/SPR_W01_Background_Placeholder.png";
        internal const string HeroPrefabPath =
            "Assets/_Veyra/Prefabs/Characters/Hero01/PF_Hero01_UserProvided.prefab";
        internal const string EnemyPrefabPath =
            "Assets/_Veyra/Prefabs/Enemies/World01/Enemy01/PF_W01_Enemy01_Placeholder.prefab";

        internal static void CreateAssets(Phase01SetupReport report)
        {
            CreateSpriteIfMissing(
                HeroSpritePath,
                64,
                96,
                DrawHero,
                32f,
                new Vector2(0.5f, 0f),
                report);

            CreateSpriteIfMissing(
                EnemySpritePath,
                72,
                88,
                DrawEnemy,
                32f,
                new Vector2(0.5f, 0f),
                report);

            CreateSpriteIfMissing(
                BackgroundSpritePath,
                108,
                192,
                DrawBackground,
                10f,
                new Vector2(0.5f, 0.5f),
                report);

            CreatePrefabIfMissing(
                HeroPrefabPath,
                "PF_Hero01_UserProvided",
                HeroSpritePath,
                10,
                report);

            CreatePrefabIfMissing(
                EnemyPrefabPath,
                "PF_W01_Enemy01_Placeholder",
                EnemySpritePath,
                20,
                report);
        }

        private static void CreateSpriteIfMissing(
            string assetPath,
            int width,
            int height,
            Action<Color32[], int, int> draw,
            float pixelsPerUnit,
            Vector2 pivot,
            Phase01SetupReport report)
        {
            if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null || File.Exists(assetPath))
            {
                report.Preserve(assetPath);
                return;
            }

            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false);

            try
            {
                Color32[] pixels = new Color32[width * height];
                draw(pixels, width, height);
                texture.SetPixels32(pixels);
                texture.Apply(false, false);

                string absolutePath = Path.GetFullPath(assetPath);
                File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"Could not obtain TextureImporter for {assetPath}.");
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.spritePivot = pivot;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;

            TextureImporterSettings textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(textureSettings);
            importer.SaveAndReimport();

            report.Create(assetPath);
        }

        private static void CreatePrefabIfMissing(
            string prefabPath,
            string prefabName,
            string spritePath,
            int sortingOrder,
            Phase01SetupReport report)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                report.Preserve(prefabPath);
                return;
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite == null)
            {
                throw new InvalidOperationException($"Sprite is missing or invalid: {spritePath}");
            }

            GameObject root = new GameObject(prefabName);

            try
            {
                SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingLayerName = "Default";
                renderer.sortingOrder = sortingOrder;
                root.transform.localScale = Vector3.one;

                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                if (savedPrefab == null)
                {
                    throw new InvalidOperationException($"Could not save prefab: {prefabPath}");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            report.Create(prefabPath);
        }

        private static void DrawHero(Color32[] pixels, int width, int height)
        {
            Clear(pixels);
            Color32 outline = new Color32(15, 30, 53, 255);
            Color32 body = new Color32(48, 188, 221, 255);
            Color32 highlight = new Color32(176, 249, 255, 255);
            Color32 accent = new Color32(255, 198, 74, 255);

            FillRect(pixels, width, 21, 10, 42, 59, outline);
            FillRect(pixels, width, 24, 13, 39, 56, body);
            FillRect(pixels, width, 17, 39, 46, 49, outline);
            FillRect(pixels, width, 20, 42, 43, 46, highlight);
            FillRect(pixels, width, 25, 59, 38, 78, outline);
            FillRect(pixels, width, 28, 61, 35, 75, highlight);
            FillRect(pixels, width, 27, 8, 31, 22, outline);
            FillRect(pixels, width, 34, 8, 38, 22, outline);
            FillRect(pixels, width, 25, 31, 39, 36, accent);
        }

        private static void DrawEnemy(Color32[] pixels, int width, int height)
        {
            Clear(pixels);
            Color32 outline = new Color32(32, 12, 45, 255);
            Color32 body = new Color32(187, 50, 99, 255);
            Color32 corruption = new Color32(119, 73, 222, 255);
            Color32 eye = new Color32(255, 229, 107, 255);

            FillRect(pixels, width, 12, 8, 59, 48, outline);
            FillRect(pixels, width, 16, 12, 55, 45, body);
            FillRect(pixels, width, 20, 43, 51, 69, outline);
            FillRect(pixels, width, 23, 46, 48, 66, corruption);
            FillRect(pixels, width, 7, 29, 20, 36, outline);
            FillRect(pixels, width, 51, 29, 64, 36, outline);
            FillRect(pixels, width, 27, 55, 31, 59, eye);
            FillRect(pixels, width, 40, 55, 44, 59, eye);
            FillRect(pixels, width, 22, 4, 29, 14, outline);
            FillRect(pixels, width, 42, 4, 49, 14, outline);
        }

        private static void DrawBackground(Color32[] pixels, int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                float t = y / (float)(height - 1);
                Color color = Color.Lerp(
                    new Color32(10, 18, 38, 255),
                    new Color32(45, 24, 68, 255),
                    t);

                for (int x = 0; x < width; x++)
                {
                    pixels[y * width + x] = color;
                }
            }

            Color32 horizon = new Color32(62, 87, 111, 255);
            Color32 ground = new Color32(18, 32, 51, 255);
            FillRect(pixels, width, 0, 42, width - 1, 46, horizon);
            FillRect(pixels, width, 0, 0, width - 1, 41, ground);

            Color32 star = new Color32(152, 211, 222, 255);
            FillRect(pixels, width, 17, 139, 19, 141, star);
            FillRect(pixels, width, 79, 157, 81, 159, star);
            FillRect(pixels, width, 53, 118, 55, 120, star);
            FillRect(pixels, width, 92, 96, 94, 98, star);
        }

        private static void Clear(Color32[] pixels)
        {
            Array.Fill(pixels, new Color32(0, 0, 0, 0));
        }

        private static void FillRect(
            Color32[] pixels,
            int width,
            int xMin,
            int yMin,
            int xMax,
            int yMax,
            Color32 color)
        {
            for (int y = yMin; y <= yMax; y++)
            {
                for (int x = xMin; x <= xMax; x++)
                {
                    pixels[y * width + x] = color;
                }
            }
        }
    }
}
#endif
