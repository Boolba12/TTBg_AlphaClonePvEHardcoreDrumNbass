using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BattleActionBarView : MonoBehaviour
{
    [SerializeField] private PurgatoryUITheme theme;
    [SerializeField] private Button[] actionButtons;
    [SerializeField] private TMP_Text unavailableLabel;

    public bool ActionsAvailable { get; private set; }

    private void Awake()
    {
        SetActionsAvailable(false);
    }

    public void Configure(
        PurgatoryUITheme configuredTheme,
        Button[] configuredButtons,
        TMP_Text configuredUnavailableLabel)
    {
        theme = configuredTheme;
        actionButtons = configuredButtons;
        unavailableLabel = configuredUnavailableLabel;
        SetActionsAvailable(false);
    }

    public void SetActionsAvailable(bool available)
    {
        ActionsAvailable = available;
        if (actionButtons != null)
        {
            foreach (Button actionButton in actionButtons)
            {
                if (actionButton != null)
                    actionButton.interactable = available;
            }
        }
        if (unavailableLabel != null)
        {
            unavailableLabel.text = theme?.UnavailableLabel ?? "Unavailable in this build";
            unavailableLabel.gameObject.SetActive(!available);
        }
    }
}
