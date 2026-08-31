using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Veyra.Combat.Encounter
{
    public sealed class EnemyMemory
    {
        public const int DefaultCapacity = 6;

        private readonly List<EncounterAction> completedActions;
        private readonly ReadOnlyCollection<EncounterAction> completedActionsView;
        private readonly int techniqueCooldownTurns;
        private int completedActionCount;
        private int lastTechniqueActionIndex = -1;
        private int techniqueIntervalsObserved;
        private int immediateTechniqueUses;

        public EnemyMemory(int techniqueCooldownTurns = 2, int capacity = DefaultCapacity)
        {
            if (techniqueCooldownTurns <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(techniqueCooldownTurns));
            }

            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            this.techniqueCooldownTurns = techniqueCooldownTurns;
            Capacity = capacity;
            completedActions = new List<EncounterAction>(capacity);
            completedActionsView = completedActions.AsReadOnly();
        }

        public int Capacity { get; }

        public IReadOnlyList<EncounterAction> CompletedActions => completedActionsView;

        public int CompletedActionCount => completedActionCount;

        public int AnalysisCount { get; private set; }

        public EncounterAction? LastCompletedAction =>
            completedActions.Count == 0
                ? (EncounterAction?)null
                : completedActions[completedActions.Count - 1];

        public int ConsecutiveCount
        {
            get
            {
                if (completedActions.Count == 0)
                {
                    return 0;
                }

                EncounterAction latest = completedActions[completedActions.Count - 1];
                int count = 1;
                for (int index = completedActions.Count - 2; index >= 0; index--)
                {
                    if (completedActions[index] != latest)
                    {
                        break;
                    }

                    count++;
                }

                return count;
            }
        }

        public EncounterAction? DominantAction
        {
            get
            {
                if (completedActions.Count == 0)
                {
                    return null;
                }

                EncounterAction bestAction = completedActions[0];
                int bestCount = 0;
                EncounterAction[] combatActions =
                {
                    EncounterAction.Attack,
                    EncounterAction.Guard,
                    EncounterAction.Technique
                };

                for (int index = 0; index < combatActions.Length; index++)
                {
                    int count = GetFrequency(combatActions[index]);
                    if (count > bestCount)
                    {
                        bestAction = combatActions[index];
                        bestCount = count;
                    }
                }

                return bestAction;
            }
        }

        public bool TendsToUseTechniqueWhenReady =>
            techniqueIntervalsObserved > 0 &&
            immediateTechniqueUses / (double)techniqueIntervalsObserved >= 0.6d;

        public bool HasRecentStrategyChange
        {
            get
            {
                if (completedActions.Count < 4)
                {
                    return false;
                }

                int lastIndex = completedActions.Count - 1;
                EncounterAction latest = completedActions[lastIndex];

                int precedingRun = 0;
                EncounterAction precedingAction = completedActions[lastIndex - 1];
                if (precedingAction != latest)
                {
                    for (int index = lastIndex - 1; index >= 0; index--)
                    {
                        if (completedActions[index] != precedingAction)
                        {
                            break;
                        }

                        precedingRun++;
                    }

                    if (precedingRun >= 3)
                    {
                        return true;
                    }
                }

                EncounterAction recent = completedActions[lastIndex];
                if (completedActions[lastIndex - 1] != recent)
                {
                    return false;
                }

                int earlierCount = lastIndex - 1;
                int differentEarlierActions = 0;
                for (int index = 0; index < earlierCount; index++)
                {
                    if (completedActions[index] != recent)
                    {
                        differentEarlierActions++;
                    }
                }

                return differentEarlierActions >= 2;
            }
        }

        public double PatternConfidence
        {
            get
            {
                if (completedActions.Count < 2)
                {
                    return 0d;
                }

                EncounterAction? dominant = DominantAction;
                if (!dominant.HasValue)
                {
                    return 0d;
                }

                double dominance = GetFrequency(dominant.Value) / (double)completedActions.Count;
                double repetitionBonus = ConsecutiveCount >= 3 ? 0.18d : 0d;
                double rhythmBonus = TendsToUseTechniqueWhenReady ? 0.12d : 0d;
                double confidence = dominance + repetitionBonus + rhythmBonus;

                if (HasRecentStrategyChange)
                {
                    confidence *= 0.35d;
                }

                return Clamp01(confidence);
            }
        }

        public string PatternSummary
        {
            get
            {
                if (HasRecentStrategyChange)
                {
                    return "Il giocatore ha cambiato strategia.";
                }

                if (TendsToUseTechniqueWhenReady)
                {
                    return "Il giocatore usa la Tecnica appena torna disponibile.";
                }

                if (LastCompletedAction == EncounterAction.Attack && ConsecutiveCount >= 3)
                {
                    return "Il giocatore ripete Attacco.";
                }

                if (LastCompletedAction == EncounterAction.Guard && ConsecutiveCount >= 3)
                {
                    return "Il giocatore ripete Guardia.";
                }

                return "Nessuna abitudine affidabile riconosciuta.";
            }
        }

        public void RecordCompletedAction(EncounterAction action)
        {
            if (action == EncounterAction.Analyze)
            {
                RecordAnalyze();
                return;
            }

            if (!IsCombatAction(action))
            {
                throw new ArgumentOutOfRangeException(nameof(action));
            }

            completedActionCount++;

            if (action == EncounterAction.Technique)
            {
                if (lastTechniqueActionIndex >= 0)
                {
                    techniqueIntervalsObserved++;
                    int interval = completedActionCount - lastTechniqueActionIndex;
                    if (interval == techniqueCooldownTurns + 1)
                    {
                        immediateTechniqueUses++;
                    }
                }

                lastTechniqueActionIndex = completedActionCount;
            }

            if (completedActions.Count == Capacity)
            {
                completedActions.RemoveAt(0);
            }

            completedActions.Add(action);
        }

        public void RecordAnalyze()
        {
            AnalysisCount++;
        }

        public int GetFrequency(EncounterAction action)
        {
            if (action == EncounterAction.Analyze)
            {
                return 0;
            }

            int count = 0;
            for (int index = 0; index < completedActions.Count; index++)
            {
                if (completedActions[index] == action)
                {
                    count++;
                }
            }

            return count;
        }

        public void Reset()
        {
            completedActions.Clear();
            completedActionCount = 0;
            AnalysisCount = 0;
            lastTechniqueActionIndex = -1;
            techniqueIntervalsObserved = 0;
            immediateTechniqueUses = 0;
        }

        private static bool IsCombatAction(EncounterAction action)
        {
            return action == EncounterAction.Attack ||
                   action == EncounterAction.Guard ||
                   action == EncounterAction.Technique;
        }

        private static double Clamp01(double value)
        {
            if (value < 0d)
            {
                return 0d;
            }

            return value > 1d ? 1d : value;
        }
    }
}
