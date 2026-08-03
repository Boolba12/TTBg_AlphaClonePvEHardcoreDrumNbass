using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class TooltipController : MonoBehaviour
{
    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform tooltipPanel;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text bodyLabel;
    [SerializeField] private TMP_Text valuesLabel;
    [SerializeField] private Image icon;
    [SerializeField, Min(0f)] private float showDelay = 0.35f;
    [SerializeField] private Vector2 cursorOffset = new Vector2(18f, -18f);

    private readonly StringBuilder valueBuilder = new StringBuilder(128);
    private Coroutine pendingShow;
    private ITooltipSource pendingSource;
    private ITooltipSource visibleSource;
    private Vector2 pointerPosition;

    public bool IsVisible => canvasGroup != null && canvasGroup.alpha > 0f;

    private void Awake()
    {
        HideImmediate();
    }

    private void OnDisable()
    {
        CancelPending();
        HideImmediate();
    }

    public void Configure(
        Canvas configuredCanvas,
        RectTransform panel,
        CanvasGroup group,
        TMP_Text title,
        TMP_Text body,
        TMP_Text values,
        Image iconImage)
    {
        canvas = configuredCanvas;
        tooltipPanel = panel;
        canvasGroup = group;
        titleLabel = title;
        bodyLabel = body;
        valuesLabel = values;
        icon = iconImage;
        HideImmediate();
    }

    public void RequestShow(ITooltipSource source, Vector2 screenPosition)
    {
        if (source?.Tooltip == null)
            return;

        pointerPosition = screenPosition;
        if (ReferenceEquals(visibleSource, source))
        {
            PositionAt(pointerPosition);
            return;
        }

        CancelPending();
        pendingSource = source;
        pendingShow = StartCoroutine(ShowAfterDelay());
    }

    public void UpdatePointer(ITooltipSource source, Vector2 screenPosition)
    {
        if (!ReferenceEquals(source, pendingSource) && !ReferenceEquals(source, visibleSource))
            return;
        pointerPosition = screenPosition;
        if (ReferenceEquals(source, visibleSource))
            PositionAt(pointerPosition);
    }

    public void Hide(ITooltipSource source)
    {
        if (!ReferenceEquals(source, pendingSource) && !ReferenceEquals(source, visibleSource))
            return;
        CancelPending();
        HideImmediate();
    }

    private IEnumerator ShowAfterDelay()
    {
        if (showDelay > 0f)
            yield return new WaitForSecondsRealtime(showDelay);

        ITooltipSource source = pendingSource;
        pendingShow = null;
        pendingSource = null;
        if (source?.Tooltip == null)
            yield break;

        visibleSource = source;
        Render(source.Tooltip);
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        PositionAt(pointerPosition);
    }

    private void Render(TooltipContent content)
    {
        if (titleLabel != null)
            titleLabel.text = content.title ?? string.Empty;
        if (bodyLabel != null)
            bodyLabel.text = content.body ?? string.Empty;
        if (icon != null)
        {
            icon.sprite = content.icon;
            icon.enabled = content.icon != null;
        }

        valueBuilder.Clear();
        if (content.values != null)
        {
            foreach (TooltipValueLine line in content.values)
            {
                if (line == null)
                    continue;
                if (valueBuilder.Length > 0)
                    valueBuilder.AppendLine();
                valueBuilder.Append(line.label ?? string.Empty);
                if (!string.IsNullOrWhiteSpace(line.value))
                    valueBuilder.Append(": ").Append(line.value);
            }
        }
        if (valuesLabel != null)
            valuesLabel.text = valueBuilder.ToString();
    }

    private void PositionAt(Vector2 screenPosition)
    {
        if (canvas == null || tooltipPanel == null)
            return;

        RectTransform canvasRect = canvas.transform as RectTransform;
        if (canvasRect == null)
            return;

        Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : canvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPosition,
                eventCamera,
                out Vector2 localPoint))
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        Vector2 position = localPoint + cursorOffset;
        Rect bounds = canvasRect.rect;
        Rect panelBounds = tooltipPanel.rect;
        Vector2 pivot = tooltipPanel.pivot;
        float minX = bounds.xMin + panelBounds.width * pivot.x;
        float maxX = bounds.xMax - panelBounds.width * (1f - pivot.x);
        float minY = bounds.yMin + panelBounds.height * pivot.y;
        float maxY = bounds.yMax - panelBounds.height * (1f - pivot.y);
        position.x = Mathf.Clamp(position.x, minX, maxX);
        position.y = Mathf.Clamp(position.y, minY, maxY);
        tooltipPanel.anchoredPosition = position;
    }

    private void CancelPending()
    {
        if (pendingShow != null)
            StopCoroutine(pendingShow);
        pendingShow = null;
        pendingSource = null;
    }

    private void HideImmediate()
    {
        visibleSource = null;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }
}
