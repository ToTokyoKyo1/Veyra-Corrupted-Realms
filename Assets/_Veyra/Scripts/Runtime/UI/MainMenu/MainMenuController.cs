using System;
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Veyra.Core;
using Veyra.Progression;
using Veyra.UI.Settings;

namespace Veyra.UI.MainMenu
{
    public sealed class MainMenuController : MonoBehaviour
    {
        [Header("Main navigation")]
        [SerializeField] private GameObject mainNavigationPanel;
        [SerializeField] private Button startButton;
        [SerializeField] private TMP_Text startButtonLabel;
        [SerializeField] private Button levelsButton;
        [SerializeField] private Button heroesButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private GameObject heroUpgradeBadge;
        [SerializeField] private TMP_Text campaignStatusText;

        [Header("Levels panel")]
        [SerializeField] private GameObject levelsPanel;
        [SerializeField] private TMP_Text completedLevelsText;
        [SerializeField] private Button[] levelButtons = new Button[10];
        [SerializeField] private TMP_Text[] levelButtonLabels = new TMP_Text[10];
        [SerializeField] private Button levelsBackButton;

        [Header("Hero01 panel")]
        [SerializeField] private GameObject heroesPanel;
        [SerializeField] private TMP_Text heroNameText;
        [SerializeField] private TMP_Text heroLevelText;
        [SerializeField] private TMP_Text heroExperienceText;
        [SerializeField] private Image heroExperienceFill;
        [SerializeField] private TMP_Text heroStatsText;
        [SerializeField] private TMP_Text heroUpgradesText;
        [SerializeField] private TMP_Text heroPointsText;
        [SerializeField] private Button heroUpgradeButton;
        [SerializeField] private Button heroesBackButton;

        [Header("Major upgrade")]
        [SerializeField] private GameObject upgradeSelectionPanel;
        [SerializeField] private Button upgradeAttackButton;
        [SerializeField] private Button upgradeGuardButton;
        [SerializeField] private Button upgradeTechniqueButton;
        [SerializeField] private Button upgradeAnalyzeButton;
        [SerializeField] private Button upgradeSelectionBackButton;
        [SerializeField] private GameObject upgradeConfirmationPanel;
        [SerializeField] private TMP_Text upgradeConfirmationTitle;
        [SerializeField] private TMP_Text upgradeConfirmationDescription;
        [SerializeField] private TMP_Text upgradeBeforeAfterText;
        [SerializeField] private Button confirmUpgradeButton;
        [SerializeField] private Button cancelUpgradeButton;

        [Header("Compatibility and settings")]
        [SerializeField] private Button replayTutorialButton;
        [SerializeField] private Button resetProgressButton;
        [SerializeField] private SettingsPanelController settingsPanel;
        [SerializeField] private GameObject resetProgressConfirmationModal;
        [SerializeField] private Button resetProgressConfirmButton;
        [SerializeField] private Button resetProgressCancelButton;

        [Header("Loading and errors")]
        [SerializeField] private GameObject loadingOverlay;
        [SerializeField] private GameObject errorModal;
        [SerializeField] private TMP_Text errorMessage;

        private bool isLoading;
        private bool returnToSettingsAfterReset;
        private HeroMajorUpgrade pendingUpgrade = HeroMajorUpgrade.None;

        public bool IsLoading => isLoading;

        private void Awake()
        {
            LocalSettingsStore.ApplyMasterVolume(LocalSettingsStore.Load().masterVolume);
            if (resetProgressConfirmationModal != null)
            {
                resetProgressConfirmationModal.SetActive(false);
            }

            if (upgradeSelectionPanel != null)
            {
                upgradeSelectionPanel.SetActive(false);
            }

            if (upgradeConfirmationPanel != null)
            {
                upgradeConfirmationPanel.SetActive(false);
            }

            RefreshCampaignState();
            MainMenuEntryPoint requestedEntryPoint = MainMenuEntryRequest.Consume();
            if (requestedEntryPoint == MainMenuEntryPoint.Heroes)
            {
                OpenHeroes();
            }
            else if (requestedEntryPoint == MainMenuEntryPoint.Levels)
            {
                OpenLevels();
            }
            else
            {
                ShowMainPanel();
            }
        }

        public void StartGame()
        {
            if (isLoading)
            {
                return;
            }

            CampaignProgressData progress = CampaignProgressStore.Load();
            if (CampaignProgressStore.HasCompletedAllImplementedLevels(progress))
            {
                OpenLevels();
                if (completedLevelsText != null)
                {
                    completedLevelsText.text =
                        "Hai completato i livelli attualmente disponibili.";
                }

                return;
            }

            StartCoroutine(LoadCampaignScene(CampaignProgressStore.GetNextSceneName(progress)));
        }

        public void ReplayTutorial() => LoadUnlockedLevel(1);

        public void OpenLevels()
        {
            if (isLoading || levelsPanel == null)
            {
                return;
            }

            SetPanelState(false, true, false);
            RefreshLevelsPanel(CampaignProgressStore.Load());
        }

        public void OpenHeroes()
        {
            if (isLoading || heroesPanel == null)
            {
                return;
            }

            SetPanelState(false, false, true);
            RefreshHeroPanel(HeroProgressStore.GetSnapshot());
        }

        public void ShowMainPanel()
        {
            if (isLoading)
            {
                return;
            }

            pendingUpgrade = HeroMajorUpgrade.None;
            SetPanelState(true, false, false);
            if (upgradeSelectionPanel != null)
            {
                upgradeSelectionPanel.SetActive(false);
            }

            if (upgradeConfirmationPanel != null)
            {
                upgradeConfirmationPanel.SetActive(false);
            }

            RefreshCampaignState();
        }

        public void OpenSettings()
        {
            if (!isLoading && settingsPanel != null)
            {
                settingsPanel.Open();
            }
        }

        public void OpenLevel01() => LoadUnlockedLevel(1);
        public void OpenLevel02() => LoadUnlockedLevel(2);
        public void OpenLevel03() => LoadUnlockedLevel(3);
        public void OpenLevel04() => LoadUnlockedLevel(4);

        public void ShowComingSoonLevel()
        {
            if (isLoading)
            {
                return;
            }

            if (errorMessage != null)
            {
                errorMessage.text = "LIVELLO NON ANCORA DISPONIBILE\nIN SVILUPPO";
            }

            if (errorModal != null)
            {
                errorModal.SetActive(true);
            }
        }

        public void OpenUpgradeSelection()
        {
            HeroProgressSnapshot snapshot = HeroProgressStore.GetSnapshot();
            if (isLoading || snapshot.UnspentMajorUpgradePoints <= 0 ||
                upgradeSelectionPanel == null)
            {
                return;
            }

            pendingUpgrade = HeroMajorUpgrade.None;
            if (heroesPanel != null)
            {
                heroesPanel.SetActive(false);
            }

            upgradeSelectionPanel.SetActive(true);
            if (upgradeConfirmationPanel != null)
            {
                upgradeConfirmationPanel.SetActive(false);
            }

            RefreshUpgradeButtons(snapshot);
        }

        public void CloseUpgradeSelection()
        {
            pendingUpgrade = HeroMajorUpgrade.None;
            if (upgradeSelectionPanel != null)
            {
                upgradeSelectionPanel.SetActive(false);
            }

            if (upgradeConfirmationPanel != null)
            {
                upgradeConfirmationPanel.SetActive(false);
            }

            if (heroesPanel != null)
            {
                heroesPanel.SetActive(true);
            }

            RefreshHeroPanel(HeroProgressStore.GetSnapshot());
        }

        public void SelectAttackUpgrade() => PreviewUpgrade(HeroMajorUpgrade.Attack);
        public void SelectGuardUpgrade() => PreviewUpgrade(HeroMajorUpgrade.GuardBastion);
        public void SelectTechniqueUpgrade() => PreviewUpgrade(HeroMajorUpgrade.Technique);
        public void SelectAnalyzeUpgrade() => PreviewUpgrade(HeroMajorUpgrade.Analyze);

        public void CancelUpgradeConfirmation()
        {
            pendingUpgrade = HeroMajorUpgrade.None;
            if (upgradeConfirmationPanel != null)
            {
                upgradeConfirmationPanel.SetActive(false);
            }

            if (upgradeSelectionPanel != null)
            {
                upgradeSelectionPanel.SetActive(true);
            }
        }

        public void ConfirmUpgrade()
        {
            if (pendingUpgrade == HeroMajorUpgrade.None || upgradeConfirmationPanel == null ||
                !upgradeConfirmationPanel.activeSelf)
            {
                return;
            }

            if (!HeroProgressStore.TryChooseMajorUpgrade(pendingUpgrade, out string failureReason))
            {
                if (upgradeConfirmationDescription != null)
                {
                    upgradeConfirmationDescription.text = failureReason;
                }

                return;
            }

            pendingUpgrade = HeroMajorUpgrade.None;
            upgradeConfirmationPanel.SetActive(false);
            if (upgradeSelectionPanel != null)
            {
                upgradeSelectionPanel.SetActive(false);
            }

            if (heroesPanel != null)
            {
                heroesPanel.SetActive(true);
            }

            RefreshCampaignState();
        }

        public void OpenResetProgressConfirmation()
        {
            if (!isLoading && resetProgressConfirmationModal != null)
            {
                returnToSettingsAfterReset = settingsPanel != null && settingsPanel.IsOpen;
                if (returnToSettingsAfterReset)
                {
                    settingsPanel.Close();
                }

                resetProgressConfirmationModal.SetActive(true);
                SetMenuControlsEnabled(false);
            }
        }

        public void CloseResetProgressConfirmation()
        {
            if (resetProgressConfirmationModal != null)
            {
                resetProgressConfirmationModal.SetActive(false);
            }

            if (!isLoading)
            {
                SetMenuControlsEnabled(true);
                if (returnToSettingsAfterReset && settingsPanel != null)
                {
                    settingsPanel.Open();
                }
            }

            returnToSettingsAfterReset = false;
        }

        public void ConfirmResetProgress()
        {
            if (isLoading || resetProgressConfirmationModal == null ||
                !resetProgressConfirmationModal.activeSelf)
            {
                return;
            }

            CampaignProgressStore.Reset();
            resetProgressConfirmationModal.SetActive(false);
            returnToSettingsAfterReset = false;
            ShowMainPanel();
            SetMenuControlsEnabled(true);
        }

        public void RefreshCampaignState()
        {
            CampaignProgressData progress = CampaignProgressStore.Load();
            HeroProgressSnapshot hero = HeroProgressStore.GetSnapshot();

            if (startButtonLabel != null)
            {
                startButtonLabel.text = GetPrimaryActionLabel(progress);
            }

            if (campaignStatusText != null)
            {
                campaignStatusText.text = GetCampaignStatus(progress, hero.HasPendingMajorUpgrade);
            }

            if (heroUpgradeBadge != null)
            {
                heroUpgradeBadge.SetActive(hero.HasPendingMajorUpgrade);
            }

            if (replayTutorialButton != null)
            {
                replayTutorialButton.gameObject.SetActive(false);
            }

            if (resetProgressButton != null)
            {
                resetProgressButton.gameObject.SetActive(progress.HasAnyProgress);
            }

            RefreshLevelsPanel(progress);
            RefreshHeroPanel(hero);
        }

        public void CloseError()
        {
            if (errorModal != null)
            {
                errorModal.SetActive(false);
            }
        }

        private void LoadUnlockedLevel(int levelNumber)
        {
            if (isLoading)
            {
                return;
            }

            CampaignProgressData progress = CampaignProgressStore.Load();
            if (!CampaignLevelCatalog.TryGetByNumber(levelNumber, out LevelDefinition definition) ||
                !definition.IsImplemented ||
                !CampaignProgressStore.IsLevelUnlocked(levelNumber, progress))
            {
                return;
            }

            StartCoroutine(LoadCampaignScene(definition.SceneName));
        }

        private void PreviewUpgrade(HeroMajorUpgrade upgrade)
        {
            HeroProgressSnapshot snapshot = HeroProgressStore.GetSnapshot();
            if (snapshot.UnspentMajorUpgradePoints <= 0 ||
                snapshot.GetUpgradeRank(upgrade) >= HeroProgressionRules.MaximumMajorUpgradeRank ||
                upgradeConfirmationPanel == null)
            {
                return;
            }

            pendingUpgrade = upgrade;
            if (upgradeSelectionPanel != null)
            {
                upgradeSelectionPanel.SetActive(false);
            }

            if (upgradeConfirmationTitle != null)
            {
                upgradeConfirmationTitle.text = "CONFERMA · " +
                                                HeroProgressionRules.GetUpgradeDisplayName(upgrade);
            }

            if (upgradeConfirmationDescription != null)
            {
                upgradeConfirmationDescription.text =
                    HeroProgressionRules.GetUpgradeDescription(upgrade);
            }

            if (upgradeBeforeAfterText != null)
            {
                upgradeBeforeAfterText.text = GetBeforeAfterText(upgrade, snapshot.CombatStats);
            }

            upgradeConfirmationPanel.SetActive(true);
        }

        private void RefreshLevelsPanel(CampaignProgressData progress)
        {
            if (completedLevelsText != null)
            {
                completedLevelsText.text =
                    CampaignProgressStore.HasCompletedAllImplementedLevels(progress)
                        ? "Hai completato i livelli attualmente disponibili."
                        : progress.CompletedLevelCount + "/10 COMPLETATI · " +
                          CampaignLevelCatalog.ImplementedLevelCount + " DISPONIBILI";
            }

            string recommendedLevelId = GetNextRecommendedLevelId(progress);

            for (int index = 0; index < CampaignLevelCatalog.All.Count; index++)
            {
                LevelDefinition definition = CampaignLevelCatalog.All[index];
                int levelNumber = definition.Number;
                bool unlocked = CampaignProgressStore.IsLevelUnlocked(levelNumber, progress);
                bool completed = CampaignProgressStore.IsLevelCompleted(levelNumber, progress);
                bool recommended = string.Equals(
                    definition.StableId,
                    recommendedLevelId,
                    StringComparison.Ordinal);
                if (levelButtons != null && index < levelButtons.Length && levelButtons[index] != null)
                {
                    Button button = levelButtons[index];
                    button.interactable = !isLoading && definition.IsImplemented && unlocked;
                    SetLevelCardColor(
                        button,
                        definition.IsImplemented,
                        unlocked,
                        completed,
                        recommended);
                }

                if (levelButtonLabels == null || index >= levelButtonLabels.Length ||
                    levelButtonLabels[index] == null)
                {
                    continue;
                }

                string state;
                if (!definition.IsImplemented)
                {
                    state = "IN SVILUPPO\nNON DISPONIBILE";
                }
                else if (completed)
                {
                    state = "COMPLETATO\nRIGIOCA";
                }
                else if (unlocked)
                {
                    state = "DISPONIBILE\nGIOCA";
                }
                else
                {
                    LevelDefinition prerequisite =
                        CampaignLevelCatalog.GetById(definition.PrerequisiteLevelId);
                    state = "BLOCCATO\nCOMPLETA IL LIVELLO " + prerequisite.Number;
                }
                levelButtonLabels[index].text =
                    (recommended ? "PROSSIMO\n" : string.Empty) +
                    levelNumber.ToString("00") + " · " + definition.Title.ToUpperInvariant() +
                    "\n" + state +
                    (completed ? GetLevelOutcome(definition, progress) : string.Empty);
            }
        }

        private void RefreshHeroPanel(HeroProgressSnapshot snapshot)
        {
            HeroCombatStats stats = snapshot.CombatStats;
            if (heroNameText != null) heroNameText.text = "HERO01";
            if (heroLevelText != null) heroLevelText.text = "LIVELLO " + snapshot.Level;

            int currentThreshold = HeroProgressionRules.GetExperienceThresholdForLevel(snapshot.Level);
            int nextThreshold = snapshot.Level < 4
                ? HeroProgressionRules.GetExperienceThresholdForLevel(snapshot.Level + 1)
                : currentThreshold;
            if (heroExperienceText != null)
            {
                heroExperienceText.text = snapshot.Level < 4
                    ? "XP " + snapshot.TotalExperience + " / " + nextThreshold
                    : "XP TOTALE " + snapshot.TotalExperience + " · PROSSIMO LIVELLO NON DISPONIBILE";
            }

            if (heroExperienceFill != null)
            {
                float progress = snapshot.Level >= 4
                    ? 1f
                    : Mathf.InverseLerp(currentThreshold, nextThreshold, snapshot.TotalExperience);
                heroExperienceFill.fillAmount = progress;
                heroExperienceFill.gameObject.SetActive(progress > 0f);
            }

            if (heroStatsText != null)
            {
                heroStatsText.text = "HP " + stats.MaxHp + "\nATTACCO " + stats.AttackDamage +
                                     "\nTECNICA " + stats.TechniqueDamage + "\nCOOLDOWN 2";
            }

            if (heroUpgradesText != null) heroUpgradesText.text = BuildUpgradeSummary(snapshot);
            if (heroPointsText != null)
            {
                heroPointsText.text = snapshot.UnspentMajorUpgradePoints > 0
                    ? "PUNTI IMPORTANTI: " + snapshot.UnspentMajorUpgradePoints +
                      "\nPOTENZIAMENTO DISPONIBILE"
                    : "PUNTI IMPORTANTI: 0";
            }

            if (heroUpgradeButton != null)
            {
                heroUpgradeButton.interactable = !isLoading && snapshot.UnspentMajorUpgradePoints > 0;
            }

            RefreshUpgradeButtons(snapshot);
        }

        private void RefreshUpgradeButtons(HeroProgressSnapshot snapshot)
        {
            SetUpgradeButtonState(upgradeAttackButton, snapshot, HeroMajorUpgrade.Attack);
            SetUpgradeButtonState(upgradeGuardButton, snapshot, HeroMajorUpgrade.GuardBastion);
            SetUpgradeButtonState(upgradeTechniqueButton, snapshot, HeroMajorUpgrade.Technique);
            SetUpgradeButtonState(upgradeAnalyzeButton, snapshot, HeroMajorUpgrade.Analyze);
        }

        private void SetUpgradeButtonState(Button button, HeroProgressSnapshot snapshot, HeroMajorUpgrade upgrade)
        {
            if (button != null)
            {
                button.interactable = !isLoading && snapshot.UnspentMajorUpgradePoints > 0 &&
                                      snapshot.GetUpgradeRank(upgrade) <
                                      HeroProgressionRules.MaximumMajorUpgradeRank;
            }
        }

        private IEnumerator LoadCampaignScene(string sceneName)
        {
            isLoading = true;
            SetMenuControlsEnabled(false);
            if (resetProgressConfirmationModal != null) resetProgressConfirmationModal.SetActive(false);
            if (errorModal != null) errorModal.SetActive(false);
            if (loadingOverlay != null) loadingOverlay.SetActive(true);

            AsyncOperation operation;
            try
            {
                operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            }
            catch (Exception exception)
            {
                ShowLoadError(exception.Message);
                yield break;
            }

            if (operation == null)
            {
                ShowLoadError("Unity non ha avviato il caricamento della scena richiesta.");
                yield break;
            }

            while (!operation.isDone) yield return null;
        }

        private void ShowLoadError(string details)
        {
            isLoading = false;
            if (loadingOverlay != null) loadingOverlay.SetActive(false);
            SetMenuControlsEnabled(true);
            if (errorMessage != null) errorMessage.text = "Impossibile aprire la scena.\n" + details;
            if (errorModal != null) errorModal.SetActive(true);
        }

        private void SetMenuControlsEnabled(bool enabled)
        {
            SetButton(startButton, enabled);
            SetButton(levelsButton, enabled);
            SetButton(heroesButton, enabled);
            SetButton(settingsButton, enabled);
            SetButton(levelsBackButton, enabled);
            SetButton(heroesBackButton, enabled);
            SetButton(upgradeSelectionBackButton, enabled);
            SetButton(confirmUpgradeButton, enabled);
            SetButton(cancelUpgradeButton, enabled);
            SetButton(resetProgressButton, enabled);
            SetButton(replayTutorialButton, enabled);
            if (enabled) RefreshCampaignState();
        }

        private void SetPanelState(bool main, bool levels, bool heroes)
        {
            if (mainNavigationPanel != null) mainNavigationPanel.SetActive(main);
            if (levelsPanel != null) levelsPanel.SetActive(levels);
            if (heroesPanel != null) heroesPanel.SetActive(heroes);
        }

        private static void SetButton(Button button, bool enabled)
        {
            if (button != null) button.interactable = enabled;
        }

        private static string GetPrimaryActionLabel(CampaignProgressData progress)
        {
            for (int index = 0; index < CampaignLevelCatalog.All.Count; index++)
            {
                LevelDefinition definition = CampaignLevelCatalog.All[index];
                if (definition.IsImplemented &&
                    !CampaignProgressStore.IsLevelCompleted(definition.StableId, progress))
                {
                    return definition.Number == 1
                        ? "GIOCA · TUTORIAL"
                        : "GIOCA · LIVELLO " + definition.Number;
                }
            }

            return "CONTENUTI COMPLETATI";
        }

        private static string GetCampaignStatus(CampaignProgressData progress, bool hasPendingMajorUpgrade)
        {
            for (int index = 0; index < CampaignLevelCatalog.All.Count; index++)
            {
                LevelDefinition definition = CampaignLevelCatalog.All[index];
                if (!definition.IsImplemented ||
                    CampaignProgressStore.IsLevelCompleted(definition.StableId, progress))
                {
                    continue;
                }

                return progress.CompletedLevelCount + "/10 COMPLETATI" +
                       (hasPendingMajorUpgrade
                           ? "  ·  POTENZIAMENTO EROE DISPONIBILE"
                           : string.Empty) +
                       "\nPROSSIMO: " + definition.Title.ToUpperInvariant();
            }

            return progress.CompletedLevelCount + "/10 LIVELLI COMPLETATI" +
                   (hasPendingMajorUpgrade
                       ? "  ·  POTENZIAMENTO EROE DISPONIBILE"
                       : string.Empty) +
                   "\nHAI COMPLETATO I LIVELLI ATTUALMENTE DISPONIBILI";
        }

        private static string GetLevelOutcome(
            LevelDefinition definition,
            CampaignProgressData progress)
        {
            if (!definition.HasMoralChoice || definition.EnemyIds.Count == 0)
            {
                return string.Empty;
            }

            if (definition.Number == 4)
            {
                return "\nBRUTO: " + GetEnemyResolutionDisplayName(
                           progress, definition, CampaignContentIds.Level04BruteEnemy) +
                       " · VEGLIA: " + GetEnemyResolutionDisplayName(
                           progress, definition, CampaignContentIds.Level04WatcherEnemy) +
                       " · MASCHERA: " + GetEnemyResolutionDisplayName(
                           progress, definition, CampaignContentIds.Level04MaskEnemy);
            }

            return " · ESITO: " + GetEnemyResolutionDisplayName(
                progress, definition, definition.EnemyIds[0]);
        }

        private static string GetEnemyResolutionDisplayName(
            CampaignProgressData progress,
            LevelDefinition definition,
            string enemyId)
        {
            return CampaignProgressStore.TryGetEnemyResolution(
                    progress, definition.StableId, enemyId, out EncounterResolution resolution)
                ? GetResolutionDisplayName(resolution)
                : "NON RISOLTO";
        }

        private static void SetLevelCardColor(
            Button button,
            bool implemented,
            bool unlocked,
            bool completed,
            bool recommended)
        {
            if (button == null || button.targetGraphic == null)
            {
                return;
            }

            button.targetGraphic.color = !implemented
                ? new Color(0.07f, 0.10f, 0.10f, 1f)
                : recommended
                    ? new Color(0.28f, 0.34f, 0.18f, 1f)
                : completed
                    ? new Color(0.08f, 0.24f, 0.18f, 1f)
                    : unlocked
                        ? new Color(0.18f, 0.30f, 0.24f, 1f)
                        : new Color(0.06f, 0.13f, 0.12f, 1f);
        }

        private static string GetNextRecommendedLevelId(CampaignProgressData progress)
        {
            for (int index = 0; index < CampaignLevelCatalog.All.Count; index++)
            {
                LevelDefinition definition = CampaignLevelCatalog.All[index];
                if (definition.IsImplemented &&
                    CampaignProgressStore.IsLevelUnlocked(definition.Number, progress) &&
                    !CampaignProgressStore.IsLevelCompleted(definition.StableId, progress))
                {
                    return definition.StableId;
                }
            }

            return string.Empty;
        }

        private static string BuildUpgradeSummary(HeroProgressSnapshot snapshot)
        {
            StringBuilder builder = new StringBuilder("POTENZIAMENTI\n");
            AppendUpgrade(builder, snapshot, HeroMajorUpgrade.Attack);
            AppendUpgrade(builder, snapshot, HeroMajorUpgrade.GuardBastion);
            AppendUpgrade(builder, snapshot, HeroMajorUpgrade.Technique);
            AppendUpgrade(builder, snapshot, HeroMajorUpgrade.Analyze);
            if (builder.Length == "POTENZIAMENTI\n".Length) builder.Append("NESSUNO");
            return builder.ToString().TrimEnd();
        }

        private static void AppendUpgrade(StringBuilder builder, HeroProgressSnapshot snapshot, HeroMajorUpgrade upgrade)
        {
            if (snapshot.HasUpgrade(upgrade))
            {
                builder.Append("- ")
                    .Append(HeroProgressionRules.GetUpgradeDisplayName(upgrade))
                    .Append(" · GRADO ")
                    .Append(snapshot.GetUpgradeRank(upgrade))
                    .Append('\n');
            }
        }

        private static string GetBeforeAfterText(HeroMajorUpgrade upgrade, HeroCombatStats current)
        {
            switch (upgrade)
            {
                case HeroMajorUpgrade.Attack:
                    return "PRIMA: ATTACCO " + current.AttackDamage + "\nDOPO: ATTACCO " +
                           (current.AttackDamage + HeroProgressionRules.AttackUpgradeBonus);
                case HeroMajorUpgrade.GuardBastion:
                    return "PRIMA: PARA IL COLPO ANNUNCIATO\nDOPO: PARA TUTTE LE AZIONI DIRETTE DELLA FASE";
                case HeroMajorUpgrade.Technique:
                    return "PRIMA: " + current.TechniqueDamage + " · AREA 35%\nDOPO: " +
                           (current.TechniqueDamage + HeroProgressionRules.TechniqueUpgradeBonus) + " · AREA 55%";
                case HeroMajorUpgrade.Analyze:
                    return "PRIMA: INFORMAZIONI DEL BERSAGLIO\nDOPO: VERE INTENZIONI + ESPOSTO 125%";
                default:
                    return string.Empty;
            }
        }

        private static string GetResolutionDisplayName(EncounterResolution resolution)
        {
            switch (resolution)
            {
                case EncounterResolution.Saved: return "SALVATO";
                case EncounterResolution.Killed: return "UCCISO";
                default: return "NON RISOLTO";
            }
        }
    }
}
