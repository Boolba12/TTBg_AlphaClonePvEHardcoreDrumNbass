using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class AbilityDetailsPanelView : MonoBehaviour
{
    [SerializeField] private PurgatoryUITheme theme;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text descriptionLabel;
    [SerializeField] private TMP_Text emptyStateLabel;

    public bool HasDetails { get; private set; }
    public BattleAttackPreview CurrentAttackPreview { get; private set; }
    public string LastResultFeedback { get; private set; }
    public Sprite DisplayedPortrait => icon != null ? icon.sprite : null;

    private void Awake()
    {
        ShowUnavailable();
    }

    public void Configure(
        PurgatoryUITheme configuredTheme,
        Image configuredIcon,
        TMP_Text configuredTitle,
        TMP_Text configuredDescription,
        TMP_Text configuredEmptyState)
    {
        theme = configuredTheme;
        icon = configuredIcon;
        titleLabel = configuredTitle;
        descriptionLabel = configuredDescription;
        emptyStateLabel = configuredEmptyState;
        ShowUnavailable();
    }

    public void ShowUnavailable()
    {
        HasDetails = false;
        CurrentAttackPreview = default;
        LastResultFeedback = null;
        if (icon != null)
            icon.gameObject.SetActive(false);
        if (titleLabel != null)
            titleLabel.text = string.Empty;
        if (descriptionLabel != null)
            descriptionLabel.text = string.Empty;
        if (emptyStateLabel != null)
        {
            emptyStateLabel.text = theme?.UnavailableLabel ?? "Unavailable in this build";
            emptyStateLabel.gameObject.SetActive(true);
        }
    }

    public void ShowAttackPreview(
        BattleAttackPreview preview,
        string targetLabel,
        Sprite targetPortrait,
        AttackDefinition definition)
    {
        CurrentAttackPreview = preview;
        LastResultFeedback = null;
        HasDetails = true;
        if (icon != null)
        {
            icon.sprite = targetPortrait != null
                ? targetPortrait
                : theme?.DevelopmentPortraitFallback;
            icon.preserveAspect = true;
            icon.gameObject.SetActive(icon.sprite != null);
        }
        if (titleLabel != null)
        {
            string attackName = definition != null ? definition.DisplayName : "Attack";
            titleLabel.text = $"{attackName} → {targetLabel}";
        }
        if (descriptionLabel != null)
        {
            descriptionLabel.text = preview.IsValid
                ? $"HP  {preview.TargetCurrentHealth} / {preview.TargetMaximumHealth}\n" +
                  $"Warriors  {preview.TargetLivingWarriors}\n" +
                  $"Hit  {UIStatFormatter.FormatPercentage(preview.HitChance)}\n" +
                  $"Critical  {UIStatFormatter.FormatPercentage(preview.CriticalChance)}\n" +
                  $"Damage  {preview.PredictedDamage}  (critical {preview.PredictedCriticalDamage})\n" +
                  $"{preview.ActionPointCost} AP  •  {preview.DamageType}"
                : preview.Validation.Reason;
        }
        if (emptyStateLabel != null)
            emptyStateLabel.gameObject.SetActive(false);
    }

    public void ShowAttackResult(BattleAttackResult result)
    {
        if (result == null || !result.WasExecuted)
            return;
        LastResultFeedback = !result.Hit
            ? "MISS"
            : result.Critical
                ? $"CRITICAL • {result.AppliedDamage} damage"
                : $"HIT • {result.AppliedDamage} damage";
        if (descriptionLabel != null)
            descriptionLabel.text = LastResultFeedback;
    }
}
