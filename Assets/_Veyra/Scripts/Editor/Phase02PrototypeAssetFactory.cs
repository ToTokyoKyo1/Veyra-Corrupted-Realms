#if UNITY_EDITOR
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Veyra.Editor
{
    internal static class Phase02PrototypeAssetFactory
    {
        internal const string FontAssetPath =
            "Assets/_Veyra/Art/Fonts/UI/Prototype/FNT_VeyraUI_Prototype.asset";
        internal const string MenuBackgroundPath =
            "Assets/_Veyra/Art/Sprites/UI/MainMenu/Prototype/SPR_MainMenuTree_Prototype.png";
        internal const string HeroCombatDotPath =
            "Assets/_Veyra/Art/Sprites/UI/Battle/Prototype/SPR_Hero01_CombatDot_Prototype.png";
        internal const string EnemyCombatDotPath =
            "Assets/_Veyra/Art/Sprites/UI/Battle/Prototype/SPR_W01_Enemy01_CombatDot_Prototype.png";
        internal const string HeroBasicProjectilePath =
            "Assets/_Veyra/Art/Sprites/UI/Battle/Prototype/SPR_HeroBasicProjectile_Prototype.png";
        internal const string HeroTechniqueProjectilePath =
            "Assets/_Veyra/Art/Sprites/UI/Battle/Prototype/SPR_HeroTechniqueProjectile_Prototype.png";
        internal const string EnemyProjectilePath =
            "Assets/_Veyra/Art/Sprites/UI/Battle/Prototype/SPR_EnemyProjectile_Prototype.png";
        internal const string GuardRingPath =
            "Assets/_Veyra/Art/Sprites/UI/Battle/Prototype/SPR_GuardRing_Prototype.png";
        internal const string MarkPulsePath =
            "Assets/_Veyra/Art/Sprites/UI/Battle/Prototype/SPR_MarkPulse_Prototype.png";

        internal const string HeroCombatDotPrefabPath =
            "Assets/_Veyra/Prefabs/UI/Battle/PF_Hero01_CombatDot_Prototype.prefab";
        internal const string EnemyCombatDotPrefabPath =
            "Assets/_Veyra/Prefabs/UI/Battle/PF_W01_Enemy01_CombatDot_Prototype.prefab";
        internal const string HeroBasicProjectilePrefabPath =
            "Assets/_Veyra/Prefabs/VFX/Combat/PF_HeroBasicProjectile_Prototype.prefab";
        internal const string HeroTechniqueProjectilePrefabPath =
            "Assets/_Veyra/Prefabs/VFX/Combat/PF_HeroTechniqueProjectile_Prototype.prefab";
        internal const string EnemyProjectilePrefabPath =
            "Assets/_Veyra/Prefabs/VFX/Combat/PF_EnemyProjectile_Prototype.prefab";
        internal const string GuardRingPrefabPath =
            "Assets/_Veyra/Prefabs/VFX/Combat/PF_GuardRing_Prototype.prefab";
        internal const string MarkPulsePrefabPath =
            "Assets/_Veyra/Prefabs/VFX/Combat/PF_MarkPulse_Prototype.prefab";

        internal static readonly string[] RequiredSpritePaths =
        {
            HeroCombatDotPath,
            EnemyCombatDotPath,
            HeroBasicProjectilePath,
            HeroTechniqueProjectilePath,
            EnemyProjectilePath,
            GuardRingPath,
            MarkPulsePath
        };

        internal static readonly string[] RequiredPrefabPaths =
        {
            HeroCombatDotPrefabPath,
            EnemyCombatDotPrefabPath,
            HeroBasicProjectilePrefabPath,
            HeroTechniqueProjectilePrefabPath,
            EnemyProjectilePrefabPath,
            GuardRingPrefabPath,
            MarkPulsePrefabPath
        };

        internal static void CreateAssets(Phase02SetupReport report)
        {
            CreateFontAssetIfMissing(report);
            CreateSpriteIfMissing(MenuBackgroundPath, 180, 320, DrawMenuBackground, report);
            CreateSpriteIfMissing(HeroCombatDotPath, 64, 64, DrawHeroDot, report);
            CreateSpriteIfMissing(EnemyCombatDotPath, 64, 64, DrawEnemyDot, report);
            CreateSpriteIfMissing(HeroBasicProjectilePath, 32, 20, DrawBasicProjectile, report);
            CreateSpriteIfMissing(HeroTechniqueProjectilePath, 48, 30, DrawTechniqueProjectile, report);
            CreateSpriteIfMissing(EnemyProjectilePath, 32, 20, DrawEnemyProjectile, report);
            CreateSpriteIfMissing(GuardRingPath, 80, 80, DrawGuardRing, report);
            CreateSpriteIfMissing(MarkPulsePath, 88, 88, DrawMarkPulse, report);

            CreateSpritePrefabIfMissing(HeroCombatDotPrefabPath, "PF_Hero01_CombatDot_Prototype", HeroCombatDotPath, 10, report);
            CreateSpritePrefabIfMissing(EnemyCombatDotPrefabPath, "PF_W01_Enemy01_CombatDot_Prototype", EnemyCombatDotPath, 10, report);
            CreateSpritePrefabIfMissing(HeroBasicProjectilePrefabPath, "PF_HeroBasicProjectile_Prototype", HeroBasicProjectilePath, 30, report);
            CreateSpritePrefabIfMissing(HeroTechniqueProjectilePrefabPath, "PF_HeroTechniqueProjectile_Prototype", HeroTechniqueProjectilePath, 30, report);
            CreateSpritePrefabIfMissing(EnemyProjectilePrefabPath, "PF_EnemyProjectile_Prototype", EnemyProjectilePath, 30, report);
            CreateSpritePrefabIfMissing(GuardRingPrefabPath, "PF_GuardRing_Prototype", GuardRingPath, 25, report);
            CreateSpritePrefabIfMissing(MarkPulsePrefabPath, "PF_MarkPulse_Prototype", MarkPulsePath, 25, report);
        }

        private static void CreateFontAssetIfMissing(Phase02SetupReport report)
        {
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath) != null)
            {
                report.Preserve(FontAssetPath);
                return;
            }

            TMP_FontAsset essentialFont = TMP_Settings.defaultFontAsset;
            if (essentialFont == null)
            {
                essentialFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            }

            if (essentialFont != null)
            {
                string sourcePath = AssetDatabase.GetAssetPath(essentialFont);
                if (string.IsNullOrEmpty(sourcePath) || !AssetDatabase.CopyAsset(sourcePath, FontAssetPath))
                {
                    throw new InvalidOperationException(
                        "Le TMP Essential Resources esistono, ma non è stato possibile creare la copia font Prototype persistente.");
                }

                TMP_FontAsset copiedFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
                if (copiedFont == null || copiedFont.material == null)
                {
                    throw new InvalidOperationException("La copia del font TMP Prototype è incompleta: " + FontAssetPath);
                }

                copiedFont.name = "FNT_VeyraUI_Prototype";
                foreach (UnityEngine.Object subAsset in AssetDatabase.LoadAllAssetRepresentationsAtPath(FontAssetPath))
                {
                    if (!subAsset.name.Contains("Prototype"))
                    {
                        subAsset.name += " Prototype";
                    }

                    EditorUtility.SetDirty(subAsset);
                }

                EditorUtility.SetDirty(copiedFont);
                AssetDatabase.SaveAssets();
                report.Create(FontAssetPath);
                return;
            }

            Font sourceFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (sourceFont == null)
            {
                throw new InvalidOperationException(
                    "Risorsa TMP essenziale mancante: Unity non espone LegacyRuntime.ttf. " +
                    "Importare manualmente le TMP Essential Resources e rilanciare il comando Phase 02.");
            }

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(sourceFont);
            if (fontAsset == null)
            {
                throw new InvalidOperationException(
                    "Impossibile creare il font TMP persistente. Importare manualmente le TMP Essential Resources " +
                    "e rilanciare il comando Phase 02.");
            }

            fontAsset.name = "FNT_VeyraUI_Prototype";
            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
            if (fontAsset.atlasTextures == null || fontAsset.atlasTextures.Length == 0 ||
                fontAsset.atlasTextures[0] == null || fontAsset.material == null)
            {
                AssetDatabase.DeleteAsset(FontAssetPath);
                throw new InvalidOperationException(
                    "Il font TMP generato non contiene atlante e materiale. Importare manualmente le TMP Essential Resources " +
                    "e rilanciare il comando Phase 02.");
            }

            AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            report.Create(FontAssetPath);
        }

        private static void CreateSpriteIfMissing(
            string assetPath,
            int width,
            int height,
            Action<Color32[], int, int> draw,
            Phase02SetupReport report)
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
                File.WriteAllBytes(Path.GetFullPath(assetPath), texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException("TextureImporter non disponibile per " + assetPath);
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32f;
            importer.spritePivot = new Vector2(0.5f, 0.5f);
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            report.Create(assetPath);
        }

        private static void CreateSpritePrefabIfMissing(
            string prefabPath,
            string prefabName,
            string spritePath,
            int sortingOrder,
            Phase02SetupReport report)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                report.Preserve(prefabPath);
                return;
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite == null)
            {
                throw new InvalidOperationException("Sprite mancante per il prefab: " + spritePath);
            }

            GameObject root = new GameObject(prefabName, typeof(SpriteRenderer));
            try
            {
                SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingLayerName = "Default";
                renderer.sortingOrder = sortingOrder;
                if (PrefabUtility.SaveAsPrefabAsset(root, prefabPath) == null)
                {
                    throw new InvalidOperationException("Impossibile salvare il prefab: " + prefabPath);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            report.Create(prefabPath);
        }

        private static void DrawMenuBackground(Color32[] pixels, int width, int height)
        {
            Color32 background = new Color32(11, 23, 21, 255);
            Color32 highlighted = new Color32(29, 55, 49, 255);
            Color32 corruption = new Color32(143, 74, 199, 255);
            Color32 cyan = new Color32(89, 215, 208, 255);
            Color32 light = new Color32(185, 255, 240, 255);
            Color32 tree = new Color32(20, 38, 34, 255);

            for (int y = 0; y < height; y++)
            {
                float vertical = y / (float)(height - 1);
                Color32 row = Color32.Lerp(background, highlighted, vertical * 0.45f);
                for (int x = 0; x < width; x++)
                {
                    float edge = Mathf.Max(Mathf.InverseLerp(width * 0.36f, 0f, x), Mathf.InverseLerp(width * 0.64f, width, x));
                    pixels[y * width + x] = Color32.Lerp(row, corruption, Mathf.Clamp01(edge * 0.34f));
                }
            }

            FillRect(pixels, width, 76, 34, 105, 244, tree);
            FillCircle(pixels, width, height, 90, 236, 50, tree);
            FillCircle(pixels, width, height, 53, 250, 34, tree);
            FillCircle(pixels, width, height, 126, 258, 38, tree);
            DrawLine(pixels, width, height, 89, 30, 91, 286, 3, cyan);
            DrawLine(pixels, width, height, 91, 118, 116, 178, 2, light);
            DrawLine(pixels, width, height, 90, 190, 68, 247, 2, cyan);
        }

        private static void DrawHeroDot(Color32[] pixels, int width, int height)
        {
            Clear(pixels);
            FillCircle(pixels, width, height, 32, 32, 28, new Color32(18, 50, 52, 255));
            FillCircle(pixels, width, height, 32, 32, 23, new Color32(89, 215, 208, 255));
            FillCircle(pixels, width, height, 25, 41, 7, new Color32(185, 255, 240, 255));
        }

        private static void DrawEnemyDot(Color32[] pixels, int width, int height)
        {
            Clear(pixels);
            FillCircle(pixels, width, height, 32, 32, 28, new Color32(55, 16, 26, 255));
            FillCircle(pixels, width, height, 32, 32, 23, new Color32(232, 92, 101, 255));
            FillCircle(pixels, width, height, 39, 41, 7, new Color32(143, 74, 199, 255));
        }

        private static void DrawBasicProjectile(Color32[] pixels, int width, int height)
        {
            Clear(pixels);
            FillEllipse(pixels, width, height, width / 2, height / 2, 13, 6, new Color32(89, 215, 208, 255));
            FillEllipse(pixels, width, height, width / 2 + 3, height / 2, 6, 3, new Color32(185, 255, 240, 255));
        }

        private static void DrawTechniqueProjectile(Color32[] pixels, int width, int height)
        {
            Clear(pixels);
            FillEllipse(pixels, width, height, width / 2, height / 2, 20, 10, new Color32(89, 215, 208, 255));
            FillEllipse(pixels, width, height, width / 2 + 4, height / 2, 12, 6, new Color32(185, 255, 240, 255));
        }

        private static void DrawEnemyProjectile(Color32[] pixels, int width, int height)
        {
            Clear(pixels);
            FillEllipse(pixels, width, height, width / 2, height / 2, 13, 6, new Color32(232, 92, 101, 255));
            FillEllipse(pixels, width, height, width / 2 - 3, height / 2, 5, 3, new Color32(255, 188, 192, 255));
        }

        private static void DrawGuardRing(Color32[] pixels, int width, int height)
        {
            Clear(pixels);
            DrawRing(pixels, width, height, width / 2, height / 2, 34, 27, new Color32(89, 215, 208, 255));
        }

        private static void DrawMarkPulse(Color32[] pixels, int width, int height)
        {
            Clear(pixels);
            DrawRing(pixels, width, height, width / 2, height / 2, 39, 33, new Color32(143, 74, 199, 235));
            DrawRing(pixels, width, height, width / 2, height / 2, 27, 23, new Color32(89, 215, 208, 235));
        }

        private static void Clear(Color32[] pixels)
        {
            Array.Fill(pixels, new Color32(0, 0, 0, 0));
        }

        private static void FillRect(Color32[] pixels, int width, int xMin, int yMin, int xMax, int yMax, Color32 color)
        {
            int height = pixels.Length / width;
            for (int y = Mathf.Max(0, yMin); y <= Mathf.Min(height - 1, yMax); y++)
            {
                for (int x = Mathf.Max(0, xMin); x <= Mathf.Min(width - 1, xMax); x++)
                {
                    pixels[y * width + x] = color;
                }
            }
        }

        private static void FillCircle(Color32[] pixels, int width, int height, int centerX, int centerY, int radius, Color32 color)
        {
            int squaredRadius = radius * radius;
            for (int y = Mathf.Max(0, centerY - radius); y <= Mathf.Min(height - 1, centerY + radius); y++)
            {
                for (int x = Mathf.Max(0, centerX - radius); x <= Mathf.Min(width - 1, centerX + radius); x++)
                {
                    int dx = x - centerX;
                    int dy = y - centerY;
                    if (dx * dx + dy * dy <= squaredRadius)
                    {
                        pixels[y * width + x] = color;
                    }
                }
            }
        }

        private static void FillEllipse(Color32[] pixels, int width, int height, int centerX, int centerY, int radiusX, int radiusY, Color32 color)
        {
            for (int y = Mathf.Max(0, centerY - radiusY); y <= Mathf.Min(height - 1, centerY + radiusY); y++)
            {
                for (int x = Mathf.Max(0, centerX - radiusX); x <= Mathf.Min(width - 1, centerX + radiusX); x++)
                {
                    float dx = (x - centerX) / (float)radiusX;
                    float dy = (y - centerY) / (float)radiusY;
                    if (dx * dx + dy * dy <= 1f)
                    {
                        pixels[y * width + x] = color;
                    }
                }
            }
        }

        private static void DrawRing(Color32[] pixels, int width, int height, int centerX, int centerY, int outerRadius, int innerRadius, Color32 color)
        {
            int outerSquared = outerRadius * outerRadius;
            int innerSquared = innerRadius * innerRadius;
            for (int y = Mathf.Max(0, centerY - outerRadius); y <= Mathf.Min(height - 1, centerY + outerRadius); y++)
            {
                for (int x = Mathf.Max(0, centerX - outerRadius); x <= Mathf.Min(width - 1, centerX + outerRadius); x++)
                {
                    int dx = x - centerX;
                    int dy = y - centerY;
                    int distance = dx * dx + dy * dy;
                    if (distance <= outerSquared && distance >= innerSquared)
                    {
                        pixels[y * width + x] = color;
                    }
                }
            }
        }

        private static void DrawLine(Color32[] pixels, int width, int height, int x0, int y0, int x1, int y1, int radius, Color32 color)
        {
            int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
            for (int index = 0; index <= steps; index++)
            {
                float t = steps == 0 ? 0f : index / (float)steps;
                FillCircle(pixels, width, height, Mathf.RoundToInt(Mathf.Lerp(x0, x1, t)), Mathf.RoundToInt(Mathf.Lerp(y0, y1, t)), radius, color);
            }
        }
    }
}
#endif
