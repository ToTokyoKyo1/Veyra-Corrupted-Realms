#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Veyra.Editor
{
    public static class Phase03TutorialSetup
    {
        private const string MenuPath = "Tools/Veyra/Tutorial/Create First Battle Tutorial";

        [MenuItem(MenuPath, priority = 300)]
        public static void CreateFirstBattleTutorial()
        {
            Phase03TutorialSetupReport report = new Phase03TutorialSetupReport();

            try
            {
                Phase03TutorialSceneFactory.CreateOrUpdateTutorialScene(report);
                Phase78ExistingSceneUpgrade.UpgradeTutorialOnly();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                report.LogSummary();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError(
                    "[Veyra Tutorial] Generazione interrotta. Gli oggetti esterni ai root Phase 03 sono stati preservati; " +
                    "correggere l'errore e rilanciare manualmente il comando.");
                throw;
            }
        }
    }

    internal sealed class Phase03TutorialSetupReport
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
                "[Veyra Tutorial] Primo livello tutorial generato.\n" +
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
