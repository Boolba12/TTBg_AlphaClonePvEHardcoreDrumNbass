using System;
using UnityEngine;
using UnityEngine.UI;

public enum MinimapCollapseState
{
    Expanded,
    Collapsing,
    Collapsed,
    Expanding
}

public sealed class MinimapCollapseController : MonoBehaviour
{
    [SerializeField] private RectTransform expandedRoot;
    [SerializeField] private CanvasGroup expandedCanvasGroup;
    [SerializeField] private GameObject collapsedRoot;
    [SerializeField] private Button collapseButton;
    [SerializeField] private Button expandButton;
    [SerializeField, Min(0.1f)] private float inactivitySeconds = 10f;
    [SerializeField, Min(0.01f)] private float animationSeconds = 0.18f;

    private float inactiveFor;
    private float transitionProgress;

    public MinimapCollapseState State { get; private set; } = MinimapCollapseState.Expanded;
    public float InactiveFor => inactiveFor;
    public Button CollapseButton => collapseButton;
    public Button ExpandButton => expandButton;
    public int ActiveAnimationCount =>
        State == MinimapCollapseState.Collapsing || State == MinimapCollapseState.Expanding ? 1 : 0;
    public event Action<MinimapCollapseState> StateChanged;

    public void Configure(
        RectTransform expanded,
        CanvasGroup canvasGroup,
        GameObject collapsed,
        Button collapse,
        Button expand,
        float timeoutSeconds = 10f,
        float transitionSeconds = 0.18f)
    {
        UnbindButtons();
        expandedRoot = expanded;
        expandedCanvasGroup = canvasGroup;
        collapsedRoot = collapsed;
        collapseButton = collapse;
        expandButton = expand;
        inactivitySeconds = Mathf.Max(0.1f, timeoutSeconds);
        animationSeconds = Mathf.Max(0.01f, transitionSeconds);
        BindButtons();
        SetImmediate(MinimapCollapseState.Expanded);
    }

    private void Awake()
    {
        BindButtons();
        ApplyVisuals(1f);
    }

    private void OnDestroy() => UnbindButtons();
    private void Update() => Advance(Time.unscaledDeltaTime);

    public void RegisterInteraction()
    {
        inactiveFor = 0f;
        if (State == MinimapCollapseState.Collapsed || State == MinimapCollapseState.Collapsing)
            BeginExpand();
    }

    public void BeginCollapse()
    {
        if (State == MinimapCollapseState.Collapsed || State == MinimapCollapseState.Collapsing)
            return;
        transitionProgress = State == MinimapCollapseState.Expanding
            ? 1f - transitionProgress
            : 0f;
        ChangeState(MinimapCollapseState.Collapsing);
    }

    public void BeginExpand()
    {
        inactiveFor = 0f;
        if (State == MinimapCollapseState.Expanded || State == MinimapCollapseState.Expanding)
            return;
        if (expandedRoot != null)
            expandedRoot.gameObject.SetActive(true);
        if (collapsedRoot != null)
            collapsedRoot.SetActive(false);
        transitionProgress = State == MinimapCollapseState.Collapsing
            ? 1f - transitionProgress
            : 0f;
        ChangeState(MinimapCollapseState.Expanding);
    }

    public void Advance(float unscaledDeltaSeconds)
    {
        float delta = Mathf.Max(0f, unscaledDeltaSeconds);
        if (State == MinimapCollapseState.Expanded)
        {
            inactiveFor += delta;
            if (inactiveFor >= inactivitySeconds)
                BeginCollapse();
            return;
        }
        if (State != MinimapCollapseState.Collapsing && State != MinimapCollapseState.Expanding)
            return;

        transitionProgress = Mathf.Clamp01(transitionProgress + delta / animationSeconds);
        float expandedAmount = State == MinimapCollapseState.Collapsing
            ? 1f - transitionProgress
            : transitionProgress;
        ApplyVisuals(expandedAmount);
        if (transitionProgress < 1f)
            return;
        if (State == MinimapCollapseState.Collapsing)
            SetImmediate(MinimapCollapseState.Collapsed);
        else
            SetImmediate(MinimapCollapseState.Expanded);
    }

    public void SetImmediate(MinimapCollapseState stableState)
    {
        bool expanded = stableState == MinimapCollapseState.Expanded;
        State = stableState;
        transitionProgress = 0f;
        inactiveFor = 0f;
        if (expandedRoot != null)
            expandedRoot.gameObject.SetActive(expanded);
        if (collapsedRoot != null)
            collapsedRoot.SetActive(!expanded);
        ApplyVisuals(expanded ? 1f : 0f);
        StateChanged?.Invoke(State);
    }

    private void ApplyVisuals(float expandedAmount)
    {
        if (expandedCanvasGroup != null)
        {
            expandedCanvasGroup.alpha = expandedAmount;
            expandedCanvasGroup.interactable = expandedAmount >= 0.99f;
            expandedCanvasGroup.blocksRaycasts = expandedAmount >= 0.99f;
        }
        if (expandedRoot != null)
            expandedRoot.localScale = Vector3.one * Mathf.Lerp(0.84f, 1f, expandedAmount);
    }

    private void ChangeState(MinimapCollapseState state)
    {
        State = state;
        StateChanged?.Invoke(state);
    }

    private void BindButtons()
    {
        if (collapseButton != null)
        {
            collapseButton.onClick.RemoveListener(BeginCollapse);
            collapseButton.onClick.AddListener(BeginCollapse);
        }
        if (expandButton != null)
        {
            expandButton.onClick.RemoveListener(BeginExpand);
            expandButton.onClick.AddListener(BeginExpand);
        }
    }

    private void UnbindButtons()
    {
        collapseButton?.onClick.RemoveListener(BeginCollapse);
        expandButton?.onClick.RemoveListener(BeginExpand);
    }
}
