#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Veyra.Editor
{
    public static class Phase046CampaignSetup
    {
        private const string MenuPath = "Tools/Veyra/Campaign/Create Encounters 02-03";

        [MenuItem(MenuPath, priority = 400)]
        public static void CreateEncounters02And03()
        {
            Phase046CampaignSetupReport report = new Phase046CampaignSetupReport();

            try
            {
                Phase046EncounterSceneFactory.CreateOrUpdateCampaign(report);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                report.LogSummary();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError(
                    "[Veyra Campaign] Generazione interrotta. Le scene preesistenti e gli oggetti esterni ai root di proprietà della campagna sono stati preservati.");
                throw;
            }
        }
    }

    internal sealed class Phase046CampaignSetupReport
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
                "[Veyra Campaign] Fasi 04-06 generate.\n" +
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
