using System.Collections;
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

    private Coroutine pulseRoutine;
    private bool hasValue;
    private float previousValue;

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
        bool changed = hasValue && !Mathf.Approximately(previousValue, current);
        previousValue = current;
        hasValue = true;
        NormalizedValue = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
        if (fill != null)
            fill.fillAmount = NormalizedValue;
        if (valueLabel != null)
            valueLabel.text = formattedValue ?? string.Empty;
        if (changed && Application.isPlaying && fill != null)
        {
            if (pulseRoutine != null)
                StopCoroutine(pulseRoutine);
            pulseRoutine = StartCoroutine(PulseFill());
        }
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

    private IEnumerator PulseFill()
    {
        const float duration = 0.14f;
        Color bright = Color.Lerp(fillColor, Color.white, 0.28f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            fill.color = Color.Lerp(bright, fillColor, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        fill.color = fillColor;
        pulseRoutine = null;
    }

    private void OnDisable()
    {
        if (pulseRoutine != null)
            StopCoroutine(pulseRoutine);
        pulseRoutine = null;
        if (fill != null)
            fill.color = fillColor;
    }
}
