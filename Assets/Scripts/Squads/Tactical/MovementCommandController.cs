using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public sealed class MovementCommandController : MonoBehaviour
{
    [Header("Battle services")]
    [SerializeField] private BattleSquadSelectionController selectionController;
    [SerializeField] private BattleTurnController turnController;
    [SerializeField] private SquadMovementService movementService;
    [SerializeField] private BattleCommandModeController commandMode;
    [SerializeField] private SquadPathPreviewView pathPreview;
    [SerializeField] private MapGenerator mapGenerator;
    [SerializeField] private MapRenderer mapRenderer;
    [SerializeField] private Camera inputCamera;

    [Header("HUD commands")]
    [SerializeField] private BattleActionControlView moveAction;
    [SerializeField] private BattleActionControlView endTurnAction;

    private bool listenersBound;
    private Vector2Int lastPreviewCell;
    private bool hasPreviewCell;
    private SquadBattleRuntime subscribedActionPointRuntime;
    private bool battleCommandsEnabled = true;

    public bool IsInitialized { get; private set; }
    public bool IsMovementTargeting =>
        commandMode != null && commandMode.ActiveMode == BattleCommandMode.Move;
    public int MovementCommandCount { get; private set; }
    public int EndTurnCommandCount { get; private set; }

    public void Configure(
        BattleSquadSelectionController selection,
        BattleTurnController turns,
        SquadMovementService movement,
        BattleCommandModeController modes,
        SquadPathPreviewView preview,
        MapGenerator generator,
        MapRenderer renderer,
        Camera camera,
        BattleActionControlView move,
        BattleActionControlView endTurn)
    {
        UnbindListeners();
        selectionController = selection;
        turnController = turns;
        movementService = movement;
        commandMode = modes;
        pathPreview = preview;
        mapGenerator = generator;
        mapRenderer = renderer;
        inputCamera = camera;
        moveAction = move;
        endTurnAction = endTurn;
        if (isActiveAndEnabled)
            BindListeners();
    }

    public bool Initialize()
    {
        IsInitialized = selectionController != null && selectionController.IsInitialized &&
                        turnController != null && turnController.HasStarted &&
                        movementService != null && movementService.IsInitialized &&
                        commandMode != null &&
                        pathPreview != null && mapGenerator != null && mapRenderer != null &&
                        moveAction != null && endTurnAction != null;
        BindListeners();
        if (subscribedActionPointRuntime == null && turnController?.ActiveSquad?.Runtime != null)
        {
            subscribedActionPointRuntime = turnController.ActiveSquad.Runtime;
            subscribedActionPointRuntime.OnActionPointsChanged += HandleActionPointsChanged;
        }
        RefreshActions();
        return IsInitialized;
    }

    public bool TryBeginMovementTargeting()
    {
        if (!battleCommandsEnabled)
            return false;
        if (!TryGetCommandSquad(out SquadBattleController controller, out string reason))
        {
            moveAction?.SetCommandState(false, false, reason);
            return false;
        }

        if (controller.Runtime.State.currentActionPoints <= 0 || movementService.IsMoving)
            return false;

        commandMode.TryEnter(BattleCommandMode.Move);
        hasPreviewCell = false;
        pathPreview.Clear();
        moveAction.SetCommandState(true, true, "Choose a destination cell");
        return true;
    }

    public void CancelMovementTargeting()
    {
        if (IsMovementTargeting)
            commandMode.Cancel();
        hasPreviewCell = false;
        pathPreview?.Clear();
        RefreshActions();
    }

    public bool TrySubmitTargetCell(Vector2Int targetCell)
    {
        if (!battleCommandsEnabled || !IsInitialized || !IsMovementTargeting ||
            !TryGetCommandSquad(out SquadBattleController controller, out _))
        {
            return false;
        }

        movementService.TryBuildPlan(controller, targetCell, out SquadMovementPlan plan);
        pathPreview.Render(plan, mapRenderer);
        UpdateMovePreviewLabel(plan);
        if (!plan.IsValid || !movementService.TryMove(plan))
            return false;

        MovementCommandCount++;
        commandMode.Cancel();
        hasPreviewCell = false;
        pathPreview.Clear();
        RefreshActions();
        return true;
    }

    public bool TryEndTurn()
    {
        if (!battleCommandsEnabled || !IsInitialized || movementService.IsMoving ||
            turnController.ActiveSquad == null ||
            turnController.ActiveSquad.ControlType != SquadControlType.Human)
        {
            return false;
        }

        commandMode.Cancel();
        if (!turnController.EndCurrentTurn())
            return false;
        EndTurnCommandCount++;
        return true;
    }

    public void SetBattleCommandsEnabled(bool enabled)
    {
        battleCommandsEnabled = enabled;
        if (!enabled)
            CancelMovementTargeting();
        RefreshActions();
    }

    private void OnEnable() => BindListeners();

    private void OnDisable()
    {
        if (IsMovementTargeting)
            commandMode.Cancel();
        hasPreviewCell = false;
        if (pathPreview != null)
            pathPreview.Clear();
        UnbindListeners();
    }

    private void Update()
    {
        if (!battleCommandsEnabled || !IsInitialized || Keyboard.current == null)
            return;

        if (Keyboard.current.mKey.wasPressedThisFrame)
            TryBeginMovementTargeting();
        if (Keyboard.current.escapeKey.wasPressedThisFrame ||
            (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame))
        {
            CancelMovementTargeting();
        }
        if (Keyboard.current.spaceKey.wasPressedThisFrame ||
            Keyboard.current.enterKey.wasPressedThisFrame)
        {
            TryEndTurn();
        }

        if (!IsMovementTargeting || Mouse.current == null ||
            (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
        {
            return;
        }

        if (!TryGetMapCellAtPointer(out Vector2Int targetCell))
        {
            pathPreview.Clear();
            hasPreviewCell = false;
            return;
        }

        if (!hasPreviewCell || targetCell != lastPreviewCell)
        {
            lastPreviewCell = targetCell;
            hasPreviewCell = true;
            if (TryGetCommandSquad(out SquadBattleController controller, out _))
            {
                movementService.TryBuildPlan(controller, targetCell, out SquadMovementPlan plan);
                pathPreview.Render(plan, mapRenderer);
                UpdateMovePreviewLabel(plan);
            }
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
            TrySubmitTargetCell(targetCell);
    }

    private bool TryGetMapCellAtPointer(out Vector2Int cell)
    {
        cell = default;
        Camera camera = inputCamera != null ? inputCamera : Camera.main;
        if (camera == null)
            return false;

        Ray ray = camera.ScreenPointToRay(Mouse.current.position.ReadValue());
        RaycastHit[] hits = Physics.RaycastAll(ray, 10000f, ~0, QueryTriggerInteraction.Collide);
        float nearestDistance = float.MaxValue;
        bool found = false;
        Vector3 worldPoint = default;
        for (int i = 0; i < hits.Length; i++)
        {
            Transform hitTransform = hits[i].collider.transform;
            bool isMap = hitTransform == mapRenderer.transform ||
                         hitTransform.IsChildOf(mapRenderer.transform);
            if (!isMap || hits[i].distance >= nearestDistance)
                continue;
            nearestDistance = hits[i].distance;
            worldPoint = hits[i].point;
            found = true;
        }

        return found && mapRenderer.TryGetClosestPlayableCell(worldPoint, out cell);
    }

    private bool TryGetCommandSquad(
        out SquadBattleController controller,
        out string reason)
    {
        controller = selectionController?.SelectedSquad ?? turnController?.ActiveSquad;
        if (controller == null)
        {
            reason = "Select the active Player squad.";
            return false;
        }
        if (turnController.ActiveSquad != controller)
        {
            reason = "Selected squad is not active.";
            return false;
        }
        if (controller.ControlType != SquadControlType.Human)
        {
            reason = "AI-controlled squads do not accept Human commands.";
            return false;
        }
        if (!controller.CanAct)
        {
            reason = "Defeated squads cannot act.";
            return false;
        }
        reason = null;
        return true;
    }

    private void BindListeners()
    {
        if (listenersBound)
            return;
        if (moveAction != null && moveAction.Button != null)
            moveAction.Button.onClick.AddListener(HandleMoveClicked);
        if (endTurnAction != null && endTurnAction.Button != null)
            endTurnAction.Button.onClick.AddListener(HandleEndTurnClicked);
        if (selectionController != null)
            selectionController.OnSelectedSquadChanged += HandleSelectionChanged;
        if (turnController != null)
            turnController.OnActiveSquadChanged += HandleActiveSquadChanged;
        if (commandMode != null)
            commandMode.OnModeChanged += HandleModeChanged;
        if (movementService != null)
        {
            movementService.OnMovementCompleted += HandleMovementCompleted;
            movementService.OnMovementFailed += HandleMovementFailed;
        }
        listenersBound = true;
    }

    private void UnbindListeners()
    {
        if (!listenersBound)
            return;
        if (moveAction != null && moveAction.Button != null)
            moveAction.Button.onClick.RemoveListener(HandleMoveClicked);
        if (endTurnAction != null && endTurnAction.Button != null)
            endTurnAction.Button.onClick.RemoveListener(HandleEndTurnClicked);
        if (selectionController != null)
            selectionController.OnSelectedSquadChanged -= HandleSelectionChanged;
        if (turnController != null)
            turnController.OnActiveSquadChanged -= HandleActiveSquadChanged;
        if (commandMode != null)
            commandMode.OnModeChanged -= HandleModeChanged;
        if (movementService != null)
        {
            movementService.OnMovementCompleted -= HandleMovementCompleted;
            movementService.OnMovementFailed -= HandleMovementFailed;
        }
        if (subscribedActionPointRuntime != null)
            subscribedActionPointRuntime.OnActionPointsChanged -= HandleActionPointsChanged;
        subscribedActionPointRuntime = null;
        listenersBound = false;
    }

    private void RefreshActions()
    {
        SquadBattleController controller = null;
        string reason = battleCommandsEnabled ? null : "Battle commands are locked.";
        bool canCommand = battleCommandsEnabled &&
                          TryGetCommandSquad(out controller, out reason);
        bool canMove = IsInitialized && canCommand && !movementService.IsMoving &&
                       controller.Runtime.State.currentActionPoints > 0;
        string moveReason = canMove
            ? "Choose Move, then select a reachable cell."
            : reason ?? "No action points remain.";
        moveAction?.RenderCommand("Move", "M", "1 AP / cell", canMove, IsMovementTargeting, moveReason);
        bool canEnd = battleCommandsEnabled && IsInitialized && turnController.ActiveSquad != null &&
                      turnController.ActiveSquad.ControlType == SquadControlType.Human &&
                      !movementService.IsMoving;
        endTurnAction?.RenderCommand(
            "End Turn",
            "Space",
            "AP —",
            canEnd,
            false,
            canEnd ? "Finish the active squad's turn." : "End Turn is unavailable now.");
    }

    private void UpdateMovePreviewLabel(SquadMovementPlan plan)
    {
        if (plan == null)
            return;
        string state = plan.Path.Count > 1
            ? $"{plan.ActionPointCost} AP"
            : plan.UnavailableReason;
        moveAction?.SetCommandState(plan.IsValid, true, state, plan.UnavailableReason);
    }

    private void HandleMoveClicked() => TryBeginMovementTargeting();
    private void HandleEndTurnClicked() => TryEndTurn();
    private void HandleSelectionChanged(SquadBattleController selected)
    {
        if (IsMovementTargeting && selected != turnController.ActiveSquad)
            CancelMovementTargeting();
        RefreshActions();
    }

    private void HandleActiveSquadChanged(SquadBattleController controller)
    {
        CancelMovementTargeting();
        if (subscribedActionPointRuntime != null)
            subscribedActionPointRuntime.OnActionPointsChanged -= HandleActionPointsChanged;
        subscribedActionPointRuntime = null;
        if (controller?.Runtime != null)
        {
            controller.Runtime.OnActionPointsChanged += HandleActionPointsChanged;
            subscribedActionPointRuntime = controller.Runtime;
        }
        RefreshActions();
    }

    private void HandleActionPointsChanged(int _) => RefreshActions();
    private void HandleModeChanged(BattleCommandMode mode)
    {
        if (mode != BattleCommandMode.Move)
        {
            hasPreviewCell = false;
            pathPreview?.Clear();
        }
        RefreshActions();
    }
    private void HandleMovementCompleted(SquadMovementPlan _) => RefreshActions();
    private void HandleMovementFailed(string _) => RefreshActions();
}
