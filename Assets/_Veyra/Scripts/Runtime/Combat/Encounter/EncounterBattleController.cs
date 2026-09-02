using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Veyra.Combat.Support;
using Veyra.Core;
using Veyra.Combat.Tactical;
using Veyra.Progression;
using Veyra.UI.Battle;

namespace Veyra.Combat.Encounter
{
    public sealed class EncounterBattleController : MonoBehaviour
    {
        [Header("Encounter profile")]
        [SerializeField] private CampaignEncounter campaignEncounter = CampaignEncounter.ThornGuardian;
        [SerializeField] private string encounterId = "W01_L02";
        [SerializeField] private string enemyDisplayName = "Custode del Rovo";
        [SerializeField] private string enemyRace = "Custode Silvano";
        [SerializeField, Range(0, 100)] private int enemyCorruptionPercent = 58;
        [SerializeField] private EnemyMood enemyInitialMood = EnemyMood.Triste;
        [SerializeField, Range(0, 3)] private int enemyIntelligenceLevel = 1;
        [SerializeField] private int enemyRandomSeed = 1202;

        [Header("Adaptive learning")]
        [SerializeField] private AdaptiveEnemyTuning adaptiveLearningTuning =
            new AdaptiveEnemyTuning();

        [Header("Combat tuning")]
        [SerializeField, Min(1)] private int heroMaxHp = 100;
        [SerializeField, Min(1)] private int enemyMaxHp = 115;
        [SerializeField, Min(1)] private int attackDamage = 20;
        [SerializeField, Min(1)] private int techniqueDamage = 32;
        [SerializeField, Min(1)] private int enemyAttackDamage = 16;
        [SerializeField, Min(1)] private int chargedStrikeDamage = 32;
        [SerializeField, Min(1)] private int techniqueCooldownTurns = 2;
        [SerializeField, Range(1, 99)] private int enemyGuardReductionPercent = 60;
        [SerializeField, Min(0.1f)] private float resultReturnDelay = 3.5f;

        [Header("Reactive dialogue")]
        [SerializeField, TextArea] private string openingDialogue =
            "Fermati... non sono io a muovere queste radici.";
        [SerializeField, TextArea] private string attackReactionDialogue =
            "Hai deciso di colpirmi... forse è l'unico modo che conosci.";
        [SerializeField, TextArea] private string guardReactionDialogue =
            "Ti stai proteggendo. Forse non vuoi davvero uccidermi.";
        [SerializeField, TextArea] private string techniqueReactionDialogue =
            "Sento quella forza. Può liberarmi oppure distruggermi.";
        [SerializeField, TextArea] private string firstAnalyzeDialogue =
            "Non guardarmi così... so già cosa mi sta succedendo.";
        [SerializeField, TextArea] private string repeatedAnalyzeDialogue =
            "Puoi continuare a studiarmi. La corruzione non se ne andrà da sola.";
        [SerializeField, TextArea] private string lowHpDialogue =
            "Non riesco a fermarmi... ma tu puoi ancora scegliere.";
        [SerializeField, TextArea] private string attackPatternDialogue =
            "Attacchi sempre nello stesso modo.";
        [SerializeField, TextArea] private string guardPatternDialogue =
            "Ti chiudi dietro la difesa. Allora aspetterò.";
        [SerializeField, TextArea] private string techniquePatternDialogue =
            "Conosco il ritmo della tua forza.";
        [SerializeField, TextArea] private string strategyChangedDialogue =
            "Hai cambiato cadenza... interessante.";
        [SerializeField, TextArea] private string defeatedDialogue =
            "Non riesco più a combattere. Adesso la scelta è tua.";
        [SerializeField, TextArea] private string savedDialogue =
            "Il dolore... si sta ritirando. Questa scelta era tua, non mia.";
        [SerializeField, TextArea] private string killedDialogue =
            "Almeno... il rumore finalmente finirà.";

        [Header("Action controls")]
        [SerializeField] private Button attackButton;
        [SerializeField] private Button guardButton;
        [SerializeField] private Button techniqueButton;
        [SerializeField] private Button analyzeButton;
        [SerializeField] private TMP_Text techniqueButtonLabel;

        [Header("HUD")]
        [SerializeField] private TMP_Text combatMessage;
        [SerializeField] private TMP_Text phaseText;
        [SerializeField] private TMP_Text intentText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text predictionFeedbackText;
        [SerializeField] private Image heroHealthFill;
        [SerializeField] private Image enemyHealthFill;
        [SerializeField] private TMP_Text heroHealthValue;
        [SerializeField] private TMP_Text enemyHealthValue;

        [Header("Contextual world HUD")]
        [SerializeField] private WorldHealthBarView heroWorldHealthBar;
        [SerializeField] private WorldHealthBarView enemyWorldHealthBar;
        [SerializeField] private WorldDialogueBubbleView enemyWorldDialogue;
        [SerializeField] private WorldDialogueBubbleView allyWorldDialogue;

        [Header("Enemy dialogue")]
        [SerializeField] private GameObject enemyDialogueRoot;
        [SerializeField] private TMP_Text enemyDialogueText;

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
        [SerializeField] private GameObject heroGuardVisual;
        [SerializeField] private GameObject enemyGuardVisual;
        [SerializeField] private GameObject enemyChargeVisual;
        [SerializeField] private GameObject savedVisual;
        [SerializeField] private GameObject killedVisual;

        [Header("Saved ally support (optional)")]
        [SerializeField] private GameObject thornGuardianAllyActor;
        [SerializeField] private GameObject thornGuardianSupportEffect;
        [SerializeField] private GameObject allyDialogueRoot;
        [SerializeField] private TMP_Text allyDialogueText;

        [Header("Analyze panel")]
        [SerializeField] private GameObject analyzePanel;
        [SerializeField] private TMP_Text analyzeNameText;
        [SerializeField] private TMP_Text analyzeRaceText;
        [SerializeField] private TMP_Text analyzeCorruptionText;
        [SerializeField] private TMP_Text analyzeMoodText;
        [SerializeField] private TMP_Text analyzeTendencyText;
        [SerializeField] private TMP_Text analyzeIntentText;
        [SerializeField] private Button analyzeCloseButton;

        [Header("Final choice")]
        [SerializeField] private GameObject finalChoicePanel;
        [SerializeField] private TMP_Text finalChoiceTitleText;
        [SerializeField] private Image finalChoicePortrait;
        [SerializeField] private TMP_Text finalChoiceDialogueText;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button killButton;

        [Header("Choice confirmation")]
        [SerializeField] private GameObject confirmationPanel;
        [SerializeField] private TMP_Text confirmationText;
        [SerializeField] private Button confirmationConfirmButton;
        [SerializeField] private Button confirmationBackButton;

        [Header("Outcome")]
        [SerializeField] private GameObject outcomeOverlay;
        [SerializeField] private TMP_Text outcomeText;
        [SerializeField] private TMP_Text outcomeDialogueText;
        [SerializeField] private TMP_Text outcomeProgressText;
        [SerializeField] private Button outcomeMenuButton;
        [SerializeField] private Button outcomeContinueButton;
        [SerializeField] private Button outcomeRetryButton;
        [SerializeField] private EncounterBattleNavigation navigation;

        private const float DialogueVisibleSeconds = 2.7f;
        private const float ReactionLeadInSeconds = 0.52f;
        private const float LowHealthThreshold = 0.35f;
        private const string MoralConsequencesText =
            "\n\nSALVA: resta vivo; potrà tornare o aiutarti." +
            "\nUCCIDI: esce dalla storia; non potrà aiutarti.";
        private const float RecordedChoiceScale = 1.06f;

        private static readonly Color RecordedSaveColor =
            new Color(0.30f, 0.78f, 0.58f, 1f);
        private static readonly Color RecordedKillColor =
            new Color(0.88f, 0.38f, 0.40f, 1f);

        private readonly HashSet<EncounterAction> spokenActionReactions =
            new HashSet<EncounterAction>();

        private EncounterBattleState battleState;
        private EnemyMemory enemyMemory;
        private AdaptiveEnemyBrain enemyBrain;
        private Coroutine dialogueHideRoutine;
        private bool actionRunning;
        private bool analyzePanelOpen;
        private bool analyzeUsedThisTurn;
        private bool finalChoiceOpen;
        private bool confirmationOpen;
        private bool? pendingSaveChoice;
        private string pendingAnalyzeDialogue = string.Empty;
        private bool lowHpDialogueShown;
        private string announcedPatternKey = string.Empty;
        private bool rewardGrantedThisBattle;
        private bool isReplayBattle;
        private bool replayChoiceChanged;
        private EncounterResolution previousResolution;
        private SavedAllySupport thornGuardianSupport;
        private Coroutine allyDialogueHideRoutine;
        private int completedHeroTurns;
        private Vector3 thornGuardianSupportBaseScale;
        private Vector3 heroBasePosition;
        private Vector3 enemyBasePosition;
        private Color heroBaseColor;
        private Color enemyBaseColor;
        private Vector3 heroBasicProjectileBaseScale;
        private Vector3 heroTechniqueProjectileBaseScale;
        private Vector3 enemyProjectileBaseScale;
        private HeroCombatPresentation heroCombatPresentation;
        private Vector3 heroGuardBaseScale;
        private Vector3 enemyGuardBaseScale;
        private Vector3 enemyChargeBaseScale;
        private Vector3 savedVisualBaseScale;
        private Vector3 killedVisualBaseScale;
        private ColorBlock saveButtonNeutralColors;
        private ColorBlock killButtonNeutralColors;
        private Vector3 saveButtonNeutralScale;
        private Vector3 killButtonNeutralScale;

        public int HeroCurrentHp => battleState != null ? battleState.HeroHp : heroMaxHp;
        public int EnemyCurrentHp => battleState != null ? battleState.EnemyHp : enemyMaxHp;
        public int EnemyCorruption => battleState != null
            ? battleState.CorruptionPercent
            : Mathf.Clamp(enemyCorruptionPercent, 0, 100);
        public EnemyMood CurrentEnemyMood => battleState != null
            ? battleState.Mood
            : enemyInitialMood;
        public NarrativeOutcome Resolution => battleState != null
            ? battleState.Resolution
            : NarrativeOutcome.None;
        public EnemyIntent CurrentEnemyIntent => enemyBrain != null && enemyBrain.HasLockedIntent
            ? enemyBrain.LockedIntent.Value
            : EnemyIntent.Attack;
        public bool IsActionRunning => actionRunning;
        public bool IsAnalyzePanelOpen => analyzePanelOpen;
        public bool IsFinalChoiceOpen => finalChoiceOpen;
        public bool IsConfirmationOpen => confirmationOpen;
        public int AnalyzeCount => enemyMemory != null ? enemyMemory.AnalysisCount : 0;
        public bool AnalyzeUsedThisTurn => analyzeUsedThisTurn;

        private void Awake()
        {
            if (!HasRequiredReferences())
            {
                enabled = false;
                return;
            }

            CapturePersistentVisualState();
            heroCombatPresentation = HeroCombatPresentation.Ensure(heroActor);
            InitializeBattle();
        }

        private void OnValidate()
        {
            heroMaxHp = Mathf.Max(1, heroMaxHp);
            enemyMaxHp = Mathf.Max(1, enemyMaxHp);
            attackDamage = Mathf.Max(1, attackDamage);
            techniqueDamage = Mathf.Max(attackDamage + 1, techniqueDamage);
            enemyAttackDamage = Mathf.Max(1, enemyAttackDamage);
            chargedStrikeDamage = Mathf.Max(enemyAttackDamage + 1, chargedStrikeDamage);
            techniqueCooldownTurns = Mathf.Max(1, techniqueCooldownTurns);
            enemyGuardReductionPercent = Mathf.Clamp(enemyGuardReductionPercent, 1, 99);
            enemyCorruptionPercent = Mathf.Clamp(enemyCorruptionPercent, 0, 100);
            enemyIntelligenceLevel = Mathf.Clamp(enemyIntelligenceLevel, 0, 3);
            adaptiveLearningTuning = AdaptiveEnemyTuning.Normalize(adaptiveLearningTuning);
            resultReturnDelay = Mathf.Max(0.1f, resultReturnDelay);
        }

        public void ChooseAttack()
        {
            if (battlefield != null && !battlefield.CanUseOffensiveAction(1, enemyActor))
            {
                combatMessage.text = "Bersaglio fuori portata · usa MUOVI";
                return;
            }
            BeginPlayerAction(EncounterAction.Attack);
        }

        public void ChooseGuard()
        {
            BeginPlayerAction(EncounterAction.Guard);
        }

        public void ChooseTechnique()
        {
            if (battlefield != null && !battlefield.CanUseOffensiveAction(2, enemyActor))
            {
                combatMessage.text = "Bersaglio fuori portata della Tecnica";
                return;
            }
            BeginPlayerAction(EncounterAction.Technique);
        }

        public void BeginTacticalMove()
        {
            if (CanAcceptInput() && battlefield != null)
            {
                battlefield.ToggleMoveMode();
            }
        }

        public void EndTacticalTurn()
        {
            if (!CanAcceptInput() || battlefield == null || !battleState.PassPlayerTurn())
            {
                return;
            }

            if (!enemyBrain.HasLockedIntent)
            {
                PlanNextIntent();
            }

            actionRunning = true;
            battlefield.CommitAction();
            SetAllActionButtons(false);
            StartCoroutine(ResolvePassedTurn(enemyBrain.LockedIntent.Value));
        }

        public void OpenAnalyze()
        {
            if (!CanAcceptInput() || analyzePanelOpen)
            {
                return;
            }

            if (analyzeUsedThisTurn)
            {
                combatMessage.text = "ANALIZZA GIÀ USATO · DISPONIBILE AL PROSSIMO TURNO";
                RefreshActionButtons();
                return;
            }

            EnemyIntent lockedBeforeAnalyze = CurrentEnemyIntent;
            var result = battleState.ResolvePlayerAction(EncounterAction.Analyze);
            if (!result.Accepted)
            {
                combatMessage.text = result.RejectionReason;
                RefreshActionButtons();
                return;
            }

            CampaignProgressStore.TryRecordPlayerAction(EncounterAction.Analyze.ToString());
            analyzeUsedThisTurn = true;

            if (!enemyBrain.HasLockedIntent || enemyBrain.LockedIntent != lockedBeforeAnalyze)
            {
                Debug.LogError("[Veyra Encounter] ANALIZZA ha modificato l'intenzione bloccata.", this);
            }

            analyzePanelOpen = true;
            SetAllActionButtons(false);
            PopulateAnalyzePanel();
            analyzeCloseButton.interactable = true;
            analyzePanel.SetActive(true);
            SetPhase("ANALIZZA · DOSSIER NEMICO");
            combatMessage.text = battleState.IsEnemyExposed
                ? "VISTA DELLA CORRUZIONE: ESPOSTO applicato · prossimo danno +25%"
                : "Informazioni sul nemico: nessun turno consumato";

            pendingAnalyzeDialogue = enemyMemory.AnalysisCount <= 1
                ? firstAnalyzeDialogue
                : repeatedAnalyzeDialogue;
            if (enemyMemory.AnalysisCount > 2)
            {
                pendingAnalyzeDialogue = string.Empty;
            }

            if (dialogueHideRoutine != null)
            {
                StopCoroutine(dialogueHideRoutine);
                dialogueHideRoutine = null;
            }

            enemyDialogueRoot.SetActive(false);
            UpdateHudImmediate();
        }

        public void CloseAnalyze()
        {
            if (!analyzePanelOpen)
            {
                return;
            }

            analyzeCloseButton.interactable = false;
            analyzePanel.SetActive(false);
            analyzePanelOpen = false;
            SetPhase("TUO TURNO · SCEGLI UN'AZIONE");
            combatMessage.text = "Analisi completata: nessun turno consumato";

            string dialogue = pendingAnalyzeDialogue;
            pendingAnalyzeDialogue = string.Empty;
            if (!string.IsNullOrWhiteSpace(dialogue))
            {
                ShowEnemyDialogue(dialogue);
            }

            RefreshActionButtons();
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
                !battleState.EnemyDefeated || battleState.IsFinished)
            {
                return;
            }

            bool save = pendingSaveChoice.Value;
            confirmationConfirmButton.interactable = false;
            confirmationBackButton.interactable = false;
            confirmationPanel.SetActive(false);
            confirmationOpen = false;
            finalChoicePanel.SetActive(false);
            finalChoiceOpen = false;
            actionRunning = true;
            if (battlefield != null)
            {
                battlefield.CommitAction();
            }

            EncounterResolution selectedResolution = save
                ? EncounterResolution.Saved
                : EncounterResolution.Killed;
            bool wasReplayBattle = isReplayBattle;
            replayChoiceChanged = isReplayBattle && previousResolution != selectedResolution;
            battleState.ResolveDefeatedEnemy(save);
            RecordCampaignResolution(save);
            StartCoroutine(ShowNarrativeOutcome(save, wasReplayBattle));
        }

        public void BackFromFinalConfirmation()
        {
            if (!confirmationOpen || battleState == null || !battleState.EnemyDefeated ||
                battleState.IsFinished)
            {
                return;
            }

            pendingSaveChoice = null;
            confirmationPanel.SetActive(false);
            confirmationOpen = false;
            confirmationConfirmButton.interactable = true;
            confirmationBackButton.interactable = true;
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
            dialogueHideRoutine = null;
            allyDialogueHideRoutine = null;
            actionRunning = false;
            analyzePanelOpen = false;
            analyzeUsedThisTurn = false;
            finalChoiceOpen = false;
            confirmationOpen = false;
            pendingSaveChoice = null;
            pendingAnalyzeDialogue = string.Empty;
            analyzePanel.SetActive(false);
            finalChoicePanel.SetActive(false);
            confirmationPanel.SetActive(false);
            outcomeOverlay.SetActive(false);
            enemyDialogueRoot.SetActive(false);
            if (allyDialogueRoot != null)
            {
                allyDialogueRoot.SetActive(false);
            }
            RestoreMoralChoiceButtonVisuals();
            ResetTransientEffects();
            SetAllActionButtons(false);
        }

        public void ShowExternalMessage(string message)
        {
            if (combatMessage != null)
            {
                combatMessage.text = message;
            }
        }

        private void InitializeBattle()
        {
            HeroCombatStats heroStats = HeroProgressStore.GetCombatStats();
            heroMaxHp = heroStats.MaxHp;
            attackDamage = heroStats.AttackDamage;
            techniqueDamage = heroStats.TechniqueDamage;

            EncounterRules rules = new EncounterRules(
                heroMaxHp,
                enemyMaxHp,
                attackDamage,
                techniqueDamage,
                enemyAttackDamage,
                chargedStrikeDamage,
                techniqueCooldownTurns,
                enemyGuardReductionPercent,
                heroStats.AnalyzeAppliesExposed,
                heroStats.ExposedDamagePercent);
            EnemyProfile profile = new EnemyProfile(
                encounterId,
                enemyDisplayName,
                enemyRace,
                enemyCorruptionPercent,
                enemyInitialMood,
                enemyIntelligenceLevel);

            AdaptiveEnemyTuning learningTuning = AdaptiveEnemyTuning.Normalize(
                adaptiveLearningTuning);
            enemyMemory = new EnemyMemory(
                techniqueCooldownTurns,
                EnemyMemory.DefaultCapacity,
                learningTuning);
            battleState = new EncounterBattleState(rules, profile, enemyMemory);
            SeedEnemyMemoryFromCampaign();
            enemyBrain = new AdaptiveEnemyBrain(
                enemyIntelligenceLevel,
                enemyRandomSeed,
                learningTuning);

            actionRunning = false;
            analyzePanelOpen = false;
            finalChoiceOpen = false;
            confirmationOpen = false;
            pendingSaveChoice = null;
            pendingAnalyzeDialogue = string.Empty;
            lowHpDialogueShown = false;
            announcedPatternKey = string.Empty;
            rewardGrantedThisBattle = false;
            isReplayBattle = CampaignProgressStore.TryGetEncounterResolution(
                campaignEncounter,
                out previousResolution);
            replayChoiceChanged = false;
            completedHeroTurns = 0;
            spokenActionReactions.Clear();

            ResetPersistentVisuals();
            RestoreMoralChoiceButtonVisuals();
            analyzePanel.SetActive(false);
            finalChoicePanel.SetActive(false);
            confirmationPanel.SetActive(false);
            outcomeOverlay.SetActive(false);
            enemyDialogueRoot.SetActive(false);
            if (allyDialogueRoot != null)
            {
                allyDialogueRoot.SetActive(false);
            }

            if (thornGuardianSupportEffect != null)
            {
                thornGuardianSupportEffect.SetActive(false);
            }
            predictionFeedbackText.text = string.Empty;
            outcomeMenuButton.interactable = true;
            if (outcomeContinueButton != null)
            {
                outcomeContinueButton.gameObject.SetActive(false);
                outcomeContinueButton.interactable = false;
            }
            if (outcomeRetryButton != null)
            {
                outcomeRetryButton.gameObject.SetActive(false);
                outcomeRetryButton.interactable = false;
            }

            PlanNextIntent();
            UpdateHudImmediate();
            heroWorldHealthBar?.SetHealthSilently(battleState.HeroHp, battleState.HeroMaxHp);
            enemyWorldHealthBar?.SetHealthSilently(battleState.EnemyHp, battleState.EnemyMaxHp);
            enemyWorldDialogue?.HideImmediate();
            allyWorldDialogue?.HideImmediate();
            SetPhase("TUO TURNO · SCEGLI UN'AZIONE");
            combatMessage.text = "Osserva l'intenzione nemica e scegli la tua azione";
            ShowEnemyDialogue(openingDialogue);
            ConfigureSavedAllySupport();
            RefreshActionButtons();
        }

        private void BeginPlayerAction(EncounterAction action)
        {
            if (!CanAcceptInput())
            {
                return;
            }

            if (!battleState.CanUsePlayerAction(action))
            {
                combatMessage.text = action == EncounterAction.Technique
                    ? "La Tecnica non è ancora pronta"
                    : "Questa azione non è disponibile";
                RefreshActionButtons();
                return;
            }

            if (!enemyBrain.HasLockedIntent)
            {
                PlanNextIntent();
            }

            EnemyIntent intentLockedBeforeInput = enemyBrain.LockedIntent.Value;
            actionRunning = true;
            SetPhase("AZIONE DI HERO01");
            SetAllActionButtons(false);
            StartCoroutine(ResolveTurn(action, intentLockedBeforeInput));
        }

        private IEnumerator ResolveTurn(EncounterAction action, EnemyIntent lockedIntent)
        {
            string reaction = GetReactionDialogue(action);
            if (!string.IsNullOrWhiteSpace(reaction))
            {
                ShowEnemyDialogue(reaction);
                yield return new WaitForSecondsRealtime(ReactionLeadInSeconds);
            }

            int enemyHpBefore = battleState.EnemyHp;
            yield return AnimatePlayerAction(action);

            var playerResult = battleState.ResolvePlayerAction(action);
            if (!playerResult.Accepted)
            {
                combatMessage.text = playerResult.RejectionReason;
                actionRunning = false;
                ResetActorPositions();
                RefreshActionButtons();
                yield break;
            }

            CampaignProgressStore.TryRecordPlayerAction(action.ToString());

            if (!enemyBrain.HasLockedIntent || enemyBrain.LockedIntent != lockedIntent)
            {
                Debug.LogError(
                    "[Veyra Encounter] L'intenzione nemica è cambiata dopo la scelta del giocatore.",
                    this);
            }

            if (action == EncounterAction.Attack || action == EncounterAction.Technique)
            {
                enemyWorldHealthBar?.ShowDamage(
                    enemyHpBefore,
                    battleState.EnemyHp,
                    battleState.EnemyMaxHp);
                if (playerResult.EnemyGuardReducedDamage)
                {
                    combatMessage.text = "GUARDIA DI CORTECCIA · COLPO BLOCCATO · 0 DANNI";
                    enemyGuardVisual.SetActive(true);
                    yield return PulseScale(
                        enemyGuardVisual,
                        enemyGuardBaseScale,
                        1.22f,
                        0.22f);
                }

                yield return Flash(enemyVisual, Color.white, 0.16f);
                yield return AnimateHealth(
                    enemyHealthFill,
                    enemyHealthValue,
                    enemyHpBefore,
                    battleState.EnemyHp,
                    battleState.EnemyMaxHp,
                    0.22f);
            }
            else if (action == EncounterAction.Guard)
            {
                heroGuardVisual.SetActive(true);
                yield return PulseScale(heroGuardVisual, heroGuardBaseScale, 1.28f, 0.30f);
            }

            ResetActorPositions();
            ApplyPersistentCombatEffects();
            UpdateHudImmediate();

            if (battleState.EnemyDefeated)
            {
                EnterFinalChoice();
                yield break;
            }

            completedHeroTurns++;
            yield return ResolveSavedAllySupportIfReady();

            UpdateAdaptiveFeedback();

            if (!lowHpDialogueShown && battleState.EnemyHp <=
                Mathf.CeilToInt(battleState.EnemyMaxHp * LowHealthThreshold))
            {
                lowHpDialogueShown = true;
                ShowEnemyDialogue(lowHpDialogue);
                yield return new WaitForSecondsRealtime(0.45f);
            }

            yield return new WaitForSecondsRealtime(0.18f);
            yield return ResolveEnemyTurn(lockedIntent);
        }

        private IEnumerator AnimatePlayerAction(EncounterAction action)
        {
            switch (action)
            {
                case EncounterAction.Attack:
                    combatMessage.text = "Hero01 attacca";
                    if (heroCombatPresentation != null)
                        yield return heroCombatPresentation.PlayMelee(enemyActor, false);
                    break;
                case EncounterAction.Technique:
                    combatMessage.text = "Hero01 usa Tecnica";
                    if (heroCombatPresentation != null)
                        yield return heroCombatPresentation.PlayMelee(enemyActor, true);
                    break;
                case EncounterAction.Guard:
                    combatMessage.text = "Hero01 prepara Guardia";
                    yield return new WaitForSecondsRealtime(0.18f);
                    break;
            }
        }

        private IEnumerator ResolveEnemyTurn(EnemyIntent lockedIntent)
        {
            SetPhase("TURNO NEMICO");
            if (!enemyBrain.HasLockedIntent || enemyBrain.LockedIntent != lockedIntent)
            {
                Debug.LogError("[Veyra Encounter] Intenzione nemica non valida al turno nemico.", this);
                actionRunning = false;
                RefreshActionButtons();
                yield break;
            }

            int heroHpBefore = battleState.HeroHp;
            switch (lockedIntent)
            {
                case EnemyIntent.Attack:
                    combatMessage.text = enemyDisplayName + " attacca";
                    yield return AnimateEnemyStrike(1f, 0.25f);
                    break;
                case EnemyIntent.Guard:
                    combatMessage.text = enemyDisplayName + " usa Guardia di corteccia";
                    yield return new WaitForSecondsRealtime(0.24f);
                    break;
                case EnemyIntent.Charge:
                    combatMessage.text = enemyDisplayName + " prepara un Colpo caricato";
                    enemyChargeVisual.SetActive(true);
                    yield return PulseScale(enemyChargeVisual, enemyChargeBaseScale, 1.32f, 0.40f);
                    break;
                case EnemyIntent.ChargedStrike:
                    combatMessage.text = enemyDisplayName + " scatena il Colpo caricato";
                    enemyProjectile.transform.localScale = enemyProjectileBaseScale * 1.48f;
                    yield return AnimateEnemyStrike(1.18f, 0.34f);
                    break;
            }

            var enemyResult = battleState.ResolveEnemyIntent(lockedIntent);
            if (!enemyResult.Accepted)
            {
                combatMessage.text = enemyResult.RejectionReason;
                actionRunning = false;
                ResetActorPositions();
                ApplyPersistentCombatEffects();
                UpdateHudImmediate();
                RefreshActionButtons();
                yield break;
            }

            enemyBrain.CompleteLockedIntent();

            heroWorldHealthBar?.ShowDamage(
                heroHpBefore,
                battleState.HeroHp,
                battleState.HeroMaxHp);

            if (lockedIntent == EnemyIntent.Attack || lockedIntent == EnemyIntent.ChargedStrike)
            {
                if (enemyResult.BlockedByGuard)
                {
                    combatMessage.text = "PARATO";
                    heroGuardVisual.SetActive(true);
                    yield return PulseScale(heroGuardVisual, heroGuardBaseScale, 1.38f, 0.28f);
                    yield return Flash(heroVisual, new Color(0.72f, 1f, 0.94f, 1f), 0.15f);
                }
                else
                {
                    yield return Flash(heroVisual, Color.white, 0.16f);
                }

                yield return AnimateHealth(
                    heroHealthFill,
                    heroHealthValue,
                    heroHpBefore,
                    battleState.HeroHp,
                    battleState.HeroMaxHp,
                    0.22f);
            }
            else if (lockedIntent == EnemyIntent.Guard)
            {
                enemyGuardVisual.SetActive(true);
                yield return PulseScale(enemyGuardVisual, enemyGuardBaseScale, 1.30f, 0.34f);
            }

            ResetActorPositions();
            ApplyPersistentCombatEffects();
            UpdateHudImmediate();

            if (battleState.IsFinished && battleState.Resolution == NarrativeOutcome.HeroDefeated)
            {
                ShowHeroDefeat();
                yield break;
            }

            if (thornGuardianSupport != null &&
                thornGuardianSupport.TryGetHeroDifficultyDialogue(
                    battleState.HeroHp,
                    battleState.HeroMaxHp,
                    out SavedAllyDialogueLine difficultyLine))
            {
                ShowAllyDialogue(difficultyLine.Text);
            }

            PlanNextIntent();
            actionRunning = false;
            analyzeUsedThisTurn = false;
            if (battlefield != null)
            {
                battlefield.BeginHeroTurn();
            }
            SetPhase("TUO TURNO · SCEGLI UN'AZIONE");
            combatMessage.text = "Scegli la tua azione";
            RefreshActionButtons();
        }

        private IEnumerator AnimateEnemyStrike(float movementMultiplier, float projectileDuration)
        {
            enemyBasePosition = enemyActor.localPosition;
            yield return MoveActor(
                enemyActor,
                enemyBasePosition,
                enemyBasePosition + Vector3.left * (0.68f * movementMultiplier),
                0.16f);
            yield return MoveEffect(
                enemyProjectile,
                enemyProjectileOrigin.position,
                heroHitTarget.position,
                projectileDuration);
        }

        private IEnumerator ResolvePassedTurn(EnemyIntent lockedIntent)
        {
            combatMessage.text = "Hero01 passa il turno";
            yield return ResolveEnemyTurn(lockedIntent);
        }

        private void ConfigureSavedAllySupport()
        {
            CampaignProgressData progress = CampaignProgressStore.Load();
            bool shouldAppear = campaignEncounter == CampaignEncounter.AshWatcher &&
                                CampaignProgressStore.TryGetEnemyResolution(
                                    progress,
                                    CampaignContentIds.Level02ThornGuardian,
                                    CampaignContentIds.ThornGuardianEnemy,
                                    out EncounterResolution guardianResolution) &&
                                guardianResolution == EncounterResolution.Saved;
            if (thornGuardianAllyActor != null)
            {
                thornGuardianAllyActor.SetActive(shouldAppear);
            }

            thornGuardianSupport = shouldAppear
                ? new SavedAllySupport(SavedAllySupportCatalog.CreateThornGuardian())
                : null;
            if (thornGuardianSupport != null &&
                thornGuardianSupport.TryGetOpeningDialogue(out SavedAllyDialogueLine openingLine))
            {
                ShowAllyDialogue(openingLine.Text);
            }
        }

        private void SeedEnemyMemoryFromCampaign()
        {
            if (enemyMemory == null ||
                !CampaignProgressStore.CanEnemiesUsePlayerProfile((int)campaignEncounter))
            {
                return;
            }

            PlayerActionProfileSnapshot profile = CampaignProgressStore.GetPlayerActionProfile();
            int firstIndex = Mathf.Max(0, profile.RecentActions.Count - enemyMemory.Capacity);
            for (int index = firstIndex; index < profile.RecentActions.Count; index++)
            {
                switch (profile.RecentActions[index])
                {
                    case PlayerCombatAction.Attack:
                        enemyMemory.RecordCompletedAction(EncounterAction.Attack);
                        break;
                    case PlayerCombatAction.Guard:
                        enemyMemory.RecordCompletedAction(EncounterAction.Guard);
                        break;
                    case PlayerCombatAction.Technique:
                        enemyMemory.RecordCompletedAction(EncounterAction.Technique);
                        break;
                    case PlayerCombatAction.Analyze:
                        enemyMemory.RecordAnalyze();
                        break;
                }
            }
        }

        private IEnumerator ResolveSavedAllySupportIfReady()
        {
            if (thornGuardianSupport == null || battleState == null || battleState.EnemyDefeated)
            {
                yield break;
            }

            SavedAllyTargetSnapshot[] targets =
            {
                new SavedAllyTargetSnapshot(
                    encounterId,
                    0,
                    battleState.EnemyHp,
                    battleState.EnemyMaxHp)
            };
            if (!thornGuardianSupport.TryIntervene(
                    completedHeroTurns,
                    targets,
                    out SavedAllySupportAction supportAction))
            {
                yield break;
            }

            int enemyHpBefore = battleState.EnemyHp;
            int appliedDamage = battleState.ApplyExternalNonLethalDamage(supportAction.AppliedDamage);
            enemyWorldHealthBar?.ShowDamage(
                enemyHpBefore,
                battleState.EnemyHp,
                battleState.EnemyMaxHp);
            combatMessage.text = supportAction.AllyDisplayName + " usa " +
                                 supportAction.AttackDisplayName + " · " + appliedDamage + " danni";
            if (supportAction.HasDialogue)
            {
                ShowAllyDialogue(supportAction.Dialogue);
            }

            if (thornGuardianSupportEffect != null)
            {
                thornGuardianSupportEffect.SetActive(true);
                yield return PulseScale(
                    thornGuardianSupportEffect,
                    thornGuardianSupportBaseScale,
                    1.42f,
                    0.38f);
                thornGuardianSupportEffect.SetActive(false);
            }

            yield return Flash(enemyVisual, new Color(0.68f, 1f, 0.62f, 1f), 0.15f);
            yield return AnimateHealth(
                enemyHealthFill,
                enemyHealthValue,
                enemyHpBefore,
                battleState.EnemyHp,
                battleState.EnemyMaxHp,
                0.22f);
            UpdateHudImmediate();
            yield return new WaitForSecondsRealtime(0.22f);
        }

        private void ShowAllyDialogue(string dialogue)
        {
            if (string.IsNullOrWhiteSpace(dialogue))
            {
                return;
            }

            if (allyWorldDialogue != null)
            {
                allyWorldDialogue.ShowDialogue("ALLEATO SALVATO", dialogue);
                if (allyDialogueRoot != null) allyDialogueRoot.SetActive(false);
                return;
            }

            if (allyDialogueRoot == null || allyDialogueText == null)
            {
                return;
            }

            if (allyDialogueHideRoutine != null)
            {
                StopCoroutine(allyDialogueHideRoutine);
            }

            allyDialogueText.text = dialogue;
            allyDialogueRoot.SetActive(true);
            allyDialogueHideRoutine = StartCoroutine(HideAllyDialogueAfterDelay());
        }

        private IEnumerator HideAllyDialogueAfterDelay()
        {
            yield return new WaitForSecondsRealtime(DialogueVisibleSeconds);
            if (allyDialogueRoot != null)
            {
                allyDialogueRoot.SetActive(false);
            }

            allyDialogueHideRoutine = null;
        }

        private void PlanNextIntent()
        {
            if (battleState == null || battleState.IsFinished || battleState.EnemyDefeated)
            {
                return;
            }

            enemyBrain.PlanAndLockIntent(enemyMemory, EnemyDecisionContext.From(battleState));
            UpdateIntentText();
            PopulateAnalyzePanel();
        }

        private void UpdateAdaptiveFeedback()
        {
            if (!enemyMemory.HasEnoughObservationsForVisibleLearning(enemyIntelligenceLevel))
            {
                return;
            }

            if (enemyMemory.HasRecentStrategyChange && !string.IsNullOrEmpty(announcedPatternKey) &&
                announcedPatternKey != "changed")
            {
                announcedPatternKey = "changed";
                predictionFeedbackText.text = "Il nemico non è più sicuro della sua previsione.";
                ShowEnemyDialogue(strategyChangedDialogue);
                return;
            }

            if (enemyMemory.HasConsecutiveActionPattern(EncounterAction.Attack))
            {
                AnnouncePattern(
                    "attack",
                    "Il nemico ha riconosciuto la tua abitudine ad attaccare.",
                    attackPatternDialogue);
                return;
            }

            if (enemyMemory.HasConsecutiveActionPattern(EncounterAction.Guard))
            {
                AnnouncePattern(
                    "guard",
                    "Il nemico ha riconosciuto la tua abitudine a difenderti.",
                    guardPatternDialogue);
                return;
            }

            if (enemyMemory.TendsToUseTechniqueWhenReady)
            {
                AnnouncePattern(
                    "technique",
                    "Il nemico ha riconosciuto il ritmo della tua Tecnica.",
                    techniquePatternDialogue);
            }
        }

        private void AnnouncePattern(string key, string feedback, string dialogue)
        {
            if (announcedPatternKey == key)
            {
                return;
            }

            announcedPatternKey = key;
            predictionFeedbackText.text = feedback;
            ShowEnemyDialogue(dialogue);
        }

        private void OpenFinalConfirmation(bool save)
        {
            if (!finalChoiceOpen || confirmationOpen || battleState == null ||
                !battleState.EnemyDefeated || battleState.IsFinished)
            {
                return;
            }

            pendingSaveChoice = save;
            finalChoicePanel.SetActive(false);
            finalChoiceOpen = false;
            saveButton.interactable = false;
            killButton.interactable = false;
            RestoreMoralChoiceButtonVisuals();
            EncounterResolution nextResolution = save
                ? EncounterResolution.Saved
                : EncounterResolution.Killed;
            string changeWarning = isReplayBattle && previousResolution != nextResolution
                ? "\n\nQUESTA DECISIONE MODIFICHERÀ LA STORIA SALVATA."
                : string.Empty;
            confirmationText.text = (save
                ? "Vuoi tentare di salvare " + enemyDisplayName + "?"
                : "Vuoi uccidere " + enemyDisplayName + "?") + changeWarning;
            confirmationConfirmButton.interactable = true;
            confirmationBackButton.interactable = true;
            confirmationPanel.SetActive(true);
            confirmationOpen = true;
            SetPhase("CONFERMA LA DECISIONE");
        }

        private void EnterFinalChoice()
        {
            actionRunning = false;
            SetAllActionButtons(false);
            analyzePanel.SetActive(false);
            analyzePanelOpen = false;
            confirmationPanel.SetActive(false);
            confirmationOpen = false;
            pendingSaveChoice = null;
            predictionFeedbackText.text = string.Empty;
            intentText.text = "INTENZIONE\nNESSUNA · INCAPACITATO";
            SetPhase("DECIDI IL SUO DESTINO");

            finalChoiceTitleText.text = "DECIDI IL SUO DESTINO";
            finalChoicePortrait.sprite = enemyVisual.sprite;
            finalChoicePortrait.color = enemyVisual.color;
            finalChoicePortrait.preserveAspect = true;
            string profile = enemyDisplayName.ToUpperInvariant() +
                             "\nRAZZA · " + enemyRace +
                             "\nCORRUZIONE · " + battleState.CorruptionPercent + "%" +
                             "\nSTATO · " + GetMoodLabel(battleState.Mood);
            string replayNotice = isReplayBattle
                ? "\n\nESITO REGISTRATO: " + GetResolutionLabel(previousResolution) +
                  "\nPuoi mantenerlo oppure cambiare la storia."
                : string.Empty;
            finalChoiceDialogueText.text =
                profile + "\n\n" + defeatedDialogue + replayNotice + MoralConsequencesText;
            saveButton.interactable = true;
            killButton.interactable = true;
            finalChoicePanel.SetActive(true);
            finalChoiceOpen = true;
            RefreshRecordedMoralChoiceVisual();
            combatMessage.text = "NEMICO INCAPACITATO";
            ShowEnemyDialogue(defeatedDialogue);
            TryShowSavedAllyEndingDialogue();
            UpdateHudImmediate();
        }

        private IEnumerator ShowNarrativeOutcome(bool save, bool isReplay = false)
        {
            ResetTransientEffects();
            SetAllActionButtons(false);

            string dialogue;
            if (save)
            {
                dialogue = savedDialogue;
                savedVisual.SetActive(true);
                if (!isReplay)
                {
                    yield return PulseScale(savedVisual, savedVisualBaseScale, 1.32f, 0.55f);
                }

                enemyVisual.color = enemyBaseColor;
                outcomeText.text = "NEMICO SALVATO";
            }
            else
            {
                dialogue = killedDialogue;
                killedVisual.SetActive(true);
                if (!isReplay)
                {
                    yield return PulseScale(killedVisual, killedVisualBaseScale, 1.18f, 0.45f);
                }

                enemyVisual.color = new Color(
                    enemyBaseColor.r * 0.45f,
                    enemyBaseColor.g * 0.45f,
                    enemyBaseColor.b * 0.45f,
                    0.42f);
                outcomeText.text = "NEMICO UCCISO";
            }

            actionRunning = false;
            if (isReplay)
            {
                outcomeText.text = "RIVINCITA COMPLETATA\n" +
                                   (save ? "NEMICO SALVATO" : "NEMICO UCCISO");
                outcomeDialogueText.text = dialogue +
                                           (replayChoiceChanged
                                               ? "\n\nLa storia salvata è stata aggiornata."
                                               : "\n\nHai mantenuto la decisione registrata.");
            }
            else
            {
                outcomeDialogueText.text = dialogue;
            }

            bool canContinue = campaignEncounter == CampaignEncounter.ThornGuardian ||
                               campaignEncounter == CampaignEncounter.AshWatcher;
            if (outcomeContinueButton != null)
            {
                outcomeContinueButton.gameObject.SetActive(canContinue);
                outcomeContinueButton.interactable = canContinue;
            }

            if (outcomeProgressText != null)
            {
                int rewardExperience = CampaignLevelCatalog
                    .GetByNumber((int)campaignEncounter)
                    .ExperienceReward;
                outcomeProgressText.text = !rewardGrantedThisBattle
                    ? "RICOMPENSE GIÀ OTTENUTE  -  " +
                      (replayChoiceChanged ? "STORIA AGGIORNATA" : "SCELTA CONFERMATA")
                    : campaignEncounter == CampaignEncounter.AshWatcher
                        ? "+" + rewardExperience + " XP  -  HERO01 LIVELLO 3\n" +
                          "POTENZIAMENTO DISPONIBILE NEL MENU EROI"
                        : "+" + rewardExperience + " XP  -  LIVELLO 3 SBLOCCATO";
            }

            if (campaignEncounter == CampaignEncounter.AshWatcher && rewardGrantedThisBattle)
            {
                outcomeText.text = "LIVELLO EROE AUMENTATO";
            }
            outcomeMenuButton.interactable = true;
            if (outcomeRetryButton != null)
            {
                outcomeRetryButton.gameObject.SetActive(false);
                outcomeRetryButton.interactable = false;
            }
            outcomeOverlay.SetActive(true);
            combatMessage.text = isReplay
                ? (replayChoiceChanged ? "Storia aggiornata: " : "Scelta confermata: ") +
                  (save ? "SALVATO" : "UCCISO")
                : save ? "La corruzione è stata purificata" : "La scelta è definitiva";
            ShowEnemyDialogue(isReplay
                ? replayChoiceChanged
                    ? "La storia ricorderà questa nuova decisione."
                    : "Hai confermato la decisione già registrata."
                : dialogue);
            SetPhase("VITTORIA");
            UpdateHudImmediate();
        }

        private void ShowHeroDefeat()
        {
            actionRunning = false;
            SetAllActionButtons(false);
            analyzePanel.SetActive(false);
            analyzePanelOpen = false;
            finalChoicePanel.SetActive(false);
            finalChoiceOpen = false;
            confirmationPanel.SetActive(false);
            confirmationOpen = false;
            RestoreMoralChoiceButtonVisuals();
            intentText.text = "INTENZIONE\nNESSUNA";
            outcomeText.text = "SCONFITTA";
            outcomeDialogueText.text = "Hero01 non puo piu combattere.";
            if (outcomeProgressText != null)
            {
                outcomeProgressText.text = "Nessun XP ottenuto";
            }

            if (outcomeContinueButton != null)
            {
                outcomeContinueButton.gameObject.SetActive(false);
                outcomeContinueButton.interactable = false;
            }
            if (outcomeRetryButton != null)
            {
                outcomeRetryButton.gameObject.SetActive(true);
                outcomeRetryButton.interactable = true;
            }
            outcomeMenuButton.interactable = true;
            outcomeOverlay.SetActive(true);
            combatMessage.text = "SCONFITTA";
            SetPhase("SCONFITTA");
            TryShowSavedAllyEndingDialogue();
            UpdateHudImmediate();
        }

        private void TryShowSavedAllyEndingDialogue()
        {
            if (thornGuardianSupport != null &&
                thornGuardianSupport.TryGetEndingDialogue(out SavedAllyDialogueLine line))
            {
                ShowAllyDialogue(line.Text);
            }
        }

        private void RecordCampaignResolution(bool save)
        {
            try
            {
                LevelDefinition level = CampaignLevelCatalog.GetByNumber((int)campaignEncounter);
                bool rewardWasClaimed = CampaignProgressStore.IsLevelRewardClaimed(level.StableId);
                CampaignProgressStore.SetEncounterResolution(
                    campaignEncounter,
                    save ? EncounterResolution.Saved : EncounterResolution.Killed);
                rewardGrantedThisBattle = !rewardWasClaimed &&
                                          CampaignProgressStore.IsLevelRewardClaimed(level.StableId);
                previousResolution = save
                    ? EncounterResolution.Saved
                    : EncounterResolution.Killed;
                isReplayBattle = true;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[Veyra Encounter] Salvataggio della scelta non riuscito: " + exception.Message,
                    this);
                combatMessage.text = "Scelta completata, ma il salvataggio non è riuscito";
            }
        }

        private IEnumerator ReturnToMenuAfterDelay()
        {
            yield return new WaitForSecondsRealtime(resultReturnDelay);
            ReturnToMenu();
        }

        private string GetReactionDialogue(EncounterAction action)
        {
            if (!spokenActionReactions.Add(action))
            {
                return string.Empty;
            }

            switch (action)
            {
                case EncounterAction.Attack:
                    return attackReactionDialogue;
                case EncounterAction.Guard:
                    return guardReactionDialogue;
                case EncounterAction.Technique:
                    return techniqueReactionDialogue;
                default:
                    return string.Empty;
            }
        }

        private void ShowEnemyDialogue(string dialogue)
        {
            if (string.IsNullOrWhiteSpace(dialogue))
            {
                return;
            }

            if (enemyWorldDialogue != null)
            {
                enemyWorldDialogue.ShowDialogue(enemyDisplayName, dialogue);
                if (enemyDialogueRoot != null) enemyDialogueRoot.SetActive(false);
                if (combatMessage != null)
                {
                    combatMessage.text = enemyDisplayName + ": " + dialogue;
                }
                return;
            }

            if (dialogueHideRoutine != null)
            {
                StopCoroutine(dialogueHideRoutine);
            }

            enemyDialogueText.text = dialogue;
            enemyDialogueRoot.SetActive(true);
            if (combatMessage != null)
            {
                combatMessage.text = enemyDisplayName + ": " + dialogue;
            }
            dialogueHideRoutine = StartCoroutine(HideDialogueAfterDelay());
        }

        private IEnumerator HideDialogueAfterDelay()
        {
            yield return new WaitForSecondsRealtime(DialogueVisibleSeconds);
            enemyDialogueRoot.SetActive(false);
            dialogueHideRoutine = null;
        }

        private void PopulateAnalyzePanel()
        {
            if (battleState == null)
            {
                return;
            }

            analyzeNameText.text = "NOME\n" + enemyDisplayName;
            analyzeRaceText.text = "RAZZA\n" + enemyRace;
            analyzeCorruptionText.text = "CORRUZIONE\n" + battleState.CorruptionPercent + "%";
            analyzeMoodText.text = "STATO ATTUALE\n" + GetMoodLabel(battleState.Mood);
            analyzeTendencyText.text = "TENDENZA\n" + GetTendencyDescription();
            analyzeIntentText.text = "MOSSA ANNUNCIATA\n" + GetIntentLabel(CurrentEnemyIntent) +
                                     (battleState.IsEnemyExposed
                                         ? "\nESPOSTO · PROSSIMO DANNO +25%"
                                         : string.Empty);
        }

        private string GetTendencyDescription()
        {
            if (battleState.IsChargedStrikePrepared || CurrentEnemyIntent == EnemyIntent.ChargedStrike)
            {
                return "Sta per liberare un colpo molto potente.";
            }

            if (CurrentEnemyIntent == EnemyIntent.Charge)
            {
                return "Sta accumulando energia: il prossimo colpo sarà pericoloso.";
            }

            switch (battleState.Mood)
            {
                case EnemyMood.Arrabbiato:
                    return "Aggressivo e instabile.";
                case EnemyMood.Triste:
                    return "Esita, ma la corruzione lo costringe a combattere.";
                case EnemyMood.Guardingo:
                    return "Osserva le tue abitudini e prepara una risposta.";
                case EnemyMood.Spaventato:
                    return "Ha paura e cerca di sopravvivere.";
                case EnemyMood.Rassegnato:
                    return "Non crede di potersi liberare da solo.";
                default:
                    return "Il suo comportamento non è ancora chiaro.";
            }
        }

        private void UpdateIntentText()
        {
            if (intentText == null || enemyBrain == null || !enemyBrain.HasLockedIntent)
            {
                return;
            }

            intentText.text = "INTENZIONE\n" + GetIntentLabel(enemyBrain.LockedIntent.Value);
        }

        private static string GetIntentLabel(EnemyIntent intent)
        {
            switch (intent)
            {
                case EnemyIntent.Attack:
                    return "ATTACCO";
                case EnemyIntent.Guard:
                    return "GUARDIA DI CORTECCIA";
                case EnemyIntent.Charge:
                    return "CARICA";
                case EnemyIntent.ChargedStrike:
                    return "COLPO CARICATO";
                default:
                    return "SCONOSCIUTA";
            }
        }

        private static string GetMoodLabel(EnemyMood mood)
        {
            switch (mood)
            {
                case EnemyMood.Felice:
                    return "Felice";
                case EnemyMood.Triste:
                    return "Triste";
                case EnemyMood.Arrabbiato:
                    return "Arrabbiato";
                case EnemyMood.Guardingo:
                    return "Guardingo";
                case EnemyMood.Spaventato:
                    return "Spaventato";
                case EnemyMood.Rassegnato:
                    return "Rassegnato";
                default:
                    return mood.ToString();
            }
        }

        private static string GetResolutionLabel(EncounterResolution resolution)
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

        private void SetPhase(string value)
        {
            if (phaseText != null)
            {
                phaseText.text = value;
            }
        }

        private void UpdateHudImmediate()
        {
            if (battleState == null)
            {
                return;
            }

            heroHealthFill.fillAmount = battleState.HeroMaxHp == 0
                ? 0f
                : (float)battleState.HeroHp / battleState.HeroMaxHp;
            enemyHealthFill.fillAmount = battleState.EnemyMaxHp == 0
                ? 0f
                : (float)battleState.EnemyHp / battleState.EnemyMaxHp;
            heroHealthValue.text = battleState.HeroHp + " / " + battleState.HeroMaxHp;
            enemyHealthValue.text = battleState.EnemyHp + " / " + battleState.EnemyMaxHp;

            string heroState = battleState.IsHeroGuardPrepared ? "GUARDIA PRONTA" : "PRONTO";
            string enemyState = battleState.IsEnemyGuardPrepared
                ? "GUARDIA ATTIVA"
                : battleState.IsChargedStrikePrepared
                    ? "COLPO CARICATO"
                    : GetMoodLabel(battleState.Mood);
            statusText.text = "EROE: " + heroState + "\nNEMICO: " + enemyState;

            techniqueButtonLabel.text = battleState.TechniqueCooldownRemaining == 0
                ? "TECNICA · DANNO " + techniqueDamage + " · PORTATA 2\nPRONTA"
                : "TECNICA · DANNO " + techniqueDamage + " · PORTATA 2\nRICARICA " +
                  battleState.TechniqueCooldownRemaining +
                  (battleState.TechniqueCooldownRemaining == 1 ? " TURNO" : " TURNI");

            PopulateAnalyzePanel();
        }

        private void RefreshActionButtons()
        {
            bool canAct = CanAcceptInput();
            attackButton.interactable = canAct && battleState.CanUsePlayerAction(EncounterAction.Attack);
            guardButton.interactable = canAct && battleState.CanUsePlayerAction(EncounterAction.Guard);
            techniqueButton.interactable =
                canAct && battleState.CanUsePlayerAction(EncounterAction.Technique);
            analyzeButton.interactable = canAct && !analyzeUsedThisTurn &&
                                         battleState.CanUsePlayerAction(EncounterAction.Analyze);
        }

        private void SetAllActionButtons(bool enabledForInput)
        {
            attackButton.interactable = enabledForInput;
            guardButton.interactable = enabledForInput;
            techniqueButton.interactable = enabledForInput;
            analyzeButton.interactable = enabledForInput;
        }

        private bool CanAcceptInput()
        {
            return battleState != null && !actionRunning && !analyzePanelOpen &&
                   !finalChoiceOpen && !confirmationOpen && !battleState.IsFinished &&
                   !battleState.EnemyDefeated;
        }

        private void CapturePersistentVisualState()
        {
            heroBasePosition = heroActor.localPosition;
            enemyBasePosition = enemyActor.localPosition;
            heroBaseColor = heroVisual.color;
            enemyBaseColor = enemyVisual.color;
            heroBasicProjectileBaseScale = heroBasicProjectile.transform.localScale;
            heroTechniqueProjectileBaseScale = heroTechniqueProjectile.transform.localScale;
            enemyProjectileBaseScale = enemyProjectile.transform.localScale;
            heroGuardBaseScale = heroGuardVisual.transform.localScale;
            enemyGuardBaseScale = enemyGuardVisual.transform.localScale;
            enemyChargeBaseScale = enemyChargeVisual.transform.localScale;
            savedVisualBaseScale = savedVisual.transform.localScale;
            killedVisualBaseScale = killedVisual.transform.localScale;
            saveButtonNeutralColors = saveButton.colors;
            killButtonNeutralColors = killButton.colors;
            saveButtonNeutralScale = saveButton.transform.localScale;
            killButtonNeutralScale = killButton.transform.localScale;
            thornGuardianSupportBaseScale = thornGuardianSupportEffect != null
                ? thornGuardianSupportEffect.transform.localScale
                : Vector3.one;
        }

        private void ResetPersistentVisuals()
        {
            heroActor.localPosition = heroBasePosition;
            enemyActor.localPosition = enemyBasePosition;
            heroVisual.color = heroBaseColor;
            enemyVisual.color = enemyBaseColor;
            heroBasicProjectile.transform.localScale = heroBasicProjectileBaseScale;
            heroTechniqueProjectile.transform.localScale = heroTechniqueProjectileBaseScale;
            enemyProjectile.transform.localScale = enemyProjectileBaseScale;
            heroGuardVisual.transform.localScale = heroGuardBaseScale;
            enemyGuardVisual.transform.localScale = enemyGuardBaseScale;
            enemyChargeVisual.transform.localScale = enemyChargeBaseScale;
            savedVisual.transform.localScale = savedVisualBaseScale;
            killedVisual.transform.localScale = killedVisualBaseScale;
            ResetTransientEffects();
        }

        private void RefreshRecordedMoralChoiceVisual()
        {
            RestoreMoralChoiceButtonVisuals();

            if (!isReplayBattle)
            {
                return;
            }

            if (previousResolution == EncounterResolution.Saved)
            {
                ApplyRecordedChoiceStyle(
                    saveButton,
                    saveButtonNeutralColors,
                    saveButtonNeutralScale,
                    RecordedSaveColor);
            }
            else if (previousResolution == EncounterResolution.Killed)
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

        private void ApplyPersistentCombatEffects()
        {
            heroGuardVisual.SetActive(battleState.IsHeroGuardPrepared);
            enemyGuardVisual.SetActive(battleState.IsEnemyGuardPrepared);
            enemyChargeVisual.SetActive(battleState.IsChargedStrikePrepared);
            heroTechniqueProjectile.transform.localScale = heroTechniqueProjectileBaseScale;
            enemyProjectile.transform.localScale = enemyProjectileBaseScale;
        }

        private void ResetTransientEffects()
        {
            ResetActorPositions();
            heroBasicProjectile.transform.position = heroProjectileOrigin.position;
            heroTechniqueProjectile.transform.position = heroProjectileOrigin.position;
            enemyProjectile.transform.position = enemyProjectileOrigin.position;
            heroBasicProjectile.SetActive(false);
            heroTechniqueProjectile.SetActive(false);
            enemyProjectile.SetActive(false);
            if (thornGuardianSupportEffect != null)
            {
                thornGuardianSupportEffect.transform.localScale = thornGuardianSupportBaseScale;
                thornGuardianSupportEffect.SetActive(false);
            }
            heroGuardVisual.SetActive(false);
            enemyGuardVisual.SetActive(false);
            enemyChargeVisual.SetActive(false);
            savedVisual.SetActive(false);
            killedVisual.SetActive(false);
        }

        private void ResetActorPositions()
        {
            enemyActor.localPosition = enemyBasePosition;
        }

        private bool HasRequiredReferences()
        {
            bool valid = attackButton != null && guardButton != null && techniqueButton != null &&
                         analyzeButton != null && techniqueButtonLabel != null &&
                         combatMessage != null && phaseText != null && intentText != null &&
                         statusText != null &&
                         predictionFeedbackText != null && heroHealthFill != null &&
                         enemyHealthFill != null && heroHealthValue != null &&
                         enemyHealthValue != null && enemyDialogueRoot != null &&
                         enemyDialogueText != null && heroActor != null && enemyActor != null &&
                         heroVisual != null && enemyVisual != null &&
                         heroProjectileOrigin != null && heroHitTarget != null &&
                         enemyProjectileOrigin != null && enemyHitTarget != null &&
                         heroBasicProjectile != null && heroTechniqueProjectile != null &&
                         enemyProjectile != null && heroGuardVisual != null &&
                         enemyGuardVisual != null && enemyChargeVisual != null &&
                         savedVisual != null && killedVisual != null && analyzePanel != null &&
                         analyzeNameText != null && analyzeRaceText != null &&
                         analyzeCorruptionText != null && analyzeMoodText != null &&
                         analyzeTendencyText != null && analyzeIntentText != null &&
                         analyzeCloseButton != null && finalChoicePanel != null &&
                         finalChoiceTitleText != null && finalChoicePortrait != null &&
                         finalChoiceDialogueText != null &&
                         saveButton != null && killButton != null && confirmationPanel != null &&
                         confirmationText != null && confirmationConfirmButton != null &&
                         confirmationBackButton != null && outcomeOverlay != null &&
                         outcomeText != null && outcomeDialogueText != null &&
                         outcomeMenuButton != null && outcomeRetryButton != null &&
                         navigation != null;

            if (!valid)
            {
                Debug.LogError(
                    "[Veyra Encounter] Riferimenti serializzati mancanti: rigenera la scena dalla factory.",
                    this);
            }

            return valid;
        }

        private static IEnumerator MoveActor(
            Transform actor,
            Vector3 start,
            Vector3 end,
            float duration)
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

        private static IEnumerator MoveEffect(
            GameObject effect,
            Vector3 start,
            Vector3 end,
            float duration)
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
                float factor;
                if (elapsed <= halfDuration)
                {
                    factor = Mathf.Lerp(1f, multiplier, Mathf.Clamp01(elapsed / halfDuration));
                }
                else
                {
                    factor = Mathf.Lerp(
                        multiplier,
                        1f,
                        Mathf.Clamp01((elapsed - halfDuration) / halfDuration));
                }

                effect.transform.localScale = baseScale * factor;
                yield return null;
            }

            effect.transform.localScale = baseScale;
        }

        private static IEnumerator AnimateHealth(
            Image fill,
            TMP_Text valueText,
            int startHp,
            int endHp,
            int maxHp,
            float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                float displayHp = Mathf.Lerp(startHp, endHp, normalized);
                fill.fillAmount = maxHp == 0 ? 0f : displayHp / maxHp;
                valueText.text = Mathf.RoundToInt(displayHp) + " / " + maxHp;
                yield return null;
            }

            fill.fillAmount = maxHp == 0 ? 0f : (float)endHp / maxHp;
            valueText.text = endHp + " / " + maxHp;
        }
    }
}
