using UnityEngine;
using UnityEngine.UI;

public sealed class MinimapViewportGraphic : MaskableGraphic
{
    [SerializeField, Min(1f)] private float lineWidth = 2f;
    private readonly Vector2[] normalizedPoints = new Vector2[4];
    private bool hasFootprint;

    public int PointCount => hasFootprint ? 4 : 0;
    public Vector2 GetNormalizedPoint(int index) => normalizedPoints[index];

    public void SetFootprint(Vector2[] points)
    {
        hasFootprint = points != null && points.Length >= 4;
        if (hasFootprint)
        {
            for (int i = 0; i < 4; i++)
                normalizedPoints[i] = points[i];
        }
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        if (!hasFootprint)
            return;
        Rect rect = rectTransform.rect;
        for (int i = 0; i < 4; i++)
        {
            Vector2 a = NormalizedToLocal(rect, normalizedPoints[i]);
            Vector2 b = NormalizedToLocal(rect, normalizedPoints[(i + 1) % 4]);
            AddLine(vertexHelper, a, b, lineWidth, color);
        }
    }

    private static Vector2 NormalizedToLocal(Rect rect, Vector2 value) => new Vector2(
        Mathf.LerpUnclamped(rect.xMin, rect.xMax, value.x),
        Mathf.LerpUnclamped(rect.yMin, rect.yMax, value.y));

    private static void AddLine(
        VertexHelper helper,
        Vector2 from,
        Vector2 to,
        float width,
        Color32 lineColor)
    {
        Vector2 direction = to - from;
        if (direction.sqrMagnitude <= 0.0001f)
            return;
        Vector2 normal = new Vector2(-direction.y, direction.x).normalized * width * 0.5f;
        int start = helper.currentVertCount;
        helper.AddVert(from - normal, lineColor, Vector2.zero);
        helper.AddVert(from + normal, lineColor, Vector2.up);
        helper.AddVert(to + normal, lineColor, Vector2.one);
        helper.AddVert(to - normal, lineColor, Vector2.right);
        helper.AddTriangle(start, start + 1, start + 2);
        helper.AddTriangle(start + 2, start + 3, start);
    }
}
