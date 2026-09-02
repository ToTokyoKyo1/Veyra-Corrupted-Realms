using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Veyra.Core;
using Veyra.Combat.Tactical;
using Veyra.Progression;
using Veyra.UI.Battle;

namespace Veyra.Combat.Tutorial
{
    public enum TutorialStep
    {
        Welcome,
        Positions,
        Health,
        AwaitingMove,
        AwaitingFirstAttack,
        EnemyCounterattack,
        AwaitingGuard,
        AwaitingTechnique,
        AwaitingAnalyze,
        EnemyLearning,
        VictoryGoal,
        EnemyIncapacitated,
        FinalChoice,
        Complete
    }

    public sealed class TutorialBattleController : MonoBehaviour
    {
        [Header("Combat tuning")]
        [SerializeField, Min(1)] private int heroMaxHp = 100;
        [SerializeField, Min(1)] private int enemyMaxHp = 100;
        [SerializeField, Min(1)] private int attackDamage = 20;
        [SerializeField, Min(1)] private int techniqueDamage = 32;
        [SerializeField, Min(1)] private int enemyAttackDamage = 25;
        [SerializeField, Min(1)] private int techniqueCooldownTurns = 2;
        [SerializeField, Range(0, 2)] private int enemyIntelligenceLevel;
        [SerializeField, Min(0.1f)] private float resultReturnDelay = 2.5f;

        [Header("Enemy profile")]
        [SerializeField] private string enemyDisplayName = "Creatura Corrotta";
        [SerializeField] private string enemyRace = "Creatura delle Radici";
        [SerializeField, Range(0, 100)] private int enemyCorruptionPercent = 70;
        [SerializeField] private EnemyMood enemyMood = EnemyMood.Arrabbiato;

        [Header("Action controls")]
        [SerializeField] private Button attackButton;
        [SerializeField] private Button guardButton;
        [SerializeField] private Button techniqueButton;
        [SerializeField] private Button analyzeButton;
        [SerializeField] private TMP_Text techniqueButtonLabel;
        [SerializeField] private GameObject attackHighlight;
        [SerializeField] private GameObject guardHighlight;
        [SerializeField] private GameObject techniqueHighlight;
        [SerializeField] private GameObject analyzeHighlight;

        [Header("HUD")]
        [SerializeField] private TMP_Text combatMessage;
        [SerializeField] private TMP_Text phaseText;
        [SerializeField] private TMP_Text intentText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Image heroHealthFill;
        [SerializeField] private Image enemyHealthFill;
        [SerializeField] private TMP_Text heroHealthValue;
        [SerializeField] private TMP_Text enemyHealthValue;

        [Header("Contextual world HUD")]
        [SerializeField] private WorldHealthBarView heroWorldHealthBar;
        [SerializeField] private WorldHealthBarView enemyWorldHealthBar;

        [Header("Characters")]
        [SerializeField] private Transform heroActor;
        [SerializeField] private Transform enemyActor;
        [SerializeField] private SpriteRenderer heroVisual;
        [SerializeField] private SpriteRenderer enemyVisual;

        [Header("Tactical battlefield")]
        [SerializeField] private TacticalBattlefieldController battlefield;
        [SerializeField] private Transform heroProjectileOrigin;
        [SerializeField] private Transform heroHitTarget;
        [SerializeField] private Transform enemyProjectileOrigin;
        [SerializeField] private Transform enemyHitTarget;

        [Header("Persistent effects")]
        [SerializeField] private GameObject heroBasicProjectile;
        [SerializeField] private GameObject heroTechniqueProjectile;
        [SerializeField] private GameObject enemyProjectile;
        [SerializeField] private GameObject guardVisual;

        [Header("Tutorial overlay")]
        [SerializeField] private GameObject tutorialOverlay;
        [SerializeField] private Image tutorialInputBlocker;
        [SerializeField] private TMP_Text tutorialStepText;
        [SerializeField] private TMP_Text tutorialBodyText;
        [SerializeField] private Button tutorialNextButton;
        [SerializeField] private Button tutorialRepeatButton;
        [SerializeField] private Button tutorialSkipButton;

        [Header("Analyze panel")]
        [SerializeField] private GameObject analyzePanel;
        [SerializeField] private TMP_Text analyzeNameText;
        [SerializeField] private TMP_Text analyzeRaceText;
        [SerializeField] private TMP_Text analyzeCorruptionText;
        [SerializeField] private TMP_Text analyzeMoodText;
        [SerializeField] private Button analyzeCloseButton;

        [Header("Final choice")]
        [SerializeField] private GameObject finalChoicePanel;
        [SerializeField] private TMP_Text finalChoiceTitleText;
        [SerializeField] private Image finalChoicePortrait;
        [SerializeField] private TMP_Text finalChoiceProfileText;
        [SerializeField] private TMP_Text finalChoiceDialogueText;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button killButton;

        [Header("Choice confirmation")]
        [SerializeField] private GameObject confirmationPanel;
        [SerializeField] private TMP_Text confirmationText;
        [SerializeField] private Button confirmationConfirmButton;
        [SerializeField] private Button confirmationBackButton;

        [Header("Outcome overlay")]
        [SerializeField] private GameObject outcomeOverlay;
        [SerializeField] private TMP_Text outcomeText;
        [SerializeField] private TMP_Text outcomeProgressText;
        [SerializeField] private Button outcomeMenuButton;
        [SerializeField] private Button outcomeContinueButton;
        [SerializeField] private Button outcomeRetryButton;
        [SerializeField] private TutorialBattleNavigation navigation;

        private const string PlayerTurnMessage = "Scegli la tua azione";
        private const string MoralConsequencesText =
            "\n\nSALVA: resta vivo; potrà tornare o aiutarti." +
            "\nUCCIDI: esce dalla storia; non potrà aiutarti.";
        private const float RecordedChoiceScale = 1.06f;

        private static readonly Color RecordedSaveColor =
            new Color(0.30f, 0.78f, 0.58f, 1f);
        private static readonly Color RecordedKillColor =
            new Color(0.88f, 0.38f, 0.40f, 1f);

        private TutorialBattleState battleState;
        private bool actionRunning;
        private bool analyzePanelOpen;
        private bool analyzeUsedThisTurn;
        private bool waitingForTutorialAdvance;
        private bool tutorialAdvanceRequested;
        private bool finalChoiceOpen;
        private bool confirmationOpen;
        private bool? pendingSaveChoice;
        private bool isReplayBattle;
        private bool tutorialExplanationsSkipped;
        private bool repeatedPatternMessageShown;
        private bool rewardGrantedThisBattle;
        private string lastTutorialStepLabel = string.Empty;
        private string lastTutorialBody = string.Empty;
        private bool lastTutorialCardWasBlocking;
        private Vector3 heroBasePosition;
        private Vector3 enemyBasePosition;
        private Color heroBaseColor;
        private Color enemyBaseColor;
        private Vector3 basicProjectileBaseScale;
        private Vector3 techniqueProjectileBaseScale;
        private Vector3 enemyProjectileBaseScale;
        private Vector3 guardBaseScale;
        private ColorBlock saveButtonNeutralColors;
        private ColorBlock killButtonNeutralColors;
        private Vector3 saveButtonNeutralScale;
        private Vector3 killButtonNeutralScale;
        private HeroCombatPresentation heroCombatPresentation;

        public int HeroCurrentHp => battleState?.HeroHp ?? heroMaxHp;
        public int EnemyCurrentHp => battleState?.EnemyHp ?? enemyMaxHp;
        public BattleOutcome Outcome => battleState?.Outcome ?? BattleOutcome.Ongoing;
        public TutorialStep CurrentTutorialStep { get; private set; }
        public bool IsActionRunning => actionRunning;
        public bool IsAnalyzePanelOpen => analyzePanelOpen;
        public bool IsFinalChoiceOpen => finalChoiceOpen;
        public bool IsConfirmationOpen => confirmationOpen;
        public bool IsTutorialComplete => CurrentTutorialStep == TutorialStep.Complete;
        public int EnemyIntelligenceLevel => enemyIntelligenceLevel;
        public int EnemyCorruptionPercent => ClampCorruptionPercent(enemyCorruptionPercent);
        public bool AnalyzeUsedThisTurn => analyzeUsedThisTurn;

        private void Awake()
        {
            CapturePersistentVisualState();
            heroCombatPresentation = HeroCombatPresentation.Ensure(heroActor);
            InitializeBattle();
        }

        private void OnValidate()
        {
            heroMaxHp = Mathf.Max(1, heroMaxHp);
            enemyMaxHp = Mathf.Max(1, enemyMaxHp);
            attackDamage = Mathf.Clamp(attackDamage, 1, int.MaxValue - 1);
            techniqueDamage = Mathf.Max(attackDamage + 1, techniqueDamage);
            enemyAttackDamage = Mathf.Max(1, enemyAttackDamage);
            techniqueCooldownTurns = Mathf.Max(1, techniqueCooldownTurns);
            enemyCorruptionPercent = ClampCorruptionPercent(enemyCorruptionPercent);
            resultReturnDelay = Mathf.Max(0.1f, resultReturnDelay);
        }

        private void Update()
        {
            if (CurrentTutorialStep != TutorialStep.AwaitingMove || battlefield == null ||
                !battlefield.MovementUsed || actionRunning || analyzePanelOpen)
            {
                return;
            }

            ShowActionPrompt(
                TutorialStep.AwaitingFirstAttack,
                "PASSO 6 / 18",
                "Ottimo. Il bersaglio è ora nella portata rossa dell'ATTACCO. La TECNICA raggiunge fino a due pedane.",
                BattleAction.Attack);
        }

        public void AdvanceTutorial()
        {
            if (waitingForTutorialAdvance)
            {
                tutorialAdvanceRequested = true;
                tutorialNextButton.interactable = false;
                return;
            }

            if (actionRunning || analyzePanelOpen || battleState == null || battleState.IsFinished)
            {
                return;
            }

            switch (CurrentTutorialStep)
            {
                case TutorialStep.Welcome:
                    CurrentTutorialStep = TutorialStep.Positions;
                    ShowBlockingTutorial(
                        "PASSO 2 / 18",
                        "Tu controlli l'eroe a sinistra. Il tuo avversario è la creatura corrotta a destra.");
                    break;
                case TutorialStep.Positions:
                    CurrentTutorialStep = TutorialStep.Health;
                    ShowBlockingTutorial(
                        "PASSO 3 / 18",
                        "Le barre mostrano gli HP. A zero, un nemico è incapacitato; se i tuoi HP arrivano a zero, perdi.");
                    break;
                case TutorialStep.Health:
                    ShowMovePrompt();
                    break;
            }
        }

        public void PreviewAttack()
        {
            if (battlefield != null && !battlefield.CanUseOffensiveAction(1, enemyActor))
            {
                combatMessage.text = "Bersaglio fuori portata · usa MUOVI";
                return;
            }
            BeginPlayerAction(BattleAction.Attack);
        }

        public void PreviewGuard()
        {
            BeginPlayerAction(BattleAction.Guard);
        }

        public void PreviewTechnique()
        {
            if (battlefield != null && !battlefield.CanUseOffensiveAction(2, enemyActor))
            {
                combatMessage.text = "Bersaglio fuori portata della Tecnica";
                return;
            }
            BeginPlayerAction(BattleAction.Technique);
        }

        public void BeginTacticalMove()
        {
            if (battlefield != null &&
                (IsTutorialComplete || CurrentTutorialStep == TutorialStep.AwaitingMove ||
                 CurrentTutorialStep == TutorialStep.AwaitingFirstAttack))
            {
                battlefield.ToggleMoveMode();
            }
        }

        public void EndTacticalTurn()
        {
            if (battlefield == null || !IsTutorialComplete || actionRunning ||
                !battleState.PassPlayerTurn())
            {
                return;
            }

            actionRunning = true;
            SetAllActionButtons(false);
            StartCoroutine(ResolvePassedTurn());
        }

        public void PreviewAnalyze()
        {
            if (actionRunning || analyzePanelOpen || battleState == null || battleState.IsFinished)
            {
                return;
            }

            if (analyzeUsedThisTurn)
            {
                combatMessage.text = "ANALIZZA GIÀ USATO · DISPONIBILE AL PROSSIMO TURNO";
                RefreshActionButtons();
                return;
            }

            bool isGuidedAnalyze = CurrentTutorialStep == TutorialStep.AwaitingAnalyze;
            if (!isGuidedAnalyze && !IsTutorialComplete)
            {
                return;
            }

            BattleActionResult result = battleState.ResolvePlayerAction(BattleAction.Analyze);
            if (!result.Accepted)
            {
                combatMessage.text = result.RejectionReason;
                RefreshActionButtons();
                return;
            }

            CampaignProgressStore.TryRecordPlayerAction(BattleAction.Analyze.ToString());
            analyzeUsedThisTurn = true;

            tutorialOverlay.SetActive(false);
            SetAllHighlights(false);
            SetAllActionButtons(false);
            PopulateAnalyzePanel();
            analyzeCloseButton.interactable = true;
            analyzePanelOpen = true;
            analyzePanel.SetActive(true);
            SetPhase("ANALIZZA · DOSSIER NEMICO");
            combatMessage.text = battleState.IsEnemyExposed
                ? "VISTA DELLA CORRUZIONE: ESPOSTO applicato · prossimo danno +25%"
                : "Informazioni sul nemico";
        }

        public void CloseAnalyzePanel()
        {
            if (!analyzePanelOpen)
            {
                return;
            }

            bool completesGuidedAnalyze = CurrentTutorialStep == TutorialStep.AwaitingAnalyze;
            analyzeCloseButton.interactable = false;
            analyzePanel.SetActive(false);
            analyzePanelOpen = false;
            SetPhase("TUO TURNO · SCEGLI UN'AZIONE");

            if (completesGuidedAnalyze)
            {
                StartCoroutine(CompleteAnalyzeTutorial());
                return;
            }

            combatMessage.text = "Analisi completata: nessun turno consumato";
            RefreshActionButtons();
        }

        public void RepeatCurrentExplanation()
        {
            if (actionRunning || finalChoiceOpen || confirmationOpen ||
                string.IsNullOrWhiteSpace(lastTutorialBody))
            {
                return;
            }

            tutorialStepText.text = lastTutorialStepLabel;
            tutorialBodyText.text = lastTutorialBody;
            tutorialInputBlocker.raycastTarget = lastTutorialCardWasBlocking;
            tutorialNextButton.gameObject.SetActive(lastTutorialCardWasBlocking);
            tutorialNextButton.interactable = true;
            tutorialOverlay.SetActive(true);
        }

        public void SkipTutorialExplanations()
        {
            if (!isReplayBattle || analyzePanelOpen || finalChoiceOpen ||
                confirmationOpen || battleState == null || battleState.IsFinished)
            {
                return;
            }

            tutorialExplanationsSkipped = true;
            waitingForTutorialAdvance = false;
            tutorialAdvanceRequested = true;
            CurrentTutorialStep = TutorialStep.Complete;
            tutorialOverlay.SetActive(false);
            SetAllHighlights(false);
            if (!actionRunning)
            {
                SetPhase("TUO TURNO · SCEGLI UN'AZIONE");
                combatMessage.text = PlayerTurnMessage;
                RefreshActionButtons();
            }
        }

        public void ChooseSave()
        {
            OpenFinalConfirmation(true);
        }

        public void ChooseKill()
        {
            OpenFinalConfirmation(false);
        }

        public void ConfirmFinalChoice()
        {
            if (!confirmationOpen || !pendingSaveChoice.HasValue || battleState == null ||
                battleState.Outcome != BattleOutcome.Victory)
            {
                return;
            }

            bool save = pendingSaveChoice.Value;
            EncounterResolution resolution = save
                ? EncounterResolution.Saved
                : EncounterResolution.Killed;
            bool hadPreviousResolution = CampaignProgressStore.TryGetEnemyResolution(
                CampaignContentIds.Level01Tutorial,
                CampaignContentIds.TutorialEnemy,
                out EncounterResolution previousResolution);
            bool replayChoiceChanged = hadPreviousResolution && previousResolution != resolution;
            bool rewardWasClaimed = CampaignProgressStore.IsLevelRewardClaimed(
                CampaignContentIds.Level01Tutorial);
            CampaignProgressStore.SetTutorialResolution(resolution);
            rewardGrantedThisBattle = !rewardWasClaimed &&
                                      CampaignProgressStore.IsLevelRewardClaimed(
                                          CampaignContentIds.Level01Tutorial);

            confirmationPanel.SetActive(false);
            confirmationOpen = false;
            finalChoicePanel.SetActive(false);
            finalChoiceOpen = false;
            pendingSaveChoice = null;
            ShowOutcome(resolution, replayChoiceChanged);
        }

        public void BackFromFinalConfirmation()
        {
            if (!confirmationOpen)
            {
                return;
            }

            pendingSaveChoice = null;
            confirmationPanel.SetActive(false);
            confirmationOpen = false;
            finalChoicePanel.SetActive(true);
            finalChoiceOpen = true;
            saveButton.interactable = true;
            killButton.interactable = true;
            RefreshRecordedMoralChoiceVisual();
            SetPhase("DECIDI IL SUO DESTINO");
        }

        public void ReturnToMenu()
        {
            if (navigation != null)
            {
                navigation.BackToMenu();
            }
        }

        public void CancelRunningActionForSceneChange()
        {
            StopAllCoroutines();
            actionRunning = false;
            analyzePanelOpen = false;
            finalChoiceOpen = false;
            confirmationOpen = false;
            pendingSaveChoice = null;
            waitingForTutorialAdvance = false;
            tutorialAdvanceRequested = false;
            analyzePanel.SetActive(false);
            if (finalChoicePanel != null) finalChoicePanel.SetActive(false);
            if (confirmationPanel != null) confirmationPanel.SetActive(false);
            ResetPersistentEffects();
            RestoreMoralChoiceButtonVisuals();
            SetAllHighlights(false);
            SetAllActionButtons(false);
        }

        public void ShowExternalMessage(string message)
        {
            if (combatMessage != null)
            {
                combatMessage.text = message;
            }
        }

        private void CapturePersistentVisualState()
        {
            heroBasePosition = heroActor.localPosition;
            enemyBasePosition = enemyActor.localPosition;
            heroBaseColor = heroVisual.color;
            enemyBaseColor = enemyVisual.color;
            basicProjectileBaseScale = heroBasicProjectile.transform.localScale;
            techniqueProjectileBaseScale = heroTechniqueProjectile.transform.localScale;
            enemyProjectileBaseScale = enemyProjectile.transform.localScale;
            guardBaseScale = guardVisual.transform.localScale;
            saveButtonNeutralColors = saveButton.colors;
            killButtonNeutralColors = killButton.colors;
            saveButtonNeutralScale = saveButton.transform.localScale;
            killButtonNeutralScale = killButton.transform.localScale;
        }

        private void InitializeBattle()
        {
            HeroCombatStats heroStats = HeroProgressStore.GetCombatStats();
            heroMaxHp = heroStats.MaxHp;
            attackDamage = heroStats.AttackDamage;
            techniqueDamage = heroStats.TechniqueDamage;

            battleState = new TutorialBattleState(
                heroMaxHp,
                enemyMaxHp,
                attackDamage,
                techniqueDamage,
                enemyAttackDamage,
                techniqueCooldownTurns,
                historyCapacity: 8,
                repeatedPatternLength: GetObservationLengthForIntelligence(),
                analyzeAppliesExposed: heroStats.AnalyzeAppliesExposed,
                exposedDamagePercent: heroStats.ExposedDamagePercent);

            actionRunning = false;
            analyzePanelOpen = false;
            analyzeUsedThisTurn = false;
            waitingForTutorialAdvance = false;
            tutorialAdvanceRequested = false;
            repeatedPatternMessageShown = false;
            rewardGrantedThisBattle = false;
            finalChoiceOpen = false;
            confirmationOpen = false;
            pendingSaveChoice = null;
            isReplayBattle = CampaignProgressStore.Load().tutorialCompleted;
            tutorialExplanationsSkipped = false;
            CurrentTutorialStep = TutorialStep.Welcome;

            ResetPersistentEffects();
            RestoreMoralChoiceButtonVisuals();
            PopulateAnalyzePanel();
            analyzePanel.SetActive(false);
            if (finalChoicePanel != null) finalChoicePanel.SetActive(false);
            if (confirmationPanel != null) confirmationPanel.SetActive(false);
            analyzeCloseButton.interactable = true;
            UpdateHealthImmediate();
            heroWorldHealthBar?.SetHealthSilently(battleState.HeroHp, battleState.HeroMaxHp);
            enemyWorldHealthBar?.SetHealthSilently(battleState.EnemyHp, battleState.EnemyMaxHp);
            UpdateStatusAndCooldown();
            intentText.text = "INTENZIONE\nATTACCO IN ARRIVO";
            combatMessage.text = "Impara le basi del combattimento";
            outcomeOverlay.SetActive(false);
            if (outcomeRetryButton != null)
            {
                outcomeRetryButton.gameObject.SetActive(false);
                outcomeRetryButton.interactable = false;
            }
            outcomeMenuButton.interactable = true;
            if (outcomeContinueButton != null)
            {
                outcomeContinueButton.gameObject.SetActive(false);
                outcomeContinueButton.interactable = false;
            }
            SetAllHighlights(false);
            if (tutorialRepeatButton != null)
            {
                tutorialRepeatButton.gameObject.SetActive(true);
                tutorialRepeatButton.interactable = true;
            }
            if (tutorialSkipButton != null)
            {
                tutorialSkipButton.gameObject.SetActive(isReplayBattle);
                tutorialSkipButton.interactable = isReplayBattle;
            }
            SetPhase("TUTORIAL · OSSERVA");
            ShowBlockingTutorial(
                "PASSO 1 / 18",
                "Benvenuto in Veyra. Imparerai movimento, portata, azioni e la scelta morale senza nascondere l'arena.");
        }

        private void BeginPlayerAction(BattleAction action)
        {
            if (actionRunning || analyzePanelOpen || battleState == null || battleState.IsFinished)
            {
                return;
            }

            TutorialStep startingStep = CurrentTutorialStep;
            if (!IsActionAllowedByTutorial(action, startingStep))
            {
                return;
            }

            if (!battleState.CanUsePlayerAction(action))
            {
                combatMessage.text = action == BattleAction.Technique
                    ? "La Tecnica non è ancora pronta"
                    : "Questa azione non è disponibile";
                RefreshActionButtons();
                return;
            }

            actionRunning = true;
            if (battlefield != null)
            {
                battlefield.CommitAction();
            }
            SetPhase("AZIONE DI HERO01");
            tutorialOverlay.SetActive(false);
            SetAllHighlights(false);
            SetAllActionButtons(false);
            StartCoroutine(ResolveTurn(action, startingStep));
        }

        private IEnumerator ResolveTurn(BattleAction action, TutorialStep startingStep)
        {
            int enemyHpBefore = battleState.EnemyHp;
            BattleActionResult playerResult;

            switch (action)
            {
                case BattleAction.Attack:
                    combatMessage.text = "Hero01 attacca";
                    if (heroCombatPresentation != null)
                        yield return heroCombatPresentation.PlayMelee(enemyActor, false);
                    playerResult = battleState.ResolvePlayerAction(action);
                    enemyWorldHealthBar?.ShowDamage(
                        enemyHpBefore,
                        battleState.EnemyHp,
                        battleState.EnemyMaxHp);
                    yield return Flash(enemyVisual, Color.white, 0.16f);
                    yield return AnimateHealth(
                        enemyHealthFill,
                        enemyHealthValue,
                        enemyHpBefore,
                        battleState.EnemyHp,
                        battleState.EnemyMaxHp,
                        0.20f);
                    break;
                case BattleAction.Technique:
                    combatMessage.text = "Hero01 usa Tecnica";
                    if (heroCombatPresentation != null)
                        yield return heroCombatPresentation.PlayMelee(enemyActor, true);
                    playerResult = battleState.ResolvePlayerAction(action);
                    enemyWorldHealthBar?.ShowDamage(
                        enemyHpBefore,
                        battleState.EnemyHp,
                        battleState.EnemyMaxHp);
                    yield return Flash(enemyVisual, new Color(0.73f, 1f, 0.94f, 1f), 0.24f);
                    yield return AnimateHealth(
                        enemyHealthFill,
                        enemyHealthValue,
                        enemyHpBefore,
                        battleState.EnemyHp,
                        battleState.EnemyMaxHp,
                        0.22f);
                    break;
                case BattleAction.Guard:
                    combatMessage.text = "Guardia preparata";
                    playerResult = battleState.ResolvePlayerAction(action);
                    guardVisual.SetActive(true);
                    yield return PulseScale(guardVisual, guardBaseScale, 1.28f, 0.34f);
                    break;
                default:
                    actionRunning = false;
                    RefreshActionButtons();
                    yield break;
            }

            if (!playerResult.Accepted)
            {
                combatMessage.text = playerResult.RejectionReason;
                actionRunning = false;
                RefreshActionButtons();
                yield break;
            }

            CampaignProgressStore.TryRecordPlayerAction(action.ToString());

            UpdateStatusAndCooldown();

            if (battleState.IsFinished)
            {
                if (battleState.Outcome == BattleOutcome.Victory)
                {
                    EnterFinalChoice();
                }
                else
                {
                    ShowOutcome();
                }
                yield break;
            }

            bool repeatedPattern = battleState.TryGetRepeatedPlayerAction(out _);

            if (startingStep == TutorialStep.AwaitingFirstAttack)
            {
                yield return WaitForTutorialCard(
                    TutorialStep.EnemyCounterattack,
                    "PASSO 7 / 18",
                    "Ora il nemico contrattacca. Anche i suoi attacchi riducono i tuoi HP.");
            }

            yield return ResolveEnemyTurn();
            if (battlefield != null && !battleState.IsFinished)
            {
                analyzeUsedThisTurn = false;
                battlefield.BeginHeroTurn();
            }
            if (battleState.IsFinished)
            {
                ShowOutcome();
                yield break;
            }

            ResetTransientEffects();
            UpdateStatusAndCooldown();

            if (tutorialExplanationsSkipped)
            {
                actionRunning = false;
                CurrentTutorialStep = TutorialStep.Complete;
                ShowPlayerTurnMessage(repeatedPattern);
                RefreshActionButtons();
                yield break;
            }

            if (startingStep == TutorialStep.AwaitingFirstAttack)
            {
                FinishWithActionPrompt(
                    TutorialStep.AwaitingGuard,
                    "PASSO 8 / 18",
                    "Premi GUARDIA: il prossimo attacco diretto parabile infliggerà esattamente zero danni, poi la Guardia sarà consumata.",
                    BattleAction.Guard);
                yield break;
            }

            if (startingStep == TutorialStep.AwaitingGuard)
            {
                FinishWithActionPrompt(
                    TutorialStep.AwaitingTechnique,
                    "PASSO 10 / 18",
                    "TECNICA è la mossa speciale: più danno, portata 2 e ricarica di " + techniqueCooldownTurns + " turni.",
                    BattleAction.Technique);
                yield break;
            }

            if (startingStep == TutorialStep.AwaitingTechnique)
            {
                FinishWithActionPrompt(
                    TutorialStep.AwaitingAnalyze,
                    "PASSO 12 / 18",
                    "ANALIZZA è gratuito una volta per turno. Mostra razza, corruzione, stato emotivo e intenzione senza consumare l'azione.",
                    BattleAction.Analyze);
                yield break;
            }

            actionRunning = false;
            ShowPlayerTurnMessage(repeatedPattern);
            RefreshActionButtons();
        }

        private IEnumerator ResolveEnemyTurn()
        {
            SetPhase("TURNO NEMICO");
            combatMessage.text = "Turno nemico";
            yield return new WaitForSecondsRealtime(0.16f);
            yield return MoveActor(
                enemyActor,
                enemyBasePosition,
                enemyBasePosition + Vector3.left * 0.72f,
                0.16f);
            yield return MoveEffect(
                enemyProjectile,
                enemyProjectileOrigin.position,
                heroHitTarget.position,
                0.26f);

            int heroHpBefore = battleState.HeroHp;
            BattleActionResult enemyResult = battleState.ResolveEnemyAttack();
            heroWorldHealthBar?.ShowDamage(
                heroHpBefore,
                battleState.HeroHp,
                battleState.HeroMaxHp);
            if (enemyResult.BlockedByGuard)
            {
                combatMessage.text = "PARATO";
                guardVisual.SetActive(true);
                yield return PulseScale(guardVisual, guardBaseScale, 1.36f, 0.28f);
                yield return Flash(heroVisual, new Color(0.73f, 1f, 0.94f, 1f), 0.16f);
            }
            else
            {
                combatMessage.text = "Hero01 subisce " + enemyResult.DamageDealt + " danni";
                yield return Flash(heroVisual, new Color(1f, 0.70f, 0.70f, 1f), 0.16f);
            }

            yield return AnimateHealth(
                heroHealthFill,
                heroHealthValue,
                heroHpBefore,
                battleState.HeroHp,
                battleState.HeroMaxHp,
                0.20f);
            yield return MoveActor(enemyActor, enemyActor.localPosition, enemyBasePosition, 0.16f);

            if (enemyResult.BlockedByGuard)
            {
                yield return new WaitForSecondsRealtime(0.35f);
            }
        }

        private IEnumerator ResolvePassedTurn()
        {
            combatMessage.text = "Hero01 passa il turno";
            yield return ResolveEnemyTurn();
            if (battleState.IsFinished)
            {
                ShowOutcome();
                yield break;
            }

            actionRunning = false;
            analyzeUsedThisTurn = false;
            battlefield.BeginHeroTurn();
            RefreshActionButtons();
        }

        private IEnumerator CompleteAnalyzeTutorial()
        {
            if (tutorialExplanationsSkipped)
            {
                CurrentTutorialStep = TutorialStep.Complete;
                SetPhase("TUO TURNO · SCEGLI UN'AZIONE");
                combatMessage.text = PlayerTurnMessage;
                UpdateStatusAndCooldown();
                RefreshActionButtons();
                yield break;
            }

            yield return WaitForTutorialCard(
                TutorialStep.EnemyLearning,
                "PASSO 14 / 18",
                "Dal terzo scontro i nemici useranno le tue azioni passate per riconoscere alcune abitudini.");
            yield return WaitForTutorialCard(
                TutorialStep.VictoryGoal,
                "PASSO 16 / 18",
                "Porta gli HP del nemico a zero per renderlo incapacitato.");

            if (tutorialExplanationsSkipped)
            {
                CurrentTutorialStep = TutorialStep.Complete;
                SetPhase("TUO TURNO · SCEGLI UN'AZIONE");
                combatMessage.text = PlayerTurnMessage;
                UpdateStatusAndCooldown();
                RefreshActionButtons();
                yield break;
            }

            CurrentTutorialStep = TutorialStep.Complete;
            SetPhase("TUO TURNO · SCEGLI UN'AZIONE");
            combatMessage.text = PlayerTurnMessage;
            UpdateStatusAndCooldown();
            RefreshActionButtons();
        }

        private IEnumerator WaitForTutorialCard(TutorialStep step, string stepLabel, string body)
        {
            if (tutorialExplanationsSkipped)
            {
                yield break;
            }

            CurrentTutorialStep = step;
            waitingForTutorialAdvance = true;
            tutorialAdvanceRequested = false;
            ShowBlockingTutorial(stepLabel, body);

            yield return new WaitUntil(
                () => tutorialAdvanceRequested || tutorialExplanationsSkipped);

            waitingForTutorialAdvance = false;
            tutorialAdvanceRequested = false;
            tutorialNextButton.interactable = true;
            tutorialOverlay.SetActive(false);
            if (tutorialExplanationsSkipped)
            {
                CurrentTutorialStep = TutorialStep.Complete;
            }
        }

        private void ShowBlockingTutorial(string stepLabel, string body)
        {
            lastTutorialStepLabel = stepLabel;
            lastTutorialBody = body;
            lastTutorialCardWasBlocking = true;
            tutorialStepText.text = stepLabel;
            tutorialBodyText.text = body;
            tutorialInputBlocker.raycastTarget = true;
            tutorialNextButton.gameObject.SetActive(true);
            tutorialNextButton.interactable = true;
            tutorialOverlay.SetActive(true);
            SetAllHighlights(false);
            SetAllActionButtons(false);
        }

        private void ShowActionPrompt(
            TutorialStep step,
            string stepLabel,
            string body,
            BattleAction requiredAction)
        {
            CurrentTutorialStep = step;
            lastTutorialStepLabel = stepLabel;
            lastTutorialBody = body;
            lastTutorialCardWasBlocking = false;
            tutorialStepText.text = stepLabel;
            tutorialBodyText.text = body;
            tutorialInputBlocker.raycastTarget = false;
            tutorialNextButton.gameObject.SetActive(false);
            tutorialOverlay.SetActive(true);
            SetAllActionButtons(false);
            SetRequiredActionEnabled(requiredAction);
            SetRequiredHighlight(requiredAction);
            SetPhase("TUO TURNO · " + GetActionDisplayName(requiredAction));
            combatMessage.text = "Premi " + GetActionDisplayName(requiredAction);
        }

        private void ShowMovePrompt()
        {
            CurrentTutorialStep = TutorialStep.AwaitingMove;
            lastTutorialStepLabel = "PASSO 4 / 18";
            lastTutorialBody =
                "Le pedane definiscono distanza e portata. Premi MUOVI, poi scegli una delle otto pedane verdi adiacenti.";
            lastTutorialCardWasBlocking = false;
            tutorialStepText.text = lastTutorialStepLabel;
            tutorialBodyText.text = lastTutorialBody;
            tutorialInputBlocker.raycastTarget = false;
            tutorialNextButton.gameObject.SetActive(false);
            tutorialOverlay.SetActive(true);
            SetAllActionButtons(false);
            SetAllHighlights(false);
            SetPhase("TUO TURNO · MUOVI");
            combatMessage.text = "Premi MUOVI · poi scegli una pedana verde";
        }

        private void FinishWithActionPrompt(
            TutorialStep step,
            string stepLabel,
            string body,
            BattleAction requiredAction)
        {
            actionRunning = false;
            ShowActionPrompt(step, stepLabel, body, requiredAction);
        }

        private void EnterFinalChoice()
        {
            actionRunning = false;
            analyzePanelOpen = false;
            waitingForTutorialAdvance = false;
            tutorialOverlay.SetActive(false);
            analyzePanel.SetActive(false);
            SetAllHighlights(false);
            SetAllActionButtons(false);
            ResetTransientEffects();
            intentText.text = "INTENZIONE\nNESSUNA · INCAPACITATO";
            combatMessage.text = "Il nemico non può più combattere";
            CurrentTutorialStep = TutorialStep.EnemyIncapacitated;
            SetPhase("DECIDI IL SUO DESTINO");

            EncounterResolution previousResolution;
            bool hasPrevious = CampaignProgressStore.TryGetEnemyResolution(
                CampaignContentIds.Level01Tutorial,
                CampaignContentIds.TutorialEnemy,
                out previousResolution);

            finalChoiceTitleText.text = "PASSO 17 / 18 · NEMICO INCAPACITATO";
            if (finalChoicePortrait != null)
            {
                finalChoicePortrait.sprite = enemyVisual.sprite;
                finalChoicePortrait.preserveAspect = true;
                finalChoicePortrait.color = Color.white;
            }
            finalChoiceProfileText.text =
                enemyDisplayName.ToUpperInvariant() + "\n" +
                "RAZZA · " + enemyRace + "\n" +
                "CORRUZIONE · " + ClampCorruptionPercent(enemyCorruptionPercent) + "%\n" +
                "STATO · " + GetMoodDisplayName(enemyMood);
            string choiceContext = hasPrevious
                ? "Esito registrato: " + GetResolutionDisplayName(previousResolution) +
                  ". Puoi mantenerlo o cambiare la storia."
                : "Un nemico sconfitto non è ancora morto. Ora devi decidere il suo destino.";
            finalChoiceDialogueText.text = choiceContext + MoralConsequencesText;
            saveButton.interactable = true;
            killButton.interactable = true;
            confirmationPanel.SetActive(false);
            confirmationOpen = false;
            pendingSaveChoice = null;
            finalChoicePanel.SetActive(true);
            finalChoiceOpen = true;
            RefreshRecordedMoralChoiceVisual();
        }

        private void OpenFinalConfirmation(bool save)
        {
            if (!finalChoiceOpen || confirmationOpen || battleState == null ||
                battleState.Outcome != BattleOutcome.Victory)
            {
                return;
            }

            pendingSaveChoice = save;
            finalChoicePanel.SetActive(false);
            finalChoiceOpen = false;
            saveButton.interactable = false;
            killButton.interactable = false;
            RestoreMoralChoiceButtonVisuals();

            EncounterResolution previousResolution;
            bool hasPrevious = CampaignProgressStore.TryGetEnemyResolution(
                CampaignContentIds.Level01Tutorial,
                CampaignContentIds.TutorialEnemy,
                out previousResolution);
            EncounterResolution nextResolution = save
                ? EncounterResolution.Saved
                : EncounterResolution.Killed;
            string changeWarning = hasPrevious && previousResolution != nextResolution
                ? "\n\nQUESTA DECISIONE MODIFICHERÀ LA STORIA SALVATA."
                : string.Empty;
            confirmationText.text = "PASSO 18 / 18\n" +
                                    (save
                                        ? "Salvare " + enemyDisplayName + "?"
                                        : "Uccidere " + enemyDisplayName + "?") +
                                    changeWarning;
            confirmationConfirmButton.interactable = true;
            confirmationBackButton.interactable = true;
            confirmationPanel.SetActive(true);
            confirmationOpen = true;
            CurrentTutorialStep = TutorialStep.FinalChoice;
            SetPhase("CONFERMA LA DECISIONE");
        }

        private void ShowOutcome(
            EncounterResolution resolution = EncounterResolution.None,
            bool replayChoiceChanged = false)
        {
            actionRunning = false;
            analyzePanelOpen = false;
            finalChoiceOpen = false;
            confirmationOpen = false;
            pendingSaveChoice = null;
            waitingForTutorialAdvance = false;
            CurrentTutorialStep = TutorialStep.Complete;
            tutorialOverlay.SetActive(false);
            analyzePanel.SetActive(false);
            if (finalChoicePanel != null) finalChoicePanel.SetActive(false);
            if (confirmationPanel != null) confirmationPanel.SetActive(false);
            RestoreMoralChoiceButtonVisuals();
            SetAllHighlights(false);
            SetAllActionButtons(false);
            ResetTransientEffects();

            bool victory = battleState.Outcome == BattleOutcome.Victory;
            string outcomeLabel = resolution == EncounterResolution.Saved
                ? "NEMICO SALVATO"
                : resolution == EncounterResolution.Killed
                    ? "NEMICO UCCISO"
                    : victory ? "VITTORIA" : "SCONFITTA";
            outcomeText.text = outcomeLabel;
            outcomeText.color = victory
                ? new Color(0.35f, 0.84f, 0.82f, 1f)
                : new Color(0.91f, 0.36f, 0.40f, 1f);
            combatMessage.text = victory
                ? resolution == EncounterResolution.Saved
                    ? "La creatura corrotta è stata salvata"
                    : "La creatura corrotta è stata uccisa"
                : "Hero01 non può più combattere";
            intentText.text = "COMBATTIMENTO\nCONCLUSO";
            SetPhase(victory ? "VITTORIA" : "SCONFITTA");
            outcomeMenuButton.interactable = true;
            if (outcomeContinueButton != null)
            {
                outcomeContinueButton.gameObject.SetActive(victory);
                outcomeContinueButton.interactable = victory;
            }
            if (outcomeRetryButton != null)
            {
                outcomeRetryButton.gameObject.SetActive(!victory);
                outcomeRetryButton.interactable = !victory;
            }

            if (outcomeProgressText != null)
            {
                int rewardExperience = CampaignLevelCatalog.GetByNumber(1).ExperienceReward;
                outcomeProgressText.text = victory
                    ? (rewardGrantedThisBattle
                        ? "+" + rewardExperience + " XP  -  LIVELLO 2 SBLOCCATO"
                        : replayChoiceChanged
                            ? "RICOMPENSE GIÀ OTTENUTE  -  STORIA AGGIORNATA"
                            : "RIVINCITA COMPLETATA  -  SCELTA CONFERMATA")
                    : "Nessun XP ottenuto";
            }
            outcomeOverlay.SetActive(true);
        }

        private IEnumerator ReturnToMenuAfterDelay()
        {
            yield return new WaitForSecondsRealtime(resultReturnDelay);
            ReturnToMenu();
        }

        private void RefreshActionButtons()
        {
            if (actionRunning || analyzePanelOpen || battleState == null || battleState.IsFinished)
            {
                SetAllActionButtons(false);
                return;
            }

            switch (CurrentTutorialStep)
            {
                case TutorialStep.AwaitingFirstAttack:
                    SetAllActionButtons(false);
                    attackButton.interactable = battleState.CanUsePlayerAction(BattleAction.Attack);
                    return;
                case TutorialStep.AwaitingGuard:
                    SetAllActionButtons(false);
                    guardButton.interactable = battleState.CanUsePlayerAction(BattleAction.Guard);
                    return;
                case TutorialStep.AwaitingTechnique:
                    SetAllActionButtons(false);
                    techniqueButton.interactable = battleState.CanUsePlayerAction(BattleAction.Technique);
                    return;
                case TutorialStep.AwaitingAnalyze:
                    SetAllActionButtons(false);
                    analyzeButton.interactable = !analyzeUsedThisTurn &&
                                                 battleState.CanUsePlayerAction(BattleAction.Analyze);
                    return;
            }

            if (!IsTutorialComplete)
            {
                SetAllActionButtons(false);
                return;
            }

            attackButton.interactable = battleState.CanUsePlayerAction(BattleAction.Attack);
            guardButton.interactable = battleState.CanUsePlayerAction(BattleAction.Guard);
            techniqueButton.interactable = battleState.CanUsePlayerAction(BattleAction.Technique);
            analyzeButton.interactable = !analyzeUsedThisTurn &&
                                         battleState.CanUsePlayerAction(BattleAction.Analyze);
        }

        private void SetAllActionButtons(bool enabled)
        {
            attackButton.interactable = enabled;
            guardButton.interactable = enabled;
            techniqueButton.interactable = enabled;
            analyzeButton.interactable = enabled;
        }

        private void SetRequiredActionEnabled(BattleAction action)
        {
            switch (action)
            {
                case BattleAction.Attack:
                    attackButton.interactable = true;
                    break;
                case BattleAction.Guard:
                    guardButton.interactable = true;
                    break;
                case BattleAction.Technique:
                    techniqueButton.interactable = true;
                    break;
                case BattleAction.Analyze:
                    analyzeButton.interactable = true;
                    break;
            }
        }

        private void SetRequiredHighlight(BattleAction action)
        {
            SetAllHighlights(false);
            switch (action)
            {
                case BattleAction.Attack:
                    attackHighlight.SetActive(true);
                    break;
                case BattleAction.Guard:
                    guardHighlight.SetActive(true);
                    break;
                case BattleAction.Technique:
                    techniqueHighlight.SetActive(true);
                    break;
                case BattleAction.Analyze:
                    analyzeHighlight.SetActive(true);
                    break;
            }
        }

        private void SetAllHighlights(bool active)
        {
            attackHighlight.SetActive(active);
            guardHighlight.SetActive(active);
            techniqueHighlight.SetActive(active);
            analyzeHighlight.SetActive(active);
        }

        private void UpdateStatusAndCooldown()
        {
            if (battleState == null)
            {
                return;
            }

            if (battleState.TechniqueCooldownRemaining > 0)
            {
                string turnLabel = battleState.TechniqueCooldownRemaining == 1 ? " TURNO" : " TURNI";
                techniqueButtonLabel.text =
                    "TECNICA · DANNO " + techniqueDamage + " · PORTATA 2\nRICARICA " +
                    battleState.TechniqueCooldownRemaining + turnLabel;
            }
            else
            {
                techniqueButtonLabel.text =
                    "TECNICA · DANNO " + techniqueDamage + " · PORTATA 2\nPRONTA";
            }

            statusText.text = battleState.IsGuardPrepared
                ? "GUARDIA\nPROSSIMO COLPO PARATO"
                : "STATO\nPRONTO";
        }

        private void UpdateHealthImmediate()
        {
            heroHealthFill.fillAmount = battleState.HeroHp / (float)battleState.HeroMaxHp;
            enemyHealthFill.fillAmount = battleState.EnemyHp / (float)battleState.EnemyMaxHp;
            heroHealthValue.text = battleState.HeroHp + " / " + battleState.HeroMaxHp;
            enemyHealthValue.text = battleState.EnemyHp + " / " + battleState.EnemyMaxHp;
        }

        private void PopulateAnalyzePanel()
        {
            analyzeNameText.text = "NOME\n" + enemyDisplayName;
            analyzeRaceText.text = "RAZZA\n" + enemyRace;
            analyzeCorruptionText.text =
                "CORRUZIONE\n" + ClampCorruptionPercent(enemyCorruptionPercent) + "%";
            analyzeMoodText.text = "STATO ATTUALE\n" + GetMoodDisplayName(enemyMood) +
                                   (battleState != null && battleState.IsEnemyExposed
                                       ? "\nESPOSTO · PROSSIMO DANNO +25%"
                                       : string.Empty);
        }

        private void ShowPlayerTurnMessage(bool repeatedPattern)
        {
            SetPhase("TUO TURNO · SCEGLI UN'AZIONE");
            if (repeatedPattern && !repeatedPatternMessageShown)
            {
                repeatedPatternMessageShown = true;
                combatMessage.text = "Il nemico ti sta osservando";
            }
            else
            {
                combatMessage.text = PlayerTurnMessage;
            }
        }

        private bool IsActionAllowedByTutorial(BattleAction action, TutorialStep step)
        {
            if (step == TutorialStep.Complete)
            {
                return action != BattleAction.Analyze;
            }

            return (step == TutorialStep.AwaitingFirstAttack && action == BattleAction.Attack) ||
                   (step == TutorialStep.AwaitingGuard && action == BattleAction.Guard) ||
                   (step == TutorialStep.AwaitingTechnique && action == BattleAction.Technique);
        }

        private int GetObservationLengthForIntelligence()
        {
            // Il tutorial (livello 0) necessita di tre azioni uguali prima di riconoscere
            // un'abitudine. I livelli futuri possono osservare la stessa cronologia più
            // rapidamente, senza conoscere l'azione corrente del giocatore.
            return enemyIntelligenceLevel == 0 ? 3 : 2;
        }

        private static string GetActionDisplayName(BattleAction action)
        {
            switch (action)
            {
                case BattleAction.Attack:
                    return "ATTACCO";
                case BattleAction.Guard:
                    return "GUARDIA";
                case BattleAction.Technique:
                    return "TECNICA";
                case BattleAction.Analyze:
                    return "ANALIZZA";
                default:
                    return string.Empty;
            }
        }

        private void SetPhase(string value)
        {
            if (phaseText != null)
            {
                phaseText.text = value;
            }
        }

        private static string GetResolutionDisplayName(EncounterResolution resolution)
        {
            switch (resolution)
            {
                case EncounterResolution.Saved:
                    return "SALVATO";
                case EncounterResolution.Killed:
                    return "UCCISO";
                default:
                    return "NON DECISO";
            }
        }

        public static int ClampCorruptionPercent(int value)
        {
            return Mathf.Clamp(value, 0, 100);
        }

        private static string GetMoodDisplayName(EnemyMood mood)
        {
            switch (mood)
            {
                case EnemyMood.Felice:
                    return "Felice";
                case EnemyMood.Triste:
                    return "Triste";
                case EnemyMood.Arrabbiato:
                    return "Arrabbiato";
                default:
                    return "Sconosciuto";
            }
        }

        private static IEnumerator AnimateHealth(
            Image fill,
            TMP_Text valueText,
            int from,
            int to,
            int maximum,
            float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                int displayed = Mathf.RoundToInt(Mathf.Lerp(from, to, progress));
                fill.fillAmount = displayed / (float)maximum;
                valueText.text = displayed + " / " + maximum;
                yield return null;
            }

            fill.fillAmount = to / (float)maximum;
            valueText.text = to + " / " + maximum;
        }

        private static IEnumerator MoveActor(Transform actor, Vector3 start, Vector3 end, float duration)
        {
            actor.localPosition = start;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                actor.localPosition = Vector3.Lerp(start, end, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            actor.localPosition = end;
        }

        private static IEnumerator MoveEffect(GameObject effect, Vector3 start, Vector3 end, float duration)
        {
            effect.transform.position = start;
            effect.SetActive(true);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                effect.transform.position = Vector3.Lerp(start, end, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            effect.transform.position = end;
            yield return new WaitForSecondsRealtime(0.05f);
            effect.SetActive(false);
        }

        private static IEnumerator Flash(SpriteRenderer target, Color flashColor, float duration)
        {
            Color original = target.color;
            target.color = flashColor;
            yield return new WaitForSecondsRealtime(duration);
            target.color = original;
        }

        private static IEnumerator PulseScale(
            GameObject effect,
            Vector3 baseScale,
            float multiplier,
            float duration)
        {
            effect.SetActive(true);
            float halfDuration = duration * 0.5f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(elapsed / halfDuration);
                float returnNormalized = Mathf.Clamp01((elapsed - halfDuration) / halfDuration);
                float factor = elapsed <= halfDuration
                    ? Mathf.Lerp(1f, multiplier, normalized)
                    : Mathf.Lerp(multiplier, 1f, returnNormalized);
                effect.transform.localScale = baseScale * factor;
                yield return null;
            }

            effect.transform.localScale = baseScale;
        }

        private void ResetPersistentEffects()
        {
            heroActor.localPosition = heroBasePosition;
            enemyActor.localPosition = enemyBasePosition;
            heroVisual.color = heroBaseColor;
            enemyVisual.color = enemyBaseColor;
            heroBasicProjectile.transform.localScale = basicProjectileBaseScale;
            heroTechniqueProjectile.transform.localScale = techniqueProjectileBaseScale;
            enemyProjectile.transform.localScale = enemyProjectileBaseScale;
            guardVisual.transform.localScale = guardBaseScale;
            ResetTransientEffects();
        }

        private void RefreshRecordedMoralChoiceVisual()
        {
            RestoreMoralChoiceButtonVisuals();

            if (!isReplayBattle ||
                !CampaignProgressStore.TryGetEnemyResolution(
                    CampaignContentIds.Level01Tutorial,
                    CampaignContentIds.TutorialEnemy,
                    out EncounterResolution recordedResolution))
            {
                return;
            }

            if (recordedResolution == EncounterResolution.Saved)
            {
                ApplyRecordedChoiceStyle(
                    saveButton,
                    saveButtonNeutralColors,
                    saveButtonNeutralScale,
                    RecordedSaveColor);
            }
            else if (recordedResolution == EncounterResolution.Killed)
            {
                ApplyRecordedChoiceStyle(
                    killButton,
                    killButtonNeutralColors,
                    killButtonNeutralScale,
                    RecordedKillColor);
            }
        }

        private void RestoreMoralChoiceButtonVisuals()
        {
            saveButton.colors = saveButtonNeutralColors;
            killButton.colors = killButtonNeutralColors;
            saveButton.transform.localScale = saveButtonNeutralScale;
            killButton.transform.localScale = killButtonNeutralScale;
        }

        private static void ApplyRecordedChoiceStyle(
            Button button,
            ColorBlock neutralColors,
            Vector3 neutralScale,
            Color recordedColor)
        {
            ColorBlock colors = neutralColors;
            colors.normalColor = recordedColor;
            colors.highlightedColor = Color.Lerp(recordedColor, Color.white, 0.18f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = Color.Lerp(recordedColor, Color.black, 0.12f);
            button.colors = colors;
            button.transform.localScale = neutralScale * RecordedChoiceScale;
        }

        private void ResetTransientEffects()
        {
            heroBasicProjectile.transform.position = heroProjectileOrigin.position;
            heroTechniqueProjectile.transform.position = heroProjectileOrigin.position;
            enemyProjectile.transform.position = enemyProjectileOrigin.position;
            heroBasicProjectile.SetActive(false);
            heroTechniqueProjectile.SetActive(false);
            enemyProjectile.SetActive(false);
            guardVisual.SetActive(battleState != null && battleState.IsGuardPrepared);
        }
    }
}
