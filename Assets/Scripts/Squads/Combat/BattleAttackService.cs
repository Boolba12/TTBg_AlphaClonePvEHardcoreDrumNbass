using System;
using System.Linq;
using UnityEngine;

public sealed class BattleAttackService : MonoBehaviour
{
    [SerializeField] private SquadBattleBootstrap squadBootstrap;
    [SerializeField] private BattleTurnController turnController;
    [SerializeField] private BattleSquadSelectionController selectionController;
    [SerializeField] private SquadMovementService movementService;
    [SerializeField] private AttackDefinition basicAttack;
    [SerializeField] private BattleCombatRules combatRules;
    [SerializeField] private bool allowDiagonalRange = true;
    [SerializeField] private int battleRandomSeed = 42;
    [SerializeField] private bool enableDevelopmentLogs;

    private IBattleRandomSource randomSource;
    private Func<bool> movementInProgress;
    private BattleTargetingService targetingService;
    private BattleAttackCalculator calculator;

    public bool IsInitialized { get; private set; }
    public bool IsExecuting { get; private set; }
    public bool CommandsEnabled { get; private set; } = true;
    public AttackDefinition BasicAttack => basicAttack;
    public BattleCombatRules CombatRules => combatRules;
    public BattleTargetingService TargetingService => targetingService;
    public BattleAttackCalculator Calculator => calculator;

    public event Action<BattleAttackResult> OnAttackStarted;
    public event Action<BattleAttackResult> OnAttackResolved;
    public event Action<BattleAttackResult> OnAttackMissed;
    public event Action<BattleAttackResult> OnAttackHit;
    public event Action<BattleAttackResult> OnCriticalHit;

    public void Configure(
        SquadBattleBootstrap bootstrap,
        BattleTurnController turns,
        BattleSquadSelectionController selection,
        SquadMovementService movement,
        AttackDefinition definition,
        BattleCombatRules rules,
        bool allowDiagonal,
        int randomSeed,
        IBattleRandomSource configuredRandomSource = null,
        Func<bool> configuredMovementInProgress = null)
    {
        squadBootstrap = bootstrap;
        turnController = turns;
        selectionController = selection;
        movementService = movement;
        basicAttack = definition;
        combatRules = rules;
        allowDiagonalRange = allowDiagonal;
        battleRandomSeed = randomSeed;
        randomSource = configuredRandomSource;
        movementInProgress = configuredMovementInProgress;
        IsInitialized = false;
    }

    public bool Initialize()
    {
        if (IsInitialized)
            return false;
        string reason = null;
        if (squadBootstrap == null || !squadBootstrap.HasBootstrapped ||
            turnController == null || !turnController.HasStarted ||
            selectionController == null || !selectionController.IsInitialized ||
            movementService == null || !movementService.IsInitialized ||
            combatRules == null || basicAttack == null ||
            !basicAttack.Validate(out reason))
        {
            Debug.LogError(
                $"BattleAttackService: initialization failed. {reason}",
                this);
            return false;
        }

        randomSource ??= new SeededBattleRandomSource(battleRandomSeed);
        targetingService = new BattleTargetingService(allowDiagonalRange);
        calculator = new BattleAttackCalculator(combatRules);
        IsInitialized = true;
        CommandsEnabled = true;
        return true;
    }

    public void SetCommandsEnabled(bool enabled) => CommandsEnabled = enabled;

    public void SetRandomSourceForTests(IBattleRandomSource source)
    {
        randomSource = source ?? throw new ArgumentNullException(nameof(source));
    }

    public BattleAttackValidationResult ValidateAvailability(
        SquadBattleController attacker,
        AttackDefinition definition = null,
        bool requireSelected = false,
        bool requireTargetInRange = true,
        BattleCommandAuthority authority = BattleCommandAuthority.HumanInput)
    {
        definition ??= basicAttack;
        BattleAttackValidationResult validation = ValidateAttacker(
            attacker,
            definition,
            requireSelected,
            authority);
        if (!validation.IsValid || !requireTargetInRange)
            return validation;

        if (targetingService.GetValidTargets(
                attacker,
                squadBootstrap.SpawnedControllers,
                definition).Count == 0)
        {
            return BattleAttackValidationResult.Reject(
                BattleAttackFailureReason.NoTargetsInRange,
                "No enemy targets are in attack range.");
        }
        return BattleAttackValidationResult.Accepted;
    }

    public BattleAttackValidationResult ValidateCommand(
        SquadBattleController attacker,
        SquadBattleController target,
        AttackDefinition definition = null,
        BattleCommandAuthority authority = BattleCommandAuthority.HumanInput)
    {
        definition ??= basicAttack;
        BattleAttackValidationResult attackerValidation = ValidateAttacker(
            attacker,
            definition,
            true,
            authority);
        if (!attackerValidation.IsValid)
            return attackerValidation;
        if (!squadBootstrap.SpawnedControllers.Contains(target))
        {
            return BattleAttackValidationResult.Reject(
                BattleAttackFailureReason.InvalidTarget,
                "Target is not a registered battle participant.");
        }
        return targetingService.ValidateTarget(attacker, target, definition);
    }

    public BattleAttackPreview PreviewAttack(
        SquadBattleController attacker,
        SquadBattleController target,
        AttackDefinition definition = null,
        BattleCommandAuthority authority = BattleCommandAuthority.HumanInput)
    {
        definition ??= basicAttack;
        BattleAttackValidationResult validation = ValidateCommand(
            attacker,
            target,
            definition,
            authority);
        if (attacker == null || target == null || definition == null || calculator == null)
        {
            return new BattleAttackPreview(
                attacker?.SquadId,
                target?.SquadId,
                definition?.StableId,
                validation,
                definition?.ActionPointCost ?? 0,
                0f,
                0f,
                0,
                0,
                definition?.DamageType ?? BattleDamageType.Physical,
                target?.Runtime?.State?.CurrentSquadHP ?? 0,
                target?.Runtime?.Stats.MaxHP ?? 0,
                CountLivingWarriors(target?.Runtime),
                attacker?.Runtime?.Equipment?.GetWeaponForAttack(definition)?.DefinitionId);
        }

        float hitChance = calculator.CalculateHitChance(
            attacker.Runtime.Stats,
            target.Runtime.Stats);
        WeaponCombatSnapshot weapon = attacker.Runtime.Equipment.GetWeaponForAttack(definition);
        float criticalChance = calculator.CalculateCriticalChance(
            attacker.Runtime.Stats,
            definition);
        BattleDamageCalculation normal = calculator.CalculateDamage(
            attacker.Runtime.Stats,
            target.Runtime.Stats,
            definition,
            false,
            weapon);
        BattleDamageCalculation critical = calculator.CalculateDamage(
            attacker.Runtime.Stats,
            target.Runtime.Stats,
            definition,
            true,
            weapon);
        return new BattleAttackPreview(
            attacker.SquadId,
            target.SquadId,
            definition.StableId,
            validation,
            definition.ActionPointCost,
            hitChance,
            criticalChance,
            normal.AppliedDamage,
            critical.AppliedDamage,
            definition.DamageType,
            target.Runtime.State.CurrentSquadHP,
            target.Runtime.Stats.MaxHP,
            CountLivingWarriors(target.Runtime),
            weapon?.DefinitionId);
    }

    public bool TryExecuteAttack(
        SquadBattleController attacker,
        SquadBattleController target,
        out BattleAttackResult result,
        AttackDefinition definition = null,
        BattleCommandAuthority authority = BattleCommandAuthority.HumanInput)
    {
        definition ??= basicAttack;
        result = CreateResult(attacker, target, definition);
        result.WeaponDefinitionId =
            attacker?.Runtime?.Equipment?.GetWeaponForAttack(definition)?.DefinitionId ?? string.Empty;
        BattleAttackValidationResult validation = ValidateCommand(
            attacker,
            target,
            definition,
            authority);
        if (!validation.IsValid)
        {
            result.FailureReason = validation.FailureReason;
            result.FailureMessage = validation.Reason;
            return false;
        }

        IsExecuting = true;
        bool damageCommitted = false;
        try
        {
            if (!attacker.Runtime.TrySpendActionPoints(definition.ActionPointCost))
            {
                result.FailureReason = BattleAttackFailureReason.InsufficientActionPoints;
                result.FailureMessage = "Action points changed before the attack could commit.";
                return false;
            }

            result.WasExecuted = true;
            result.ActionPointsSpent = definition.ActionPointCost;
            Raise(OnAttackStarted, result);

            float hitChance = calculator.CalculateHitChance(
                attacker.Runtime.Stats,
                target.Runtime.Stats);
            result.Hit = randomSource.Next01() < hitChance;
            if (!result.Hit)
            {
                Raise(OnAttackMissed, result);
                Log(result, "missed");
                return true;
            }

            float criticalChance = calculator.CalculateCriticalChance(
                attacker.Runtime.Stats,
                definition);
            result.Critical = criticalChance > 0f && randomSource.Next01() < criticalChance;
            BattleDamageCalculation damage = calculator.CalculateDamage(
                attacker.Runtime.Stats,
                target.Runtime.Stats,
                definition,
                result.Critical,
                attacker.Runtime.Equipment.GetWeaponForAttack(definition));
            result.RawDamage = damage.RawDamage;
            result.MitigatedDamage = damage.MitigatedDamage;

            SquadDamageResult applied = target.ReceiveFinalDamage(
                damage.AppliedDamage,
                definition.Distribution);
            damageCommitted = true;
            result.AppliedDamage = applied.AppliedDamage;
            result.AddDefeatedWarriors(applied.DefeatedWarriorIds);
            result.CommanderDamaged = applied.CommanderDamage > 0;
            result.CommanderDefeated = applied.CommanderDefeated;
            result.SquadDefeated = applied.SquadDefeated;

            Raise(OnAttackHit, result);
            if (result.Critical)
                Raise(OnCriticalHit, result);
            Log(result, result.Critical ? "critically hit" : "hit");
            return true;
        }
        catch (Exception exception)
        {
            if (result.ActionPointsSpent > 0 && !damageCommitted)
            {
                attacker.Runtime.RestoreActionPointsAfterFailedCommit(
                    result.ActionPointsSpent);
                result.ActionPointsSpent = 0;
                result.WasExecuted = false;
            }
            result.FailureReason = BattleAttackFailureReason.RuntimeFailure;
            result.FailureMessage = exception.Message;
            Debug.LogException(exception, this);
            return false;
        }
        finally
        {
            IsExecuting = false;
            if (result.WasExecuted && result.FailureReason == BattleAttackFailureReason.None)
                Raise(OnAttackResolved, result);
        }
    }

    private BattleAttackValidationResult ValidateAttacker(
        SquadBattleController attacker,
        AttackDefinition definition,
        bool requireSelected,
        BattleCommandAuthority authority)
    {
        if (!IsInitialized || targetingService == null || calculator == null)
        {
            return BattleAttackValidationResult.Reject(
                BattleAttackFailureReason.ServiceNotInitialized,
                "Attack service is not initialized.");
        }
        if (!CommandsEnabled || turnController.IsBattleLocked)
        {
            return BattleAttackValidationResult.Reject(
                BattleAttackFailureReason.BattleCompleted,
                "Battle commands are locked.");
        }
        if (IsExecuting)
        {
            return BattleAttackValidationResult.Reject(
                BattleAttackFailureReason.AttackInProgress,
                "An attack is already resolving.");
        }
        if (definition == null)
        {
            return BattleAttackValidationResult.Reject(
                BattleAttackFailureReason.MissingDefinition,
                "Attack definition is missing.");
        }
        if (!definition.Validate(out string definitionReason))
        {
            return BattleAttackValidationResult.Reject(
                BattleAttackFailureReason.InvalidDefinition,
                definitionReason);
        }
        if (!turnController.HasStarted)
        {
            return BattleAttackValidationResult.Reject(
                BattleAttackFailureReason.BattleNotStarted,
                "Battle has not started.");
        }
        if (attacker == null || !attacker.IsInitialized)
        {
            return BattleAttackValidationResult.Reject(
                BattleAttackFailureReason.InvalidAttacker,
                "Attacker is unavailable.");
        }
        if (!attacker.CanAct)
        {
            return BattleAttackValidationResult.Reject(
                BattleAttackFailureReason.AttackerDefeated,
                "Defeated squads cannot attack.");
        }
        if (!turnController.IsActive(attacker))
        {
            return BattleAttackValidationResult.Reject(
                BattleAttackFailureReason.AttackerNotActive,
                "Selected squad is not active.");
        }
        if (authority == BattleCommandAuthority.HumanInput &&
            attacker.Side != BattleSide.Player)
        {
            return BattleAttackValidationResult.Reject(
                BattleAttackFailureReason.AttackerNotPlayerSide,
                "Only the Player-side squad accepts Human attack commands.");
        }
        if (authority == BattleCommandAuthority.HumanInput &&
            attacker.ControlType != SquadControlType.Human)
        {
            return BattleAttackValidationResult.Reject(
                BattleAttackFailureReason.AttackerNotHumanControlled,
                "AI-controlled squads do not accept Human attack commands.");
        }
        if (authority == BattleCommandAuthority.TacticalAI &&
            attacker.ControlType != SquadControlType.AI)
        {
            return BattleAttackValidationResult.Reject(
                BattleAttackFailureReason.AttackerNotHumanControlled,
                "Only an AI-controlled squad accepts tactical AI attack commands.");
        }
        if (authority == BattleCommandAuthority.TacticalAI &&
            attacker.Side != BattleSide.Enemy)
        {
            return BattleAttackValidationResult.Reject(
                BattleAttackFailureReason.AttackerNotPlayerSide,
                "Enemy Tactical AI only controls Enemy-side squads in AI v0.");
        }
        if (authority == BattleCommandAuthority.HumanInput && requireSelected &&
            selectionController.SelectedSquad != attacker)
        {
            return BattleAttackValidationResult.Reject(
                BattleAttackFailureReason.AttackerNotSelected,
                "Select the active squad before attacking.");
        }
        if ((movementInProgress?.Invoke() ?? movementService.IsMoving))
        {
            return BattleAttackValidationResult.Reject(
                BattleAttackFailureReason.MovementInProgress,
                "Attack is unavailable while movement is in progress.");
        }
        if (attacker.Runtime.State.currentActionPoints < definition.ActionPointCost)
        {
            return BattleAttackValidationResult.Reject(
                BattleAttackFailureReason.InsufficientActionPoints,
                $"Attack needs {definition.ActionPointCost} AP; " +
                $"only {attacker.Runtime.State.currentActionPoints} remain.");
        }
        return BattleAttackValidationResult.Accepted;
    }

    private static BattleAttackResult CreateResult(
        SquadBattleController attacker,
        SquadBattleController target,
        AttackDefinition definition)
    {
        return new BattleAttackResult
        {
            AttackerId = attacker?.SquadId ?? string.Empty,
            TargetId = target?.SquadId ?? string.Empty,
            AttackId = definition?.StableId ?? string.Empty,
            FailureReason = BattleAttackFailureReason.None
        };
    }

    private static int CountLivingWarriors(SquadBattleRuntime runtime)
    {
        if (runtime?.State?.warriors == null)
            return 0;
        return runtime.State.warriors.Count(
            warrior => warrior != null && !warrior.defeated && warrior.currentHP > 0);
    }

    private static void Raise(
        Action<BattleAttackResult> handler,
        BattleAttackResult result)
    {
        if (handler == null)
            return;
        foreach (Action<BattleAttackResult> subscriber in handler.GetInvocationList())
        {
            try
            {
                subscriber(result);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }

    private void Log(BattleAttackResult result, string verb)
    {
        if (!enableDevelopmentLogs)
            return;
        Debug.Log(
            $"BattleAttackService: {result.AttackerId} {verb} {result.TargetId}; " +
            $"attack={result.AttackId}, AP={result.ActionPointsSpent}, " +
            $"damage={result.AppliedDamage}, critical={result.Critical}.",
            this);
    }
}
