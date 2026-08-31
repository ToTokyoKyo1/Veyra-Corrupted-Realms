using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Veyra.Core;

namespace Veyra.Combat.Tutorial
{
    public enum TutorialStep
    {
        Welcome,
        Positions,
        Health,
        AwaitingFirstAttack,
        EnemyCounterattack,
        AwaitingGuard,
        AwaitingTechnique,
        AwaitingAnalyze,
        EnemyLearning,
        VictoryGoal,
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
        [SerializeField] private TMP_Text intentText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Image heroHealthFill;
        [SerializeField] private Image enemyHealthFill;
        [SerializeField] private TMP_Text heroHealthValue;
        [SerializeField] private TMP_Text enemyHealthValue;

        [Header("Characters")]
        [SerializeField] private Transform heroActor;
        [SerializeField] private Transform enemyActor;
        [SerializeField] private SpriteRenderer heroVisual;
        [SerializeField] private SpriteRenderer enemyVisual;
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

        [Header("Analyze panel")]
        [SerializeField] private GameObject analyzePanel;
        [SerializeField] private TMP_Text analyzeNameText;
        [SerializeField] private TMP_Text analyzeRaceText;
        [SerializeField] private TMP_Text analyzeCorruptionText;
        [SerializeField] private TMP_Text analyzeMoodText;
        [SerializeField] private Button analyzeCloseButton;

        [Header("Outcome overlay")]
        [SerializeField] private GameObject outcomeOverlay;
        [SerializeField] private TMP_Text outcomeText;
        [SerializeField] private Button outcomeMenuButton;
        [SerializeField] private TutorialBattleNavigation navigation;

        private const string PlayerTurnMessage = "Scegli la tua azione";

        private TutorialBattleState battleState;
        private bool actionRunning;
        private bool analyzePanelOpen;
        private bool waitingForTutorialAdvance;
        private bool tutorialAdvanceRequested;
        private bool repeatedPatternMessageShown;
        private Vector3 heroBasePosition;
        private Vector3 enemyBasePosition;
        private Color heroBaseColor;
        private Color enemyBaseColor;
        private Vector3 basicProjectileBaseScale;
        private Vector3 techniqueProjectileBaseScale;
        private Vector3 enemyProjectileBaseScale;
        private Vector3 guardBaseScale;

        public int HeroCurrentHp => battleState?.HeroHp ?? heroMaxHp;
        public int EnemyCurrentHp => battleState?.EnemyHp ?? enemyMaxHp;
        public BattleOutcome Outcome => battleState?.Outcome ?? BattleOutcome.Ongoing;
        public TutorialStep CurrentTutorialStep { get; private set; }
        public bool IsActionRunning => actionRunning;
        public bool IsAnalyzePanelOpen => analyzePanelOpen;
        public bool IsTutorialComplete => CurrentTutorialStep == TutorialStep.Complete;
        public int EnemyIntelligenceLevel => enemyIntelligenceLevel;
        public int EnemyCorruptionPercent => ClampCorruptionPercent(enemyCorruptionPercent);

        private void Awake()
        {
            CapturePersistentVisualState();
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
                        "PASSO 2 / 10",
                        "Tu controlli l'eroe a sinistra. Il tuo avversario è la creatura corrotta a destra.");
                    break;
                case TutorialStep.Positions:
                    CurrentTutorialStep = TutorialStep.Health;
                    ShowBlockingTutorial(
                        "PASSO 3 / 10",
                        "Le barre mostrano i punti vita, o HP. Se i tuoi HP raggiungono zero, perdi.");
                    break;
                case TutorialStep.Health:
                    ShowActionPrompt(
                        TutorialStep.AwaitingFirstAttack,
                        "PASSO 4 / 10",
                        "Premi ATTACCO per colpire il nemico. Ogni attacco riduce gli HP del bersaglio.",
                        BattleAction.Attack);
                    break;
            }
        }

        public void PreviewAttack()
        {
            BeginPlayerAction(BattleAction.Attack);
        }

        public void PreviewGuard()
        {
            BeginPlayerAction(BattleAction.Guard);
        }

        public void PreviewTechnique()
        {
            BeginPlayerAction(BattleAction.Technique);
        }

        public void PreviewAnalyze()
        {
            if (actionRunning || analyzePanelOpen || battleState == null || battleState.IsFinished)
            {
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

            tutorialOverlay.SetActive(false);
            SetAllHighlights(false);
            SetAllActionButtons(false);
            PopulateAnalyzePanel();
            analyzeCloseButton.interactable = true;
            analyzePanelOpen = true;
            analyzePanel.SetActive(true);
            combatMessage.text = "Informazioni sul nemico";
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

            if (completesGuidedAnalyze)
            {
                StartCoroutine(CompleteAnalyzeTutorial());
                return;
            }

            combatMessage.text = "Analisi completata: nessun turno consumato";
            RefreshActionButtons();
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
            waitingForTutorialAdvance = false;
            tutorialAdvanceRequested = false;
            analyzePanel.SetActive(false);
            ResetPersistentEffects();
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
        }

        private void InitializeBattle()
        {
            battleState = new TutorialBattleState(
                heroMaxHp,
                enemyMaxHp,
                attackDamage,
                techniqueDamage,
                enemyAttackDamage,
                techniqueCooldownTurns,
                historyCapacity: 8,
                repeatedPatternLength: GetObservationLengthForIntelligence());

            actionRunning = false;
            analyzePanelOpen = false;
            waitingForTutorialAdvance = false;
            tutorialAdvanceRequested = false;
            repeatedPatternMessageShown = false;
            CurrentTutorialStep = TutorialStep.Welcome;

            ResetPersistentEffects();
            PopulateAnalyzePanel();
            analyzePanel.SetActive(false);
            analyzeCloseButton.interactable = true;
            UpdateHealthImmediate();
            UpdateStatusAndCooldown();
            intentText.text = "INTENZIONE\nATTACCO IN ARRIVO";
            combatMessage.text = "Impara le basi del combattimento";
            outcomeOverlay.SetActive(false);
            outcomeMenuButton.interactable = true;
            SetAllHighlights(false);
            ShowBlockingTutorial("PASSO 1 / 10", "Benvenuto nel combattimento.");
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
                    yield return MoveActor(
                        heroActor,
                        heroBasePosition,
                        heroBasePosition + Vector3.right * 0.72f,
                        0.16f);
                    yield return MoveEffect(
                        heroBasicProjectile,
                        heroProjectileOrigin.position,
                        enemyHitTarget.position,
                        0.24f);
                    playerResult = battleState.ResolvePlayerAction(action);
                    yield return Flash(enemyVisual, Color.white, 0.16f);
                    yield return AnimateHealth(
                        enemyHealthFill,
                        enemyHealthValue,
                        enemyHpBefore,
                        battleState.EnemyHp,
                        battleState.EnemyMaxHp,
                        0.20f);
                    yield return MoveActor(heroActor, heroActor.localPosition, heroBasePosition, 0.16f);
                    break;
                case BattleAction.Technique:
                    combatMessage.text = "Hero01 usa Tecnica";
                    yield return MoveActor(
                        heroActor,
                        heroBasePosition,
                        heroBasePosition + Vector3.right * 0.58f,
                        0.16f);
                    heroTechniqueProjectile.transform.localScale = techniqueProjectileBaseScale * 1.25f;
                    yield return MoveEffect(
                        heroTechniqueProjectile,
                        heroProjectileOrigin.position,
                        enemyHitTarget.position,
                        0.38f);
                    playerResult = battleState.ResolvePlayerAction(action);
                    yield return Flash(enemyVisual, new Color(0.73f, 1f, 0.94f, 1f), 0.24f);
                    yield return AnimateHealth(
                        enemyHealthFill,
                        enemyHealthValue,
                        enemyHpBefore,
                        battleState.EnemyHp,
                        battleState.EnemyMaxHp,
                        0.22f);
                    yield return MoveActor(heroActor, heroActor.localPosition, heroBasePosition, 0.16f);
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

            UpdateStatusAndCooldown();

            if (battleState.IsFinished)
            {
                ShowOutcome();
                yield break;
            }

            bool repeatedPattern = battleState.TryGetRepeatedPlayerAction(out _);

            if (startingStep == TutorialStep.AwaitingFirstAttack)
            {
                yield return WaitForTutorialCard(
                    TutorialStep.EnemyCounterattack,
                    "PASSO 5 / 10",
                    "Ora il nemico contrattacca. Anche i suoi attacchi riducono i tuoi HP.");
            }

            yield return ResolveEnemyTurn();
            if (battleState.IsFinished)
            {
                ShowOutcome();
                yield break;
            }

            ResetTransientEffects();
            UpdateStatusAndCooldown();

            if (startingStep == TutorialStep.AwaitingFirstAttack)
            {
                FinishWithActionPrompt(
                    TutorialStep.AwaitingGuard,
                    "PASSO 6 / 10",
                    "Il nemico sta per attaccare di nuovo. Premi GUARDIA per parare il prossimo colpo.",
                    BattleAction.Guard);
                yield break;
            }

            if (startingStep == TutorialStep.AwaitingGuard)
            {
                FinishWithActionPrompt(
                    TutorialStep.AwaitingTechnique,
                    "PASSO 7 / 10",
                    "TECNICA è la tua mossa speciale. Infligge più danni, ma deve ricaricarsi dopo l'uso.",
                    BattleAction.Technique);
                yield break;
            }

            if (startingStep == TutorialStep.AwaitingTechnique)
            {
                FinishWithActionPrompt(
                    TutorialStep.AwaitingAnalyze,
                    "PASSO 8 / 10",
                    "Usa ANALIZZA per conoscere meglio il nemico. Puoi scoprire la sua razza, la corruzione e il suo stato emotivo.",
                    BattleAction.Analyze);
                yield break;
            }

            actionRunning = false;
            ShowPlayerTurnMessage(repeatedPattern);
            RefreshActionButtons();
        }

        private IEnumerator ResolveEnemyTurn()
        {
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

        private IEnumerator CompleteAnalyzeTutorial()
        {
            yield return WaitForTutorialCard(
                TutorialStep.EnemyLearning,
                "PASSO 9 / 10",
                "I nemici più evoluti osserveranno le azioni che hai già completato e impareranno come combatti.");
            yield return WaitForTutorialCard(
                TutorialStep.VictoryGoal,
                "PASSO 10 / 10",
                "Porta gli HP del nemico a zero per vincere.");

            CurrentTutorialStep = TutorialStep.Complete;
            combatMessage.text = PlayerTurnMessage;
            UpdateStatusAndCooldown();
            RefreshActionButtons();
        }

        private IEnumerator WaitForTutorialCard(TutorialStep step, string stepLabel, string body)
        {
            CurrentTutorialStep = step;
            waitingForTutorialAdvance = true;
            tutorialAdvanceRequested = false;
            ShowBlockingTutorial(stepLabel, body);

            yield return new WaitUntil(() => tutorialAdvanceRequested);

            waitingForTutorialAdvance = false;
            tutorialAdvanceRequested = false;
            tutorialNextButton.interactable = true;
            tutorialOverlay.SetActive(false);
        }

        private void ShowBlockingTutorial(string stepLabel, string body)
        {
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
            tutorialStepText.text = stepLabel;
            tutorialBodyText.text = body;
            tutorialInputBlocker.raycastTarget = false;
            tutorialNextButton.gameObject.SetActive(false);
            tutorialOverlay.SetActive(true);
            SetAllActionButtons(false);
            SetRequiredActionEnabled(requiredAction);
            SetRequiredHighlight(requiredAction);
            combatMessage.text = "Premi " + GetActionDisplayName(requiredAction);
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

        private void ShowOutcome()
        {
            actionRunning = false;
            analyzePanelOpen = false;
            waitingForTutorialAdvance = false;
            tutorialOverlay.SetActive(false);
            analyzePanel.SetActive(false);
            SetAllHighlights(false);
            SetAllActionButtons(false);
            ResetTransientEffects();

            bool victory = battleState.Outcome == BattleOutcome.Victory;
            if (victory)
            {
                CampaignProgressStore.MarkTutorialCompleted();
            }

            outcomeText.text = victory ? "VITTORIA" : "SCONFITTA";
            outcomeText.color = victory
                ? new Color(0.35f, 0.84f, 0.82f, 1f)
                : new Color(0.91f, 0.36f, 0.40f, 1f);
            combatMessage.text = victory
                ? "La creatura corrotta è stata sconfitta"
                : "Hero01 non può più combattere";
            intentText.text = "COMBATTIMENTO\nCONCLUSO";
            outcomeMenuButton.interactable = true;
            outcomeOverlay.SetActive(true);
            StartCoroutine(ReturnToMenuAfterDelay());
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
                    analyzeButton.interactable = battleState.CanUsePlayerAction(BattleAction.Analyze);
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
            analyzeButton.interactable = battleState.CanUsePlayerAction(BattleAction.Analyze);
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
                    "TECNICA\n" + battleState.TechniqueCooldownRemaining + turnLabel;
            }
            else
            {
                techniqueButtonLabel.text = "TECNICA";
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
            analyzeMoodText.text = "STATO ATTUALE\n" + GetMoodDisplayName(enemyMood);
        }

        private void ShowPlayerTurnMessage(bool repeatedPattern)
        {
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
