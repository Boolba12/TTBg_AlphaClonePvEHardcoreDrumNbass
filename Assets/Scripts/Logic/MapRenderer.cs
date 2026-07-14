using System.Collections.Generic;
using UnityEngine;

public class MapRenderer : MonoBehaviour
{
    public MapGenerator mapGenerator;
    public Material planeMaterial;
    public float cellHeight = 0.1f;
    public Vector2 cellOffset = Vector2.zero;
    [Range(0.01f, 1f)] public float cellOffsetNoiseScale = 0.25f;
    public bool fillInteriorHoles = true;
    public bool autoRender = true;
    
    private static Mesh sharedPlaneMesh;
    private static Material sharedFallbackMaterial;
    private GameObject mapContainer;
    private Mesh generatedMesh;
    private Material[] generatedMaterials;

    public bool HasMap => mapContainer != null;

    private void Start()
    {
        if (!Application.isPlaying || mapGenerator == null)
            return;

        if (mapGenerator.generatedPlayableCount <= 0)
            mapGenerator.Generate();

        RenderMap();
    }

    private void OnDisable()
    {
        ClearMap();
    }

    private void OnDestroy()
    {
        ClearMap();
    }

    [ContextMenu("Render Map")]
    public void RenderMap()
    {
        if (mapGenerator == null)
        {
            Debug.LogError("MapRenderer: MapGenerator not assigned!");
            return;
        }

        if (!mapGenerator.HasGeneratedData || mapGenerator.generatedPlayableCount <= 0)
        {
            mapGenerator.Generate();
        }

        ClearMap();
        CreateMapVisuals();
    }

    public void ClearGeneratedMap()
    {
        ClearMap();
    }

    private void ClearMap()
    {
        if (generatedMaterials != null)
        {
            for (int i = 0; i < generatedMaterials.Length; i++)
            {
                if (generatedMaterials[i] == null)
                    continue;

                if (Application.isPlaying)
                    Destroy(generatedMaterials[i]);
                else
                    DestroyImmediate(generatedMaterials[i]);
            }

            generatedMaterials = null;
        }

        if (generatedMesh != null)
        {
            if (Application.isPlaying)
                Destroy(generatedMesh);
            else
                DestroyImmediate(generatedMesh);

            generatedMesh = null;
        }

        if (mapContainer != null)
        {
            if (Application.isPlaying)
                Destroy(mapContainer);
            else
                DestroyImmediate(mapContainer);

            mapContainer = null;
        }
    }

    private void CreateMapVisuals()
    {
        mapContainer = new GameObject("MapVisuals");
        mapContainer.transform.parent = transform;
        mapContainer.transform.localPosition = Vector3.zero;
        mapContainer.transform.localRotation = Quaternion.identity;

        int width = mapGenerator.width;
        int height = mapGenerator.height;

        int vertCols = width + 1;
        int vertRows = height + 1;
        Vector3[] vertices = new Vector3[vertCols * vertRows];
        Vector3[] normals = new Vector3[vertCols * vertRows];
        Vector2[] uvs = new Vector2[vertCols * vertRows];

        for (int x = 0; x < vertCols; x++)
        {
            for (int y = 0; y < vertRows; y++)
            {
                Vector2 offset = GetVertexOffset(x, y);
                float xPos = x * mapGenerator.cellSize + offset.x;
                float zPos = y * mapGenerator.cellSize + offset.y;
                int index = x + y * vertCols;
                vertices[index] = new Vector3(xPos, cellHeight * 0.5f, zPos);
                normals[index] = Vector3.up;
                uvs[index] = new Vector2((float)x / width, (float)y / height);
            }
        }

        List<int>[] submeshTriangles = new List<int>[7];
        for (int i = 0; i < submeshTriangles.Length; i++)
            submeshTriangles[i] = new List<int>();

        Vector2Int startCell = mapGenerator.GetStartCell();
        bool[,] interiorHoleMask = MapInteriorHoleUtility.BuildInteriorHoleMask(mapGenerator);
        int[,] nearestPlayableSubmeshMap = GetNearestPlayableSubmeshMap(width, height);
        int playableCellsRendered = 0;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                bool isPlayable = mapGenerator.GetIsPlayable(x, y);
                bool shouldFillHole = fillInteriorHoles && !isPlayable && interiorHoleMask[x, y];

                if (!isPlayable && !shouldFillHole)
                    continue;

                if (isPlayable)
                    playableCellsRendered++;

                bool isStart = isPlayable && x == startCell.x && y == startCell.y;
                int submeshIndex = shouldFillHole ? nearestPlayableSubmeshMap[x, y] : GetSubmeshIndex(x, y, isStart);

                int bottomLeft = x + y * vertCols;
                int bottomRight = (x + 1) + y * vertCols;
                int topLeft = x + (y + 1) * vertCols;
                int topRight = (x + 1) + (y + 1) * vertCols;

                submeshTriangles[submeshIndex].Add(bottomLeft);
                submeshTriangles[submeshIndex].Add(topLeft);
                submeshTriangles[submeshIndex].Add(bottomRight);
                submeshTriangles[submeshIndex].Add(bottomRight);
                submeshTriangles[submeshIndex].Add(topLeft);
                submeshTriangles[submeshIndex].Add(topRight);
            }
        }

        if (playableCellsRendered == 0)
        {
            Debug.LogWarning("MapRenderer: No playable cells were found while building mesh. Check map generation timing/references.");
        }

        Mesh mesh = GetCombinedMapMesh(vertices, normals, uvs, submeshTriangles);
        generatedMesh = mesh;
        MeshFilter meshFilter = mapContainer.AddComponent<MeshFilter>();
        meshFilter.mesh = mesh;

        MeshCollider meshCollider = mapContainer.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = mesh;

        MeshRenderer meshRenderer = mapContainer.AddComponent<MeshRenderer>();
        generatedMaterials = GetMapMaterials();
        meshRenderer.sharedMaterials = generatedMaterials;
    }

    public Vector3 GetCellWorldCenter(int x, int y)
    {
        Vector3 localCenter = GetCellLocalCenter(x, y);

        if (mapContainer != null)
            return mapContainer.transform.TransformPoint(localCenter);

        return transform.TransformPoint(localCenter);
    }

    public Vector3 GetCellWorldCenter(Vector2Int cell)
    {
        return GetCellWorldCenter(cell.x, cell.y);
    }

    public bool TryGetClosestPlayableCell(Vector3 worldPosition, out Vector2Int closestCell)
    {
        closestCell = default;

        if (mapGenerator == null || !mapGenerator.HasGeneratedData)
            return false;

        float bestDistance = float.MaxValue;
        bool found = false;

        for (int x = 0; x < mapGenerator.width; x++)
        {
            for (int y = 0; y < mapGenerator.height; y++)
            {
                if (!mapGenerator.GetIsPlayable(x, y))
                    continue;

                Vector3 cellCenter = GetCellWorldCenter(x, y);
                float distance = (cellCenter - worldPosition).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    closestCell = new Vector2Int(x, y);
                    found = true;
                }
            }
        }

        return found;
    }

    private int GetSubmeshIndex(int x, int y, bool isStart)
    {
        if (isStart)
            return 0;

        return GetBiomeSubmeshIndex(mapGenerator.GetBiomeAt(x, y));
    }

    private int GetBiomeSubmeshIndex(BiomeType biome)
    {
        return biome switch
        {
            BiomeType.Forest => 1,
            BiomeType.Desert => 2,
            BiomeType.Mountain => 3,
            BiomeType.Swamp => 4,
            BiomeType.Plains => 5,
            BiomeType.Jungle => 6,
            _ => 5
        };
    }

    private int[,] GetNearestPlayableSubmeshMap(int width, int height)
    {
        int[,] nearestPlayableSubmeshMap = new int[width, height];
        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                nearestPlayableSubmeshMap[x, y] = -1;

                if (!mapGenerator.GetIsPlayable(x, y))
                    continue;

                nearestPlayableSubmeshMap[x, y] = GetBiomeSubmeshIndex(mapGenerator.GetBiomeAt(x, y));
                queue.Enqueue(new Vector2Int(x, y));
            }
        }

        while (queue.Count > 0)
        {
            Vector2Int cell = queue.Dequeue();
            int submeshIndex = nearestPlayableSubmeshMap[cell.x, cell.y];

            PropagatePlayableSubmesh(cell.x + 1, cell.y, width, height, submeshIndex, nearestPlayableSubmeshMap, queue);
            PropagatePlayableSubmesh(cell.x - 1, cell.y, width, height, submeshIndex, nearestPlayableSubmeshMap, queue);
            PropagatePlayableSubmesh(cell.x, cell.y + 1, width, height, submeshIndex, nearestPlayableSubmeshMap, queue);
            PropagatePlayableSubmesh(cell.x, cell.y - 1, width, height, submeshIndex, nearestPlayableSubmeshMap, queue);
        }

        return nearestPlayableSubmeshMap;
    }

    private void PropagatePlayableSubmesh(int x, int y, int width, int height, int submeshIndex, int[,] nearestPlayableSubmeshMap, Queue<Vector2Int> queue)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
            return;

        if (nearestPlayableSubmeshMap[x, y] != -1)
            return;

        nearestPlayableSubmeshMap[x, y] = submeshIndex;
        queue.Enqueue(new Vector2Int(x, y));
    }

    private Material[] GetMapMaterials()
    {
        return new Material[]
        {
            GetColoredMaterial(mapGenerator.startCellColor),
            GetColoredMaterial(mapGenerator.biomeForest),
            GetColoredMaterial(mapGenerator.biomeDesert),
            GetColoredMaterial(mapGenerator.biomeMountain),
            GetColoredMaterial(mapGenerator.biomeSwamp),
            GetColoredMaterial(mapGenerator.biomePlains),
            GetColoredMaterial(mapGenerator.biomeJungle)
        };
    }

    private Material GetColoredMaterial(Color color)
    {
        Material material = new Material(GetCompatibleShader())
        {
            name = "BiomeMaterial"
        };

        if (planeMaterial != null)
        {
            CopyIfExists(planeMaterial, material, "_MainTex");
            CopyIfExists(planeMaterial, material, "_BaseMap");
        }

        SetMaterialColor(material, color);
        return material;
    }

    private Material GetFallbackMaterial()
    {
        if (sharedFallbackMaterial == null)
        {
            sharedFallbackMaterial = new Material(GetCompatibleShader()) { name = "SharedPlaneFallback" };
        }

        return sharedFallbackMaterial;
    }

    private Shader GetCompatibleShader()
    {
        if (planeMaterial != null && planeMaterial.shader != null)
        {
            string shaderName = planeMaterial.shader.name;
            if (!string.IsNullOrEmpty(shaderName) && !shaderName.Contains("Diffuse"))
                return planeMaterial.shader;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("HDRP/Unlit");
        if (shader == null)
            shader = Shader.Find("HDRP/Lit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Standard");

        return shader;
    }

    private void SetMaterialColor(Material material, Color color)
    {
        color.a = 1f;
        material.color = color;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);

        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 0.5f);
        }
    }

    private static void CopyIfExists(Material source, Material destination, string propertyName)
    {
        if (!source.HasProperty(propertyName) || !destination.HasProperty(propertyName))
            return;

        destination.SetTexture(propertyName, source.GetTexture(propertyName));
    }

    private Color GetBiomeColor(BiomeType biome)
    {
        return biome switch
        {
            BiomeType.Forest => mapGenerator.biomeForest,
            BiomeType.Desert => mapGenerator.biomeDesert,
            BiomeType.Mountain => mapGenerator.biomeMountain,
            BiomeType.Swamp => mapGenerator.biomeSwamp,
            BiomeType.Plains => mapGenerator.biomePlains,
            BiomeType.Jungle => mapGenerator.biomeJungle,
            _ => Color.white
        };
    }

    private Mesh GetCombinedMapMesh(Vector3[] vertices, Vector3[] normals, Vector2[] uvs, List<int>[] submeshTriangles)
    {
        Mesh mesh = new Mesh();
        mesh.name = "CombinedMapMesh";
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.subMeshCount = submeshTriangles.Length;

        for (int i = 0; i < submeshTriangles.Length; i++)
        {
            mesh.SetTriangles(submeshTriangles[i], i);
        }

        mesh.RecalculateBounds();
        return mesh;
    }

    private Vector2 GetCellOffset(int x, int y)
    {
        float nx = (x + mapGenerator.seed * 0.1f) * cellOffsetNoiseScale;
        float ny = (y + mapGenerator.seed * 0.1f) * cellOffsetNoiseScale;
        float offsetX = (Mathf.PerlinNoise(nx, ny) - 0.5f) * cellOffset.x;
        float offsetY = (Mathf.PerlinNoise(nx + 100f, ny + 100f) - 0.5f) * cellOffset.y;
        return new Vector2(offsetX, offsetY);
    }

    private Vector2 GetVertexOffset(int x, int y)
    {
        float nx = (x + mapGenerator.seed * 0.1f) * cellOffsetNoiseScale;
        float ny = (y + mapGenerator.seed * 0.1f) * cellOffsetNoiseScale;
        float offsetX = (Mathf.PerlinNoise(nx, ny) - 0.5f) * cellOffset.x;
        float offsetY = (Mathf.PerlinNoise(nx + 100f, ny + 100f) - 0.5f) * cellOffset.y;
        return new Vector2(offsetX, offsetY);
    }

    private Vector3 GetCellLocalCenter(int x, int y)
    {
        Vector2 offset00 = GetVertexOffset(x, y);
        Vector2 offset10 = GetVertexOffset(x + 1, y);
        Vector2 offset01 = GetVertexOffset(x, y + 1);
        Vector2 offset11 = GetVertexOffset(x + 1, y + 1);

        Vector3 corner00 = new Vector3(x * mapGenerator.cellSize + offset00.x, cellHeight * 0.5f, y * mapGenerator.cellSize + offset00.y);
        Vector3 corner10 = new Vector3((x + 1) * mapGenerator.cellSize + offset10.x, cellHeight * 0.5f, y * mapGenerator.cellSize + offset10.y);
        Vector3 corner01 = new Vector3(x * mapGenerator.cellSize + offset01.x, cellHeight * 0.5f, (y + 1) * mapGenerator.cellSize + offset01.y);
        Vector3 corner11 = new Vector3((x + 1) * mapGenerator.cellSize + offset11.x, cellHeight * 0.5f, (y + 1) * mapGenerator.cellSize + offset11.y);

        return (corner00 + corner10 + corner01 + corner11) * 0.25f;
    }

    private static Mesh GetPlaneMesh()
    {
        if (sharedPlaneMesh != null)
            return sharedPlaneMesh;

        Mesh mesh = new Mesh();
        mesh.name = "PlaneMesh";

        Vector3[] vertices = new Vector3[]
        {
            new Vector3(-0.5f, 0, -0.5f),
            new Vector3(0.5f, 0, -0.5f),
            new Vector3(-0.5f, 0, 0.5f),
            new Vector3(0.5f, 0, 0.5f)
        };

        int[] triangles = new int[]
        {
            0, 2, 1,
            1, 2, 3
        };

        Vector2[] uv = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(0, 1),
            new Vector2(1, 1)
        };

        Vector3[] normals = new Vector3[]
        {
            Vector3.up,
            Vector3.up,
            Vector3.up,
            Vector3.up
        };

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uv;
        mesh.normals = normals;
        mesh.RecalculateBounds();

        sharedPlaneMesh = mesh;
        return sharedPlaneMesh;
    }

    private Material GetSharedMaterial()
    {
        if (planeMaterial != null)
            return planeMaterial;

        if (sharedFallbackMaterial == null)
            sharedFallbackMaterial = new Material(Shader.Find("Standard")) { name = "SharedPlaneFallback" };

        return sharedFallbackMaterial;
    }
}
