using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Veyra.Combat.Tutorial
{
    public enum TutorialStep
    {
        Welcome,
        Positions,
        Health,
        AwaitingFirstAttack,
        EnemyCounterattack,
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
        [SerializeField, Min(1)] private int enemyAttackDamage = 12;
        [SerializeField, Min(0)] private int guardDamageReduction = 6;
        [SerializeField, Min(0)] private int techniqueCooldownTurns = 2;
        [SerializeField, Min(1.01f)] private float markDamageMultiplier = 1.5f;
        [SerializeField, Range(0, 2)] private int enemyIntelligenceLevel;
        [SerializeField, Min(0.1f)] private float resultReturnDelay = 2.5f;

        [Header("Action controls")]
        [SerializeField] private Button attackButton;
        [SerializeField] private Button guardButton;
        [SerializeField] private Button techniqueButton;
        [SerializeField] private Button markButton;
        [SerializeField] private TMP_Text techniqueButtonLabel;
        [SerializeField] private GameObject attackHighlight;

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
        [SerializeField] private GameObject markVisual;

        [Header("Tutorial overlay")]
        [SerializeField] private GameObject tutorialOverlay;
        [SerializeField] private Image tutorialInputBlocker;
        [SerializeField] private TMP_Text tutorialStepText;
        [SerializeField] private TMP_Text tutorialBodyText;
        [SerializeField] private Button tutorialNextButton;

        [Header("Outcome overlay")]
        [SerializeField] private GameObject outcomeOverlay;
        [SerializeField] private TMP_Text outcomeText;
        [SerializeField] private Button outcomeMenuButton;
        [SerializeField] private TutorialBattleNavigation navigation;

        private const string PlayerTurnMessage = "Scegli la tua azione";

        private TutorialBattleState battleState;
        private bool actionRunning;
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
        private Vector3 markBaseScale;

        public int HeroCurrentHp => battleState?.HeroHp ?? heroMaxHp;
        public int EnemyCurrentHp => battleState?.EnemyHp ?? enemyMaxHp;
        public BattleOutcome Outcome => battleState?.Outcome ?? BattleOutcome.Ongoing;
        public TutorialStep CurrentTutorialStep { get; private set; }
        public bool IsActionRunning => actionRunning;
        public bool IsTutorialComplete => CurrentTutorialStep == TutorialStep.Complete;
        public int EnemyIntelligenceLevel => enemyIntelligenceLevel;

        private void Awake()
        {
            CapturePersistentVisualState();
            InitializeBattle();
        }

        private void OnValidate()
        {
            heroMaxHp = Mathf.Max(1, heroMaxHp);
            enemyMaxHp = Mathf.Max(1, enemyMaxHp);
            attackDamage = Mathf.Max(1, attackDamage);
            techniqueDamage = Mathf.Max(1, techniqueDamage);
            enemyAttackDamage = Mathf.Max(1, enemyAttackDamage);
            guardDamageReduction = Mathf.Clamp(guardDamageReduction, 0, Mathf.Max(0, enemyAttackDamage - 1));
            techniqueCooldownTurns = Mathf.Max(0, techniqueCooldownTurns);
            markDamageMultiplier = Mathf.Max(1.01f, markDamageMultiplier);
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

            if (actionRunning || battleState == null || battleState.IsFinished)
            {
                return;
            }

            switch (CurrentTutorialStep)
            {
                case TutorialStep.Welcome:
                    CurrentTutorialStep = TutorialStep.Positions;
                    ShowBlockingTutorial(
                        "PASSO 2 / 7",
                        "Tu controlli l'eroe a sinistra. Il tuo avversario è la creatura corrotta a destra.");
                    break;
                case TutorialStep.Positions:
                    CurrentTutorialStep = TutorialStep.Health;
                    ShowBlockingTutorial(
                        "PASSO 3 / 7",
                        "Le barre mostrano i punti vita, o HP. Se i tuoi HP raggiungono zero, perdi.");
                    break;
                case TutorialStep.Health:
                    CurrentTutorialStep = TutorialStep.AwaitingFirstAttack;
                    ShowAttackPrompt();
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

        public void PreviewMark()
        {
            BeginPlayerAction(BattleAction.Mark);
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
            waitingForTutorialAdvance = false;
            tutorialAdvanceRequested = false;
            ResetPersistentEffects();
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
            markBaseScale = markVisual.transform.localScale;
        }

        private void InitializeBattle()
        {
            battleState = new TutorialBattleState(
                heroMaxHp,
                enemyMaxHp,
                attackDamage,
                techniqueDamage,
                enemyAttackDamage,
                guardDamageReduction,
                techniqueCooldownTurns,
                markDamageMultiplier,
                historyCapacity: 8,
                repeatedPatternLength: GetObservationLengthForIntelligence());

            actionRunning = false;
            waitingForTutorialAdvance = false;
            tutorialAdvanceRequested = false;
            repeatedPatternMessageShown = false;
            CurrentTutorialStep = TutorialStep.Welcome;

            ResetPersistentEffects();
            UpdateHealthImmediate();
            UpdateStatusAndCooldown();
            intentText.text = "INTENZIONE\nATTACCO IN ARRIVO";
            combatMessage.text = "Impara le basi del combattimento";
            outcomeOverlay.SetActive(false);
            outcomeMenuButton.interactable = true;
            attackHighlight.SetActive(false);
            ShowBlockingTutorial("PASSO 1 / 7", "Benvenuto nel combattimento.");
        }

        private void BeginPlayerAction(BattleAction action)
        {
            if (actionRunning || battleState == null || battleState.IsFinished)
            {
                return;
            }

            bool firstAttackRequired = CurrentTutorialStep == TutorialStep.AwaitingFirstAttack;
            if (!IsTutorialComplete && (!firstAttackRequired || action != BattleAction.Attack))
            {
                return;
            }

            if (!battleState.CanUsePlayerAction(action))
            {
                combatMessage.text = "La Tecnica non è ancora pronta";
                RefreshActionButtons();
                return;
            }

            actionRunning = true;
            tutorialOverlay.SetActive(false);
            attackHighlight.SetActive(false);
            SetAllActionButtons(false);
            StartCoroutine(ResolveTurn(action, firstAttackRequired));
        }

        private IEnumerator ResolveTurn(BattleAction action, bool isFirstAttack)
        {
            int enemyHpBefore = battleState.EnemyHp;
            BattleActionResult playerResult;

            switch (action)
            {
                case BattleAction.Attack:
                    combatMessage.text = "Hero01 attacca";
                    yield return MoveActor(heroActor, heroBasePosition, heroBasePosition + Vector3.right * 0.72f, 0.16f);
                    yield return MoveEffect(heroBasicProjectile, heroProjectileOrigin.position, enemyHitTarget.position, 0.24f);
                    playerResult = battleState.ResolvePlayerAction(action);
                    yield return Flash(enemyVisual, Color.white, 0.16f);
                    yield return AnimateHealth(enemyHealthFill, enemyHealthValue, enemyHpBefore, battleState.EnemyHp, battleState.EnemyMaxHp, 0.20f);
                    yield return MoveActor(heroActor, heroActor.localPosition, heroBasePosition, 0.16f);
                    break;
                case BattleAction.Technique:
                    combatMessage.text = "Hero01 usa Tecnica";
                    yield return MoveActor(heroActor, heroBasePosition, heroBasePosition + Vector3.right * 0.58f, 0.16f);
                    heroTechniqueProjectile.transform.localScale = techniqueProjectileBaseScale * 1.25f;
                    yield return MoveEffect(heroTechniqueProjectile, heroProjectileOrigin.position, enemyHitTarget.position, 0.38f);
                    playerResult = battleState.ResolvePlayerAction(action);
                    yield return Flash(enemyVisual, new Color(0.73f, 1f, 0.94f, 1f), 0.24f);
                    yield return AnimateHealth(enemyHealthFill, enemyHealthValue, enemyHpBefore, battleState.EnemyHp, battleState.EnemyMaxHp, 0.22f);
                    yield return MoveActor(heroActor, heroActor.localPosition, heroBasePosition, 0.16f);
                    break;
                case BattleAction.Guard:
                    combatMessage.text = "Guardia preparata";
                    playerResult = battleState.ResolvePlayerAction(action);
                    guardVisual.SetActive(true);
                    yield return PulseScale(guardVisual, guardBaseScale, 1.28f, 0.34f);
                    break;
                case BattleAction.Mark:
                    combatMessage.text = "Marchio applicato: il prossimo colpo sarà potenziato";
                    playerResult = battleState.ResolvePlayerAction(action);
                    markVisual.SetActive(true);
                    yield return PulseScale(markVisual, markBaseScale, 1.55f, 0.42f);
                    break;
                default:
                    actionRunning = false;
                    yield break;
            }

            if (!playerResult.Accepted)
            {
                combatMessage.text = playerResult.RejectionReason;
                actionRunning = false;
                RefreshActionButtons();
                yield break;
            }

            if (battleState.IsFinished)
            {
                ShowOutcome();
                yield break;
            }

            bool repeatedPattern = battleState.TryGetRepeatedPlayerAction(out _);

            if (isFirstAttack)
            {
                yield return WaitForTutorialCard(
                    TutorialStep.EnemyCounterattack,
                    "PASSO 5 / 7",
                    "Ora il nemico contrattacca. Anche i suoi attacchi riducono i tuoi HP.");
            }

            yield return ResolveEnemyTurn();
            if (battleState.IsFinished)
            {
                ShowOutcome();
                yield break;
            }

            if (isFirstAttack)
            {
                yield return WaitForTutorialCard(
                    TutorialStep.EnemyLearning,
                    "PASSO 6 / 7",
                    "I nemici più evoluti osserveranno le tue abitudini e impareranno come combatti.");
                yield return WaitForTutorialCard(
                    TutorialStep.VictoryGoal,
                    "PASSO 7 / 7",
                    "Porta gli HP del nemico a zero per vincere.");
                CurrentTutorialStep = TutorialStep.Complete;
            }

            actionRunning = false;
            ResetTransientEffects();
            UpdateStatusAndCooldown();

            if (repeatedPattern && !repeatedPatternMessageShown)
            {
                repeatedPatternMessageShown = true;
                combatMessage.text = "Il nemico ti sta osservando";
            }
            else
            {
                combatMessage.text = PlayerTurnMessage;
            }

            RefreshActionButtons();
        }

        private IEnumerator ResolveEnemyTurn()
        {
            combatMessage.text = "Turno nemico";
            yield return new WaitForSecondsRealtime(0.16f);
            yield return MoveActor(enemyActor, enemyBasePosition, enemyBasePosition + Vector3.left * 0.72f, 0.16f);
            yield return MoveEffect(enemyProjectile, enemyProjectileOrigin.position, heroHitTarget.position, 0.26f);

            int heroHpBefore = battleState.HeroHp;
            BattleActionResult enemyResult = battleState.ResolveEnemyAttack();
            Color hitColor = enemyResult.ReducedByGuard
                ? new Color(0.73f, 1f, 0.94f, 1f)
                : new Color(1f, 0.70f, 0.70f, 1f);
            yield return Flash(heroVisual, hitColor, 0.16f);
            yield return AnimateHealth(heroHealthFill, heroHealthValue, heroHpBefore, battleState.HeroHp, battleState.HeroMaxHp, 0.20f);
            yield return MoveActor(enemyActor, enemyActor.localPosition, enemyBasePosition, 0.16f);
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
            attackHighlight.SetActive(false);
            SetAllActionButtons(false);
        }

        private void ShowAttackPrompt()
        {
            tutorialStepText.text = "PASSO 4 / 7";
            tutorialBodyText.text = "Premi ATTACCO per colpire il nemico. Ogni attacco riduce gli HP del bersaglio.";
            tutorialInputBlocker.raycastTarget = false;
            tutorialNextButton.gameObject.SetActive(false);
            tutorialOverlay.SetActive(true);
            attackHighlight.SetActive(true);
            SetAllActionButtons(false);
            attackButton.interactable = true;
            combatMessage.text = "Premi ATTACCO";
        }

        private void ShowOutcome()
        {
            actionRunning = false;
            waitingForTutorialAdvance = false;
            tutorialOverlay.SetActive(false);
            attackHighlight.SetActive(false);
            SetAllActionButtons(false);
            ResetTransientEffects();

            bool victory = battleState.Outcome == BattleOutcome.Victory;
            outcomeText.text = victory ? "VITTORIA" : "SCONFITTA";
            outcomeText.color = victory ? new Color(0.35f, 0.84f, 0.82f, 1f) : new Color(0.91f, 0.36f, 0.40f, 1f);
            combatMessage.text = victory ? "La creatura corrotta è stata sconfitta" : "Hero01 non può più combattere";
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
            if (actionRunning || battleState == null || battleState.IsFinished)
            {
                SetAllActionButtons(false);
                return;
            }

            if (CurrentTutorialStep == TutorialStep.AwaitingFirstAttack)
            {
                SetAllActionButtons(false);
                attackButton.interactable = true;
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
            markButton.interactable = battleState.CanUsePlayerAction(BattleAction.Mark);
        }

        private void SetAllActionButtons(bool enabled)
        {
            attackButton.interactable = enabled;
            guardButton.interactable = enabled;
            techniqueButton.interactable = enabled;
            markButton.interactable = enabled;
        }

        private void UpdateStatusAndCooldown()
        {
            if (battleState == null)
            {
                return;
            }

            techniqueButtonLabel.text = battleState.TechniqueCooldownRemaining > 0
                ? "TECNICA\n" + battleState.TechniqueCooldownRemaining + " TURNI"
                : "TECNICA";

            if (battleState.IsMarkPrepared)
            {
                int bonusPercent = Mathf.RoundToInt((markDamageMultiplier - 1f) * 100f);
                statusText.text = "MARCHIO\nPROSSIMO COLPO +" + bonusPercent + "%";
            }
            else if (battleState.IsGuardPrepared)
            {
                statusText.text = "GUARDIA\nDANNO RIDOTTO";
            }
            else
            {
                statusText.text = "STATO\nPRONTO";
            }
        }

        private void UpdateHealthImmediate()
        {
            heroHealthFill.fillAmount = battleState.HeroHp / (float)battleState.HeroMaxHp;
            enemyHealthFill.fillAmount = battleState.EnemyHp / (float)battleState.EnemyMaxHp;
            heroHealthValue.text = battleState.HeroHp + " / " + battleState.HeroMaxHp;
            enemyHealthValue.text = battleState.EnemyHp + " / " + battleState.EnemyMaxHp;
        }

        private int GetObservationLengthForIntelligence()
        {
            // Il tutorial (livello 0) necessita di tre azioni uguali prima di riconoscere
            // un'abitudine. I livelli futuri possono osservare la stessa cronologia più
            // rapidamente, senza conoscere l'azione corrente del giocatore.
            return enemyIntelligenceLevel == 0 ? 3 : 2;
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

        private static IEnumerator PulseScale(GameObject effect, Vector3 baseScale, float multiplier, float duration)
        {
            effect.SetActive(true);
            float halfDuration = duration * 0.5f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(elapsed / halfDuration);
                float factor = elapsed <= halfDuration
                    ? Mathf.Lerp(1f, multiplier, normalized)
                    : Mathf.Lerp(multiplier, 1f, normalized - 1f);
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
            markVisual.transform.localScale = markBaseScale;
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
            markVisual.SetActive(battleState != null && battleState.IsMarkPrepared);
        }
    }
}
