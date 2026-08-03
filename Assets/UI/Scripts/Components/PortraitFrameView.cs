using UnityEngine;
using UnityEngine.UI;

public sealed class PortraitFrameView : MonoBehaviour
{
    [SerializeField] private PurgatoryUITheme theme;
    [SerializeField] private Image frame;
    [SerializeField] private Image portrait;

    public Sprite DisplayedPortrait => portrait != null ? portrait.sprite : null;

    public void Configure(
        PurgatoryUITheme configuredTheme,
        Image frameImage,
        Image portraitImage)
    {
        theme = configuredTheme;
        frame = frameImage;
        portrait = portraitImage;
        ApplyTheme();
    }

    public void SetPortrait(Sprite sprite)
    {
        if (portrait == null)
            return;
        portrait.sprite = sprite != null ? sprite : theme?.DevelopmentPortraitFallback;
        portrait.enabled = portrait.sprite != null;
    }

    public void ApplyTheme()
    {
        if (theme == null || frame == null)
            return;
        frame.sprite = theme.PortraitFrameSprite;
        frame.type = frame.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        frame.color = Color.white;
    }
}
