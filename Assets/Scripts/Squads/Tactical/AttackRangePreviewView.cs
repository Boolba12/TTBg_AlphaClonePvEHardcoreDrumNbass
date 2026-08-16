using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reuses one procedural mesh for all in-range cells and one LineRenderer for
/// LOS. Nothing is instantiated per cell or per hover.
/// </summary>
public sealed class AttackRangePreviewView : MonoBehaviour
{
    [SerializeField] private MapGenerator mapGenerator;
    [SerializeField] private MapRenderer mapRenderer;
    [SerializeField] private MeshFilter rangeMeshFilter;
    [SerializeField] private MeshRenderer rangeMeshRenderer;
    [SerializeField] private LineRenderer lineOfSightLine;
    [SerializeField] private Color rangeColor = new Color(0.20f, 0.62f, 0.43f, 0.20f);
    [SerializeField] private Color clearLineColor = new Color(0.20f, 0.72f, 0.48f, 0.9f);
    [SerializeField] private Color blockedLineColor = new Color(0.70f, 0.25f, 0.22f, 0.9f);
    [SerializeField, Min(0f)] private float heightOffset = 0.045f;

    private Mesh runtimeMesh;
    private MaterialPropertyBlock materialProperties;

    public int RangeCellCount { get; private set; }
    public bool HasLinePreview => lineOfSightLine != null && lineOfSightLine.enabled;
    public LineOfSightStatus LastLineOfSightStatus { get; private set; } =
        LineOfSightStatus.NotRequired;

    public void Configure(MapGenerator generator, MapRenderer renderer,
        MeshFilter meshFilter, MeshRenderer meshRenderer, LineRenderer losLine)
    {
        mapGenerator = generator;
        mapRenderer = renderer;
        rangeMeshFilter = meshFilter;
        rangeMeshRenderer = meshRenderer;
        lineOfSightLine = losLine;
        EnsureMesh();
        Clear();
    }

    public void ShowRange(Vector2Int attackerCell, AttackDefinition definition,
        bool allowDiagonal)
    {
        EnsureMesh();
        runtimeMesh.Clear();
        RangeCellCount = 0;
        if (definition == null || mapGenerator == null || mapRenderer == null ||
            !mapGenerator.HasGeneratedData)
        {
            if (rangeMeshRenderer != null)
                rangeMeshRenderer.enabled = false;
            return;
        }

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        float half = Mathf.Max(0.01f, mapGenerator.cellSize * 0.42f);
        for (int x = 0; x < mapGenerator.Width; x++)
        {
            for (int y = 0; y < mapGenerator.Height; y++)
            {
                if (!mapGenerator.GetIsPlayable(x, y))
                    continue;
                Vector2Int cell = new Vector2Int(x, y);
                int distance = BattleTargetingService.GetGridDistance(
                    attackerCell, cell, allowDiagonal);
                if (distance < definition.MinimumRange ||
                    distance > definition.MaximumRange)
                {
                    continue;
                }

                Vector3 center = mapRenderer.GetCellWorldCenter(cell);
                center.y += heightOffset;
                int index = vertices.Count;
                vertices.Add(center + new Vector3(-half, 0f, -half));
                vertices.Add(center + new Vector3(half, 0f, -half));
                vertices.Add(center + new Vector3(half, 0f, half));
                vertices.Add(center + new Vector3(-half, 0f, half));
                triangles.Add(index);
                triangles.Add(index + 2);
                triangles.Add(index + 1);
                triangles.Add(index);
                triangles.Add(index + 3);
                triangles.Add(index + 2);
                RangeCellCount++;
            }
        }

        runtimeMesh.SetVertices(vertices);
        runtimeMesh.SetTriangles(triangles, 0);
        runtimeMesh.RecalculateBounds();
        if (rangeMeshRenderer != null)
        {
            rangeMeshRenderer.enabled = RangeCellCount > 0;
            materialProperties ??= new MaterialPropertyBlock();
            materialProperties.SetColor("_Color", rangeColor);
            rangeMeshRenderer.SetPropertyBlock(materialProperties);
        }
    }

    public void ShowLine(LineOfSightResult result)
    {
        LastLineOfSightStatus = result.Status;
        if (lineOfSightLine == null || mapRenderer == null ||
            result.TraversedCells == null || result.TraversedCells.Count < 2)
        {
            if (lineOfSightLine != null)
                lineOfSightLine.enabled = false;
            return;
        }

        Vector2Int start = result.TraversedCells[0];
        Vector2Int target = result.TraversedCells[result.TraversedCells.Count - 1];
        bool blocked = result.Status == LineOfSightStatus.Blocked &&
                       result.BlockingCell.HasValue;
        lineOfSightLine.positionCount = blocked ? 3 : 2;
        lineOfSightLine.SetPosition(0, RaisedCenter(start));
        if (blocked)
        {
            lineOfSightLine.SetPosition(1, RaisedCenter(result.BlockingCell.Value));
            lineOfSightLine.SetPosition(2, RaisedCenter(target));
        }
        else
        {
            lineOfSightLine.SetPosition(1, RaisedCenter(target));
        }
        Color color = blocked ? blockedLineColor : clearLineColor;
        lineOfSightLine.startColor = color;
        lineOfSightLine.endColor = color;
        lineOfSightLine.enabled = true;
    }

    public void ClearLine()
    {
        LastLineOfSightStatus = LineOfSightStatus.NotRequired;
        if (lineOfSightLine != null)
        {
            lineOfSightLine.enabled = false;
            lineOfSightLine.positionCount = 0;
        }
    }

    public void Clear()
    {
        RangeCellCount = 0;
        if (runtimeMesh != null)
            runtimeMesh.Clear();
        if (rangeMeshRenderer != null)
            rangeMeshRenderer.enabled = false;
        ClearLine();
    }

    private Vector3 RaisedCenter(Vector2Int cell)
    {
        Vector3 center = mapRenderer.GetCellWorldCenter(cell);
        center.y += heightOffset * 2f;
        return center;
    }

    private void EnsureMesh()
    {
        if (runtimeMesh == null)
        {
            runtimeMesh = new Mesh { name = "Runtime_AttackRangePreview" };
            runtimeMesh.MarkDynamic();
        }
        if (rangeMeshFilter != null)
            rangeMeshFilter.sharedMesh = runtimeMesh;
    }

    private void OnDisable() => Clear();

    private void OnDestroy()
    {
        if (runtimeMesh == null)
            return;
        if (Application.isPlaying)
            Destroy(runtimeMesh);
        else
            DestroyImmediate(runtimeMesh);
    }
}
