using UnityEngine;

public sealed class SquadPathPreviewView : MonoBehaviour
{
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private Color reachableColor = new Color32(45, 183, 122, 235);
    [SerializeField] private Color invalidColor = new Color32(178, 63, 57, 235);
    [SerializeField, Min(0.01f)] private float lineWidth = 0.065f;
    [SerializeField] private float heightOffset = 0.1f;

    private Material runtimeMaterial;

    public SquadMovementPlan DisplayedPlan { get; private set; }
    public bool IsVisible => lineRenderer != null && lineRenderer.positionCount > 1;

    public void Configure(LineRenderer configuredLineRenderer)
    {
        lineRenderer = configuredLineRenderer;
        EnsureRenderer();
        Clear();
    }

    public void Render(SquadMovementPlan plan, MapRenderer mapRenderer)
    {
        EnsureRenderer();
        DisplayedPlan = plan;
        if (lineRenderer == null || mapRenderer == null || plan?.Path == null || plan.Path.Count <= 1)
        {
            Clear();
            return;
        }

        Color color = plan.IsValid ? reachableColor : invalidColor;
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
        lineRenderer.positionCount = plan.Path.Count;
        for (int i = 0; i < plan.Path.Count; i++)
        {
            Vector3 position = mapRenderer.GetCellWorldCenter(plan.Path[i]);
            position.y += heightOffset;
            lineRenderer.SetPosition(i, position);
        }
    }

    public void Clear()
    {
        DisplayedPlan = null;
        if (lineRenderer != null)
            lineRenderer.positionCount = 0;
    }

    private void EnsureRenderer()
    {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
            return;

        Shader shader = Shader.Find("Sprites/Default");
        if (runtimeMaterial == null && shader != null)
        {
            runtimeMaterial = new Material(shader) { name = "Runtime_SquadPathPreview" };
            lineRenderer.sharedMaterial = runtimeMaterial;
        }
        lineRenderer.useWorldSpace = true;
        lineRenderer.loop = false;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
    }

    private void OnDestroy()
    {
        if (runtimeMaterial == null)
            return;
        if (Application.isPlaying)
            Destroy(runtimeMaterial);
        else
            DestroyImmediate(runtimeMaterial);
    }
}
