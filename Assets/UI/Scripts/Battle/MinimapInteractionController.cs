using UnityEngine;
using UnityEngine.EventSystems;

public sealed class MinimapInteractionController : MonoBehaviour,
    IPointerEnterHandler,
    IPointerClickHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler,
    IScrollHandler
{
    [SerializeField] private RectTransform interactionRect;
    [SerializeField] private MinimapCollapseController collapseController;
    [SerializeField, Min(0.05f)] private float scrollStep = 1f;

    private TacticalCameraController cameraController;
    private MinimapCoordinateMapper mapper;
    private Camera uiCamera;

    public int AcceptedFocusCount { get; private set; }
    public int RejectedFocusCount { get; private set; }
    public int DragCount { get; private set; }
    public int ScrollCount { get; private set; }

    public void Configure(
        RectTransform rect,
        TacticalCameraController camera,
        MinimapCoordinateMapper coordinateMapper,
        MinimapCollapseController collapse,
        Camera eventCamera = null)
    {
        interactionRect = rect;
        cameraController = camera;
        mapper = coordinateMapper;
        collapseController = collapse;
        uiCamera = eventCamera;
    }

    public void OnPointerEnter(PointerEventData eventData) =>
        collapseController?.RegisterInteraction();

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;
        TryFocusScreenPoint(eventData.position, eventData.pressEventCamera ?? uiCamera);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        collapseController?.RegisterInteraction();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;
        if (TryFocusScreenPoint(eventData.position, eventData.pressEventCamera ?? uiCamera))
            DragCount++;
    }

    public void OnEndDrag(PointerEventData eventData) =>
        collapseController?.RegisterInteraction();

    public void OnScroll(PointerEventData eventData)
    {
        collapseController?.RegisterInteraction();
        if (cameraController != null &&
            cameraController.ZoomBy(Mathf.Sign(eventData.scrollDelta.y) * scrollStep))
        {
            ScrollCount++;
        }
    }

    public bool TryFocusNormalized(Vector2 normalized)
    {
        collapseController?.RegisterInteraction();
        if (cameraController == null || mapper == null ||
            !mapper.TryNormalizedToGrid(normalized, out Vector2Int cell, true) ||
            !cameraController.FocusGrid(cell))
        {
            RejectedFocusCount++;
            return false;
        }
        AcceptedFocusCount++;
        return true;
    }

    private bool TryFocusScreenPoint(Vector2 screenPoint, Camera eventCamera)
    {
        if (interactionRect == null ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                interactionRect,
                screenPoint,
                eventCamera,
                out Vector2 local))
        {
            RejectedFocusCount++;
            return false;
        }
        Rect rect = interactionRect.rect;
        Vector2 normalized = new Vector2(
            Mathf.InverseLerp(rect.xMin, rect.xMax, local.x),
            Mathf.InverseLerp(rect.yMin, rect.yMax, local.y));
        return TryFocusNormalized(normalized);
    }
}
