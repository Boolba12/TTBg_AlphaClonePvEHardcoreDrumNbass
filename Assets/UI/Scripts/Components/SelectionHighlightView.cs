using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class SelectionHighlightView : MonoBehaviour
{
    [SerializeField] private PurgatoryUITheme theme;
    [SerializeField] private Image highlight;
    [SerializeField, Range(0.08f, 0.15f)] private float fadeDuration = 0.12f;

    private Coroutine fadeRoutine;

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
        if (!Application.isPlaying)
        {
            highlight.color = highlighted ? Color.white : new Color(1f, 1f, 1f, 0f);
            highlight.enabled = highlighted;
            return;
        }

        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);
        highlight.enabled = true;
        fadeRoutine = StartCoroutine(Fade(highlighted ? 1f : 0f));
    }

    private IEnumerator Fade(float targetAlpha)
    {
        Color color = highlight.color;
        float start = color.a;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            color.a = Mathf.Lerp(start, targetAlpha, Mathf.Clamp01(elapsed / fadeDuration));
            highlight.color = color;
            yield return null;
        }
        color.a = targetAlpha;
        highlight.color = color;
        highlight.enabled = targetAlpha > 0f;
        fadeRoutine = null;
    }
}
