using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class SquadMovementService : MonoBehaviour
{
    [SerializeField] private MapGenerator mapGenerator;
    [SerializeField] private MapRenderer mapRenderer;
    [SerializeField] private GridOccupancyService occupancy;
    [SerializeField] private BattleTurnController turnController;
    [SerializeField] private GridTacticalTerrainService tacticalTerrain;
    [SerializeField] private bool allowDiagonalMovement = true;
    [SerializeField, Range(0.02f, 0.5f)] private float movementStepDuration = 0.12f;

    private Coroutine movementRoutine;
    private Vector2Int movementSourceCell;
    private Vector3 movementSourcePosition;
    private Vector2Int movementDestination;

    public bool IsInitialized { get; private set; }
    public bool CommandsEnabled { get; private set; } = true;
    public bool IsMoving => movementRoutine != null;
    public SquadBattleController MovingSquad { get; private set; }
    public float MovementStepDuration => movementStepDuration;
    public bool AllowDiagonalMovement => allowDiagonalMovement;
    public GridTacticalTerrainService TacticalTerrain => tacticalTerrain;

    public event Action<SquadMovementPlan> OnMovementStarted;
    public event Action<SquadMovementPlan> OnMovementCompleted;
    public event Action<string> OnMovementFailed;

    public void Configure(
        MapGenerator generator,
        MapRenderer renderer,
        GridOccupancyService occupancyService,
        BattleTurnController turns,
        bool allowDiagonal,
        float stepDuration)
    {
        Configure(generator, renderer, occupancyService, turns, tacticalTerrain,
            allowDiagonal, stepDuration);
    }

    public void Configure(
        MapGenerator generator,
        MapRenderer renderer,
        GridOccupancyService occupancyService,
        BattleTurnController turns,
        GridTacticalTerrainService configuredTacticalTerrain,
        bool allowDiagonal,
        float stepDuration)
    {
        mapGenerator = generator;
        mapRenderer = renderer;
        occupancy = occupancyService;
        turnController = turns;
        tacticalTerrain = configuredTacticalTerrain;
        allowDiagonalMovement = allowDiagonal;
        movementStepDuration = Mathf.Clamp(stepDuration, 0.02f, 0.5f);
    }

    public bool Initialize()
    {
        IsInitialized = mapGenerator != null && mapRenderer != null &&
                        occupancy != null && occupancy.IsInitialized &&
                         turnController != null &&
                         (tacticalTerrain == null || tacticalTerrain.Initialize());
        CommandsEnabled = IsInitialized;
        return IsInitialized;
    }

    public void SetCommandsEnabled(bool enabled)
    {
        CommandsEnabled = enabled;
        if (!enabled && IsMoving)
            CancelActiveMovement();
    }

    public bool TryBuildPlan(
        SquadBattleController controller,
        Vector2Int destination,
        out SquadMovementPlan plan,
        BattleCommandAuthority authority = BattleCommandAuthority.HumanInput)
    {
        string reason = ValidateCommandSource(controller, authority);
        if (reason != null)
        {
            plan = new SquadMovementPlan(
                controller,
                destination,
                null,
                0,
                false,
                reason);
            return false;
        }

        Vector2Int start = controller.GridAnchor.CurrentCell;
        List<Vector2Int> path;
        bool hasPath = GridPathfinder.TryBuildPath(
            mapGenerator,
            start,
            destination,
            allowDiagonalMovement,
            cell => CanEnterCell(controller, cell),
            out path);
        if (!hasPath)
        {
            plan = new SquadMovementPlan(
                controller,
                destination,
                null,
                0,
                false,
                "No playable route to this cell.");
            return false;
        }

        int cost = Mathf.Max(0, path.Count - 1);
        if (cost == 0)
            reason = "Choose a different destination cell.";
        else if (tacticalTerrain != null && tacticalTerrain.BlocksMovement(destination))
            reason = "Destination cell contains a solid tactical obstacle.";
        else if (!CanEnterCell(controller, destination))
            reason = "Destination cell is occupied or reserved.";
        else if (cost > controller.Runtime.State.currentActionPoints)
            reason = $"Movement needs {cost} AP; only {controller.Runtime.State.currentActionPoints} remain.";

        plan = new SquadMovementPlan(
            controller,
            destination,
            path,
            cost,
            reason == null,
            reason);
        return plan.IsValid;
    }

    public bool TryMove(
        SquadMovementPlan requestedPlan,
        BattleCommandAuthority authority = BattleCommandAuthority.HumanInput)
    {
        if (requestedPlan == null || !requestedPlan.IsValid || IsMoving ||
            !TryBuildPlan(
                requestedPlan.Squad,
                requestedPlan.Destination,
                out SquadMovementPlan currentPlan,
                authority) ||
            !occupancy.TryReserve(currentPlan.Squad, currentPlan.Destination))
        {
            OnMovementFailed?.Invoke(
                requestedPlan?.UnavailableReason ?? "Movement command is no longer valid.");
            return false;
        }

        MovingSquad = currentPlan.Squad;
        movementSourceCell = MovingSquad.GridAnchor.CurrentCell;
        movementSourcePosition = MovingSquad.transform.position;
        movementDestination = currentPlan.Destination;
        movementRoutine = StartCoroutine(ExecuteMovement(currentPlan));
        OnMovementStarted?.Invoke(currentPlan);
        return true;
    }

    public bool CanEnterCell(SquadBattleController controller, Vector2Int cell)
    {
        return occupancy != null && occupancy.CanEnter(controller, cell) &&
               (tacticalTerrain == null || !tacticalTerrain.BlocksMovement(cell));
    }

    public bool CancelActiveMovement()
    {
        if (!IsMoving || MovingSquad == null)
            return false;

        SquadBattleController cancelled = MovingSquad;
        StopCoroutine(movementRoutine);
        movementRoutine = null;
        cancelled.transform.position = movementSourcePosition;
        occupancy.CancelReservation(cancelled);
        MovingSquad = null;
        OnMovementFailed?.Invoke("Movement was cancelled before logical commit.");
        return true;
    }

    private IEnumerator ExecuteMovement(SquadMovementPlan plan)
    {
        bool committed = false;
        try
        {
            for (int i = 1; i < plan.Path.Count; i++)
            {
                if (!plan.Squad.CanAct)
                    yield break;

                Vector3 from = plan.Squad.transform.position;
                Vector3 to = plan.Squad.GridAnchor.GetWorldPosition(plan.Path[i]);
                float elapsed = 0f;
                while (elapsed < movementStepDuration)
                {
                    if (!plan.Squad.CanAct)
                        yield break;
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / movementStepDuration);
                    plan.Squad.transform.position =
                        Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
                    yield return null;
                }
                plan.Squad.transform.position = to;
            }

            if (!occupancy.CanCommitMove(plan.Squad, plan.Destination) ||
                !plan.Squad.GridAnchor.CanCommitCell(plan.Destination) ||
                !plan.Squad.Runtime.TrySpendActionPoints(plan.ActionPointCost) ||
                !occupancy.TryCommitMove(plan.Squad, plan.Destination) ||
                !plan.Squad.GridAnchor.CommitVisualArrival(plan.Destination))
            {
                OnMovementFailed?.Invoke("Movement could not commit its AP, occupancy, and logical cell atomically.");
                yield break;
            }

            committed = true;
        }
        finally
        {
            if (!committed && plan.Squad != null)
            {
                plan.Squad.transform.position = movementSourcePosition;
                occupancy.CancelReservation(plan.Squad);
            }
            movementRoutine = null;
            MovingSquad = null;
        }

        if (committed)
            OnMovementCompleted?.Invoke(plan);
    }

    private string ValidateCommandSource(
        SquadBattleController controller,
        BattleCommandAuthority authority)
    {
        if (!IsInitialized)
            return "Movement service is not initialized.";
        if (!CommandsEnabled || turnController.IsBattleLocked)
            return "Battle commands are locked.";
        if (IsMoving)
            return "A squad is already moving.";
        if (controller == null || !controller.IsInitialized || controller.GridAnchor == null ||
            !controller.GridAnchor.IsPlaced)
        {
            return "Squad has no valid battle-grid representation.";
        }
        if (!controller.CanAct)
            return "Defeated squads cannot move.";
        if (authority == BattleCommandAuthority.HumanInput &&
            controller.ControlType != SquadControlType.Human)
        {
            return "Only a Human-controlled squad accepts Human movement commands.";
        }
        if (authority == BattleCommandAuthority.TacticalAI &&
            controller.ControlType != SquadControlType.AI)
        {
            return "Only an AI-controlled squad accepts tactical AI movement commands.";
        }
        if (!turnController.IsActive(controller))
            return "Selected squad is not the active squad.";
        if (controller.Runtime.State.currentActionPoints <= 0)
            return "No action points remain.";
        return null;
    }

    private void OnDisable()
    {
        if (IsMoving)
            CancelActiveMovement();
    }
}
