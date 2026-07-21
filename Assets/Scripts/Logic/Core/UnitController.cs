using System.Collections.Generic;
using UnityEngine;

public abstract class UnitController : MonoBehaviour
{
    [Header("References")]
    public MapGenerator mapGenerator;
    public MapRenderer mapRenderer;

    [Header("Movement")]
    public float movementCooldown = 0.1f;
    [Range(0.02f, 0.5f)] public float movementStepDuration = 0.12f;
    public float worldDepthOffset = 0.05f;

    public Vector2Int CurrentCell => currentCell;
    public Vector2Int LastCell => lastCell;

    protected bool hasPlaced;
    protected bool isMoving;
    protected float nextMoveTime;
    protected readonly Queue<Vector2Int> queuedPathSteps = new Queue<Vector2Int>();

    protected bool IsMovementBusy => isMoving || queuedPathSteps.Count > 0 || Time.time < nextMoveTime;
    protected int QueuedStepCount => queuedPathSteps.Count;

    protected static readonly Vector2Int[] CardinalDirections =
    {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1)
    };

    private Vector2Int currentCell;
    private Vector2Int lastCell;
    private Vector2Int moveTargetCell;
    private float moveStartTime;
    private Vector3 moveStartPosition;
    private Vector3 moveTargetPosition;

    public virtual void SetMapReferences(MapGenerator generator, MapRenderer renderer)
    {
        mapGenerator = generator;
        mapRenderer = renderer;
        hasPlaced = false;
        isMoving = false;
        nextMoveTime = 0f;
        queuedPathSteps.Clear();
    }

    public virtual void ForceSpawnAtCell(Vector2Int cell)
    {
        if (mapGenerator == null || mapRenderer == null || !mapRenderer.HasMap)
            return;

        currentCell = cell;
        lastCell = cell;
        hasPlaced = true;
        isMoving = false;
        queuedPathSteps.Clear();
        transform.position = GetCellWorldPosition(cell);
        moveTargetPosition = transform.position;
    }

    protected bool TryPlaceOnStartCell(Vector2Int startCell)
    {
        if (hasPlaced)
            return true;

        if (mapGenerator == null || mapRenderer == null || !mapRenderer.HasMap)
            return false;

        currentCell = startCell;
        lastCell = currentCell;
        transform.position = GetCellWorldPosition(currentCell);
        moveTargetPosition = transform.position;
        hasPlaced = true;
        return true;
    }

    protected void TickMovement()
    {
        UpdateActiveMovement();

        if (!isMoving && queuedPathSteps.Count > 0 && Time.time >= nextMoveTime)
        {
            Vector2Int nextStep = queuedPathSteps.Dequeue();
            BeginMoveToCell(nextStep);
            OnQueuedStepStarted();
        }
    }

    protected void ClearQueuedSteps()
    {
        queuedPathSteps.Clear();
    }

    protected void QueuePathSteps(IReadOnlyList<Vector2Int> path, int stepsToQueue = -1)
    {
        queuedPathSteps.Clear();

        if (path == null || path.Count <= 1)
            return;

        int maxSteps = path.Count - 1;
        if (stepsToQueue >= 0)
            maxSteps = Mathf.Min(maxSteps, stepsToQueue);

        for (int i = 1; i <= maxSteps; i++)
            queuedPathSteps.Enqueue(path[i]);
    }

    protected bool TryMoveSingleStep(Vector2Int direction)
    {
        if (mapGenerator == null)
            return false;

        Vector2Int nextCell = currentCell + direction;
        if (!mapGenerator.GetIsPlayable(nextCell.x, nextCell.y))
            return false;

        BeginMoveToCell(nextCell);
        return true;
    }

    protected bool TryBuildPath(Vector2Int start, Vector2Int target, out List<Vector2Int> path)
    {
        path = new List<Vector2Int>();

        if (mapGenerator == null)
            return false;

        if (start == target)
        {
            path.Add(start);
            return true;
        }

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

        queue.Enqueue(start);
        visited.Add(start);

        bool found = false;
        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            if (current == target)
            {
                found = true;
                break;
            }

            foreach (var dir in CardinalDirections)
            {
                Vector2Int next = current + dir;
                if (visited.Contains(next))
                    continue;

                if (!mapGenerator.GetIsPlayable(next.x, next.y))
                    continue;

                visited.Add(next);
                cameFrom[next] = current;
                queue.Enqueue(next);
            }
        }

        if (!found)
            return false;

        Vector2Int step = target;
        path.Add(step);
        while (step != start)
        {
            step = cameFrom[step];
            path.Add(step);
        }

        path.Reverse();
        return true;
    }

    protected virtual Vector3 GetCellWorldPosition(Vector2Int cell)
    {
        return mapRenderer.GetCellWorldCenter(cell) + Vector3.up * worldDepthOffset;
    }

    protected virtual void OnQueuedStepStarted()
    {
    }

    protected virtual void OnCellReached(Vector2Int reachedCell)
    {
    }

    private void BeginMoveToCell(Vector2Int cell)
    {
        isMoving = true;
        moveTargetCell = cell;
        moveStartTime = Time.time;
        moveStartPosition = transform.position;
        moveTargetPosition = GetCellWorldPosition(cell);
        nextMoveTime = Time.time + movementCooldown;
    }

    private void UpdateActiveMovement()
    {
        if (!isMoving)
            return;

        float duration = Mathf.Max(0.01f, movementStepDuration);
        float t = Mathf.Clamp01((Time.time - moveStartTime) / duration);
        float easedT = Mathf.SmoothStep(0f, 1f, t);
        transform.position = Vector3.Lerp(moveStartPosition, moveTargetPosition, easedT);

        if (t < 1f)
            return;

        transform.position = moveTargetPosition;
        lastCell = currentCell;
        currentCell = moveTargetCell;
        isMoving = false;
        OnCellReached(currentCell);
    }
}