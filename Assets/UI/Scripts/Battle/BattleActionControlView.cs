using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BattleActionControlView : MonoBehaviour
{
    [SerializeField] private PurgatoryUITheme theme;
    [SerializeField] private Button button;
    [SerializeField] private Image background;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text label;
    [SerializeField] private TMP_Text hotkeyLabel;
    [SerializeField] private TMP_Text costLabel;
    [SerializeField] private TMP_Text stateLabel;

    public Button Button => button;
    public Sprite DisplayedIcon => icon != null ? icon.sprite : null;

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
        ApplyTheme();
    }

    public void RenderPlaceholder(
        string actionName,
        string hotkey,
        string cost,
        Sprite actionIcon = null)
    {
        if (label != null)
            label.text = actionName ?? string.Empty;
        if (hotkeyLabel != null)
            hotkeyLabel.text = hotkey ?? "—";
        if (costLabel != null)
            costLabel.text = cost ?? "AP —";
        if (stateLabel != null)
            stateLabel.text = theme?.UnavailableLabel ?? "Unavailable in this build";
        if (icon != null)
        {
            icon.sprite = actionIcon != null ? actionIcon : theme?.IconPlaceholderSprite;
            icon.enabled = icon.sprite != null;
            icon.preserveAspect = true;
        }
        if (button != null)
            button.interactable = false;
    }

    public void SetInteractable(bool interactable)
    {
        if (button != null)
            button.interactable = interactable;
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
}
