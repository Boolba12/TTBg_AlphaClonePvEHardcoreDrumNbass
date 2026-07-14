using System.Collections.Generic;
using UnityEngine;

public class MapRockPlacer : MonoBehaviour
{
    [System.Serializable]
    public class BiomeObjectConfig
    {
        public BiomeType biomeType;
        [Range(0f, 1f)] public float spawnRate = 0.3f;
        [Min(0.1f)] public float minScale = 0.3f;
        [Min(0.1f)] public float maxScale = 0.8f;
        public GameObject objectPrefab;
        [Tooltip("If false, uses default cube primitive")]
        public bool usePrefab = true;
    }

    [Header("References")]
    public MapGenerator mapGenerator;
    public MapRenderer mapRenderer;

    [Header("Spawn Area")]
    [Tooltip("How many cells deep from playable border to spawn. Limits spawning to inside the map.")]
    [Min(1)] public int borderDepth = 3;

    [Header("Environment Objects")]
    public BiomeObjectConfig[] biomeObjects = new BiomeObjectConfig[]
    {
        new BiomeObjectConfig { biomeType = BiomeType.Forest, spawnRate = 0.4f, minScale = 0.2f, maxScale = 0.7f },
        new BiomeObjectConfig { biomeType = BiomeType.Swamp, spawnRate = 0.3f, minScale = 0.25f, maxScale = 0.6f },
        new BiomeObjectConfig { biomeType = BiomeType.Desert, spawnRate = 0.25f, minScale = 0.3f, maxScale = 0.8f }
    };

    public int seed = 0;

    private GameObject environmentContainer;

    private void Start()
    {
        if (!Application.isPlaying)
            return;

        if (mapGenerator == null || mapRenderer == null)
        {
            Debug.LogWarning("MapRockPlacer: missing references.");
            return;
        }

        StartCoroutine(WaitAndPlace());
    }

    private System.Collections.IEnumerator WaitAndPlace()
    {
        while (!mapGenerator.HasGeneratedData || mapGenerator.generatedPlayableCount <= 0)
            yield return null;

        while (!mapRenderer.HasMap)
            yield return null;

        PlaceEnvironment();
    }

    [ContextMenu("Place Environment")]
    public void PlaceEnvironment()
    {
        if (mapGenerator == null || mapRenderer == null)
            return;

        ClearEnvironment();

        environmentContainer = new GameObject("MapEnvironment");
        environmentContainer.transform.parent = transform;
        environmentContainer.transform.localPosition = Vector3.zero;

        System.Random rng = new System.Random(seed);

        int w = mapGenerator.width;
        int h = mapGenerator.height;

        // Compute distance from each unplayable cell to nearest playable cell
        int[,] distToPlayable = ComputeDistanceToPlayable();

        // Spawn objects for forest, swamp, desert ONLY within borderDepth
        foreach (var config in biomeObjects)
        {
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    if (mapGenerator.GetIsPlayable(x, y))
                        continue;

                    int dist = distToPlayable[x, y];
                    if (dist == int.MaxValue || dist > borderDepth)
                        continue;

                    if (mapGenerator.GetBiomeAt(x, y) != config.biomeType)
                        continue;

                    if (rng.NextDouble() < config.spawnRate)
                    {
                        SpawnEnvironmentObject(x, y, config, rng);
                    }
                }
            }
        }
    }

    public void ClearEnvironment()
    {
        if (environmentContainer != null)
        {
            if (Application.isPlaying)
                Destroy(environmentContainer);
            else
                DestroyImmediate(environmentContainer);

            environmentContainer = null;
        }
    }

    private void SpawnEnvironmentObject(int x, int y, BiomeObjectConfig config, System.Random rng)
    {
        Vector3 worldPos = mapRenderer.GetCellWorldCenter(x, y);
        float cellSize = mapGenerator.cellSize;

        // Random position within cell
        float ox = ((float)rng.NextDouble() - 0.5f) * cellSize * 0.6f;
        float oz = ((float)rng.NextDouble() - 0.5f) * cellSize * 0.6f;
        worldPos.x += ox;
        worldPos.z += oz;

        // Random scale within config range
        float scale = Mathf.Lerp(config.minScale, config.maxScale, (float)rng.NextDouble()) * cellSize;
        worldPos.y = mapRenderer.cellHeight * 0.5f + scale * 0.5f;

        // Random rotation
        float rotY = (float)rng.NextDouble() * 360f;
        float rotX = (float)rng.NextDouble() * 20f - 10f;
        float rotZ = (float)rng.NextDouble() * 20f - 10f;

        GameObject obj;

        if (config.usePrefab && config.objectPrefab != null)
        {
            // Instantiate from prefab
            obj = Instantiate(config.objectPrefab);
            obj.name = $"EnvObject_{config.biomeType}_{x}_{y}";
            obj.transform.parent = environmentContainer.transform;
            obj.transform.position = worldPos;
            obj.transform.localScale = new Vector3(scale, scale, scale);
            obj.transform.rotation = Quaternion.Euler(rotX, rotY, rotZ);
        }
        else
        {
            // Fallback to cube primitive
            obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = $"EnvObject_{config.biomeType}_{x}_{y}";
            obj.transform.parent = environmentContainer.transform;
            obj.transform.position = worldPos;
            obj.transform.localScale = new Vector3(scale, scale * 1.2f, scale);  // Slightly taller for tree-like appearance
            obj.transform.rotation = Quaternion.Euler(rotX, rotY, rotZ);

            // Remove collider on primitive
            Collider col = obj.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying)
                    Destroy(col);
                else
                    DestroyImmediate(col);
            }

            // Color variation for primitive
            MeshRenderer mr = obj.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                Material objMat = new Material(mr.sharedMaterial);
                Color baseColor = mapGenerator.GetBiomeColor(config.biomeType);
                float variation = 0.7f + (float)rng.NextDouble() * 0.3f;
                objMat.color = baseColor * variation;
                mr.sharedMaterial = objMat;
            }
        }
    }

    private int[,] ComputeDistanceToPlayable()
    {
        int w = mapGenerator.width;
        int h = mapGenerator.height;
        int[,] dist = new int[w, h];
        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        // Initialize: playable cells have distance 0
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                if (mapGenerator.GetIsPlayable(x, y))
                {
                    dist[x, y] = 0;
                    queue.Enqueue(new Vector2Int(x, y));
                }
                else
                {
                    dist[x, y] = int.MaxValue;
                }
            }
        }

        // BFS to compute distance
        while (queue.Count > 0)
        {
            Vector2Int curr = queue.Dequeue();
            int d = dist[curr.x, curr.y];

            int cx = curr.x;
            int cy = curr.y;

            TryEnqueue(cx + 1, cy, d, w, h, dist, queue);
            TryEnqueue(cx - 1, cy, d, w, h, dist, queue);
            TryEnqueue(cx, cy + 1, d, w, h, dist, queue);
            TryEnqueue(cx, cy - 1, d, w, h, dist, queue);
        }

        return dist;
    }

    private static void TryEnqueue(int x, int y, int currentDist, int w, int h, int[,] dist, Queue<Vector2Int> queue)
    {
        if (x < 0 || x >= w || y < 0 || y >= h)
            return;

        if (dist[x, y] <= currentDist + 1)
            return;

        dist[x, y] = currentDist + 1;
        queue.Enqueue(new Vector2Int(x, y));
    }
}
