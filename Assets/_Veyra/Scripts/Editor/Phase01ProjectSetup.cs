#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Veyra.Editor
{
    public static class Phase01ProjectSetup
    {
        private const string MenuPath = "Tools/Veyra/Phase 01/Create Project Foundation";

        private static readonly string[] RequiredFolders =
        {
            "Assets/_Veyra/Art/Sprites/Characters/Hero01",
            "Assets/_Veyra/Art/Sprites/Enemies/World01/Enemy01",
            "Assets/_Veyra/Art/Sprites/Environment/World01",
            "Assets/_Veyra/Art/Sprites/UI",
            "Assets/_Veyra/Art/Sprites/VFX",
            "Assets/_Veyra/Art/Materials",
            "Assets/_Veyra/Animations/Hero01",
            "Assets/_Veyra/Animations/Enemies/World01/Enemy01",
            "Assets/_Veyra/Animations/UI",
            "Assets/_Veyra/Audio/Music",
            "Assets/_Veyra/Audio/SFX",
            "Assets/_Veyra/Data/Heroes",
            "Assets/_Veyra/Data/Enemies/World01",
            "Assets/_Veyra/Data/Combat",
            "Assets/_Veyra/Data/Worlds/World01",
            "Assets/_Veyra/Prefabs/Characters/Hero01",
            "Assets/_Veyra/Prefabs/Enemies/World01/Enemy01",
            "Assets/_Veyra/Prefabs/Environment/World01",
            "Assets/_Veyra/Prefabs/UI",
            "Assets/_Veyra/Prefabs/VFX",
            "Assets/_Veyra/Scenes",
            "Assets/_Veyra/Scripts/Runtime/Core",
            "Assets/_Veyra/Scripts/Runtime/Combat",
            "Assets/_Veyra/Scripts/Runtime/AI",
            "Assets/_Veyra/Scripts/Runtime/Data",
            "Assets/_Veyra/Scripts/Runtime/UI",
            "Assets/_Veyra/Scripts/Editor",
            "Assets/_Veyra/Settings"
        };

        [MenuItem(MenuPath, priority = 100)]
        public static void CreateProjectFoundation()
        {
            Phase01SetupReport report = new Phase01SetupReport();

            try
            {
                CreateRequiredFolders(report);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                ConfigurePlayerSettings(report);
                Phase01PlaceholderFactory.CreateAssets(report);
                Phase01SceneFactory.CreateScene(report);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                report.LogSummary();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("[Veyra Phase 01] Setup stopped before completion. Existing assets were preserved.");
                throw;
            }
        }

        private static void CreateRequiredFolders(Phase01SetupReport report)
        {
            foreach (string folder in RequiredFolders)
            {
                EnsureFolder(folder, report);
            }
        }

        private static void EnsureFolder(string folder, Phase01SetupReport report)
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

        private static void ConfigurePlayerSettings(Phase01SetupReport report)
        {
            PlayerSettings.productName = "Veyra: Corrupted Realms";
            PlayerSettings.companyName = "TokyoKyo";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.totokyokyo.veyra");

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            report.Configure("Player Settings: product, company, Android identifier, and Portrait orientation");

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.Android,
                    BuildTarget.Android);

                if (switched)
                {
                    report.Configure("Active build target: Android");
                }
                else
                {
                    report.Warn("Android Build Support is unavailable; the active build target was not changed.");
                }
            }
            else
            {
                report.Preserve("Active build target already set to Android");
            }
        }
    }

    internal sealed class Phase01SetupReport
    {
        private readonly List<string> created = new List<string>();
        private readonly List<string> preserved = new List<string>();
        private readonly List<string> configured = new List<string>();
        private readonly List<string> warnings = new List<string>();

        public void Create(string item) => created.Add(item);
        public void Preserve(string item) => preserved.Add(item);
        public void Configure(string item) => configured.Add(item);
        public void Warn(string item) => warnings.Add(item);

        public void LogSummary()
        {
            string summary =
                "[Veyra Phase 01] Project foundation complete.\n" +
                FormatSection("Created", created) +
                FormatSection("Preserved", preserved) +
                FormatSection("Configured", configured) +
                FormatSection("Warnings", warnings);

            if (warnings.Count > 0)
            {
                Debug.LogWarning(summary);
            }
            else
            {
                Debug.Log(summary);
            }
        }

        private static string FormatSection(string title, IReadOnlyCollection<string> items)
        {
            string body = items.Count == 0
                ? "  - None"
                : string.Join("\n", items.OrderBy(item => item).Select(item => "  - " + item));

            return $"{title} ({items.Count}):\n{body}\n";
        }
    }
}
#endif

