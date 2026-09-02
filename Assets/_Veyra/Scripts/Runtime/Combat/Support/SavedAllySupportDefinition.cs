using System;
using System.Collections.Generic;

namespace Veyra.Combat.Support
{
    public sealed class SavedAllyDialogueDefinition
    {
        public SavedAllyDialogueDefinition(
            SavedAllyDialogueCue cue,
            string text,
            int maximumUses = 1)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("Dialogue text cannot be empty.", nameof(text));
            }

            if (maximumUses <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumUses));
            }

            Cue = cue;
            Text = text.Trim();
            MaximumUses = maximumUses;
        }

        public SavedAllyDialogueCue Cue { get; }

        public string Text { get; }

        public int MaximumUses { get; }
    }

    public sealed class SavedAllySupportDefinition
    {
        private readonly Dictionary<SavedAllyDialogueCue, SavedAllyDialogueDefinition> dialogues;

        public SavedAllySupportDefinition(
            SavedAllyId allyId,
            string displayName,
            string attackDisplayName,
            int attackDamage,
            int minimumCompletedHeroTurns,
            SavedAllyTargetRule targetRule,
            params SavedAllyDialogueDefinition[] dialogueDefinitions)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("Ally display name cannot be empty.", nameof(displayName));
            }

            if (string.IsNullOrWhiteSpace(attackDisplayName))
            {
                throw new ArgumentException("Attack display name cannot be empty.", nameof(attackDisplayName));
            }

            if (attackDamage <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(attackDamage));
            }

            if (minimumCompletedHeroTurns < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumCompletedHeroTurns));
            }

            AllyId = allyId;
            DisplayName = displayName.Trim();
            AttackDisplayName = attackDisplayName.Trim();
            AttackDamage = attackDamage;
            MinimumCompletedHeroTurns = minimumCompletedHeroTurns;
            TargetRule = targetRule;
            dialogues = BuildDialogueMap(dialogueDefinitions);
        }

        public SavedAllyId AllyId { get; }

        public string DisplayName { get; }

        public string AttackDisplayName { get; }

        public int AttackDamage { get; }

        public int MinimumCompletedHeroTurns { get; }

        public SavedAllyTargetRule TargetRule { get; }

        public bool TryGetDialogue(
            SavedAllyDialogueCue cue,
            out SavedAllyDialogueDefinition dialogue)
        {
            return dialogues.TryGetValue(cue, out dialogue);
        }

        private static Dictionary<SavedAllyDialogueCue, SavedAllyDialogueDefinition> BuildDialogueMap(
            SavedAllyDialogueDefinition[] dialogueDefinitions)
        {
            var result = new Dictionary<SavedAllyDialogueCue, SavedAllyDialogueDefinition>();
            if (dialogueDefinitions == null)
            {
                return result;
            }

            for (int index = 0; index < dialogueDefinitions.Length; index++)
            {
                SavedAllyDialogueDefinition dialogue = dialogueDefinitions[index];
                if (dialogue == null)
                {
                    throw new ArgumentException(
                        "Dialogue definitions cannot contain null entries.",
                        nameof(dialogueDefinitions));
                }

                if (result.ContainsKey(dialogue.Cue))
                {
                    throw new ArgumentException(
                        "Only one dialogue definition is allowed for each cue.",
                        nameof(dialogueDefinitions));
                }

                result.Add(dialogue.Cue, dialogue);
            }

            return result;
        }
    }

    public static class SavedAllySupportCatalog
    {
        public const int ThornGuardianDamage = 8;
        public const int AshWatcherDamage = 10;
        public const int DefaultThornGuardianTurn = 2;
        public const int DefaultAshWatcherTurn = 3;

        public static SavedAllySupportDefinition CreateThornGuardian(
            int minimumCompletedHeroTurns = DefaultThornGuardianTurn,
            SavedAllyTargetRule targetRule = SavedAllyTargetRule.LowestHealthRatioThenRosterOrder)
        {
            return new SavedAllySupportDefinition(
                SavedAllyId.ThornGuardian,
                "Custode del Rovo",
                "FRUSTA DI ROVI",
                ThornGuardianDamage,
                minimumCompletedHeroTurns,
                targetRule,
                new SavedAllyDialogueDefinition(
                    SavedAllyDialogueCue.EncounterOpening,
                    "Non posso cancellare ciò che ero, ma posso ancora scegliere dove colpire."),
                new SavedAllyDialogueDefinition(
                    SavedAllyDialogueCue.BeforeSupportAttack,
                    "Queste radici adesso rispondono a me."),
                new SavedAllyDialogueDefinition(
                    SavedAllyDialogueCue.HeroBelowHalfHealth,
                    "Non affrontare tutto da solo."),
                new SavedAllyDialogueDefinition(
                    SavedAllyDialogueCue.EncounterEnd,
                    "Lo scontro è finito. Qualunque cosa accada, non sei rimasto solo."));
        }

        public static SavedAllySupportDefinition CreateAshWatcher(
            int minimumCompletedHeroTurns = DefaultAshWatcherTurn,
            SavedAllyTargetRule targetRule = SavedAllyTargetRule.LowestHealthRatioThenRosterOrder)
        {
            return new SavedAllySupportDefinition(
                SavedAllyId.AshWatcher,
                "Vigile delle Ceneri",
                "SCHEGGIA DI CENERE",
                AshWatcherDamage,
                minimumCompletedHeroTurns,
                targetRule,
                new SavedAllyDialogueDefinition(
                    SavedAllyDialogueCue.EncounterOpening,
                    "Ho studiato abbastanza la violenza. Ora userò ciò che so per fermarla."),
                new SavedAllyDialogueDefinition(
                    SavedAllyDialogueCue.BeforeSupportAttack,
                    "Conosco il momento in cui una difesa si spezza."),
                new SavedAllyDialogueDefinition(
                    SavedAllyDialogueCue.HeroBelowHalfHealth,
                    "Cambia ritmo. Io ti darò il tempo necessario."),
                new SavedAllyDialogueDefinition(
                    SavedAllyDialogueCue.EncounterEnd,
                    "Il ritmo si è spezzato. Ora dobbiamo convivere con ciò che resta."));
        }
    }
}
