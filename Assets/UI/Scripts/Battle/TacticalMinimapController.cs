using System.Collections;
using UnityEngine;

/// <summary>
/// Production composition root for the minimap. It owns presentation bindings only;
/// gameplay state remains in the generated map, squad controllers and grid anchors.
/// </summary>
public sealed class TacticalMinimapController : MonoBehaviour
{
    [SerializeField] private MapGenerator mapGenerator;
    [SerializeField] private MapRenderer mapRenderer;
    [SerializeField] private SquadBattleBootstrap squadBootstrap;
    [SerializeField] private TacticalCameraController cameraController;
    [SerializeField] private MinimapGridPresenter gridPresenter;
    [SerializeField] private MinimapSquadMarkerPresenter markerPresenter;
    [SerializeField] private MinimapCameraViewportPresenter viewportPresenter;
    [SerializeField] private MinimapInteractionController interactionController;
    [SerializeField] private MinimapCollapseController collapseController;

    public bool IsInitialized { get; private set; }
    public string FailureReason { get; private set; }
    public int SuccessfulInitializationCount { get; private set; }
    public MinimapCoordinateMapper Mapper { get; private set; }
    public MinimapGridPresenter GridPresenter => gridPresenter;
    public MinimapSquadMarkerPresenter MarkerPresenter => markerPresenter;
    public MinimapCameraViewportPresenter ViewportPresenter => viewportPresenter;
    public MinimapInteractionController InteractionController => interactionController;
    public MinimapCollapseController CollapseController => collapseController;

    public void Configure(
        MapGenerator generator,
        MapRenderer renderer,
        SquadBattleBootstrap bootstrap,
        TacticalCameraController tacticalCamera,
        MinimapGridPresenter grid,
        MinimapSquadMarkerPresenter markers,
        MinimapCameraViewportPresenter viewport,
        MinimapInteractionController interaction,
        MinimapCollapseController collapse)
    {
        mapGenerator = generator;
        mapRenderer = renderer;
        squadBootstrap = bootstrap;
        cameraController = tacticalCamera;
        gridPresenter = grid;
        markerPresenter = markers;
        viewportPresenter = viewport;
        interactionController = interaction;
        collapseController = collapse;
        IsInitialized = false;
        FailureReason = null;
    }

    private IEnumerator Start()
    {
        if (mapGenerator == null || mapRenderer == null || squadBootstrap == null)
        {
            Fail("Map or squad bootstrap references are missing.");
            yield break;
        }

        while (!mapGenerator.HasGeneratedData ||
               squadBootstrap.State == SquadBootstrapState.NotInitialized ||
               squadBootstrap.State == SquadBootstrapState.Initializing)
        {
            yield return null;
        }

        if (squadBootstrap.State != SquadBootstrapState.Initialized)
        {
            Fail($"Squad bootstrap failed: {squadBootstrap.FailureReason}");
            yield break;
        }
        TryInitialize();
    }

    public bool TryInitialize()
    {
        if (IsInitialized)
            return true;
        if (mapGenerator == null || mapRenderer == null || squadBootstrap == null ||
            !mapGenerator.HasGeneratedData || !squadBootstrap.HasBootstrapped ||
            cameraController == null || gridPresenter == null || markerPresenter == null ||
            viewportPresenter == null || interactionController == null ||
            collapseController == null)
        {
            return Fail("Required minimap sources or presenters are unavailable.");
        }
        if (!mapRenderer.TryGetGeneratedWorldBounds(out Bounds mapBounds, false))
            return Fail("Generated map world bounds are unavailable.");
        if (!cameraController.Initialize())
            return Fail("Tactical camera could not initialize from the generated map.");

        Mapper = new MinimapCoordinateMapper(
            mapGenerator.Width,
            mapGenerator.Height,
            mapBounds,
            mapGenerator.GetIsPlayable,
            mapRenderer.GetCellWorldCenter);
        if (!gridPresenter.Build(mapGenerator, Mapper))
            return Fail("Static minimap grid could not be built.");
        if (!markerPresenter.Bind(squadBootstrap.SpawnedControllers, Mapper))
            return Fail("Squad minimap markers could not bind.");
        if (!viewportPresenter.Bind(cameraController, Mapper))
            return Fail("Camera viewport overlay could not bind.");

        interactionController.Configure(
            interactionController.transform as RectTransform,
            cameraController,
            Mapper,
            collapseController);
        collapseController.SetImmediate(MinimapCollapseState.Expanded);
        IsInitialized = true;
        SuccessfulInitializationCount++;
        Debug.Log(
            $"TacticalMinimapController: initialized {mapGenerator.Width}x{mapGenerator.Height}; " +
            $"potential={mapGenerator.PotentialCellCount}, playable={mapGenerator.PlayableCellCount}, " +
            $"staticBuildMs={gridPresenter.LastBuildMilliseconds:F2}.",
            this);
        return true;
    }

    private bool Fail(string reason)
    {
        FailureReason = reason;
        Debug.LogError($"TacticalMinimapController: {reason}", this);
        return false;
    }
}
