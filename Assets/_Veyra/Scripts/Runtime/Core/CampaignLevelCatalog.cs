using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Veyra.Core
{
    /// <summary>
    /// Stable identifiers shared by authored scenes, progress data and UI.
    /// Values must never be changed after a build has saved them.
    /// </summary>
    public static class CampaignContentIds
    {
        public const string Level01Tutorial = "W01_L01_TUTORIAL";
        public const string Level02ThornGuardian = "W01_L02_THORN_GUARDIAN";
        public const string Level03AshWatcher = "W01_L03_ASH_WATCHER";
        public const string Level04ThreefoldAssault = "W01_L04_THREEFOLD_ASSAULT";
        public const string Level05ComingSoon = "W01_L05_COMING_SOON";
        public const string Level06ComingSoon = "W01_L06_COMING_SOON";
        public const string Level07ComingSoon = "W01_L07_COMING_SOON";
        public const string Level08ComingSoon = "W01_L08_COMING_SOON";
        public const string Level09ComingSoon = "W01_L09_COMING_SOON";
        public const string Level10ComingSoon = "W01_L10_COMING_SOON";

        public const string TutorialEnemy = "W01_L01_TUTORIAL_ENEMY";
        public const string ThornGuardianEnemy = "W01_L02_THORN_GUARDIAN";
        public const string AshWatcherEnemy = "W01_L03_ASH_WATCHER";
        public const string Level04BruteEnemy = "W01_L04_BRUTE";
        public const string Level04WatcherEnemy = "W01_L04_WATCHER";
        public const string Level04MaskEnemy = "W01_L04_MASK";

        public const string TutorialCombatBasics = "TUTORIAL_COMBAT_BASICS";
        public const string TutorialMoralChoice = "TUTORIAL_MORAL_CHOICE";
        public const string TutorialMultiTarget = "TUTORIAL_MULTI_TARGET";
    }

    public enum CampaignCombatType
    {
        Unavailable = 0,
        Tutorial = 1,
        SingleEnemy = 2,
        MultiEnemy = 3
    }

    /// <summary>
    /// Immutable authored metadata for one campaign level. Runtime progress is
    /// deliberately stored elsewhere and references this definition by StableId.
    /// </summary>
    public sealed class LevelDefinition
    {
        private readonly ReadOnlyCollection<string> enemyIds;

        internal LevelDefinition(
            string stableId,
            int number,
            string title,
            string shortDescription,
            string sceneName,
            bool isImplemented,
            string prerequisiteLevelId,
            string nextLevelId,
            CampaignCombatType combatType,
            IEnumerable<string> enemyIds,
            int experienceReward,
            int recommendedHeroLevel,
            bool hasTutorial,
            bool hasMoralChoice,
            bool replayAllowed)
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                throw new ArgumentException("A level needs a stable id.", nameof(stableId));
            }

            if (number < 1 || number > 10)
            {
                throw new ArgumentOutOfRangeException(nameof(number));
            }

            StableId = stableId;
            Number = number;
            Title = title ?? string.Empty;
            ShortDescription = shortDescription ?? string.Empty;
            SceneName = sceneName ?? string.Empty;
            IsImplemented = isImplemented;
            PrerequisiteLevelId = prerequisiteLevelId ?? string.Empty;
            NextLevelId = nextLevelId ?? string.Empty;
            CombatType = combatType;
            this.enemyIds = new List<string>(enemyIds ?? Array.Empty<string>()).AsReadOnly();
            ExperienceReward = Math.Max(0, experienceReward);
            RecommendedHeroLevel = Math.Max(1, recommendedHeroLevel);
            HasTutorial = hasTutorial;
            HasMoralChoice = hasMoralChoice;
            ReplayAllowed = replayAllowed;

            if (isImplemented && string.IsNullOrWhiteSpace(SceneName))
            {
                throw new ArgumentException("An implemented level needs an authored scene.");
            }
        }

        public string StableId { get; }
        public int Number { get; }
        public string Title { get; }
        public string ShortDescription { get; }
        public string SceneName { get; }
        public bool IsImplemented { get; }
        public string PrerequisiteLevelId { get; }
        public string NextLevelId { get; }
        public CampaignCombatType CombatType { get; }
        public IReadOnlyList<string> EnemyIds => enemyIds;
        public int ExperienceReward { get; }
        public int RecommendedHeroLevel { get; }
        public bool HasTutorial { get; }
        public bool HasMoralChoice { get; }
        public bool ReplayAllowed { get; }
        public bool IsComingSoon => !IsImplemented;
    }

    /// <summary>
    /// Single source of truth for the current ten-slot World01 campaign view.
    /// Only levels one through four are authored; the other entries are inert
    /// placeholders and contain no speculative encounter content.
    /// </summary>
    public static class CampaignLevelCatalog
    {
        private static readonly LevelDefinition[] Definitions =
        {
            new LevelDefinition(
                CampaignContentIds.Level01Tutorial,
                1,
                "Tutorial",
                "Impara Attacco, Guardia, Tecnica, Analizza e decidi il destino del nemico.",
                SceneNames.World01Level01Tutorial,
                true,
                string.Empty,
                CampaignContentIds.Level02ThornGuardian,
                CampaignCombatType.Tutorial,
                new[] { CampaignContentIds.TutorialEnemy },
                60,
                1,
                true,
                true,
                true),
            new LevelDefinition(
                CampaignContentIds.Level02ThornGuardian,
                2,
                "Custode del Rovo",
                "Affronta il Custode e decidi se salvarlo o ucciderlo.",
                SceneNames.World01Level02ThornGuardian,
                true,
                CampaignContentIds.Level01Tutorial,
                CampaignContentIds.Level03AshWatcher,
                CampaignCombatType.SingleEnemy,
                new[] { CampaignContentIds.ThornGuardianEnemy },
                90,
                1,
                false,
                true,
                true),
            new LevelDefinition(
                CampaignContentIds.Level03AshWatcher,
                3,
                "Vigile delle Ceneri",
                "Il nemico osserva le tue abitudini e reagisce alle ripetizioni.",
                SceneNames.World01Level03AshWatcher,
                true,
                CampaignContentIds.Level02ThornGuardian,
                CampaignContentIds.Level04ThreefoldAssault,
                CampaignCombatType.SingleEnemy,
                new[] { CampaignContentIds.AshWatcherEnemy },
                150,
                2,
                false,
                true,
                true),
            new LevelDefinition(
                CampaignContentIds.Level04ThreefoldAssault,
                4,
                "Assalto dei Tre",
                "Scegli il bersaglio e affronta tre comportamenti differenti.",
                SceneNames.World01Level04ThreefoldAssault,
                true,
                CampaignContentIds.Level03AshWatcher,
                string.Empty,
                CampaignCombatType.MultiEnemy,
                new[]
                {
                    CampaignContentIds.Level04BruteEnemy,
                    CampaignContentIds.Level04WatcherEnemy,
                    CampaignContentIds.Level04MaskEnemy
                },
                200,
                3,
                true,
                true,
                true),
            ComingSoon(CampaignContentIds.Level05ComingSoon, 5, CampaignContentIds.Level04ThreefoldAssault),
            ComingSoon(CampaignContentIds.Level06ComingSoon, 6, CampaignContentIds.Level05ComingSoon),
            ComingSoon(CampaignContentIds.Level07ComingSoon, 7, CampaignContentIds.Level06ComingSoon),
            ComingSoon(CampaignContentIds.Level08ComingSoon, 8, CampaignContentIds.Level07ComingSoon),
            ComingSoon(CampaignContentIds.Level09ComingSoon, 9, CampaignContentIds.Level08ComingSoon),
            ComingSoon(CampaignContentIds.Level10ComingSoon, 10, CampaignContentIds.Level09ComingSoon)
        };

        private static readonly ReadOnlyCollection<LevelDefinition> ReadOnlyDefinitions =
            Array.AsReadOnly(Definitions);
        private static readonly Dictionary<string, LevelDefinition> ById = BuildIdLookup();
        private static readonly Dictionary<int, LevelDefinition> ByNumber = BuildNumberLookup();

        public static IReadOnlyList<LevelDefinition> All => ReadOnlyDefinitions;
        public static int ImplementedLevelCount => 4;

        public static bool TryGetById(string levelId, out LevelDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(levelId))
            {
                definition = null;
                return false;
            }

            return ById.TryGetValue(levelId, out definition);
        }

        public static bool TryGetByNumber(int number, out LevelDefinition definition)
        {
            return ByNumber.TryGetValue(number, out definition);
        }

        public static LevelDefinition GetById(string levelId)
        {
            if (!TryGetById(levelId, out LevelDefinition definition))
            {
                throw new ArgumentOutOfRangeException(nameof(levelId), levelId, "Livello non riconosciuto.");
            }

            return definition;
        }

        public static LevelDefinition GetByNumber(int number)
        {
            if (!TryGetByNumber(number, out LevelDefinition definition))
            {
                throw new ArgumentOutOfRangeException(nameof(number), number, "Livello non riconosciuto.");
            }

            return definition;
        }

        public static bool IsKnownEnemy(string levelId, string enemyId)
        {
            if (!TryGetById(levelId, out LevelDefinition definition) ||
                string.IsNullOrWhiteSpace(enemyId))
            {
                return false;
            }

            for (int index = 0; index < definition.EnemyIds.Count; index++)
            {
                if (string.Equals(definition.EnemyIds[index], enemyId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static LevelDefinition ComingSoon(string id, int number, string prerequisiteId)
        {
            return new LevelDefinition(
                id,
                number,
                "Livello " + number,
                "Contenuto non ancora disponibile.",
                string.Empty,
                false,
                prerequisiteId,
                string.Empty,
                CampaignCombatType.Unavailable,
                Array.Empty<string>(),
                0,
                1,
                false,
                false,
                false);
        }

        private static Dictionary<string, LevelDefinition> BuildIdLookup()
        {
            Dictionary<string, LevelDefinition> lookup =
                new Dictionary<string, LevelDefinition>(StringComparer.Ordinal);
            for (int index = 0; index < Definitions.Length; index++)
            {
                LevelDefinition definition = Definitions[index];
                if (lookup.ContainsKey(definition.StableId))
                {
                    throw new InvalidOperationException("Duplicate campaign level id: " + definition.StableId);
                }

                lookup.Add(definition.StableId, definition);
            }

            return lookup;
        }

        private static Dictionary<int, LevelDefinition> BuildNumberLookup()
        {
            Dictionary<int, LevelDefinition> lookup = new Dictionary<int, LevelDefinition>();
            for (int index = 0; index < Definitions.Length; index++)
            {
                LevelDefinition definition = Definitions[index];
                if (lookup.ContainsKey(definition.Number))
                {
                    throw new InvalidOperationException(
                        "Duplicate campaign level number: " + definition.Number);
                }

                lookup.Add(definition.Number, definition);
            }

            return lookup;
        }
    }
}
