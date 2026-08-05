using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SectionHeaderView : MonoBehaviour
{
    [SerializeField] private PurgatoryUITheme theme;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Image separator;

    public void Configure(
        PurgatoryUITheme configuredTheme,
        TMP_Text labelText,
        Image separatorImage,
        string localizationReadyLabel)
    {
        theme = configuredTheme;
        label = labelText;
        separator = separatorImage;
        if (label != null)
            label.text = localizationReadyLabel ?? string.Empty;
        ApplyTheme();
    }

    public void ApplyTheme()
    {
        if (theme == null)
            return;

        if (label != null)
        {
            label.font = theme.AccentFont;
            label.fontSize = theme.CaptionSize;
            label.color = theme.TextPrimary;
        }

        if (separator != null)
        {
            separator.sprite = theme.SeparatorSprite;
            separator.type = separator.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            separator.color = Color.white;
        }
    }
}
