using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Veyra.Combat.Encounter;
using Veyra.Combat.Support;
using Veyra.Combat.Tactical;
using Veyra.Core;
using Veyra.Progression;
using Veyra.UI.Battle;

namespace Veyra.Combat.MultiEnemy
{
    public sealed class MultiEnemyBattleController : MonoBehaviour
    {
        private enum MoralChoiceUiStage
        {
            Choosing,
            Review
        }

        [Serializable]
        private sealed class EnemyView
        {
            [Header("Configurable profile")]
            public string enemyId;
            public string displayName;
            public string race;
            public int maxHp;
            [Range(0, 100)] public int corruptionPercent;
            public EnemyMood initialMood;
            [Range(0, 3)] public int intelligenceLevel;
            public EnemyAltitude altitude;
            public int attackDamage;
            public int chargedStrikeDamage;
            public int assaultDamage;
            public EnemyBehaviorTraits traits;
            [Range(0f, 1f)] public float aggressiveWeight = 1f;
            [Range(0f, 1f)] public float patientWeight = 1f;
            [Range(0f, 1f)] public float deceptiveWeight = 1f;
            [Range(0f, 0.35f)] public float bluffProbability = 0.30f;
            [Min(3)] public int minimumTurnsBetweenBluffs = 3;
            [Range(0f, 1f)] public float feintIntentWeight = 0.20f;
            [TextArea] public string openingDialogue;
            [TextArea] public string incapacitatedDialogue;

            [Header("Persistent scene references")]
            public Transform actor;
            public SpriteRenderer visual;
            public Button targetButton;
            public TMP_Text nameText;
            public TMP_Text healthText;
            public Image healthFill;
            public TMP_Text intentText;
            public TMP_Text targetStateText;
            public GameObject selectionIndicator;
            public GameObject instabilityClue;
            public GameObject incapacitatedState;
            public GameObject guardEffect;
            public GameObject chargeEffect;
            public GameObject hitEffect;
            public WorldHealthBarView worldHealthBar;
            public WorldDialogueBubbleView worldDialogue;
        }

        [Header("Encounter")]
        [SerializeField] private int randomSeed = 4404;
        [SerializeField] private EnemyView[] enemyViews = new EnemyView[3];

        [Header("Hero")]
        [SerializeField] private Transform heroActor;
        [SerializeField] private SpriteRenderer heroVisual;

        [Header("Tactical battlefield")]
        [SerializeField] private TacticalBattlefieldController battlefield;
        [SerializeField] private Image heroHealthFill;
        [SerializeField] private TMP_Text heroHealthText;
        [SerializeField] private WorldHealthBarView heroWorldHealthBar;
        [SerializeField] private GameObject heroGuardEffect;
        [SerializeField] private GameObject heroAttackEffect;
        [SerializeField] private GameObject heroTechniqueEffect;

        [Header("Actions and HUD")]
        [SerializeField] private Button attackButton;
        [SerializeField] private Button guardButton;
        [SerializeField] private Button techniqueButton;
        [SerializeField] private Button analyzeButton;
        [SerializeField] private TMP_Text techniqueButtonLabel;
        [SerializeField] private TMP_Text combatMessageText;
        [SerializeField] private TMP_Text selectedTargetText;
        [SerializeField] private TMP_Text heroStatusText;
        [SerializeField] private TMP_Text phaseIndicatorText;

        [Header("Level 4 contextual tutorial")]
        [SerializeField] private GameObject targetTutorialOverlay;
        [SerializeField] private TMP_Text targetTutorialText;
        [SerializeField] private Button targetTutorialContinueButton;

        [Header("Dialogue")]
        [SerializeField] private GameObject dialogueRoot;
        [SerializeField] private TMP_Text dialogueText;

        [Header("Analyze")]
        [SerializeField] private GameObject analyzePanel;
        [SerializeField] private TMP_Text analyzeTitleText;
        [SerializeField] private TMP_Text analyzeBodyText;
        [SerializeField] private Button analyzeCloseButton;

        [Header("Saved allies")]
        [SerializeField] private GameObject thornGuardianAllyActor;
        [SerializeField] private GameObject thornGuardianSupportEffect;
        [SerializeField] private GameObject ashWatcherAllyActor;
        [SerializeField] private GameObject ashWatcherSupportEffect;
        [SerializeField] private GameObject allyDialogueRoot;
        [SerializeField] private TMP_Text allyDialogueText;
        [SerializeField] private WorldDialogueBubbleView thornGuardianWorldDialogue;
        [SerializeField] private WorldDialogueBubbleView ashWatcherWorldDialogue;

        [Header("Moral choices")]
        [SerializeField] private GameObject moralChoicePanel;
        [SerializeField] private TMP_Text[] moralChoiceStateTexts = new TMP_Text[3];
        [SerializeField] private TMP_Text[] moralCurrentIndicators = new TMP_Text[3];
        [SerializeField] private Outline[] moralCurrentOutlines = new Outline[3];
        [SerializeField] private Button[] moralSaveButtons = new Button[3];
        [SerializeField] private Button[] moralKillButtons = new Button[3];
        [SerializeField] private TMP_Text moralSummaryText;
        [SerializeField] private Button moralConfirmButton;
        [SerializeField] private Button moralReviewButton;
        [SerializeField] private TMP_Text moralFocusTitleText;
        [SerializeField] private TMP_Text moralFocusBodyText;
        [SerializeField] private Image moralFocusPortrait;

        [Header("Outcome and navigation")]
        [SerializeField] private GameObject outcomePanel;
        [SerializeField] private TMP_Text outcomeTitleText;
        [SerializeField] private TMP_Text outcomeBodyText;
        [SerializeField] private Button outcomeMenuButton;
        [SerializeField] private Button outcomeRetryButton;
        [SerializeField] private TMP_Text outcomeRetryButtonLabel;
        [SerializeField] private MultiEnemyBattleNavigation navigation;

        private readonly EnemyMoralOutcome[] pendingMoralOutcomes =
            new EnemyMoralOutcome[3];
        private readonly EnemyMoralOutcome[] originalMoralOutcomes =
            new EnemyMoralOutcome[3];
        private readonly Dictionary<string, int> enemyViewIndices =
            new Dictionary<string, int>(StringComparer.Ordinal);

        private MultiEnemyBattleState battleState;
        private HeroProgressSnapshot heroSnapshot;
        private SavedAllySupport thornSupport;
        private SavedAllySupport ashSupport;
        private Coroutine dialogueRoutine;
        private Coroutine allyDialogueRoutine;
        private bool actionRunning;
        private bool analyzeOpen;
        private bool analyzeUsedThisTurn;
        private int completedHeroTurns;
        private Color heroBaseColor;
        private readonly List<Color> enemyBaseColors = new List<Color>();
        private int lastLearningDialogueTurn = int.MinValue;
        private int currentMoralChoiceIndex;
        private MoralChoiceUiStage moralChoiceUiStage;
        private bool moralChoiceIsReplay;
        private bool openingSequenceRunning;
        private bool enemyPhasePresentationRunning;
        private bool showingVictoryOutcome;
        private HeroCombatPresentation heroCombatPresentation;
        private bool moralSessionInitialized;

        public int HeroCurrentHp => battleState != null ? battleState.HeroHp : 0;
        public MultiEnemyBattlePhase CurrentPhase => battleState != null
            ? battleState.Phase
            : MultiEnemyBattlePhase.HeroTurn;
        public string SelectedEnemyId => battleState != null ? battleState.SelectedEnemyId : string.Empty;
        public bool HasValidSelectedTarget => battleState != null && battleState.HasValidSelectedTarget;
        public bool RequiresTargetSelection => battleState != null && battleState.RequiresTargetSelection;
        public int ActiveEnemyCount => battleState != null ? battleState.ActiveEnemyCount : 0;
        public bool IsActionRunning => actionRunning;
        public bool IsAnalyzeOpen => analyzeOpen;
        public bool ShowingVictoryOutcome => showingVictoryOutcome;
        public bool AnalyzeUsedThisTurn => analyzeUsedThisTurn;

        private void Awake()
        {
            if (!HasRequiredReferences())
            {
                enabled = false;
                return;
            }

            CapturePersistentState();
            if (battlefield != null) battlefield.EnemySelected += OnTacticalEnemySelected;
            heroCombatPresentation = HeroCombatPresentation.Ensure(heroActor);
            InitializeBattle();
        }

        private void OnDestroy()
        {
            if (battlefield != null) battlefield.EnemySelected -= OnTacticalEnemySelected;
        }

        private void OnTacticalEnemySelected(Transform target)
        {
            if (!CanAcceptHeroInput() || target == null) return;
            for (int index = 0; index < enemyViews.Length; index++)
            {
                if (enemyViews[index] != null && enemyViews[index].actor == target)
                {
                    SelectTargetAt(index);
                    return;
                }
            }
        }

        private void OnValidate()
        {
            if (randomSeed == 0) randomSeed = 4404;
            if (enemyViews == null) return;
            for (int index = 0; index < enemyViews.Length; index++)
            {
                EnemyView view = enemyViews[index];
                if (view == null) continue;
                view.aggressiveWeight = Mathf.Clamp01(view.aggressiveWeight);
                view.patientWeight = Mathf.Clamp01(view.patientWeight);
                view.deceptiveWeight = Mathf.Clamp01(view.deceptiveWeight);
                view.bluffProbability = Mathf.Clamp(
                    view.bluffProbability,
                    0f,
                    (float)EnemyDeceptionSettings.HardMaximumBluffProbability);
                view.minimumTurnsBetweenBluffs = Mathf.Max(
                    EnemyDeceptionSettings.HardMinimumTurnsBetweenBluffs,
                    view.minimumTurnsBetweenBluffs);
                view.feintIntentWeight = Mathf.Clamp01(view.feintIntentWeight);
            }
        }

        public void SelectBrute() => SelectTargetAt(0);
        public void SelectWatcher() => SelectTargetAt(1);
        public void SelectMask() => SelectTargetAt(2);
        public void SelectTargetByIndex(int enemyIndex) => SelectTargetAt(enemyIndex);
        public void ChooseAttack()
        {
            if (!CanUseSelectedTacticalTarget(1)) return;
            BeginHeroAction(MultiEnemyHeroAction.Attack);
        }
        public void ChooseGuard() => BeginHeroAction(MultiEnemyHeroAction.Guard);
        public void ChooseTechnique()
        {
            if (!CanUseSelectedTacticalTarget(2)) return;
            BeginHeroAction(MultiEnemyHeroAction.Technique);
        }

        public void BeginTacticalMove()
        {
            if (CanAcceptHeroInput() && battlefield != null)
            {
                battlefield.ToggleMoveMode();
            }
        }

        public void EndTacticalTurn()
        {
            if (!CanAcceptHeroInput() || battlefield == null || !battleState.PassHeroTurn())
            {
                return;
            }

            actionRunning = true;
            battlefield.CommitAction();
            SetActionButtons(false);
            StartCoroutine(ResolveEnemyPhase());
        }

        public void OpenAnalyze()
        {
            if (!CanAcceptHeroInput() || analyzeOpen)
            {
                return;
            }

            if (analyzeUsedThisTurn)
            {
                combatMessageText.text = "ANALIZZA GIÀ USATO · DISPONIBILE AL PROSSIMO TURNO";
                RefreshControls();
                return;
            }

            if (!battleState.CanUseHeroAction(MultiEnemyHeroAction.Analyze))
            {
                combatMessageText.text = "SCEGLI UN BERSAGLIO PRIMA DI ANALIZZARE";
                return;
            }

            IReadOnlyList<EnemyTurnPlan> plansBefore = battleState.CurrentPlans.ToArray();
            bool canApplyExposed = heroSnapshot.HasUpgrade(HeroMajorUpgrade.Analyze) &&
                                   !battleState.AnalyzeExposedAppliedThisTurn;
            HeroActionResolution result = battleState.ResolveHeroAction(
                MultiEnemyHeroAction.Analyze,
                battleState.SelectedEnemyId);
            if (!result.Accepted)
            {
                combatMessageText.text = result.RejectionReason;
                return;
            }

            PersistPlayerAction(MultiEnemyHeroAction.Analyze);
            analyzeUsedThisTurn = true;
            TryShowLearningDialogue(MultiEnemyHeroAction.Analyze);

            if (!PlansStillLocked(plansBefore))
            {
                Debug.LogError("[Veyra L04] ANALIZZA ha modificato un'intenzione già bloccata.", this);
            }

            analyzeOpen = true;
            SetActionButtons(false);
            analyzeTitleText.text = heroSnapshot.HasUpgrade(HeroMajorUpgrade.Analyze)
                ? "VISTA DELLA CORRUZIONE · TUTTI I NEMICI"
                : "ANALISI · BERSAGLIO SELEZIONATO";
            analyzeBodyText.text = BuildAnalyzeText(result.Intel);
            analyzeCloseButton.interactable = true;
            analyzePanel.SetActive(true);
            combatMessageText.text = heroSnapshot.HasUpgrade(HeroMajorUpgrade.Analyze)
                ? canApplyExposed
                    ? "Vere intenzioni rivelate · bersaglio ESPOSTO"
                    : "Vere intenzioni rivelate · ESPOSTO già usato in questo turno"
                : "Analisi completata senza consumare il turno";
            UpdateHud();
        }

        public void CloseAnalyze()
        {
            if (!analyzeOpen)
            {
                return;
            }

            analyzeOpen = false;
            analyzePanel.SetActive(false);
            analyzeCloseButton.interactable = true;
            combatMessageText.text = "Scegli la tua azione";
            UpdateHud();
            RefreshControls();
        }

        public void ChooseBruteSaved() => SetMoralChoice(0, EnemyMoralOutcome.Saved);
        public void ChooseBruteKilled() => SetMoralChoice(0, EnemyMoralOutcome.Killed);
        public void ChooseWatcherSaved() => SetMoralChoice(1, EnemyMoralOutcome.Saved);
        public void ChooseWatcherKilled() => SetMoralChoice(1, EnemyMoralOutcome.Killed);
        public void ChooseMaskSaved() => SetMoralChoice(2, EnemyMoralOutcome.Saved);
        public void ChooseMaskKilled() => SetMoralChoice(2, EnemyMoralOutcome.Killed);

        public void ReviewMoralChoices()
        {
            if (battleState == null ||
                battleState.Phase != MultiEnemyBattlePhase.AwaitingMoralChoices)
            {
                return;
            }

            moralChoiceUiStage = MoralChoiceUiStage.Choosing;
            currentMoralChoiceIndex = 0;
            UpdateMoralSummary();
        }

        public void CompleteMultiTargetTutorial()
        {
            if (targetTutorialOverlay == null || !targetTutorialOverlay.activeSelf)
            {
                return;
            }

            CampaignProgressStore.MarkTutorialSeen(CampaignContentIds.TutorialMultiTarget);
            targetTutorialOverlay.SetActive(false);
            openingSequenceRunning = false;
            actionRunning = false;
            combatMessageText.text = "SCEGLI UN BERSAGLIO";
            UpdateHud();
            RefreshControls();
        }

        public void ConfirmMoralChoices()
        {
            if (battleState == null || battleState.Phase != MultiEnemyBattlePhase.AwaitingMoralChoices ||
                moralChoiceUiStage != MoralChoiceUiStage.Review ||
                pendingMoralOutcomes.Any(outcome => outcome == EnemyMoralOutcome.None))
            {
                return;
            }

            actionRunning = true;
            moralConfirmButton.interactable = false;

            for (int index = 0; index < enemyViews.Length; index++)
            {
                MoralChoiceResolution resolution = battleState.ResolveMoralChoice(
                    enemyViews[index].enemyId,
                    pendingMoralOutcomes[index]);
                if (!resolution.Accepted)
                {
                    combatMessageText.text = resolution.RejectionReason;
                    actionRunning = false;
                    UpdateMoralSummary();
                    return;
                }
            }

            bool firstClear = !moralChoiceIsReplay;
            if (firstClear)
            {
                CampaignProgressStore.RecordLevel04Resolutions(
                    ToCampaignResolution(pendingMoralOutcomes[0]),
                    ToCampaignResolution(pendingMoralOutcomes[1]),
                    ToCampaignResolution(pendingMoralOutcomes[2]));
            }
            else
            {
                for (int index = 0; index < pendingMoralOutcomes.Length; index++)
                {
                    CampaignProgressStore.SetEnemyResolution(
                        CampaignContentIds.Level04ThreefoldAssault,
                        enemyViews[index].enemyId,
                        ToCampaignResolution(pendingMoralOutcomes[index]));
                }
            }

            bool storyChanged = HasMoralStoryChanged();
            ShowVictoryOutcome(firstClear, storyChanged);
        }

        public void ReturnToMenu()
        {
            if (navigation != null) navigation.BackToMenu();
        }

        public void RetryLevel()
        {
            if (navigation == null)
            {
                return;
            }

            if (showingVictoryOutcome)
            {
                navigation.OpenLevelSelection();
            }
            else
            {
                navigation.RetryCurrentLevel();
            }
        }

        public void CancelForSceneChange()
        {
            StopAllCoroutines();
            dialogueRoutine = null;
            allyDialogueRoutine = null;
            actionRunning = false;
            analyzeOpen = false;
            analyzePanel.SetActive(false);
            moralChoicePanel.SetActive(false);
            outcomePanel.SetActive(false);
            if (targetTutorialOverlay != null) targetTutorialOverlay.SetActive(false);
            if (outcomeRetryButton != null) outcomeRetryButton.gameObject.SetActive(false);
            dialogueRoot.SetActive(false);
            allyDialogueRoot.SetActive(false);
            openingSequenceRunning = false;
            enemyPhasePresentationRunning = false;
            SetActionButtons(false);
        }

        public void ShowExternalMessage(string message)
        {
            if (combatMessageText != null) combatMessageText.text = message;
        }

        private void InitializeBattle()
        {
            heroSnapshot = HeroProgressStore.GetSnapshot();
            HeroCombatStats stats = heroSnapshot.CombatStats;
            bool attackUpgrade = heroSnapshot.HasUpgrade(HeroMajorUpgrade.Attack);
            bool techniqueUpgrade = heroSnapshot.HasUpgrade(HeroMajorUpgrade.Technique);
            MultiEnemyBattleRules rules = new MultiEnemyBattleRules(
                stats.MaxHp,
                stats.AttackDamage - (attackUpgrade ? HeroProgressionRules.AttackUpgradeBonus : 0),
                stats.TechniqueDamage - (techniqueUpgrade ? HeroProgressionRules.TechniqueUpgradeBonus : 0));
            HeroSkillUpgrades upgrades = new HeroSkillUpgrades(
                attackUpgrade,
                heroSnapshot.HasUpgrade(HeroMajorUpgrade.GuardBastion),
                techniqueUpgrade,
                heroSnapshot.HasUpgrade(HeroMajorUpgrade.Analyze));
            battleState = new MultiEnemyBattleState(
                rules,
                BuildProfiles(),
                upgrades,
                randomSeed,
                LoadPersistentPlayerTendencies());

            completedHeroTurns = 0;
            actionRunning = true;
            analyzeOpen = false;
            analyzeUsedThisTurn = false;
            lastLearningDialogueTurn = int.MinValue;
            openingSequenceRunning = true;
            enemyPhasePresentationRunning = false;
            enemyViewIndices.Clear();
            moralSessionInitialized = false;
            for (int index = 0; index < enemyViews.Length; index++)
            {
                enemyViewIndices[enemyViews[index].enemyId] = index;
                pendingMoralOutcomes[index] = EnemyMoralOutcome.None;
            }

            ResetPersistentVisuals();
            analyzePanel.SetActive(false);
            moralChoicePanel.SetActive(false);
            outcomePanel.SetActive(false);
            if (targetTutorialOverlay != null) targetTutorialOverlay.SetActive(false);
            dialogueRoot.SetActive(false);
            allyDialogueRoot.SetActive(false);
            outcomeMenuButton.interactable = true;
            if (outcomeRetryButton != null)
            {
                outcomeRetryButton.gameObject.SetActive(false);
                outcomeRetryButton.interactable = true;
            }
            ConfigureSavedAllies();
            UpdateHud();
            heroWorldHealthBar?.SetHealthSilently(battleState.HeroHp, battleState.HeroMaxHp);
            for (int index = 0; index < enemyViews.Length; index++)
            {
                EnemyView view = enemyViews[index];
                MultiEnemyEnemyState enemy = battleState.GetEnemy(view.enemyId);
                view.worldHealthBar?.SetHealthSilently(enemy.CurrentHp, enemy.Profile.MaxHp);
                view.worldDialogue?.HideImmediate();
            }
            thornGuardianWorldDialogue?.HideImmediate();
            ashWatcherWorldDialogue?.HideImmediate();
            RefreshControls();
            combatMessageText.text = "OSSERVA I NEMICI";
            StartCoroutine(ShowOpeningSequence());
        }

        private IReadOnlyList<MultiEnemyProfile> BuildProfiles()
        {
            List<MultiEnemyProfile> profiles = new List<MultiEnemyProfile>();
            for (int index = 0; index < enemyViews.Length; index++)
            {
                EnemyView view = enemyViews[index];
                profiles.Add(new MultiEnemyProfile(
                    view.enemyId,
                    view.displayName,
                    view.race,
                    view.maxHp,
                    view.corruptionPercent,
                    view.initialMood,
                    view.intelligenceLevel,
                    view.altitude,
                    view.attackDamage,
                    view.chargedStrikeDamage,
                    view.assaultDamage,
                    view.traits,
                    new EnemyTraitWeights(
                        Mathf.Clamp01(view.aggressiveWeight),
                        Mathf.Clamp01(view.patientWeight),
                        Mathf.Clamp01(view.deceptiveWeight)),
                    new EnemyDeceptionSettings(
                        Mathf.Clamp(
                            view.bluffProbability,
                            0f,
                            (float)EnemyDeceptionSettings.HardMaximumBluffProbability),
                        Mathf.Max(
                            EnemyDeceptionSettings.HardMinimumTurnsBetweenBluffs,
                            view.minimumTurnsBetweenBluffs),
                        Mathf.Clamp01(view.feintIntentWeight))));
            }

            return profiles.AsReadOnly();
        }

        private void SelectTargetAt(int index)
        {
            if (battleState == null || index < 0 || index >= enemyViews.Length || actionRunning ||
                analyzeOpen || battleState.Phase != MultiEnemyBattlePhase.HeroTurn)
            {
                return;
            }

            if (battleState.SelectTarget(enemyViews[index].enemyId))
            {
                if (battlefield != null)
                {
                    battlefield.SetSelectedEnemy(enemyViews[index].actor);
                }
                combatMessageText.text = "BERSAGLIO SELEZIONATO · " +
                                         enemyViews[index].displayName.ToUpperInvariant();
                UpdateHud();
                RefreshControls();
            }
        }

        private void BeginHeroAction(MultiEnemyHeroAction action)
        {
            if (!CanAcceptHeroInput())
            {
                return;
            }

            if (!battleState.CanUseHeroAction(action))
            {
                combatMessageText.text = battleState.RequiresTargetSelection &&
                                         action != MultiEnemyHeroAction.Guard
                    ? "SCEGLI UN BERSAGLIO"
                    : action == MultiEnemyHeroAction.Technique
                        ? "LA TECNICA NON È ANCORA PRONTA"
                        : "AZIONE NON DISPONIBILE";
                return;
            }

            actionRunning = true;
            if (battlefield != null)
            {
                battlefield.CommitAction();
            }
            SetActionButtons(false);
            UpdatePhaseIndicator();
            StartCoroutine(ResolveHeroTurn(action));
        }

        private IEnumerator ResolveHeroTurn(MultiEnemyHeroAction action)
        {
            string lockedTarget = battleState.SelectedEnemyId;
            combatMessageText.text = action == MultiEnemyHeroAction.Guard
                ? "Hero01 prepara " + (heroSnapshot.HasUpgrade(HeroMajorUpgrade.GuardBastion)
                    ? "BASTIONE"
                    : "GUARDIA")
                : "Hero01 usa " + (action == MultiEnemyHeroAction.Technique ? "TECNICA" : "ATTACCO");
            GameObject heroEffect = action == MultiEnemyHeroAction.Technique
                ? heroTechniqueEffect
                : action == MultiEnemyHeroAction.Attack ? heroAttackEffect : heroGuardEffect;
            if (heroEffect != null)
            {
                heroEffect.SetActive(true);
                yield return Pulse(heroEffect, 1.32f, 0.28f);
                if (action != MultiEnemyHeroAction.Guard) heroEffect.SetActive(false);
            }

            if (heroCombatPresentation != null &&
                (action == MultiEnemyHeroAction.Attack ||
                 action == MultiEnemyHeroAction.Technique) &&
                enemyViewIndices.TryGetValue(lockedTarget, out int targetViewIndex))
            {
                yield return heroCombatPresentation.PlayMelee(
                    enemyViews[targetViewIndex].actor,
                    action == MultiEnemyHeroAction.Technique);
            }

            int[] enemyHpBeforeAction = new int[enemyViews.Length];
            for (int index = 0; index < enemyViews.Length; index++)
            {
                enemyHpBeforeAction[index] = battleState.GetEnemy(enemyViews[index].enemyId).CurrentHp;
            }

            HeroActionResolution result = battleState.ResolveHeroAction(action, lockedTarget);
            if (!result.Accepted)
            {
                combatMessageText.text = result.RejectionReason;
                actionRunning = false;
                RefreshControls();
                yield break;
            }

            PersistPlayerAction(action);
            TryShowLearningDialogue(action);

            foreach (DamageEvent damage in result.DamageEvents)
            {
                if (!enemyViewIndices.TryGetValue(damage.TargetEnemyId, out int viewIndex)) continue;
                EnemyView view = enemyViews[viewIndex];
                MultiEnemyEnemyState enemyAfterDamage = battleState.GetEnemy(view.enemyId);
                view.worldHealthBar?.ShowDamage(
                    enemyHpBeforeAction[viewIndex],
                    enemyAfterDamage.CurrentHp,
                    enemyAfterDamage.Profile.MaxHp);
                string prefix = damage.WasSplash ? "ONDA" : "COLPO";
                combatMessageText.text = (damage.ReducedByGuard
                    ? "GUARDIA DI " + view.displayName.ToUpperInvariant() + " · COLPO BLOCCATO · 0 DANNI"
                    : prefix + " SU " + view.displayName.ToUpperInvariant() +
                      " · " + damage.AppliedDamage + " DANNI") +
                                         (damage.UsedExposed ? " · ESPOSTO" : string.Empty);
                if (view.hitEffect != null)
                {
                    view.hitEffect.SetActive(true);
                    yield return Pulse(view.hitEffect, 1.30f, 0.20f);
                    view.hitEffect.SetActive(false);
                }

                yield return Flash(view.visual, Color.white, 0.13f);
                UpdateHud();
                yield return new WaitForSecondsRealtime(0.12f);
            }

            if (battleState.Phase == MultiEnemyBattlePhase.AwaitingMoralChoices)
            {
                if (result.AllEnemiesIncapacitated)
                {
                    yield return ShowSavedAllyEndingDialogues();
                }

                CampaignProgressData campaign = CampaignProgressStore.Load();
                EnterMoralChoices(campaign);
                yield break;
            }

            completedHeroTurns++;
            yield return ResolveSavedAllySupports();
            yield return new WaitForSecondsRealtime(0.16f);
            yield return ResolveEnemyPhase();
        }

        private IEnumerator ResolveEnemyPhase()
        {
            enemyPhasePresentationRunning = true;
            int presentedHeroHp = battleState.HeroHp;
            EnemyPhaseResolution phase = battleState.ResolveEnemyPhase();
            if (!phase.Accepted)
            {
                enemyPhasePresentationRunning = false;
                combatMessageText.text = phase.RejectionReason;
                actionRunning = false;
                RefreshControls();
                yield break;
            }

            foreach (EnemyActionResolution action in phase.Actions)
            {
                if (action.Plan == null || !enemyViewIndices.TryGetValue(action.Plan.EnemyId, out int index))
                {
                    continue;
                }

                EnemyView view = enemyViews[index];
                if (action.SkippedBecauseIncapacitated) continue;
                int heroHpAfterAction = Mathf.Max(0, presentedHeroHp - action.DamageDealt);
                heroWorldHealthBar?.ShowDamage(
                    presentedHeroHp,
                    heroHpAfterAction,
                    battleState.HeroMaxHp);
                presentedHeroHp = heroHpAfterAction;
                if (battlefield != null)
                {
                    TacticalEnemyMovementStyle movementStyle = index == 0
                        ? TacticalEnemyMovementStyle.Aggressive
                        : index == 1
                            ? TacticalEnemyMovementStyle.Patient
                            : TacticalEnemyMovementStyle.Deceptive;
                    yield return battlefield.MoveEnemyForPersonality(view.actor, movementStyle);
                }
                if (action.BluffRevealed)
                {
                    combatMessageText.text = view.displayName.ToUpperInvariant() +
                                             " · HA MENTITO SULLA SUA INTENZIONE · " +
                                             GetIntentLabel(action.Plan.DisplayedIntent) + " -> " +
                                             GetIntentLabel(action.Plan.TrueIntent);
                    yield return new WaitForSecondsRealtime(1.10f);
                }

                combatMessageText.text = BuildEnemyActionMessage(view, action);
                if (action.PreparedGuard && view.guardEffect != null)
                {
                    view.guardEffect.SetActive(true);
                    yield return Pulse(view.guardEffect, 1.22f, 0.25f);
                }
                else if ((action.BeganCharge || action.HeldCharge) && view.chargeEffect != null)
                {
                    view.chargeEffect.SetActive(true);
                    yield return Pulse(view.chargeEffect, 1.28f, 0.30f);
                }
                else if (action.DamageDealt > 0 || action.BlockedByGuard)
                {
                    yield return Flash(heroVisual,
                        action.BlockedByGuard ? new Color(0.62f, 1f, 0.92f) : Color.white,
                        0.15f);
                }

                UpdateHud();
                yield return new WaitForSecondsRealtime(0.42f);
            }

            enemyPhasePresentationRunning = false;

            if (phase.HeroDefeated)
            {
                ShowDefeatOutcome();
                yield break;
            }

            TryShowDifficultyDialogue(thornSupport);
            TryShowDifficultyDialogue(ashSupport);
            actionRunning = false;
            analyzeUsedThisTurn = false;
            if (battlefield != null)
            {
                battlefield.BeginHeroTurn();
            }
            SetHeroTurnPrompt();
            UpdateHud();
            RefreshControls();
        }

        private IEnumerator ResolveSavedAllySupports()
        {
            yield return ResolveSavedAllySupport(
                thornSupport,
                thornGuardianSupportEffect,
                new Color(0.58f, 1f, 0.52f));
            yield return ResolveSavedAllySupport(
                ashSupport,
                ashWatcherSupportEffect,
                new Color(1f, 0.72f, 0.42f));
        }

        private IEnumerator ResolveSavedAllySupport(
            SavedAllySupport support,
            GameObject supportEffect,
            Color flashColor)
        {
            if (support == null) yield break;
            List<SavedAllyTargetSnapshot> targets = new List<SavedAllyTargetSnapshot>();
            for (int index = 0; index < battleState.Enemies.Count; index++)
            {
                MultiEnemyEnemyState enemy = battleState.Enemies[index];
                targets.Add(new SavedAllyTargetSnapshot(
                    enemy.Profile.EnemyId,
                    index,
                    enemy.CurrentHp,
                    enemy.Profile.MaxHp,
                    !enemy.IsIncapacitated));
            }

            if (!support.TryIntervene(completedHeroTurns, targets, out SavedAllySupportAction action))
            {
                yield break;
            }

            DamageEvent damage = battleState.ApplyExternalNonLethalDamage(
                action.TargetId,
                action.AppliedDamage);
            EnemyView view = enemyViews[enemyViewIndices[action.TargetId]];
            MultiEnemyEnemyState supportedEnemy = battleState.GetEnemy(action.TargetId);
            view.worldHealthBar?.ShowDamage(
                action.TargetHpBefore,
                supportedEnemy.CurrentHp,
                supportedEnemy.Profile.MaxHp);
            combatMessageText.text = action.AllyDisplayName + " usa " + action.AttackDisplayName +
                                     " · " + damage.AppliedDamage + " DANNI NON LETALI";
            if (action.HasDialogue) ShowAllyDialogue(action.AllyId, action.Dialogue);
            if (supportEffect != null)
            {
                supportEffect.SetActive(true);
                yield return Pulse(supportEffect, 1.38f, 0.32f);
                supportEffect.SetActive(false);
            }

            yield return Flash(view.visual, flashColor, 0.16f);
            UpdateHud();
            yield return new WaitForSecondsRealtime(0.30f);
        }

        private void ConfigureSavedAllies()
        {
            CampaignProgressData progress = CampaignProgressStore.Load();
            EncounterResolution thornResolution;
            EncounterResolution ashResolution;
            bool thornSaved = CampaignProgressStore.TryGetEnemyResolution(
                                  progress,
                                  CampaignContentIds.Level02ThornGuardian,
                                  CampaignContentIds.ThornGuardianEnemy,
                                  out thornResolution) &&
                              thornResolution == EncounterResolution.Saved;
            bool ashSaved = CampaignProgressStore.TryGetEnemyResolution(
                                progress,
                                CampaignContentIds.Level03AshWatcher,
                                CampaignContentIds.AshWatcherEnemy,
                                out ashResolution) &&
                            ashResolution == EncounterResolution.Saved;
            thornGuardianAllyActor.SetActive(thornSaved);
            ashWatcherAllyActor.SetActive(ashSaved);
            thornSupport = thornSaved
                ? new SavedAllySupport(SavedAllySupportCatalog.CreateThornGuardian())
                : null;
            ashSupport = ashSaved
                ? new SavedAllySupport(SavedAllySupportCatalog.CreateAshWatcher())
                : null;
        }

        private IEnumerator ShowOpeningSequence()
        {
            for (int index = 0; index < enemyViews.Length; index++)
            {
                ShowDialogue(enemyViews[index].enemyId, enemyViews[index].openingDialogue);
                yield return new WaitForSecondsRealtime(1.25f);
            }

            if (thornSupport != null && thornSupport.TryGetOpeningDialogue(out SavedAllyDialogueLine thornLine))
            {
                ShowAllyDialogue(SavedAllyId.ThornGuardian, thornLine.Text);
                yield return new WaitForSecondsRealtime(1.1f);
            }

            if (ashSupport != null && ashSupport.TryGetOpeningDialogue(out SavedAllyDialogueLine ashLine))
            {
                ShowAllyDialogue(SavedAllyId.AshWatcher, ashLine.Text);
            }

            string learnedOpening = BuildPersistentLearningDialogue();
            if (!string.IsNullOrWhiteSpace(learnedOpening))
            {
                yield return new WaitForSecondsRealtime(1.1f);
                ShowDialogue(enemyViews[0].enemyId, learnedOpening);
            }

            openingSequenceRunning = false;
            if (!CampaignProgressStore.HasSeenTutorial(CampaignContentIds.TutorialMultiTarget))
            {
                if (targetTutorialOverlay != null && targetTutorialText != null &&
                    targetTutorialContinueButton != null)
                {
                    targetTutorialText.text =
                        "Tocca un personaggio o la sua scheda per sceglierlo come bersaglio. " +
                        "Guardia resta disponibile anche prima della scelta.";
                    targetTutorialContinueButton.interactable = true;
                    targetTutorialOverlay.SetActive(true);
                    combatMessageText.text = "COME SCEGLIERE UN BERSAGLIO";
                    UpdateHud();
                    yield break;
                }

                Debug.LogWarning(
                    "[Veyra L04] Tutorial multi-bersaglio non cablato: overlay, testo o pulsante mancanti.",
                    this);
            }

            actionRunning = false;
            combatMessageText.text = "SCEGLI UN BERSAGLIO";
            UpdateHud();
            RefreshControls();
        }

        private IEnumerator ShowSavedAllyEndingDialogues()
        {
            if (thornSupport != null &&
                thornSupport.TryGetEndingDialogue(out SavedAllyDialogueLine thornLine))
            {
                ShowAllyDialogue(SavedAllyId.ThornGuardian, thornLine.Text);
                yield return new WaitForSecondsRealtime(1.1f);
            }

            if (ashSupport != null &&
                ashSupport.TryGetEndingDialogue(out SavedAllyDialogueLine ashLine))
            {
                ShowAllyDialogue(SavedAllyId.AshWatcher, ashLine.Text);
                yield return new WaitForSecondsRealtime(1.1f);
            }
        }

        private void EnterMoralChoices(CampaignProgressData campaign)
        {
            actionRunning = false;
            analyzeOpen = false;
            analyzePanel.SetActive(false);
            SetActionButtons(false);
            if (outcomeRetryButton != null) outcomeRetryButton.gameObject.SetActive(false);
            if (!moralSessionInitialized)
            {
                moralChoiceIsReplay = CampaignProgressStore.IsLevelCompleted(
                    CampaignContentIds.Level04ThreefoldAssault,
                    campaign);
                for (int index = 0; index < pendingMoralOutcomes.Length; index++)
                {
                    EnemyMoralOutcome existing = moralChoiceIsReplay
                        ? GetPersistedBattleOutcome(campaign, index)
                        : EnemyMoralOutcome.None;
                    pendingMoralOutcomes[index] = existing;
                    originalMoralOutcomes[index] = existing;
                }

                moralSessionInitialized = true;
            }

            currentMoralChoiceIndex = FindEnemyViewIndex(battleState.PendingMoralEnemyId);
            if (currentMoralChoiceIndex < 0)
            {
                Debug.LogError("[Veyra L4] Nessun nemico associato alla decisione corrente.", this);
                return;
            }

            moralChoiceUiStage = MoralChoiceUiStage.Choosing;
            combatMessageText.text = "DECIDI IL SUO DESTINO · " +
                                     enemyViews[currentMoralChoiceIndex].displayName.ToUpperInvariant();
            UpdateMoralSummary();
            moralChoicePanel.SetActive(true);
            UpdateHud();
        }

        private void SetMoralChoice(int index, EnemyMoralOutcome outcome)
        {
            if (battleState == null || battleState.Phase != MultiEnemyBattlePhase.AwaitingMoralChoices ||
                moralChoiceUiStage != MoralChoiceUiStage.Choosing ||
                index < 0 || index >= pendingMoralOutcomes.Length ||
                index != currentMoralChoiceIndex)
            {
                return;
            }

            StartCoroutine(ResolveImmediateMoralChoice(index, outcome));
        }

        private IEnumerator ResolveImmediateMoralChoice(int index, EnemyMoralOutcome outcome)
        {
            actionRunning = true;
            SetActionButtons(false);
            if (moralSaveButtons[index] != null) moralSaveButtons[index].interactable = false;
            if (moralKillButtons[index] != null) moralKillButtons[index].interactable = false;

            MoralChoiceResolution resolution = battleState.ResolveMoralChoice(
                enemyViews[index].enemyId,
                outcome);
            if (!resolution.Accepted)
            {
                combatMessageText.text = resolution.RejectionReason;
                actionRunning = false;
                UpdateMoralSummary();
                yield break;
            }

            pendingMoralOutcomes[index] = outcome;
            moralChoicePanel.SetActive(false);
            yield return PresentResolvedEnemy(index, outcome);

            if (battleState.Phase == MultiEnemyBattlePhase.AwaitingMoralChoices)
            {
                actionRunning = false;
                EnterMoralChoices(CampaignProgressStore.Load());
                yield break;
            }

            if (battleState.Phase == MultiEnemyBattlePhase.Completed)
            {
                CompleteLevelAfterFinalMoralChoice();
                yield break;
            }

            combatMessageText.text = outcome == EnemyMoralOutcome.Saved
                ? enemyViews[index].displayName.ToUpperInvariant() + " È SALVO · LO SCONTRO CONTINUA"
                : enemyViews[index].displayName.ToUpperInvariant() + " È STATO UCCISO · LO SCONTRO CONTINUA";
            UpdateHud();
            yield return new WaitForSecondsRealtime(0.35f);
            yield return ResolveEnemyPhase();
        }

        private IEnumerator PresentResolvedEnemy(int index, EnemyMoralOutcome outcome)
        {
            EnemyView view = enemyViews[index];
            if (battlefield != null) battlefield.RemoveUnitFromArena(view.actor);
            view.targetButton.interactable = false;
            view.selectionIndicator.SetActive(false);
            view.incapacitatedState.SetActive(false);
            view.guardEffect.SetActive(false);
            view.chargeEffect.SetActive(false);

            Vector3 startPosition = view.actor.position;
            Vector3 startScale = view.actor.localScale;
            Color startColor = view.visual.color;
            float elapsed = 0f;
            const float duration = 0.42f;

            if (outcome == EnemyMoralOutcome.Saved)
            {
                Vector3 backgroundPosition = new Vector3(-5.25f + index * 1.05f, 2.45f, 0f);
                Vector3 backgroundScale = startScale * 0.46f;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                    view.actor.position = Vector3.Lerp(startPosition, backgroundPosition, t);
                    view.actor.localScale = Vector3.Lerp(startScale, backgroundScale, t);
                    view.visual.color = Color.Lerp(startColor,
                        new Color(startColor.r, startColor.g, startColor.b, 0.72f), t);
                    yield return null;
                }

                view.actor.position = backgroundPosition;
                view.actor.localScale = backgroundScale;
                view.visual.sortingOrder = 3;
                combatMessageText.text = view.displayName.ToUpperInvariant() +
                                         " È STATO SALVATO · ORA OSSERVA DALLO SFONDO";
            }
            else
            {
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    view.actor.localScale = Vector3.Lerp(startScale, startScale * 0.12f, t);
                    view.visual.color = new Color(
                        startColor.r,
                        startColor.g,
                        startColor.b,
                        Mathf.Lerp(startColor.a, 0f, t));
                    yield return null;
                }

                view.actor.gameObject.SetActive(false);
                combatMessageText.text = view.displayName.ToUpperInvariant() +
                                         " È STATO UCCISO · È SCOMPARSO DALL'ARENA";
            }

            yield return new WaitForSecondsRealtime(0.30f);
        }

        private void CompleteLevelAfterFinalMoralChoice()
        {
            bool firstClear = !moralChoiceIsReplay;
            if (firstClear)
            {
                CampaignProgressStore.RecordLevel04Resolutions(
                    ToCampaignResolution(pendingMoralOutcomes[0]),
                    ToCampaignResolution(pendingMoralOutcomes[1]),
                    ToCampaignResolution(pendingMoralOutcomes[2]));
            }
            else
            {
                for (int index = 0; index < pendingMoralOutcomes.Length; index++)
                {
                    CampaignProgressStore.SetEnemyResolution(
                        CampaignContentIds.Level04ThreefoldAssault,
                        enemyViews[index].enemyId,
                        ToCampaignResolution(pendingMoralOutcomes[index]));
                }
            }

            bool storyChanged = HasMoralStoryChanged();
            ShowVictoryOutcome(firstClear, storyChanged);
        }

        private int FindEnemyViewIndex(string enemyId)
        {
            return !string.IsNullOrEmpty(enemyId) &&
                   enemyViewIndices.TryGetValue(enemyId, out int index)
                ? index
                : -1;
        }

        private void UpdateMoralSummary()
        {
            bool complete = true;
            List<string> summary = new List<string>();
            for (int index = 0; index < pendingMoralOutcomes.Length; index++)
            {
                EnemyMoralOutcome outcome = pendingMoralOutcomes[index];
                complete &= outcome != EnemyMoralOutcome.None;
                string label = GetMoralLabel(outcome);
                if (moralChoiceStateTexts != null && index < moralChoiceStateTexts.Length &&
                    moralChoiceStateTexts[index] != null)
                {
                    moralChoiceStateTexts[index].text = label;
                }

                summary.Add(enemyViews[index].displayName + ": " + label);

                bool isCurrent = moralChoiceUiStage == MoralChoiceUiStage.Choosing &&
                                 index == currentMoralChoiceIndex;
                if (moralCurrentIndicators != null &&
                    index < moralCurrentIndicators.Length &&
                    moralCurrentIndicators[index] != null)
                {
                    moralCurrentIndicators[index].text = "> IN DECISIONE";
                    moralCurrentIndicators[index].gameObject.SetActive(isCurrent);
                }

                if (moralCurrentOutlines != null &&
                    index < moralCurrentOutlines.Length &&
                    moralCurrentOutlines[index] != null)
                {
                    moralCurrentOutlines[index].enabled = isCurrent;
                }

                if (moralSaveButtons != null && index < moralSaveButtons.Length &&
                    moralSaveButtons[index] != null)
                {
                    moralSaveButtons[index].gameObject.SetActive(isCurrent);
                    moralSaveButtons[index].interactable = isCurrent;
                }

                if (moralKillButtons != null && index < moralKillButtons.Length &&
                    moralKillButtons[index] != null)
                {
                    moralKillButtons[index].gameObject.SetActive(isCurrent);
                    moralKillButtons[index].interactable = isCurrent;
                }
            }

            bool reviewing = moralChoiceUiStage == MoralChoiceUiStage.Review;
            if (!reviewing)
            {
                EnemyView current = enemyViews[currentMoralChoiceIndex];
                string step = "NEMICO ABBATTUTO";
                summary.Insert(0, step + " · " + current.displayName.ToUpperInvariant());
                if (moralFocusTitleText != null)
                {
                    moralFocusTitleText.text = "DECIDI IL SUO DESTINO · " + step;
                }

                if (moralFocusBodyText != null)
                {
                    moralFocusBodyText.text = current.displayName.ToUpperInvariant() +
                                              "\nRAZZA: " + current.race +
                                              " · CORRUZIONE: " + current.corruptionPercent + "%" +
                                              "\nSTATO: " + GetMoodLabel(current.initialMood) +
                                              "\n\n" + current.incapacitatedDialogue +
                                              "\nSALVA: POTRÀ TORNARE · UCCIDI: USCIRÀ DALLA STORIA" +
                                              (moralChoiceIsReplay
                                                  ? "\nSCELTA REGISTRATA: " +
                                                    GetMoralLabel(originalMoralOutcomes[currentMoralChoiceIndex])
                                                  : string.Empty);
                }

                if (moralFocusPortrait != null)
                {
                    moralFocusPortrait.gameObject.SetActive(true);
                    moralFocusPortrait.sprite = current.visual.sprite;
                    moralFocusPortrait.preserveAspect = true;
                }
            }
            else
            {
                bool changed = HasMoralStoryChanged();
                summary.Insert(0, "RIEPILOGO FINALE");
                summary.Add(moralChoiceIsReplay
                    ? changed
                        ? "ATTENZIONE: LA CONFERMA MODIFICHERÀ LA STORIA SALVATA"
                        : "LE DECISIONI COINCIDONO CON LA STORIA SALVATA"
                    : "CONFERMA UNICA · LE TRE SCELTE SARANNO REGISTRATE");
                if (moralFocusTitleText != null)
                {
                    moralFocusTitleText.text = "RIEPILOGO · RIVEDI O CONFERMA";
                }

                if (moralFocusBodyText != null)
                {
                    moralFocusBodyText.text = moralChoiceIsReplay && changed
                        ? "Questa decisione modificherà la storia salvata. Nessuna ricompensa verrà duplicata."
                        : "Controlla i tre esiti prima della conferma definitiva.";
                }

                if (moralFocusPortrait != null)
                {
                    moralFocusPortrait.gameObject.SetActive(false);
                }
            }

            moralSummaryText.text = string.Join("\n", summary);
            moralConfirmButton.gameObject.SetActive(reviewing);
            moralConfirmButton.interactable = reviewing && complete;
            if (moralReviewButton != null)
            {
                moralReviewButton.gameObject.SetActive(reviewing);
                moralReviewButton.interactable = reviewing;
            }

            UpdatePhaseIndicator();
        }

        private void ShowVictoryOutcome(bool firstClear, bool storyChanged)
        {
            actionRunning = false;
            showingVictoryOutcome = true;
            moralChoicePanel.SetActive(false);
            outcomeTitleText.text = "VITTORIA · HERO01 LIVELLO 4";
            HeroProgressSnapshot updatedHero = HeroProgressStore.GetSnapshot();
            int rewardExperience = CampaignLevelCatalog.GetByNumber(4).ExperienceReward;
            string progressSummary = firstClear
                ? "+" + rewardExperience + " XP · " + updatedHero.TotalExperience +
                  " XP TOTALI\n4/10 LIVELLI COMPLETATI"
                : storyChanged
                    ? "RIVINCITA COMPLETATA · STORIA AGGIORNATA\nNESSUN XP AGGIUNTIVO"
                    : "RIVINCITA COMPLETATA\nDECISIONI CONFERMATE\nNESSUN XP AGGIUNTIVO";
            outcomeBodyText.text = progressSummary + "\n" +
                                   BuildConfirmedMoralOutcomeSummary() +
                                   "\nHai completato i livelli attualmente disponibili.";
            outcomePanel.SetActive(true);
            outcomeMenuButton.interactable = true;
            if (outcomeRetryButton != null)
            {
                outcomeRetryButton.gameObject.SetActive(true);
                outcomeRetryButton.interactable = true;
            }
            if (outcomeRetryButtonLabel != null)
            {
                outcomeRetryButtonLabel.text = "RIGIOCA UN LIVELLO";
            }

            combatMessageText.text = storyChanged
                ? "Le tre decisioni sono state confermate · storia aggiornata"
                : "Le tre decisioni sono state confermate";
            SetActionButtons(false);
            UpdateHud();
        }

        private void ShowDefeatOutcome()
        {
            actionRunning = false;
            showingVictoryOutcome = false;
            analyzeOpen = false;
            analyzePanel.SetActive(false);
            moralChoicePanel.SetActive(false);
            outcomeTitleText.text = "SCONFITTA";
            outcomeBodyText.text = "Nessun XP ottenuto\nIl Livello 4 non è stato completato";
            outcomePanel.SetActive(true);
            outcomeMenuButton.interactable = true;
            if (outcomeRetryButton != null)
            {
                outcomeRetryButton.gameObject.SetActive(true);
                outcomeRetryButton.interactable = true;
            }
            if (outcomeRetryButtonLabel != null) outcomeRetryButtonLabel.text = "RIPROVA";
            combatMessageText.text = "Hero01 non può più combattere";
            SetActionButtons(false);
            UpdateHud();
        }

        private void UpdateHud()
        {
            if (battleState == null) return;
            heroHealthFill.fillAmount = battleState.HeroHp / (float)battleState.HeroMaxHp;
            heroHealthText.text = battleState.HeroHp + " / " + battleState.HeroMaxHp;
            selectedTargetText.text = "BERSAGLIO · " + GetSelectedTargetName();
            heroStatusText.text = heroSnapshot.HasUpgrade(HeroMajorUpgrade.GuardBastion)
                ? "GUARDIA · BASTIONE DISPONIBILE"
                : "GUARDIA · PARA UN ATTACCO DIRETTO";

            for (int index = 0; index < enemyViews.Length; index++)
            {
                EnemyView view = enemyViews[index];
                MultiEnemyEnemyState enemy = battleState.GetEnemy(view.enemyId);
                view.nameText.text = view.displayName.ToUpperInvariant();
                view.healthFill.fillAmount = enemy.CurrentHp / (float)enemy.Profile.MaxHp;
                view.healthText.text = enemy.CurrentHp + " / " + enemy.Profile.MaxHp;
                bool selected = battleState.IsTargetSelected(view.enemyId);
                view.targetStateText.text = enemy.IsIncapacitated
                    ? "INCAPACITATO"
                    : selected ? "[ BERSAGLIO ]" : "TOCCA PER SELEZIONARE";
                if (view.selectionIndicator != null)
                {
                    view.selectionIndicator.SetActive(selected);
                }
                view.visual.color = enemy.IsIncapacitated
                    ? Color.Lerp(enemyBaseColors[index], Color.gray, 0.62f)
                    : selected
                        ? Color.Lerp(enemyBaseColors[index], new Color(0.42f, 1f, 0.96f), 0.48f)
                        : enemyBaseColors[index];
                view.incapacitatedState.SetActive(enemy.IsIncapacitated);
                view.targetButton.interactable = !actionRunning && !analyzeOpen &&
                                                 battleState.CanSelectTarget(view.enemyId);
                EnemyTurnPlan plan = battleState.GetPlan(view.enemyId);
                if (plan == null)
                {
                    view.intentText.text = "INTENZIONE\nNESSUNA";
                    view.instabilityClue.SetActive(false);
                }
                else
                {
                    MultiEnemyIntent visible = plan.GetVisibleIntent(battleState.AnalyzedPlansRevealed);
                    view.intentText.text = "INTENZIONE\n" + GetIntentLabel(visible);
                    view.instabilityClue.SetActive(plan.IsBluff && !battleState.AnalyzedPlansRevealed);
                }

                view.guardEffect.SetActive(enemy.GuardPrepared);
                view.chargeEffect.SetActive(enemy.ChargePrepared);
            }

            techniqueButtonLabel.text = battleState.TechniqueCooldownRemaining == 0
                ? "TECNICA · DANNO " + heroSnapshot.CombatStats.TechniqueDamage +
                  " · PORTATA 2\nPRONTA"
                : "TECNICA · DANNO " + heroSnapshot.CombatStats.TechniqueDamage +
                  " · PORTATA 2\nRICARICA " + battleState.TechniqueCooldownRemaining + " TURNI";
            UpdatePhaseIndicator();
        }

        private void UpdatePhaseIndicator()
        {
            if (phaseIndicatorText == null || battleState == null)
            {
                return;
            }

            if (targetTutorialOverlay != null && targetTutorialOverlay.activeSelf)
            {
                phaseIndicatorText.text = "COME SCEGLIERE";
                return;
            }

            if (openingSequenceRunning)
            {
                phaseIndicatorText.text = "OSSERVA I NEMICI";
                return;
            }

            if (enemyPhasePresentationRunning)
            {
                phaseIndicatorText.text = "TURNO NEMICO";
                return;
            }

            switch (battleState.Phase)
            {
                case MultiEnemyBattlePhase.EnemyPhase:
                    phaseIndicatorText.text = "TURNO NEMICO";
                    return;
                case MultiEnemyBattlePhase.AwaitingMoralChoices:
                    phaseIndicatorText.text = moralChoiceUiStage == MoralChoiceUiStage.Review
                        ? "RIVEDI LE DECISIONI"
                        : "DECIDI IL SUO DESTINO";
                    return;
                case MultiEnemyBattlePhase.Completed:
                    phaseIndicatorText.text = "VITTORIA";
                    return;
                case MultiEnemyBattlePhase.HeroDefeated:
                    phaseIndicatorText.text = "SCONFITTA";
                    return;
                case MultiEnemyBattlePhase.HeroTurn:
                    if (analyzeOpen)
                    {
                        phaseIndicatorText.text = "ANALISI";
                    }
                    else if (actionRunning)
                    {
                        phaseIndicatorText.text = "AZIONE IN CORSO";
                    }
                    else
                    {
                        phaseIndicatorText.text = battleState.RequiresTargetSelection
                            ? "SCEGLI UN BERSAGLIO"
                            : "SCEGLI UN'AZIONE";
                    }

                    return;
                default:
                    phaseIndicatorText.text = string.Empty;
                    return;
            }
        }

        private void RefreshControls()
        {
            bool canAct = CanAcceptHeroInput();
            attackButton.interactable = canAct && battleState.CanUseHeroAction(MultiEnemyHeroAction.Attack);
            guardButton.interactable = canAct && battleState.CanUseHeroAction(MultiEnemyHeroAction.Guard);
            techniqueButton.interactable = canAct && battleState.CanUseHeroAction(MultiEnemyHeroAction.Technique);
            analyzeButton.interactable = canAct && !analyzeUsedThisTurn &&
                                         battleState.CanUseHeroAction(MultiEnemyHeroAction.Analyze);
        }

        private void SetActionButtons(bool enabledForInput)
        {
            attackButton.interactable = enabledForInput;
            guardButton.interactable = enabledForInput;
            techniqueButton.interactable = enabledForInput;
            analyzeButton.interactable = enabledForInput;
        }

        private bool CanAcceptHeroInput()
        {
            return battleState != null && !actionRunning && !analyzeOpen &&
                   battleState.Phase == MultiEnemyBattlePhase.HeroTurn;
        }

        private bool CanUseSelectedTacticalTarget(int range)
        {
            if (battlefield == null || battleState == null ||
                !enemyViewIndices.TryGetValue(battleState.SelectedEnemyId, out int index))
            {
                return battlefield == null;
            }

            bool valid = battlefield.CanUseOffensiveAction(range, enemyViews[index].actor);
            if (!valid)
            {
                combatMessageText.text = "BERSAGLIO FUORI PORTATA · USA MUOVI";
            }

            return valid;
        }

        private MultiEnemyPlayerTendencies LoadPersistentPlayerTendencies()
        {
            if (!CampaignProgressStore.CanEnemiesUsePlayerProfile(4))
            {
                return MultiEnemyPlayerTendencies.None;
            }

            var profile = CampaignProgressStore.GetPlayerActionProfile();
            MultiEnemyHeroAction? lastAction = profile.RecentActions.Count > 0
                ? ToBattleAction(profile.RecentActions[profile.RecentActions.Count - 1])
                : (MultiEnemyHeroAction?)null;
            return new MultiEnemyPlayerTendencies(
                profile.AttackCount,
                profile.GuardCount,
                profile.TechniqueCount,
                profile.AnalyzeCount,
                lastAction,
                profile.CurrentRepeatCount);
        }

        private static MultiEnemyHeroAction ToBattleAction(PlayerCombatAction action)
        {
            switch (action)
            {
                case PlayerCombatAction.Attack: return MultiEnemyHeroAction.Attack;
                case PlayerCombatAction.Guard: return MultiEnemyHeroAction.Guard;
                case PlayerCombatAction.Technique: return MultiEnemyHeroAction.Technique;
                case PlayerCombatAction.Analyze: return MultiEnemyHeroAction.Analyze;
                default: throw new ArgumentOutOfRangeException(nameof(action), action, null);
            }
        }

        private static void PersistPlayerAction(MultiEnemyHeroAction action)
        {
            switch (action)
            {
                case MultiEnemyHeroAction.Attack:
                    CampaignProgressStore.RecordPlayerAction(PlayerCombatAction.Attack);
                    break;
                case MultiEnemyHeroAction.Guard:
                    CampaignProgressStore.RecordPlayerAction(PlayerCombatAction.Guard);
                    break;
                case MultiEnemyHeroAction.Technique:
                    CampaignProgressStore.RecordPlayerAction(PlayerCombatAction.Technique);
                    break;
                case MultiEnemyHeroAction.Analyze:
                    CampaignProgressStore.RecordPlayerAction(PlayerCombatAction.Analyze);
                    break;
            }
        }

        private void SetHeroTurnPrompt()
        {
            if (!string.IsNullOrEmpty(battleState.LastAutoSelectedEnemyId))
            {
                combatMessageText.text = "UNICO BERSAGLIO RIMASTO · " +
                                         GetSelectedTargetName();
                return;
            }

            combatMessageText.text = battleState.RequiresTargetSelection
                ? "SCEGLI UN BERSAGLIO"
                : "BERSAGLIO MANTENUTO · " + GetSelectedTargetName() + " · SCEGLI UN'AZIONE";
        }

        private string BuildPersistentLearningDialogue()
        {
            MultiEnemyHeroAction? dominant = battleState.PlayerTendencies.DominantAction;
            if (!dominant.HasValue)
            {
                return string.Empty;
            }

            switch (dominant.Value)
            {
                case MultiEnemyHeroAction.Attack:
                    return "Maschera del Vento: «So che preferisci colpire per primo.»";
                case MultiEnemyHeroAction.Guard:
                    return "Veglia Sospesa: «Conosciamo la pazienza della tua Guardia.»";
                case MultiEnemyHeroAction.Technique:
                    return "Bruto delle Radici: «Aspetti sempre la tua Tecnica.»";
                case MultiEnemyHeroAction.Analyze:
                    return "Maschera del Vento: «Ci studi da molto tempo. Anche noi studiamo te.»";
                default:
                    return string.Empty;
            }
        }

        private void TryShowLearningDialogue(MultiEnemyHeroAction action)
        {
            if (battleState.PlayerTendencies.CurrentRepeatCount < 3 ||
                (lastLearningDialogueTurn != int.MinValue &&
                 battleState.TurnNumber - lastLearningDialogueTurn < 2))
            {
                return;
            }

            EnemyView speaker = null;
            for (int index = 0; index < enemyViews.Length; index++)
            {
                MultiEnemyEnemyState enemy = battleState.GetEnemy(enemyViews[index].enemyId);
                if (enemy == null || enemy.IsIncapacitated)
                {
                    continue;
                }

                if (speaker == null || enemyViews[index].intelligenceLevel > speaker.intelligenceLevel)
                {
                    speaker = enemyViews[index];
                }
            }

            if (speaker == null)
            {
                return;
            }

            string observation;
            switch (action)
            {
                case MultiEnemyHeroAction.Attack:
                    observation = "«Continui a scegliere l'attacco.»";
                    break;
                case MultiEnemyHeroAction.Guard:
                    observation = "«Ti affidi ancora alla Guardia.»";
                    break;
                case MultiEnemyHeroAction.Technique:
                    observation = "«Ripeti il ritmo della tua Tecnica.»";
                    break;
                case MultiEnemyHeroAction.Analyze:
                    observation = "«Continui a studiarci.»";
                    break;
                default:
                    return;
            }

            lastLearningDialogueTurn = battleState.TurnNumber;
            ShowDialogue(speaker.enemyId, observation);
        }

        private void CapturePersistentState()
        {
            heroBaseColor = heroVisual.color;
            enemyBaseColors.Clear();
            for (int index = 0; index < enemyViews.Length; index++)
            {
                enemyBaseColors.Add(enemyViews[index].visual.color);
            }
        }

        private void ResetPersistentVisuals()
        {
            heroVisual.color = heroBaseColor;
            heroGuardEffect.SetActive(false);
            heroAttackEffect.SetActive(false);
            heroTechniqueEffect.SetActive(false);
            thornGuardianSupportEffect.SetActive(false);
            ashWatcherSupportEffect.SetActive(false);
            for (int index = 0; index < enemyViews.Length; index++)
            {
                EnemyView view = enemyViews[index];
                view.visual.color = enemyBaseColors[index];
                if (view.selectionIndicator != null) view.selectionIndicator.SetActive(false);
                view.instabilityClue.SetActive(false);
                view.incapacitatedState.SetActive(false);
                view.guardEffect.SetActive(false);
                view.chargeEffect.SetActive(false);
                view.hitEffect.SetActive(false);
            }
        }

        private bool HasRequiredReferences()
        {
            bool valid = enemyViews != null && enemyViews.Length == 3 && heroActor != null &&
                         heroVisual != null && heroHealthFill != null && heroHealthText != null &&
                         attackButton != null && guardButton != null && techniqueButton != null &&
                         analyzeButton != null && techniqueButtonLabel != null &&
                         combatMessageText != null && selectedTargetText != null &&
                         heroStatusText != null && phaseIndicatorText != null &&
                         targetTutorialOverlay != null && targetTutorialText != null &&
                         targetTutorialContinueButton != null &&
                         analyzePanel != null && analyzeBodyText != null &&
                         moralChoicePanel != null && moralChoiceStateTexts != null &&
                         moralChoiceStateTexts.Length == 3 &&
                         moralCurrentIndicators != null &&
                         moralCurrentIndicators.Length == 3 &&
                         moralCurrentOutlines != null && moralCurrentOutlines.Length == 3 &&
                         moralSaveButtons != null &&
                         moralSaveButtons.Length == 3 && moralKillButtons != null &&
                         moralKillButtons.Length == 3 && moralSummaryText != null &&
                         moralConfirmButton != null && moralReviewButton != null &&
                         moralFocusTitleText != null && moralFocusBodyText != null &&
                         moralFocusPortrait != null && outcomePanel != null &&
                         outcomeMenuButton != null && outcomeRetryButton != null &&
                         outcomeRetryButtonLabel != null && navigation != null &&
                         dialogueRoot != null && dialogueText != null &&
                         allyDialogueRoot != null && allyDialogueText != null &&
                         thornGuardianAllyActor != null && ashWatcherAllyActor != null &&
                         thornGuardianSupportEffect != null && ashWatcherSupportEffect != null;
            if (valid)
            {
                for (int index = 0; index < enemyViews.Length; index++)
                {
                    EnemyView view = enemyViews[index];
                    valid &= view != null && !string.IsNullOrWhiteSpace(view.enemyId) &&
                             moralCurrentIndicators[index] != null &&
                             moralCurrentOutlines[index] != null &&
                             view.actor != null && view.visual != null && view.targetButton != null &&
                             view.nameText != null && view.healthText != null && view.healthFill != null &&
                             view.intentText != null && view.targetStateText != null &&
                             view.selectionIndicator != null &&
                             view.instabilityClue != null && view.incapacitatedState != null &&
                             view.guardEffect != null && view.chargeEffect != null && view.hitEffect != null;
                }
            }

            if (!valid) Debug.LogError("[Veyra L04] Riferimenti persistenti mancanti.", this);
            return valid;
        }

        private string BuildConfirmedMoralOutcomeSummary()
        {
            List<string> lines = new List<string>(enemyViews.Length);
            for (int index = 0; index < enemyViews.Length; index++)
            {
                lines.Add(enemyViews[index].displayName.ToUpperInvariant() + ": " +
                          GetConfirmedMoralOutcomeLabel(pendingMoralOutcomes[index]));
            }

            return string.Join("\n", lines);
        }

        private bool PlansStillLocked(IReadOnlyList<EnemyTurnPlan> before)
        {
            if (before.Count != battleState.CurrentPlans.Count) return false;
            for (int index = 0; index < before.Count; index++)
            {
                EnemyTurnPlan current = battleState.CurrentPlans[index];
                if (before[index].EnemyId != current.EnemyId ||
                    before[index].TrueIntent != current.TrueIntent ||
                    before[index].DisplayedIntent != current.DisplayedIntent)
                {
                    return false;
                }
            }

            return true;
        }

        private string BuildAnalyzeText(IReadOnlyList<EnemyIntel> intel)
        {
            List<string> blocks = new List<string>();
            foreach (EnemyIntel item in intel)
            {
                string intent = item.TrueIntent.HasValue
                    ? GetIntentLabel(item.TrueIntent.Value) +
                      (item.BluffRevealed ? " · BLUFF RIVELATO" : string.Empty)
                    : item.DisplayedIntent.HasValue
                        ? GetIntentLabel(item.DisplayedIntent.Value) +
                          (string.IsNullOrWhiteSpace(item.InstabilityClue)
                              ? string.Empty
                              : " · POSSIBILE INSTABILITÀ")
                        : "NESSUNA";
                blocks.Add(item.DisplayName.ToUpperInvariant() +
                           "\nRAZZA: " + item.Race + " · CORRUZIONE: " + item.CorruptionPercent + "%" +
                           "\nSTATO: " + GetMoodLabel(item.Mood) + " · HP " + item.CurrentHp + "/" + item.MaxHp +
                           "\nINTENZIONE: " + intent);
            }

            return string.Join("\n\n", blocks);
        }

        private void ShowDialogue(string enemyId, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            if (!string.IsNullOrWhiteSpace(enemyId) &&
                enemyViewIndices.TryGetValue(enemyId, out int enemyIndex) &&
                enemyViews[enemyIndex].worldDialogue != null)
            {
                EnemyView speaker = enemyViews[enemyIndex];
                speaker.worldDialogue.ShowDialogue(speaker.displayName, text);
                if (dialogueRoot != null) dialogueRoot.SetActive(false);
                return;
            }

            if (dialogueRoutine != null) StopCoroutine(dialogueRoutine);
            dialogueText.text = text;
            dialogueRoot.SetActive(true);
            dialogueRoutine = StartCoroutine(HideDialogue(dialogueRoot, false));
        }

        private void ShowAllyDialogue(SavedAllyId allyId, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            WorldDialogueBubbleView worldDialogue = allyId == SavedAllyId.AshWatcher
                ? ashWatcherWorldDialogue
                : thornGuardianWorldDialogue;
            if (worldDialogue != null)
            {
                string displayName = allyId == SavedAllyId.AshWatcher
                    ? "VEGLIANTE DELLE CENERI"
                    : "CUSTODE DEL ROVO";
                worldDialogue.ShowDialogue(displayName, text);
                if (allyDialogueRoot != null) allyDialogueRoot.SetActive(false);
                return;
            }

            if (allyDialogueRoutine != null) StopCoroutine(allyDialogueRoutine);
            allyDialogueText.text = text;
            allyDialogueRoot.SetActive(true);
            allyDialogueRoutine = StartCoroutine(HideDialogue(allyDialogueRoot, true));
        }

        private IEnumerator HideDialogue(GameObject root, bool ally)
        {
            yield return new WaitForSecondsRealtime(2.7f);
            root.SetActive(false);
            if (ally) allyDialogueRoutine = null;
            else dialogueRoutine = null;
        }

        private void TryShowDifficultyDialogue(SavedAllySupport support)
        {
            if (support != null && support.TryGetHeroDifficultyDialogue(
                    battleState.HeroHp,
                    battleState.HeroMaxHp,
                    out SavedAllyDialogueLine line))
            {
                ShowAllyDialogue(support.Definition.AllyId, line.Text);
            }
        }

        private string GetSelectedTargetName()
        {
            if (string.IsNullOrEmpty(battleState.SelectedEnemyId)) return "NESSUNO";
            return enemyViewIndices.TryGetValue(battleState.SelectedEnemyId, out int index)
                ? enemyViews[index].displayName.ToUpperInvariant()
                : "NESSUNO";
        }

        private static string BuildEnemyActionMessage(EnemyView view, EnemyActionResolution action)
        {
            switch (action.Plan.TrueIntent)
            {
                case MultiEnemyIntent.Attack:
                case MultiEnemyIntent.Assault:
                case MultiEnemyIntent.ChargedStrike:
                    return view.displayName + " usa " + GetIntentLabel(action.Plan.TrueIntent) +
                           (action.BlockedByGuard ? " · PARATO" : " · " + action.DamageDealt + " DANNI");
                case MultiEnemyIntent.Guard:
                    return view.displayName + " prepara GUARDIA";
                case MultiEnemyIntent.Finta:
                    return view.displayName + " usa FINTA · nessun danno";
                case MultiEnemyIntent.Charge:
                    return view.displayName + " CARICA il colpo";
                case MultiEnemyIntent.HoldCharge:
                    return view.displayName + " TRATTIENE LA CARICA";
                default:
                    return view.displayName + " ATTENDE";
            }
        }

        private static string GetIntentLabel(MultiEnemyIntent intent)
        {
            switch (intent)
            {
                case MultiEnemyIntent.Attack: return "ATTACCO";
                case MultiEnemyIntent.Guard: return "GUARDIA";
                case MultiEnemyIntent.Wait: return "ATTESA";
                case MultiEnemyIntent.Finta: return "FINTA";
                case MultiEnemyIntent.Charge: return "CARICA";
                case MultiEnemyIntent.HoldCharge: return "TRATTIENE CARICA";
                case MultiEnemyIntent.ChargedStrike: return "COLPO CARICATO";
                case MultiEnemyIntent.Assault: return "ASSALTO";
                default: return "SCONOSCIUTA";
            }
        }

        private static string GetMoodLabel(EnemyMood mood)
        {
            switch (mood)
            {
                case EnemyMood.Felice: return "Felice";
                case EnemyMood.Triste: return "Triste";
                case EnemyMood.Arrabbiato: return "Arrabbiato";
                case EnemyMood.Guardingo: return "Guardingo";
                case EnemyMood.Spaventato: return "Spaventato";
                case EnemyMood.Rassegnato: return "Rassegnato";
                default: return "Sconosciuto";
            }
        }

        private static string GetMoralLabel(EnemyMoralOutcome outcome)
        {
            switch (outcome)
            {
                case EnemyMoralOutcome.Saved: return "SALVA";
                case EnemyMoralOutcome.Killed: return "UCCIDI";
                default: return "DA SCEGLIERE";
            }
        }

        private static string GetConfirmedMoralOutcomeLabel(EnemyMoralOutcome outcome)
        {
            switch (outcome)
            {
                case EnemyMoralOutcome.Saved: return "SALVATO";
                case EnemyMoralOutcome.Killed: return "UCCISO";
                default: return "NON DECISO";
            }
        }

        private static EncounterResolution ToCampaignResolution(EnemyMoralOutcome outcome)
        {
            return outcome == EnemyMoralOutcome.Saved
                ? EncounterResolution.Saved
                : EncounterResolution.Killed;
        }

        private static EnemyMoralOutcome ToBattleOutcome(EncounterResolution resolution)
        {
            return resolution == EncounterResolution.Saved
                ? EnemyMoralOutcome.Saved
                : EnemyMoralOutcome.Killed;
        }

        private EnemyMoralOutcome GetPersistedBattleOutcome(
            CampaignProgressData campaign,
            int enemyIndex)
        {
            EncounterResolution resolution;
            return enemyIndex >= 0 && enemyIndex < enemyViews.Length &&
                   CampaignProgressStore.TryGetEnemyResolution(
                       campaign,
                       CampaignContentIds.Level04ThreefoldAssault,
                       enemyViews[enemyIndex].enemyId,
                       out resolution)
                ? ToBattleOutcome(resolution)
                : EnemyMoralOutcome.None;
        }

        private bool HasMoralStoryChanged()
        {
            if (!moralChoiceIsReplay)
            {
                return false;
            }

            for (int index = 0; index < pendingMoralOutcomes.Length; index++)
            {
                if (pendingMoralOutcomes[index] != originalMoralOutcomes[index])
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerator Flash(SpriteRenderer target, Color color, float duration)
        {
            Color original = target.color;
            target.color = color;
            yield return new WaitForSecondsRealtime(duration);
            target.color = original;
        }

        private static IEnumerator Pulse(GameObject target, float multiplier, float duration)
        {
            Vector3 original = target.transform.localScale;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                float factor = normalized < 0.5f
                    ? Mathf.Lerp(1f, multiplier, normalized * 2f)
                    : Mathf.Lerp(multiplier, 1f, (normalized - 0.5f) * 2f);
                target.transform.localScale = original * factor;
                yield return null;
            }

            target.transform.localScale = original;
        }
    }
}
