using System;

namespace Veyra.Combat.Support
{
    public enum SavedAllyId
    {
        ThornGuardian = 0,
        AshWatcher = 1
    }

    public enum SavedAllyDialogueCue
    {
        EncounterOpening = 0,
        HeroBelowHalfHealth = 1,
        BeforeSupportAttack = 2,
        EncounterEnd = 3
    }

    public enum SavedAllyTargetRule
    {
        RosterOrder = 0,
        LowestCurrentHealthThenRosterOrder = 1,
        LowestHealthRatioThenRosterOrder = 2,
        HighestCurrentHealthThenRosterOrder = 3
    }

    public readonly struct SavedAllyTargetSnapshot
    {
        public SavedAllyTargetSnapshot(
            string targetId,
            int rosterIndex,
            int currentHp,
            int maximumHp,
            bool isActive = true)
        {
            if (string.IsNullOrWhiteSpace(targetId))
            {
                throw new ArgumentException("Target id cannot be empty.", nameof(targetId));
            }

            if (rosterIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rosterIndex));
            }

            if (maximumHp <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumHp));
            }

            if (currentHp < 0 || currentHp > maximumHp)
            {
                throw new ArgumentOutOfRangeException(nameof(currentHp));
            }

            TargetId = targetId.Trim();
            RosterIndex = rosterIndex;
            CurrentHp = currentHp;
            MaximumHp = maximumHp;
            IsActive = isActive;
        }

        public string TargetId { get; }

        public int RosterIndex { get; }

        public int CurrentHp { get; }

        public int MaximumHp { get; }

        public bool IsActive { get; }

        public bool CanReceiveNonLethalSupport => IsActive && CurrentHp > 1;
    }

    public readonly struct SavedAllyDialogueLine
    {
        internal SavedAllyDialogueLine(
            SavedAllyId allyId,
            SavedAllyDialogueCue cue,
            string text,
            int useNumber,
            int useLimit)
        {
            AllyId = allyId;
            Cue = cue;
            Text = text ?? string.Empty;
            UseNumber = useNumber;
            UseLimit = useLimit;
        }

        public SavedAllyId AllyId { get; }

        public SavedAllyDialogueCue Cue { get; }

        public string Text { get; }

        public int UseNumber { get; }

        public int UseLimit { get; }

        public bool HasText => !string.IsNullOrWhiteSpace(Text);
    }

    public readonly struct SavedAllySupportAction
    {
        internal SavedAllySupportAction(
            SavedAllyId allyId,
            string allyDisplayName,
            string attackDisplayName,
            int targetListIndex,
            SavedAllyTargetSnapshot target,
            int requestedDamage,
            int appliedDamage,
            string dialogue)
        {
            AllyId = allyId;
            AllyDisplayName = allyDisplayName ?? string.Empty;
            AttackDisplayName = attackDisplayName ?? string.Empty;
            TargetListIndex = targetListIndex;
            TargetId = target.TargetId;
            TargetRosterIndex = target.RosterIndex;
            TargetHpBefore = target.CurrentHp;
            RequestedDamage = requestedDamage;
            AppliedDamage = appliedDamage;
            TargetHpAfter = target.CurrentHp - appliedDamage;
            Dialogue = dialogue ?? string.Empty;
        }

        public SavedAllyId AllyId { get; }

        public string AllyDisplayName { get; }

        public string AttackDisplayName { get; }

        public int TargetListIndex { get; }

        public string TargetId { get; }

        public int TargetRosterIndex { get; }

        public int TargetHpBefore { get; }

        public int TargetHpAfter { get; }

        public int RequestedDamage { get; }

        public int AppliedDamage { get; }

        public string Dialogue { get; }

        public bool HasDialogue => !string.IsNullOrWhiteSpace(Dialogue);

        // This explicit contract keeps support outside Hero01's action economy.
        public bool ConsumesHeroTurn => false;

        public bool AdvancesTechniqueCooldown => false;

        public bool RecordsHeroAction => false;

        public bool CanDefeatTarget => false;
    }
}
