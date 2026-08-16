using System;
using System.Collections;
using UnityEngine;

public sealed class EnemyTacticalAIController : MonoBehaviour
{
    [SerializeField] private SquadBattleBootstrap squadBootstrap;
    [SerializeField] private MapGenerator mapGenerator;
    [SerializeField] private BattleTurnController turnController;
    [SerializeField] private GridOccupancyService occupancy;
    [SerializeField] private SquadMovementService movementService;
    [SerializeField] private BattleAttackService attackService;
    [SerializeField] private BattleAbilityService abilityService;
    [SerializeField] private BattleCompletionController completionController;
    [SerializeField] private TacticalCameraController tacticalCamera;
    [SerializeField, Range(1, 16)] private int maximumActionsPerTurn = 8;
    [SerializeField] private bool enableDevelopmentLogs = true;

    private EnemyTacticalDecisionService decisionService;
    private Coroutine turnRoutine;
    private bool listenersBound;
    private bool cancellationRequested;
    private int concurrentRoutineCount;

    public bool IsInitialized { get; private set; }
    public bool IsExecutingTurn { get; private set; }
    public int BegunTurnCount { get; private set; }
    public int CompletedTurnCount { get; private set; }
    public int DuplicateBeginRejectedCount { get; private set; }
    public int MovementActionCount { get; private set; }
    public int BasicAttackActionCount { get; private set; }
    public int RangedAttackActionCount { get; private set; }
    public int AbilityActionCount { get; private set; }
    public int EndTurnRequestCount { get; private set; }
    public int PeakConcurrentRoutineCount { get; private set; }
    public EnemyTacticalTurnSummary LastTurnSummary { get; private set; }
    public EnemyTacticalDecision LastDecision { get; private set; }
    public EnemyTacticalDecisionService DecisionService => decisionService;

    public event Action<EnemyTacticalDecision> OnDecisionMade;
    public event Action<EnemyTacticalTurnSummary> OnAITurnCompleted;

    public void Configure(
        SquadBattleBootstrap bootstrap,
        MapGenerator generator,
        BattleTurnController turns,
        GridOccupancyService occupancyService,
        SquadMovementService movement,
        BattleAttackService attacks,
        BattleAbilityService abilities,
        BattleCompletionController completion,
        TacticalCameraController cameraController = null,
        int actionLimit = 8,
        bool developmentLogs = true)
    {
        UnbindListeners();
        squadBootstrap = bootstrap;
        mapGenerator = generator;
        turnController = turns;
        occupancy = occupancyService;
        movementService = movement;
        attackService = attacks;
        abilityService = abilities;
        completionController = completion;
        tacticalCamera = cameraController;
        maximumActionsPerTurn = Mathf.Clamp(actionLimit, 1, 16);
        enableDevelopmentLogs = developmentLogs;
        IsInitialized = false;
        decisionService = null;
    }

    public bool Initialize()
    {
        if (IsInitialized || squadBootstrap == null || !squadBootstrap.HasBootstrapped ||
            mapGenerator == null || !mapGenerator.HasGeneratedData ||
            turnController == null || !turnController.HasStarted ||
            occupancy == null || !occupancy.IsInitialized ||
            movementService == null || !movementService.IsInitialized ||
            attackService == null || !attackService.IsInitialized ||
            abilityService == null || !abilityService.IsInitialized ||
            completionController == null ||
            completionController.State != BattleCompletionState.Running)
        {
            return false;
        }

        decisionService = new EnemyTacticalDecisionService(
            squadBootstrap,
            mapGenerator,
            occupancy,
            movementService,
            attackService,
            abilityService,
            completionController);
        IsInitialized = true;
        BindListeners();
        BeginTurn(turnController.ActiveSquad);
        return true;
    }

    private void HandleTurnStarted(SquadBattleController active) => BeginTurn(active);

    public bool BeginTurn(SquadBattleController active)
    {
        if (!IsInitialized || !isActiveAndEnabled || active == null ||
            active.ControlType != SquadControlType.AI ||
            active.Side != BattleSide.Enemy)
        {
            return false;
        }
        if (IsExecutingTurn || turnRoutine != null)
        {
            DuplicateBeginRejectedCount++;
            return false;
        }

        cancellationRequested = false;
        IsExecutingTurn = true;
        BegunTurnCount++;
        concurrentRoutineCount++;
        PeakConcurrentRoutineCount = Mathf.Max(
            PeakConcurrentRoutineCount,
            concurrentRoutineCount);
        turnRoutine = StartCoroutine(ExecuteTurn(active));
        return true;
    }

    private IEnumerator ExecuteTurn(SquadBattleController actor)
    {
        yield return null;

        EnemyTacticalTurnSummary summary = new EnemyTacticalTurnSummary
        {
            squadId = actor?.SquadId ?? string.Empty,
            actionPointsAtStart = actor?.Runtime?.State?.currentActionPoints ?? 0
        };
        LastTurnSummary = summary;
        tacticalCamera?.FocusGrid(actor.GridAnchor.CurrentCell);
        Log($"AI [{summary.squadId}] turn started with {summary.actionPointsAtStart} AP.");

        string endReason = "No valid action.";
        while (summary.actionCount < maximumActionsPerTurn && CanContinue(actor))
        {
            EnemyTacticalDecision decision = decisionService.Decide(actor);
            LastDecision = decision;
            summary.lastDecision = decision.ActionType.ToString();
            summary.selectedTargetId = decision.Target?.SquadId ?? string.Empty;
            summary.selectedDestination = decision.Destination;
            summary.selectedPathCost = decision.PathCost;
            OnDecisionMade?.Invoke(decision);

            if (decision.ActionType == EnemyTacticalActionType.EndTurn ||
                decision.ActionType == EnemyTacticalActionType.None)
            {
                endReason = decision.Reason;
                break;
            }

            bool committed = false;
            int actionPointsBefore = actor.Runtime.State.currentActionPoints;
            switch (decision.ActionType)
            {
                case EnemyTacticalActionType.MoveToAttack:
                    committed = movementService.TryMove(
                        decision.MovementPlan,
                        BattleCommandAuthority.TacticalAI);
                    if (committed)
                    {
                        while (movementService.IsMoving && !cancellationRequested)
                            yield return null;
                        committed = actor.GridAnchor.IsPlaced &&
                                    actor.GridAnchor.CurrentCell == decision.Destination;
                        if (committed)
                        {
                            MovementActionCount++;
                            summary.movementCount++;
                            tacticalCamera?.FocusGrid(decision.Destination);
                        }
                    }
                    break;

                case EnemyTacticalActionType.BasicAttack:
                    committed = attackService.TryExecuteAttack(
                        actor,
                        decision.Target,
                        out BattleAttackResult attackResult,
                        decision.AttackDefinition ?? attackService.BasicAttack,
                        BattleCommandAuthority.TacticalAI) &&
                        attackResult.WasExecuted;
                    if (committed)
                    {
                        BasicAttackActionCount++;
                        summary.basicAttackCount++;
                        if (decision.AttackDefinition?.Delivery ==
                            BattleAttackDelivery.Ranged)
                        {
                            RangedAttackActionCount++;
                            summary.rangedAttackCount++;
                        }
                    }
                    break;

                case EnemyTacticalActionType.PowerStrike:
                case EnemyTacticalActionType.SweepingBlow:
                case EnemyTacticalActionType.Rally:
                    committed = abilityService.TryExecuteAbility(
                        actor,
                        decision.Target,
                        decision.Ability,
                        out BattleAbilityResult abilityResult,
                        BattleCommandAuthority.TacticalAI) &&
                        abilityResult.WasExecuted;
                    if (committed)
                    {
                        AbilityActionCount++;
                        summary.abilityCount++;
                    }
                    break;
            }

            if (!committed)
            {
                endReason = $"{decision.ActionType} failed production validation or commit.";
                break;
            }

            summary.actionCount++;
            Log(
                $"AI [{summary.squadId}] {decision.ActionType}; " +
                $"target={summary.selectedTargetId}; destination={decision.Destination}; " +
                $"pathCost={decision.PathCost}; AP {actionPointsBefore} -> " +
                $"{actor.Runtime.State.currentActionPoints}.");
            yield return null;
        }

        if (summary.actionCount >= maximumActionsPerTurn && CanContinue(actor))
            endReason = $"Safety action limit ({maximumActionsPerTurn}) reached.";
        FinishTurn(actor, summary, endReason);
    }

    private bool CanContinue(SquadBattleController actor)
    {
        return !cancellationRequested && IsInitialized &&
               completionController.State == BattleCompletionState.Running &&
               !turnController.IsBattleLocked &&
               turnController.IsActive(actor) && actor != null && actor.CanAct &&
               actor.ControlType == SquadControlType.AI &&
               actor.Side == BattleSide.Enemy;
    }

    private void FinishTurn(
        SquadBattleController actor,
        EnemyTacticalTurnSummary summary,
        string endReason)
    {
        summary.endReason = endReason ?? string.Empty;
        summary.actionPointsAtEnd = actor?.Runtime?.State?.currentActionPoints ?? 0;
        LastTurnSummary = summary;
        IsExecutingTurn = false;
        turnRoutine = null;
        concurrentRoutineCount = Mathf.Max(0, concurrentRoutineCount - 1);
        CompletedTurnCount++;
        OnAITurnCompleted?.Invoke(summary);
        Log($"AI [{summary.squadId}] turn ended: {summary.endReason}");

        if (!cancellationRequested && completionController.State == BattleCompletionState.Running &&
            !turnController.IsBattleLocked && turnController.IsActive(actor))
        {
            EndTurnRequestCount++;
            turnController.EndCurrentTurn();
        }
    }

    private void HandleBattleStopped()
    {
        cancellationRequested = true;
    }

    private void BindListeners()
    {
        if (listenersBound || turnController == null)
            return;
        turnController.OnTurnStarted += HandleTurnStarted;
        turnController.OnBattleStopped += HandleBattleStopped;
        listenersBound = true;
    }

    private void UnbindListeners()
    {
        if (!listenersBound || turnController == null)
            return;
        turnController.OnTurnStarted -= HandleTurnStarted;
        turnController.OnBattleStopped -= HandleBattleStopped;
        listenersBound = false;
    }

    private void Log(string message)
    {
        if (enableDevelopmentLogs)
            Debug.Log(message, this);
    }

    private void OnEnable()
    {
        if (IsInitialized)
        {
            BindListeners();
            BeginTurn(turnController.ActiveSquad);
        }
    }

    private void OnDisable()
    {
        cancellationRequested = true;
        UnbindListeners();
        if (turnRoutine != null)
            StopCoroutine(turnRoutine);
        turnRoutine = null;
        IsExecutingTurn = false;
        concurrentRoutineCount = 0;
    }

    private void OnDestroy() => UnbindListeners();
}
