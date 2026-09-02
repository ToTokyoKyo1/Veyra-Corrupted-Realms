#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Veyra.Combat.Tutorial;

namespace Veyra.Editor
{
    public static class Phase03TutorialValidator
    {
        private const string MainMenuScenePath = "Assets/_Veyra/Scenes/SCN_MainMenu.unity";
        private const string TutorialScenePath = "Assets/_Veyra/Scenes/SCN_W01_L01_Tutorial.unity";

        private static readonly string[] ControllerReferences =
        {
            "attackButton",
            "guardButton",
            "techniqueButton",
            "analyzeButton",
            "techniqueButtonLabel",
            "attackHighlight",
            "guardHighlight",
            "techniqueHighlight",
            "analyzeHighlight",
            "combatMessage",
            "intentText",
            "statusText",
            "heroHealthFill",
            "enemyHealthFill",
            "heroHealthValue",
            "enemyHealthValue",
            "heroActor",
            "enemyActor",
            "heroVisual",
            "enemyVisual",
            "heroProjectileOrigin",
            "heroHitTarget",
            "enemyProjectileOrigin",
            "enemyHitTarget",
            "heroBasicProjectile",
            "heroTechniqueProjectile",
            "enemyProjectile",
            "guardVisual",
            "tutorialOverlay",
            "tutorialInputBlocker",
            "tutorialStepText",
            "tutorialBodyText",
            "tutorialNextButton",
            "analyzePanel",
            "analyzeNameText",
            "analyzeRaceText",
            "analyzeCorruptionText",
            "analyzeMoodText",
            "analyzeCloseButton",
            "outcomeOverlay",
            "outcomeText",
            "outcomeMenuButton",
            "navigation"
        };

        private static readonly string[] NavigationReferences =
        {
            "backButton",
            "resultMenuButton",
            "battleController"
        };

        [MenuItem("Tools/Veyra/Tutorial/Validate First Battle Tutorial", priority = 300)]
        public static void ValidatePhase03()
        {
            List<string> errors = new List<string>();

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                errors.Add("La validazione Edit Mode non può partire durante il Play Mode.");
                CompleteValidation(errors);
                ExitBatchMode(1);
                return;
            }

            ValidateBattleModel(errors);
            ValidateBuildSettings(errors);
            ValidatePersistentTutorialScene(errors);

            CompleteValidation(errors);
            ExitBatchMode(errors.Count == 0 ? 0 : 1);
        }

        private static void ValidateBattleModel(List<string> errors)
        {
            RunAssertion(errors, "Attacco base 100 -> 80", () =>
            {
                TutorialBattleState state = new TutorialBattleState();
                BattleActionResult result = state.ResolvePlayerAction(BattleAction.Attack);

                Require(result.Accepted, "L'Attacco base è stato rifiutato.");
                Require(result.DamageDealt == 20, "L'Attacco base non infligge 20 danni.");
                Require(state.EnemyHp == 80, "Gli HP nemici attesi erano 80, trovati " + state.EnemyHp + ".");
            });

            RunAssertion(errors, "Contrattacco nemico 100 -> 75", () =>
            {
                TutorialBattleState state = new TutorialBattleState();
                BattleActionResult result = state.ResolveEnemyAttack();

                Require(result.Accepted, "Il contrattacco nemico è stato rifiutato.");
                Require(result.DamageDealt == 25, "Il contrattacco non infligge 25 danni.");
                Require(state.HeroHp == 75, "Gli HP eroe attesi erano 75, trovati " + state.HeroHp + ".");
            });

            RunAssertion(errors, "Clamp HP e Vittoria", () =>
            {
                TutorialBattleState state = new TutorialBattleState(enemyMaxHp: 7, attackDamage: 20);
                BattleActionResult result = state.ResolvePlayerAction(BattleAction.Attack);

                Require(result.Accepted, "Il colpo letale è stato rifiutato.");
                Require(result.DamageDealt == 7, "Il danno effettivo non è stato limitato agli HP residui.");
                Require(state.EnemyHp == 0, "Gli HP nemici sono scesi sotto zero o non hanno raggiunto zero.");
                Require(state.Outcome == BattleOutcome.Victory, "Gli HP nemici a zero non producono Vittoria.");
            });

            RunAssertion(errors, "Clamp HP e Sconfitta", () =>
            {
                TutorialBattleState state = new TutorialBattleState(heroMaxHp: 5);
                BattleActionResult result = state.ResolveEnemyAttack();

                Require(result.Accepted, "Il colpo letale nemico è stato rifiutato.");
                Require(result.DamageDealt == 5, "Il danno nemico effettivo non è stato limitato agli HP residui.");
                Require(state.HeroHp == 0, "Gli HP eroe sono scesi sotto zero o non hanno raggiunto zero.");
                Require(state.Outcome == BattleOutcome.Defeat, "Gli HP eroe a zero non producono Sconfitta.");
            });

            RunAssertion(errors, "Azioni rifiutate dopo l'esito", () =>
            {
                TutorialBattleState state = new TutorialBattleState(enemyMaxHp: 20);
                BattleActionResult lethal = state.ResolvePlayerAction(BattleAction.Attack);
                int heroHp = state.HeroHp;
                int enemyHp = state.EnemyHp;
                int historyCount = state.CompletedPlayerActions.Count;

                BattleActionResult latePlayerAction = state.ResolvePlayerAction(BattleAction.Guard);
                BattleActionResult lateAnalyze = state.ResolvePlayerAction(BattleAction.Analyze);
                BattleActionResult lateEnemyAction = state.ResolveEnemyAttack();

                Require(lethal.Outcome == BattleOutcome.Victory, "Lo scenario non ha raggiunto Vittoria.");
                Require(!latePlayerAction.Accepted, "Un'azione giocatore è stata accettata dopo la Vittoria.");
                Require(!lateAnalyze.Accepted, "Analizza è stato accettato dopo la Vittoria.");
                Require(!lateEnemyAction.Accepted, "Un'azione nemica è stata accettata dopo la Vittoria.");
                Require(!string.IsNullOrEmpty(latePlayerAction.RejectionReason), "Il rifiuto non espone una motivazione.");
                Require(state.HeroHp == heroHp && state.EnemyHp == enemyHp, "Gli HP cambiano dopo la fine dello scontro.");
                Require(state.CompletedPlayerActions.Count == historyCount, "La cronologia cambia dopo la fine dello scontro.");
            });

            RunAssertion(errors, "Guardia annulla un solo attacco ed è consumata", () =>
            {
                TutorialBattleState state = new TutorialBattleState(enemyAttackDamage: 12);

                BattleActionResult guard = state.ResolvePlayerAction(BattleAction.Guard);
                int historyAfterGuard = state.CompletedPlayerActions.Count;
                BattleActionResult stackedGuard = state.ResolvePlayerAction(BattleAction.Guard);
                BattleActionResult guardedHit = state.ResolveEnemyAttack();
                BattleActionResult followingHit = state.ResolveEnemyAttack();

                Require(guard.Accepted && state.CompletedPlayerActions[0] == BattleAction.Guard,
                    "La Guardia non è stata completata o registrata.");
                Require(guard.ConsumesTurn, "La Guardia deve consumare il turno del giocatore.");
                Require(!stackedGuard.Accepted && state.CompletedPlayerActions.Count == historyAfterGuard,
                    "È stato possibile accumulare più Guardie.");
                Require(guardedHit.Accepted && guardedHit.BlockedByGuard, "Il primo colpo non usa la Guardia.");
                Require(guardedHit.DamageDealt == 0, "La Guardia deve annullare completamente il prossimo danno.");
                Require(!state.IsGuardPrepared, "La Guardia non è stata consumata.");
                Require(followingHit.DamageDealt == 12 && !followingHit.BlockedByGuard,
                    "La Guardia ha bloccato più di un colpo.");
                Require(state.HeroHp == 88, "Gli HP dopo i due colpi attesi erano 88, trovati " + state.HeroHp + ".");
            });

            RunAssertion(errors, "Cooldown Tecnica", () =>
            {
                TutorialBattleState state = new TutorialBattleState(techniqueCooldownTurns: 2);
                BattleActionResult firstTechnique = state.ResolvePlayerAction(BattleAction.Technique);
                int historyAfterTechnique = state.CompletedPlayerActions.Count;
                BattleActionResult rejectedTechnique = state.ResolvePlayerAction(BattleAction.Technique);

                Require(firstTechnique.Accepted, "La prima Tecnica è stata rifiutata.");
                Require(firstTechnique.DamageDealt == 32 && state.EnemyHp == 68,
                    "La Tecnica non infligge i 32 danni configurati.");
                Require(firstTechnique.ConsumesTurn, "La Tecnica deve consumare il turno.");
                Require(state.TechniqueCooldownRemaining == 2, "Il cooldown iniziale atteso era 2.");
                Require(!rejectedTechnique.Accepted, "La Tecnica è stata accettata durante il cooldown.");
                Require(state.CompletedPlayerActions.Count == historyAfterTechnique,
                    "Una Tecnica rifiutata è stata registrata come completata.");

                state.ResolvePlayerAction(BattleAction.Attack);
                Require(state.TechniqueCooldownRemaining == 1, "Il cooldown non è sceso a 1 dopo un turno.");
                state.ResolvePlayerAction(BattleAction.Guard);
                Require(state.TechniqueCooldownRemaining == 0, "Il cooldown non è terminato dopo due turni.");
                Require(state.CanUsePlayerAction(BattleAction.Technique), "La Tecnica non torna disponibile.");
            });

            RunAssertion(errors, "Cooldown Tecnica sempre positivo", () =>
            {
                bool rejectedZeroCooldown = false;
                try
                {
                    _ = new TutorialBattleState(techniqueCooldownTurns: 0);
                }
                catch (ArgumentOutOfRangeException)
                {
                    rejectedZeroCooldown = true;
                }

                Require(rejectedZeroCooldown, "È stato accettato un cooldown Tecnica pari a zero.");
            });

            RunAssertion(errors, "Bilanciamento predefinito: Vittoria e Sconfitta raggiungibili", () =>
            {
                TutorialBattleState defeatState = new TutorialBattleState();
                defeatState.ResolvePlayerAction(BattleAction.Attack);
                defeatState.ResolveEnemyAttack();
                defeatState.ResolvePlayerAction(BattleAction.Guard);
                defeatState.ResolveEnemyAttack();
                defeatState.ResolvePlayerAction(BattleAction.Technique);
                defeatState.ResolveEnemyAttack();
                defeatState.ResolvePlayerAction(BattleAction.Attack);
                defeatState.ResolveEnemyAttack();
                defeatState.ResolvePlayerAction(BattleAction.Attack);
                defeatState.ResolveEnemyAttack();
                Require(defeatState.Outcome == BattleOutcome.Defeat,
                    "Ripetere solo Attacco dopo le azioni guidate non può produrre Sconfitta.");

                TutorialBattleState victoryState = new TutorialBattleState();
                victoryState.ResolvePlayerAction(BattleAction.Attack);
                victoryState.ResolveEnemyAttack();
                victoryState.ResolvePlayerAction(BattleAction.Guard);
                victoryState.ResolveEnemyAttack();
                victoryState.ResolvePlayerAction(BattleAction.Technique);
                victoryState.ResolveEnemyAttack();
                victoryState.ResolvePlayerAction(BattleAction.Guard);
                victoryState.ResolveEnemyAttack();
                victoryState.ResolvePlayerAction(BattleAction.Attack);
                victoryState.ResolveEnemyAttack();
                victoryState.ResolvePlayerAction(BattleAction.Technique);
                Require(victoryState.Outcome == BattleOutcome.Victory,
                    "Usare Guardia e Tecnica con criterio non può produrre Vittoria.");
            });

            RunAssertion(errors, "Analizza non consuma turno, HP o cronologia", () =>
            {
                TutorialBattleState state = new TutorialBattleState();
                state.ResolvePlayerAction(BattleAction.Technique);
                int heroHp = state.HeroHp;
                int enemyHp = state.EnemyHp;
                int cooldown = state.TechniqueCooldownRemaining;
                int historyCount = state.CompletedPlayerActions.Count;

                BattleActionResult analyze = state.ResolvePlayerAction(BattleAction.Analyze);

                Require(analyze.Accepted, "Analizza è stato rifiutato.");
                Require(!analyze.ConsumesTurn, "Analizza non deve consumare il turno del giocatore.");
                Require(analyze.DamageDealt == 0, "Analizza non deve infliggere danni.");
                Require(state.HeroHp == heroHp && state.EnemyHp == enemyHp,
                    "Analizza ha modificato gli HP.");
                Require(state.TechniqueCooldownRemaining == cooldown,
                    "Analizza ha fatto avanzare il cooldown della Tecnica.");
                Require(state.CompletedPlayerActions.Count == historyCount,
                    "Analizza è entrato nella cronologia osservata dal nemico.");
            });

            RunAssertion(errors, "Corruzione limitata tra 0 e 100", () =>
            {
                Require(TutorialBattleController.ClampCorruptionPercent(-25) == 0,
                    "Una corruzione negativa non viene limitata a 0.");
                Require(TutorialBattleController.ClampCorruptionPercent(70) == 70,
                    "Una corruzione valida viene modificata.");
                Require(TutorialBattleController.ClampCorruptionPercent(140) == 100,
                    "Una corruzione superiore a 100 non viene limitata a 100.");
            });

            RunAssertion(errors, "Cronologia completata e pattern", () =>
            {
                TutorialBattleState state = new TutorialBattleState(
                    attackDamage: 1,
                    techniqueDamage: 2,
                    historyCapacity: 3,
                    repeatedPatternLength: 2);

                BattleActionResult invalid = state.ResolvePlayerAction((BattleAction)999);
                Require(!invalid.Accepted && state.CompletedPlayerActions.Count == 0,
                    "Un'azione sconosciuta è stata registrata come completata.");

                state.ResolvePlayerAction(BattleAction.Guard);
                state.ResolvePlayerAction(BattleAction.Attack);
                state.ResolvePlayerAction(BattleAction.Attack);

                Require(state.CompletedPlayerActions.Count == 3, "La cronologia non contiene tre azioni completate.");
                Require(state.LastCompletedPlayerAction == BattleAction.Attack, "L'ultima azione completata non è Attacco.");
                Require(state.HasRepeatedPlayerPattern, "Due Attacchi consecutivi non producono un pattern.");
                Require(state.TryGetRepeatedPlayerAction(out BattleAction repeated) && repeated == BattleAction.Attack,
                    "Il pattern non restituisce l'azione Attacco.");

                state.ResolvePlayerAction(BattleAction.Analyze);
                Require(state.CompletedPlayerActions.Count == 3, "La cronologia supera la capacità configurata.");
                Require(state.CompletedPlayerActions[0] == BattleAction.Guard,
                    "Analizza ha modificato la cronologia conservata.");
                Require(state.HasRepeatedPlayerPattern,
                    "Analizza non deve alterare il pattern di azioni di combattimento già completate.");

                state.ResolvePlayerAction(BattleAction.Technique);
                Require(state.CompletedPlayerActions.Count == 3, "La cronologia supera la capacità configurata.");
                Require(state.CompletedPlayerActions[0] == BattleAction.Attack,
                    "La cronologia non rimuove correttamente l'azione più vecchia.");
                Require(!state.HasRepeatedPlayerPattern, "Il pattern resta attivo dopo un'azione differente.");
            });
        }

        private static void ValidateBuildSettings(List<string> errors)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            if (scenes.Length < 2)
            {
                errors.Add("Le Build Settings devono contenere almeno Menu principale e Tutorial.");
                return;
            }

            if (!scenes[0].enabled || scenes[0].path != MainMenuScenePath)
            {
                errors.Add("SCN_MainMenu deve essere la scena abilitata all'indice 0.");
            }

            if (!scenes[1].enabled || scenes[1].path != TutorialScenePath)
            {
                errors.Add("SCN_W01_L01_Tutorial deve essere la scena abilitata all'indice 1.");
            }
        }

        private static void ValidatePersistentTutorialScene(List<string> errors)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TutorialScenePath) == null)
            {
                errors.Add("Scena tutorial persistente mancante: " + TutorialScenePath);
                return;
            }

            Scene activeSceneBefore = SceneManager.GetActiveScene();
            Scene tutorialScene = SceneManager.GetSceneByPath(TutorialScenePath);
            bool wasAlreadyLoaded = tutorialScene.IsValid() && tutorialScene.isLoaded;

            if (wasAlreadyLoaded && tutorialScene.isDirty)
            {
                errors.Add(
                    "SCN_W01_L01_Tutorial contiene modifiche non salvate: la scena è stata preservata e non validata.");
                return;
            }

            try
            {
                if (!wasAlreadyLoaded)
                {
                    tutorialScene = EditorSceneManager.OpenScene(TutorialScenePath, OpenSceneMode.Additive);
                }

                ValidateTutorialSceneContents(tutorialScene, errors);
            }
            catch (Exception exception)
            {
                errors.Add("Impossibile validare la scena tutorial: " + exception.Message);
            }
            finally
            {
                if (!wasAlreadyLoaded && tutorialScene.IsValid() && tutorialScene.isLoaded)
                {
                    if (activeSceneBefore.IsValid() && activeSceneBefore.isLoaded)
                    {
                        SceneManager.SetActiveScene(activeSceneBefore);
                    }

                    EditorSceneManager.CloseScene(tutorialScene, true);
                }
            }
        }

        private static void ValidateTutorialSceneContents(Scene scene, List<string> errors)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            GameObject sceneRoot = RequireUniqueRoot(roots, "SCN_W01_L01_Tutorial", errors);
            GameObject battleRoot = RequireUniqueDirectChild(sceneRoot, "TutorialBattleRoot", errors);
            GameObject uiRoot = RequireUniqueDirectChild(sceneRoot, "TutorialUIRoot", errors);

            TutorialBattleController controller = RequireSingleComponent<TutorialBattleController>(scene, errors);
            TutorialBattleNavigation navigation = RequireSingleComponent<TutorialBattleNavigation>(scene, errors);
            RequireSingleComponent<EventSystem>(scene, errors);
            Canvas canvas = RequireSingleComponent<Canvas>(scene, errors);

            if (controller == null || navigation == null)
            {
                return;
            }

            ValidateRequiredReferences(controller, ControllerReferences, errors);
            ValidateRequiredReferences(navigation, NavigationReferences, errors);
            ValidateSceneTuning(controller, errors);

            SerializedObject controllerSerialized = new SerializedObject(controller);
            SerializedObject navigationSerialized = new SerializedObject(navigation);

            Transform heroActor = GetReference<Transform>(controllerSerialized, "heroActor", errors);
            Transform enemyActor = GetReference<Transform>(controllerSerialized, "enemyActor", errors);
            Image heroHealthFill = GetReference<Image>(controllerSerialized, "heroHealthFill", errors);
            Image enemyHealthFill = GetReference<Image>(controllerSerialized, "enemyHealthFill", errors);
            TMP_Text heroHealthValue = GetReference<TMP_Text>(controllerSerialized, "heroHealthValue", errors);
            TMP_Text enemyHealthValue = GetReference<TMP_Text>(controllerSerialized, "enemyHealthValue", errors);
            GameObject tutorialOverlay = GetReference<GameObject>(controllerSerialized, "tutorialOverlay", errors);
            GameObject outcomeOverlay = GetReference<GameObject>(controllerSerialized, "outcomeOverlay", errors);
            Image tutorialInputBlocker = GetReference<Image>(controllerSerialized, "tutorialInputBlocker", errors);
            TMP_Text tutorialStepText = GetReference<TMP_Text>(controllerSerialized, "tutorialStepText", errors);
            Button analyzeButton = GetReference<Button>(controllerSerialized, "analyzeButton", errors);
            GameObject attackHighlight = GetReference<GameObject>(controllerSerialized, "attackHighlight", errors);
            GameObject guardHighlight = GetReference<GameObject>(controllerSerialized, "guardHighlight", errors);
            GameObject techniqueHighlight = GetReference<GameObject>(controllerSerialized, "techniqueHighlight", errors);
            GameObject analyzeHighlight = GetReference<GameObject>(controllerSerialized, "analyzeHighlight", errors);
            GameObject analyzePanel = GetReference<GameObject>(controllerSerialized, "analyzePanel", errors);
            TMP_Text analyzeNameText = GetReference<TMP_Text>(controllerSerialized, "analyzeNameText", errors);
            TMP_Text analyzeRaceText = GetReference<TMP_Text>(controllerSerialized, "analyzeRaceText", errors);
            TMP_Text analyzeCorruptionText = GetReference<TMP_Text>(controllerSerialized, "analyzeCorruptionText", errors);
            TMP_Text analyzeMoodText = GetReference<TMP_Text>(controllerSerialized, "analyzeMoodText", errors);
            Button analyzeCloseButton = GetReference<Button>(controllerSerialized, "analyzeCloseButton", errors);

            if (battleRoot != null && !IsSelfOrChild(controller.transform, battleRoot.transform))
            {
                errors.Add("TutorialBattleController deve appartenere a TutorialBattleRoot.");
            }

            if (uiRoot != null && !IsSelfOrChild(navigation.transform, uiRoot.transform))
            {
                errors.Add("TutorialBattleNavigation deve appartenere a TutorialUIRoot.");
            }

            if (uiRoot != null && canvas != null && !IsSelfOrChild(canvas.transform, uiRoot.transform))
            {
                errors.Add("Il Canvas deve appartenere a TutorialUIRoot.");
            }

            ValidateActorAlignment(heroActor, enemyActor, battleRoot, errors);
            ValidateHealthUi(heroHealthFill, enemyHealthFill, heroHealthValue, enemyHealthValue, uiRoot, errors);
            ValidateOverlays(tutorialOverlay, outcomeOverlay, tutorialInputBlocker, uiRoot, errors);
            ValidateActionTutorialUi(
                analyzeButton,
                attackHighlight,
                guardHighlight,
                techniqueHighlight,
                analyzeHighlight,
                tutorialStepText,
                uiRoot,
                errors);
            ValidateAnalyzePanel(
                analyzePanel,
                analyzeNameText,
                analyzeRaceText,
                analyzeCorruptionText,
                analyzeMoodText,
                analyzeCloseButton,
                uiRoot,
                errors);

            SerializedProperty linkedController = navigationSerialized.FindProperty("battleController");
            if (linkedController != null && linkedController.objectReferenceValue != controller)
            {
                errors.Add("TutorialBattleNavigation.battleController non punta all'unico controller della scena.");
            }

            SerializedProperty linkedNavigation = controllerSerialized.FindProperty("navigation");
            if (linkedNavigation != null && linkedNavigation.objectReferenceValue != navigation)
            {
                errors.Add("TutorialBattleController.navigation non punta all'unica navigazione della scena.");
            }
        }

        private static void ValidateSceneTuning(TutorialBattleController controller, List<string> errors)
        {
            SerializedObject serialized = new SerializedObject(controller);
            ValidateIntProperty(serialized, "heroMaxHp", 100, errors);
            ValidateIntProperty(serialized, "enemyMaxHp", 100, errors);
            ValidateIntProperty(serialized, "attackDamage", 20, errors);
            ValidateIntProperty(serialized, "techniqueDamage", 32, errors);
            ValidateIntProperty(serialized, "enemyAttackDamage", 25, errors);
            ValidateIntProperty(serialized, "techniqueCooldownTurns", 2, errors);
            ValidateIntProperty(serialized, "enemyIntelligenceLevel", 0, errors);
            ValidateIntProperty(serialized, "enemyCorruptionPercent", 70, errors);
            ValidateFloatProperty(serialized, "resultReturnDelay", 2.5f, errors);
            ValidateStringProperty(serialized, "enemyDisplayName", "Creatura Corrotta", errors);
            ValidateStringProperty(serialized, "enemyRace", "Creatura delle Radici", errors);
            ValidateEnumIndex(serialized, "enemyMood", 2, errors);
        }

        private static void ValidateActorAlignment(
            Transform heroActor,
            Transform enemyActor,
            GameObject battleRoot,
            List<string> errors)
        {
            if (heroActor == null || enemyActor == null)
            {
                return;
            }

            if (battleRoot != null &&
                (!IsSelfOrChild(heroActor, battleRoot.transform) || !IsSelfOrChild(enemyActor, battleRoot.transform)))
            {
                errors.Add("Eroe e nemico devono appartenere a TutorialBattleRoot.");
            }

            if (heroActor.position.x >= enemyActor.position.x)
            {
                errors.Add("L'eroe deve essere a sinistra del nemico.");
            }

            float verticalDifference = Mathf.Abs(heroActor.position.y - enemyActor.position.y);
            if (verticalDifference > 0.05f)
            {
                errors.Add(
                    "Eroe e nemico devono essere sulla stessa linea: differenza Y " +
                    verticalDifference.ToString("0.###") + " (massimo 0.05).");
            }
        }

        private static void ValidateHealthUi(
            Image heroFill,
            Image enemyFill,
            TMP_Text heroValue,
            TMP_Text enemyValue,
            GameObject uiRoot,
            List<string> errors)
        {
            ValidateHealthFill(heroFill, "Hero HP", uiRoot, errors);
            ValidateHealthFill(enemyFill, "Enemy HP", uiRoot, errors);
            ValidateHealthText(heroValue, "Hero HP", uiRoot, errors);
            ValidateHealthText(enemyValue, "Enemy HP", uiRoot, errors);
        }

        private static void ValidateHealthFill(Image fill, string label, GameObject uiRoot, List<string> errors)
        {
            if (fill == null)
            {
                return;
            }

            if (uiRoot != null && !IsSelfOrChild(fill.transform, uiRoot.transform))
            {
                errors.Add(label + " Fill deve appartenere a TutorialUIRoot.");
            }

            if (fill.type != Image.Type.Filled)
            {
                errors.Add(label + " Fill deve usare Image.Type.Filled.");
            }

            if (!Mathf.Approximately(fill.fillAmount, 1f))
            {
                errors.Add(label + " Fill deve iniziare pieno.");
            }
        }

        private static void ValidateHealthText(TMP_Text text, string label, GameObject uiRoot, List<string> errors)
        {
            if (text == null)
            {
                return;
            }

            if (uiRoot != null && !IsSelfOrChild(text.transform, uiRoot.transform))
            {
                errors.Add(label + " Value deve appartenere a TutorialUIRoot.");
            }

            if (!string.Equals(text.text.Trim(), "100 / 100", StringComparison.Ordinal))
            {
                errors.Add(label + " Value deve iniziare con '100 / 100'.");
            }
        }

        private static void ValidateOverlays(
            GameObject tutorialOverlay,
            GameObject outcomeOverlay,
            Image tutorialInputBlocker,
            GameObject uiRoot,
            List<string> errors)
        {
            if (tutorialOverlay != null)
            {
                if (uiRoot != null && !IsSelfOrChild(tutorialOverlay.transform, uiRoot.transform))
                {
                    errors.Add("TutorialOverlay deve appartenere a TutorialUIRoot.");
                }

                if (!tutorialOverlay.activeSelf)
                {
                    errors.Add("TutorialOverlay deve essere persistente e inizialmente attivo.");
                }
            }

            if (outcomeOverlay != null)
            {
                if (uiRoot != null && !IsSelfOrChild(outcomeOverlay.transform, uiRoot.transform))
                {
                    errors.Add("OutcomeOverlay deve appartenere a TutorialUIRoot.");
                }

                if (outcomeOverlay.activeSelf)
                {
                    errors.Add("OutcomeOverlay deve essere inizialmente inattivo.");
                }
            }

            if (tutorialInputBlocker != null && !tutorialInputBlocker.raycastTarget)
            {
                errors.Add("TutorialInputBlocker deve bloccare inizialmente i raycast.");
            }
        }

        private static void ValidateActionTutorialUi(
            Button analyzeButton,
            GameObject attackHighlight,
            GameObject guardHighlight,
            GameObject techniqueHighlight,
            GameObject analyzeHighlight,
            TMP_Text tutorialStepText,
            GameObject uiRoot,
            List<string> errors)
        {
            if (analyzeButton != null)
            {
                if (uiRoot != null && !IsSelfOrChild(analyzeButton.transform, uiRoot.transform))
                {
                    errors.Add("BTN_Analyze deve appartenere a TutorialUIRoot.");
                }

                if (analyzeButton.name != "BTN_Analyze")
                {
                    errors.Add("Il quarto comando deve chiamarsi BTN_Analyze.");
                }

                TMP_Text label = analyzeButton.GetComponentInChildren<TMP_Text>(true);
                if (label == null || !string.Equals(label.text.Trim(), "ANALIZZA", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add("Il quarto comando deve mostrare il testo ANALIZZA.");
                }
            }

            ValidateActionHighlight(attackHighlight, "AttackHighlight", uiRoot, errors);
            ValidateActionHighlight(guardHighlight, "GuardHighlight", uiRoot, errors);
            ValidateActionHighlight(techniqueHighlight, "TechniqueHighlight", uiRoot, errors);
            ValidateActionHighlight(analyzeHighlight, "AnalyzeHighlight", uiRoot, errors);

            if (tutorialStepText != null &&
                !string.Equals(tutorialStepText.text.Trim(), "PASSO 1 / 10", StringComparison.Ordinal))
            {
                errors.Add("Il tutorial persistente deve iniziare con PASSO 1 / 10.");
            }

            if (uiRoot != null)
            {
                TMP_Text obsoleteMarkText = uiRoot.GetComponentsInChildren<TMP_Text>(true)
                    .FirstOrDefault(text => text.text.IndexOf("MARCHIO", StringComparison.OrdinalIgnoreCase) >= 0);
                if (obsoleteMarkText != null)
                {
                    errors.Add("La UI contiene ancora il testo obsoleto MARCHIO in " + obsoleteMarkText.name + ".");
                }
            }
        }

        private static void ValidateActionHighlight(
            GameObject highlight,
            string expectedName,
            GameObject uiRoot,
            List<string> errors)
        {
            if (highlight == null)
            {
                return;
            }

            if (highlight.name != expectedName)
            {
                errors.Add("Highlight azione atteso " + expectedName + ", trovato " + highlight.name + ".");
            }

            if (uiRoot != null && !IsSelfOrChild(highlight.transform, uiRoot.transform))
            {
                errors.Add(expectedName + " deve appartenere a TutorialUIRoot.");
            }

            if (highlight.activeSelf)
            {
                errors.Add(expectedName + " deve iniziare inattivo.");
            }

            Image image = highlight.GetComponent<Image>();
            if (image == null || image.raycastTarget)
            {
                errors.Add(expectedName + " deve avere un'Image che non intercetta i raycast.");
            }
        }

        private static void ValidateAnalyzePanel(
            GameObject analyzePanel,
            TMP_Text nameText,
            TMP_Text raceText,
            TMP_Text corruptionText,
            TMP_Text moodText,
            Button closeButton,
            GameObject uiRoot,
            List<string> errors)
        {
            if (analyzePanel == null)
            {
                return;
            }

            if (uiRoot != null && !IsSelfOrChild(analyzePanel.transform, uiRoot.transform))
            {
                errors.Add("AnalyzePanel deve appartenere a TutorialUIRoot.");
            }

            if (analyzePanel.activeSelf)
            {
                errors.Add("AnalyzePanel deve essere persistente ma inizialmente inattivo.");
            }

            ValidateAnalyzeText(nameText, analyzePanel, "NOME", "Creatura Corrotta", errors);
            ValidateAnalyzeText(raceText, analyzePanel, "RAZZA", "Creatura delle Radici", errors);
            ValidateAnalyzeText(corruptionText, analyzePanel, "CORRUZIONE", "70%", errors);
            ValidateAnalyzeText(moodText, analyzePanel, "STATO ATTUALE", "Arrabbiato", errors);

            if (closeButton != null)
            {
                if (!IsSelfOrChild(closeButton.transform, analyzePanel.transform))
                {
                    errors.Add("Il pulsante CHIUDI deve appartenere ad AnalyzePanel.");
                }

                TMP_Text closeLabel = closeButton.GetComponentInChildren<TMP_Text>(true);
                if (closeLabel == null ||
                    !string.Equals(closeLabel.text.Trim(), "CHIUDI", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add("AnalyzePanel deve contenere un pulsante CHIUDI leggibile.");
                }
            }
        }

        private static void ValidateAnalyzeText(
            TMP_Text text,
            GameObject analyzePanel,
            string requiredLabel,
            string requiredValue,
            List<string> errors)
        {
            if (text == null)
            {
                return;
            }

            if (!IsSelfOrChild(text.transform, analyzePanel.transform))
            {
                errors.Add(text.name + " deve appartenere ad AnalyzePanel.");
            }

            if (text.text.IndexOf(requiredLabel, StringComparison.OrdinalIgnoreCase) < 0 ||
                text.text.IndexOf(requiredValue, StringComparison.OrdinalIgnoreCase) < 0)
            {
                errors.Add(
                    text.name + " deve mostrare " + requiredLabel + " e il valore iniziale " + requiredValue + ".");
            }
        }

        private static GameObject RequireUniqueRoot(
            IEnumerable<GameObject> roots,
            string rootName,
            List<string> errors)
        {
            GameObject[] matches = roots.Where(root => root.name == rootName).ToArray();
            if (matches.Length != 1)
            {
                errors.Add("La scena deve contenere esattamente un root " + rootName + ".");
                return matches.FirstOrDefault();
            }

            return matches[0];
        }

        private static GameObject RequireUniqueDirectChild(
            GameObject parent,
            string childName,
            List<string> errors)
        {
            if (parent == null)
            {
                return null;
            }

            GameObject[] matches = Enumerable.Range(0, parent.transform.childCount)
                .Select(index => parent.transform.GetChild(index).gameObject)
                .Where(child => child.name == childName)
                .ToArray();

            if (matches.Length != 1)
            {
                errors.Add(
                    parent.name + " deve contenere esattamente un figlio diretto " + childName + ".");
                return matches.FirstOrDefault();
            }

            return matches[0];
        }

        private static T RequireSingleComponent<T>(Scene scene, List<string> errors) where T : Component
        {
            T[] components = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();

            if (components.Length != 1)
            {
                errors.Add(
                    "La scena deve contenere esattamente un " + typeof(T).Name +
                    "; trovati " + components.Length + ".");
                return components.FirstOrDefault();
            }

            return components[0];
        }

        private static void ValidateRequiredReferences(
            MonoBehaviour component,
            IEnumerable<string> propertyNames,
            List<string> errors)
        {
            SerializedObject serialized = new SerializedObject(component);
            foreach (string propertyName in propertyNames)
            {
                SerializedProperty property = serialized.FindProperty(propertyName);
                if (property == null)
                {
                    errors.Add(component.GetType().Name + "." + propertyName + " non esiste.");
                }
                else if (property.propertyType != SerializedPropertyType.ObjectReference ||
                         property.objectReferenceValue == null)
                {
                    errors.Add(component.GetType().Name + "." + propertyName + " non è assegnato.");
                }
            }
        }

        private static T GetReference<T>(
            SerializedObject serialized,
            string propertyName,
            List<string> errors) where T : UnityEngine.Object
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            T value = property == null ? null : property.objectReferenceValue as T;
            if (value == null)
            {
                errors.Add(serialized.targetObject.GetType().Name + "." + propertyName +
                           " deve riferire un " + typeof(T).Name + ".");
            }

            return value;
        }

        private static void ValidateIntProperty(
            SerializedObject serialized,
            string propertyName,
            int expected,
            List<string> errors)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.Integer)
            {
                errors.Add(serialized.targetObject.GetType().Name + "." + propertyName + " non è disponibile.");
                return;
            }

            if (property.intValue != expected)
            {
                errors.Add(
                    serialized.targetObject.GetType().Name + "." + propertyName +
                    " deve valere " + expected + ", trovato " + property.intValue + ".");
            }
        }

        private static void ValidateStringProperty(
            SerializedObject serialized,
            string propertyName,
            string expected,
            List<string> errors)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.String)
            {
                errors.Add(serialized.targetObject.GetType().Name + "." + propertyName + " non è disponibile.");
                return;
            }

            if (!string.Equals(property.stringValue, expected, StringComparison.Ordinal))
            {
                errors.Add(
                    serialized.targetObject.GetType().Name + "." + propertyName +
                    " deve valere '" + expected + "', trovato '" + property.stringValue + "'.");
            }
        }

        private static void ValidateFloatProperty(
            SerializedObject serialized,
            string propertyName,
            float expected,
            List<string> errors)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.Float)
            {
                errors.Add(serialized.targetObject.GetType().Name + "." + propertyName + " non è disponibile.");
                return;
            }

            if (!Mathf.Approximately(property.floatValue, expected))
            {
                errors.Add(
                    serialized.targetObject.GetType().Name + "." + propertyName +
                    " deve valere " + expected + ", trovato " + property.floatValue + ".");
            }
        }

        private static void ValidateEnumIndex(
            SerializedObject serialized,
            string propertyName,
            int expectedIndex,
            List<string> errors)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.Enum)
            {
                errors.Add(serialized.targetObject.GetType().Name + "." + propertyName + " non è disponibile.");
                return;
            }

            if (property.enumValueIndex != expectedIndex)
            {
                errors.Add(
                    serialized.targetObject.GetType().Name + "." + propertyName +
                    " deve usare il terzo valore enum (Arrabbiato).");
            }
        }

        private static bool IsSelfOrChild(Transform candidate, Transform expectedRoot)
        {
            return candidate == expectedRoot || candidate.IsChildOf(expectedRoot);
        }

        private static void RunAssertion(List<string> errors, string label, Action assertion)
        {
            try
            {
                assertion();
            }
            catch (Exception exception)
            {
                errors.Add(label + ": " + exception.Message);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static void CompleteValidation(IReadOnlyCollection<string> errors)
        {
            if (errors.Count == 0)
            {
                Debug.Log(
                    "[Veyra Tutorial Validation] Edit Mode superata: modello, Build Settings e scena persistente conformi.");
                return;
            }

            Debug.LogError(
                "[Veyra Tutorial Validation] Edit Mode fallita:\n- " +
                string.Join("\n- ", errors));
        }

        private static void ExitBatchMode(int exitCode)
        {
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
        }
    }
}
#endif
