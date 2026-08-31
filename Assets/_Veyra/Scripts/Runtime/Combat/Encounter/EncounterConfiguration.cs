using System;

namespace Veyra.Combat.Encounter
{
    public sealed class EnemyProfile
    {
        public EnemyProfile(
            string encounterId,
            string displayName,
            string race,
            int corruptionPercent,
            EnemyMood initialMood,
            int intelligenceLevel)
        {
            if (string.IsNullOrWhiteSpace(encounterId))
            {
                throw new ArgumentException("Encounter id cannot be empty.", nameof(encounterId));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("Enemy display name cannot be empty.", nameof(displayName));
            }

            if (string.IsNullOrWhiteSpace(race))
            {
                throw new ArgumentException("Enemy race cannot be empty.", nameof(race));
            }

            EncounterId = encounterId.Trim();
            DisplayName = displayName.Trim();
            Race = race.Trim();
            CorruptionPercent = Corruption.Clamp(corruptionPercent);
            InitialMood = initialMood;
            IntelligenceLevel = ClampIntelligence(intelligenceLevel);
        }

        public string EncounterId { get; }

        public string DisplayName { get; }

        public string Race { get; }

        public int CorruptionPercent { get; }

        public EnemyMood InitialMood { get; }

        public int IntelligenceLevel { get; }

        private static int ClampIntelligence(int value)
        {
            if (value < 0)
            {
                return 0;
            }

            return value > 3 ? 3 : value;
        }
    }

    public sealed class EncounterRules
    {
        public EncounterRules(
            int heroMaxHp = 100,
            int enemyMaxHp = 115,
            int attackDamage = 20,
            int techniqueDamage = 32,
            int enemyAttackDamage = 16,
            int chargedStrikeDamage = 32,
            int techniqueCooldownTurns = 2,
            int enemyGuardReductionPercent = 60)
        {
            RequirePositive(heroMaxHp, nameof(heroMaxHp));
            RequirePositive(enemyMaxHp, nameof(enemyMaxHp));
            RequirePositive(attackDamage, nameof(attackDamage));
            RequirePositive(techniqueDamage, nameof(techniqueDamage));
            RequirePositive(enemyAttackDamage, nameof(enemyAttackDamage));
            RequirePositive(chargedStrikeDamage, nameof(chargedStrikeDamage));

            if (techniqueDamage <= attackDamage)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(techniqueDamage),
                    "Technique damage must be greater than attack damage.");
            }

            if (chargedStrikeDamage <= enemyAttackDamage)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(chargedStrikeDamage),
                    "Charged strike damage must be greater than enemy attack damage.");
            }

            if (techniqueCooldownTurns <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(techniqueCooldownTurns));
            }

            if (enemyGuardReductionPercent < 1 || enemyGuardReductionPercent > 99)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(enemyGuardReductionPercent),
                    "Enemy guard reduction must be between 1 and 99 percent.");
            }

            HeroMaxHp = heroMaxHp;
            EnemyMaxHp = enemyMaxHp;
            AttackDamage = attackDamage;
            TechniqueDamage = techniqueDamage;
            EnemyAttackDamage = enemyAttackDamage;
            ChargedStrikeDamage = chargedStrikeDamage;
            TechniqueCooldownTurns = techniqueCooldownTurns;
            EnemyGuardReductionPercent = enemyGuardReductionPercent;
        }

        public int HeroMaxHp { get; }

        public int EnemyMaxHp { get; }

        public int AttackDamage { get; }

        public int TechniqueDamage { get; }

        public int EnemyAttackDamage { get; }

        public int ChargedStrikeDamage { get; }

        public int TechniqueCooldownTurns { get; }

        public int EnemyGuardReductionPercent { get; }

        private static void RequirePositive(int value, string parameterName)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Value must be greater than zero.");
            }
        }
    }
}
