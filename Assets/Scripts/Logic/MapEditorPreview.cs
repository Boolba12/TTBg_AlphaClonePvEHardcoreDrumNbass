using UnityEngine;

[ExecuteAlways]
public class MapEditorPreview : MonoBehaviour
{
    public MapGenerator mapGenerator;
    public MapRenderer mapRenderer;
    public bool autoPreview = true;

    private bool pendingRefresh;

    private void OnEnable()
    {
        if (!Application.isPlaying && autoPreview)
            pendingRefresh = true;
    }

    private void OnValidate()
    {
        if (!Application.isPlaying && autoPreview)
            pendingRefresh = true;
    }

    private void Update()
    {
        if (Application.isPlaying)
            return;

        if (!autoPreview || !pendingRefresh)
            return;

        if (mapGenerator == null || mapRenderer == null)
            return;

        pendingRefresh = false;
        mapGenerator.Generate();
        mapRenderer.RenderMap();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying && mapRenderer != null)
            mapRenderer.ClearGeneratedMap();
    }

    [ContextMenu("Refresh Preview")]
    public void RefreshPreview()
    {
        if (mapGenerator == null || mapRenderer == null)
            return;

        mapGenerator.Generate();
        mapRenderer.RenderMap();
    }
}
