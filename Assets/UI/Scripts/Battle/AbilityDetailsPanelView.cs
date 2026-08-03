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
}
