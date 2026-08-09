using System.Collections;
using TMPro;
using UnityEngine;

public enum SquadAttackTargetVisualState
{
    None,
    Available,
    Unavailable,
    HoveredValid,
    HoveredInvalid,
    Resolving
}

public sealed class SquadAttackTargetView : MonoBehaviour
{
    [SerializeField] private PurgatoryUITheme theme;
    [SerializeField] private LineRenderer targetRing;
    [SerializeField] private TMP_Text feedbackLabel;
    [SerializeField, Range(12, 64)] private int segmentCount = 32;
    [SerializeField, Min(0.1f)] private float radius = 0.55f;
    [SerializeField, Min(0.005f)] private float width = 0.045f;
    [SerializeField] private float height = 0.055f;

    private Material runtimeMaterial;
    private Coroutine feedbackRoutine;

    public SquadAttackTargetVisualState State { get; private set; }
    public string LastFeedback { get; private set; }
    public LineRenderer TargetRing => targetRing;
    public TMP_Text FeedbackLabel => feedbackLabel;

    public void Configure(
        PurgatoryUITheme configuredTheme,
        LineRenderer ring,
        TMP_Text configuredFeedbackLabel)
    {
        theme = configuredTheme;
        targetRing = ring;
        feedbackLabel = configuredFeedbackLabel;
        ConfigureRing();
        SetState(SquadAttackTargetVisualState.None);
    }

    private void Awake()
    {
        ConfigureRing();
        SetState(SquadAttackTargetVisualState.None);
        if (feedbackLabel != null)
            feedbackLabel.gameObject.SetActive(false);
    }

    public void SetState(SquadAttackTargetVisualState state)
    {
        State = state;
        if (targetRing == null)
            return;

        targetRing.enabled = state != SquadAttackTargetVisualState.None;
        if (!targetRing.enabled)
            return;

        Color color = state switch
        {
            SquadAttackTargetVisualState.Available =>
                theme != null ? theme.EnemySide : new Color32(126, 64, 54, 230),
            SquadAttackTargetVisualState.Unavailable =>
                theme != null ? theme.Disabled : new Color32(91, 94, 91, 150),
            SquadAttackTargetVisualState.HoveredValid =>
                theme != null ? theme.Danger : new Color32(155, 51, 47, 255),
            SquadAttackTargetVisualState.HoveredInvalid =>
                theme != null ? theme.Disabled : new Color32(91, 94, 91, 210),
            SquadAttackTargetVisualState.Resolving =>
                theme != null ? theme.Bronze : new Color32(133, 88, 42, 255),
            _ => Color.clear
        };
        targetRing.startColor = color;
        targetRing.endColor = color;
        float stateWidth = state == SquadAttackTargetVisualState.HoveredValid ||
                           state == SquadAttackTargetVisualState.Resolving
            ? width * 1.35f
            : width;
        targetRing.startWidth = stateWidth;
        targetRing.endWidth = stateWidth;
    }

    public void ShowAttackPulse()
    {
        StartFeedback("", theme != null ? theme.Bronze : Color.yellow, 0.22f);
    }

    public void ShowResult(BattleAttackResult result)
    {
        if (result == null || !result.WasExecuted)
            return;
        string text;
        Color color;
        if (!result.Hit)
        {
            text = "MISS";
            color = theme != null ? theme.TextSecondary : Color.gray;
        }
        else if (result.Critical)
        {
            text = $"CRITICAL  -{result.AppliedDamage}";
            color = theme != null ? theme.Gold : Color.yellow;
        }
        else
        {
            text = $"-{result.AppliedDamage}";
            color = theme != null ? theme.Danger : Color.red;
        }
        StartFeedback(text, color, 0.7f);
    }

    public void ShowAbilityResult(BattleAbilityResult result)
    {
        if (result == null || !result.WasExecuted)
            return;
        if (result.MoraleRestored > 0f)
        {
            StartFeedback(
                $"+{result.MoraleRestored:0.#} MORALE",
                theme != null ? theme.Emerald : Color.green,
                0.7f);
            return;
        }

        string text = !result.Hit
            ? "MISS"
            : result.Critical
                ? $"CRITICAL  -{result.Damage}"
                : $"-{result.Damage}";
        Color color = !result.Hit
            ? theme != null ? theme.TextSecondary : Color.gray
            : result.Critical
                ? theme != null ? theme.Gold : Color.yellow
                : theme != null ? theme.Danger : Color.red;
        StartFeedback(text, color, 0.7f);
    }

    private void StartFeedback(string text, Color color, float duration)
    {
        LastFeedback = text;
        if (!Application.isPlaying)
        {
            if (feedbackLabel != null)
            {
                feedbackLabel.text = text;
                feedbackLabel.color = color;
            }
            return;
        }
        if (feedbackRoutine != null)
            StopCoroutine(feedbackRoutine);
        feedbackRoutine = StartCoroutine(FeedbackRoutine(text, color, duration));
    }

    private IEnumerator FeedbackRoutine(string text, Color color, float duration)
    {
        SquadAttackTargetVisualState previous = State;
        SetState(SquadAttackTargetVisualState.Resolving);
        if (feedbackLabel != null)
        {
            feedbackLabel.text = text;
            feedbackLabel.color = color;
            feedbackLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(text));
        }
        yield return new WaitForSecondsRealtime(duration);
        if (feedbackLabel != null)
            feedbackLabel.gameObject.SetActive(false);
        feedbackRoutine = null;
        SetState(previous == SquadAttackTargetVisualState.Resolving
            ? SquadAttackTargetVisualState.None
            : previous);
    }

    private void ConfigureRing()
    {
        if (targetRing == null)
            return;
        if (Application.isPlaying && runtimeMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                runtimeMaterial = new Material(shader) { name = "Runtime_SquadAttackTargetRing" };
                targetRing.sharedMaterial = runtimeMaterial;
            }
        }

        targetRing.useWorldSpace = false;
        targetRing.loop = true;
        targetRing.positionCount = segmentCount;
        for (int i = 0; i < segmentCount; i++)
        {
            float angle = Mathf.PI * 2f * i / segmentCount;
            targetRing.SetPosition(
                i,
                new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius));
        }
    }

    private void OnDisable()
    {
        if (feedbackRoutine != null)
            StopCoroutine(feedbackRoutine);
        feedbackRoutine = null;
        SetState(SquadAttackTargetVisualState.None);
        if (feedbackLabel != null)
            feedbackLabel.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (feedbackLabel != null && feedbackLabel.gameObject.activeSelf && Camera.main != null)
            feedbackLabel.transform.rotation = Camera.main.transform.rotation;
    }

    private void OnDestroy()
    {
        if (runtimeMaterial == null)
            return;
        if (Application.isPlaying)
            Destroy(runtimeMaterial);
        else
            DestroyImmediate(runtimeMaterial);
    }
}
