using TMPro;
using UnityEngine;

public sealed class StatRowView : MonoBehaviour
{
    [SerializeField] private PurgatoryUITheme theme;
    [SerializeField] private TMP_Text label;
    [SerializeField] private TMP_Text value;

    public string DisplayedValue => value != null ? value.text : string.Empty;

    public void Configure(
        PurgatoryUITheme configuredTheme,
        TMP_Text labelText,
        TMP_Text valueText,
        string localizationReadyLabel)
    {
        theme = configuredTheme;
        label = labelText;
        value = valueText;
        if (label != null)
            label.text = localizationReadyLabel ?? string.Empty;
        ApplyTheme();
    }

    public void SetValue(string formattedValue)
    {
        if (value != null)
            value.text = formattedValue ?? string.Empty;
    }

    public void ApplyTheme()
    {
        if (theme == null)
            return;

        if (label != null)
        {
            label.font = theme.PrimaryFont;
            label.fontSize = theme.CaptionSize;
            label.color = theme.Marble;
        }

        if (value != null)
        {
            value.font = theme.PrimaryFont;
            value.fontSize = theme.CaptionSize;
            value.color = theme.Gold;
        }
    }
}
