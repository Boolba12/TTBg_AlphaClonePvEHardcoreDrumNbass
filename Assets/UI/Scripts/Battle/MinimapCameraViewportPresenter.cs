using UnityEngine;

public sealed class MinimapCameraViewportPresenter : MonoBehaviour
{
    [SerializeField] private MinimapViewportGraphic viewportGraphic;
    private readonly Vector3[] worldFootprint = new Vector3[4];
    private readonly Vector2[] normalizedFootprint = new Vector2[4];
    private TacticalCameraController cameraController;
    private MinimapCoordinateMapper mapper;

    public int RefreshCount { get; private set; }

    public void Configure(MinimapViewportGraphic graphic) => viewportGraphic = graphic;

    public bool Bind(TacticalCameraController camera, MinimapCoordinateMapper coordinateMapper)
    {
        Unbind();
        if (camera == null || coordinateMapper == null || viewportGraphic == null)
            return false;
        cameraController = camera;
        mapper = coordinateMapper;
        cameraController.ViewportChanged += Refresh;
        Refresh();
        return true;
    }

    public void Refresh()
    {
        if (cameraController == null || mapper == null ||
            !cameraController.TryGetFootprint(worldFootprint))
        {
            viewportGraphic?.SetFootprint(null);
            return;
        }
        for (int i = 0; i < worldFootprint.Length; i++)
            normalizedFootprint[i] = mapper.WorldToNormalized(worldFootprint[i]);
        viewportGraphic.SetFootprint(normalizedFootprint);
        RefreshCount++;
    }

    private void OnDestroy() => Unbind();

    public void Unbind()
    {
        if (cameraController != null)
            cameraController.ViewportChanged -= Refresh;
        cameraController = null;
        mapper = null;
    }
}
