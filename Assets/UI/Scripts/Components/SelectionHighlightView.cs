using UnityEngine;
using UnityEngine.UI;

public sealed class SelectionHighlightView : MonoBehaviour
{
    [SerializeField] private PurgatoryUITheme theme;
    [SerializeField] private Image highlight;

    public bool IsHighlighted { get; private set; }

    public void Configure(PurgatoryUITheme configuredTheme, Image highlightImage)
    {
        theme = configuredTheme;
        highlight = highlightImage;
        if (highlight != null && theme != null)
        {
            highlight.sprite = theme.SelectedFrameSprite;
            highlight.type = highlight.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        }
        SetHighlighted(false);
    }

    public void SetHighlighted(bool highlighted)
    {
        IsHighlighted = highlighted;
        if (highlight == null)
            return;
        highlight.color = Color.white;
        highlight.enabled = highlighted;
    }
}
