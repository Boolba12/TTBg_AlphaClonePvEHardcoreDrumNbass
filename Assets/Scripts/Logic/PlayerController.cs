using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public MapGenerator mapGenerator;
    public MapRenderer mapRenderer;
    public Camera inputCamera;
    public float movementCooldown = 0.1f;
    [Range(0.02f, 0.5f)] public float movementStepDuration = 0.12f;
    public float worldDepthOffset = 0.05f;
    public float pathLineHeight = 0.08f;
    public float pathLineWidth = 0.08f;
    public Color pathLineColor = Color.cyan;

    public Vector2Int CurrentCell => currentCell;
    public event System.Action<Vector2Int> OnPlayerMoved;

    private Vector2Int currentCell;
    private bool hasPlaced;
    private bool isMoving;
    private float nextMoveTime;
    private float moveStartTime;
    private readonly Queue<Vector2Int> queuedPathSteps = new Queue<Vector2Int>();
    private readonly List<Vector2Int> currentPath = new List<Vector2Int>();
    private LineRenderer pathLineRenderer;
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

    private void Awake()
    {
        EnsurePathLineRenderer();
    }

    private void Update()
    {
        if (!TryPlaceOnStartCell())
            return;

        UpdateActiveMovement();

        TryHandleMousePathRequest();

        if (!isMoving && queuedPathSteps.Count > 0 && Time.time >= nextMoveTime)
        {
            Vector2Int nextStep = queuedPathSteps.Dequeue();
            BeginMoveToCell(nextStep);
            UpdatePathLine();
        }

        if (isMoving || Time.time < nextMoveTime)
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
            queuedPathSteps.Clear();
            currentPath.Clear();
            UpdatePathLine();
            TryMove(direction);
        }
    }

    public void SetMapReferences(MapGenerator generator, MapRenderer renderer)
    {
        mapGenerator = generator;
        mapRenderer = renderer;
        hasPlaced = false;
        isMoving = false;
        queuedPathSteps.Clear();
        currentPath.Clear();
        UpdatePathLine();
    }

    private bool TryPlaceOnStartCell()
    {
        if (hasPlaced)
            return true;

        if (mapGenerator == null || mapRenderer == null || !mapRenderer.HasMap)
            return false;

        currentCell = mapGenerator.GetCentralPlayableCell();
        transform.position = mapRenderer.GetCellWorldCenter(currentCell) + Vector3.forward * worldDepthOffset;
        moveTargetPosition = transform.position;
        hasPlaced = true;
        UpdatePathLine();
        return true;
    }

    private void TryHandleMousePathRequest()
    {
        if (isMoving)
            return;

        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            return;

        Camera cam = inputCamera != null ? inputCamera : Camera.main;
        if (cam == null)
            return;

        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, 10000f))
            return;

        if (!mapRenderer.TryGetClosestPlayableCell(hit.point, out Vector2Int targetCell))
            return;

        if (!TryBuildPath(currentCell, targetCell, out List<Vector2Int> path))
            return;

        queuedPathSteps.Clear();
        currentPath.Clear();
        currentPath.AddRange(path);

        for (int i = 1; i < path.Count; i++)
            queuedPathSteps.Enqueue(path[i]);

        UpdatePathLine();
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

    private void TryMove(Vector2Int direction)
    {
        Vector2Int nextCell = currentCell + direction;

        if (!mapGenerator.GetIsPlayable(nextCell.x, nextCell.y))
            return;

        BeginMoveToCell(nextCell);
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
        OnPlayerMoved?.Invoke(currentCell);
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

        pathLineRenderer.positionCount = currentPath.Count;
        for (int i = 0; i < currentPath.Count; i++)
        {
            Vector3 p = mapRenderer.GetCellWorldCenter(currentPath[i]);
            p.y += pathLineHeight;
            pathLineRenderer.SetPosition(i, p);
        }
    }
}
