#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Veyra.Combat.Encounter;
using Veyra.Core;

namespace Veyra.Editor
{
    public static class Phase046CampaignValidator
    {
        private const string MenuPath = "Tools/Veyra/Campaign/Validate Phases 04-06";

        private static readonly string[] ControllerReferences =
        {
            "attackButton",
            "guardButton",
            "techniqueButton",
            "analyzeButton",
            "techniqueButtonLabel",
            "combatMessage",
            "intentText",
            "statusText",
            "predictionFeedbackText",
            "heroHealthFill",
            "enemyHealthFill",
            "heroHealthValue",
            "enemyHealthValue",
            "enemyDialogueRoot",
            "enemyDialogueText",
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
            "heroGuardVisual",
            "enemyGuardVisual",
            "enemyChargeVisual",
            "savedVisual",
            "killedVisual",
            "analyzePanel",
            "analyzeNameText",
            "analyzeRaceText",
            "analyzeCorruptionText",
            "analyzeMoodText",
            "analyzeTendencyText",
            "analyzeIntentText",
            "analyzeCloseButton",
            "finalChoicePanel",
            "finalChoiceTitleText",
            "finalChoicePortrait",
            "finalChoiceDialogueText",
            "saveButton",
            "killButton",
            "confirmationPanel",
            "confirmationText",
            "confirmationConfirmButton",
            "confirmationBackButton",
            "outcomeOverlay",
            "outcomeText",
            "outcomeDialogueText",
            "outcomeMenuButton",
            "navigation"
        };

        private static readonly string[] NavigationReferences =
        {
            "backButton",
            "resultMenuButton",
            "battleController"
        };

        private static readonly string[] InitiallyInactiveControllerObjects =
        {
            "heroBasicProjectile",
            "heroTechniqueProjectile",
            "enemyProjectile",
            "heroGuardVisual",
            "enemyGuardVisual",
            "enemyChargeVisual",
            "savedVisual",
            "killedVisual",
            "analyzePanel",
            "finalChoicePanel",
            "confirmationPanel",
            "outcomeOverlay"
        };

        private static readonly string[] RequiredDialogueProperties =
        {
            "openingDialogue",
            "attackReactionDialogue",
            "guardReactionDialogue",
            "techniqueReactionDialogue",
            "firstAnalyzeDialogue",
            "repeatedAnalyzeDialogue",
            "lowHpDialogue",
            "attackPatternDialogue",
            "guardPatternDialogue",
            "techniquePatternDialogue",
            "strategyChangedDialogue",
            "defeatedDialogue",
            "savedDialogue",
            "killedDialogue"
        };

        private static readonly EncounterSceneExpectation ThornGuardian =
            new EncounterSceneExpectation(
                Phase046EncounterSceneFactory.Level02ScenePath,
                SceneNames.World01Level02ThornGuardian,
                "ThornGuardian",
                "world01_encounter02_thorn_guardian",
                "Custode del Rovo",
                "Custode Silvano",
                58,
                "Triste",
                1,
                2403,
                115,
                22,
                40);

        private static readonly EncounterSceneExpectation AshWatcher =
            new EncounterSceneExpectation(
                Phase046EncounterSceneFactory.Level03ScenePath,
                SceneNames.World01Level03AshWatcher,
                "AshWatcher",
                "world01_encounter03_ash_watcher",
                "Vigile delle Ceneri",
                "Umano Mutato",
                82,
                "Arrabbiato",
                2,
                3503,
                130,
                24,
                44);

        [MenuItem(MenuPath, priority = 410)]
        public static void ValidatePhases04To06()
        {
            List<string> errors = new List<string>();

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                errors.Add("La validazione delle Fasi 04-06 può essere eseguita soltanto in Edit Mode.");
                CompleteValidation(errors);
                ExitBatchMode(1);
                return;
            }

            ValidateEncounterModel(errors);
            ValidateCampaignProgression(errors);
            ValidateBuildSettings(errors);
            ValidatePersistentEncounterScene(ThornGuardian, errors);
            ValidatePersistentEncounterScene(AshWatcher, errors);

            CompleteValidation(errors);
            ExitBatchMode(errors.Count == 0 ? 0 : 1);
        }

        private static void ValidateEncounterModel(List<string> errors)
        {
            RunAssertion(errors, "Analizza non consuma il turno", () =>
            {
                EncounterBattleState state = CreateState(
                    enemyMaxHp: 100,
                    corruption: 58,
                    initialMood: EnemyMood.Triste,
                    intelligence: 1);

                EncounterActionResult technique = state.ResolvePlayerAction(EncounterAction.Technique);
                Require(technique.Accepted && technique.ConsumesTurn, "La Tecnica iniziale è stata rifiutata.");

                int heroHp = state.HeroHp;
                int enemyHp = state.EnemyHp;
                int cooldown = state.TechniqueCooldownRemaining;
                int historyCount = state.Memory.CompletedActions.Count;
                int completedCount = state.Memory.CompletedActionCount;

                EncounterActionResult analyze = state.ResolvePlayerAction(EncounterAction.Analyze);
                Require(analyze.Accepted, "Analizza è stato rifiutato.");
                Require(!analyze.ConsumesTurn, "Analizza non deve consumare il turno.");
                Require(analyze.DamageDealt == 0, "Analizza non deve infliggere danni.");
                Require(state.HeroHp == heroHp && state.EnemyHp == enemyHp,
                    "Analizza ha modificato gli HP.");
                Require(state.TechniqueCooldownRemaining == cooldown,
                    "Analizza ha modificato il cooldown della Tecnica.");
                Require(state.Memory.CompletedActions.Count == historyCount &&
                        state.Memory.CompletedActionCount == completedCount,
                    "Analizza è entrato nella cronologia di combattimento.");
                Require(state.Memory.AnalysisCount == 1,
                    "Analizza non è stato registrato come evento percettivo separato.");
            });

            RunAssertion(errors, "Guardie e cooldown", () =>
            {
                EncounterBattleState state = CreateState(100, 58, EnemyMood.Triste, 1);
                EnemyIntentResult enemyGuard = state.ResolveEnemyIntent(EnemyIntent.Guard);
                EncounterActionResult attack = state.ResolvePlayerAction(EncounterAction.Attack);
                Require(enemyGuard.Accepted && enemyGuard.PreparedGuard,
                    "La Guardia nemica non è stata preparata.");
                Require(attack.EnemyGuardReducedDamage && attack.DamageDealt == 0,
                    "La Guardia nemica deve bloccare il primo colpo parabile e applicare 0 danni.");
                Require(!state.IsEnemyGuardPrepared,
                    "La Guardia nemica deve consumarsi al primo danno.");

                EncounterActionResult heroGuard = state.ResolvePlayerAction(EncounterAction.Guard);
                EnemyIntentResult enemyAttack = state.ResolveEnemyIntent(EnemyIntent.Attack);
                Require(heroGuard.Accepted && enemyAttack.BlockedByGuard,
                    "La Guardia dell'eroe non ha bloccato il colpo successivo.");
                Require(enemyAttack.DamageDealt == 0,
                    "Un attacco bloccato deve infliggere zero danni.");

                EncounterActionResult technique = state.ResolvePlayerAction(EncounterAction.Technique);
                Require(technique.Accepted && state.TechniqueCooldownRemaining == 2,
                    "La Tecnica deve avviare un cooldown di due azioni.");
                Require(!state.CanUsePlayerAction(EncounterAction.Technique),
                    "La Tecnica non deve essere riutilizzabile durante il cooldown.");
            });

            RunAssertion(errors, "Bilanciamento deterministico del Custode", () =>
            {
                EncounterAction[] recklessActions =
                {
                    EncounterAction.Attack,
                    EncounterAction.Attack,
                    EncounterAction.Attack,
                    EncounterAction.Attack,
                    EncounterAction.Attack,
                    EncounterAction.Attack,
                    EncounterAction.Attack
                };
                EncounterBattleState recklessState = SimulateThornGuardian(
                    ThornGuardian.RandomSeed,
                    recklessActions);
                Require(recklessState.Resolution == NarrativeOutcome.HeroDefeated &&
                        recklessState.EnemyHp == 1,
                    "Ripetere soltanto Attacco deve poter produrre una sconfitta chiara.");

                EncounterAction[] tacticalActions =
                {
                    EncounterAction.Technique,
                    EncounterAction.Attack,
                    EncounterAction.Guard,
                    EncounterAction.Technique,
                    EncounterAction.Attack,
                    EncounterAction.Attack
                };
                EncounterBattleState tacticalState = SimulateThornGuardian(
                    ThornGuardian.RandomSeed,
                    tacticalActions);
                Require(tacticalState.EnemyDefeated &&
                        tacticalState.Resolution == NarrativeOutcome.None &&
                        tacticalState.HeroHp == 34,
                    "Leggere il Colpo caricato e alternare Guardia e Tecnica deve vincere il Livello 2.");
            });

            RunAssertion(errors, "Intenzione bloccata prima dell'input", () =>
            {
                EnemyMemory memory = CreateRepeatedMemory(EncounterAction.Attack, 3);
                EncounterBattleState state = CreateState(115, 82, EnemyMood.Arrabbiato, 2, memory);
                AdaptiveEnemyBrain brain = new AdaptiveEnemyBrain(2, 1977);
                EnemyDecisionContext context = EnemyDecisionContext.From(state);

                EnemyIntent first = brain.PlanAndLockIntent(memory, context);
                LearnedPattern patternBeforeInput = brain.LastDecision.Pattern;
                memory.RecordCompletedAction(EncounterAction.Guard);
                EnemyIntent afterInput = brain.PlanAndLockIntent(memory, context);

                Require(brain.HasLockedIntent, "L'intenzione pianificata non risulta bloccata.");
                Require(first == afterInput && brain.LockedIntent.Value == first,
                    "Il nemico ha cambiato l'intenzione dopo l'input del giocatore.");
                Require(brain.LastDecision.Pattern == patternBeforeInput,
                    "La decisione bloccata è stata ricalcolata durante il turno corrente.");

                brain.CompleteLockedIntent();
                Require(!brain.HasLockedIntent,
                    "L'intenzione completata deve essere sbloccata per il turno seguente.");
            });

            RunAssertion(errors, "Carica seguita dal Colpo caricato", () =>
            {
                EncounterBattleState state = CreateState(115, 58, EnemyMood.Triste, 1);
                EnemyIntentResult charge = state.ResolveEnemyIntent(EnemyIntent.Charge);
                Require(charge.Accepted && charge.BeganCharge && state.IsChargedStrikePrepared,
                    "Charge non ha preparato il Colpo caricato.");

                EnemyIntentResult illegalAttack = state.ResolveEnemyIntent(EnemyIntent.Attack);
                Require(!illegalAttack.Accepted,
                    "Dopo Charge il nemico non deve poter sostituire il colpo annunciato con Attack.");

                EnemyIntentResult strike = state.ResolveEnemyIntent(EnemyIntent.ChargedStrike);
                Require(strike.Accepted && strike.DamageDealt == 40,
                    "ChargedStrike non ha risolto il danno configurato.");
                Require(!state.IsChargedStrikePrepared,
                    "La carica deve consumarsi dopo ChargedStrike.");
            });

            RunAssertion(errors, "Memoria di sei azioni e Analizza separato", () =>
            {
                EnemyMemory memory = new EnemyMemory(2, 6);
                EncounterAction[] actions =
                {
                    EncounterAction.Attack,
                    EncounterAction.Guard,
                    EncounterAction.Technique,
                    EncounterAction.Attack,
                    EncounterAction.Guard,
                    EncounterAction.Technique,
                    EncounterAction.Attack
                };

                foreach (EncounterAction action in actions)
                {
                    memory.RecordCompletedAction(action);
                }

                memory.RecordAnalyze();
                Require(memory.CompletedActions.Count == 6,
                    "La memoria non conserva esattamente le ultime sei azioni.");
                Require(memory.CompletedActionCount == 7,
                    "Il contatore totale delle azioni completate non è corretto.");
                Require(memory.CompletedActions[0] == EncounterAction.Guard,
                    "La memoria non ha rimosso l'azione più vecchia.");
                Require(memory.CompletedActions.All(action => action != EncounterAction.Analyze),
                    "Analizza non deve comparire nella memoria di combattimento.");
                Require(memory.AnalysisCount == 1,
                    "L'evento Analizza separato non è stato conservato.");
                Require(memory.GetFrequency(EncounterAction.Attack) == 2 &&
                        memory.GetFrequency(EncounterAction.Guard) == 2 &&
                        memory.GetFrequency(EncounterAction.Technique) == 2,
                    "Le frequenze delle ultime sei azioni non sono corrette.");
            });

            RunAssertion(errors, "Riconoscimento pattern adattivi", () =>
            {
                EnemyDecisionContext context = CreateDecisionContext();

                EnemyMemory attacks = CreateRepeatedMemory(EncounterAction.Attack, 3);
                AdaptiveEnemyBrain attackBrain = new AdaptiveEnemyBrain(2, 101);
                attackBrain.PlanAndLockIntent(attacks, context);
                Require(attackBrain.LastDecision.Pattern == LearnedPattern.RepeatedAttack,
                    "Tre Attacchi non vengono riconosciuti come abitudine.");
                Require(attackBrain.LastDecision.CounterProbability > 0d,
                    "Il pattern Attacco non influenza la decisione futura.");

                EnemyMemory guards = CreateRepeatedMemory(EncounterAction.Guard, 3);
                AdaptiveEnemyBrain guardBrain = new AdaptiveEnemyBrain(2, 102);
                guardBrain.PlanAndLockIntent(guards, context);
                Require(guardBrain.LastDecision.Pattern == LearnedPattern.RepeatedGuard,
                    "Tre Guardie non vengono riconosciute come abitudine.");
                Require(guardBrain.LastDecision.CounterProbability > 0d,
                    "Il pattern Guardia non influenza la decisione futura.");

                EnemyMemory technique = new EnemyMemory(2, 6);
                technique.RecordCompletedAction(EncounterAction.Technique);
                technique.RecordCompletedAction(EncounterAction.Attack);
                technique.RecordCompletedAction(EncounterAction.Guard);
                technique.RecordCompletedAction(EncounterAction.Technique);
                AdaptiveEnemyBrain techniqueBrain = new AdaptiveEnemyBrain(2, 103);
                techniqueBrain.PlanAndLockIntent(technique, context);
                Require(technique.TendsToUseTechniqueWhenReady,
                    "Il ritmo immediato della Tecnica non è stato rilevato.");
                Require(techniqueBrain.LastDecision.Pattern == LearnedPattern.TechniqueRhythm,
                    "Il ritmo della Tecnica non entra nella previsione.");

                EnemyMemory changed = CreateRepeatedMemory(EncounterAction.Attack, 3);
                double confidenceBefore = changed.PatternConfidence;
                changed.RecordCompletedAction(EncounterAction.Guard);
                AdaptiveEnemyBrain changedBrain = new AdaptiveEnemyBrain(2, 104);
                changedBrain.PlanAndLockIntent(changed, context);
                Require(changed.HasRecentStrategyChange,
                    "Il cambio improvviso da Attacco a Guardia non è stato rilevato.");
                Require(changed.PatternConfidence < confidenceBefore,
                    "Il cambio strategia deve abbassare la sicurezza della previsione.");
                Require(changedBrain.LastDecision.Pattern == LearnedPattern.StrategyChanged,
                    "Il cervello non espone il cambio strategia rilevato.");
            });

            RunAssertion(errors, "Analizza persistente influenza l'IA senza hard-counter", () =>
            {
                AdaptiveEnemyTuning analyzeTuning = new AdaptiveEnemyTuning
                {
                    AnalyzePatternMinimumCount = 3,
                    AnalyzePatternFrequencyThreshold = 0.40d,
                    AnalyzeResponseProbabilityMultiplier = 0.65d
                };
                EnemyMemory analyzeMemory = new EnemyMemory(2, 6, analyzeTuning);
                analyzeMemory.RecordAnalyze();
                analyzeMemory.RecordAnalyze();

                AdaptiveEnemyBrain belowThreshold = new AdaptiveEnemyBrain(
                    2,
                    105,
                    analyzeTuning);
                belowThreshold.PlanAndLockIntent(analyzeMemory, CreateDecisionContext());
                Require(belowThreshold.LastDecision.Pattern == LearnedPattern.None &&
                        belowThreshold.LastDecision.CounterProbability == 0d,
                    "Due Analisi non devono ancora produrre una previsione adattiva.");

                analyzeMemory.RecordAnalyze();
                AdaptiveEnemyBrain analyzeBrain = new AdaptiveEnemyBrain(
                    2,
                    106,
                    analyzeTuning);
                analyzeBrain.PlanAndLockIntent(analyzeMemory, CreateDecisionContext());
                Require(analyzeMemory.HasFrequentAnalyzePattern &&
                        analyzeMemory.HasEnoughObservationsForVisibleLearning(2),
                    "Tre Analisi persistenti non formano un'abitudine osservabile.");
                Require(analyzeBrain.LastDecision.Pattern == LearnedPattern.FrequentAnalyze,
                    "L'IA non riconosce l'uso frequente di Analizza.");
                Require(analyzeBrain.LastDecision.CounterProbability > 0d &&
                        analyzeBrain.LastDecision.CounterProbability < 0.50d,
                    "La risposta ad Analizza deve essere moderata, fallibile e mai perfetta.");
                Require(analyzeMemory.PatternSummary.Contains("Analizza"),
                    "Il riepilogo del pattern non comunica l'abitudine ad Analizza.");

                EnemyMemory dilutedAnalyzeMemory = new EnemyMemory(2, 6, analyzeTuning);
                dilutedAnalyzeMemory.RecordAnalyze();
                dilutedAnalyzeMemory.RecordAnalyze();
                dilutedAnalyzeMemory.RecordAnalyze();
                dilutedAnalyzeMemory.RecordCompletedAction(EncounterAction.Attack);
                dilutedAnalyzeMemory.RecordCompletedAction(EncounterAction.Guard);
                dilutedAnalyzeMemory.RecordCompletedAction(EncounterAction.Technique);
                dilutedAnalyzeMemory.RecordCompletedAction(EncounterAction.Attack);
                dilutedAnalyzeMemory.RecordCompletedAction(EncounterAction.Guard);
                dilutedAnalyzeMemory.RecordCompletedAction(EncounterAction.Technique);
                Require(!dilutedAnalyzeMemory.HasFrequentAnalyzePattern,
                    "La soglia percentuale configurata ignora una frequenza Analizza troppo bassa.");

                AdaptiveEnemyTuning weakerResponse = new AdaptiveEnemyTuning
                {
                    AnalyzePatternMinimumCount = 3,
                    AnalyzePatternFrequencyThreshold = 0.40d,
                    AnalyzeResponseProbabilityMultiplier = 0.20d
                };
                AdaptiveEnemyBrain weakerBrain = new AdaptiveEnemyBrain(
                    2,
                    106,
                    weakerResponse);
                weakerBrain.PlanAndLockIntent(analyzeMemory, CreateDecisionContext());
                Require(weakerBrain.LastDecision.Pattern == LearnedPattern.FrequentAnalyze &&
                        weakerBrain.LastDecision.CounterProbability > 0d &&
                        weakerBrain.LastDecision.CounterProbability <
                        analyzeBrain.LastDecision.CounterProbability,
                    "Il moltiplicatore configurabile non riduce la risposta ad Analizza.");

                AdaptiveEnemyBrain unawareBrain = new AdaptiveEnemyBrain(
                    0,
                    106,
                    analyzeTuning);
                unawareBrain.PlanAndLockIntent(analyzeMemory, CreateDecisionContext());
                Require(unawareBrain.LastDecision.Pattern == LearnedPattern.None &&
                        unawareBrain.LastDecision.CounterProbability == 0d,
                    "Un nemico senza intelligenza adattiva non deve usare il profilo Analizza.");
            });

            RunAssertion(errors, "Contromossa limitata e seed deterministico", () =>
            {
                EnemyMemory memory = CreateRepeatedMemory(EncounterAction.Attack, 6);
                EnemyDecisionContext context = CreateDecisionContext();

                for (int intelligence = 0; intelligence <= 3; intelligence++)
                {
                    AdaptiveEnemyBrain capped = new AdaptiveEnemyBrain(intelligence, 200 + intelligence);
                    capped.PlanAndLockIntent(memory, context);
                    Require(capped.LastDecision.CounterProbability <=
                            AdaptiveEnemyBrain.MaximumCounterProbability,
                        "La probabilità di contromossa supera il 65% al livello " + intelligence + ".");
                }

                AdaptiveEnemyBrain first = new AdaptiveEnemyBrain(2, 424242);
                AdaptiveEnemyBrain second = new AdaptiveEnemyBrain(2, 424242);
                for (int turn = 0; turn < 12; turn++)
                {
                    EnemyIntent firstIntent = first.PlanAndLockIntent(memory, context);
                    EnemyIntent secondIntent = second.PlanAndLockIntent(memory, context);
                    Require(firstIntent == secondIntent,
                        "Lo stesso seed ha prodotto intenzioni diverse al turno " + turn + ".");
                    Require(Math.Abs(first.LastDecision.CounterProbability -
                                     second.LastDecision.CounterProbability) < 0.000001d,
                        "Lo stesso seed ha prodotto decisioni adattive diverse.");
                    first.CompleteLockedIntent();
                    second.CompleteLockedIntent();
                }
            });

            RunAssertion(errors, "Corruzione e stati emotivi dinamici", () =>
            {
                Require(Corruption.Clamp(-5) == 0 && Corruption.Clamp(150) == 100,
                    "La corruzione deve essere limitata tra 0 e 100.");

                EnemyMemory empty = new EnemyMemory();
                Require(EnemyMoodEvaluator.Evaluate(
                            EnemyMood.Felice, EnemyMood.Felice, 100, 100, 20, empty) ==
                        EnemyMood.Felice,
                    "Lo stato Felice non è più compatibile.");
                Require(EnemyMoodEvaluator.Evaluate(
                            EnemyMood.Triste, EnemyMood.Triste, 30, 100, 58, empty) ==
                        EnemyMood.Spaventato,
                    "Un nemico Triste con pochi HP deve diventare Spaventato.");
                Require(EnemyMoodEvaluator.Evaluate(
                            EnemyMood.Arrabbiato, EnemyMood.Arrabbiato, 0, 100, 82, empty) ==
                        EnemyMood.Rassegnato,
                    "Un nemico sconfitto deve diventare Rassegnato.");

                EnemyMemory observedPattern = CreateRepeatedMemory(EncounterAction.Attack, 3);
                Require(EnemyMoodEvaluator.Evaluate(
                            EnemyMood.Arrabbiato,
                            EnemyMood.Arrabbiato,
                            80,
                            100,
                            82,
                            observedPattern) == EnemyMood.Guardingo,
                    "Il riconoscimento di un pattern deve poter rendere il nemico Guardingo.");
            });

            RunAssertion(errors, "Esiti Saved, Killed e HeroDefeated", () =>
            {
                EncounterBattleState savedState = CreateState(20, 58, EnemyMood.Triste, 1);
                EncounterActionResult finalBlow = savedState.ResolvePlayerAction(EncounterAction.Technique);
                Require(finalBlow.Accepted && savedState.EnemyDefeated,
                    "Il nemico non entra nello stato sconfitto a zero HP.");
                Require(savedState.Resolution == NarrativeOutcome.None && savedState.IsAwaitingResolution,
                    "Il nemico sconfitto non deve essere considerato automaticamente morto.");
                Require(!savedState.ResolvePlayerAction(EncounterAction.Attack).Accepted,
                    "Le azioni devono fermarsi durante la scelta finale.");
                Require(savedState.ResolveDefeatedEnemy(true) == NarrativeOutcome.Saved,
                    "La scelta Salva non produce Saved.");
                Require(savedState.CorruptionPercent == 0,
                    "Salva deve completare la purificazione portando la corruzione a zero.");
                Require(!savedState.ResolvePlayerAction(EncounterAction.Attack).Accepted &&
                        !savedState.ResolveEnemyIntent(EnemyIntent.Attack).Accepted,
                    "Nessuna azione deve essere accettata dopo Saved.");

                EncounterBattleState killedState = CreateState(20, 82, EnemyMood.Arrabbiato, 2);
                killedState.ResolvePlayerAction(EncounterAction.Technique);
                Require(killedState.ResolveDefeatedEnemy(false) == NarrativeOutcome.Killed,
                    "La scelta Uccidi non produce Killed.");
                Require(killedState.CorruptionPercent == 82,
                    "Uccidi non deve simulare una purificazione.");
                Require(!killedState.ResolvePlayerAction(EncounterAction.Guard).Accepted,
                    "Nessuna azione deve essere accettata dopo Killed.");

                EncounterRules defeatRules = new EncounterRules(
                    10, 100, 20, 32, 16, 40, 2, 65);
                EnemyProfile defeatProfile = new EnemyProfile(
                    "validator_defeat",
                    "Nemico Test",
                    "Razza Test",
                    70,
                    EnemyMood.Arrabbiato,
                    2);
                EncounterBattleState defeatedHero = new EncounterBattleState(
                    defeatRules,
                    defeatProfile,
                    new EnemyMemory(2, 6));
                EnemyIntentResult defeat = defeatedHero.ResolveEnemyIntent(EnemyIntent.Attack);
                Require(defeat.Accepted &&
                        defeatedHero.Resolution == NarrativeOutcome.HeroDefeated,
                    "Gli HP eroe a zero non producono HeroDefeated.");
                Require(!defeatedHero.ResolvePlayerAction(EncounterAction.Attack).Accepted &&
                        !defeatedHero.ResolveEnemyIntent(EnemyIntent.Attack).Accepted,
                    "Nessuna azione deve essere accettata dopo HeroDefeated.");
            });
        }

        private static void ValidateCampaignProgression(List<string> errors)
        {
            RunAssertion(errors, "Progressione della campagna", () =>
            {
                CampaignProgressData fresh = CampaignProgressStore.Defaults;
                Require(fresh.version == CampaignProgressStore.CurrentVersion,
                    "La progressione predefinita usa una versione errata.");
                Require(CampaignProgressStore.GetNextSceneName(fresh) ==
                        SceneNames.World01Level01Tutorial,
                    "Una nuova partita deve iniziare dal tutorial.");
                Require(!CampaignProgressStore.IsEncounterUnlocked(
                            CampaignEncounter.ThornGuardian, fresh),
                    "Il Livello 2 non deve essere sbloccato prima del tutorial.");

                CampaignProgressData tutorialComplete = fresh;
                tutorialComplete.tutorialCompleted = true;
                Require(CampaignProgressStore.GetNextSceneName(tutorialComplete) ==
                        SceneNames.World01Level02ThornGuardian,
                    "Completare il tutorial deve sbloccare il Livello 2.");
                Require(CampaignProgressStore.IsEncounterUnlocked(
                            CampaignEncounter.ThornGuardian, tutorialComplete),
                    "Il Custode del Rovo non risulta sbloccato dopo il tutorial.");
                Require(!CampaignProgressStore.IsEncounterUnlocked(
                            CampaignEncounter.AshWatcher, tutorialComplete),
                    "Il Livello 3 non deve essere sbloccato prima di risolvere il Livello 2.");

                CampaignProgressData saved = tutorialComplete;
                saved.encounter02Resolved = true;
                saved.encounter02Resolution = Veyra.Core.EncounterResolution.Saved;
                Require(CampaignProgressStore.GetNextSceneName(saved) ==
                        SceneNames.World01Level03AshWatcher,
                    "Salvare il Custode deve sbloccare il Livello 3.");
                Require(CampaignProgressStore.IsEncounterUnlocked(
                            CampaignEncounter.AshWatcher, saved),
                    "Il Vigile delle Ceneri non risulta sbloccato dopo il Livello 2.");

                CampaignProgressData killed = tutorialComplete;
                killed.encounter02Resolved = true;
                killed.encounter02Resolution = Veyra.Core.EncounterResolution.Killed;
                Require(CampaignProgressStore.GetNextSceneName(killed) ==
                        SceneNames.World01Level03AshWatcher,
                    "Uccidere il Custode deve comunque sbloccare il Livello 3.");
                Require(saved.encounter02Resolution != killed.encounter02Resolution,
                    "Saved e Killed devono essere registrati come decisioni distinte.");

                killed.encounter03Resolved = true;
                killed.encounter03Resolution = Veyra.Core.EncounterResolution.Saved;
                Require(killed.HasAnyProgress,
                    "Una campagna completata deve risultare come progresso presente.");
            });
        }

        private static void ValidateBuildSettings(List<string> errors)
        {
            string[] expectedOrder =
            {
                Phase046EncounterSceneFactory.MainMenuScenePath,
                Phase046EncounterSceneFactory.TutorialScenePath,
                Phase046EncounterSceneFactory.Level02ScenePath,
                Phase046EncounterSceneFactory.Level03ScenePath
            };

            EditorBuildSettingsScene[] configured = EditorBuildSettings.scenes;
            if (configured.Length < expectedOrder.Length)
            {
                errors.Add(
                    "Build Settings deve contenere almeno Main Menu, Tutorial, Livello 2 e Livello 3.");
                return;
            }

            for (int index = 0; index < expectedOrder.Length; index++)
            {
                string actualPath = configured[index].path.Replace('\\', '/');
                if (!string.Equals(actualPath, expectedOrder[index], StringComparison.Ordinal))
                {
                    errors.Add(
                        "Build Settings posizione " + index + ": atteso " + expectedOrder[index] +
                        ", trovato " + actualPath + ".");
                }

                if (!configured[index].enabled)
                {
                    errors.Add("La scena " + expectedOrder[index] + " deve essere abilitata nelle Build Settings.");
                }

                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(expectedOrder[index]) == null)
                {
                    errors.Add("Scena delle Build Settings mancante: " + expectedOrder[index] + ".");
                }
            }
        }

        private static void ValidatePersistentEncounterScene(
            EncounterSceneExpectation expected,
            List<string> errors)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(expected.ScenePath) == null)
            {
                errors.Add("Scena persistente mancante: " + expected.ScenePath + ".");
                return;
            }

            Scene scene = SceneManager.GetSceneByPath(expected.ScenePath);
            bool openedForValidation = !scene.IsValid() || !scene.isLoaded;

            try
            {
                if (openedForValidation)
                {
                    scene = EditorSceneManager.OpenScene(expected.ScenePath, OpenSceneMode.Additive);
                }

                ValidateLoadedEncounterScene(scene, expected, errors);
            }
            catch (Exception exception)
            {
                errors.Add(expected.SceneName + ": impossibile validare la scena: " + exception.Message);
            }
            finally
            {
                if (openedForValidation && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void ValidateLoadedEncounterScene(
            Scene scene,
            EncounterSceneExpectation expected,
            List<string> errors)
        {
            string prefix = expected.SceneName + ": ";
            GameObject[] roots = scene.GetRootGameObjects();
            GameObject sceneRoot = RequireUniqueRoot(roots, expected.SceneName, prefix, errors);
            GameObject battleRoot = RequireUniqueDirectChild(
                sceneRoot,
                Phase046EncounterSceneFactory.BattleRootName,
                prefix,
                errors);
            GameObject uiRoot = RequireUniqueDirectChild(
                sceneRoot,
                Phase046EncounterSceneFactory.UiRootName,
                prefix,
                errors);

            EncounterBattleController controller = RequireSingleComponent<EncounterBattleController>(
                scene,
                prefix,
                errors);
            EncounterBattleNavigation navigation = RequireSingleComponent<EncounterBattleNavigation>(
                scene,
                prefix,
                errors);

            if (controller == null || navigation == null)
            {
                return;
            }

            ValidateMissingScripts(scene, prefix, errors);
            ValidateRequiredReferences(controller, ControllerReferences, scene, prefix, errors);
            ValidateRequiredReferences(navigation, NavigationReferences, scene, prefix, errors);

            if (battleRoot != null && !IsSelfOrChild(controller.transform, battleRoot.transform))
            {
                errors.Add(prefix + "EncounterBattleController deve appartenere a EncounterBattleRoot.");
            }

            if (uiRoot != null && !IsSelfOrChild(navigation.transform, uiRoot.transform))
            {
                errors.Add(prefix + "EncounterBattleNavigation deve appartenere a EncounterUIRoot.");
            }

            SerializedObject controllerSerialized = new SerializedObject(controller);
            SerializedObject navigationSerialized = new SerializedObject(navigation);
            controllerSerialized.Update();
            navigationSerialized.Update();

            ValidateSceneConfiguration(controllerSerialized, expected, prefix, errors);
            ValidateCrossReferences(
                controllerSerialized,
                navigationSerialized,
                controller,
                navigation,
                prefix,
                errors);
            ValidateActors(controllerSerialized, battleRoot, expected, prefix, errors);
            ValidateInitialState(controllerSerialized, prefix, errors);
            ValidateEncounterUi(controllerSerialized, uiRoot, expected, prefix, errors);
            ValidatePersistentListeners(
                controllerSerialized,
                navigationSerialized,
                controller,
                navigation,
                prefix,
                errors);
            ValidateUniqueAuthoredObjects(scene, expected, prefix, errors);
        }

        private static void ValidateSceneConfiguration(
            SerializedObject serialized,
            EncounterSceneExpectation expected,
            string prefix,
            List<string> errors)
        {
            ValidateEnumProperty(serialized, "campaignEncounter", expected.CampaignEncounterName, prefix, errors);
            ValidateStringProperty(serialized, "encounterId", expected.EncounterId, prefix, errors);
            ValidateStringProperty(serialized, "enemyDisplayName", expected.EnemyDisplayName, prefix, errors);
            ValidateStringProperty(serialized, "enemyRace", expected.EnemyRace, prefix, errors);
            ValidateIntProperty(serialized, "enemyCorruptionPercent", expected.CorruptionPercent, prefix, errors);
            ValidateEnumProperty(serialized, "enemyInitialMood", expected.InitialMoodName, prefix, errors);
            ValidateIntProperty(serialized, "enemyIntelligenceLevel", expected.IntelligenceLevel, prefix, errors);
            ValidateIntProperty(serialized, "enemyRandomSeed", expected.RandomSeed, prefix, errors);
            ValidateIntProperty(serialized, "heroMaxHp", 100, prefix, errors);
            ValidateIntProperty(serialized, "enemyMaxHp", expected.EnemyMaxHp, prefix, errors);
            ValidateIntProperty(serialized, "attackDamage", 20, prefix, errors);
            ValidateIntProperty(serialized, "techniqueDamage", 32, prefix, errors);
            ValidateIntProperty(serialized, "enemyAttackDamage", expected.EnemyAttackDamage, prefix, errors);
            ValidateIntProperty(serialized, "chargedStrikeDamage", expected.ChargedStrikeDamage, prefix, errors);
            ValidateIntProperty(serialized, "techniqueCooldownTurns", 2, prefix, errors);
            ValidateIntProperty(serialized, "enemyGuardReductionPercent", 65, prefix, errors);
            ValidateFloatProperty(serialized, "resultReturnDelay", 2.5f, prefix, errors);

            foreach (string dialogueProperty in RequiredDialogueProperties)
            {
                SerializedProperty property = serialized.FindProperty(dialogueProperty);
                if (property == null || property.propertyType != SerializedPropertyType.String ||
                    string.IsNullOrWhiteSpace(property.stringValue))
                {
                    errors.Add(prefix + serialized.targetObject.GetType().Name + "." +
                               dialogueProperty + " deve contenere un dialogo persistente.");
                }
            }
        }

        private static void ValidateCrossReferences(
            SerializedObject controllerSerialized,
            SerializedObject navigationSerialized,
            EncounterBattleController controller,
            EncounterBattleNavigation navigation,
            string prefix,
            List<string> errors)
        {
            EncounterBattleNavigation linkedNavigation = GetReference<EncounterBattleNavigation>(
                controllerSerialized,
                "navigation",
                prefix,
                errors);
            EncounterBattleController linkedController = GetReference<EncounterBattleController>(
                navigationSerialized,
                "battleController",
                prefix,
                errors);

            if (linkedNavigation != null && linkedNavigation != navigation)
            {
                errors.Add(prefix + "Il controller non punta all'unica navigazione della scena.");
            }

            if (linkedController != null && linkedController != controller)
            {
                errors.Add(prefix + "La navigazione non punta all'unico controller della scena.");
            }

            Button outcomeButton = GetReference<Button>(
                controllerSerialized,
                "outcomeMenuButton",
                prefix,
                errors);
            Button navigationOutcomeButton = GetReference<Button>(
                navigationSerialized,
                "resultMenuButton",
                prefix,
                errors);
            if (outcomeButton != null && navigationOutcomeButton != null &&
                outcomeButton != navigationOutcomeButton)
            {
                errors.Add(prefix + "Controller e navigazione devono condividere BTN_OutcomeMenu.");
            }
        }

        private static void ValidateActors(
            SerializedObject serialized,
            GameObject battleRoot,
            EncounterSceneExpectation expected,
            string prefix,
            List<string> errors)
        {
            Transform hero = GetReference<Transform>(serialized, "heroActor", prefix, errors);
            Transform enemy = GetReference<Transform>(serialized, "enemyActor", prefix, errors);
            if (hero == null || enemy == null)
            {
                return;
            }

            if (battleRoot != null &&
                (!IsSelfOrChild(hero, battleRoot.transform) ||
                 !IsSelfOrChild(enemy, battleRoot.transform)))
            {
                errors.Add(prefix + "Hero01 e il nemico devono appartenere a EncounterBattleRoot.");
            }

            if (hero.position.x >= enemy.position.x)
            {
                errors.Add(prefix + "Hero01 deve essere a sinistra del nemico.");
            }

            float verticalDifference = Mathf.Abs(hero.position.y - enemy.position.y);
            if (verticalDifference > 0.05f)
            {
                errors.Add(prefix + "Hero01 e il nemico non sono quasi in linea: differenza Y " +
                           verticalDifference.ToString("0.###") + ".");
            }

            if (!Mathf.Approximately(hero.localPosition.x, -2.25f) ||
                !Mathf.Approximately(enemy.localPosition.x, 2.25f) ||
                !Mathf.Approximately(hero.localPosition.y, -4.9f) ||
                !Mathf.Approximately(enemy.localPosition.y, -4.9f))
            {
                errors.Add(prefix + "Le posizioni persistenti attese sono Hero (-2.25, -4.9) e " +
                           expected.EnemyDisplayName + " (2.25, -4.9).");
            }
        }

        private static void ValidateInitialState(
            SerializedObject serialized,
            string prefix,
            List<string> errors)
        {
            foreach (string propertyName in InitiallyInactiveControllerObjects)
            {
                GameObject value = GetReference<GameObject>(serialized, propertyName, prefix, errors);
                if (value != null && value.activeSelf)
                {
                    errors.Add(prefix + value.name + " deve essere persistente ma inizialmente inattivo.");
                }
            }

            Image heroFill = GetReference<Image>(serialized, "heroHealthFill", prefix, errors);
            Image enemyFill = GetReference<Image>(serialized, "enemyHealthFill", prefix, errors);
            ValidateInitialHealthFill(heroFill, "Hero", prefix, errors);
            ValidateInitialHealthFill(enemyFill, "Nemico", prefix, errors);
        }

        private static void ValidateEncounterUi(
            SerializedObject serialized,
            GameObject uiRoot,
            EncounterSceneExpectation expected,
            string prefix,
            List<string> errors)
        {
            Button analyze = GetReference<Button>(serialized, "analyzeButton", prefix, errors);
            if (analyze != null)
            {
                if (analyze.name != "BTN_Analyze")
                {
                    errors.Add(prefix + "Il quarto comando deve chiamarsi BTN_Analyze.");
                }

                TMP_Text label = analyze.GetComponentInChildren<TMP_Text>(true);
                if (label == null ||
                    !string.Equals(label.text.Trim(), "ANALIZZA", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(prefix + "Il quarto comando deve mostrare ANALIZZA.");
                }
            }

            TMP_Text analyzeName = GetReference<TMP_Text>(serialized, "analyzeNameText", prefix, errors);
            TMP_Text analyzeRace = GetReference<TMP_Text>(serialized, "analyzeRaceText", prefix, errors);
            TMP_Text analyzeCorruption = GetReference<TMP_Text>(
                serialized,
                "analyzeCorruptionText",
                prefix,
                errors);
            TMP_Text analyzeMood = GetReference<TMP_Text>(serialized, "analyzeMoodText", prefix, errors);
            TMP_Text analyzeTendency = GetReference<TMP_Text>(
                serialized,
                "analyzeTendencyText",
                prefix,
                errors);
            TMP_Text analyzeIntent = GetReference<TMP_Text>(
                serialized,
                "analyzeIntentText",
                prefix,
                errors);

            ValidateTextContains(analyzeName, expected.EnemyDisplayName, prefix, errors);
            ValidateTextContains(analyzeRace, expected.EnemyRace, prefix, errors);
            ValidateTextContains(analyzeCorruption, expected.CorruptionPercent + "%", prefix, errors);
            ValidateTextContains(analyzeMood, expected.InitialMoodName, prefix, errors);
            ValidateTextContains(analyzeTendency, "TENDENZA", prefix, errors);
            ValidateTextContains(analyzeIntent, "MOSSA ANNUNCIATA", prefix, errors);

            if (uiRoot != null)
            {
                TMP_Text obsoleteMark = uiRoot.GetComponentsInChildren<TMP_Text>(true)
                    .FirstOrDefault(text =>
                        text.text.IndexOf("MARCHIO", StringComparison.OrdinalIgnoreCase) >= 0);
                if (obsoleteMark != null)
                {
                    errors.Add(prefix + "La UI contiene ancora il comando obsoleto MARCHIO.");
                }
            }
        }

        private static void ValidatePersistentListeners(
            SerializedObject controllerSerialized,
            SerializedObject navigationSerialized,
            EncounterBattleController controller,
            EncounterBattleNavigation navigation,
            string prefix,
            List<string> errors)
        {
            ValidateButtonListener(controllerSerialized, "attackButton", controller, "ChooseAttack", prefix, errors);
            ValidateButtonListener(controllerSerialized, "guardButton", controller, "ChooseGuard", prefix, errors);
            ValidateButtonListener(
                controllerSerialized,
                "techniqueButton",
                controller,
                "ChooseTechnique",
                prefix,
                errors);
            ValidateButtonListener(controllerSerialized, "analyzeButton", controller, "OpenAnalyze", prefix, errors);
            ValidateButtonListener(
                controllerSerialized,
                "analyzeCloseButton",
                controller,
                "CloseAnalyze",
                prefix,
                errors);
            ValidateButtonListener(controllerSerialized, "saveButton", controller, "ChooseSave", prefix, errors);
            ValidateButtonListener(controllerSerialized, "killButton", controller, "ChooseKill", prefix, errors);
            ValidateButtonListener(
                controllerSerialized,
                "confirmationConfirmButton",
                controller,
                "ConfirmFinalChoice",
                prefix,
                errors);
            ValidateButtonListener(
                controllerSerialized,
                "confirmationBackButton",
                controller,
                "BackFromFinalConfirmation",
                prefix,
                errors);
            ValidateButtonListener(
                navigationSerialized,
                "backButton",
                navigation,
                "BackToMenu",
                prefix,
                errors);
            ValidateButtonListener(
                controllerSerialized,
                "outcomeMenuButton",
                controller,
                "ReturnToMenu",
                prefix,
                errors);
        }

        private static void ValidateUniqueAuthoredObjects(
            Scene scene,
            EncounterSceneExpectation expected,
            string prefix,
            List<string> errors)
        {
            string[] uniqueNames =
            {
                "HeroSlot",
                "EnemySlot_" + expected.EncounterId,
                "PersistentEffects",
                "HeroBasicProjectile",
                "HeroTechniqueProjectile",
                "EnemyProjectile",
                "HeroGuardVisual",
                "EnemyGuardVisual",
                "EnemyChargeVisual",
                "SavedPurificationVisual",
                "KilledFadeVisual",
                "AnalyzePanel",
                "FinalChoicePanel",
                "ConfirmationPanel",
                "OutcomeOverlay",
                "BTN_Attack",
                "BTN_Guard",
                "BTN_Technique",
                "BTN_Analyze",
                "BTN_BackToMenu",
                "BTN_SaveEnemy",
                "BTN_KillEnemy",
                "BTN_ConfirmChoice",
                "BTN_BackChoice",
                "BTN_OutcomeMenu"
            };

            foreach (string objectName in uniqueNames)
            {
                int count = CountNamedObjects(scene, objectName);
                if (count != 1)
                {
                    errors.Add(prefix + "atteso un solo " + objectName + ", trovati " + count + ".");
                }
            }

            int cameras = FindComponentsInScene<Camera>(scene).Length;
            int eventSystems = FindComponentsInScene<EventSystem>(scene).Length;
            if (cameras != 1)
            {
                errors.Add(prefix + "la scena deve contenere una sola Camera; trovate " + cameras + ".");
            }

            if (eventSystems != 1)
            {
                errors.Add(prefix + "la scena deve contenere un solo EventSystem; trovati " +
                           eventSystems + ".");
            }
        }

        private static EncounterBattleState CreateState(
            int enemyMaxHp,
            int corruption,
            EnemyMood initialMood,
            int intelligence,
            EnemyMemory memory = null)
        {
            EncounterRules rules = new EncounterRules(
                100,
                enemyMaxHp,
                20,
                32,
                22,
                40,
                2,
                65);
            EnemyProfile profile = new EnemyProfile(
                "validator_encounter",
                "Nemico Test",
                "Razza Test",
                corruption,
                initialMood,
                intelligence);
            return new EncounterBattleState(rules, profile, memory ?? new EnemyMemory(2, 6));
        }

        private static EncounterBattleState SimulateThornGuardian(
            int randomSeed,
            IReadOnlyList<EncounterAction> playerActions)
        {
            EncounterBattleState state = CreateState(
                ThornGuardian.EnemyMaxHp,
                ThornGuardian.CorruptionPercent,
                EnemyMood.Triste,
                ThornGuardian.IntelligenceLevel);
            AdaptiveEnemyBrain brain = new AdaptiveEnemyBrain(
                ThornGuardian.IntelligenceLevel,
                randomSeed);

            for (int index = 0; index < playerActions.Count; index++)
            {
                if (state.IsFinished || state.EnemyDefeated)
                {
                    break;
                }

                EnemyIntent lockedIntent = brain.PlanAndLockIntent(
                    state.Memory,
                    EnemyDecisionContext.From(state));
                EncounterActionResult playerResult = state.ResolvePlayerAction(playerActions[index]);
                if (!playerResult.Accepted)
                {
                    throw new InvalidOperationException(
                        "Azione di bilanciamento rifiutata al turno " + (index + 1) + ".");
                }

                if (state.EnemyDefeated)
                {
                    break;
                }

                EnemyIntentResult enemyResult = state.ResolveEnemyIntent(lockedIntent);
                if (!enemyResult.Accepted)
                {
                    throw new InvalidOperationException(
                        "Intenzione di bilanciamento rifiutata al turno " + (index + 1) + ".");
                }

                brain.CompleteLockedIntent();
            }

            return state;
        }

        private static EnemyMemory CreateRepeatedMemory(EncounterAction action, int repetitions)
        {
            EnemyMemory memory = new EnemyMemory(2, 6);
            for (int index = 0; index < repetitions; index++)
            {
                memory.RecordCompletedAction(action);
            }

            return memory;
        }

        private static EnemyDecisionContext CreateDecisionContext()
        {
            return new EnemyDecisionContext(
                100,
                100,
                100,
                130,
                82,
                EnemyMood.Arrabbiato,
                false,
                false,
                false);
        }

        private static GameObject RequireUniqueRoot(
            IEnumerable<GameObject> roots,
            string rootName,
            string prefix,
            List<string> errors)
        {
            GameObject[] matches = roots.Where(root => root.name == rootName).ToArray();
            if (matches.Length != 1)
            {
                errors.Add(prefix + "atteso esattamente un root " + rootName +
                           "; trovati " + matches.Length + ".");
                return matches.FirstOrDefault();
            }

            return matches[0];
        }

        private static GameObject RequireUniqueDirectChild(
            GameObject parent,
            string childName,
            string prefix,
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
                errors.Add(prefix + parent.name + " deve contenere un solo figlio diretto " +
                           childName + "; trovati " + matches.Length + ".");
                return matches.FirstOrDefault();
            }

            return matches[0];
        }

        private static T RequireSingleComponent<T>(
            Scene scene,
            string prefix,
            List<string> errors) where T : Component
        {
            T[] components = FindComponentsInScene<T>(scene);
            if (components.Length != 1)
            {
                errors.Add(prefix + "atteso un solo " + typeof(T).Name +
                           "; trovati " + components.Length + ".");
                return components.FirstOrDefault();
            }

            return components[0];
        }

        private static T[] FindComponentsInScene<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
        }

        private static void ValidateMissingScripts(
            Scene scene,
            string prefix,
            List<string> errors)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (transform.gameObject.GetComponents<Component>().Any(component => component == null))
                    {
                        errors.Add(prefix + "riferimento script mancante in " + transform.name + ".");
                    }
                }
            }
        }

        private static void ValidateRequiredReferences(
            MonoBehaviour component,
            IEnumerable<string> propertyNames,
            Scene expectedScene,
            string prefix,
            List<string> errors)
        {
            SerializedObject serialized = new SerializedObject(component);
            serialized.Update();
            foreach (string propertyName in propertyNames)
            {
                SerializedProperty property = serialized.FindProperty(propertyName);
                if (property == null)
                {
                    errors.Add(prefix + component.GetType().Name + "." + propertyName + " non esiste.");
                    continue;
                }

                if (property.propertyType != SerializedPropertyType.ObjectReference ||
                    property.objectReferenceValue == null)
                {
                    errors.Add(prefix + component.GetType().Name + "." + propertyName +
                               " non è assegnato.");
                    continue;
                }

                if (!BelongsToScene(property.objectReferenceValue, expectedScene))
                {
                    errors.Add(prefix + component.GetType().Name + "." + propertyName +
                               " non appartiene alla scena validata.");
                }
            }
        }

        private static bool BelongsToScene(UnityEngine.Object value, Scene scene)
        {
            Component component = value as Component;
            if (component != null)
            {
                return component.gameObject.scene == scene;
            }

            GameObject gameObject = value as GameObject;
            return gameObject != null && gameObject.scene == scene;
        }

        private static T GetReference<T>(
            SerializedObject serialized,
            string propertyName,
            string prefix,
            List<string> errors) where T : UnityEngine.Object
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            T value = property == null ? null : property.objectReferenceValue as T;
            if (value == null)
            {
                errors.Add(prefix + serialized.targetObject.GetType().Name + "." + propertyName +
                           " deve riferire un " + typeof(T).Name + ".");
            }

            return value;
        }

        private static void ValidateButtonListener(
            SerializedObject serialized,
            string buttonProperty,
            UnityEngine.Object expectedTarget,
            string expectedMethod,
            string prefix,
            List<string> errors)
        {
            Button button = GetReference<Button>(serialized, buttonProperty, prefix, errors);
            if (button == null)
            {
                return;
            }

            int count = button.onClick.GetPersistentEventCount();
            int matches = 0;
            for (int index = 0; index < count; index++)
            {
                if (button.onClick.GetPersistentTarget(index) == expectedTarget &&
                    string.Equals(
                        button.onClick.GetPersistentMethodName(index),
                        expectedMethod,
                        StringComparison.Ordinal) &&
                    button.onClick.GetPersistentListenerState(index) != UnityEventCallState.Off)
                {
                    matches++;
                }
            }

            if (matches != 1)
            {
                errors.Add(prefix + button.name + " deve avere un listener persistente attivo verso " +
                           expectedTarget.GetType().Name + "." + expectedMethod + ".");
            }

            if (count != 1)
            {
                errors.Add(prefix + button.name + " contiene " + count +
                           " listener persistenti; atteso uno solo per evitare duplicati.");
            }
        }

        private static void ValidateInitialHealthFill(
            Image fill,
            string label,
            string prefix,
            List<string> errors)
        {
            if (fill == null)
            {
                return;
            }

            if (fill.type != Image.Type.Filled)
            {
                errors.Add(prefix + label + " HP Fill deve usare Image.Type.Filled.");
            }

            if (!Mathf.Approximately(fill.fillAmount, 1f))
            {
                errors.Add(prefix + label + " HP Fill deve iniziare pieno.");
            }
        }

        private static void ValidateTextContains(
            TMP_Text text,
            string expectedFragment,
            string prefix,
            List<string> errors)
        {
            if (text != null &&
                text.text.IndexOf(expectedFragment, StringComparison.OrdinalIgnoreCase) < 0)
            {
                errors.Add(prefix + text.name + " deve contenere '" + expectedFragment + "'.");
            }
        }

        private static void ValidateIntProperty(
            SerializedObject serialized,
            string propertyName,
            int expected,
            string prefix,
            List<string> errors)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.Integer)
            {
                errors.Add(prefix + serialized.targetObject.GetType().Name + "." + propertyName +
                           " non è disponibile come intero.");
                return;
            }

            if (property.intValue != expected)
            {
                errors.Add(prefix + serialized.targetObject.GetType().Name + "." + propertyName +
                           " deve valere " + expected + ", trovato " + property.intValue + ".");
            }
        }

        private static void ValidateFloatProperty(
            SerializedObject serialized,
            string propertyName,
            float expected,
            string prefix,
            List<string> errors)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.Float)
            {
                errors.Add(prefix + serialized.targetObject.GetType().Name + "." + propertyName +
                           " non è disponibile come float.");
                return;
            }

            if (!Mathf.Approximately(property.floatValue, expected))
            {
                errors.Add(prefix + serialized.targetObject.GetType().Name + "." + propertyName +
                           " deve valere " + expected + ", trovato " + property.floatValue + ".");
            }
        }

        private static void ValidateStringProperty(
            SerializedObject serialized,
            string propertyName,
            string expected,
            string prefix,
            List<string> errors)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.String)
            {
                errors.Add(prefix + serialized.targetObject.GetType().Name + "." + propertyName +
                           " non è disponibile come stringa.");
                return;
            }

            if (!string.Equals(property.stringValue, expected, StringComparison.Ordinal))
            {
                errors.Add(prefix + serialized.targetObject.GetType().Name + "." + propertyName +
                           " deve valere '" + expected + "', trovato '" + property.stringValue + "'.");
            }
        }

        private static void ValidateEnumProperty(
            SerializedObject serialized,
            string propertyName,
            string expectedName,
            string prefix,
            List<string> errors)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.Enum)
            {
                errors.Add(prefix + serialized.targetObject.GetType().Name + "." + propertyName +
                           " non è disponibile come enum.");
                return;
            }

            string actualName = property.enumValueIndex >= 0 &&
                                property.enumValueIndex < property.enumNames.Length
                ? property.enumNames[property.enumValueIndex]
                : string.Empty;
            if (!string.Equals(actualName, expectedName, StringComparison.Ordinal))
            {
                errors.Add(prefix + serialized.targetObject.GetType().Name + "." + propertyName +
                           " deve valere " + expectedName + ", trovato " + actualName + ".");
            }
        }

        private static int CountNamedObjects(Scene scene, string objectName)
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Count(transform => transform.name == objectName);
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
                    "[Veyra Campaign Validation] Edit Mode superata: modello adattivo, " +
                    "progressione, Build Settings e scene persistenti delle Fasi 04-06 conformi.");
                return;
            }

            Debug.LogError(
                "[Veyra Campaign Validation] Edit Mode fallita:\n- " +
                string.Join("\n- ", errors));
        }

        private static void ExitBatchMode(int exitCode)
        {
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
        }

        private sealed class EncounterSceneExpectation
        {
            internal EncounterSceneExpectation(
                string scenePath,
                string sceneName,
                string campaignEncounterName,
                string encounterId,
                string enemyDisplayName,
                string enemyRace,
                int corruptionPercent,
                string initialMoodName,
                int intelligenceLevel,
                int randomSeed,
                int enemyMaxHp,
                int enemyAttackDamage,
                int chargedStrikeDamage)
            {
                ScenePath = scenePath;
                SceneName = sceneName;
                CampaignEncounterName = campaignEncounterName;
                EncounterId = encounterId;
                EnemyDisplayName = enemyDisplayName;
                EnemyRace = enemyRace;
                CorruptionPercent = corruptionPercent;
                InitialMoodName = initialMoodName;
                IntelligenceLevel = intelligenceLevel;
                RandomSeed = randomSeed;
                EnemyMaxHp = enemyMaxHp;
                EnemyAttackDamage = enemyAttackDamage;
                ChargedStrikeDamage = chargedStrikeDamage;
            }

            internal string ScenePath { get; }
            internal string SceneName { get; }
            internal string CampaignEncounterName { get; }
            internal string EncounterId { get; }
            internal string EnemyDisplayName { get; }
            internal string EnemyRace { get; }
            internal int CorruptionPercent { get; }
            internal string InitialMoodName { get; }
            internal int IntelligenceLevel { get; }
            internal int RandomSeed { get; }
            internal int EnemyMaxHp { get; }
            internal int EnemyAttackDamage { get; }
            internal int ChargedStrikeDamage { get; }
        }
    }
}
#endif
