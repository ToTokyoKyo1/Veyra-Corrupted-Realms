#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
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
using Veyra.Combat.MultiEnemy;
using Veyra.Combat.Support;
using Veyra.Combat.Tutorial;
using Veyra.Core;
using Veyra.Progression;
using Veyra.UI.MainMenu;
using Veyra.UI.Settings;

namespace Veyra.Editor
{
    /// <summary>
    /// Edit Mode validation for the persistent campaign menu, Hero01 progression,
    /// saved allies and the pure three-enemy battle model. The validator deliberately
    /// does not depend on the Phase 7/8 factory, so it still compiles and reports a
    /// precise missing-asset error before that factory (or Level 04) has been created.
    /// </summary>
    public static class Phase78ProgressionValidator
    {
        private const string MenuPath =
            "Tools/Veyra/Progression/Validate Progression and Level 04";

        private const string MainMenuScenePath =
            "Assets/_Veyra/Scenes/SCN_MainMenu.unity";
        private const string TutorialScenePath =
            "Assets/_Veyra/Scenes/SCN_W01_L01_Tutorial.unity";
        private const string Level02ScenePath =
            "Assets/_Veyra/Scenes/SCN_W01_L02_ThornGuardian.unity";
        private const string Level03ScenePath =
            "Assets/_Veyra/Scenes/SCN_W01_L03_AshWatcher.unity";
        private const string Level04ScenePath =
            "Assets/_Veyra/Scenes/SCN_W01_L04_ThreefoldAssault.unity";

        private static readonly string[] RequiredBuildScenePaths =
        {
            MainMenuScenePath,
            TutorialScenePath,
            Level02ScenePath,
            Level03ScenePath,
            Level04ScenePath
        };

        private static readonly string[] MainButtonNames =
        {
            "BTN_Start",
            "BTN_Levels",
            "BTN_Heroes",
            "BTN_Options"
        };

        private static readonly string[] RequiredMainMenuReferences =
        {
            "startButton",
            "startButtonLabel",
            "levelsButton",
            "heroesButton",
            "settingsButton",
            "mainNavigationPanel",
            "levelsPanel",
            "heroesPanel",
            "completedLevelsText",
            "heroNameText",
            "heroLevelText",
            "heroExperienceText",
            "heroExperienceFill",
            "heroStatsText",
            "heroUpgradesText",
            "heroPointsText",
            "heroUpgradeButton",
            "resetProgressButton",
            "resetProgressConfirmationModal",
            "settingsPanel"
        };

        private static readonly string[] RequiredLevel04References =
        {
            "heroActor",
            "heroVisual",
            "heroHealthFill",
            "heroHealthText",
            "heroGuardEffect",
            "heroAttackEffect",
            "heroTechniqueEffect",
            "attackButton",
            "guardButton",
            "techniqueButton",
            "analyzeButton",
            "techniqueButtonLabel",
            "combatMessageText",
            "selectedTargetText",
            "heroStatusText",
            "phaseIndicatorText",
            "targetTutorialOverlay",
            "targetTutorialText",
            "targetTutorialContinueButton",
            "dialogueRoot",
            "dialogueText",
            "analyzePanel",
            "analyzeTitleText",
            "analyzeBodyText",
            "analyzeCloseButton",
            "thornGuardianAllyActor",
            "thornGuardianSupportEffect",
            "ashWatcherAllyActor",
            "ashWatcherSupportEffect",
            "allyDialogueRoot",
            "allyDialogueText",
            "moralChoicePanel",
            "moralSummaryText",
            "moralConfirmButton",
            "moralReviewButton",
            "moralFocusTitleText",
            "moralFocusBodyText",
            "moralFocusPortrait",
            "outcomePanel",
            "outcomeTitleText",
            "outcomeBodyText",
            "outcomeMenuButton",
            "outcomeRetryButton",
            "outcomeRetryButtonLabel",
            "navigation"
        };

        private static readonly string[] RequiredEnemyViewReferences =
        {
            "actor",
            "visual",
            "targetButton",
            "nameText",
            "healthText",
            "healthFill",
            "intentText",
            "targetStateText",
            "selectionIndicator",
            "instabilityClue",
            "incapacitatedState",
            "guardEffect",
            "chargeEffect",
            "hitEffect"
        };

        [MenuItem(MenuPath, priority = 172)]
        public static void ValidateProgressionAndLevel04()
        {
            var errors = new List<string>();

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                errors.Add("Esci dal Play Mode prima di eseguire il validatore di progressione.");
                CompleteValidation(errors);
                return;
            }

            RunCheck(errors, "Catalogo campagna L1-L10", ValidateCampaignCatalog);
            RunCheck(errors, "Progressione campagna", ValidateCampaignProgressionModel);
            RunCheck(errors, "XP, statistiche, potenziamenti e migrazione", ValidateHeroProgression);
            RunCheck(errors, "Vista della Corruzione negli scontri singoli",
                ValidateSingleEncounterAnalyzeAndExposed);
            RunCheck(errors, "Supporti degli alleati salvati", ValidateSavedAllySupport);
            RunCheck(errors, "Roster e combattimento multi-nemico", ValidateMultiEnemyModel);
            RunCheck(errors, "Bluff e determinismo", ValidateBluffAndDeterminism);
            RunCheck(errors, "Build Settings", () => ValidateBuildSettings(errors));
            RunCheck(errors, "Regole di authoring runtime", () => ValidateRuntimeAuthoringRules(errors));
            RunCheck(errors, "SCN_MainMenu", () => ValidateSceneAsset(
                MainMenuScenePath,
                errors,
                ValidateMainMenuScene));
            RunCheck(errors, "Regressioni scene 01-03", () => ValidateRegressionScenes(errors));
            RunCheck(errors, "SCN_W01_L04_ThreefoldAssault", () => ValidateSceneAsset(
                Level04ScenePath,
                errors,
                ValidateLevel04Scene));

            CompleteValidation(errors);
        }

        [MenuItem(MenuPath, true)]
        private static bool CanValidateProgressionAndLevel04()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode &&
                   !EditorApplication.isCompiling;
        }

        private static void ValidateCampaignCatalog()
        {
            IReadOnlyList<LevelDefinition> levels = CampaignLevelCatalog.All;
            string[] expectedIds =
            {
                CampaignContentIds.Level01Tutorial,
                CampaignContentIds.Level02ThornGuardian,
                CampaignContentIds.Level03AshWatcher,
                CampaignContentIds.Level04ThreefoldAssault,
                CampaignContentIds.Level05ComingSoon,
                CampaignContentIds.Level06ComingSoon,
                CampaignContentIds.Level07ComingSoon,
                CampaignContentIds.Level08ComingSoon,
                CampaignContentIds.Level09ComingSoon,
                CampaignContentIds.Level10ComingSoon
            };

            Require(levels.Count == 10,
                "Il catalogo deve esporre esattamente dieci livelli.");
            Require(CampaignLevelCatalog.ImplementedLevelCount == 4,
                "Il catalogo deve dichiarare esattamente quattro livelli implementati.");
            Require(levels.Select(level => level.StableId).Distinct().Count() == 10 &&
                    levels.Select(level => level.Number).Distinct().Count() == 10,
                "Il catalogo contiene id stabili o numeri di livello duplicati.");

            for (int index = 0; index < levels.Count; index++)
            {
                int number = index + 1;
                LevelDefinition level = levels[index];
                Require(level.Number == number && level.StableId == expectedIds[index],
                    "Lo slot " + number + " non conserva numero e id stabile previsti.");
                Require(ReferenceEquals(CampaignLevelCatalog.GetByNumber(number), level) &&
                        ReferenceEquals(CampaignLevelCatalog.GetById(level.StableId), level),
                    "Le lookup del catalogo non restituiscono lo slot " + number + ".");

                if (index < CampaignLevelCatalog.ImplementedLevelCount)
                {
                    Require(level.IsImplemented && !level.IsComingSoon &&
                            !string.IsNullOrWhiteSpace(level.SceneName) &&
                            level.HasMoralChoice && level.ReplayAllowed &&
                            level.ExperienceReward > 0,
                        "Il Livello " + number +
                        " non espone scena, scelta morale, replay o ricompensa implementata.");
                }
                else
                {
                    Require(!level.IsImplemented && level.IsComingSoon &&
                            string.IsNullOrEmpty(level.SceneName) &&
                            level.CombatType == CampaignCombatType.Unavailable &&
                            level.EnemyIds.Count == 0 && level.ExperienceReward == 0 &&
                            !level.HasTutorial && !level.HasMoralChoice && !level.ReplayAllowed,
                        "Il Livello " + number +
                        " deve restare un placeholder non giocabile e senza ricompense.");
                }

                string expectedPrerequisite = index == 0
                    ? string.Empty
                    : expectedIds[index - 1];
                Require(level.PrerequisiteLevelId == expectedPrerequisite,
                    "Prerequisito errato per il Livello " + number + ".");
            }

            for (int index = 0; index < CampaignLevelCatalog.ImplementedLevelCount - 1; index++)
            {
                Require(levels[index].NextLevelId == levels[index + 1].StableId,
                    "Collegamento al livello successivo errato nello slot " + (index + 1) + ".");
            }

            LevelDefinition level01 = levels[0];
            LevelDefinition level02 = levels[1];
            LevelDefinition level03 = levels[2];
            LevelDefinition level04 = levels[3];
            Require(level01.SceneName == SceneNames.World01Level01Tutorial &&
                    level02.SceneName == SceneNames.World01Level02ThornGuardian &&
                    level03.SceneName == SceneNames.World01Level03AshWatcher &&
                    level04.SceneName == SceneNames.World01Level04ThreefoldAssault,
                "Il catalogo non punta alle quattro scene persistenti previste.");
            Require(level01.CombatType == CampaignCombatType.Tutorial && level01.HasTutorial &&
                    level02.CombatType == CampaignCombatType.SingleEnemy &&
                    level03.CombatType == CampaignCombatType.SingleEnemy &&
                    level04.CombatType == CampaignCombatType.MultiEnemy && level04.HasTutorial,
                "Tipologia di combattimento o tutorial contestuale errati nel catalogo.");
            Require(level01.ExperienceReward == 60 && level02.ExperienceReward == 90 &&
                    level03.ExperienceReward == 150 && level04.ExperienceReward == 200 &&
                    level01.RecommendedHeroLevel == 1 &&
                    level02.RecommendedHeroLevel == 1 &&
                    level03.RecommendedHeroLevel == 2 &&
                    level04.RecommendedHeroLevel == 3,
                "Ricompense XP o livelli consigliati del catalogo non sono conformi.");
            Require(level01.EnemyIds.SequenceEqual(new[] { CampaignContentIds.TutorialEnemy }) &&
                    level02.EnemyIds.SequenceEqual(new[] { CampaignContentIds.ThornGuardianEnemy }) &&
                    level03.EnemyIds.SequenceEqual(new[] { CampaignContentIds.AshWatcherEnemy }) &&
                    level04.EnemyIds.SequenceEqual(new[]
                    {
                        CampaignContentIds.Level04BruteEnemy,
                        CampaignContentIds.Level04WatcherEnemy,
                        CampaignContentIds.Level04MaskEnemy
                    }),
                "Gli id nemico stabili del catalogo non corrispondono agli incontri.");
            Require(string.IsNullOrEmpty(level04.NextLevelId),
                "Il Livello 4 non deve anticipare un livello successivo non implementato.");
        }

        private static void ValidateCampaignProgressionModel()
        {
            CampaignProgressData fresh = CampaignProgressStore.Defaults;
            Require(fresh.version == CampaignProgressStore.CurrentVersion,
                "La versione predefinita della campagna non è quella corrente.");
            Require(fresh.CompletedLevelCount == 0, "Un nuovo salvataggio deve iniziare da 0/10.");
            Require(CampaignProgressStore.IsLevelUnlocked(1, fresh),
                "Il Tutorial deve essere sbloccato in un nuovo salvataggio.");
            Require(!CampaignProgressStore.IsLevelUnlocked(2, fresh),
                "Il Livello 2 non deve essere sbloccato prima del Tutorial.");
            Require(CampaignProgressStore.GetNextSceneName(fresh) ==
                    SceneNames.World01Level01Tutorial,
                "GIOCA non indirizza al Tutorial in un nuovo salvataggio.");

            CampaignProgressData afterTutorial = fresh;
            afterTutorial.tutorialCompleted = true;
            Require(afterTutorial.CompletedLevelCount == 1,
                "Il Tutorial completato deve portare il conteggio a 1/10.");
            Require(CampaignProgressStore.IsLevelUnlocked(2, afterTutorial),
                "Il Tutorial non sblocca il Livello 2.");
            Require(!CampaignProgressStore.IsLevelUnlocked(3, afterTutorial),
                "Il Livello 3 si sblocca troppo presto.");
            Require(CampaignProgressStore.GetNextSceneName(afterTutorial) ==
                    SceneNames.World01Level02ThornGuardian,
                "GIOCA non indirizza al Livello 2 dopo il Tutorial.");

            CampaignProgressData afterLevel02 = afterTutorial;
            afterLevel02.encounter02Resolved = true;
            afterLevel02.encounter02Resolution = EncounterResolution.Saved;
            Require(afterLevel02.CompletedLevelCount == 2,
                "Il Livello 2 risolto deve portare il conteggio a 2/10.");
            Require(CampaignProgressStore.IsLevelUnlocked(3, afterLevel02),
                "Il Livello 2 non sblocca il Livello 3.");
            Require(!CampaignProgressStore.IsLevelUnlocked(4, afterLevel02),
                "Il Livello 4 si sblocca troppo presto.");
            Require(CampaignProgressStore.GetNextSceneName(afterLevel02) ==
                    SceneNames.World01Level03AshWatcher,
                "GIOCA non indirizza al Livello 3 dopo il Custode.");

            CampaignProgressData afterLevel03 = afterLevel02;
            afterLevel03.encounter03Resolved = true;
            afterLevel03.encounter03Resolution = EncounterResolution.Killed;
            Require(afterLevel03.CompletedLevelCount == 3,
                "Il Livello 3 risolto deve portare il conteggio a 3/10.");
            Require(CampaignProgressStore.IsLevelUnlocked(4, afterLevel03),
                "Il Livello 3 non sblocca il Livello 4.");
            Require(CampaignProgressStore.GetNextSceneName(afterLevel03) ==
                    SceneNames.World01Level04ThreefoldAssault,
                "GIOCA non indirizza al Livello 4 dopo il Vigile.");

            CampaignProgressData afterLevel04 = afterLevel03;
            afterLevel04.level04Completed = true;
            afterLevel04.level04BruteResolution = EncounterResolution.Saved;
            afterLevel04.level04WatcherResolution = EncounterResolution.Killed;
            afterLevel04.level04MaskResolution = EncounterResolution.Saved;
            Require(afterLevel04.CompletedLevelCount == 4,
                "Il Livello 4 completato deve portare il conteggio a 4/10.");
            Require(CampaignProgressStore.GetNextSceneName(afterLevel04) ==
                    SceneNames.MainMenu,
                "Dopo il contenuto corrente GIOCA deve restare nel menu.");
            Require(CampaignProgressStore.HasCompletedAllImplementedLevels(afterLevel04),
                "Il salvataggio non riconosce il completamento dei livelli disponibili.");

            for (int level = 5; level <= 10; level++)
            {
                Require(!CampaignProgressStore.IsLevelUnlocked(level, afterLevel04),
                    "Il livello futuro " + level + " risulta caricabile.");
            }
        }

        private static void ValidateHeroProgression()
        {
            Require(HeroProgressionRules.TutorialExperience == 60,
                "Il Tutorial deve assegnare 60 XP.");
            Require(HeroProgressionRules.ThornGuardianExperience == 90,
                "Il Livello 2 deve assegnare 90 XP.");
            Require(HeroProgressionRules.AshWatcherExperience == 150,
                "Il Livello 3 deve assegnare 150 XP.");
            Require(HeroProgressionRules.ThreefoldAssaultExperience == 200,
                "Il Livello 4 deve assegnare 200 XP.");

            Require(HeroProgressionRules.GetLevelForExperience(0) == 1,
                "0 XP deve corrispondere al Livello 1.");
            Require(HeroProgressionRules.GetLevelForExperience(100) == 2,
                "100 XP deve corrispondere al Livello 2.");
            Require(HeroProgressionRules.GetLevelForExperience(300) == 3,
                "300 XP deve corrispondere al Livello 3.");
            Require(HeroProgressionRules.GetLevelForExperience(500) == 4,
                "500 XP deve corrispondere al Livello 4.");

            AssertStats(CreateProgressAtExperience(0), 1, 100, 20, 32, 35, "Livello 1");
            AssertStats(CreateProgressAtExperience(100), 2, 110, 22, 35, 35, "Livello 2");
            AssertStats(CreateProgressAtExperience(300), 3, 120, 24, 38, 35, "Livello 3");
            AssertStats(CreateProgressAtExperience(500), 4, 130, 26, 41, 35, "Livello 4");

            HeroProgressData level03 = CreateProgressAtExperience(300);
            HeroCombatStats baseStats = HeroProgressionRules.GetCombatStats(level03);

            HeroProgressData attack = level03;
            attack.attackUpgradeRank = 1;
            HeroCombatStats attackStats = HeroProgressionRules.GetCombatStats(attack);
            Require(attackStats.AttackDamage == baseStats.AttackDamage + 8,
                "ATTACCO non applica esclusivamente il bonus +8 previsto.");
            Require(attackStats.MaxHp == baseStats.MaxHp &&
                    attackStats.TechniqueDamage == baseStats.TechniqueDamage &&
                    attackStats.TechniqueSplashPercent == baseStats.TechniqueSplashPercent &&
                    !attackStats.GuardBlocksAllDirectEnemyActions &&
                    !attackStats.AnalyzeRevealsAllEnemyIntents,
                "ATTACCO modifica proprietà che non gli appartengono.");

            HeroProgressData guard = level03;
            guard.guardUpgradeRank = 1;
            HeroCombatStats guardStats = HeroProgressionRules.GetCombatStats(guard);
            Require(guardStats.GuardBlocksAllDirectEnemyActions,
                "GUARDIA · BASTIONE non abilita il blocco della fase nemica.");
            RequireSameDamageStats(baseStats, guardStats, "GUARDIA · BASTIONE");

            HeroProgressData technique = level03;
            technique.techniqueUpgradeRank = 1;
            HeroCombatStats techniqueStats = HeroProgressionRules.GetCombatStats(technique);
            Require(techniqueStats.TechniqueDamage == baseStats.TechniqueDamage + 14 &&
                    techniqueStats.TechniqueSplashPercent == 55,
                "TECNICA deve applicare +14 e portare il danno secondario al 55%.");
            Require(techniqueStats.AttackDamage == baseStats.AttackDamage &&
                    techniqueStats.MaxHp == baseStats.MaxHp &&
                    !techniqueStats.GuardBlocksAllDirectEnemyActions &&
                    !techniqueStats.AnalyzeRevealsAllEnemyIntents,
                "TECNICA modifica proprietà non previste.");

            HeroProgressData analyze = level03;
            analyze.analyzeUpgradeRank = 1;
            HeroCombatStats analyzeStats = HeroProgressionRules.GetCombatStats(analyze);
            Require(analyzeStats.AnalyzeRevealsAllEnemyIntents &&
                    analyzeStats.AnalyzeAppliesExposed &&
                    analyzeStats.ExposedDamagePercent == 125,
                "ANALIZZA non abilita verità globale ed ESPOSTO 125%.");
            RequireSameDamageStats(baseStats, analyzeStats, "ANALIZZA");

            Require(HeroProgressionRules.GetEligibleMajorUpgradePoints(3) == 1 &&
                    HeroProgressionRules.GetEligibleMajorUpgradePoints(6) == 2 &&
                    HeroProgressionRules.GetEligibleMajorUpgradePoints(9) == 3,
                "I milestone 3/6/9 non preparano correttamente i tre gradi importanti.");
            HeroProgressData repeatedRank = HeroProgressStore.Defaults;
            repeatedRank.unspentMajorUpgradePoints = 3;
            repeatedRank.awardedMajorUpgradeMilestones = 3;
            bool acceptedRank01 = HeroProgressionRules.TrySpendMajorUpgradePoint(
                ref repeatedRank,
                HeroMajorUpgrade.Attack,
                out string rankFailure01);
            bool acceptedRank02 = HeroProgressionRules.TrySpendMajorUpgradePoint(
                ref repeatedRank,
                HeroMajorUpgrade.Attack,
                out string rankFailure02);
            bool acceptedRank03 = HeroProgressionRules.TrySpendMajorUpgradePoint(
                ref repeatedRank,
                HeroMajorUpgrade.Attack,
                out string rankFailure03);
            Require(acceptedRank01 && acceptedRank02 && acceptedRank03,
                "Lo stesso potenziamento non accetta tre gradi futuri: " +
                rankFailure01 + rankFailure02 + rankFailure03);
            Require(repeatedRank.GetUpgradeRank(HeroMajorUpgrade.Attack) == 3 &&
                    repeatedRank.unspentMajorUpgradePoints == 0,
                "Tre punti non producono esattamente ATTACCO grado 3.");
            HeroProgressData beforeRejectedRank = repeatedRank;
            repeatedRank.unspentMajorUpgradePoints = 1;
            Require(!HeroProgressionRules.TrySpendMajorUpgradePoint(
                        ref repeatedRank,
                        HeroMajorUpgrade.Attack,
                        out _) &&
                    repeatedRank.GetUpgradeRank(HeroMajorUpgrade.Attack) ==
                    beforeRejectedRank.GetUpgradeRank(HeroMajorUpgrade.Attack),
                "Un quarto grado viene accettato o modifica il grado massimo.");

            ValidateProgressPersistenceAndMigration();
        }

        private static void ValidateProgressPersistenceAndMigration()
        {
            using (new ProgressPrefsScope())
            {
                PlayerPrefs.SetFloat(LocalSettingsStore.MasterVolumeKey, 0.314159f);
                CampaignProgressStore.Reset();

                CampaignProgressStore.SetTutorialResolution(EncounterResolution.Saved);
                Require(HeroProgressStore.Load().totalExperience == 60,
                    "La prima vittoria del Tutorial non assegna 60 XP.");
                CampaignProgressStore.SetTutorialResolution(EncounterResolution.Killed);
                Require(HeroProgressStore.Load().totalExperience == 60,
                    "Rigiocare il Tutorial assegna nuovamente XP.");
                Require(CampaignProgressStore.TryGetEnemyResolution(
                            CampaignContentIds.Level01Tutorial,
                            CampaignContentIds.TutorialEnemy,
                            out EncounterResolution tutorialResolution) &&
                        tutorialResolution == EncounterResolution.Killed,
                    "Il replay del Tutorial non aggiorna la decisione morale confermata.");

                CampaignProgressStore.SetEncounterResolution(
                    CampaignEncounter.ThornGuardian,
                    EncounterResolution.Saved);
                Require(HeroProgressStore.Load().totalExperience == 150,
                    "La prima vittoria del Livello 2 non porta il totale a 150 XP.");
                CampaignProgressStore.SetEncounterResolution(
                    CampaignEncounter.ThornGuardian,
                    EncounterResolution.Killed);
                Require(HeroProgressStore.Load().totalExperience == 150,
                    "La rivincita del Livello 2 assegna nuovamente XP.");
                Require(CampaignProgressStore.TryGetEncounterResolution(
                            CampaignEncounter.ThornGuardian,
                            out EncounterResolution thornResolution) &&
                        thornResolution == EncounterResolution.Killed,
                    "La rivincita del Livello 2 non applica l'esito UCCISO confermato.");
                Require(CampaignProgressStore.TryGetEnemyResolution(
                            CampaignContentIds.Level02ThornGuardian,
                            CampaignContentIds.ThornGuardianEnemy,
                            out EncounterResolution stableThornResolution) &&
                        stableThornResolution == EncounterResolution.Killed,
                    "L'esito del Custode non è disponibile tramite gli id stabili usati dagli alleati.");

                int beforeUnrecordedDefeat = HeroProgressStore.Load().totalExperience;
                HeroProgressStore.Load();
                Require(HeroProgressStore.Load().totalExperience == beforeUnrecordedDefeat,
                    "Una lettura senza vittoria ha modificato gli XP (la sconfitta deve essere neutra).");

                CampaignProgressStore.SetEncounterResolution(
                    CampaignEncounter.AshWatcher,
                    EncounterResolution.Killed);
                CampaignProgressStore.SetEncounterResolution(
                    CampaignEncounter.AshWatcher,
                    EncounterResolution.Saved);
                Require(CampaignProgressStore.TryGetEncounterResolution(
                            CampaignEncounter.AshWatcher,
                            out EncounterResolution ashResolution) &&
                        ashResolution == EncounterResolution.Saved,
                    "La rivincita del Livello 3 non applica l'esito SALVATO confermato.");
                Require(CampaignProgressStore.TryGetEnemyResolution(
                            CampaignContentIds.Level03AshWatcher,
                            CampaignContentIds.AshWatcherEnemy,
                            out EncounterResolution stableAshResolution) &&
                        stableAshResolution == EncounterResolution.Saved,
                    "L'esito del Vigile non è disponibile tramite gli id stabili usati dagli alleati.");
                HeroProgressSnapshot level03 = HeroProgressStore.GetSnapshot();
                Require(level03.TotalExperience == 300 && level03.Level == 3,
                    "Dopo i primi tre scontri Hero01 deve avere 300 XP ed essere al Livello 3.");
                Require(level03.UnspentMajorUpgradePoints == 1,
                    "Al Livello 3 deve essere assegnato un solo punto importante.");

                Require(HeroProgressStore.TryChooseMajorUpgrade(
                        HeroMajorUpgrade.Attack,
                        out string firstFailure),
                    "Il primo punto importante non può essere speso: " + firstFailure);
                Require(!HeroProgressStore.TryChooseMajorUpgrade(
                        HeroMajorUpgrade.Technique,
                        out _),
                    "Lo stesso punto importante può essere speso due volte.");
                HeroProgressData persistedChoice = HeroProgressStore.Load();
                Require(persistedChoice.HasUpgrade(HeroMajorUpgrade.Attack) &&
                        persistedChoice.unspentMajorUpgradePoints == 0,
                    "La scelta del potenziamento non persiste dopo un nuovo caricamento.");

                Require(!CampaignProgressStore.HasSeenTutorial(
                            CampaignContentIds.TutorialMultiTarget),
                    "Il tutorial multi-bersaglio risulta già visto prima del primo accesso.");
                CampaignProgressStore.MarkTutorialSeen(CampaignContentIds.TutorialMultiTarget);
                CampaignProgressStore.MarkTutorialSeen(CampaignContentIds.TutorialMultiTarget);
                CampaignProgressData tutorialMarked = CampaignProgressStore.Load();
                Require(CampaignProgressStore.HasSeenTutorial(
                            CampaignContentIds.TutorialMultiTarget) &&
                        tutorialMarked.tutorialRecords.Count(record =>
                            record != null && record.seen &&
                            record.tutorialId == CampaignContentIds.TutorialMultiTarget) == 1,
                    "Il tutorial multi-bersaglio non persiste in modo idempotente.");

                CampaignProgressStore.RecordLevel04Resolutions(
                    EncounterResolution.Saved,
                    EncounterResolution.Killed,
                    EncounterResolution.Saved);
                Require(HeroProgressStore.GetSnapshot().TotalExperience == 500 &&
                        HeroProgressStore.GetSnapshot().Level == 4,
                    "Il Livello 4 non porta Hero01 a 500 XP e Livello 4.");
                CampaignProgressStore.SetEnemyResolution(
                    CampaignContentIds.Level04ThreefoldAssault,
                    CampaignContentIds.Level04BruteEnemy,
                    EncounterResolution.Killed);
                CampaignProgressStore.SetEnemyResolution(
                    CampaignContentIds.Level04ThreefoldAssault,
                    CampaignContentIds.Level04WatcherEnemy,
                    EncounterResolution.Saved);
                CampaignProgressStore.SetEnemyResolution(
                    CampaignContentIds.Level04ThreefoldAssault,
                    CampaignContentIds.Level04MaskEnemy,
                    EncounterResolution.Killed);
                Require(HeroProgressStore.Load().totalExperience == 500,
                    "Rigiocare il Livello 4 assegna nuovamente XP.");
                CampaignProgressData replayedLevel04 = CampaignProgressStore.Load();
                Require(replayedLevel04.level04BruteResolution == EncounterResolution.Killed &&
                        replayedLevel04.level04WatcherResolution == EncounterResolution.Saved &&
                        replayedLevel04.level04MaskResolution == EncounterResolution.Killed,
                    "Il replay del Livello 4 non sostituisce tutti e tre gli esiti confermati.");
                Require(CampaignProgressStore.TryGetEnemyResolution(
                            CampaignContentIds.Level04ThreefoldAssault,
                            CampaignContentIds.Level04BruteEnemy,
                            out EncounterResolution stableBruteResolution) &&
                        stableBruteResolution == EncounterResolution.Killed &&
                        CampaignProgressStore.TryGetEnemyResolution(
                            CampaignContentIds.Level04ThreefoldAssault,
                            CampaignContentIds.Level04WatcherEnemy,
                            out EncounterResolution stableWatcherResolution) &&
                        stableWatcherResolution == EncounterResolution.Saved &&
                        CampaignProgressStore.TryGetEnemyResolution(
                            CampaignContentIds.Level04ThreefoldAssault,
                            CampaignContentIds.Level04MaskEnemy,
                            out EncounterResolution stableMaskResolution) &&
                        stableMaskResolution == EncounterResolution.Killed,
                    "Il replay del Livello 4 non aggiorna i tre esiti tramite gli id stabili.");
                Require(!CampaignProgressStore.TryGetEncounterResolution(
                            CampaignEncounter.ThreefoldAssault,
                            out EncounterResolution unsupportedLevel04Resolution) &&
                        unsupportedLevel04Resolution == EncounterResolution.None,
                    "Il helper a esito singolo espone erroneamente il Livello 4, che richiede " +
                    "tre decisioni distinte.");

                CampaignProgressStore.RecordPlayerAction(PlayerCombatAction.Attack);
                CampaignProgressStore.RecordPlayerAction(PlayerCombatAction.Guard);
                CampaignProgressStore.RecordPlayerAction(PlayerCombatAction.Analyze);
                CampaignProgressStore.RecordPlayerAction(PlayerCombatAction.Attack);
                CampaignProgressStore.RecordPlayerAction(PlayerCombatAction.Attack);
                PlayerActionProfileSnapshot playerProfile =
                    CampaignProgressStore.GetPlayerActionProfile();
                Require(!CampaignProgressStore.CanEnemiesUsePlayerProfile(2) &&
                        CampaignProgressStore.CanEnemiesUsePlayerProfile(3) &&
                        CampaignProgressStore.CanEnemiesUsePlayerProfile(4) &&
                        playerProfile.TotalValidActions == 5 &&
                        playerProfile.AttackCount == 3 && playerProfile.GuardCount == 1 &&
                        playerProfile.TechniqueCount == 0 && playerProfile.AnalyzeCount == 1 &&
                        playerProfile.RecentActions.Count == 5 &&
                        playerProfile.DominantAction == PlayerCombatAction.Attack &&
                        playerProfile.CurrentRepeatCount == 2 &&
                        Mathf.Approximately(
                            playerProfile.GetUsageRatio(PlayerCombatAction.Attack),
                            0.6f),
                    "Il profilo persistente non conserva conteggi, dominante e ripetizioni " +
                    "o si attiva prima del terzo scontro.");

                CampaignProgressStore.Reset();
                Require(!CampaignProgressStore.Load().HasAnyProgress &&
                        HeroProgressStore.GetSnapshot().TotalExperience == 0 &&
                        HeroProgressStore.GetSnapshot().UnspentMajorUpgradePoints == 0 &&
                        CampaignProgressStore.GetPlayerActionProfile().TotalValidActions == 0 &&
                        !CampaignProgressStore.HasSeenTutorial(
                            CampaignContentIds.TutorialMultiTarget),
                    "Reset non cancella campagna, XP, potenziamenti, tutorial e profilo azioni.");
                Require(Mathf.Approximately(
                        PlayerPrefs.GetFloat(LocalSettingsStore.MasterVolumeKey),
                        0.314159f),
                    "Reset campagna ha cancellato o modificato le opzioni locali.");

                CampaignProgressData legacyCampaign = CampaignProgressStore.Defaults;
                legacyCampaign.saveVersion = 0;
                legacyCampaign.version = 1;
                legacyCampaign.tutorialCompleted = true;
                legacyCampaign.encounter02Resolved = true;
                legacyCampaign.encounter02Resolution = EncounterResolution.Saved;
                legacyCampaign.encounter03Resolved = true;
                legacyCampaign.encounter03Resolution = EncounterResolution.Killed;
                PlayerPrefs.SetString(
                    CampaignProgressStore.ProgressKey,
                    JsonUtility.ToJson(legacyCampaign));
                PlayerPrefs.DeleteKey(HeroProgressStore.ProgressKey);
                PlayerPrefs.Save();

                CampaignProgressData migratedCampaign = CampaignProgressStore.Load();
                CampaignProgressData migratedCampaignAgain = CampaignProgressStore.Load();
                HeroProgressSnapshot migratedHero = HeroProgressStore.GetSnapshot();
                Require(migratedCampaign.version == CampaignProgressStore.CurrentVersion &&
                        migratedCampaign.saveVersion == CampaignProgressStore.CurrentVersion &&
                        migratedCampaignAgain.version == CampaignProgressStore.CurrentVersion &&
                        migratedCampaignAgain.saveVersion == CampaignProgressStore.CurrentVersion &&
                        (migratedCampaign.levelRecords?.Count ?? 0) ==
                        (migratedCampaignAgain.levelRecords?.Count ?? 0) &&
                        (migratedCampaign.moralDecisions?.Count ?? 0) ==
                        (migratedCampaignAgain.moralDecisions?.Count ?? 0) &&
                        (migratedCampaign.tutorialRecords?.Count ?? 0) ==
                        (migratedCampaignAgain.tutorialRecords?.Count ?? 0),
                    "Il salvataggio campagna precedente non viene migrato alla versione corrente.");
                Require(migratedHero.TotalExperience == 300 && migratedHero.Level == 3 &&
                        migratedHero.UnspentMajorUpgradePoints == 1,
                    "Un vecchio salvataggio con Livello 3 risolto non migra a 300 XP, " +
                    "Livello 3 e un punto importante.");

                const string legacyHeroJson =
                    "{\"version\":1,\"totalExperience\":300," +
                    "\"tutorialRewardClaimed\":true,\"encounter02RewardClaimed\":true," +
                    "\"encounter03RewardClaimed\":true,\"level04RewardClaimed\":false," +
                    "\"selectedMajorUpgradesMask\":4," +
                    "\"unspentMajorUpgradePoints\":0," +
                    "\"awardedMajorUpgradeMilestones\":1}";
                PlayerPrefs.SetString(HeroProgressStore.ProgressKey, legacyHeroJson);
                PlayerPrefs.Save();
                HeroProgressData migratedLegacyUpgrade = HeroProgressStore.Load();
                HeroProgressData migratedLegacyUpgradeAgain = HeroProgressStore.Load();
                Require(migratedLegacyUpgrade.version == HeroProgressStore.CurrentVersion &&
                        migratedLegacyUpgrade.GetUpgradeRank(HeroMajorUpgrade.Technique) == 1 &&
                        migratedLegacyUpgrade.unspentMajorUpgradePoints == 0 &&
                        migratedLegacyUpgrade.selectedMajorUpgradesMask ==
                        (int)HeroMajorUpgrade.Technique &&
                        migratedLegacyUpgradeAgain.GetUpgradeRank(HeroMajorUpgrade.Technique) == 1,
                    "Il mask potenziamenti v1 non migra in modo idempotente al grado 1 v2.");

                HeroProgressData currentRankSave = HeroProgressStore.Defaults;
                currentRankSave.totalExperience = 300;
                currentRankSave.tutorialRewardClaimed = true;
                currentRankSave.encounter02RewardClaimed = true;
                currentRankSave.encounter03RewardClaimed = true;
                currentRankSave.analyzeUpgradeRank = 1;
                currentRankSave.awardedMajorUpgradeMilestones = 1;
                PlayerPrefs.SetString(
                    HeroProgressStore.ProgressKey,
                    JsonUtility.ToJson(currentRankSave));
                PlayerPrefs.Save();
                HeroProgressData normalizedCurrentRank = HeroProgressStore.Load();
                Require(normalizedCurrentRank.GetUpgradeRank(HeroMajorUpgrade.Analyze) == 1 &&
                        normalizedCurrentRank.selectedMajorUpgradesMask ==
                        (int)HeroMajorUpgrade.Analyze &&
                        normalizedCurrentRank.unspentMajorUpgradePoints == 0,
                    "Il salvataggio v2 non ricostruisce il mirror legacy dai gradi espliciti.");
            }
        }

        private static void ValidateSingleEncounterAnalyzeAndExposed()
        {
            EnemyProfile profile = new EnemyProfile(
                "VALIDATION_SINGLE",
                "Bersaglio di prova",
                "Costrutto",
                50,
                Veyra.Combat.Encounter.EnemyMood.Guardingo,
                1);

            EncounterBattleState regular = new EncounterBattleState(
                new EncounterRules(enemyMaxHp: 200),
                profile,
                new EnemyMemory(2, 6));
            EncounterActionResult regularAnalyze = regular.ResolvePlayerAction(
                EncounterAction.Analyze);
            EncounterActionResult regularAttack = regular.ResolvePlayerAction(
                EncounterAction.Attack);
            Require(regularAnalyze.Accepted && !regularAnalyze.ConsumesTurn &&
                    !regular.IsEnemyExposed && regularAttack.DamageDealt == 20,
                "ANALIZZA normale applica ESPOSTO o consuma il turno nello scontro singolo.");

            EncounterBattleState upgraded = new EncounterBattleState(
                new EncounterRules(
                    enemyMaxHp: 200,
                    analyzeAppliesExposed: true,
                    exposedDamagePercent: 125),
                profile,
                new EnemyMemory(2, 6));
            int historyBefore = upgraded.Memory.CompletedActions.Count;
            int cooldownBefore = upgraded.TechniqueCooldownRemaining;
            Require(upgraded.ResolvePlayerAction(EncounterAction.Analyze).Accepted &&
                    upgraded.ResolvePlayerAction(EncounterAction.Analyze).Accepted &&
                    upgraded.IsEnemyExposed &&
                    upgraded.Memory.CompletedActions.Count == historyBefore &&
                    upgraded.TechniqueCooldownRemaining == cooldownBefore,
                "Vista della Corruzione non applica un ESPOSTO singolo senza turno, memoria o cooldown.");
            upgraded.ApplyExternalNonLethalDamage(8);
            Require(upgraded.IsEnemyExposed,
                "Il supporto esterno consuma erroneamente ESPOSTO.");
            EncounterActionResult exposedAttack = upgraded.ResolvePlayerAction(
                EncounterAction.Attack);
            EncounterActionResult followingAttack = upgraded.ResolvePlayerAction(
                EncounterAction.Attack);
            Require(exposedAttack.DamageDealt == 25 && !upgraded.IsEnemyExposed &&
                    followingAttack.DamageDealt == 20,
                "ESPOSTO singolo non aumenta una sola volta il prossimo danno del 25%.");
            upgraded.ResolvePlayerAction(EncounterAction.Analyze);
            upgraded.Reset();
            Require(!upgraded.IsEnemyExposed,
                "Reset non cancella ESPOSTO nello scontro singolo.");

            EncounterBattleState guarded = new EncounterBattleState(
                new EncounterRules(
                    enemyMaxHp: 200,
                    analyzeAppliesExposed: true,
                    exposedDamagePercent: 125),
                profile,
                new EnemyMemory(2, 6));
            guarded.ResolvePlayerAction(EncounterAction.Analyze);
            guarded.ResolveEnemyIntent(EnemyIntent.Guard);
            EncounterActionResult guardedAttack = guarded.ResolvePlayerAction(
                EncounterAction.Attack);
            Require(guardedAttack.DamageDealt == 0 &&
                    guardedAttack.EnemyGuardReducedDamage &&
                    !guarded.IsEnemyExposed,
                "ESPOSTO e Guardia nemica non rispettano l'ordine: bonus calcolato, poi colpo bloccato a 0.");
        }

        private static void ValidateSavedAllySupport()
        {
            SavedAllySupportDefinition thornDefinition =
                SavedAllySupportCatalog.CreateThornGuardian();
            SavedAllySupportDefinition ashDefinition =
                SavedAllySupportCatalog.CreateAshWatcher();
            Require(thornDefinition.AttackDamage == 8,
                "Il Custode salvato deve infliggere 8 danni non letali.");
            Require(ashDefinition.AttackDamage == 10,
                "Il Vigile salvato deve infliggere 10 danni non letali.");

            var targets = new List<SavedAllyTargetSnapshot>
            {
                new SavedAllyTargetSnapshot("A", 0, 20, 50),
                new SavedAllyTargetSnapshot("B", 1, 10, 100),
                new SavedAllyTargetSnapshot("C", 2, 1, 40)
            };
            SavedAllySupport thorn = new SavedAllySupport(thornDefinition);
            Require(!thorn.CanIntervene(1, targets),
                "Il Custode interviene prima del turno deterministico previsto.");
            Require(thorn.TryIntervene(2, targets, out SavedAllySupportAction action),
                "Il Custode non interviene quando sono soddisfatte le condizioni.");
            Require(action.TargetId == "B" && action.TargetListIndex == 1,
                "La selezione del bersaglio del supporto non è deterministica.");
            Require(action.RequestedDamage == 8 && action.AppliedDamage == 8 &&
                    action.TargetHpAfter == 2,
                "Il danno del Custode non è applicato correttamente.");
            Require(!action.ConsumesHeroTurn && !action.AdvancesTechniqueCooldown &&
                    !action.RecordsHeroAction && !action.CanDefeatTarget,
                "Il supporto altera turno, cooldown, memoria di Hero01 o può sconfiggere.");
            Require(!thorn.TryIntervene(3, targets, out _),
                "Lo stesso alleato può intervenire più di una volta nello scontro.");

            Require(SavedAllySupport.CalculateNonLethalDamage(5, 10) == 4 &&
                    SavedAllySupport.CalculateNonLethalDamage(1, 10) == 0,
                "Il supporto può portare un bersaglio sotto 1 HP.");

            SavedAllySupport dialogueSupport = new SavedAllySupport(thornDefinition);
            Require(dialogueSupport.TryGetOpeningDialogue(out SavedAllyDialogueLine opening) &&
                    opening.HasText,
                "Il dialogo contestuale iniziale del Custode è assente.");
            Require(!dialogueSupport.TryGetOpeningDialogue(out _),
                "Lo stesso dialogo contestuale supera il limite d'uso.");
            Require(!dialogueSupport.TryGetHeroDifficultyDialogue(50, 100, out _),
                "Il dialogo di difficoltà appare quando Hero01 non è sotto metà vita.");
            Require(dialogueSupport.TryGetHeroDifficultyDialogue(49, 100, out _),
                "Il dialogo di difficoltà non appare sotto metà vita.");
            Require(dialogueSupport.TryGetEndingDialogue(out SavedAllyDialogueLine thornEnding) &&
                    thornEnding.HasText &&
                    !dialogueSupport.TryGetEndingDialogue(out _),
                "Il dialogo finale del Custode è assente o supera il limite d'uso.");
            SavedAllySupport ashDialogueSupport = new SavedAllySupport(ashDefinition);
            Require(ashDialogueSupport.TryGetEndingDialogue(out SavedAllyDialogueLine ashEnding) &&
                    ashEnding.HasText &&
                    !ashDialogueSupport.TryGetEndingDialogue(out _),
                "Il dialogo finale del Vigile è assente o supera il limite d'uso.");

            CampaignProgressData saved = CampaignProgressStore.Defaults;
            saved.encounter02Resolved = true;
            saved.encounter02Resolution = EncounterResolution.Saved;
            saved.encounter03Resolved = true;
            saved.encounter03Resolution = EncounterResolution.Saved;
            Require(IsAllyAvailable(saved, SavedAllyId.ThornGuardian) &&
                    IsAllyAvailable(saved, SavedAllyId.AshWatcher),
                "Gli esiti Saved non rendono disponibili gli alleati corretti.");
            saved.encounter02Resolution = EncounterResolution.Killed;
            saved.encounter03Resolution = EncounterResolution.Killed;
            Require(!IsAllyAvailable(saved, SavedAllyId.ThornGuardian) &&
                    !IsAllyAvailable(saved, SavedAllyId.AshWatcher),
                "Gli alleati risultano disponibili dopo un esito Killed.");
        }

        private static void ValidateMultiEnemyModel()
        {
            IReadOnlyList<MultiEnemyProfile> roster = Level04EnemyRoster.Create();
            Require(roster.Count == 3, "Il roster del Livello 4 deve contenere tre nemici.");
            Require(roster.Select(profile => profile.EnemyId).Distinct().Count() == 3,
                "Gli id dei tre nemici non sono univoci.");

            AssertProfile(
                roster[0],
                Level04EnemyRoster.BruteId,
                "Bruto delle Radici",
                "Umano Corrotto",
                50,
                74,
                Veyra.Combat.Encounter.EnemyMood.Arrabbiato,
                1,
                EnemyAltitude.Ground,
                10,
                0,
                14,
                EnemyBehaviorTraits.Aggressive);
            AssertProfile(
                roster[1],
                Level04EnemyRoster.WatcherId,
                "Veglia Sospesa",
                "Spirito Corrotto",
                45,
                61,
                Veyra.Combat.Encounter.EnemyMood.Guardingo,
                2,
                EnemyAltitude.Flying,
                6,
                18,
                0,
                EnemyBehaviorTraits.Patient);
            AssertProfile(
                roster[2],
                Level04EnemyRoster.MaskId,
                "Maschera del Vento",
                "Creatura Mutaforma",
                38,
                69,
                Veyra.Combat.Encounter.EnemyMood.Felice,
                3,
                EnemyAltitude.Flying,
                8,
                0,
                0,
                EnemyBehaviorTraits.Deceptive);
            Require(roster.Count(profile => profile.Altitude == EnemyAltitude.Ground) == 1 &&
                    roster.Count(profile => profile.Altitude == EnemyAltitude.Flying) == 2,
                "Il roster deve avere un nemico a terra e due in volo.");
            ValidateTraitWeights();
            ValidateDeceptionSettings();

            MultiEnemyBattleRules rules = MultiEnemyBattleRules.Level04HeroAtLevel3;
            MultiEnemyBattleState initialSelection =
                CreateBattle(HeroSkillUpgrades.None, 4404);
            Require(string.IsNullOrEmpty(initialSelection.SelectedEnemyId) &&
                    initialSelection.SelectedEnemy == null &&
                    !initialSelection.HasValidSelectedTarget &&
                    initialSelection.RequiresTargetSelection &&
                    initialSelection.ActiveEnemyCount == 3,
                "Il Livello 4 deve iniziare senza bersaglio e richiedere una scelta esplicita.");
            Require(initialSelection.CanUseHeroAction(MultiEnemyHeroAction.Guard) &&
                    !initialSelection.CanUseHeroAction(MultiEnemyHeroAction.Attack) &&
                    !initialSelection.CanUseHeroAction(MultiEnemyHeroAction.Technique) &&
                    !initialSelection.CanUseHeroAction(MultiEnemyHeroAction.Analyze),
                "Senza bersaglio soltanto GUARDIA deve essere disponibile.");
            Require(!initialSelection.ResolveHeroAction(MultiEnemyHeroAction.Attack).Accepted &&
                    !initialSelection.ResolveHeroAction(MultiEnemyHeroAction.Technique).Accepted &&
                    !initialSelection.ResolveHeroAction(MultiEnemyHeroAction.Analyze).Accepted &&
                    initialSelection.Phase == MultiEnemyBattlePhase.HeroTurn &&
                    string.IsNullOrEmpty(initialSelection.SelectedEnemyId),
                "Un'azione mirata senza selezione viene accettata o altera il turno.");
            Require(!initialSelection.SelectTarget("UNKNOWN_ENEMY"),
                "È possibile selezionare un id nemico inesistente.");

            MultiEnemyBattleState attackState = CreateBattle(HeroSkillUpgrades.None, 4404);
            Require(attackState.SelectTarget(Level04EnemyRoster.WatcherId),
                "Non è possibile selezionare il Vigile come bersaglio.");
            Require(attackState.IsTargetSelected(Level04EnemyRoster.WatcherId) &&
                    attackState.HasValidSelectedTarget &&
                    !attackState.RequiresTargetSelection &&
                    attackState.CanUseHeroAction(MultiEnemyHeroAction.Attack),
                "La selezione esplicita non abilita le azioni mirate sul Vigile.");
            int bruteBefore = attackState.GetEnemy(Level04EnemyRoster.BruteId).CurrentHp;
            int watcherBefore = attackState.GetEnemy(Level04EnemyRoster.WatcherId).CurrentHp;
            int maskBefore = attackState.GetEnemy(Level04EnemyRoster.MaskId).CurrentHp;
            HeroActionResolution attack = attackState.ResolveHeroAction(
                MultiEnemyHeroAction.Attack,
                Level04EnemyRoster.WatcherId);
            Require(attack.Accepted && attack.DamageEvents.Count == 1,
                "ATTACCO deve produrre un solo evento di danno.");
            Require(attackState.GetEnemy(Level04EnemyRoster.BruteId).CurrentHp == bruteBefore &&
                    attackState.GetEnemy(Level04EnemyRoster.WatcherId).CurrentHp ==
                    watcherBefore - rules.HeroAttackDamage &&
                    attackState.GetEnemy(Level04EnemyRoster.MaskId).CurrentHp == maskBefore,
                "ATTACCO colpisce più di un bersaglio o ignora quello selezionato.");
            EnemyPhaseResolution attackEnemyPhase = attackState.ResolveEnemyPhase();
            Require(attackEnemyPhase.Accepted && !attackEnemyPhase.HeroDefeated &&
                    attackState.IsTargetSelected(Level04EnemyRoster.WatcherId) &&
                    attackState.HasValidSelectedTarget,
                "Un bersaglio ancora attivo non resta selezionato nel turno successivo.");

            MultiEnemyBattleState techniqueState = CreateBattle(HeroSkillUpgrades.None, 4404);
            Require(techniqueState.SelectTarget(Level04EnemyRoster.BruteId),
                "Il test di TECNICA non riesce a selezionare il Bruto.");
            HeroActionResolution technique = techniqueState.ResolveHeroAction(
                MultiEnemyHeroAction.Technique,
                Level04EnemyRoster.BruteId);
            Require(technique.Accepted && technique.DamageEvents.Count == 3,
                "TECNICA deve colpire il bersaglio e i due nemici secondari.");
            Require(technique.DamageEvents.Count(item => !item.WasSplash &&
                    item.RequestedDamage == 38) == 1,
                "Il danno diretto base di TECNICA non è 38.");
            Require(technique.DamageEvents.Count(item => item.WasSplash &&
                    item.RequestedDamage == 13) == 2,
                "Il danno secondario base di TECNICA non corrisponde al 35%.");
            Require(techniqueState.TechniqueCooldownRemaining == 2,
                "TECNICA non avvia il cooldown di due turni.");

            MultiEnemyBattleState upgradedTechniqueState =
                CreateBattle(HeroSkillUpgrades.Technique, 4404);
            Require(upgradedTechniqueState.SelectTarget(Level04EnemyRoster.BruteId),
                "Il test di TECNICA potenziata non riesce a selezionare il Bruto.");
            HeroActionResolution upgradedTechnique = upgradedTechniqueState.ResolveHeroAction(
                MultiEnemyHeroAction.Technique,
                Level04EnemyRoster.BruteId);
            Require(upgradedTechnique.DamageEvents.Count(item => !item.WasSplash &&
                    item.RequestedDamage == 52) == 1,
                "Il potenziamento TECNICA non applica +14 al danno diretto.");
            Require(upgradedTechnique.DamageEvents.Count(item => item.WasSplash &&
                    item.RequestedDamage == 29) == 2,
                "Il potenziamento TECNICA non applica il 55% al danno secondario.");

            MultiEnemyBattleRules decisiveRules = new MultiEnemyBattleRules(10000, 1000, 1000);
            MultiEnemyBattleState incapacitation = new MultiEnemyBattleState(
                decisiveRules,
                Level04EnemyRoster.Create(),
                HeroSkillUpgrades.None,
                4404);
            Require(incapacitation.SelectTarget(Level04EnemyRoster.BruteId),
                "Il test d'incapacitazione non riesce a selezionare il Bruto.");
            HeroActionResolution firstDefeat = incapacitation.ResolveHeroAction(
                MultiEnemyHeroAction.Attack,
                Level04EnemyRoster.BruteId);
            Require(firstDefeat.Accepted && !firstDefeat.AllEnemiesIncapacitated &&
                    incapacitation.Phase == MultiEnemyBattlePhase.AwaitingMoralChoices &&
                    incapacitation.PendingMoralEnemyId == Level04EnemyRoster.BruteId,
                "La scelta immediata non si apre dopo avere incapacitato il primo nemico.");
            Require(incapacitation.GetPlan(Level04EnemyRoster.BruteId) == null &&
                    incapacitation.GetVisibleIntent(Level04EnemyRoster.BruteId) == null &&
                    incapacitation.CurrentPlans.All(plan =>
                        plan.EnemyId != Level04EnemyRoster.BruteId),
                "Il piano e l'intenzione del nemico incapacitato non vengono rimossi subito.");
            Require(string.IsNullOrEmpty(incapacitation.SelectedEnemyId) &&
                    !incapacitation.HasValidSelectedTarget &&
                    incapacitation.ActiveEnemyCount == 2,
                "Il bersaglio incapacitato resta selezionato quando esistono due alternative.");
            MoralChoiceResolution firstChoice = incapacitation.ResolveMoralChoice(
                Level04EnemyRoster.BruteId,
                EnemyMoralOutcome.Saved);
            MoralChoiceResolution duplicateChoice = incapacitation.ResolveMoralChoice(
                Level04EnemyRoster.BruteId,
                EnemyMoralOutcome.Killed);
            Require(firstChoice.Accepted && !firstChoice.AllChoicesCompleted &&
                    !duplicateChoice.Accepted &&
                    incapacitation.Phase == MultiEnemyBattlePhase.EnemyPhase,
                "La prima scelta morale non riprende correttamente lo scontro.");
            EnemyPhaseResolution firstEnemyPhase = incapacitation.ResolveEnemyPhase();
            Require(firstEnemyPhase.Accepted && firstEnemyPhase.Actions.All(item =>
                    item.Plan.EnemyId != Level04EnemyRoster.BruteId),
                "La fase nemica conserva un'azione per il nemico già incapacitato.");
            Require(incapacitation.RequiresTargetSelection &&
                    !incapacitation.CanSelectTarget(Level04EnemyRoster.BruteId) &&
                    !incapacitation.SelectTarget(Level04EnemyRoster.BruteId),
                "Nel nuovo turno il nemico incapacitato è ancora selezionabile.");

            Require(incapacitation.SelectTarget(Level04EnemyRoster.WatcherId),
                "Il test non riesce a selezionare il secondo bersaglio attivo.");
            HeroActionResolution secondDefeat = incapacitation.ResolveHeroAction(
                MultiEnemyHeroAction.Attack,
                Level04EnemyRoster.WatcherId);
            Require(secondDefeat.Accepted &&
                    incapacitation.GetEnemy(Level04EnemyRoster.WatcherId).IsIncapacitated &&
                    incapacitation.ActiveEnemyCount == 1 &&
                    incapacitation.IsTargetSelected(Level04EnemyRoster.MaskId) &&
                    incapacitation.LastAutoSelectedEnemyId == Level04EnemyRoster.MaskId &&
                    incapacitation.PendingMoralEnemyId == Level04EnemyRoster.WatcherId,
                "L'unico nemico attivo non viene selezionato automaticamente con stato esplicito.");
            MoralChoiceResolution secondChoice = incapacitation.ResolveMoralChoice(
                Level04EnemyRoster.WatcherId,
                EnemyMoralOutcome.Killed);
            Require(secondChoice.Accepted &&
                    incapacitation.Phase == MultiEnemyBattlePhase.EnemyPhase,
                "La seconda scelta morale non riprende correttamente lo scontro.");
            EnemyPhaseResolution secondEnemyPhase = incapacitation.ResolveEnemyPhase();
            Require(secondEnemyPhase.Accepted &&
                    incapacitation.IsTargetSelected(Level04EnemyRoster.MaskId) &&
                    !incapacitation.RequiresTargetSelection &&
                    !incapacitation.CanSelectTarget(Level04EnemyRoster.WatcherId),
                "L'auto-selezione non persiste o permette di riselezionare un incapacitato.");

            HeroActionResolution thirdDefeat = incapacitation.ResolveHeroAction(
                MultiEnemyHeroAction.Attack,
                Level04EnemyRoster.MaskId);
            Require(thirdDefeat.Accepted,
                "Il test non riesce a colpire l'unico bersaglio auto-selezionato.");
            for (int attempt = 0;
                 attempt < 8 && !incapacitation.AllEnemiesIncapacitated;
                 attempt++)
            {
                Require(incapacitation.Phase == MultiEnemyBattlePhase.EnemyPhase,
                    "Il colpo bloccato dalla Guardia non passa correttamente al turno nemico.");
                Require(incapacitation.ResolveEnemyPhase().Accepted,
                    "La fase nemica dopo una Guardia non viene risolta.");
                Require(incapacitation.SelectTarget(Level04EnemyRoster.MaskId),
                    "La Maschera attiva non può essere riselezionata dopo avere bloccato un colpo.");
                thirdDefeat = incapacitation.ResolveHeroAction(
                    MultiEnemyHeroAction.Attack,
                    Level04EnemyRoster.MaskId);
                Require(thirdDefeat.Accepted,
                    "Il nuovo tentativo contro la Maschera è stato rifiutato.");
            }
            Require(incapacitation.AllEnemiesIncapacitated &&
                    incapacitation.Phase == MultiEnemyBattlePhase.AwaitingMoralChoices,
                "La scelta morale finale non inizia quando il terzo nemico è incapacitato.");
            MoralChoiceResolution thirdChoice = incapacitation.ResolveMoralChoice(
                Level04EnemyRoster.MaskId,
                EnemyMoralOutcome.Saved);
            Require(thirdChoice.Accepted && thirdChoice.AllChoicesCompleted &&
                    incapacitation.Phase == MultiEnemyBattlePhase.Completed,
                "Le tre decisioni morali non sono indipendenti, univoche e completabili.");

            ValidateGuardAndBastion();
            ValidateAnalyzeAndExposed();
            ValidateBattleCapsAndChargeLegality();

            Require(CanWinWithoutSupport(HeroSkillUpgrades.None) &&
                    CanWinWithoutSupport(HeroSkillUpgrades.Attack) &&
                    CanWinWithoutSupport(HeroSkillUpgrades.Guard) &&
                    CanWinWithoutSupport(HeroSkillUpgrades.Technique) &&
                    CanWinWithoutSupport(HeroSkillUpgrades.Analyze),
                "Il Livello 4 non risulta vincibile senza alleati con ogni scelta di potenziamento.");
        }

        private static void ValidateTraitWeights()
        {
            EnemyTraitWeights aggressiveWeights = new EnemyTraitWeights(1d, 0.10d, 0.10d);
            EnemyTraitWeights patientWeights = new EnemyTraitWeights(0.10d, 1d, 0.10d);
            MultiEnemyProfile aggressiveWeighted = CreateCombinedTraitProfile(aggressiveWeights);
            MultiEnemyProfile patientWeighted = CreateCombinedTraitProfile(patientWeights);
            MultiEnemyProfile zeroWeighted = CreateCombinedTraitProfile(
                new EnemyTraitWeights(1d, 0d, 0d));

            EnemyBehaviorTraits combined = EnemyBehaviorTraits.Aggressive |
                                           EnemyBehaviorTraits.Patient |
                                           EnemyBehaviorTraits.Deceptive;
            Require(aggressiveWeighted.Traits == combined && patientWeighted.Traits == combined &&
                    aggressiveWeighted.HasTrait(EnemyBehaviorTraits.Aggressive) &&
                    aggressiveWeighted.HasTrait(EnemyBehaviorTraits.Patient) &&
                    aggressiveWeighted.HasTrait(EnemyBehaviorTraits.Deceptive) &&
                    Math.Abs(aggressiveWeighted.TraitWeights.Aggressive - 1d) < 0.000001d &&
                    Math.Abs(aggressiveWeighted.TraitWeights.Patient - 0.10d) < 0.000001d &&
                    Math.Abs(aggressiveWeighted.TraitWeights.Deceptive - 0.10d) < 0.000001d &&
                    Math.Abs(patientWeighted.TraitWeights.Aggressive - 0.10d) < 0.000001d &&
                    Math.Abs(patientWeighted.TraitWeights.Patient - 1d) < 0.000001d &&
                    Math.Abs(patientWeighted.TraitWeights.Deceptive - 0.10d) < 0.000001d,
                "I pesi dei tratti combinabili non vengono conservati nel profilo.");
            Require(zeroWeighted.HasTrait(EnemyBehaviorTraits.Aggressive) &&
                    !zeroWeighted.HasTrait(EnemyBehaviorTraits.Patient) &&
                    !zeroWeighted.HasTrait(EnemyBehaviorTraits.Deceptive),
                "Un tratto con peso zero continua a risultare attivo nel profilo combinato.");

            bool observedDifferentPlan = false;
            for (int seed = 1; seed <= 512; seed++)
            {
                MultiEnemyBattleState aggressiveState = new MultiEnemyBattleState(
                    MultiEnemyBattleRules.Level04HeroAtLevel3,
                    new[] { aggressiveWeighted },
                    HeroSkillUpgrades.None,
                    seed);
                MultiEnemyBattleState patientState = new MultiEnemyBattleState(
                    MultiEnemyBattleRules.Level04HeroAtLevel3,
                    new[] { patientWeighted },
                    HeroSkillUpgrades.None,
                    seed);
                if (aggressiveState.CurrentPlans.Single().TrueIntent !=
                    patientState.CurrentPlans.Single().TrueIntent)
                {
                    observedDifferentPlan = true;
                    break;
                }
            }

            Require(observedDifferentPlan,
                "Due profili identici con pesi aggressivo/paziente diversi producono sempre " +
                "lo stesso piano: i pesi non risultano usati dalla pianificazione.");
            RequireThrows<ArgumentOutOfRangeException>(
                () => new EnemyTraitWeights(double.NaN, 0d, 0d),
                "EnemyTraitWeights accetta un peso NaN.");
            RequireThrows<ArgumentOutOfRangeException>(
                () => new EnemyTraitWeights(-0.01d, 0d, 0d),
                "EnemyTraitWeights accetta un peso negativo.");
            RequireThrows<ArgumentOutOfRangeException>(
                () => new EnemyTraitWeights(double.PositiveInfinity, 0d, 0d),
                "EnemyTraitWeights accetta un peso infinito.");
        }

        private static void ValidateDeceptionSettings()
        {
            EnemyDeceptionSettings defaults = EnemyDeceptionSettings.Default;
            Require(Math.Abs(EnemyDeceptionSettings.HardMaximumBluffProbability - 0.35d) <
                    0.000001d &&
                    EnemyDeceptionSettings.HardMinimumTurnsBetweenBluffs == 3 &&
                    Math.Abs(defaults.BluffProbability - 0.30d) < 0.000001d &&
                    defaults.MinimumTurnsBetweenBluffs == 3 &&
                    Math.Abs(defaults.FeintIntentWeight - 0.20d) < 0.000001d,
                "Le impostazioni predefinite di FINTA/bluff non rispettano i limiti di equità.");

            var custom = new EnemyDeceptionSettings(0.12d, 5, 0.44d);
            var profile = new MultiEnemyProfile(
                "VALIDATION_DECEPTION",
                "Ingannatore di prova",
                "Costrutto",
                100,
                50,
                Veyra.Combat.Encounter.EnemyMood.Guardingo,
                3,
                EnemyAltitude.Flying,
                8,
                0,
                0,
                EnemyBehaviorTraits.Deceptive,
                new EnemyTraitWeights(0d, 0d, 1d),
                custom);
            Require(Math.Abs(profile.DeceptionSettings.BluffProbability - 0.12d) < 0.000001d &&
                    profile.DeceptionSettings.MinimumTurnsBetweenBluffs == 5 &&
                    Math.Abs(profile.DeceptionSettings.FeintIntentWeight - 0.44d) < 0.000001d,
                "Il profilo nemico non conserva la configurazione personalizzata di bluff/FINTA.");

            RequireThrows<ArgumentOutOfRangeException>(
                () => new EnemyDeceptionSettings(0.351d, 3, 0.20d),
                "EnemyDeceptionSettings accetta una probabilità oltre il 35%.");
            RequireThrows<ArgumentOutOfRangeException>(
                () => new EnemyDeceptionSettings(0.20d, 2, 0.20d),
                "EnemyDeceptionSettings accetta meno di tre turni tra due bluff.");
            RequireThrows<ArgumentOutOfRangeException>(
                () => new EnemyDeceptionSettings(0.20d, 3, 1.01d),
                "EnemyDeceptionSettings accetta un peso FINTA superiore a uno.");
        }

        private static MultiEnemyProfile CreateCombinedTraitProfile(EnemyTraitWeights weights)
        {
            return new MultiEnemyProfile(
                "VALIDATION_COMBINED_TRAITS",
                "Profilo combinato di prova",
                "Costrutto",
                100,
                50,
                Veyra.Combat.Encounter.EnemyMood.Guardingo,
                3,
                EnemyAltitude.Flying,
                8,
                18,
                14,
                EnemyBehaviorTraits.Aggressive |
                EnemyBehaviorTraits.Patient |
                EnemyBehaviorTraits.Deceptive,
                weights);
        }

        private static void ValidateGuardAndBastion()
        {
            int usefulSeed = FindSeedWithAtLeastTwoOffensivePlans();
            Require(usefulSeed >= 0,
                "Non è stato trovato un seed capace di esercitare due azioni offensive.");

            MultiEnemyBattleState normalGuard = CreateBattle(HeroSkillUpgrades.None, usefulSeed);
            Require(normalGuard.ResolveHeroAction(MultiEnemyHeroAction.Guard).Accepted,
                "GUARDIA base non viene accettata.");
            EnemyPhaseResolution normalPhase = normalGuard.ResolveEnemyPhase();
            int normalOffensive = normalPhase.Actions.Count(item => IsOffensive(item.Plan.TrueIntent));
            int normalBlocked = normalPhase.Actions.Count(item =>
                IsOffensive(item.Plan.TrueIntent) && item.BlockedByGuard);
            Require(normalOffensive >= 2 && normalBlocked == 1,
                "GUARDIA base deve bloccare una sola azione offensiva diretta.");

            MultiEnemyBattleState bastion = CreateBattle(HeroSkillUpgrades.Guard, usefulSeed);
            HeroActionResolution guardAction = bastion.ResolveHeroAction(MultiEnemyHeroAction.Guard);
            EnemyPhaseResolution bastionPhase = bastion.ResolveEnemyPhase();
            Require(guardAction.Accepted && guardAction.BastionPrepared,
                "Il potenziamento BASTIONE non viene preparato.");
            Require(bastionPhase.Actions.Where(item => IsOffensive(item.Plan.TrueIntent)).All(
                    item => item.BlockedByGuard && item.DamageDealt == 0),
                "BASTIONE non blocca tutte le azioni nemiche dirette della fase.");
        }

        private static void ValidateAnalyzeAndExposed()
        {
            MultiEnemyBattleState state = CreateBattle(HeroSkillUpgrades.Analyze, 4404);
            Dictionary<string, MultiEnemyIntent> lockedTruth = state.CurrentPlans.ToDictionary(
                plan => plan.EnemyId,
                plan => plan.TrueIntent);
            Require(state.SelectTarget(Level04EnemyRoster.MaskId),
                "Il test ANALIZZA non riesce a selezionare la Maschera.");
            HeroActionResolution analyze = state.ResolveHeroAction(
                MultiEnemyHeroAction.Analyze,
                Level04EnemyRoster.MaskId);
            Require(analyze.Accepted && !analyze.ConsumesTurn && analyze.Intel.Count == 3,
                "ANALIZZA potenziato deve mostrare i tre nemici senza consumare il turno.");
            Require(state.AnalyzedPlansRevealed && state.CurrentPlans.All(plan =>
                    lockedTruth[plan.EnemyId] == plan.TrueIntent &&
                    state.GetVisibleIntent(plan.EnemyId) == plan.TrueIntent),
                "ANALIZZA non mostra la verità o modifica un'intenzione già bloccata.");
            Require(state.GetEnemy(Level04EnemyRoster.MaskId).Exposed,
                "ANALIZZA potenziato non applica ESPOSTO al bersaglio.");

            Require(state.SelectTarget(Level04EnemyRoster.WatcherId),
                "Il test ANALIZZA ripetuto non riesce a selezionare il Vigile.");
            HeroActionResolution repeatedAnalyze = state.ResolveHeroAction(
                MultiEnemyHeroAction.Analyze,
                Level04EnemyRoster.WatcherId);
            Require(repeatedAnalyze.Accepted && !repeatedAnalyze.ConsumesTurn &&
                    repeatedAnalyze.Intel.Count == 3 &&
                    state.AnalyzeExposedAppliedThisTurn,
                "ANALIZZA ripetuto nello stesso turno deve restare informativo e non consumare il turno.");
            Require(state.GetEnemy(Level04EnemyRoster.MaskId).Exposed &&
                    !state.GetEnemy(Level04EnemyRoster.WatcherId).Exposed,
                "ANALIZZA potenziato applica ESPOSTO a più di un bersaglio nello stesso turno.");

            Require(state.SelectTarget(Level04EnemyRoster.MaskId),
                "Il test ESPOSTO non riesce a riselezionare la Maschera.");
            int hpBefore = state.GetEnemy(Level04EnemyRoster.MaskId).CurrentHp;
            HeroActionResolution attack = state.ResolveHeroAction(
                MultiEnemyHeroAction.Attack,
                Level04EnemyRoster.MaskId);
            DamageEvent damage = attack.DamageEvents.Single();
            Require(damage.UsedExposed && hpBefore -
                    state.GetEnemy(Level04EnemyRoster.MaskId).CurrentHp == 30,
                "ESPOSTO non aumenta del 25% il prossimo danno di Hero01.");
            Require(!state.GetEnemy(Level04EnemyRoster.MaskId).Exposed,
                "ESPOSTO non viene consumato dal primo colpo di Hero01.");

            EnemyPhaseResolution enemyPhase = state.ResolveEnemyPhase();
            Require(enemyPhase.Accepted && !enemyPhase.HeroDefeated &&
                    state.Phase == MultiEnemyBattlePhase.HeroTurn &&
                    !state.AnalyzeExposedAppliedThisTurn,
                "Il limite di ESPOSTO di ANALIZZA non si azzera all'inizio del nuovo turno.");
            Require(state.SelectTarget(Level04EnemyRoster.WatcherId),
                "Nel nuovo turno ANALIZZA non riesce a selezionare il Vigile.");
            HeroActionResolution nextTurnAnalyze = state.ResolveHeroAction(
                MultiEnemyHeroAction.Analyze,
                Level04EnemyRoster.WatcherId);
            Require(nextTurnAnalyze.Accepted && !nextTurnAnalyze.ConsumesTurn &&
                    state.GetEnemy(Level04EnemyRoster.WatcherId).Exposed &&
                    state.AnalyzeExposedAppliedThisTurn,
                "ANALIZZA non può applicare nuovamente ESPOSTO dopo la fase nemica.");

            MultiEnemyBattleState expiration = CreateBattle(HeroSkillUpgrades.Analyze, 4404);
            Require(expiration.SelectTarget(Level04EnemyRoster.MaskId) &&
                    expiration.ResolveHeroAction(
                        MultiEnemyHeroAction.Analyze,
                        Level04EnemyRoster.MaskId).Accepted &&
                    expiration.GetEnemy(Level04EnemyRoster.MaskId).Exposed,
                "Il test di scadenza non riesce ad applicare ESPOSTO alla Maschera.");
            Require(expiration.ResolveHeroAction(MultiEnemyHeroAction.Guard).Accepted,
                "Il test di scadenza non riesce a terminare il turno con GUARDIA.");
            EnemyPhaseResolution expirationPhase = expiration.ResolveEnemyPhase();
            Require(expirationPhase.Accepted && !expirationPhase.HeroDefeated &&
                    !expiration.GetEnemy(Level04EnemyRoster.MaskId).Exposed &&
                    !expiration.AnalyzeExposedAppliedThisTurn,
                "ESPOSTO non consumato persiste o si accumula oltre la fine del turno.");
            Require(expiration.SelectTarget(Level04EnemyRoster.WatcherId),
                "Dopo la scadenza di ESPOSTO non si riesce a selezionare il Vigile.");
            HeroActionResolution expirationNextAnalyze = expiration.ResolveHeroAction(
                MultiEnemyHeroAction.Analyze,
                Level04EnemyRoster.WatcherId);
            Require(expirationNextAnalyze.Accepted &&
                    expiration.GetEnemy(Level04EnemyRoster.WatcherId).Exposed &&
                    !expiration.GetEnemy(Level04EnemyRoster.MaskId).Exposed,
                "Nel nuovo turno ANALIZZA non applica ESPOSTO soltanto al nuovo bersaglio.");
        }

        private static void ValidateBattleCapsAndChargeLegality()
        {
            bool observedChargedStrike = false;
            bool observedBluff = false;
            bool observedFinta = false;

            for (int seed = 1; seed <= 96; seed++)
            {
                MultiEnemyBattleRules durableRules = new MultiEnemyBattleRules(100000, 24, 38);
                MultiEnemyBattleState state = new MultiEnemyBattleState(
                    durableRules,
                    Level04EnemyRoster.Create(),
                    HeroSkillUpgrades.Guard,
                    seed);
                var lastBluffTurns = new Dictionary<string, int>();

                for (int turn = 0; turn < 14 &&
                     state.Phase != MultiEnemyBattlePhase.HeroDefeated; turn++)
                {
                    IReadOnlyList<EnemyTurnPlan> plans = state.CurrentPlans;
                    Require(state.ArePlansLocked,
                        "Le intenzioni non sono bloccate prima dell'input di Hero01.");
                    Require(plans.Count(plan => IsOffensive(plan.TrueIntent)) <=
                            MultiEnemyBattleState.MaximumOffensiveActionsPerEnemyPhase,
                        "Una fase nemica supera due azioni offensive.");
                    Require(plans.Count(plan => plan.TrueIntent ==
                            MultiEnemyIntent.ChargedStrike) <=
                            MultiEnemyBattleState.MaximumChargedStrikesPerEnemyPhase,
                        "Una fase nemica supera un Colpo caricato.");

                    foreach (EnemyTurnPlan plan in plans)
                    {
                        MultiEnemyEnemyState enemy = state.GetEnemy(plan.EnemyId);
                        if (plan.TrueIntent == MultiEnemyIntent.ChargedStrike)
                        {
                            observedChargedStrike = true;
                            Require(enemy.ChargePrepared,
                                "È stato pianificato un Colpo caricato senza carica precedente.");
                        }

                        if (plan.TrueIntent == MultiEnemyIntent.Finta)
                        {
                            observedFinta = true;
                            Require(enemy.Profile.HasTrait(EnemyBehaviorTraits.Deceptive) &&
                                    !IsOffensive(plan.TrueIntent),
                                "FINTA è stata assegnata a un nemico non ingannevole o " +
                                "classificata come offensiva.");
                        }

                        if (!plan.IsBluff)
                        {
                            continue;
                        }

                        observedBluff = true;
                        Require(plan.TrueIntent != plan.DisplayedIntent &&
                                !string.IsNullOrWhiteSpace(plan.InstabilityClue),
                            "Un bluff non differisce dall'intenzione reale o non mostra l'indizio.");
                        if (lastBluffTurns.TryGetValue(plan.EnemyId, out int previousTurn))
                        {
                            Require(plan.TurnNumber - previousTurn >=
                                    DeceptiveEnemyTrait.MinimumTurnsBetweenBluffs,
                                "Il cooldown di tre turni del bluff non è rispettato.");
                        }

                        lastBluffTurns[plan.EnemyId] = plan.TurnNumber;
                    }

                    HeroActionResolution guard = state.ResolveHeroAction(MultiEnemyHeroAction.Guard);
                    Require(guard.Accepted, "GUARDIA non accettata durante il test dei limiti.");
                    EnemyPhaseResolution phase = state.ResolveEnemyPhase();
                    Require(phase.Accepted, "La fase nemica non viene risolta nel seed sweep.");
                    foreach (EnemyActionResolution action in phase.Actions.Where(item =>
                                 item.Plan.TrueIntent == MultiEnemyIntent.Finta))
                    {
                        Require(action.DamageDealt == 0 && !action.BlockedByGuard &&
                                !action.PreparedGuard && !action.BeganCharge &&
                                !action.HeldCharge,
                            "FINTA infligge danno, prepara guardia o modifica lo stato di carica.");
                    }
                }
            }

            Require(observedChargedStrike,
                "I seed di validazione non hanno mai esercitato un Colpo caricato.");
            Require(observedBluff,
                "I seed di validazione non hanno mai esercitato un bluff.");
            Require(observedFinta,
                "I seed deterministici di validazione non hanno mai esercitato FINTA.");
        }

        private static void ValidateBluffAndDeterminism()
        {
            Require(Math.Abs(DeceptiveEnemyTrait.MaximumBluffProbability - 0.35d) < 0.000001d,
                "La probabilità massima del bluff deve essere 35%.");
            Require(DeceptiveEnemyTrait.MinimumTurnsBetweenBluffs == 3,
                "Il cooldown minimo del bluff deve essere tre turni.");

            MultiEnemyBattleState first = CreateBattle(HeroSkillUpgrades.None, 4404);
            MultiEnemyBattleState second = CreateBattle(HeroSkillUpgrades.None, 4404);
            Require(first.SelectTarget(Level04EnemyRoster.MaskId) &&
                    second.SelectTarget(Level04EnemyRoster.MaskId),
                "Il test deterministico non riesce a selezionare lo stesso bersaglio.");
            for (int turn = 0; turn < 8; turn++)
            {
                Require(PlansAreEqual(first.CurrentPlans, second.CurrentPlans),
                    "Lo stesso seed non produce una sequenza di intenzioni ripetibile.");

                Dictionary<string, MultiEnemyIntent> locked = first.CurrentPlans.ToDictionary(
                    plan => plan.EnemyId,
                    plan => plan.TrueIntent);
                HeroActionResolution firstAnalyze = first.ResolveHeroAction(
                    MultiEnemyHeroAction.Analyze,
                    Level04EnemyRoster.MaskId);
                HeroActionResolution secondAnalyze = second.ResolveHeroAction(
                    MultiEnemyHeroAction.Analyze,
                    Level04EnemyRoster.MaskId);
                Require(firstAnalyze.Accepted && secondAnalyze.Accepted &&
                        first.CurrentPlans.All(plan =>
                            locked[plan.EnemyId] == plan.TrueIntent) &&
                        PlansAreEqual(first.CurrentPlans, second.CurrentPlans),
                    "L'input ANALIZZA modifica il piano già bloccato o rompe il determinismo.");

                HeroActionResolution firstGuard = first.ResolveHeroAction(
                    MultiEnemyHeroAction.Guard);
                HeroActionResolution secondGuard = second.ResolveHeroAction(
                    MultiEnemyHeroAction.Guard);
                Require(firstGuard.Accepted && secondGuard.Accepted,
                    "Impossibile avanzare il test deterministico.");
                EnemyPhaseResolution firstPhase = first.ResolveEnemyPhase();
                EnemyPhaseResolution secondPhase = second.ResolveEnemyPhase();
                Require(firstPhase.HeroHpAfter == secondPhase.HeroHpAfter,
                    "Lo stesso seed produce danni diversi.");
            }
        }

        private static void ValidateBuildSettings(List<string> errors)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            if (scenes.Length < RequiredBuildScenePaths.Length)
            {
                errors.Add("Build Settings: servono almeno cinque scene; trovate " + scenes.Length + ".");
                return;
            }

            for (int index = 0; index < RequiredBuildScenePaths.Length; index++)
            {
                EditorBuildSettingsScene scene = scenes[index];
                if (scene.path != RequiredBuildScenePaths[index])
                {
                    errors.Add("Build Settings indice " + index + ": attesa '" +
                               RequiredBuildScenePaths[index] + "', trovata '" + scene.path + "'.");
                }

                if (!scene.enabled)
                {
                    errors.Add("Build Settings: la scena richiesta è disabilitata: " + scene.path);
                }
            }

            for (int index = RequiredBuildScenePaths.Length; index < scenes.Length; index++)
            {
                if (scenes[index].enabled)
                {
                    errors.Add(
                        "Build Settings: scena estranea al flusso L1-L4 ancora abilitata: " +
                        scenes[index].path);
                }
            }
        }

        private static void ValidateRuntimeAuthoringRules(List<string> errors)
        {
            string[] scriptGuids = AssetDatabase.FindAssets(
                "t:MonoScript",
                new[] { "Assets/_Veyra/Scripts/Runtime" });
            string[] forbiddenTokens =
            {
                "new GameObject(",
                ".AddComponent<",
                "GameObject.Find(",
                "FindObjectOfType<",
                "FindFirstObjectByType<",
                "FindAnyObjectByType<",
                "Resources.Load<"
            };

            foreach (string guid in scriptGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                string absolutePath = Path.GetFullPath(assetPath);
                string source = File.ReadAllText(absolutePath);
                foreach (string token in forbiddenTokens)
                {
                    if (source.IndexOf(token, StringComparison.Ordinal) >= 0)
                    {
                        errors.Add("Authoring runtime vietato in " + assetPath + ": '" + token + "'.");
                    }
                }
            }
        }

        private static void ValidateMainMenuScene(Scene scene, List<string> errors)
        {
            const string prefix = "SCN_MainMenu: ";
            ValidateNoMissingScripts(scene, prefix, errors);
            RequireExactlyOneComponent<MainMenuController>(scene, prefix, errors);
            RequireExactlyOneComponent<EventSystem>(scene, prefix, errors);

            GameObject navigationRoot = FindUniqueGameObject(
                scene,
                "MainNavigationPanel",
                prefix,
                errors);
            if (navigationRoot != null)
            {
                Button[] mainButtons = navigationRoot.GetComponentsInChildren<Button>(true);
                if (mainButtons.Length != 4)
                {
                    errors.Add(prefix + "MainNavigationPanel deve contenere esattamente quattro " +
                               "Button; trovati " + mainButtons.Length + ".");
                }

                foreach (string buttonName in MainButtonNames)
                {
                    Button button = mainButtons.SingleOrDefault(item => item.name == buttonName);
                    if (button == null)
                    {
                        errors.Add(prefix + "pulsante principale mancante: " + buttonName + ".");
                    }
                    else
                    {
                        ValidatePersistentListener(button, prefix, errors);
                    }
                }
            }

            MainMenuController controller = GetSceneComponents<MainMenuController>(scene)
                .SingleOrDefault();
            if (controller == null)
            {
                return;
            }

            var serialized = new SerializedObject(controller);
            foreach (string propertyName in RequiredMainMenuReferences)
            {
                ValidateObjectReference(serialized, propertyName, prefix, errors);
            }

            ValidateObjectReferenceArray(serialized, "levelButtons", 10, prefix, errors);
            ValidateObjectReferenceArray(serialized, "levelButtonLabels", 10, prefix, errors);
            SerializedProperty levelButtons = serialized.FindProperty("levelButtons");
            if (levelButtons != null && levelButtons.isArray && levelButtons.arraySize == 10)
            {
                for (int index = 0; index < 10; index++)
                {
                    Button button = levelButtons.GetArrayElementAtIndex(index).objectReferenceValue as Button;
                    string expectedName = "BTN_Level" + (index + 1).ToString("00");
                    if (button == null || button.name != expectedName)
                    {
                        errors.Add(prefix + "slot " + (index + 1) +
                                   " non punta a " + expectedName + ".");
                        continue;
                    }

                    ValidatePersistentListener(button, prefix, errors);
                    string expectedMethod = index < 4
                        ? "OpenLevel" + (index + 1).ToString("00")
                        : "ShowComingSoonLevel";
                    ValidateExpectedPersistentListener(
                        button,
                        controller,
                        expectedMethod,
                        prefix,
                        errors);
                    if (index >= 4 && button.interactable)
                    {
                        errors.Add(prefix + expectedName +
                                   " deve essere disattivato nella scena persistente.");
                    }
                }
            }

            SerializedProperty heroNameProperty = serialized.FindProperty("heroNameText");
            TMP_Text heroName = heroNameProperty != null
                ? heroNameProperty.objectReferenceValue as TMP_Text
                : null;
            if (heroName == null || heroName.text.IndexOf("HERO01", StringComparison.OrdinalIgnoreCase) < 0)
            {
                errors.Add(prefix + "il pannello EROI non mostra Hero01.");
            }

            SerializedProperty resetModal = serialized.FindProperty("resetProgressConfirmationModal");
            SerializedProperty resetButton = serialized.FindProperty("resetProgressButton");
            if (resetModal == null || resetModal.objectReferenceValue == null ||
                resetButton == null || resetButton.objectReferenceValue == null)
            {
                errors.Add(prefix + "il reset non possiede una modale di conferma persistente.");
            }

            ValidateLevel04MenuOutcomeSummary(controller, serialized, prefix, errors);
            ValidateSceneButtons(scene, prefix, errors, null);
        }

        private static void ValidateLevel04MenuOutcomeSummary(
            MainMenuController controller,
            SerializedObject serialized,
            string prefix,
            List<string> errors)
        {
            SerializedProperty labelsProperty = serialized.FindProperty("levelButtonLabels");
            SerializedProperty buttonsProperty = serialized.FindProperty("levelButtons");
            TMP_Text completedLevels = serialized.FindProperty("completedLevelsText")
                ?.objectReferenceValue as TMP_Text;
            GameObject mainPanel = serialized.FindProperty("mainNavigationPanel")
                ?.objectReferenceValue as GameObject;
            GameObject levelsPanel = serialized.FindProperty("levelsPanel")
                ?.objectReferenceValue as GameObject;
            GameObject heroesPanel = serialized.FindProperty("heroesPanel")
                ?.objectReferenceValue as GameObject;
            if (labelsProperty == null || !labelsProperty.isArray ||
                labelsProperty.arraySize != 10 || buttonsProperty == null ||
                !buttonsProperty.isArray || buttonsProperty.arraySize != 10 ||
                completedLevels == null || mainPanel == null || levelsPanel == null ||
                heroesPanel == null)
            {
                return;
            }

            var labels = new TMP_Text[10];
            var buttons = new Button[10];
            var originalTexts = new string[10];
            var originalInteractable = new bool[10];
            for (int index = 0; index < 10; index++)
            {
                labels[index] = labelsProperty.GetArrayElementAtIndex(index)
                    .objectReferenceValue as TMP_Text;
                buttons[index] = buttonsProperty.GetArrayElementAtIndex(index)
                    .objectReferenceValue as Button;
                if (labels[index] == null || buttons[index] == null)
                {
                    return;
                }

                originalTexts[index] = labels[index].text;
                originalInteractable[index] = buttons[index].interactable;
            }

            string originalCompletedText = completedLevels.text;
            bool originalMainActive = mainPanel.activeSelf;
            bool originalLevelsActive = levelsPanel.activeSelf;
            bool originalHeroesActive = heroesPanel.activeSelf;
            using (new ProgressPrefsScope())
            {
                try
                {
                    CampaignProgressStore.Reset();
                    CampaignProgressStore.SetTutorialResolution(EncounterResolution.Saved);
                    CampaignProgressStore.RecordEncounterResolution(
                        CampaignEncounter.ThornGuardian,
                        EncounterResolution.Saved);
                    CampaignProgressStore.RecordEncounterResolution(
                        CampaignEncounter.AshWatcher,
                        EncounterResolution.Killed);
                    CampaignProgressStore.RecordLevel04Resolutions(
                        EncounterResolution.Saved,
                        EncounterResolution.Killed,
                        EncounterResolution.Saved);

                    controller.OpenLevels();
                    string normalizedSummary = (labels[3].text ?? string.Empty)
                        .ToUpperInvariant()
                        .Replace(":", string.Empty)
                        .Replace("\n", " ");
                    Require(normalizedSummary.Contains("BRUTO SALVATO") &&
                            normalizedSummary.Contains("VEGLIA UCCISO") &&
                            normalizedSummary.Contains("MASCHERA SALVATO"),
                        "Lo slot del Livello 4 non mostra BRUTO SALVATO, VEGLIA UCCISO e " +
                        "MASCHERA SALVATO dopo il refresh della campagna completata.");
                }
                finally
                {
                    for (int index = 0; index < 10; index++)
                    {
                        labels[index].text = originalTexts[index];
                        buttons[index].interactable = originalInteractable[index];
                    }

                    completedLevels.text = originalCompletedText;
                    mainPanel.SetActive(originalMainActive);
                    levelsPanel.SetActive(originalLevelsActive);
                    heroesPanel.SetActive(originalHeroesActive);
                }
            }
        }

        private static void ValidateRegressionScenes(List<string> errors)
        {
            ValidateSceneAsset(TutorialScenePath, errors, (scene, targetErrors) =>
            {
                ValidateNoMissingScripts(scene, "Tutorial: ", targetErrors);
                RequireExactlyOneComponent<TutorialBattleController>(
                    scene,
                    "Tutorial: ",
                    targetErrors);
                ValidateButtonLabel(
                    scene,
                    "BTN_OutcomeContinue",
                    "LIVELLO 2",
                    "Tutorial: ",
                    targetErrors);
                ValidateButtonLabel(
                    scene,
                    "BTN_OutcomeMenu",
                    "MENU PRINCIPALE",
                    "Tutorial: ",
                    targetErrors);
            });
            ValidateSceneAsset(Level02ScenePath, errors, (scene, targetErrors) =>
            {
                ValidateNoMissingScripts(scene, "Livello 2: ", targetErrors);
                RequireExactlyOneComponent<EncounterBattleController>(
                    scene,
                    "Livello 2: ",
                    targetErrors);
                ValidateButtonLabel(
                    scene,
                    "BTN_OutcomeContinue",
                    "LIVELLO 3",
                    "Livello 2: ",
                    targetErrors);
                ValidateButtonLabel(
                    scene,
                    "BTN_OutcomeMenu",
                    "MENU PRINCIPALE",
                    "Livello 2: ",
                    targetErrors);
            });
            ValidateSceneAsset(Level03ScenePath, errors, (scene, targetErrors) =>
            {
                const string prefix = "Livello 3: ";
                ValidateNoMissingScripts(scene, prefix, targetErrors);
                RequireExactlyOneComponent<EncounterBattleController>(scene, prefix, targetErrors);
                EncounterBattleController controller =
                    GetSceneComponents<EncounterBattleController>(scene).SingleOrDefault();
                if (controller == null)
                {
                    return;
                }

                var serialized = new SerializedObject(controller);
                ValidateObjectReference(serialized, "thornGuardianAllyActor", prefix, targetErrors);
                ValidateObjectReference(serialized, "thornGuardianSupportEffect", prefix, targetErrors);
                ValidateObjectReference(serialized, "allyDialogueRoot", prefix, targetErrors);
                ValidateObjectReference(serialized, "allyDialogueText", prefix, targetErrors);
            });
        }

        private static void ValidateLevel04Scene(Scene scene, List<string> errors)
        {
            const string prefix = "Livello 4: ";
            ValidateNoMissingScripts(scene, prefix, errors);
            RequireExactlyOneComponent<MultiEnemyBattleController>(scene, prefix, errors);
            RequireExactlyOneComponent<MultiEnemyBattleNavigation>(scene, prefix, errors);
            RequireExactlyOneComponent<EventSystem>(scene, prefix, errors);

            MultiEnemyBattleController controller =
                GetSceneComponents<MultiEnemyBattleController>(scene).SingleOrDefault();
            if (controller == null)
            {
                return;
            }

            var serialized = new SerializedObject(controller);
            foreach (string propertyName in RequiredLevel04References)
            {
                ValidateObjectReference(serialized, propertyName, prefix, errors);
            }

            ValidateObjectReferenceArray(serialized, "moralChoiceStateTexts", 3, prefix, errors);
            ValidateObjectReferenceArray(serialized, "moralCurrentIndicators", 3, prefix, errors);
            ValidateObjectReferenceArray(serialized, "moralCurrentOutlines", 3, prefix, errors);
            ValidateObjectReferenceArray(serialized, "moralSaveButtons", 3, prefix, errors);
            ValidateObjectReferenceArray(serialized, "moralKillButtons", 3, prefix, errors);
            ValidateMoralCurrentPresentation(serialized, prefix, errors);
            ValidateLevel04OutcomePresentation(serialized, prefix, errors);
            ValidateLevel04ButtonAndNavigationWiring(scene, controller, serialized, prefix, errors);

            SerializedProperty enemyViews = serialized.FindProperty("enemyViews");
            if (enemyViews == null || !enemyViews.isArray || enemyViews.arraySize != 3)
            {
                errors.Add(prefix + "enemyViews deve contenere esattamente tre nemici persistenti.");
            }
            else
            {
                IReadOnlyList<MultiEnemyProfile> expectedRoster = Level04EnemyRoster.Create();
                var expectedProfiles = expectedRoster.ToDictionary(
                    profile => profile.EnemyId,
                    profile => profile);
                var actors = new HashSet<Transform>();
                int ground = 0;
                int flying = 0;
                for (int index = 0; index < enemyViews.arraySize; index++)
                {
                    SerializedProperty view = enemyViews.GetArrayElementAtIndex(index);
                    string enemyId = GetString(view, "enemyId");
                    if (enemyId != expectedRoster[index].EnemyId)
                    {
                        errors.Add(prefix + "enemyViews[" + index +
                                   "] non conserva l'ordine Bruto, Veglia, Maschera.");
                    }

                    if (!expectedProfiles.TryGetValue(enemyId, out MultiEnemyProfile expected))
                    {
                        errors.Add(prefix + "enemyViews[" + index + "] usa un id inatteso: '" +
                                   enemyId + "'.");
                    }
                    else
                    {
                        ValidateSerializedEnemyProfile(view, expected, prefix, errors);
                    }

                    if (string.IsNullOrWhiteSpace(
                            GetString(view, "incapacitatedDialogue")))
                    {
                        errors.Add(prefix + "enemyViews[" + index +
                                   "] non contiene il dialogo individuale da incapacitato.");
                    }

                    foreach (string referenceName in RequiredEnemyViewReferences)
                    {
                        ValidateRelativeObjectReference(
                            view,
                            referenceName,
                            prefix + "enemyViews[" + index + "]: ",
                            errors);
                    }

                    SerializedProperty actorProperty = view.FindPropertyRelative("actor");
                    Transform actor = actorProperty != null
                        ? actorProperty.objectReferenceValue as Transform
                        : null;
                    if (actor != null && !actors.Add(actor))
                    {
                        errors.Add(prefix + "due profili condividono lo stesso enemy actor.");
                    }

                    Button targetButton = view.FindPropertyRelative("targetButton")
                        ?.objectReferenceValue as Button;
                    string[] targetMethods = { "SelectBrute", "SelectWatcher", "SelectMask" };
                    if (targetButton != null)
                    {
                        ValidateExpectedPersistentListener(
                            targetButton,
                            controller,
                            targetMethods[index],
                            prefix,
                            errors);
                    }

                    if (actor != null)
                    {
                        Collider2D collider = actor.GetComponent<Collider2D>();
                        MultiEnemyActorTarget actorTarget =
                            actor.GetComponent<MultiEnemyActorTarget>();
                        if (collider == null || !collider.enabled)
                        {
                            errors.Add(prefix + actor.name +
                                       " deve avere un Collider2D attivo per il click diretto.");
                        }

                        if (actorTarget == null || !actorTarget.enabled)
                        {
                            errors.Add(prefix + actor.name +
                                       " deve avere MultiEnemyActorTarget attivo.");
                        }
                        else
                        {
                            var actorTargetSerialized = new SerializedObject(actorTarget);
                            MultiEnemyBattleController assignedController =
                                actorTargetSerialized.FindProperty("battleController")
                                    ?.objectReferenceValue as MultiEnemyBattleController;
                            SerializedProperty enemyIndexProperty =
                                actorTargetSerialized.FindProperty("enemyIndex");
                            if (assignedController != controller ||
                                enemyIndexProperty == null ||
                                enemyIndexProperty.intValue != index ||
                                actorTarget.EnemyIndex != index)
                            {
                                errors.Add(prefix + actor.name +
                                           " non collega controller e indice bersaglio " + index + ".");
                            }
                        }
                    }

                    int altitude = GetInt(view, "altitude");
                    if (altitude == (int)EnemyAltitude.Ground) ground++;
                    if (altitude == (int)EnemyAltitude.Flying) flying++;
                }

                if (actors.Count != 3)
                {
                    errors.Add(prefix + "servono esattamente tre enemy actor distinti.");
                }

                if (ground != 1 || flying != 2)
                {
                    errors.Add(prefix + "servono un enemy actor a terra e due in volo.");
                }

                int actorTargetCount = GetSceneComponents<MultiEnemyActorTarget>(scene).Count;
                if (actorTargetCount != 3)
                {
                    errors.Add(prefix + "servono esattamente tre MultiEnemyActorTarget; trovati " +
                               actorTargetCount + ".");
                }
            }

            string[] actorNames =
            {
                "EnemyActor_Brute",
                "EnemyActor_Watcher",
                "EnemyActor_Mask"
            };
            foreach (string actorName in actorNames)
            {
                FindUniqueGameObject(scene, actorName, prefix, errors);
            }

            SerializedProperty heroActorProperty = serialized.FindProperty("heroActor");
            Transform heroActor = heroActorProperty != null
                ? heroActorProperty.objectReferenceValue as Transform
                : null;
            if (heroActor != null && enemyViews != null && enemyViews.isArray &&
                enemyViews.arraySize == 3)
            {
                for (int index = 0; index < 3; index++)
                {
                    Transform enemyActor = enemyViews.GetArrayElementAtIndex(index)
                        .FindPropertyRelative("actor").objectReferenceValue as Transform;
                    if (enemyActor != null && heroActor.position.x >= enemyActor.position.x)
                    {
                        errors.Add(prefix + "Hero01 deve essere a sinistra di tutti i nemici.");
                    }
                }
            }

            bool hasTitle = GetSceneComponents<TMP_Text>(scene).Any(text =>
                text.text != null &&
                text.text.IndexOf("LIVELLO 4", StringComparison.OrdinalIgnoreCase) >= 0 &&
                text.text.IndexOf("ASSALTO DEI TRE", StringComparison.OrdinalIgnoreCase) >= 0);
            if (!hasTitle)
            {
                errors.Add(prefix + "titolo 'LIVELLO 4 · ASSALTO DEI TRE' mancante.");
            }

            ValidateSceneButtons(scene, prefix, errors, null);
        }

        private static void ValidateMoralCurrentPresentation(
            SerializedObject serialized,
            string prefix,
            List<string> errors)
        {
            SerializedProperty indicators = serialized.FindProperty("moralCurrentIndicators");
            SerializedProperty outlines = serialized.FindProperty("moralCurrentOutlines");
            if (indicators == null || !indicators.isArray || indicators.arraySize != 3 ||
                outlines == null || !outlines.isArray || outlines.arraySize != 3)
            {
                return;
            }

            var distinctIndicators = new HashSet<TMP_Text>();
            var distinctOutlines = new HashSet<Outline>();
            for (int index = 0; index < 3; index++)
            {
                TMP_Text indicator = indicators.GetArrayElementAtIndex(index)
                    .objectReferenceValue as TMP_Text;
                Outline outline = outlines.GetArrayElementAtIndex(index)
                    .objectReferenceValue as Outline;
                if (indicator == null || outline == null)
                {
                    continue;
                }

                if (!distinctIndicators.Add(indicator) || !distinctOutlines.Add(outline))
                {
                    errors.Add(prefix +
                               "ogni riga morale deve avere indicatore e bordo distinti.");
                }

                if (!string.Equals(
                        (indicator.text ?? string.Empty).Trim(),
                        "> IN DECISIONE",
                        StringComparison.Ordinal))
                {
                    errors.Add(prefix + "moralCurrentIndicators[" + index +
                               "] deve mostrare l'icona e il testo '> IN DECISIONE'.");
                }

                if (indicator.transform.parent != outline.transform)
                {
                    errors.Add(prefix + "l'indicatore morale " + index +
                               " non appartiene alla riga evidenziata dal suo bordo.");
                }

                if (indicator.gameObject.activeSelf || outline.enabled)
                {
                    errors.Add(prefix + "indicatore e bordo morale " + index +
                               " devono iniziare nascosti e attivarsi solo sulla riga corrente.");
                }

                if (outline.effectColor.a < 0.75f || outline.effectDistance.sqrMagnitude < 1f)
                {
                    errors.Add(prefix + "il bordo morale " + index +
                               " non è abbastanza visibile.");
                }
            }
        }

        private static void ValidateLevel04OutcomePresentation(
            SerializedObject serialized,
            string prefix,
            List<string> errors)
        {
            TMP_Text body = serialized.FindProperty("outcomeBodyText")
                ?.objectReferenceValue as TMP_Text;
            if (body == null)
            {
                return;
            }

            string normalized = (body.text ?? string.Empty).ToUpperInvariant();
            string[] requiredTokens =
            {
                "BRUTO DELLE RADICI",
                "VEGLIA SOSPESA",
                "MASCHERA DEL VENTO",
                "SALVATO",
                "UCCISO",
                "CONTENUTO DISPONIBILE COMPLETATO",
                "LIVELLO 5 PROSSIMAMENTE"
            };
            foreach (string token in requiredTokens)
            {
                if (normalized.IndexOf(token, StringComparison.Ordinal) < 0)
                {
                    errors.Add(prefix + "l'esito persistente del Livello 4 non contiene '" +
                               token + "'.");
                }
            }
        }

        private static void ValidateLevel04ButtonAndNavigationWiring(
            Scene scene,
            MultiEnemyBattleController controller,
            SerializedObject controllerSerialized,
            string prefix,
            List<string> errors)
        {
            string[] controllerButtonProperties =
            {
                "attackButton",
                "guardButton",
                "techniqueButton",
                "analyzeButton",
                "analyzeCloseButton",
                "targetTutorialContinueButton",
                "moralReviewButton",
                "moralConfirmButton",
                "outcomeRetryButton"
            };
            string[] controllerMethods =
            {
                "ChooseAttack",
                "ChooseGuard",
                "ChooseTechnique",
                "OpenAnalyze",
                "CloseAnalyze",
                "CompleteMultiTargetTutorial",
                "ReviewMoralChoices",
                "ConfirmMoralChoices",
                "RetryLevel"
            };
            for (int index = 0; index < controllerButtonProperties.Length; index++)
            {
                ValidateReferencedButtonListener(
                    controllerSerialized,
                    controllerButtonProperties[index],
                    controller,
                    controllerMethods[index],
                    prefix,
                    errors);
            }

            string[] saveMethods =
            {
                "ChooseBruteSaved",
                "ChooseWatcherSaved",
                "ChooseMaskSaved"
            };
            string[] killMethods =
            {
                "ChooseBruteKilled",
                "ChooseWatcherKilled",
                "ChooseMaskKilled"
            };
            var moralButtons = new HashSet<Button>();
            ValidateMoralChoiceButtonArray(
                controllerSerialized,
                "moralSaveButtons",
                controller,
                saveMethods,
                moralButtons,
                prefix,
                errors);
            ValidateMoralChoiceButtonArray(
                controllerSerialized,
                "moralKillButtons",
                controller,
                killMethods,
                moralButtons,
                prefix,
                errors);
            if (moralButtons.Count != 6)
            {
                errors.Add(prefix +
                           "le tre scelte SALVA e le tre UCCIDI devono usare sei pulsanti distinti.");
            }

            Button reviewButton = GetReferencedButton(controllerSerialized, "moralReviewButton");
            Button confirmButton = GetReferencedButton(controllerSerialized, "moralConfirmButton");
            if (reviewButton != null && reviewButton == confirmButton)
            {
                errors.Add(prefix + "RIVEDI e CONFERMA devono essere due pulsanti distinti.");
            }

            MultiEnemyBattleNavigation navigation =
                GetSceneComponents<MultiEnemyBattleNavigation>(scene).SingleOrDefault();
            if (navigation == null)
            {
                return;
            }

            var navigationSerialized = new SerializedObject(navigation);
            string[] navigationReferences =
            {
                "backButton",
                "resultMenuButton",
                "retryButton",
                "battleController"
            };
            foreach (string propertyName in navigationReferences)
            {
                ValidateObjectReference(navigationSerialized, propertyName, prefix, errors);
            }

            MultiEnemyBattleNavigation assignedNavigation =
                controllerSerialized.FindProperty("navigation")?.objectReferenceValue as
                    MultiEnemyBattleNavigation;
            MultiEnemyBattleController assignedController =
                navigationSerialized.FindProperty("battleController")?.objectReferenceValue as
                    MultiEnemyBattleController;
            if (assignedNavigation != navigation || assignedController != controller)
            {
                errors.Add(prefix +
                           "controller e navigazione non possiedono riferimenti reciproci corretti.");
            }

            Button backButton = GetReferencedButton(navigationSerialized, "backButton");
            Button resultMenuButton =
                GetReferencedButton(navigationSerialized, "resultMenuButton");
            Button navigationRetryButton =
                GetReferencedButton(navigationSerialized, "retryButton");
            Button outcomeMenuButton =
                GetReferencedButton(controllerSerialized, "outcomeMenuButton");
            Button outcomeRetryButton =
                GetReferencedButton(controllerSerialized, "outcomeRetryButton");
            if (resultMenuButton != null && resultMenuButton != outcomeMenuButton)
            {
                errors.Add(prefix +
                           "navigation.resultMenuButton deve coincidere con outcomeMenuButton.");
            }

            if (navigationRetryButton != null && navigationRetryButton != outcomeRetryButton)
            {
                errors.Add(prefix +
                           "navigation.retryButton deve coincidere con outcomeRetryButton.");
            }

            if (backButton != null)
            {
                ValidateExpectedPersistentListener(
                    backButton,
                    navigation,
                    "BackToMenu",
                    prefix,
                    errors);
            }

            if (resultMenuButton != null)
            {
                ValidateExpectedPersistentListener(
                    resultMenuButton,
                    navigation,
                    "BackToMenu",
                    prefix,
                    errors);
            }

            if (navigationRetryButton != null)
            {
                ValidateExpectedPersistentListener(
                    navigationRetryButton,
                    controller,
                    "RetryLevel",
                    prefix,
                    errors);
            }
        }

        private static void ValidateMoralChoiceButtonArray(
            SerializedObject serialized,
            string propertyName,
            MultiEnemyBattleController controller,
            IReadOnlyList<string> expectedMethods,
            ISet<Button> distinctButtons,
            string prefix,
            List<string> errors)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || !property.isArray || property.arraySize != expectedMethods.Count)
            {
                return;
            }

            for (int index = 0; index < property.arraySize; index++)
            {
                Button button = property.GetArrayElementAtIndex(index).objectReferenceValue as Button;
                if (button == null)
                {
                    continue;
                }

                distinctButtons.Add(button);
                ValidateExpectedPersistentListener(
                    button,
                    controller,
                    expectedMethods[index],
                    prefix,
                    errors);
            }
        }

        private static void ValidateReferencedButtonListener(
            SerializedObject serialized,
            string propertyName,
            UnityEngine.Object expectedTarget,
            string expectedMethod,
            string prefix,
            List<string> errors)
        {
            Button button = GetReferencedButton(serialized, propertyName);
            if (button != null)
            {
                ValidateExpectedPersistentListener(
                    button,
                    expectedTarget,
                    expectedMethod,
                    prefix,
                    errors);
            }
        }

        private static Button GetReferencedButton(
            SerializedObject serialized,
            string propertyName)
        {
            return serialized.FindProperty(propertyName)?.objectReferenceValue as Button;
        }

        private static void ValidateSerializedEnemyProfile(
            SerializedProperty view,
            MultiEnemyProfile expected,
            string prefix,
            List<string> errors)
        {
            bool matches = GetString(view, "displayName") == expected.DisplayName &&
                           GetString(view, "race") == expected.Race &&
                           GetInt(view, "maxHp") == expected.MaxHp &&
                           GetInt(view, "corruptionPercent") == expected.CorruptionPercent &&
                           GetInt(view, "initialMood") == (int)expected.Mood &&
                           GetInt(view, "intelligenceLevel") == expected.IntelligenceLevel &&
                           GetInt(view, "altitude") == (int)expected.Altitude &&
                           GetInt(view, "attackDamage") == expected.AttackDamage &&
                           GetInt(view, "chargedStrikeDamage") == expected.ChargedStrikeDamage &&
                           GetInt(view, "assaultDamage") == expected.AssaultDamage &&
                           GetInt(view, "traits") == (int)expected.Traits &&
                           Mathf.Approximately(
                               GetFloat(view, "aggressiveWeight"),
                               (float)expected.TraitWeights.Aggressive) &&
                           Mathf.Approximately(
                               GetFloat(view, "patientWeight"),
                               (float)expected.TraitWeights.Patient) &&
                           Mathf.Approximately(
                               GetFloat(view, "deceptiveWeight"),
                               (float)expected.TraitWeights.Deceptive) &&
                           Mathf.Approximately(
                               GetFloat(view, "bluffProbability"),
                               (float)expected.DeceptionSettings.BluffProbability) &&
                           GetInt(view, "minimumTurnsBetweenBluffs") ==
                           expected.DeceptionSettings.MinimumTurnsBetweenBluffs &&
                           Mathf.Approximately(
                               GetFloat(view, "feintIntentWeight"),
                               (float)expected.DeceptionSettings.FeintIntentWeight);
            if (!matches)
            {
                errors.Add(prefix + "profilo serializzato non conforme per " +
                           expected.DisplayName + ".");
            }
        }

        private static void ValidateSceneAsset(
            string scenePath,
            List<string> errors,
            Action<Scene, List<string>> validator)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                errors.Add("Scena persistente mancante: " + scenePath +
                           ". Esegui prima la factory Progression.");
                return;
            }

            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool openedForValidation = !scene.IsValid() || !scene.isLoaded;
            try
            {
                if (openedForValidation)
                {
                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                }

                validator(scene, errors);
            }
            finally
            {
                if (openedForValidation && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void ValidateNoMissingScripts(
            Scene scene,
            string prefix,
            List<string> errors)
        {
            foreach (GameObject gameObject in GetSceneGameObjects(scene))
            {
                Component[] components = gameObject.GetComponents<Component>();
                for (int index = 0; index < components.Length; index++)
                {
                    if (components[index] == null)
                    {
                        errors.Add(prefix + "Missing Script su '" +
                                   GetHierarchyPath(gameObject.transform) + "'.");
                    }
                }
            }
        }

        private static void ValidateSceneButtons(
            Scene scene,
            string prefix,
            List<string> errors,
            Func<Button, bool> skipPredicate)
        {
            foreach (Button button in GetSceneComponents<Button>(scene))
            {
                if (skipPredicate != null && skipPredicate(button))
                {
                    continue;
                }

                ValidatePersistentListener(button, prefix, errors);
            }
        }

        private static void ValidatePersistentListener(
            Button button,
            string prefix,
            List<string> errors)
        {
            int listenerCount = button.onClick.GetPersistentEventCount();
            if (listenerCount <= 0)
            {
                errors.Add(prefix + "il pulsante '" + button.name +
                           "' non possiede listener persistenti.");
                return;
            }

            bool hasValidListener = false;
            for (int index = 0; index < listenerCount; index++)
            {
                UnityEngine.Object target = button.onClick.GetPersistentTarget(index);
                string method = button.onClick.GetPersistentMethodName(index);
                UnityEventCallState state = button.onClick.GetPersistentListenerState(index);
                if (target != null && !string.IsNullOrWhiteSpace(method) &&
                    state != UnityEventCallState.Off)
                {
                    hasValidListener = true;
                    break;
                }
            }

            if (!hasValidListener)
            {
                errors.Add(prefix + "il pulsante '" + button.name +
                           "' non possiede un listener persistente valido e attivo.");
            }
        }

        private static void ValidateExpectedPersistentListener(
            Button button,
            UnityEngine.Object expectedTarget,
            string expectedMethod,
            string prefix,
            List<string> errors)
        {
            for (int index = 0; index < button.onClick.GetPersistentEventCount(); index++)
            {
                if (button.onClick.GetPersistentTarget(index) == expectedTarget &&
                    button.onClick.GetPersistentMethodName(index) == expectedMethod &&
                    button.onClick.GetPersistentListenerState(index) != UnityEventCallState.Off)
                {
                    return;
                }
            }

            errors.Add(prefix + "il pulsante '" + button.name +
                       "' non richiama il metodo persistente previsto " + expectedMethod + ".");
        }

        private static void ValidateButtonLabel(
            Scene scene,
            string buttonName,
            string expectedText,
            string prefix,
            List<string> errors)
        {
            GameObject buttonObject = FindUniqueGameObject(
                scene,
                buttonName,
                prefix,
                errors);
            TMP_Text label = buttonObject != null
                ? buttonObject.GetComponentInChildren<TMP_Text>(true)
                : null;
            if (label == null || label.text != expectedText)
            {
                errors.Add(prefix + "il pulsante '" + buttonName +
                           "' deve mostrare esattamente '" + expectedText + "'.");
            }
        }

        private static void ValidateObjectReference(
            SerializedObject serialized,
            string propertyName,
            string prefix,
            List<string> errors)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                errors.Add(prefix + "campo serializzato non trovato: " + propertyName + ".");
            }
            else if (property.propertyType != SerializedPropertyType.ObjectReference ||
                     property.objectReferenceValue == null)
            {
                errors.Add(prefix + "riferimento persistente mancante: " + propertyName + ".");
            }
        }

        private static void ValidateRelativeObjectReference(
            SerializedProperty parent,
            string propertyName,
            string prefix,
            List<string> errors)
        {
            SerializedProperty property = parent.FindPropertyRelative(propertyName);
            if (property == null ||
                property.propertyType != SerializedPropertyType.ObjectReference ||
                property.objectReferenceValue == null)
            {
                errors.Add(prefix + "riferimento persistente mancante: " + propertyName + ".");
            }
        }

        private static void ValidateObjectReferenceArray(
            SerializedObject serialized,
            string propertyName,
            int expectedSize,
            string prefix,
            List<string> errors)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || !property.isArray || property.arraySize != expectedSize)
            {
                errors.Add(prefix + propertyName + " deve avere esattamente " +
                           expectedSize + " elementi.");
                return;
            }

            for (int index = 0; index < expectedSize; index++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(index);
                if (element.propertyType != SerializedPropertyType.ObjectReference ||
                    element.objectReferenceValue == null)
                {
                    errors.Add(prefix + propertyName + "[" + index +
                               "] non è assegnato in scena.");
                }
            }
        }

        private static void RequireExactlyOneComponent<T>(
            Scene scene,
            string prefix,
            List<string> errors)
            where T : Component
        {
            int count = GetSceneComponents<T>(scene).Count;
            if (count != 1)
            {
                errors.Add(prefix + "atteso esattamente un componente " + typeof(T).Name +
                           "; trovati " + count + ".");
            }
        }

        private static GameObject FindUniqueGameObject(
            Scene scene,
            string objectName,
            string prefix,
            List<string> errors)
        {
            List<GameObject> matches = GetSceneGameObjects(scene)
                .Where(item => item.name == objectName)
                .ToList();
            if (matches.Count != 1)
            {
                errors.Add(prefix + "atteso esattamente un GameObject '" + objectName +
                           "'; trovati " + matches.Count + ".");
                return null;
            }

            return matches[0];
        }

        private static IReadOnlyList<T> GetSceneComponents<T>(Scene scene)
            where T : Component
        {
            var result = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                result.AddRange(root.GetComponentsInChildren<T>(true));
            }

            return result;
        }

        private static IEnumerable<GameObject> GetSceneGameObjects(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    yield return transform.gameObject;
                }
            }
        }

        private static string GetHierarchyPath(Transform transform)
        {
            string result = transform.name;
            Transform current = transform.parent;
            while (current != null)
            {
                result = current.name + "/" + result;
                current = current.parent;
            }

            return result;
        }

        private static bool IsFutureLevelButton(Button button)
        {
            if (!button.name.StartsWith("BTN_Level", StringComparison.Ordinal))
            {
                return false;
            }

            string suffix = button.name.Substring("BTN_Level".Length);
            return int.TryParse(suffix, out int level) && level >= 5 && level <= 10;
        }

        private static bool IsAllyAvailable(CampaignProgressData progress, SavedAllyId allyId)
        {
            switch (allyId)
            {
                case SavedAllyId.ThornGuardian:
                    return progress.encounter02Resolved &&
                           progress.encounter02Resolution == EncounterResolution.Saved;
                case SavedAllyId.AshWatcher:
                    return progress.encounter03Resolved &&
                           progress.encounter03Resolution == EncounterResolution.Saved;
                default:
                    return false;
            }
        }

        private static HeroProgressData CreateProgressAtExperience(int experience)
        {
            HeroProgressData progress = HeroProgressStore.Defaults;
            progress.totalExperience = experience;
            return progress;
        }

        private static void AssertStats(
            HeroProgressData progress,
            int level,
            int maxHp,
            int attack,
            int technique,
            int splash,
            string context)
        {
            HeroCombatStats stats = HeroProgressionRules.GetCombatStats(progress);
            Require(stats.Level == level && stats.MaxHp == maxHp &&
                    stats.AttackDamage == attack && stats.TechniqueDamage == technique &&
                    stats.TechniqueSplashPercent == splash,
                context + ": statistiche errate; attese HP " + maxHp + ", ATT " +
                attack + ", TEC " + technique + ", AREA " + splash + "%.");
        }

        private static void RequireSameDamageStats(
            HeroCombatStats expected,
            HeroCombatStats actual,
            string context)
        {
            Require(actual.MaxHp == expected.MaxHp &&
                    actual.AttackDamage == expected.AttackDamage &&
                    actual.TechniqueDamage == expected.TechniqueDamage &&
                    actual.TechniqueSplashPercent == expected.TechniqueSplashPercent,
                context + " modifica statistiche di danno o HP non previste.");
        }

        private static void AssertProfile(
            MultiEnemyProfile profile,
            string enemyId,
            string displayName,
            string race,
            int maxHp,
            int corruption,
            Veyra.Combat.Encounter.EnemyMood mood,
            int intelligence,
            EnemyAltitude altitude,
            int attack,
            int chargedStrike,
            int assault,
            EnemyBehaviorTraits trait)
        {
            Require(profile.EnemyId == enemyId && profile.DisplayName == displayName &&
                    profile.Race == race && profile.MaxHp == maxHp &&
                    profile.CorruptionPercent == corruption && profile.Mood == mood &&
                    profile.IntelligenceLevel == intelligence && profile.Altitude == altitude &&
                    profile.AttackDamage == attack &&
                    profile.ChargedStrikeDamage == chargedStrike &&
                    profile.AssaultDamage == assault && profile.Traits == trait,
                "Profilo non conforme: " + displayName + ".");
        }

        private static MultiEnemyBattleState CreateBattle(HeroSkillUpgrades upgrades, int seed)
        {
            return new MultiEnemyBattleState(
                MultiEnemyBattleRules.Level04HeroAtLevel3,
                Level04EnemyRoster.Create(),
                upgrades,
                seed);
        }

        private static void IncapacitateNext(MultiEnemyBattleState state, string enemyId)
        {
            Require(state.Phase == MultiEnemyBattlePhase.HeroTurn,
                "Il test non è tornato al turno di Hero01.");
            Require(state.SelectTarget(enemyId),
                "Il test non riesce a selezionare il bersaglio " + enemyId + ".");
            HeroActionResolution result = state.ResolveHeroAction(
                MultiEnemyHeroAction.Attack,
                enemyId);
            Require(result.Accepted, "Impossibile incapacitare il bersaglio di test " + enemyId + ".");
            if (state.Phase == MultiEnemyBattlePhase.AwaitingMoralChoices)
            {
                MoralChoiceResolution choice = state.ResolveMoralChoice(
                    state.PendingMoralEnemyId,
                    EnemyMoralOutcome.Saved);
                Require(choice.Accepted, "La scelta morale immediata del test è stata rifiutata.");
            }
            if (!state.AllEnemiesIncapacitated)
            {
                state.ResolveEnemyPhase();
            }
        }

        private static int FindSeedWithAtLeastTwoOffensivePlans()
        {
            for (int seed = 1; seed <= 512; seed++)
            {
                MultiEnemyBattleState state = CreateBattle(HeroSkillUpgrades.None, seed);
                if (state.CurrentPlans.Count(plan => IsOffensive(plan.TrueIntent)) >= 2)
                {
                    return seed;
                }
            }

            return -1;
        }

        private static bool CanWinWithoutSupport(HeroSkillUpgrades upgrades)
        {
            MultiEnemyBattleState state = CreateBattle(upgrades, 4404);
            for (int turn = 0; turn < 12; turn++)
            {
                if (state.AllEnemiesIncapacitated)
                {
                    return true;
                }

                if (state.Phase == MultiEnemyBattlePhase.HeroDefeated)
                {
                    return false;
                }

                if (state.Phase != MultiEnemyBattlePhase.HeroTurn)
                {
                    return false;
                }

                MultiEnemyEnemyState target = state.Enemies.First(enemy => !enemy.IsIncapacitated);
                if (!state.IsTargetSelected(target.Profile.EnemyId) &&
                    !state.SelectTarget(target.Profile.EnemyId))
                {
                    return false;
                }

                MultiEnemyHeroAction action;
                if (state.CanUseHeroAction(MultiEnemyHeroAction.Technique))
                {
                    action = MultiEnemyHeroAction.Technique;
                }
                else if (state.TechniqueCooldownRemaining == 1)
                {
                    action = MultiEnemyHeroAction.Guard;
                }
                else
                {
                    action = MultiEnemyHeroAction.Attack;
                }

                HeroActionResolution hero = state.ResolveHeroAction(
                    action,
                    action == MultiEnemyHeroAction.Guard ? null : target.Profile.EnemyId);
                if (!hero.Accepted)
                {
                    return false;
                }

                while (state.Phase == MultiEnemyBattlePhase.AwaitingMoralChoices)
                {
                    MoralChoiceResolution choice = state.ResolveMoralChoice(
                        state.PendingMoralEnemyId,
                        EnemyMoralOutcome.Saved);
                    if (!choice.Accepted) return false;
                }

                if (!state.AllEnemiesIncapacitated)
                {
                    EnemyPhaseResolution enemy = state.ResolveEnemyPhase();
                    if (!enemy.Accepted || enemy.HeroDefeated)
                    {
                        return false;
                    }
                }
            }

            return state.AllEnemiesIncapacitated;
        }

        private static bool PlansAreEqual(
            IReadOnlyList<EnemyTurnPlan> first,
            IReadOnlyList<EnemyTurnPlan> second)
        {
            if (first.Count != second.Count)
            {
                return false;
            }

            for (int index = 0; index < first.Count; index++)
            {
                if (first[index].EnemyId != second[index].EnemyId ||
                    first[index].TurnNumber != second[index].TurnNumber ||
                    first[index].TrueIntent != second[index].TrueIntent ||
                    first[index].DisplayedIntent != second[index].DisplayedIntent ||
                    first[index].IsBluff != second[index].IsBluff ||
                    first[index].InstabilityClue != second[index].InstabilityClue)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsOffensive(MultiEnemyIntent intent)
        {
            return intent == MultiEnemyIntent.Attack ||
                   intent == MultiEnemyIntent.Assault ||
                   intent == MultiEnemyIntent.ChargedStrike;
        }

        private static string GetString(SerializedProperty parent, string propertyName)
        {
            SerializedProperty property = parent.FindPropertyRelative(propertyName);
            return property != null ? property.stringValue : string.Empty;
        }

        private static int GetInt(SerializedProperty parent, string propertyName)
        {
            SerializedProperty property = parent.FindPropertyRelative(propertyName);
            return property != null ? property.intValue : int.MinValue;
        }

        private static float GetFloat(SerializedProperty parent, string propertyName)
        {
            SerializedProperty property = parent.FindPropertyRelative(propertyName);
            return property != null && property.propertyType == SerializedPropertyType.Float
                ? property.floatValue
                : float.NaN;
        }

        private static void RunCheck(List<string> errors, string label, Action validation)
        {
            try
            {
                validation();
            }
            catch (Exception exception)
            {
                errors.Add(label + ": " + exception.GetType().Name + " — " + exception.Message);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static void RequireThrows<TException>(Action action, string message)
            where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }

            throw new InvalidOperationException(message);
        }

        private static void CompleteValidation(IReadOnlyCollection<string> errors)
        {
            if (errors.Count == 0)
            {
                Debug.Log(
                    "[Veyra Progression Validation] SUPERATA — menu 4/10, Hero01, XP, " +
                    "potenziamenti, alleati salvati, Assalto dei Tre, bluff, scene persistenti " +
                    "e Build Settings sono conformi.");
                return;
            }

            Debug.LogError(
                "[Veyra Progression Validation] FALLITA (" + errors.Count + " problemi):\n- " +
                string.Join("\n- ", errors));
        }

        /// <summary>
        /// Saves and restores only the PlayerPrefs keys touched by the progression tests.
        /// It never enumerates or clears unrelated user preferences.
        /// </summary>
        private sealed class ProgressPrefsScope : IDisposable
        {
            private readonly List<PreferenceSnapshot> snapshots = new List<PreferenceSnapshot>
            {
                PreferenceSnapshot.String(CampaignProgressStore.ProgressKey),
                PreferenceSnapshot.String(HeroProgressStore.ProgressKey),
                PreferenceSnapshot.Int(LocalSettingsStore.VersionKey),
                PreferenceSnapshot.Float(LocalSettingsStore.MasterVolumeKey),
                PreferenceSnapshot.Float(LocalSettingsStore.MusicVolumeKey),
                PreferenceSnapshot.Float(LocalSettingsStore.SfxVolumeKey),
                PreferenceSnapshot.Int(LocalSettingsStore.VibrationEnabledKey)
            };

            public void Dispose()
            {
                foreach (PreferenceSnapshot snapshot in snapshots)
                {
                    snapshot.Restore();
                }

                PlayerPrefs.Save();
            }
        }

        private sealed class PreferenceSnapshot
        {
            private enum ValueKind
            {
                String,
                Int,
                Float
            }

            private readonly string key;
            private readonly ValueKind kind;
            private readonly bool existed;
            private readonly string stringValue;
            private readonly int intValue;
            private readonly float floatValue;

            private PreferenceSnapshot(string key, ValueKind kind)
            {
                this.key = key;
                this.kind = kind;
                existed = PlayerPrefs.HasKey(key);
                stringValue = existed && kind == ValueKind.String
                    ? PlayerPrefs.GetString(key)
                    : string.Empty;
                intValue = existed && kind == ValueKind.Int ? PlayerPrefs.GetInt(key) : 0;
                floatValue = existed && kind == ValueKind.Float ? PlayerPrefs.GetFloat(key) : 0f;
            }

            internal static PreferenceSnapshot String(string key)
            {
                return new PreferenceSnapshot(key, ValueKind.String);
            }

            internal static PreferenceSnapshot Int(string key)
            {
                return new PreferenceSnapshot(key, ValueKind.Int);
            }

            internal static PreferenceSnapshot Float(string key)
            {
                return new PreferenceSnapshot(key, ValueKind.Float);
            }

            internal void Restore()
            {
                if (!existed)
                {
                    PlayerPrefs.DeleteKey(key);
                    return;
                }

                switch (kind)
                {
                    case ValueKind.String:
                        PlayerPrefs.SetString(key, stringValue);
                        break;
                    case ValueKind.Int:
                        PlayerPrefs.SetInt(key, intValue);
                        break;
                    case ValueKind.Float:
                        PlayerPrefs.SetFloat(key, floatValue);
                        break;
                }
            }
        }
    }
}
#endif
