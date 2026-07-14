using System.Collections.Generic;
using UnityEngine;

public enum BiomeType { Forest, Desert, Mountain, Swamp, Plains, Jungle }

[ExecuteAlways]
public class MapGenerator : MonoBehaviour
{
    public int width = 12;
    public int height = 12;
    [Min(1)] public int playableCount = 100;
    public float cellSize = 1f;
    public int seed = 0;
    public bool autoGenerate = true;
    public Color playableColor = Color.green;
    public Color blockedColor = Color.gray;
    public Color startCellColor = Color.yellow;
    [Range(0f, 1f)] public float centerBias = 0.3f;
    [Min(0)] public int startRadius = 2;
    [Min(1)] public int smoothingPasses = 3;
    [Min(1)] public int maxDrawCells = 4000;
    public bool drawGizmos = true;
    [Range(0.01f, 0.2f)] public float biomeNoiseScale = 0.08f;
    public Color biomeForest = new Color(0.2f, 0.6f, 0.2f);
    public Color biomeDesert = new Color(0.9f, 0.8f, 0.3f);
    public Color biomeMountain = new Color(0.5f, 0.5f, 0.5f);
    public Color biomeSwamp = new Color(0.4f, 0.5f, 0.3f);
    public Color biomePlains = new Color(0.7f, 0.7f, 0.3f);
    public Color biomeJungle = new Color(0.15f, 0.45f, 0.1f);

    private static readonly Vector2Int[] CardinalDirections =
    {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1)
    };

    private bool[,] isPlayable;
    private BiomeType[,] biomeMap;
    private Vector2Int startCell;
    private HashSet<Vector2Int> playableCells;
    private HashSet<Vector2Int> frontierCells;
    [HideInInspector] public int generatedPlayableCount;
    public int GenerationVersion { get; private set; }

    public bool HasGeneratedData => isPlayable != null;

    private void Start()
    {
        if (Application.isPlaying && autoGenerate)
            Generate();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying && autoGenerate)
        {
            Generate();
        }
    }

    [ContextMenu("Generate Map")]
    public void Generate()
    {
        if (width <= 0 || height <= 0)
            return;

        MapInteriorHoleUtility.InvalidateCache(this);
        GenerationVersion++;

        playableCount = Mathf.Min(playableCount, width * height);
        isPlayable = new bool[width, height];
        biomeMap = new BiomeType[width, height];
        playableCells = new HashSet<Vector2Int>();
        frontierCells = new HashSet<Vector2Int>();

        Random.InitState(seed);

        GenerateBiomeMap();

        Vector2Int center = new Vector2Int(width / 2, height / 2);
        startCell = GetStartCell(center);

        playableCells.Add(startCell);
        isPlayable[startCell.x, startCell.y] = true;
        AddNeighborsToFrontier(startCell);

        // Phase 1: Grow blob larger than target
        int targetGrowth = Mathf.RoundToInt(playableCount * 1.2f);
        targetGrowth = Mathf.Min(targetGrowth, width * height - 1);

        while (playableCells.Count < targetGrowth && frontierCells.Count > 0)
        {
            List<Vector2Int> frontierList = new List<Vector2Int>(frontierCells);
            Vector2Int chosen = frontierList[Random.Range(0, frontierList.Count)];

            frontierCells.Remove(chosen);
            playableCells.Add(chosen);
            isPlayable[chosen.x, chosen.y] = true;
            AddNeighborsToFrontier(chosen);
        }

        // Phase 2: Smooth the blob
        for (int pass = 0; pass < smoothingPasses; pass++)
        {
            SmoothMap();
        }

        // Phase 3: Fill enclosed holes and reconnect 4-directional regions
        FillInteriorHoles();
        ConnectAllPlayableRegions();

        // Phase 4: Adjust to exact count without breaking connectivity
        NormalizePlayableCount(playableCount);

        generatedPlayableCount = CountPlayable();
    }

    private void GenerateBiomeMap()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float nx = (x + seed * 0.1f) * biomeNoiseScale;
                float ny = (y + seed * 0.1f) * biomeNoiseScale;
                float value = Mathf.PerlinNoise(nx, ny);
                
                if (value < 0.167f)
                    biomeMap[x, y] = BiomeType.Desert;
                else if (value < 0.334f)
                    biomeMap[x, y] = BiomeType.Plains;
                else if (value < 0.5f)
                    biomeMap[x, y] = BiomeType.Forest;
                else if (value < 0.667f)
                    biomeMap[x, y] = BiomeType.Jungle;
                else if (value < 0.834f)
                    biomeMap[x, y] = BiomeType.Swamp;
                else
                    biomeMap[x, y] = BiomeType.Mountain;
            }
        }
    }

    public Color GetBiomeColor(BiomeType biome)
    {
        return biome switch
        {
            BiomeType.Forest => biomeForest,
            BiomeType.Desert => biomeDesert,
            BiomeType.Mountain => biomeMountain,
            BiomeType.Swamp => biomeSwamp,
            BiomeType.Plains => biomePlains,
            BiomeType.Jungle => biomeJungle,
            _ => Color.white
        };
    }

    private void FillInteriorHoles()
    {
        bool[,] exteriorBlocked = new bool[width, height];
        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        for (int x = 0; x < width; x++)
        {
            EnqueueExteriorBlockedCell(x, 0, exteriorBlocked, queue);
            EnqueueExteriorBlockedCell(x, height - 1, exteriorBlocked, queue);
        }

        for (int y = 0; y < height; y++)
        {
            EnqueueExteriorBlockedCell(0, y, exteriorBlocked, queue);
            EnqueueExteriorBlockedCell(width - 1, y, exteriorBlocked, queue);
        }

        while (queue.Count > 0)
        {
            Vector2Int cell = queue.Dequeue();

            foreach (var dir in CardinalDirections)
            {
                int nx = cell.x + dir.x;
                int ny = cell.y + dir.y;
                if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                    continue;

                if (isPlayable[nx, ny] || exteriorBlocked[nx, ny])
                    continue;

                exteriorBlocked[nx, ny] = true;
                queue.Enqueue(new Vector2Int(nx, ny));
            }
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (!isPlayable[x, y] && !exteriorBlocked[x, y])
                {
                    isPlayable[x, y] = true;
                }
            }
        }
    }

    private void EnqueueExteriorBlockedCell(int x, int y, bool[,] exteriorBlocked, Queue<Vector2Int> queue)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
            return;

        if (isPlayable[x, y] || exteriorBlocked[x, y])
            return;

        exteriorBlocked[x, y] = true;
        queue.Enqueue(new Vector2Int(x, y));
    }

    private void ConnectAllPlayableRegions()
    {
        bool[,] visited = new bool[width, height];
        List<HashSet<Vector2Int>> components = new List<HashSet<Vector2Int>>();

        // Find all connected components
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (isPlayable[x, y] && !visited[x, y])
                {
                    HashSet<Vector2Int> component = new HashSet<Vector2Int>();
                    FloodFillComponent(x, y, visited, component);
                    components.Add(component);
                }
            }
        }

        if (components.Count <= 1)
            return; // Only one component, no islands

        // Find largest component
        HashSet<Vector2Int> largest = components[0];
        foreach (var comp in components)
        {
            if (comp.Count > largest.Count)
                largest = comp;
        }

        // Connect every other component to the largest one using 4-direction paths.
        foreach (var comp in components)
        {
            if (comp != largest)
            {
                ConnectComponentToLargest(comp, largest);
            }
        }
    }

    private void NormalizePlayableCount(int targetCount)
    {
        if (targetCount <= 0)
            return;

        while (CountPlayable() > targetCount)
        {
            if (!RemoveOnePlayableCellSafely())
                break;
        }

        while (CountPlayable() < targetCount)
        {
            int before = CountPlayable();
            AddCellsToTarget(targetCount - before);

            if (CountPlayable() == before)
                break;
        }
    }

    private bool RemoveOnePlayableCellSafely()
    {
        List<Vector2Int> leafCandidates = new List<Vector2Int>();
        List<Vector2Int> otherCandidates = new List<Vector2Int>();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (!isPlayable[x, y] || (x == startCell.x && y == startCell.y))
                    continue;

                if (CountPlayableNeighbors(x, y) <= 1)
                    leafCandidates.Add(new Vector2Int(x, y));
                else
                    otherCandidates.Add(new Vector2Int(x, y));
            }
        }

        if (TryRemoveCandidateList(leafCandidates))
            return true;

        return TryRemoveCandidateList(otherCandidates);
    }

    private bool TryRemoveCandidateList(List<Vector2Int> candidates)
    {
        if (candidates.Count == 0)
            return false;

        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            (candidates[i], candidates[swapIndex]) = (candidates[swapIndex], candidates[i]);
        }

        foreach (var cell in candidates)
        {
            if (CanRemoveCellWithoutDisconnecting(cell.x, cell.y))
            {
                isPlayable[cell.x, cell.y] = false;
                return true;
            }
        }

        return false;
    }

    private bool CanRemoveCellWithoutDisconnecting(int x, int y)
    {
        bool previous = isPlayable[x, y];
        isPlayable[x, y] = false;

        int totalPlayable = CountPlayable();
        int reachablePlayable = CountReachablePlayableCells();

        isPlayable[x, y] = previous;
        return reachablePlayable == totalPlayable;
    }

    private int CountReachablePlayableCells()
    {
        if (isPlayable == null || width <= 0 || height <= 0)
            return 0;

        bool[,] visited = new bool[width, height];
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(startCell);
        visited[startCell.x, startCell.y] = true;

        int count = 0;
        while (queue.Count > 0)
        {
            Vector2Int cell = queue.Dequeue();
            count++;

            foreach (var dir in CardinalDirections)
            {
                int nx = cell.x + dir.x;
                int ny = cell.y + dir.y;
                if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                    continue;

                if (!isPlayable[nx, ny] || visited[nx, ny])
                    continue;

                visited[nx, ny] = true;
                queue.Enqueue(new Vector2Int(nx, ny));
            }
        }

        return count;
    }

    private void ConnectComponentToLargest(HashSet<Vector2Int> component, HashSet<Vector2Int> largest)
    {
        Vector2Int fromCell = default;
        Vector2Int toCell = default;
        int bestDistance = int.MaxValue;

        foreach (var cell in component)
        {
            foreach (var mainCell in largest)
            {
                int distance = Mathf.Abs(cell.x - mainCell.x) + Mathf.Abs(cell.y - mainCell.y);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    fromCell = cell;
                    toCell = mainCell;
                }
            }
        }

        int x = fromCell.x;
        int y = fromCell.y;

        while (x != toCell.x)
        {
            isPlayable[x, y] = true;
            x += toCell.x > x ? 1 : -1;
        }

        while (y != toCell.y)
        {
            isPlayable[x, y] = true;
            y += toCell.y > y ? 1 : -1;
        }

        isPlayable[toCell.x, toCell.y] = true;
    }

    private void FloodFillComponent(int x, int y, bool[,] visited, HashSet<Vector2Int> component)
    {
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(new Vector2Int(x, y));
        visited[x, y] = true;

        while (queue.Count > 0)
        {
            Vector2Int cell = queue.Dequeue();
            component.Add(cell);

            foreach (var dir in CardinalDirections)
            {
                int nx = cell.x + dir.x;
                int ny = cell.y + dir.y;
                if (nx >= 0 && nx < width && ny >= 0 && ny < height && isPlayable[nx, ny] && !visited[nx, ny])
                {
                    visited[nx, ny] = true;
                    queue.Enqueue(new Vector2Int(nx, ny));
                }
            }
        }
    }

    private void SmoothMap()
    {
        bool[,] smoothed = (bool[,])isPlayable.Clone();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int neighbors = CountPlayableNeighbors(x, y);

                if (isPlayable[x, y])
                {
                    // Remove dead ends and very thin walls.
                    if (neighbors <= 1)
                    {
                        smoothed[x, y] = false;
                    }
                }
                else
                {
                    // Fill holes that are almost fully enclosed by 4-direction neighbors.
                    if (neighbors >= 3)
                    {
                        smoothed[x, y] = true;
                    }
                }
            }
        }

        isPlayable = smoothed;
    }

    private void AdjustToExactCount()
    {
        int current = CountPlayable();

        if (current > playableCount)
        {
            RemoveCellsToTarget(current - playableCount);
        }
        else if (current < playableCount)
        {
            AddCellsToTarget(playableCount - current);
        }
    }

    private void RemoveCellsToTarget(int toRemove)
    {
        List<Vector2Int> candidates = new List<Vector2Int>();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (isPlayable[x, y] && !(x == startCell.x && y == startCell.y))
                {
                    int neighbors = CountPlayableNeighbors(x, y);
                    if (neighbors >= 1)
                    {
                        candidates.Add(new Vector2Int(x, y));
                    }
                }
            }
        }

        for (int i = 0; i < toRemove && candidates.Count > 0; i++)
        {
            int idx = Random.Range(0, candidates.Count);
            Vector2Int cell = candidates[idx];
            isPlayable[cell.x, cell.y] = false;
            candidates.RemoveAt(idx);
        }
    }

    private void AddCellsToTarget(int toAdd)
    {
        List<Vector2Int> candidates = new List<Vector2Int>();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (!isPlayable[x, y] && CountPlayableNeighbors(x, y) > 0)
                {
                    candidates.Add(new Vector2Int(x, y));
                }
            }
        }

        for (int i = 0; i < toAdd && candidates.Count > 0; i++)
        {
            int idx = Random.Range(0, candidates.Count);
            Vector2Int cell = candidates[idx];
            isPlayable[cell.x, cell.y] = true;
            candidates.RemoveAt(idx);
        }
    }

    private Vector2Int GetStartCell(Vector2Int center)
    {
        if (startRadius <= 0)
            return center;

        int radius = Mathf.Clamp(startRadius, 0, Mathf.Max(width, height));
        int offsetX = Random.Range(-radius, radius + 1);
        int offsetY = Random.Range(-radius, radius + 1);

        return new Vector2Int(
            Mathf.Clamp(center.x + offsetX, 0, width - 1),
            Mathf.Clamp(center.y + offsetY, 0, height - 1));
    }

    private void AddNeighborsToFrontier(Vector2Int cell)
    {
        Vector2Int[] directions =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1)
        };

        foreach (var dir in directions)
        {
            Vector2Int next = cell + dir;
            if (next.x >= 0 && next.x < width && next.y >= 0 && next.y < height && !playableCells.Contains(next) && !frontierCells.Contains(next))
            {
                frontierCells.Add(next);
            }
        }
    }

    private int CountPlayable()
    {
        int count = 0;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (isPlayable[x, y])
                    count++;
            }
        }
        return count;
    }

    // Public accessors for MapRenderer
    public bool GetIsPlayable(int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
            return false;

        if (isPlayable == null)
            return false;

        return isPlayable[x, y];
    }

    public Vector2Int GetStartCell()
    {
        return startCell;
    }

    public Vector2Int GetCentralPlayableCell()
    {
        Vector2Int center = new Vector2Int(width / 2, height / 2);

        if (GetIsPlayable(center.x, center.y))
            return center;

        return FindNearestPlayableCell(center);
    }

    public BiomeType GetBiomeAt(int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
            return BiomeType.Plains;

        if (biomeMap == null)
            return BiomeType.Plains;

        return biomeMap[x, y];
    }

    private Vector2Int FindNearestPlayableCell(Vector2Int from)
    {
        if (isPlayable == null)
            return startCell;

        bool[,] visited = new bool[width, height];
        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        queue.Enqueue(from);
        visited[from.x, from.y] = true;

        while (queue.Count > 0)
        {
            Vector2Int cell = queue.Dequeue();

            if (isPlayable[cell.x, cell.y])
                return cell;

            foreach (var dir in CardinalDirections)
            {
                int nx = cell.x + dir.x;
                int ny = cell.y + dir.y;
                if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                    continue;

                if (visited[nx, ny])
                    continue;

                visited[nx, ny] = true;
                queue.Enqueue(new Vector2Int(nx, ny));
            }
        }

        return startCell;
    }

    private int CountPlayableNeighbors(int x, int y)
    {
        int count = 0;
        for (int i = 0; i < CardinalDirections.Length; i++)
        {
            Vector2Int dir = CardinalDirections[i];

            int nx = x + dir.x;
            int ny = y + dir.y;
            if (nx >= 0 && nx < width && ny >= 0 && ny < height && isPlayable[nx, ny])
            {
                count++;
            }
        }
        return count;
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos || isPlayable == null)
            return;

        Gizmos.matrix = Matrix4x4.TRS(transform.position, Quaternion.identity, Vector3.one);

        int drawCount = 0;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (drawCount >= maxDrawCells)
                    return;

                Vector3 center = new Vector3(x * cellSize + cellSize * 0.5f, 0f, y * cellSize + cellSize * 0.5f);

                if (x == startCell.x && y == startCell.y)
                {
                    Gizmos.color = startCellColor;
                }
                else if (isPlayable[x, y])
                {
                    if (biomeMap != null)
                    {
                        Gizmos.color = GetBiomeColor(biomeMap[x, y]);
                    }
                    else
                    {
                        Gizmos.color = playableColor;
                    }
                }
                else
                {
                    Gizmos.color = blockedColor;
                }

                Gizmos.DrawCube(center, Vector3.one * cellSize * 0.95f);
                drawCount++;
            }
        }
    }

}