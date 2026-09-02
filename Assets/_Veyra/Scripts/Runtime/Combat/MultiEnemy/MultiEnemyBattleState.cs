using System;
using System.Collections.Generic;
using Veyra.Combat;

namespace Veyra.Combat.MultiEnemy
{
    /// <summary>
    /// Pure, deterministic combat model for encounters with several simultaneous enemies.
    /// It owns no scene objects and can be driven one animation step at a time by a controller.
    /// </summary>
    public sealed class MultiEnemyBattleState
    {
        public const int MaximumOffensiveActionsPerEnemyPhase = 2;
        public const int MaximumChargedStrikesPerEnemyPhase = 1;

        private const string WrongPhaseReason = "Azione non valida nella fase attuale.";
        private const string NoTargetReason = "Nessun nemico attivo disponibile come bersaglio.";
        private const string UnknownTargetReason = "Il bersaglio non appartiene a questo scontro.";

        private readonly int seed;
        private readonly List<MultiEnemyEnemyState> enemies =
            new List<MultiEnemyEnemyState>();
        private readonly Dictionary<string, MultiEnemyEnemyState> enemiesById =
            new Dictionary<string, MultiEnemyEnemyState>(StringComparer.Ordinal);
        private readonly List<EnemyTurnPlan> currentPlans = new List<EnemyTurnPlan>();
        private readonly Dictionary<string, EnemyTurnPlan> plansByEnemyId =
            new Dictionary<string, EnemyTurnPlan>(StringComparer.Ordinal);
        private readonly IReadOnlyList<MultiEnemyEnemyState> enemiesView;
        private readonly Queue<string> pendingMoralEnemyIds = new Queue<string>();
        private readonly MultiEnemyPlayerTendencies initialPlayerTendencies;

        private System.Random random;
        private MultiEnemyPlayerTendencies playerTendencies;
        private bool plansLocked;
        private int heroGuardBlocksRemaining;
        private bool bastionActiveForEnemyPhase;
        private bool analyzedPlansRevealed;
        private bool analyzeExposedAppliedThisTurn;
        private bool resumeEnemyPhaseAfterMoralChoice;

        public MultiEnemyBattleState(
            MultiEnemyBattleRules rules,
            IEnumerable<MultiEnemyProfile> profiles,
            HeroSkillUpgrades upgrades,
            int seed,
            MultiEnemyPlayerTendencies? historicalPlayerTendencies = null)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Upgrades = upgrades;
            this.seed = seed;
            initialPlayerTendencies = historicalPlayerTendencies ??
                                      MultiEnemyPlayerTendencies.None;

            if (profiles == null)
            {
                throw new ArgumentNullException(nameof(profiles));
            }

            foreach (MultiEnemyProfile profile in profiles)
            {
                if (profile == null)
                {
                    throw new ArgumentException("Enemy profiles cannot contain null.", nameof(profiles));
                }

                if (enemiesById.ContainsKey(profile.EnemyId))
                {
                    throw new ArgumentException(
                        "Enemy ids must be unique: " + profile.EnemyId,
                        nameof(profiles));
                }

                MultiEnemyEnemyState enemy = new MultiEnemyEnemyState(profile);
                enemies.Add(enemy);
                enemiesById.Add(profile.EnemyId, enemy);
            }

            if (enemies.Count == 0)
            {
                throw new ArgumentException("At least one enemy is required.", nameof(profiles));
            }

            enemiesView = enemies.AsReadOnly();
            Reset();
        }

        public MultiEnemyBattleRules Rules { get; }

        public HeroSkillUpgrades Upgrades { get; }

        public IReadOnlyList<MultiEnemyEnemyState> Enemies => enemiesView;

        public IReadOnlyList<EnemyTurnPlan> CurrentPlans => currentPlans.AsReadOnly();

        public int HeroMaxHp => Rules.HeroMaxHp;

        public int HeroHp { get; private set; }

        public int TurnNumber { get; private set; }

        public int TechniqueCooldownRemaining { get; private set; }

        public bool AnalyzeExposedAppliedThisTurn => analyzeExposedAppliedThisTurn;

        public string SelectedEnemyId { get; private set; }

        public string LastAutoSelectedEnemyId { get; private set; }

        public MultiEnemyEnemyState SelectedEnemy
        {
            get
            {
                MultiEnemyEnemyState selected;
                return !string.IsNullOrEmpty(SelectedEnemyId) &&
                       enemiesById.TryGetValue(SelectedEnemyId, out selected) &&
                       !selected.IsIncapacitated
                    ? selected
                    : null;
            }
        }

        public bool HasValidSelectedTarget => SelectedEnemy != null;

        public bool RequiresTargetSelection =>
            Phase == MultiEnemyBattlePhase.HeroTurn &&
            !AllEnemiesIncapacitated &&
            !HasValidSelectedTarget;

        public int ActiveEnemyCount => CountActiveEnemies();

        public MultiEnemyPlayerTendencies PlayerTendencies => playerTendencies;

        public MultiEnemyBattlePhase Phase { get; private set; }

        public bool ArePlansLocked => plansLocked;

        public bool AnalyzedPlansRevealed => analyzedPlansRevealed;

        public bool AllEnemiesIncapacitated => CountActiveEnemies() == 0;

        public string PendingMoralEnemyId =>
            pendingMoralEnemyIds.Count > 0 ? pendingMoralEnemyIds.Peek() : string.Empty;

        public bool AllMoralChoicesCompleted
        {
            get
            {
                if (!AllEnemiesIncapacitated)
                {
                    return false;
                }

                for (int index = 0; index < enemies.Count; index++)
                {
                    if (enemies[index].MoralOutcome == EnemyMoralOutcome.None)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public int EffectiveAttackDamage => Rules.HeroAttackDamage +
                                            (Upgrades.AttackMastery ? Rules.AttackUpgradeBonus : 0);

        public int EffectiveTechniqueDamage => Rules.HeroTechniqueDamage +
                                               (Upgrades.TechniqueMastery
                                                   ? Rules.TechniqueUpgradeBonus
                                                   : 0);

        public int EffectiveTechniqueSplashPercent => Upgrades.TechniqueMastery
            ? Rules.UpgradedTechniqueSplashPercent
            : Rules.BaseTechniqueSplashPercent;

        public bool SelectTarget(string enemyId)
        {
            MultiEnemyEnemyState enemy;
            if (!CanSelectTarget(enemyId) ||
                !enemiesById.TryGetValue(enemyId, out enemy))
            {
                return false;
            }

            SelectedEnemyId = enemy.Profile.EnemyId;
            LastAutoSelectedEnemyId = string.Empty;
            return true;
        }

        public bool CanSelectTarget(string enemyId)
        {
            return Phase == MultiEnemyBattlePhase.HeroTurn && IsActiveTarget(enemyId);
        }

        public bool IsTargetSelected(string enemyId)
        {
            return HasValidSelectedTarget &&
                   string.Equals(SelectedEnemyId, enemyId, StringComparison.Ordinal);
        }

        public MultiEnemyEnemyState GetEnemy(string enemyId)
        {
            MultiEnemyEnemyState enemy;
            return !string.IsNullOrWhiteSpace(enemyId) &&
                   enemiesById.TryGetValue(enemyId, out enemy)
                ? enemy
                : null;
        }

        public EnemyTurnPlan GetPlan(string enemyId)
        {
            EnemyTurnPlan plan;
            return !string.IsNullOrWhiteSpace(enemyId) &&
                   plansByEnemyId.TryGetValue(enemyId, out plan)
                ? plan
                : null;
        }

        public MultiEnemyIntent? GetVisibleIntent(string enemyId)
        {
            EnemyTurnPlan plan = GetPlan(enemyId);
            return plan == null
                ? (MultiEnemyIntent?)null
                : plan.GetVisibleIntent(analyzedPlansRevealed);
        }

        public bool CanUseHeroAction(MultiEnemyHeroAction action)
        {
            return CanUseHeroAction(action, SelectedEnemyId);
        }

        public bool CanUseHeroAction(MultiEnemyHeroAction action, string targetEnemyId)
        {
            if (Phase != MultiEnemyBattlePhase.HeroTurn || AllEnemiesIncapacitated)
            {
                return false;
            }

            switch (action)
            {
                case MultiEnemyHeroAction.Guard:
                    return true;
                case MultiEnemyHeroAction.Attack:
                case MultiEnemyHeroAction.Analyze:
                    return IsActiveTarget(targetEnemyId);
                case MultiEnemyHeroAction.Technique:
                    return TechniqueCooldownRemaining == 0 && IsActiveTarget(targetEnemyId);
                default:
                    return false;
            }
        }

        public HeroActionResolution ResolveHeroAction(
            MultiEnemyHeroAction action,
            string targetEnemyId = null)
        {
            string targetForValidation = string.IsNullOrWhiteSpace(targetEnemyId)
                ? SelectedEnemyId
                : targetEnemyId;
            if (!CanUseHeroAction(action, targetForValidation))
            {
                string reason = GetHeroActionRejectionReason(action, targetForValidation);
                return RejectHeroAction(action, reason);
            }

            LastAutoSelectedEnemyId = string.Empty;

            if (action == MultiEnemyHeroAction.Analyze)
            {
                HeroActionResolution analyze = ResolveAnalyze(targetForValidation);
                if (analyze.Accepted)
                {
                    playerTendencies = playerTendencies.WithRecordedAction(action);
                }

                return analyze;
            }

            List<DamageEvent> damageEvents = new List<DamageEvent>();
            string resolvedTargetId = string.Empty;
            bool guardPrepared = false;

            if (action == MultiEnemyHeroAction.Attack ||
                action == MultiEnemyHeroAction.Technique)
            {
                MultiEnemyEnemyState target;
                string targetFailure;
                if (!TryResolveTarget(targetForValidation, out target, out targetFailure))
                {
                    return RejectHeroAction(action, targetFailure);
                }

                resolvedTargetId = target.Profile.EnemyId;
                int directDamage = action == MultiEnemyHeroAction.Attack
                    ? EffectiveAttackDamage
                    : EffectiveTechniqueDamage;
                damageEvents.Add(ApplyHeroDamage(target, directDamage, false));

                if (action == MultiEnemyHeroAction.Technique)
                {
                    int splashDamage = ScalePercent(
                        EffectiveTechniqueDamage,
                        EffectiveTechniqueSplashPercent);
                    for (int index = 0; index < enemies.Count; index++)
                    {
                        MultiEnemyEnemyState splashTarget = enemies[index];
                        if (splashTarget == target || splashTarget.IsIncapacitated)
                        {
                            continue;
                        }

                        damageEvents.Add(ApplyHeroDamage(splashTarget, splashDamage, true));
                    }

                    TechniqueCooldownRemaining = Rules.TechniqueCooldownTurns;
                }
            }
            else if (action == MultiEnemyHeroAction.Guard)
            {
                guardPrepared = true;
                bastionActiveForEnemyPhase = Upgrades.Bastion;
                heroGuardBlocksRemaining = Upgrades.Bastion ? int.MaxValue : 1;
            }

            if (action != MultiEnemyHeroAction.Technique && TechniqueCooldownRemaining > 0)
            {
                TechniqueCooldownRemaining--;
            }

            RefreshSelectedTargetAfterDamage();
            bool allIncapacitated = AllEnemiesIncapacitated;
            if (pendingMoralEnemyIds.Count > 0)
            {
                heroGuardBlocksRemaining = 0;
                bastionActiveForEnemyPhase = false;
                resumeEnemyPhaseAfterMoralChoice = !allIncapacitated;
                Phase = MultiEnemyBattlePhase.AwaitingMoralChoices;
            }
            else
            {
                Phase = MultiEnemyBattlePhase.EnemyPhase;
            }

            playerTendencies = playerTendencies.WithRecordedAction(action);
            return new HeroActionResolution(
                true,
                action,
                true,
                resolvedTargetId,
                damageEvents,
                Array.Empty<EnemyIntel>(),
                guardPrepared,
                guardPrepared && Upgrades.Bastion,
                allIncapacitated,
                string.Empty);
        }

        public EnemyPhaseResolution ResolveEnemyPhase()
        {
            int hpBefore = HeroHp;
            if (Phase != MultiEnemyBattlePhase.EnemyPhase)
            {
                return new EnemyPhaseResolution(
                    false,
                    Array.Empty<EnemyActionResolution>(),
                    hpBefore,
                    HeroHp,
                    HeroHp == 0,
                    WrongPhaseReason);
            }

            List<EnemyActionResolution> resolutions = new List<EnemyActionResolution>();
            for (int index = 0; index < currentPlans.Count; index++)
            {
                EnemyTurnPlan plan = currentPlans[index];
                MultiEnemyEnemyState enemy = enemiesById[plan.EnemyId];
                if (enemy.IsIncapacitated)
                {
                    resolutions.Add(new EnemyActionResolution(
                        plan,
                        true,
                        0,
                        false,
                        false,
                        false,
                        false,
                        plan.IsBluff));
                    continue;
                }

                int damage = 0;
                bool blocked = false;
                bool preparedGuard = false;
                bool beganCharge = false;
                bool heldCharge = false;

                switch (plan.TrueIntent)
                {
                    case MultiEnemyIntent.Attack:
                        damage = DamageHero(enemy.Profile.AttackDamage, out blocked);
                        enemy.ConsecutiveAttacks++;
                        break;
                    case MultiEnemyIntent.Assault:
                        damage = DamageHero(enemy.Profile.AssaultDamage, out blocked);
                        enemy.ConsecutiveAttacks = 0;
                        break;
                    case MultiEnemyIntent.ChargedStrike:
                        damage = DamageHero(enemy.Profile.ChargedStrikeDamage, out blocked);
                        enemy.ChargePrepared = false;
                        enemy.ChargeHoldAvailable = false;
                        enemy.ConsecutiveAttacks = 0;
                        break;
                    case MultiEnemyIntent.Guard:
                        enemy.GuardPrepared = true;
                        enemy.ConsecutiveAttacks = 0;
                        preparedGuard = true;
                        break;
                    case MultiEnemyIntent.Charge:
                        enemy.ChargePrepared = true;
                        enemy.ChargeHoldAvailable =
                            enemy.Profile.HasTrait(EnemyBehaviorTraits.Patient);
                        enemy.ConsecutiveAttacks = 0;
                        beganCharge = true;
                        break;
                    case MultiEnemyIntent.HoldCharge:
                        enemy.ChargeHoldAvailable = false;
                        enemy.ConsecutiveAttacks = 0;
                        heldCharge = true;
                        break;
                    case MultiEnemyIntent.Wait:
                    case MultiEnemyIntent.Finta:
                        enemy.ConsecutiveAttacks = 0;
                        break;
                }

                resolutions.Add(new EnemyActionResolution(
                    plan,
                    false,
                    damage,
                    blocked,
                    preparedGuard,
                    beganCharge,
                    heldCharge,
                    plan.IsBluff));

                if (HeroHp == 0)
                {
                    break;
                }
            }

            heroGuardBlocksRemaining = 0;
            bastionActiveForEnemyPhase = false;
            ClearPlans();

            if (HeroHp == 0)
            {
                Phase = MultiEnemyBattlePhase.HeroDefeated;
            }
            else
            {
                TurnNumber++;
                analyzedPlansRevealed = false;
                ClearExposedStatuses();
                analyzeExposedAppliedThisTurn = false;
                Phase = MultiEnemyBattlePhase.HeroTurn;
                PlanAndLockCurrentTurn();
            }

            return new EnemyPhaseResolution(
                true,
                resolutions,
                hpBefore,
                HeroHp,
                HeroHp == 0,
                string.Empty);
        }

        public bool PassHeroTurn()
        {
            if (Phase != MultiEnemyBattlePhase.HeroTurn || AllEnemiesIncapacitated)
            {
                return false;
            }

            heroGuardBlocksRemaining = 0;
            bastionActiveForEnemyPhase = false;
            if (TechniqueCooldownRemaining > 0)
            {
                TechniqueCooldownRemaining--;
            }

            Phase = MultiEnemyBattlePhase.EnemyPhase;
            return true;
        }

        /// <summary>
        /// Applies deterministic ally/support damage without consuming a turn, enemy guard,
        /// Exposed, technique cooldown, or locked intent. Damage can never incapacitate.
        /// </summary>
        public DamageEvent ApplyExternalNonLethalDamage(string targetEnemyId, int damage)
        {
            if (damage <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(damage));
            }

            if (Phase != MultiEnemyBattlePhase.HeroTurn &&
                Phase != MultiEnemyBattlePhase.EnemyPhase)
            {
                throw new InvalidOperationException(WrongPhaseReason);
            }

            MultiEnemyEnemyState target;
            if (!TryGetActiveEnemy(targetEnemyId, out target))
            {
                throw new InvalidOperationException(
                    GetTargetFailureReason(targetEnemyId));
            }

            int applied = Math.Min(damage, Math.Max(0, target.CurrentHp - 1));
            target.CurrentHp -= applied;
            return new DamageEvent(
                target.Profile.EnemyId,
                damage,
                applied,
                false,
                false,
                false,
                false,
                true);
        }

        public MoralChoiceResolution ResolveMoralChoice(
            string enemyId,
            EnemyMoralOutcome outcome)
        {
            if (Phase != MultiEnemyBattlePhase.AwaitingMoralChoices)
            {
                return new MoralChoiceResolution(
                    false,
                    enemyId,
                    outcome,
                    false,
                    WrongPhaseReason);
            }

            if (outcome != EnemyMoralOutcome.Saved && outcome != EnemyMoralOutcome.Killed)
            {
                return new MoralChoiceResolution(
                    false,
                    enemyId,
                    outcome,
                    false,
                    "Choose Saved or Killed.");
            }

            MultiEnemyEnemyState enemy;
            if (string.IsNullOrWhiteSpace(enemyId) ||
                !enemiesById.TryGetValue(enemyId, out enemy))
            {
                return new MoralChoiceResolution(
                    false,
                    enemyId,
                    outcome,
                    false,
                    UnknownTargetReason);
            }

            if (!enemy.IsIncapacitated)
            {
                return new MoralChoiceResolution(
                    false,
                    enemyId,
                    outcome,
                    false,
                    "Only an incapacitated enemy can receive a moral outcome.");
            }

            if (enemy.MoralOutcome != EnemyMoralOutcome.None)
            {
                return new MoralChoiceResolution(
                    false,
                    enemyId,
                    outcome,
                    AllMoralChoicesCompleted,
                    "This enemy's choice has already been confirmed.");
            }

            if (pendingMoralEnemyIds.Count == 0 ||
                !string.Equals(pendingMoralEnemyIds.Peek(), enemyId, StringComparison.Ordinal))
            {
                return new MoralChoiceResolution(
                    false,
                    enemyId,
                    outcome,
                    false,
                    "Resolve the currently displayed enemy first.");
            }

            enemy.MoralOutcome = outcome;
            pendingMoralEnemyIds.Dequeue();
            bool complete = AllMoralChoicesCompleted && pendingMoralEnemyIds.Count == 0;
            if (complete)
            {
                Phase = MultiEnemyBattlePhase.Completed;
            }
            else if (pendingMoralEnemyIds.Count == 0 && resumeEnemyPhaseAfterMoralChoice)
            {
                resumeEnemyPhaseAfterMoralChoice = false;
                Phase = MultiEnemyBattlePhase.EnemyPhase;
            }

            return new MoralChoiceResolution(
                true,
                enemy.Profile.EnemyId,
                outcome,
                complete,
                string.Empty);
        }

        public IReadOnlyList<EnemyTurnPlan> PlanAndLockCurrentTurn()
        {
            if (Phase != MultiEnemyBattlePhase.HeroTurn)
            {
                return currentPlans.AsReadOnly();
            }

            if (plansLocked)
            {
                return currentPlans.AsReadOnly();
            }

            currentPlans.Clear();
            plansByEnemyId.Clear();
            int activeCount = CountActiveEnemies();
            int offensiveActions = 0;
            int chargedStrikes = 0;

            for (int index = 0; index < enemies.Count; index++)
            {
                MultiEnemyEnemyState enemy = enemies[index];
                if (enemy.IsIncapacitated)
                {
                    continue;
                }

                EnemyPlanningContext context = new EnemyPlanningContext(
                    enemy,
                    TurnNumber,
                    HeroHp,
                    HeroMaxHp,
                    activeCount,
                    playerTendencies);
                EnemyBehaviorComposer composer = new EnemyBehaviorComposer(enemy.Profile);
                MultiEnemyIntent trueIntent = composer.ChooseTrueIntent(context, random);

                if (trueIntent == MultiEnemyIntent.ChargedStrike &&
                    chargedStrikes >= MaximumChargedStrikesPerEnemyPhase)
                {
                    trueIntent = MultiEnemyIntent.HoldCharge;
                }

                if (IsOffensive(trueIntent) &&
                    offensiveActions >= MaximumOffensiveActionsPerEnemyPhase)
                {
                    trueIntent = enemy.ChargePrepared
                        ? MultiEnemyIntent.HoldCharge
                        : ChooseCoordinatedNonOffensiveFallback(enemy);
                }

                if (trueIntent == MultiEnemyIntent.ChargedStrike)
                {
                    chargedStrikes++;
                }

                if (IsOffensive(trueIntent))
                {
                    offensiveActions++;
                }

                bool isBluff;
                string clue;
                MultiEnemyIntent displayedIntent = composer.ChooseDisplayedIntent(
                    context,
                    trueIntent,
                    random,
                    out isBluff,
                    out clue);
                EnemyTurnPlan plan = new EnemyTurnPlan(
                    enemy.Profile.EnemyId,
                    TurnNumber,
                    trueIntent,
                    displayedIntent,
                    isBluff,
                    clue);
                currentPlans.Add(plan);
                plansByEnemyId.Add(plan.EnemyId, plan);
            }

            plansLocked = true;
            return currentPlans.AsReadOnly();
        }

        public void Reset()
        {
            random = new System.Random(seed);
            HeroHp = Rules.HeroMaxHp;
            TurnNumber = 1;
            TechniqueCooldownRemaining = 0;
            Phase = MultiEnemyBattlePhase.HeroTurn;
            heroGuardBlocksRemaining = 0;
            bastionActiveForEnemyPhase = false;
            analyzedPlansRevealed = false;
            analyzeExposedAppliedThisTurn = false;
            resumeEnemyPhaseAfterMoralChoice = false;
            pendingMoralEnemyIds.Clear();
            playerTendencies = initialPlayerTendencies;
            ClearPlans();

            for (int index = 0; index < enemies.Count; index++)
            {
                enemies[index].Reset();
            }

            SelectedEnemyId = string.Empty;
            LastAutoSelectedEnemyId = string.Empty;
            PlanAndLockCurrentTurn();
        }

        private HeroActionResolution ResolveAnalyze(string targetEnemyId)
        {
            List<EnemyIntel> intel = new List<EnemyIntel>();
            string resolvedTarget = string.Empty;

            if (Upgrades.AnalyzeMastery)
            {
                MultiEnemyEnemyState exposedTarget;
                string failure;
                if (!TryResolveTarget(targetEnemyId, out exposedTarget, out failure))
                {
                    return RejectHeroAction(MultiEnemyHeroAction.Analyze, failure);
                }

                resolvedTarget = exposedTarget.Profile.EnemyId;
                if (!analyzeExposedAppliedThisTurn)
                {
                    exposedTarget.Exposed = true;
                    analyzeExposedAppliedThisTurn = true;
                }

                analyzedPlansRevealed = true;
                for (int index = 0; index < enemies.Count; index++)
                {
                    MultiEnemyEnemyState enemy = enemies[index];
                    if (enemy.IsIncapacitated)
                    {
                        continue;
                    }

                    intel.Add(new EnemyIntel(enemy, GetPlan(enemy.Profile.EnemyId), true));
                }
            }
            else
            {
                MultiEnemyEnemyState target;
                string failure;
                if (!TryResolveTarget(targetEnemyId, out target, out failure))
                {
                    return RejectHeroAction(MultiEnemyHeroAction.Analyze, failure);
                }

                resolvedTarget = target.Profile.EnemyId;
                intel.Add(new EnemyIntel(target, GetPlan(target.Profile.EnemyId), false));
            }

            return new HeroActionResolution(
                true,
                MultiEnemyHeroAction.Analyze,
                false,
                resolvedTarget,
                Array.Empty<DamageEvent>(),
                intel,
                false,
                false,
                false,
                string.Empty);
        }

        private DamageEvent ApplyHeroDamage(
            MultiEnemyEnemyState target,
            int requestedDamage,
            bool wasSplash)
        {
            bool usedExposed = target.Exposed;
            int damage = requestedDamage;
            if (usedExposed)
            {
                damage = ScalePercent(damage, 100 + Rules.ExposedBonusPercent);
                target.Exposed = false;
            }

            CombatDamageResolution resolution = CombatDamageResolver.Resolve(
                damage,
                target.GuardPrepared);
            bool reducedByGuard = resolution.BlockedByGuard;
            damage = resolution.AppliedDamage;
            if (target.GuardPrepared) target.GuardPrepared = false;

            int hpBefore = target.CurrentHp;
            target.CurrentHp = Math.Max(0, target.CurrentHp - damage);
            int applied = hpBefore - target.CurrentHp;
            if (target.IsIncapacitated)
            {
                pendingMoralEnemyIds.Enqueue(target.Profile.EnemyId);
                target.GuardPrepared = false;
                target.ChargePrepared = false;
                target.ChargeHoldAvailable = false;
                target.Exposed = false;
                target.ConsecutiveAttacks = 0;
                if (SelectedEnemyId == target.Profile.EnemyId)
                {
                    SelectedEnemyId = string.Empty;
                }

                RemovePlanForEnemy(target.Profile.EnemyId);
            }

            return new DamageEvent(
                target.Profile.EnemyId,
                requestedDamage,
                applied,
                wasSplash,
                reducedByGuard,
                usedExposed,
                target.IsIncapacitated,
                false);
        }

        private int DamageHero(int requestedDamage, out bool blockedByGuard)
        {
            CombatDamageResolution resolution = CombatDamageResolver.Resolve(
                requestedDamage,
                heroGuardBlocksRemaining > 0);
            blockedByGuard = resolution.BlockedByGuard;
            if (blockedByGuard)
            {
                if (!bastionActiveForEnemyPhase)
                {
                    heroGuardBlocksRemaining--;
                }

                return 0;
            }

            int hpBefore = HeroHp;
            HeroHp = Math.Max(0, HeroHp - resolution.AppliedDamage);
            return hpBefore - HeroHp;
        }

        private bool TryResolveTarget(
            string requestedId,
            out MultiEnemyEnemyState target,
            out string failure)
        {
            target = null;
            failure = string.Empty;

            if (!string.IsNullOrWhiteSpace(requestedId))
            {
                if (!TryGetActiveEnemy(requestedId, out target))
                {
                    failure = GetTargetFailureReason(requestedId);
                    return false;
                }

                SelectedEnemyId = target.Profile.EnemyId;
                return true;
            }

            if (!string.IsNullOrEmpty(SelectedEnemyId) &&
                enemiesById.TryGetValue(SelectedEnemyId, out target) &&
                !target.IsIncapacitated)
            {
                return true;
            }

            failure = NoTargetReason;
            return false;
        }

        private void RefreshSelectedTargetAfterDamage()
        {
            if (HasValidSelectedTarget)
            {
                return;
            }

            SelectedEnemyId = string.Empty;
            if (CountActiveEnemies() != 1)
            {
                return;
            }

            for (int index = 0; index < enemies.Count; index++)
            {
                if (!enemies[index].IsIncapacitated)
                {
                    SelectedEnemyId = enemies[index].Profile.EnemyId;
                    LastAutoSelectedEnemyId = SelectedEnemyId;
                    return;
                }
            }
        }

        private bool IsActiveTarget(string enemyId)
        {
            MultiEnemyEnemyState enemy;
            return TryGetActiveEnemy(enemyId, out enemy);
        }

        private bool TryGetActiveEnemy(string enemyId, out MultiEnemyEnemyState enemy)
        {
            enemy = null;
            return !string.IsNullOrWhiteSpace(enemyId) &&
                   enemiesById.TryGetValue(enemyId, out enemy) &&
                   !enemy.IsIncapacitated;
        }

        private string GetHeroActionRejectionReason(
            MultiEnemyHeroAction action,
            string targetEnemyId)
        {
            if (Phase != MultiEnemyBattlePhase.HeroTurn || AllEnemiesIncapacitated)
            {
                return WrongPhaseReason;
            }

            if (action == MultiEnemyHeroAction.Technique && TechniqueCooldownRemaining > 0)
            {
                return "La Tecnica non è ancora pronta.";
            }

            if (action == MultiEnemyHeroAction.Attack ||
                action == MultiEnemyHeroAction.Technique ||
                action == MultiEnemyHeroAction.Analyze)
            {
                return GetTargetFailureReason(targetEnemyId);
            }

            return "Azione non disponibile.";
        }

        private string GetTargetFailureReason(string enemyId)
        {
            if (string.IsNullOrWhiteSpace(enemyId))
            {
                return "Seleziona prima un bersaglio attivo.";
            }

            MultiEnemyEnemyState enemy;
            if (!enemiesById.TryGetValue(enemyId, out enemy))
            {
                return UnknownTargetReason;
            }

            return enemy.IsIncapacitated
                ? "Il bersaglio è già incapacitato."
                : NoTargetReason;
        }

        private HeroActionResolution RejectHeroAction(
            MultiEnemyHeroAction action,
            string reason)
        {
            return new HeroActionResolution(
                false,
                action,
                false,
                string.Empty,
                Array.Empty<DamageEvent>(),
                Array.Empty<EnemyIntel>(),
                false,
                false,
                AllEnemiesIncapacitated,
                reason);
        }

        private int CountActiveEnemies()
        {
            int count = 0;
            for (int index = 0; index < enemies.Count; index++)
            {
                if (!enemies[index].IsIncapacitated)
                {
                    count++;
                }
            }

            return count;
        }

        private void ClearPlans()
        {
            currentPlans.Clear();
            plansByEnemyId.Clear();
            plansLocked = false;
        }

        private void RemovePlanForEnemy(string enemyId)
        {
            plansByEnemyId.Remove(enemyId);
            for (int index = currentPlans.Count - 1; index >= 0; index--)
            {
                if (currentPlans[index].EnemyId == enemyId)
                {
                    currentPlans.RemoveAt(index);
                }
            }
        }

        private void ClearExposedStatuses()
        {
            for (int index = 0; index < enemies.Count; index++)
            {
                enemies[index].Exposed = false;
            }
        }

        private static MultiEnemyIntent ChooseCoordinatedNonOffensiveFallback(
            MultiEnemyEnemyState enemy)
        {
            if (enemy.Profile.HasTrait(EnemyBehaviorTraits.Patient) || enemy.GuardPrepared)
            {
                return MultiEnemyIntent.Wait;
            }

            return MultiEnemyIntent.Guard;
        }

        private static bool IsOffensive(MultiEnemyIntent intent)
        {
            return intent == MultiEnemyIntent.Attack ||
                   intent == MultiEnemyIntent.Assault ||
                   intent == MultiEnemyIntent.ChargedStrike;
        }

        private static int ScalePercent(int value, int percent)
        {
            return Math.Max(0, (value * percent + 50) / 100);
        }
    }
}
