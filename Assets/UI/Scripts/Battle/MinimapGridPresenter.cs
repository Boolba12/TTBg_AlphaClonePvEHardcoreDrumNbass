using UnityEngine;
using UnityEngine.UI;

public sealed class MinimapGridPresenter : MonoBehaviour
{
    [SerializeField] private MinimapGridGraphic gridGraphic;
    [SerializeField] private AspectRatioFitter aspectRatioFitter;
    [SerializeField] private PurgatoryUITheme theme;

    public bool IsBuilt { get; private set; }
    public double LastBuildMilliseconds { get; private set; }
    public MinimapGridGraphic GridGraphic => gridGraphic;

    public void Configure(
        MinimapGridGraphic graphic,
        AspectRatioFitter fitter,
        PurgatoryUITheme configuredTheme)
    {
        gridGraphic = graphic;
        aspectRatioFitter = fitter;
        theme = configuredTheme;
    }

    public bool Build(MapGenerator generator, MinimapCoordinateMapper mapper)
    {
        if (IsBuilt || generator == null || mapper == null || gridGraphic == null)
            return false;
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        if (aspectRatioFitter != null)
        {
            aspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspectRatioFitter.aspectRatio = mapper.MapAspect;
        }
        gridGraphic.Configure(generator, theme);
        Canvas.ForceUpdateCanvases();
        gridGraphic.SetAllDirty();
        IsBuilt = true;
        LastBuildMilliseconds =
            (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000d /
            System.Diagnostics.Stopwatch.Frequency;
        return true;
    }
}
