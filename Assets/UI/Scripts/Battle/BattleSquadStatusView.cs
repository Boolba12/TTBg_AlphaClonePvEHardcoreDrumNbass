using System.Collections;
using TMPro;
using UnityEngine;

public sealed class BattleSquadStatusView : MonoBehaviour
{
    [SerializeField] private PurgatoryUITheme theme;
    [SerializeField] private GameObject contentRoot;
    [SerializeField] private GameObject emptyStateRoot;
    [SerializeField] private TMP_Text emptyStateLabel;
    [SerializeField] private TMP_Text squadLabel;
    [SerializeField] private TMP_Text commanderLabel;
    [SerializeField] private PortraitFrameView portraitFrame;
    [SerializeField] private ProgressBarView healthBar;
    [SerializeField] private ProgressBarView actionPointsBar;
    [SerializeField] private ProgressBarView moraleBar;
    [SerializeField] private StatRowView warriorCountRow;
    [SerializeField] private CanvasGroup contentCanvasGroup;

    private Coroutine contentFadeRoutine;
    private string displayedSquadId;

    public int RenderCount { get; private set; }
    public int EmptyStateCount { get; private set; }
    public bool HasData { get; private set; }
    public BattleSquadStatusModel CurrentModel { get; private set; }
    public Sprite DisplayedPortrait => portraitFrame != null
        ? portraitFrame.DisplayedPortrait
        : null;

    public void Configure(
        PurgatoryUITheme configuredTheme,
        GameObject configuredContentRoot,
        GameObject configuredEmptyStateRoot,
        TMP_Text configuredEmptyStateLabel,
        TMP_Text configuredSquadLabel,
        TMP_Text configuredCommanderLabel,
        PortraitFrameView configuredPortraitFrame,
        ProgressBarView configuredHealthBar,
        ProgressBarView configuredActionPointsBar,
        ProgressBarView configuredMoraleBar,
        StatRowView configuredWarriorCountRow,
        CanvasGroup configuredContentCanvasGroup = null)
    {
        theme = configuredTheme;
        contentRoot = configuredContentRoot;
        emptyStateRoot = configuredEmptyStateRoot;
        emptyStateLabel = configuredEmptyStateLabel;
        squadLabel = configuredSquadLabel;
        commanderLabel = configuredCommanderLabel;
        portraitFrame = configuredPortraitFrame;
        healthBar = configuredHealthBar;
        actionPointsBar = configuredActionPointsBar;
        moraleBar = configuredMoraleBar;
        warriorCountRow = configuredWarriorCountRow;
        contentCanvasGroup = configuredContentCanvasGroup;
        ApplyTheme();
    }

    public void Render(BattleSquadStatusModel model)
    {
        bool identityChanged = displayedSquadId != model.SquadId;
        displayedSquadId = model.SquadId;
        CurrentModel = model;
        HasData = true;
        RenderCount++;
        if (contentRoot != null)
            contentRoot.SetActive(true);
        if (emptyStateRoot != null)
            emptyStateRoot.SetActive(false);

        if (squadLabel != null)
            squadLabel.text = model.SquadId;
        if (commanderLabel != null)
            commanderLabel.text = model.CommanderId;
        portraitFrame?.SetPortrait(model.CommanderPortrait);
        healthBar?.SetValue(
            model.CurrentHealth,
            model.MaximumHealth,
            UIStatFormatter.FormatCurrentMaximum(model.CurrentHealth, model.MaximumHealth));
        actionPointsBar?.SetValue(
            model.CurrentActionPoints,
            model.MaximumActionPoints,
            UIStatFormatter.FormatCurrentMaximum(
                model.CurrentActionPoints,
                model.MaximumActionPoints));
        moraleBar?.SetValue(
            model.CurrentMorale,
            model.MaximumMorale,
            UIStatFormatter.FormatCurrentMaximum(model.CurrentMorale, model.MaximumMorale));
        warriorCountRow?.SetValue(
            UIStatFormatter.FormatCurrentMaximum(
                model.LivingWarriors,
                model.MaximumWarriors));
        if (identityChanged)
            PlayContentFade();
    }

    public void ShowEmpty(string reason = null)
    {
        HasData = false;
        displayedSquadId = null;
        CurrentModel = default;
        EmptyStateCount++;
        if (contentRoot != null)
            contentRoot.SetActive(false);
        if (emptyStateRoot != null)
            emptyStateRoot.SetActive(true);
        if (emptyStateLabel != null)
        {
            emptyStateLabel.text = string.IsNullOrWhiteSpace(reason)
                ? theme?.EmptySquadLabel ?? "Player squad is unavailable"
                : reason;
        }
    }

    private void ApplyTheme()
    {
        if (theme == null)
            return;
        if (emptyStateLabel != null)
        {
            emptyStateLabel.font = theme.PrimaryFont;
            emptyStateLabel.fontSize = theme.BodySize;
            emptyStateLabel.color = theme.TextSecondary;
        }
        if (squadLabel != null)
        {
            squadLabel.font = theme.AccentFont;
            squadLabel.fontSize = theme.HeadingSize;
            squadLabel.color = theme.Gold;
            squadLabel.overflowMode = TextOverflowModes.Ellipsis;
            squadLabel.textWrappingMode = TextWrappingModes.NoWrap;
        }
        if (commanderLabel != null)
        {
            commanderLabel.font = theme.PrimaryFont;
            commanderLabel.fontSize = theme.BodySize;
            commanderLabel.color = theme.TextPrimary;
            commanderLabel.overflowMode = TextOverflowModes.Ellipsis;
            commanderLabel.textWrappingMode = TextWrappingModes.NoWrap;
        }
    }

    private void PlayContentFade()
    {
        if (contentCanvasGroup == null || !Application.isPlaying)
            return;
        if (contentFadeRoutine != null)
            StopCoroutine(contentFadeRoutine);
        contentFadeRoutine = StartCoroutine(FadeContent());
    }

    private IEnumerator FadeContent()
    {
        const float duration = 0.12f;
        contentCanvasGroup.alpha = 0.62f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            contentCanvasGroup.alpha = Mathf.Lerp(0.62f, 1f, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        contentCanvasGroup.alpha = 1f;
        contentFadeRoutine = null;
    }

    private void OnDisable()
    {
        if (contentFadeRoutine != null)
            StopCoroutine(contentFadeRoutine);
        contentFadeRoutine = null;
        if (contentCanvasGroup != null)
            contentCanvasGroup.alpha = 1f;
    }
}
