using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ProgressBarView : MonoBehaviour
{
    [SerializeField] private PurgatoryUITheme theme;
    [SerializeField] private Image background;
    [SerializeField] private Image fill;
    [SerializeField] private TMP_Text valueLabel;
    [SerializeField] private Color fillColor = Color.white;

    public float NormalizedValue { get; private set; }

    public void Configure(
        PurgatoryUITheme configuredTheme,
        Image backgroundImage,
        Image fillImage,
        TMP_Text label,
        Color configuredFillColor)
    {
        theme = configuredTheme;
        background = backgroundImage;
        fill = fillImage;
        valueLabel = label;
        fillColor = configuredFillColor;
        ApplyTheme();
    }

    public void SetValue(float current, float maximum, string formattedValue)
    {
        NormalizedValue = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
        if (fill != null)
            fill.fillAmount = NormalizedValue;
        if (valueLabel != null)
            valueLabel.text = formattedValue ?? string.Empty;
    }

    public void ApplyTheme()
    {
        if (theme == null)
            return;

        if (background != null)
            background.color = theme.Granite;
        if (fill != null)
        {
            fill.color = fillColor;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
        }
        if (valueLabel != null)
        {
            valueLabel.font = theme.PrimaryFont;
            valueLabel.fontSize = theme.CaptionSize;
            valueLabel.color = theme.Marble;
        }
    }
}
