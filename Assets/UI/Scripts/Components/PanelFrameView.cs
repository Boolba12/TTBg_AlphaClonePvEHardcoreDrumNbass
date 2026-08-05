using UnityEngine;
using UnityEngine.UI;

public enum PanelFrameStyle
{
    Outer,
    Inset
}

public sealed class PanelFrameView : MonoBehaviour
{
    [SerializeField] private PurgatoryUITheme theme;
    [SerializeField] private Image background;
    [SerializeField] private PanelFrameStyle style;

    public PurgatoryUITheme Theme => theme;

    public void Configure(PurgatoryUITheme configuredTheme, Image backgroundImage)
    {
        Configure(configuredTheme, backgroundImage, PanelFrameStyle.Outer);
    }

    public void Configure(
        PurgatoryUITheme configuredTheme,
        Image backgroundImage,
        PanelFrameStyle configuredStyle)
    {
        theme = configuredTheme;
        background = backgroundImage;
        style = configuredStyle;
        ApplyTheme();
    }

    public void ApplyTheme()
    {
        if (theme == null || background == null)
            return;

        background.sprite = style == PanelFrameStyle.Inset
            ? theme.InsetPanelSprite
            : theme.OuterFrameSprite;
        background.type = background.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        background.color = Color.white;
    }
}
