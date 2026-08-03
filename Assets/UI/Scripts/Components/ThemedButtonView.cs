using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum ThemedButtonStyle
{
    Primary,
    Secondary,
    Icon
}

[RequireComponent(typeof(Button))]
public sealed class ThemedButtonView : MonoBehaviour
{
    [SerializeField] private PurgatoryUITheme theme;
    [SerializeField] private ThemedButtonStyle style;
    [SerializeField] private Button button;
    [SerializeField] private Image background;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Image icon;

    public Button Button => button;

    public void Configure(
        PurgatoryUITheme configuredTheme,
        ThemedButtonStyle configuredStyle,
        Button configuredButton,
        Image backgroundImage,
        TMP_Text labelText,
        Image iconImage)
    {
        theme = configuredTheme;
        style = configuredStyle;
        button = configuredButton;
        background = backgroundImage;
        label = labelText;
        icon = iconImage;
        ApplyTheme();
    }

    public void SetInteractable(bool value)
    {
        if (button != null)
            button.interactable = value;
    }

    public void ApplyTheme()
    {
        if (theme == null)
            return;

        if (background != null)
        {
            background.sprite = theme.ButtonSprite;
            background.type = theme.ButtonSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            background.color = Color.white;
        }

        if (label != null)
        {
            label.font = theme.PrimaryFont;
            label.fontSize = theme.CaptionSize;
            label.color = theme.Marble;
        }

        if (icon != null)
            icon.color = theme.Marble;

        if (button != null)
        {
            button.transition = Selectable.Transition.SpriteSwap;
            SpriteState sprites = button.spriteState;
            sprites.highlightedSprite = theme.ButtonHoverSprite;
            sprites.pressedSprite = theme.ButtonPressedSprite;
            sprites.selectedSprite = theme.SelectedFrameSprite;
            sprites.disabledSprite = theme.ButtonDisabledSprite;
            button.spriteState = sprites;

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.disabledColor = Color.white;
            button.colors = colors;
        }
    }
}
