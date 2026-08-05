using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class BattleActionControlView : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    [SerializeField] private PurgatoryUITheme theme;
    [SerializeField] private Button button;
    [SerializeField] private Image background;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text label;
    [SerializeField] private TMP_Text hotkeyLabel;
    [SerializeField] private TMP_Text costLabel;
    [SerializeField] private TMP_Text stateLabel;

    private Coroutine transitionRoutine;
    private Vector3 restingScale = Vector3.one;

    public Button Button => button;
    public Sprite DisplayedIcon => icon != null ? icon.sprite : null;
    public bool IsSelectedAction { get; private set; }
    public string UnavailableReason { get; private set; }

    public void Configure(
        PurgatoryUITheme configuredTheme,
        Button configuredButton,
        Image backgroundImage,
        Image iconImage,
        TMP_Text actionLabel,
        TMP_Text configuredHotkeyLabel,
        TMP_Text configuredCostLabel,
        TMP_Text configuredStateLabel)
    {
        theme = configuredTheme;
        button = configuredButton;
        background = backgroundImage;
        icon = iconImage;
        label = actionLabel;
        hotkeyLabel = configuredHotkeyLabel;
        costLabel = configuredCostLabel;
        stateLabel = configuredStateLabel;
        restingScale = transform.localScale;
        ApplyTheme();
    }

    public void RenderPlaceholder(
        string actionName,
        string hotkey,
        string cost,
        Sprite actionIcon = null)
    {
        RenderCommand(
            actionName,
            hotkey ?? "—",
            cost ?? "AP —",
            false,
            false,
            theme?.UnavailableLabel ?? "Unavailable in this build",
            actionIcon);
    }

    public void RenderCommand(
        string actionName,
        string hotkey,
        string cost,
        bool interactable,
        bool selected,
        string unavailableReason,
        Sprite actionIcon = null)
    {
        if (label != null)
            label.text = actionName ?? string.Empty;
        if (hotkeyLabel != null)
            hotkeyLabel.text = hotkey ?? "—";
        if (costLabel != null)
            costLabel.text = cost ?? "AP —";
        if (icon != null)
        {
            icon.sprite = actionIcon != null ? actionIcon : theme?.IconPlaceholderSprite;
            icon.enabled = icon.sprite != null;
            icon.preserveAspect = true;
        }
        SetCommandState(interactable, selected, unavailableReason, unavailableReason);
    }

    public void SetInteractable(bool interactable)
    {
        if (button != null)
            button.interactable = interactable;
    }

    public void SetCommandState(
        bool interactable,
        bool selected,
        string state,
        string unavailableReason = null)
    {
        IsSelectedAction = selected;
        UnavailableReason = unavailableReason;
        if (button != null)
            button.interactable = interactable;
        if (stateLabel != null)
            stateLabel.text = state ?? string.Empty;

        TooltipAnchor tooltipAnchor = GetComponent<TooltipAnchor>();
        if (tooltipAnchor?.Tooltip != null)
        {
            tooltipAnchor.Tooltip.title = label != null ? label.text : string.Empty;
            tooltipAnchor.Tooltip.body = string.IsNullOrWhiteSpace(unavailableReason)
                ? state ?? string.Empty
                : unavailableReason;
        }

        if (background != null && theme != null)
        {
            background.sprite = selected ? theme.SelectedFrameSprite : theme.ButtonSprite;
            background.color = Color.white;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (button != null && button.interactable)
            AnimateScale(1.012f, 0.1f);
    }

    public void OnPointerExit(PointerEventData eventData) => AnimateScale(1f, 0.1f);

    public void OnPointerDown(PointerEventData eventData)
    {
        if (button != null && button.interactable)
            AnimateScale(0.985f, 0.08f);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        AnimateScale(button != null && button.interactable ? 1.012f : 1f, 0.08f);
    }

    private void ApplyTheme()
    {
        if (theme == null)
            return;

        if (background != null)
        {
            background.sprite = theme.ButtonSprite;
            background.type = background.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            background.color = Color.white;
        }

        if (button != null)
        {
            button.targetGraphic = background;
            button.transition = Selectable.Transition.SpriteSwap;
            SpriteState sprites = button.spriteState;
            sprites.highlightedSprite = theme.ButtonHoverSprite;
            sprites.pressedSprite = theme.ButtonPressedSprite;
            sprites.selectedSprite = theme.SelectedFrameSprite;
            sprites.disabledSprite = theme.ButtonDisabledSprite;
            button.spriteState = sprites;
        }

        ApplyText(label, theme.TextPrimary, theme.CaptionSize);
        ApplyText(hotkeyLabel, theme.TextSecondary, theme.CaptionSize);
        ApplyText(costLabel, theme.Gold, theme.CaptionSize);
        ApplyText(stateLabel, theme.TextSecondary, Mathf.Max(12f, theme.CaptionSize - 4f));
        if (icon != null)
            icon.color = theme.TextPrimary;
    }

    private void ApplyText(TMP_Text target, Color color, float size)
    {
        if (target == null)
            return;
        target.font = theme.PrimaryFont;
        target.fontSize = size;
        target.color = color;
    }

    private void AnimateScale(float multiplier, float duration)
    {
        if (!Application.isPlaying)
        {
            transform.localScale = restingScale * multiplier;
            return;
        }
        if (transitionRoutine != null)
            StopCoroutine(transitionRoutine);
        transitionRoutine = StartCoroutine(ScaleRoutine(restingScale * multiplier, duration));
    }

    private IEnumerator ScaleRoutine(Vector3 target, float duration)
    {
        Vector3 start = transform.localScale;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            transform.localScale = Vector3.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        transform.localScale = target;
        transitionRoutine = null;
    }

    private void OnDisable()
    {
        if (transitionRoutine != null)
            StopCoroutine(transitionRoutine);
        transitionRoutine = null;
        transform.localScale = restingScale;
    }
}
