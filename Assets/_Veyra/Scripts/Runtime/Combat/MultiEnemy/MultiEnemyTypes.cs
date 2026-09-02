using System;
using System.Collections.Generic;
using Veyra.Combat.Encounter;

namespace Veyra.Combat.MultiEnemy
{
    public enum MultiEnemyHeroAction
    {
        Attack,
        Guard,
        Technique,
        Analyze
    }

    public enum MultiEnemyIntent
    {
        Attack,
        Guard,
        Wait,
        Charge,
        HoldCharge,
        ChargedStrike,
        Assault,
        Finta
    }

    [Flags]
    public enum EnemyBehaviorTraits
    {
        None = 0,
        Aggressive = 1 << 0,
        Patient = 1 << 1,
        Deceptive = 1 << 2
    }

    public enum EnemyAltitude
    {
        Ground,
        Flying
    }

    public enum MultiEnemyBattlePhase
    {
        HeroTurn,
        EnemyPhase,
        AwaitingMoralChoices,
        Completed,
        HeroDefeated
    }

    public enum EnemyMoralOutcome
    {
        None,
        Saved,
        Killed
    }

    public sealed class EnemyTurnPlan
    {
        internal EnemyTurnPlan(
            string enemyId,
            int turnNumber,
            MultiEnemyIntent trueIntent,
            MultiEnemyIntent displayedIntent,
            bool isBluff,
            string instabilityClue)
        {
            EnemyId = enemyId;
            TurnNumber = turnNumber;
            TrueIntent = trueIntent;
            DisplayedIntent = displayedIntent;
            IsBluff = isBluff && trueIntent != displayedIntent;
            InstabilityClue = instabilityClue ?? string.Empty;
        }

        public string EnemyId { get; }

        public int TurnNumber { get; }

        /// <summary>
        /// The action locked before the player can act. Presentation code should normally
        /// use DisplayedIntent and only expose this value after a reveal or upgraded Analyze.
        /// </summary>
        public MultiEnemyIntent TrueIntent { get; }

        public MultiEnemyIntent DisplayedIntent { get; }

        public bool IsBluff { get; }

        public bool IntentChanged => TrueIntent != DisplayedIntent;

        public string InstabilityClue { get; }

        public MultiEnemyIntent GetVisibleIntent(bool revealTrueIntent)
        {
            return revealTrueIntent ? TrueIntent : DisplayedIntent;
        }
    }

    public sealed class DamageEvent
    {
        internal DamageEvent(
            string targetEnemyId,
            int requestedDamage,
            int appliedDamage,
            bool wasSplash,
            bool reducedByGuard,
            bool usedExposed,
            bool incapacitated,
            bool nonLethal)
        {
            TargetEnemyId = targetEnemyId ?? string.Empty;
            RequestedDamage = requestedDamage;
            AppliedDamage = appliedDamage;
            WasSplash = wasSplash;
            ReducedByGuard = reducedByGuard;
            UsedExposed = usedExposed;
            Incapacitated = incapacitated;
            NonLethal = nonLethal;
        }

        public string TargetEnemyId { get; }

        public int RequestedDamage { get; }

        public int AppliedDamage { get; }

        public bool WasSplash { get; }

        public bool ReducedByGuard { get; }

        public bool UsedExposed { get; }

        public bool Incapacitated { get; }

        public bool NonLethal { get; }
    }

    public sealed class EnemyIntel
    {
        internal EnemyIntel(
            MultiEnemyEnemyState enemy,
            EnemyTurnPlan plan,
            bool revealsTrueIntent)
        {
            EnemyId = enemy.Profile.EnemyId;
            DisplayName = enemy.Profile.DisplayName;
            Race = enemy.Profile.Race;
            CorruptionPercent = enemy.Profile.CorruptionPercent;
            Mood = enemy.Profile.Mood;
            CurrentHp = enemy.CurrentHp;
            MaxHp = enemy.Profile.MaxHp;
            Altitude = enemy.Profile.Altitude;
            Traits = enemy.Profile.Traits;
            DisplayedIntent = plan == null ? (MultiEnemyIntent?)null : plan.DisplayedIntent;
            TrueIntent = plan == null || !revealsTrueIntent
                ? (MultiEnemyIntent?)null
                : plan.TrueIntent;
            BluffRevealed = plan != null && revealsTrueIntent && plan.IsBluff;
            InstabilityClue = plan == null ? string.Empty : plan.InstabilityClue;
        }

        public string EnemyId { get; }

        public string DisplayName { get; }

        public string Race { get; }

        public int CorruptionPercent { get; }

        public EnemyMood Mood { get; }

        public int CurrentHp { get; }

        public int MaxHp { get; }

        public EnemyAltitude Altitude { get; }

        public EnemyBehaviorTraits Traits { get; }

        public MultiEnemyIntent? DisplayedIntent { get; }

        public MultiEnemyIntent? TrueIntent { get; }

        public bool BluffRevealed { get; }

        public string InstabilityClue { get; }
    }

    public sealed class HeroActionResolution
    {
        internal HeroActionResolution(
            bool accepted,
            MultiEnemyHeroAction action,
            bool consumesTurn,
            string selectedEnemyId,
            IList<DamageEvent> damageEvents,
            IList<EnemyIntel> intel,
            bool guardPrepared,
            bool bastionPrepared,
            bool allEnemiesIncapacitated,
            string rejectionReason)
        {
            Accepted = accepted;
            Action = action;
            ConsumesTurn = consumesTurn;
            SelectedEnemyId = selectedEnemyId ?? string.Empty;
            DamageEvents = new List<DamageEvent>(damageEvents ?? Array.Empty<DamageEvent>()).AsReadOnly();
            Intel = new List<EnemyIntel>(intel ?? Array.Empty<EnemyIntel>()).AsReadOnly();
            GuardPrepared = guardPrepared;
            BastionPrepared = bastionPrepared;
            AllEnemiesIncapacitated = allEnemiesIncapacitated;
            RejectionReason = rejectionReason ?? string.Empty;
        }

        public bool Accepted { get; }

        public MultiEnemyHeroAction Action { get; }

        public bool ConsumesTurn { get; }

        public string SelectedEnemyId { get; }

        public IReadOnlyList<DamageEvent> DamageEvents { get; }

        public IReadOnlyList<EnemyIntel> Intel { get; }

        public bool GuardPrepared { get; }

        public bool BastionPrepared { get; }

        public bool AllEnemiesIncapacitated { get; }

        public string RejectionReason { get; }
    }

    public sealed class EnemyActionResolution
    {
        internal EnemyActionResolution(
            EnemyTurnPlan plan,
            bool skippedBecauseIncapacitated,
            int damageDealt,
            bool blockedByGuard,
            bool preparedGuard,
            bool beganCharge,
            bool heldCharge,
            bool bluffRevealed)
        {
            Plan = plan;
            SkippedBecauseIncapacitated = skippedBecauseIncapacitated;
            DamageDealt = damageDealt;
            BlockedByGuard = blockedByGuard;
            PreparedGuard = preparedGuard;
            BeganCharge = beganCharge;
            HeldCharge = heldCharge;
            BluffRevealed = bluffRevealed;
        }

        public EnemyTurnPlan Plan { get; }

        public bool SkippedBecauseIncapacitated { get; }

        public int DamageDealt { get; }

        public bool BlockedByGuard { get; }

        public bool PreparedGuard { get; }

        public bool BeganCharge { get; }

        public bool HeldCharge { get; }

        public bool BluffRevealed { get; }
    }

    public sealed class EnemyPhaseResolution
    {
        internal EnemyPhaseResolution(
            bool accepted,
            IList<EnemyActionResolution> actions,
            int heroHpBefore,
            int heroHpAfter,
            bool heroDefeated,
            string rejectionReason)
        {
            Accepted = accepted;
            Actions = new List<EnemyActionResolution>(
                actions ?? Array.Empty<EnemyActionResolution>()).AsReadOnly();
            HeroHpBefore = heroHpBefore;
            HeroHpAfter = heroHpAfter;
            HeroDefeated = heroDefeated;
            RejectionReason = rejectionReason ?? string.Empty;
        }

        public bool Accepted { get; }

        public IReadOnlyList<EnemyActionResolution> Actions { get; }

        public int HeroHpBefore { get; }

        public int HeroHpAfter { get; }

        public bool HeroDefeated { get; }

        public string RejectionReason { get; }
    }

    public sealed class MoralChoiceResolution
    {
        internal MoralChoiceResolution(
            bool accepted,
            string enemyId,
            EnemyMoralOutcome outcome,
            bool allChoicesCompleted,
            string rejectionReason)
        {
            Accepted = accepted;
            EnemyId = enemyId ?? string.Empty;
            Outcome = outcome;
            AllChoicesCompleted = allChoicesCompleted;
            RejectionReason = rejectionReason ?? string.Empty;
        }

        public bool Accepted { get; }

        public string EnemyId { get; }

        public EnemyMoralOutcome Outcome { get; }

        public bool AllChoicesCompleted { get; }

        public string RejectionReason { get; }
    }
}
