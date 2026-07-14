using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class FogOfWar : MonoBehaviour
{
    [Header("References")]
    public MapGenerator mapGenerator;
    public MapRenderer mapRenderer;
    public PlayerController playerController;

    [Header("Vision")]
    [Min(1)] public int visionRadius = 5;

    [Header("Fog Appearance")]
    public Color fogColor = new Color(0.05f, 0.05f, 0.05f, 1f);
    [Range(0.1f, 1f)] public float maxFogAlpha = 0.85f;
    [Min(1)] public int gradientCells = 4;
    public float fogHeight = 0.15f;

    private bool[,] explored;
    private Mesh fogMesh;
    private MeshFilter fogMeshFilter;
    private MeshRenderer fogMeshRenderer;
    private Material[] fogMaterials;
    private bool initialized;

    private static readonly Vector2Int[] CardinalDirections =
    {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1)
    };

    private void OnEnable()
    {
        if (playerController != null)
            playerController.OnPlayerMoved += HandlePlayerMoved;
    }

    private void OnDisable()
    {
        if (playerController != null)
            playerController.OnPlayerMoved -= HandlePlayerMoved;

        ClearFog();
    }

    private void OnDestroy()
    {
        DestroyFogResources();
    }

    private void Start()
    {
        if (!Application.isPlaying)
            return;

        if (mapGenerator == null || mapRenderer == null || playerController == null)
        {
            Debug.LogWarning("FogOfWar: missing references.");
            return;
        }

        Initialize();
        RevealAround(playerController.CurrentCell);
        RebuildFogMesh();
    }

    private void Initialize()
    {
        if (initialized)
            return;

        explored = new bool[mapGenerator.width, mapGenerator.height];
        fogMaterials = BuildGradientMaterials();
        EnsureFogObjects();
        initialized = true;
    }

    private void HandlePlayerMoved(Vector2Int cell)
    {
        if (!initialized)
            Initialize();

        RevealAround(cell);
        RebuildFogMesh();
    }

    private void RevealAround(Vector2Int center)
    {
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        bool[,] inRadius = new bool[mapGenerator.width, mapGenerator.height];

        if (!IsInBounds(center))
            return;

        queue.Enqueue(center);
        inRadius[center.x, center.y] = true;

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            int dist = Mathf.Abs(current.x - center.x) + Mathf.Abs(current.y - center.y);

            if (dist >= visionRadius)
                continue;

            foreach (var dir in CardinalDirections)
            {
                Vector2Int next = current + dir;
                if (!IsInBounds(next) || inRadius[next.x, next.y])
                    continue;

                inRadius[next.x, next.y] = true;
                queue.Enqueue(next);
            }
        }

        for (int x = 0; x < mapGenerator.width; x++)
            for (int y = 0; y < mapGenerator.height; y++)
                if (inRadius[x, y])
                    explored[x, y] = true;
    }

    private int[,] ComputeDistanceFromExplored()
    {
        int w = mapGenerator.width;
        int h = mapGenerator.height;
        int[,] dist = new int[w, h];
        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                if (explored[x, y])
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

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            int d = dist[current.x, current.y];

            foreach (var dir in CardinalDirections)
            {
                int nx = current.x + dir.x;
                int ny = current.y + dir.y;
                if (nx < 0 || nx >= w || ny < 0 || ny >= h)
                    continue;

                if (dist[nx, ny] <= d + 1)
                    continue;

                dist[nx, ny] = d + 1;
                queue.Enqueue(new Vector2Int(nx, ny));
            }
        }

        return dist;
    }

    private void RebuildFogMesh()
    {
        int w = mapGenerator.width;
        int h = mapGenerator.height;
        int levels = gradientCells + 1;

        EnsureFogObjects();

        if (fogMaterials == null || fogMaterials.Length != levels)
        {
            DestroyMaterials();
            fogMaterials = BuildGradientMaterials();
            if (fogMeshRenderer != null)
                fogMeshRenderer.sharedMaterials = fogMaterials;
        }

        int[,] dist = ComputeDistanceFromExplored();
        bool[,] interiorHoleMask = MapInteriorHoleUtility.BuildInteriorHoleMask(mapGenerator);
        bool hasValidInteriorMask =
            interiorHoleMask != null &&
            interiorHoleMask.GetLength(0) == w &&
            interiorHoleMask.GetLength(1) == h;

        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<int>[] submeshes = new List<int>[levels];
        for (int i = 0; i < levels; i++)
            submeshes[i] = new List<int>();

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                bool isPlayable = mapGenerator.GetIsPlayable(x, y);
                bool shouldCoverHole =
                    mapRenderer != null &&
                    mapRenderer.fillInteriorHoles &&
                    hasValidInteriorMask &&
                    interiorHoleMask[x, y];

                if (!isPlayable && !shouldCoverHole)
                    continue;

                int d = dist[x, y];
                if (d == 0)
                    continue;

                int levelIndex = Mathf.Clamp(d - 1, 0, levels - 1);

                Vector3 center = transform.InverseTransformPoint(mapRenderer.GetCellWorldCenter(x, y));
                center.y = mapRenderer.cellHeight + fogHeight;

                float half = mapGenerator.cellSize * 0.5f;
                int baseIndex = vertices.Count;

                vertices.Add(center + new Vector3(-half, 0, -half));
                vertices.Add(center + new Vector3(half, 0, -half));
                vertices.Add(center + new Vector3(-half, 0, half));
                vertices.Add(center + new Vector3(half, 0, half));

                normals.Add(Vector3.up);
                normals.Add(Vector3.up);
                normals.Add(Vector3.up);
                normals.Add(Vector3.up);

                submeshes[levelIndex].Add(baseIndex);
                submeshes[levelIndex].Add(baseIndex + 2);
                submeshes[levelIndex].Add(baseIndex + 1);
                submeshes[levelIndex].Add(baseIndex + 1);
                submeshes[levelIndex].Add(baseIndex + 2);
                submeshes[levelIndex].Add(baseIndex + 3);
            }
        }

        bool anyGeometry = false;
        foreach (var sub in submeshes)
            if (sub.Count > 0) { anyGeometry = true; break; }

        if (!anyGeometry)
        {
            fogMesh.Clear();
            if (fogMeshRenderer != null)
                fogMeshRenderer.enabled = false;
            return;
        }

        if (fogMeshRenderer != null)
            fogMeshRenderer.enabled = true;

        fogMesh.Clear();
        fogMesh.SetVertices(vertices);
        fogMesh.SetNormals(normals);
        fogMesh.subMeshCount = levels;
        for (int i = 0; i < levels; i++)
            fogMesh.SetTriangles(submeshes[i], i);

        fogMesh.RecalculateBounds();
    }

    private void EnsureFogObjects()
    {
        if (fogMeshFilter == null)
            fogMeshFilter = GetComponent<MeshFilter>();

        if (fogMeshRenderer == null)
            fogMeshRenderer = GetComponent<MeshRenderer>();

        if (fogMesh == null)
        {
            fogMesh = new Mesh();
            fogMesh.name = "FogMesh";
            fogMesh.indexFormat = IndexFormat.UInt32;
            fogMesh.MarkDynamic();
        }

        if (fogMeshFilter.sharedMesh != fogMesh)
            fogMeshFilter.sharedMesh = fogMesh;

        if (fogMaterials != null && fogMeshRenderer.sharedMaterials != fogMaterials)
            fogMeshRenderer.sharedMaterials = fogMaterials;
    }

    private Material[] BuildGradientMaterials()
    {
        int levels = gradientCells + 1;
        Material[] mats = new Material[levels];

        for (int i = 0; i < levels; i++)
        {
            float t = (float)(i + 1) / levels;
            float alpha = Mathf.Lerp(0f, maxFogAlpha, t);
            Color c = new Color(fogColor.r, fogColor.g, fogColor.b, alpha);
            mats[i] = CreateTransparentMaterial(c);
        }

        return mats;
    }

    private Material CreateTransparentMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material mat = new Material(shader) { name = "FogGradient" };

        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
        }
        else
        {
            mat.SetFloat("_Mode", 3f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.SetOverrideTag("RenderType", "Transparent");
        }

        mat.renderQueue = (int)RenderQueue.Transparent;

        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);

        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);

        return mat;
    }

    private void ClearFog()
    {
        if (fogMesh != null)
            fogMesh.Clear();

        if (fogMeshRenderer != null)
            fogMeshRenderer.enabled = false;
    }

    private void DestroyFogResources()
    {
        if (fogMesh != null)
        {
            if (Application.isPlaying)
                Destroy(fogMesh);
            else
                DestroyImmediate(fogMesh);

            fogMesh = null;
        }

        fogMeshFilter = null;
        fogMeshRenderer = null;

        DestroyMaterials();
    }

    private void DestroyMaterials()
    {
        if (fogMaterials == null)
            return;

        foreach (var mat in fogMaterials)
        {
            if (mat == null)
                continue;

            if (Application.isPlaying)
                Destroy(mat);
            else
                DestroyImmediate(mat);
        }

        fogMaterials = null;
    }

    private bool IsInBounds(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < mapGenerator.width &&
               cell.y >= 0 && cell.y < mapGenerator.height;
    }
}
