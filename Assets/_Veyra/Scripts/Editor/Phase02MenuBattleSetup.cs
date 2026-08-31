#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using TMPro.EditorUtilities;
using UnityEditor;
using UnityEngine;

namespace Veyra.Editor
{
    public static class Phase02MenuBattleSetup
    {
        private const string CreateMenuPath = "Tools/Veyra/Phase 02/Create Main Menu And Battle Preview";
        private static bool importingTmpResources;
        private static double tmpImportStartedAt;

        private static readonly string[] RequiredPhase01Assets =
        {
            "Assets/_Veyra/Scripts/Editor/Phase01ProjectSetup.cs",
            "Assets/_Veyra/Scenes/SCN_BattlePrototype.unity",
            Phase01PlaceholderFactory.HeroPrefabPath,
            Phase01PlaceholderFactory.EnemyPrefabPath,
            Phase01PlaceholderFactory.HeroSpritePath,
            Phase01PlaceholderFactory.EnemySpritePath
        };

        private static readonly string[] RequiredFolders =
        {
            "Assets/_Veyra/Art/Fonts/UI/Prototype",
            "Assets/_Veyra/Art/Sprites/UI/MainMenu/Prototype",
            "Assets/_Veyra/Art/Sprites/UI/Settings/Prototype",
            "Assets/_Veyra/Art/Sprites/UI/Battle/Prototype",
            "Assets/_Veyra/Animations/UI/MainMenu",
            "Assets/_Veyra/Prefabs/UI/MainMenu",
            "Assets/_Veyra/Prefabs/UI/Settings",
            "Assets/_Veyra/Prefabs/UI/Battle",
            "Assets/_Veyra/Prefabs/VFX/Combat",
            "Assets/_Veyra/Scripts/Runtime/UI/MainMenu",
            "Assets/_Veyra/Scripts/Runtime/UI/Settings",
            "Assets/_Veyra/Scripts/Runtime/Combat/Preview"
        };

        [MenuItem(CreateMenuPath, priority = 200)]
        public static void CreateMainMenuAndBattlePreview()
        {
            Phase02SetupReport report = new Phase02SetupReport();

            try
            {
                VerifyPhase01Prerequisites();
                foreach (string folder in RequiredFolders)
                {
                    EnsureFolder(folder, report);
                }

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Phase02PrototypeAssetFactory.CreateAssets(report);
                Phase02SceneFactory.CreateScenes(report);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                report.LogSummary();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError(
                    "[Veyra Phase 02] Generazione interrotta. Gli asset esistenti sono stati preservati; " +
                    "correggere l'errore e rilanciare manualmente il comando.");
                throw;
            }
        }

        [MenuItem("Tools/Veyra/Phase 02/Import TMP Essential Resources", priority = 199)]
        public static void ImportTmpEssentialResources()
        {
            string packagePath = TMP_EditorUtility.packageFullPath +
                "/Package Resources/TMP Essential Resources.unitypackage";
            if (!File.Exists(packagePath))
            {
                throw new FileNotFoundException("Pacchetto TMP Essential Resources non trovato.", packagePath);
            }

            AssetDatabase.importPackageCompleted -= OnTmpImportCompleted;
            AssetDatabase.importPackageCompleted += OnTmpImportCompleted;
            AssetDatabase.importPackageFailed -= OnTmpImportFailed;
            AssetDatabase.importPackageFailed += OnTmpImportFailed;
            EditorApplication.update -= MonitorTmpImport;
            EditorApplication.update += MonitorTmpImport;
            importingTmpResources = true;
            tmpImportStartedAt = EditorApplication.timeSinceStartup;
            AssetDatabase.ImportPackage(packagePath, false);
            Debug.Log(
                "[Veyra Phase 02] Importazione ufficiale TMP Essential Resources richiesta. " +
                "Attendere il completamento e rilanciare Create Main Menu And Battle Preview.");
        }

        private static void OnTmpImportCompleted(string packageName)
        {
            if (!packageName.Contains("TMP Essential Resources"))
            {
                return;
            }

            FinishTmpImport(true, "TMP Essential Resources importate correttamente.");
        }

        private static void OnTmpImportFailed(string packageName, string errorMessage)
        {
            if (!packageName.Contains("TMP Essential Resources"))
            {
                return;
            }

            FinishTmpImport(false, "Importazione TMP Essential Resources fallita: " + errorMessage);
        }

        private static void MonitorTmpImport()
        {
            if (importingTmpResources && EditorApplication.timeSinceStartup - tmpImportStartedAt > 90d)
            {
                FinishTmpImport(false, "Timeout durante l'importazione TMP Essential Resources.");
            }
        }

        private static void FinishTmpImport(bool succeeded, string message)
        {
            importingTmpResources = false;
            AssetDatabase.importPackageCompleted -= OnTmpImportCompleted;
            AssetDatabase.importPackageFailed -= OnTmpImportFailed;
            EditorApplication.update -= MonitorTmpImport;
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            if (succeeded)
            {
                Debug.Log("[Veyra Phase 02] " + message);
            }
            else
            {
                Debug.LogError("[Veyra Phase 02] " + message);
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(succeeded ? 0 : 1);
            }
        }

        internal static void VerifyPhase01Prerequisites()
        {
            List<string> missing = RequiredPhase01Assets
                .Where(path => AssetDatabase.LoadMainAssetAtPath(path) == null && !File.Exists(path))
                .ToList();

            if (!AssetDatabase.IsValidFolder("Assets/_Veyra"))
            {
                missing.Insert(0, "Assets/_Veyra/");
            }

            string documentationPath = Path.GetFullPath("Docs/PROJECT_STRUCTURE.md");
            if (!File.Exists(documentationPath))
            {
                missing.Add("Docs/PROJECT_STRUCTURE.md");
            }

            if (missing.Count > 0)
            {
                throw new InvalidOperationException(
                    "La Fase 1 non è salvata correttamente. Elementi mancanti:\n- " +
                    string.Join("\n- ", missing) +
                    "\nEseguire Tools > Veyra > Phase 01 > Create Project Foundation e salvare il progetto.");
            }
        }

        internal static void EnsureFolder(string folder, Phase02SetupReport report)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                report.Preserve(folder);
                return;
            }

            string[] segments = folder.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                    report.Create(next);
                }

                current = next;
            }
        }
    }

    internal sealed class Phase02SetupReport
    {
        private readonly List<string> created = new List<string>();
        private readonly List<string> preserved = new List<string>();
        private readonly List<string> configured = new List<string>();
        private readonly List<string> warnings = new List<string>();

        internal void Create(string item) => created.Add(item);
        internal void Preserve(string item) => preserved.Add(item);
        internal void Configure(string item) => configured.Add(item);
        internal void Warn(string item) => warnings.Add(item);

        internal void LogSummary()
        {
            string summary =
                "[Veyra Phase 02] Menu e battle preview generati.\n" +
                Format("Creati", created) +
                Format("Preservati", preserved) +
                Format("Configurati", configured) +
                Format("Avvisi", warnings);

            if (warnings.Count > 0)
            {
                Debug.LogWarning(summary);
            }
            else
            {
                Debug.Log(summary);
            }
        }

        private static string Format(string title, IReadOnlyCollection<string> items)
        {
            string body = items.Count == 0
                ? "  - Nessuno"
                : string.Join("\n", items.OrderBy(item => item).Select(item => "  - " + item));
            return $"{title} ({items.Count}):\n{body}\n";
        }
    }
}
#endif
