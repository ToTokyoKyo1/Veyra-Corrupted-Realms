using System;

namespace Veyra.Combat.Encounter
{
    public sealed class EncounterBattleState
    {
        private const string BattleResolvedReason = "The encounter has already been resolved.";
        private const string EnemyDefeatedReason = "The enemy is defeated and awaits a final decision.";
        private const string TechniqueCooldownReason = "Technique is still on cooldown.";
        private const string GuardAlreadyPreparedReason = "Hero guard is already prepared.";
        private const string ChargedStrikeRequiredReason = "The announced charged strike must resolve next.";
        private const string ChargedStrikeNotPreparedReason = "No charged strike has been prepared.";

        private readonly EnemyMood initialMood;
        private int corruptionPercent;

        public EncounterBattleState(
            EncounterRules rules,
            EnemyProfile enemyProfile,
            EnemyMemory memory = null)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            EnemyProfile = enemyProfile ?? throw new ArgumentNullException(nameof(enemyProfile));
            Memory = memory ?? new EnemyMemory(rules.TechniqueCooldownTurns);
            initialMood = enemyProfile.InitialMood;
            Reset();
        }

        public EncounterRules Rules { get; }

        public EnemyProfile EnemyProfile { get; }

        public EnemyMemory Memory { get; }

        public int HeroMaxHp => Rules.HeroMaxHp;

        public int EnemyMaxHp => Rules.EnemyMaxHp;

        public int HeroHp { get; private set; }

        public int EnemyHp { get; private set; }

        public int TechniqueCooldownRemaining { get; private set; }

        public bool IsHeroGuardPrepared { get; private set; }

        public bool IsEnemyGuardPrepared { get; private set; }

        public bool IsChargedStrikePrepared { get; private set; }

        public bool EnemyDefeated => EnemyHp == 0;

        public bool IsAwaitingResolution => EnemyDefeated && Resolution == NarrativeOutcome.None;

        public bool IsFinished => Resolution != NarrativeOutcome.None;

        public NarrativeOutcome Resolution { get; private set; }

        public EnemyMood Mood { get; private set; }

        public int CorruptionPercent => corruptionPercent;

        public bool CanUsePlayerAction(EncounterAction action)
        {
            if (Resolution != NarrativeOutcome.None || EnemyDefeated || HeroHp == 0)
            {
                return false;
            }

            switch (action)
            {
                case EncounterAction.Attack:
                case EncounterAction.Analyze:
                    return true;
                case EncounterAction.Guard:
                    return !IsHeroGuardPrepared;
                case EncounterAction.Technique:
                    return TechniqueCooldownRemaining == 0;
                default:
                    return false;
            }
        }

        public EncounterActionResult ResolvePlayerAction(EncounterAction action)
        {
            if (Resolution != NarrativeOutcome.None)
            {
                return EncounterActionResult.Rejected(action, EnemyDefeated, BattleResolvedReason);
            }

            if (EnemyDefeated)
            {
                return EncounterActionResult.Rejected(action, true, EnemyDefeatedReason);
            }

            if (!IsKnownAction(action))
            {
                return EncounterActionResult.Rejected(action, false, "The player action is not supported.");
            }

            if (action == EncounterAction.Technique && TechniqueCooldownRemaining > 0)
            {
                return EncounterActionResult.Rejected(action, false, TechniqueCooldownReason);
            }

            if (action == EncounterAction.Guard && IsHeroGuardPrepared)
            {
                return EncounterActionResult.Rejected(action, false, GuardAlreadyPreparedReason);
            }

            if (action == EncounterAction.Analyze)
            {
                Memory.RecordAnalyze();
                UpdateMood();
                return EncounterActionResult.Completed(action, 0, false, false, false);
            }

            int damageDealt = 0;
            bool enemyGuardReducedDamage = false;

            switch (action)
            {
                case EncounterAction.Attack:
                    damageDealt = DamageEnemy(Rules.AttackDamage, out enemyGuardReducedDamage);
                    break;
                case EncounterAction.Guard:
                    IsHeroGuardPrepared = true;
                    break;
                case EncounterAction.Technique:
                    damageDealt = DamageEnemy(Rules.TechniqueDamage, out enemyGuardReducedDamage);
                    TechniqueCooldownRemaining = Rules.TechniqueCooldownTurns;
                    break;
            }

            if (action != EncounterAction.Technique && TechniqueCooldownRemaining > 0)
            {
                TechniqueCooldownRemaining--;
            }

            Memory.RecordCompletedAction(action);

            if (EnemyDefeated)
            {
                IsEnemyGuardPrepared = false;
                IsChargedStrikePrepared = false;
            }

            UpdateMood();
            return EncounterActionResult.Completed(
                action,
                damageDealt,
                true,
                enemyGuardReducedDamage,
                EnemyDefeated);
        }

        public EnemyIntentResult ResolveEnemyIntent(EnemyIntent intent)
        {
            if (Resolution != NarrativeOutcome.None)
            {
                return EnemyIntentResult.Rejected(intent, Resolution, BattleResolvedReason);
            }

            if (EnemyDefeated)
            {
                return EnemyIntentResult.Rejected(intent, Resolution, EnemyDefeatedReason);
            }

            if (!IsKnownIntent(intent))
            {
                return EnemyIntentResult.Rejected(intent, Resolution, "The enemy intent is not supported.");
            }

            if (IsChargedStrikePrepared && intent != EnemyIntent.ChargedStrike)
            {
                return EnemyIntentResult.Rejected(intent, Resolution, ChargedStrikeRequiredReason);
            }

            if (!IsChargedStrikePrepared && intent == EnemyIntent.ChargedStrike)
            {
                return EnemyIntentResult.Rejected(intent, Resolution, ChargedStrikeNotPreparedReason);
            }

            int damageDealt = 0;
            bool blockedByGuard = false;
            bool preparedGuard = false;
            bool beganCharge = false;

            switch (intent)
            {
                case EnemyIntent.Attack:
                    damageDealt = DamageHero(Rules.EnemyAttackDamage, out blockedByGuard);
                    break;
                case EnemyIntent.Guard:
                    IsEnemyGuardPrepared = true;
                    preparedGuard = true;
                    break;
                case EnemyIntent.Charge:
                    IsChargedStrikePrepared = true;
                    beganCharge = true;
                    break;
                case EnemyIntent.ChargedStrike:
                    IsChargedStrikePrepared = false;
                    damageDealt = DamageHero(Rules.ChargedStrikeDamage, out blockedByGuard);
                    break;
            }

            if (HeroHp == 0)
            {
                Resolution = NarrativeOutcome.HeroDefeated;
                IsHeroGuardPrepared = false;
                IsChargedStrikePrepared = false;
            }

            UpdateMood();
            return EnemyIntentResult.Completed(
                intent,
                damageDealt,
                blockedByGuard,
                preparedGuard,
                beganCharge,
                Resolution);
        }

        public NarrativeOutcome ResolveDefeatedEnemy(bool save)
        {
            if (Resolution != NarrativeOutcome.None)
            {
                return Resolution;
            }

            if (!EnemyDefeated)
            {
                throw new InvalidOperationException("The enemy must be defeated before making the final decision.");
            }

            Resolution = save ? NarrativeOutcome.Saved : NarrativeOutcome.Killed;
            if (save)
            {
                corruptionPercent = 0;
            }

            Mood = EnemyMood.Rassegnato;
            IsHeroGuardPrepared = false;
            IsEnemyGuardPrepared = false;
            IsChargedStrikePrepared = false;
            return Resolution;
        }

        public void Reset()
        {
            HeroHp = Rules.HeroMaxHp;
            EnemyHp = Rules.EnemyMaxHp;
            TechniqueCooldownRemaining = 0;
            IsHeroGuardPrepared = false;
            IsEnemyGuardPrepared = false;
            IsChargedStrikePrepared = false;
            Resolution = NarrativeOutcome.None;
            corruptionPercent = Corruption.Clamp(EnemyProfile.CorruptionPercent);
            Mood = initialMood;
            Memory.Reset();
        }

        private int DamageEnemy(int requestedDamage, out bool reducedByGuard)
        {
            reducedByGuard = IsEnemyGuardPrepared;
            int appliedDamage = requestedDamage;
            if (IsEnemyGuardPrepared)
            {
                appliedDamage = Math.Max(
                    1,
                    requestedDamage * (100 - Rules.EnemyGuardReductionPercent) / 100);
                IsEnemyGuardPrepared = false;
            }

            int previousHp = EnemyHp;
            EnemyHp = Math.Max(0, EnemyHp - appliedDamage);
            return previousHp - EnemyHp;
        }

        private int DamageHero(int requestedDamage, out bool blockedByGuard)
        {
            blockedByGuard = IsHeroGuardPrepared;
            IsHeroGuardPrepared = false;

            if (blockedByGuard)
            {
                return 0;
            }

            int previousHp = HeroHp;
            HeroHp = Math.Max(0, HeroHp - requestedDamage);
            return previousHp - HeroHp;
        }

        private void UpdateMood()
        {
            Mood = EnemyMoodEvaluator.Evaluate(
                initialMood,
                Mood,
                EnemyHp,
                EnemyMaxHp,
                corruptionPercent,
                Memory);
        }

        private static bool IsKnownAction(EncounterAction action)
        {
            return action == EncounterAction.Attack ||
                   action == EncounterAction.Guard ||
                   action == EncounterAction.Technique ||
                   action == EncounterAction.Analyze;
        }

        private static bool IsKnownIntent(EnemyIntent intent)
        {
            return intent == EnemyIntent.Attack ||
                   intent == EnemyIntent.Guard ||
                   intent == EnemyIntent.Charge ||
                   intent == EnemyIntent.ChargedStrike;
        }
    }

    public static class EnemyMoodEvaluator
    {
        public static EnemyMood Evaluate(
            EnemyMood initialMood,
            EnemyMood currentMood,
            int enemyHp,
            int enemyMaxHp,
            int corruptionPercent,
            EnemyMemory memory)
        {
            if (enemyMaxHp <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(enemyMaxHp));
            }

            if (memory == null)
            {
                throw new ArgumentNullException(nameof(memory));
            }

            int clampedHp = Math.Max(0, Math.Min(enemyMaxHp, enemyHp));
            double healthRatio = clampedHp / (double)enemyMaxHp;
            int corruption = Corruption.Clamp(corruptionPercent);

            if (clampedHp == 0)
            {
                return EnemyMood.Rassegnato;
            }

            if (healthRatio <= 0.20d)
            {
                return initialMood == EnemyMood.Arrabbiato && corruption >= 75
                    ? EnemyMood.Rassegnato
                    : EnemyMood.Spaventato;
            }

            if (memory.HasRecentStrategyChange ||
                (memory.CompletedActions.Count >= 3 && memory.PatternConfidence >= 0.60d))
            {
                return EnemyMood.Guardingo;
            }

            if (healthRatio <= 0.35d)
            {
                return initialMood == EnemyMood.Triste
                    ? EnemyMood.Spaventato
                    : EnemyMood.Guardingo;
            }

            if (memory.AnalysisCount >= 2)
            {
                if (initialMood == EnemyMood.Triste && healthRatio <= 0.60d)
                {
                    return EnemyMood.Rassegnato;
                }

                if (initialMood == EnemyMood.Arrabbiato)
                {
                    return EnemyMood.Guardingo;
                }
            }

            if (corruption >= 80 && healthRatio > 0.50d)
            {
                return EnemyMood.Arrabbiato;
            }

            return currentMood == EnemyMood.Spaventato ||
                   currentMood == EnemyMood.Rassegnato
                ? currentMood
                : initialMood;
        }
    }
}
