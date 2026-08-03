using UnityEngine;
using UnityEngine.EventSystems;

public sealed class TooltipAnchor : MonoBehaviour,
    ITooltipSource,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerMoveHandler
{
    [SerializeField] private TooltipController controller;
    [SerializeField] private TooltipContent tooltip = new TooltipContent();

    public TooltipContent Tooltip => tooltip;

    public void Configure(TooltipController configuredController, TooltipContent content)
    {
        controller = configuredController;
        tooltip = content ?? new TooltipContent();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        controller?.RequestShow(this, eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        controller?.Hide(this);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        controller?.UpdatePointer(this, eventData.position);
    }

    private void OnDisable()
    {
        controller?.Hide(this);
    }
}
