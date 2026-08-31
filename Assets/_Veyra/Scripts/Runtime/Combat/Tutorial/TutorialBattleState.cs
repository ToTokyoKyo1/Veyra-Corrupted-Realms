using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Veyra.Combat.Tutorial
{
    public enum BattleAction
    {
        Attack,
        Technique,
        Guard,
        Analyze
    }

    public enum EnemyMood
    {
        Felice,
        Triste,
        Arrabbiato
    }

    public enum BattleOutcome
    {
        Ongoing,
        Victory,
        Defeat
    }

    public readonly struct BattleActionResult
    {
        private BattleActionResult(
            bool accepted,
            BattleAction action,
            int damageDealt,
            bool consumesTurn,
            bool blockedByGuard,
            BattleOutcome outcome,
            string rejectionReason)
        {
            Accepted = accepted;
            Action = action;
            DamageDealt = damageDealt;
            ConsumesTurn = consumesTurn;
            BlockedByGuard = blockedByGuard;
            Outcome = outcome;
            RejectionReason = rejectionReason;
        }

        public bool Accepted { get; }

        public BattleAction Action { get; }

        public int DamageDealt { get; }

        public bool ConsumesTurn { get; }

        public bool BlockedByGuard { get; }

        public BattleOutcome Outcome { get; }

        public string RejectionReason { get; }

        internal static BattleActionResult Completed(
            BattleAction action,
            int damageDealt,
            bool consumesTurn,
            bool blockedByGuard,
            BattleOutcome outcome)
        {
            return new BattleActionResult(
                true,
                action,
                damageDealt,
                consumesTurn,
                blockedByGuard,
                outcome,
                string.Empty);
        }

        internal static BattleActionResult Rejected(
            BattleAction action,
            BattleOutcome outcome,
            string reason)
        {
            return new BattleActionResult(false, action, 0, false, false, outcome, reason);
        }
    }

    public sealed class TutorialBattleState
    {
        private const string BattleFinishedReason = "The battle has already ended.";
        private const string TechniqueCooldownReason = "Technique is still on cooldown.";
        private const string GuardAlreadyPreparedReason = "Guard is already prepared.";
        private const string UnknownActionReason = "The requested action is not supported.";

        private readonly List<BattleAction> completedPlayerActions = new List<BattleAction>();
        private readonly ReadOnlyCollection<BattleAction> completedPlayerActionsView;
        private readonly int historyCapacity;
        private readonly int repeatedPatternLength;

        public TutorialBattleState(
            int heroMaxHp = 100,
            int enemyMaxHp = 100,
            int attackDamage = 20,
            int techniqueDamage = 32,
            int enemyAttackDamage = 25,
            int techniqueCooldownTurns = 2,
            int historyCapacity = 8,
            int repeatedPatternLength = 2)
        {
            RequirePositive(heroMaxHp, nameof(heroMaxHp));
            RequirePositive(enemyMaxHp, nameof(enemyMaxHp));
            RequirePositive(attackDamage, nameof(attackDamage));
            RequirePositive(techniqueDamage, nameof(techniqueDamage));
            RequirePositive(enemyAttackDamage, nameof(enemyAttackDamage));

            if (techniqueDamage <= attackDamage)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(techniqueDamage),
                    "Technique damage must be greater than base attack damage.");
            }

            if (techniqueCooldownTurns <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(techniqueCooldownTurns),
                    "Technique cooldown must be at least one turn.");
            }

            RequirePositive(historyCapacity, nameof(historyCapacity));
            if (repeatedPatternLength < 2 || repeatedPatternLength > historyCapacity)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(repeatedPatternLength),
                    "Repeated pattern length must be at least two and fit in the retained history.");
            }

            HeroMaxHp = heroMaxHp;
            EnemyMaxHp = enemyMaxHp;
            AttackDamage = attackDamage;
            TechniqueDamage = techniqueDamage;
            EnemyAttackDamage = enemyAttackDamage;
            TechniqueCooldownTurns = techniqueCooldownTurns;
            this.historyCapacity = historyCapacity;
            this.repeatedPatternLength = repeatedPatternLength;
            completedPlayerActionsView = completedPlayerActions.AsReadOnly();

            Reset();
        }

        public int HeroMaxHp { get; }

        public int EnemyMaxHp { get; }

        public int AttackDamage { get; }

        public int TechniqueDamage { get; }

        public int EnemyAttackDamage { get; }

        public int TechniqueCooldownTurns { get; }

        public int HeroHp { get; private set; }

        public int EnemyHp { get; private set; }

        public int TechniqueCooldownRemaining { get; private set; }

        public bool IsGuardPrepared { get; private set; }

        public BattleOutcome Outcome { get; private set; }

        public bool IsFinished => Outcome != BattleOutcome.Ongoing;

        public BattleAction EnemyPlannedAction => BattleAction.Attack;

        public IReadOnlyList<BattleAction> CompletedPlayerActions => completedPlayerActionsView;

        public BattleAction? LastCompletedPlayerAction =>
            completedPlayerActions.Count == 0
                ? (BattleAction?)null
                : completedPlayerActions[completedPlayerActions.Count - 1];

        public bool HasRepeatedPlayerPattern
        {
            get
            {
                if (completedPlayerActions.Count < repeatedPatternLength)
                {
                    return false;
                }

                BattleAction latest = completedPlayerActions[completedPlayerActions.Count - 1];
                int firstIndex = completedPlayerActions.Count - repeatedPatternLength;
                for (int index = firstIndex; index < completedPlayerActions.Count - 1; index++)
                {
                    if (completedPlayerActions[index] != latest)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public void Reset()
        {
            HeroHp = HeroMaxHp;
            EnemyHp = EnemyMaxHp;
            TechniqueCooldownRemaining = 0;
            IsGuardPrepared = false;
            Outcome = BattleOutcome.Ongoing;
            completedPlayerActions.Clear();
        }

        public bool CanUsePlayerAction(BattleAction action)
        {
            if (IsFinished)
            {
                return false;
            }

            switch (action)
            {
                case BattleAction.Attack:
                case BattleAction.Analyze:
                    return true;
                case BattleAction.Technique:
                    return TechniqueCooldownRemaining == 0;
                case BattleAction.Guard:
                    return !IsGuardPrepared;
                default:
                    return false;
            }
        }

        public BattleActionResult ResolvePlayerAction(BattleAction action)
        {
            if (IsFinished)
            {
                return BattleActionResult.Rejected(action, Outcome, BattleFinishedReason);
            }

            if (!IsKnownAction(action))
            {
                return BattleActionResult.Rejected(action, Outcome, UnknownActionReason);
            }

            if (action == BattleAction.Technique && TechniqueCooldownRemaining > 0)
            {
                return BattleActionResult.Rejected(action, Outcome, TechniqueCooldownReason);
            }

            if (action == BattleAction.Guard && IsGuardPrepared)
            {
                return BattleActionResult.Rejected(action, Outcome, GuardAlreadyPreparedReason);
            }

            if (action == BattleAction.Analyze)
            {
                return BattleActionResult.Completed(action, 0, false, false, Outcome);
            }

            int damageDealt = 0;

            switch (action)
            {
                case BattleAction.Attack:
                    damageDealt = DamageEnemy(AttackDamage);
                    break;
                case BattleAction.Technique:
                    damageDealt = DamageEnemy(TechniqueDamage);
                    TechniqueCooldownRemaining = TechniqueCooldownTurns;
                    break;
                case BattleAction.Guard:
                    IsGuardPrepared = true;
                    break;
            }

            if (action != BattleAction.Technique && TechniqueCooldownRemaining > 0)
            {
                TechniqueCooldownRemaining--;
            }

            RecordCompletedPlayerAction(action);
            UpdateOutcome();

            return BattleActionResult.Completed(action, damageDealt, true, false, Outcome);
        }

        public BattleActionResult ResolveEnemyAttack()
        {
            if (IsFinished)
            {
                return BattleActionResult.Rejected(BattleAction.Attack, Outcome, BattleFinishedReason);
            }

            bool blockedByGuard = IsGuardPrepared;
            int requestedDamage = blockedByGuard ? 0 : EnemyAttackDamage;

            IsGuardPrepared = false;
            int previousHp = HeroHp;
            HeroHp = Math.Max(0, HeroHp - requestedDamage);
            int damageDealt = previousHp - HeroHp;
            UpdateOutcome();

            return BattleActionResult.Completed(
                BattleAction.Attack,
                damageDealt,
                true,
                blockedByGuard,
                Outcome);
        }

        public bool TryGetRepeatedPlayerAction(out BattleAction action)
        {
            if (!HasRepeatedPlayerPattern)
            {
                action = BattleAction.Attack;
                return false;
            }

            action = completedPlayerActions[completedPlayerActions.Count - 1];
            return true;
        }

        private int DamageEnemy(int requestedDamage)
        {
            int previousHp = EnemyHp;
            EnemyHp = Math.Max(0, EnemyHp - requestedDamage);
            return previousHp - EnemyHp;
        }

        private void RecordCompletedPlayerAction(BattleAction action)
        {
            if (completedPlayerActions.Count == historyCapacity)
            {
                completedPlayerActions.RemoveAt(0);
            }

            completedPlayerActions.Add(action);
        }

        private void UpdateOutcome()
        {
            if (EnemyHp == 0)
            {
                Outcome = BattleOutcome.Victory;
            }
            else if (HeroHp == 0)
            {
                Outcome = BattleOutcome.Defeat;
            }
        }

        private static bool IsKnownAction(BattleAction action)
        {
            return action == BattleAction.Attack ||
                   action == BattleAction.Technique ||
                   action == BattleAction.Guard ||
                   action == BattleAction.Analyze;
        }

        private static void RequirePositive(int value, string parameterName)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Value must be greater than zero.");
            }
        }
    }
}
