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
    public BattleAbilityPreview CurrentAbilityPreview { get; private set; }
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
        CurrentAbilityPreview = default;
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

    public void ShowAbilityPreview(
        BattleAbilityPreview preview,
        string targetLabel,
        Sprite targetPortrait,
        AbilityDefinition definition)
    {
        CurrentAbilityPreview = preview;
        CurrentAttackPreview = preview.AttackPreview;
        LastResultFeedback = null;
        HasDetails = true;
        if (icon != null)
        {
            icon.sprite = targetPortrait != null
                ? targetPortrait
                : definition?.Icon != null
                    ? definition.Icon
                    : theme?.DevelopmentPortraitFallback;
            icon.preserveAspect = true;
            icon.gameObject.SetActive(icon.sprite != null);
        }
        if (titleLabel != null)
            titleLabel.text = $"{definition?.DisplayName ?? "Ability"} → {targetLabel}";
        if (descriptionLabel != null)
        {
            if (!preview.IsValid)
            {
                descriptionLabel.text = preview.Validation.Reason;
            }
            else if (definition != null &&
                     definition.EffectType == BattleAbilityEffectType.RestoreMorale)
            {
                descriptionLabel.text =
                    $"{definition.Description}\n" +
                    $"Morale  {preview.CurrentMorale:0.#} / {preview.MaximumMorale:0.#}\n" +
                    $"Restore  +{preview.PredictedMoraleRestore:0.#}\n" +
                    $"{preview.ActionPointCost} AP  •  Cooldown {definition.CooldownRounds}";
            }
            else
            {
                BattleAttackPreview attack = preview.AttackPreview;
                string areaWarning = definition != null &&
                                     definition.DamageDistribution == SquadDamageDistribution.Area
                    ? "\nArea: damage propagates through this formation"
                    : string.Empty;
                descriptionLabel.text =
                    $"{definition?.Description}\n" +
                    $"HP  {attack.TargetCurrentHealth} / {attack.TargetMaximumHealth}\n" +
                    $"Warriors  {attack.TargetLivingWarriors}\n" +
                    $"Hit  {UIStatFormatter.FormatPercentage(attack.HitChance)}\n" +
                    $"Critical  {UIStatFormatter.FormatPercentage(attack.CriticalChance)}\n" +
                    $"Damage  {attack.PredictedDamage}  (critical {attack.PredictedCriticalDamage})\n" +
                    $"{preview.ActionPointCost} AP  •  {attack.DamageType}  •  " +
                    $"Cooldown {definition?.CooldownRounds}{areaWarning}";
            }
        }
        if (emptyStateLabel != null)
            emptyStateLabel.gameObject.SetActive(false);
    }

    public void ShowAbilityResult(BattleAbilityResult result, AbilityDefinition definition)
    {
        if (result == null || !result.WasExecuted)
            return;
        if (definition != null &&
            definition.EffectType == BattleAbilityEffectType.RestoreMorale)
        {
            LastResultFeedback = $"RALLY  +{result.MoraleRestored:0.#} morale";
        }
        else
        {
            LastResultFeedback = !result.Hit
                ? "MISS"
                : result.Critical
                    ? $"CRITICAL • {result.Damage} damage"
                    : $"HIT • {result.Damage} damage";
        }
        if (descriptionLabel != null)
            descriptionLabel.text = LastResultFeedback;
    }
}
