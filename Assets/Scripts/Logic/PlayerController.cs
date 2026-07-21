using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : UnitController
{
    public EnemyController enemyController;
    public Camera inputCamera;

    [Header("Turn")]
    public bool useTurnSystem = true;
    [Min(1)] public int maxStepsPerTurn = 6;

    [Header("Alignment")]
    public bool autoCenterVisualOnCell = true;
    public Vector2 manualCellOffsetXZ = Vector2.zero;

    [Header("Path Preview")]
    public float pathLineHeight = 0.08f;
    public float pathLineWidth = 0.08f;
    public Color pathLineColor = Color.cyan;
    public Color pathOutOfRangeColor = Color.red;

    public event System.Action<Vector2Int> OnPlayerMoved;
    public event System.Action OnTurnMoveCompleted;

    private bool hasPreviewTarget;
    private bool previewMoveQueued;
    private bool previewOutOfRange;
    private bool isPlayerTurn = true;
    private Vector2Int previewTargetCell;
    private readonly List<Vector2Int> currentPath = new List<Vector2Int>();
    private LineRenderer pathLineRenderer;
    private Vector3 visualCenterOffsetXZ;

    private void Awake()
    {
        EnsurePathLineRenderer();
        CacheVisualCenterOffset();
    }

    private void Update()
    {
        if (!TryPlaceOnOwnStartCell())
            return;

        TickMovement();

        if (IsMovementBusy)
            return;

        if (useTurnSystem && !isPlayerTurn)
            return;

        TryHandleMousePathRequest();

        if (useTurnSystem)
            return;

        Vector2Int direction = Vector2Int.zero;

        if (Keyboard.current != null && Keyboard.current.wKey.wasPressedThisFrame)
            direction = Vector2Int.up;
        else if (Keyboard.current != null && Keyboard.current.sKey.wasPressedThisFrame)
            direction = Vector2Int.down;
        else if (Keyboard.current != null && Keyboard.current.aKey.wasPressedThisFrame)
            direction = Vector2Int.left;
        else if (Keyboard.current != null && Keyboard.current.dKey.wasPressedThisFrame)
            direction = Vector2Int.right;

        if (direction != Vector2Int.zero)
        {
            ClearPreviewPath();
            TryMoveDirection(direction);
        }
    }

    public override void SetMapReferences(MapGenerator generator, MapRenderer renderer)
    {
        base.SetMapReferences(generator, renderer);
        hasPreviewTarget = false;
        previewMoveQueued = false;
        previewOutOfRange = false;
        CacheVisualCenterOffset();
        currentPath.Clear();
        UpdatePathLine();
    }

    public override void ForceSpawnAtCell(Vector2Int cell)
    {
        base.ForceSpawnAtCell(cell);
        hasPreviewTarget = false;
        previewMoveQueued = false;
        previewOutOfRange = false;
        currentPath.Clear();
        UpdatePathLine();
    }

    public void SetPlayerTurn(bool active)
    {
        isPlayerTurn = active;

        if (!active)
            ClearPreviewPath();
    }

    private bool TryPlaceOnOwnStartCell()
    {
        if (hasPlaced)
            return true;

        if (mapGenerator == null)
            return false;

        bool placed = TryPlaceOnStartCell(mapGenerator.GetStartCell());
        if (placed)
            UpdatePathLine();

        return placed;
    }

    private void TryHandleMousePathRequest()
    {
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            return;

        Camera cam = inputCamera != null ? inputCamera : Camera.main;
        if (cam == null)
            return;

        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, 10000f))
            return;

        if (mapRenderer == null || !mapRenderer.TryGetClosestPlayableCell(hit.point, out Vector2Int targetCell))
            return;

        if (!TryBuildPath(CurrentCell, targetCell, out List<Vector2Int> path))
            return;

        if (enemyController != null && targetCell == enemyController.CurrentCell && path.Count > 1)
        {
            // Stop one cell before enemy so encounter starts from adjacent cells.
            path.RemoveAt(path.Count - 1);
            targetCell = path[path.Count - 1];
        }

        if (useTurnSystem)
        {
            HandleTurnBasedPathClick(targetCell, path);
            return;
        }

        StartMoveSequence(path);
    }

    private void HandleTurnBasedPathClick(Vector2Int targetCell, List<Vector2Int> path)
    {
        int steps = Mathf.Max(0, path.Count - 1);
        bool clickedSameTarget = hasPreviewTarget && targetCell == previewTargetCell;

        hasPreviewTarget = true;
        previewTargetCell = targetCell;
        previewOutOfRange = steps > maxStepsPerTurn;
        currentPath.Clear();
        currentPath.AddRange(path);
        UpdatePathLine();

        if (!clickedSameTarget)
            return;

        if (steps <= 0 || steps > maxStepsPerTurn)
            return;

        StartMoveSequence(path);
    }

    private void StartMoveSequence(List<Vector2Int> path)
    {
        currentPath.Clear();
        currentPath.AddRange(path);

        QueuePathSteps(path);
        previewMoveQueued = QueuedStepCount > 0;
        previewOutOfRange = false;
        hasPreviewTarget = false;
        UpdatePathLine();
    }

    private void TryMoveDirection(Vector2Int direction)
    {
        Vector2Int nextCell = CurrentCell + direction;
        if (enemyController != null && nextCell == enemyController.CurrentCell)
            return;

        TryMoveSingleStep(direction);
    }

    protected override Vector3 GetCellWorldPosition(Vector2Int cell)
    {
        Vector3 pos = base.GetCellWorldPosition(cell);
        pos += new Vector3(manualCellOffsetXZ.x, 0f, manualCellOffsetXZ.y);

        if (autoCenterVisualOnCell)
            pos -= visualCenterOffsetXZ;

        return pos;
    }

    protected override void OnQueuedStepStarted()
    {
        UpdatePathLine();
    }

    protected override void OnCellReached(Vector2Int reachedCell)
    {
        OnPlayerMoved?.Invoke(reachedCell);

        if (!previewMoveQueued || QueuedStepCount > 0)
            return;

        previewMoveQueued = false;
        if (useTurnSystem)
        {
            ClearPreviewPath();
            OnTurnMoveCompleted?.Invoke();
        }
    }

    private void ClearPreviewPath()
    {
        hasPreviewTarget = false;
        previewMoveQueued = false;
        previewOutOfRange = false;
        currentPath.Clear();
        ClearQueuedSteps();
        UpdatePathLine();
    }

    private void CacheVisualCenterOffset()
    {
        visualCenterOffsetXZ = Vector3.zero;

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null || renderers[i] is LineRenderer)
                continue;

            Vector3 delta = renderers[i].bounds.center - transform.position;
            visualCenterOffsetXZ = new Vector3(delta.x, 0f, delta.z);
            return;
        }
    }

    private void EnsurePathLineRenderer()
    {
        if (pathLineRenderer != null)
            return;

        pathLineRenderer = GetComponent<LineRenderer>();
        if (pathLineRenderer == null)
            pathLineRenderer = gameObject.AddComponent<LineRenderer>();

        pathLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        pathLineRenderer.startWidth = pathLineWidth;
        pathLineRenderer.endWidth = pathLineWidth;
        pathLineRenderer.useWorldSpace = true;
        pathLineRenderer.startColor = pathLineColor;
        pathLineRenderer.endColor = pathLineColor;
        pathLineRenderer.positionCount = 0;
    }

    private void UpdatePathLine()
    {
        EnsurePathLineRenderer();

        if (currentPath.Count <= 1 || mapRenderer == null)
        {
            pathLineRenderer.positionCount = 0;
            return;
        }

        Color previewColor = previewOutOfRange ? pathOutOfRangeColor : pathLineColor;
        pathLineRenderer.startColor = previewColor;
        pathLineRenderer.endColor = previewColor;

        pathLineRenderer.positionCount = currentPath.Count;
        for (int i = 0; i < currentPath.Count; i++)
        {
            Vector3 p = mapRenderer.GetCellWorldCenter(currentPath[i]);
            p.y += pathLineHeight;
            pathLineRenderer.SetPosition(i, p);
        }
    }
}
