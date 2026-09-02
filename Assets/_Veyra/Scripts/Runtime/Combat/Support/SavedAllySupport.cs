using System;
using System.Collections.Generic;

namespace Veyra.Combat.Support
{
    /// <summary>
    /// Encounter-scoped, UI-agnostic state for one saved ally.
    /// The caller remains responsible for visuals and for applying AppliedDamage to its enemy model.
    /// </summary>
    public sealed class SavedAllySupport
    {
        private readonly Dictionary<SavedAllyDialogueCue, int> dialogueUseCounts =
            new Dictionary<SavedAllyDialogueCue, int>();

        public SavedAllySupport(SavedAllySupportDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        public SavedAllySupportDefinition Definition { get; }

        public bool HasIntervened { get; private set; }

        public bool CanIntervene(
            int completedHeroTurns,
            IReadOnlyList<SavedAllyTargetSnapshot> targets)
        {
            ValidateCompletedHeroTurns(completedHeroTurns);
            return !HasIntervened &&
                   completedHeroTurns >= Definition.MinimumCompletedHeroTurns &&
                   SelectTargetIndex(targets, Definition.TargetRule) >= 0;
        }

        public bool TryIntervene(
            int completedHeroTurns,
            IReadOnlyList<SavedAllyTargetSnapshot> targets,
            out SavedAllySupportAction action)
        {
            ValidateCompletedHeroTurns(completedHeroTurns);

            if (HasIntervened || completedHeroTurns < Definition.MinimumCompletedHeroTurns)
            {
                action = default;
                return false;
            }

            int targetListIndex = SelectTargetIndex(targets, Definition.TargetRule);
            if (targetListIndex < 0)
            {
                action = default;
                return false;
            }

            SavedAllyTargetSnapshot target = targets[targetListIndex];
            int appliedDamage = CalculateNonLethalDamage(target.CurrentHp, Definition.AttackDamage);
            if (appliedDamage <= 0)
            {
                action = default;
                return false;
            }

            HasIntervened = true;
            string dialogue = TryConsumeDialogue(
                SavedAllyDialogueCue.BeforeSupportAttack,
                out SavedAllyDialogueLine line)
                ? line.Text
                : string.Empty;

            action = new SavedAllySupportAction(
                Definition.AllyId,
                Definition.DisplayName,
                Definition.AttackDisplayName,
                targetListIndex,
                target,
                Definition.AttackDamage,
                appliedDamage,
                dialogue);
            return true;
        }

        public bool TryGetOpeningDialogue(out SavedAllyDialogueLine line)
        {
            return TryConsumeDialogue(SavedAllyDialogueCue.EncounterOpening, out line);
        }

        public bool TryGetHeroDifficultyDialogue(
            int heroHp,
            int heroMaximumHp,
            out SavedAllyDialogueLine line)
        {
            if (heroMaximumHp <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(heroMaximumHp));
            }

            if (heroHp < 0 || heroHp > heroMaximumHp)
            {
                throw new ArgumentOutOfRangeException(nameof(heroHp));
            }

            if ((long)heroHp * 2L >= heroMaximumHp)
            {
                line = default;
                return false;
            }

            return TryConsumeDialogue(SavedAllyDialogueCue.HeroBelowHalfHealth, out line);
        }

        public bool TryGetEndingDialogue(out SavedAllyDialogueLine line)
        {
            return TryConsumeDialogue(SavedAllyDialogueCue.EncounterEnd, out line);
        }

        public int GetDialogueUseCount(SavedAllyDialogueCue cue)
        {
            return dialogueUseCounts.TryGetValue(cue, out int count) ? count : 0;
        }

        public void ResetEncounter()
        {
            HasIntervened = false;
            dialogueUseCounts.Clear();
        }

        public static int CalculateNonLethalDamage(int currentHp, int requestedDamage)
        {
            if (currentHp < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(currentHp));
            }

            if (requestedDamage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(requestedDamage));
            }

            if (currentHp <= 1 || requestedDamage == 0)
            {
                return 0;
            }

            return Math.Min(requestedDamage, currentHp - 1);
        }

        public static int SelectTargetIndex(
            IReadOnlyList<SavedAllyTargetSnapshot> targets,
            SavedAllyTargetRule targetRule)
        {
            if (targets == null)
            {
                throw new ArgumentNullException(nameof(targets));
            }

            int selectedIndex = -1;
            for (int index = 0; index < targets.Count; index++)
            {
                SavedAllyTargetSnapshot candidate = targets[index];
                if (!candidate.CanReceiveNonLethalSupport)
                {
                    continue;
                }

                if (selectedIndex < 0 || IsPreferred(candidate, targets[selectedIndex], targetRule))
                {
                    selectedIndex = index;
                }
            }

            return selectedIndex;
        }

        private bool TryConsumeDialogue(
            SavedAllyDialogueCue cue,
            out SavedAllyDialogueLine line)
        {
            if (!Definition.TryGetDialogue(cue, out SavedAllyDialogueDefinition dialogue))
            {
                line = default;
                return false;
            }

            int previousCount = GetDialogueUseCount(cue);
            if (previousCount >= dialogue.MaximumUses)
            {
                line = default;
                return false;
            }

            int useNumber = previousCount + 1;
            dialogueUseCounts[cue] = useNumber;
            line = new SavedAllyDialogueLine(
                Definition.AllyId,
                cue,
                dialogue.Text,
                useNumber,
                dialogue.MaximumUses);
            return true;
        }

        private static bool IsPreferred(
            SavedAllyTargetSnapshot candidate,
            SavedAllyTargetSnapshot current,
            SavedAllyTargetRule targetRule)
        {
            int comparison;
            switch (targetRule)
            {
                case SavedAllyTargetRule.RosterOrder:
                    comparison = 0;
                    break;
                case SavedAllyTargetRule.LowestCurrentHealthThenRosterOrder:
                    comparison = candidate.CurrentHp.CompareTo(current.CurrentHp);
                    break;
                case SavedAllyTargetRule.LowestHealthRatioThenRosterOrder:
                    long candidateRatio = (long)candidate.CurrentHp * current.MaximumHp;
                    long currentRatio = (long)current.CurrentHp * candidate.MaximumHp;
                    comparison = candidateRatio.CompareTo(currentRatio);
                    break;
                case SavedAllyTargetRule.HighestCurrentHealthThenRosterOrder:
                    comparison = current.CurrentHp.CompareTo(candidate.CurrentHp);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(targetRule), targetRule, null);
            }

            if (comparison != 0)
            {
                return comparison < 0;
            }

            comparison = candidate.RosterIndex.CompareTo(current.RosterIndex);
            if (comparison != 0)
            {
                return comparison < 0;
            }

            return string.CompareOrdinal(candidate.TargetId, current.TargetId) < 0;
        }

        private static void ValidateCompletedHeroTurns(int completedHeroTurns)
        {
            if (completedHeroTurns < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(completedHeroTurns));
            }
        }
    }
}
