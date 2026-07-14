using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyController : MonoBehaviour
{
    [Header("References")]
    public MapGenerator mapGenerator;
    public MapRenderer mapRenderer;
    public PlayerController playerController;

    [Header("Turn")]
    [Min(1)] public int maxStepsPerTurn = 4;

    [Header("Movement")]
    public float movementCooldown = 0.1f;
    [Range(0.02f, 0.5f)] public float movementStepDuration = 0.12f;
    public float worldDepthOffset = 0.05f;

    public Vector2Int CurrentCell => currentCell;

    private Vector2Int currentCell;
    private bool hasPlaced;
    private bool isMoving;
    private float nextMoveTime;
    private float moveStartTime;
    private readonly Queue<Vector2Int> queuedPathSteps = new Queue<Vector2Int>();
    private Vector2Int moveTargetCell;
    private Vector3 moveStartPosition;
    private Vector3 moveTargetPosition;

    private static readonly Vector2Int[] CardinalDirections =
    {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1)
    };

    private void Update()
    {
        if (!TryPlaceOnSpawnCell())
            return;

        UpdateActiveMovement();

        if (!isMoving && queuedPathSteps.Count > 0 && Time.time >= nextMoveTime)
        {
            Vector2Int nextStep = queuedPathSteps.Dequeue();
            BeginMoveToCell(nextStep);
        }
    }

    public void SetMapReferences(MapGenerator generator, MapRenderer renderer, PlayerController player)
    {
        mapGenerator = generator;
        mapRenderer = renderer;
        playerController = player;
        hasPlaced = false;
        isMoving = false;
        queuedPathSteps.Clear();
    }

    public void ExecuteTurn(Action onComplete)
    {
        StartCoroutine(ExecuteTurnRoutine(onComplete));
    }

    private IEnumerator ExecuteTurnRoutine(Action onComplete)
    {
        while (!TryPlaceOnSpawnCell())
            yield return null;

        if (playerController == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        if (!TryBuildPath(currentCell, playerController.CurrentCell, out List<Vector2Int> path))
        {
            onComplete?.Invoke();
            yield break;
        }

        int stepsToTake = Mathf.Min(maxStepsPerTurn, Mathf.Max(0, path.Count - 1));
        if (stepsToTake <= 0)
        {
            onComplete?.Invoke();
            yield break;
        }

        queuedPathSteps.Clear();
        for (int i = 1; i <= stepsToTake; i++)
            queuedPathSteps.Enqueue(path[i]);

        while (queuedPathSteps.Count > 0 || isMoving)
            yield return null;

        onComplete?.Invoke();
    }

    private bool TryPlaceOnSpawnCell()
    {
        if (hasPlaced)
            return true;

        if (mapGenerator == null || mapRenderer == null || !mapRenderer.HasMap)
            return false;

        Vector2Int origin = playerController != null ? playerController.CurrentCell : mapGenerator.GetCentralPlayableCell();
        currentCell = FindFarthestPlayableCell(origin);
        transform.position = mapRenderer.GetCellWorldCenter(currentCell) + Vector3.forward * worldDepthOffset;
        hasPlaced = true;
        return true;
    }

    private Vector2Int FindFarthestPlayableCell(Vector2Int origin)
    {
        Vector2Int best = mapGenerator.GetCentralPlayableCell();
        int bestDist = -1;

        for (int x = 0; x < mapGenerator.width; x++)
        {
            for (int y = 0; y < mapGenerator.height; y++)
            {
                if (!mapGenerator.GetIsPlayable(x, y))
                    continue;

                int d = Mathf.Abs(x - origin.x) + Mathf.Abs(y - origin.y);
                if (d > bestDist)
                {
                    bestDist = d;
                    best = new Vector2Int(x, y);
                }
            }
        }

        return best;
    }

    private bool TryBuildPath(Vector2Int start, Vector2Int target, out List<Vector2Int> path)
    {
        path = new List<Vector2Int>();

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

    private void BeginMoveToCell(Vector2Int cell)
    {
        isMoving = true;
        moveTargetCell = cell;
        moveStartTime = Time.time;
        moveStartPosition = transform.position;
        moveTargetPosition = mapRenderer.GetCellWorldCenter(cell) + Vector3.forward * worldDepthOffset;
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
        currentCell = moveTargetCell;
        isMoving = false;
    }
}
