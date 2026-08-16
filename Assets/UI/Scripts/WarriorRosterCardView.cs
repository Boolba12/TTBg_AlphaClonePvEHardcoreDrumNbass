using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class WarriorRosterCardView : MonoBehaviour
{
    [SerializeField] private Button selectButton;
    [SerializeField] private TMP_Text identityLabel;
    [SerializeField] private TMP_Text statsLabel;
    [SerializeField] private TMP_Text statusLabel;
    [SerializeField] private GameObject selectedFrame;

    private Action<string> onSelected;

    public string WarriorId { get; private set; }
    public Button SelectButton => selectButton;

    public void Configure(
        Button button,
        TMP_Text identity,
        TMP_Text stats,
        TMP_Text status,
        GameObject selection)
    {
        selectButton = button;
        identityLabel = identity;
        statsLabel = stats;
        statusLabel = status;
        selectedFrame = selection;
    }

    public void Bind(
        SquadManagementWarriorEntry entry,
        bool selected,
        Action<string> selectedCallback)
    {
        Unbind();
        WarriorId = entry?.WarriorId ?? string.Empty;
        onSelected = selectedCallback;
        WarriorData warrior = entry?.Warrior;
        if (identityLabel != null)
            identityLabel.text = warrior?.DisplayName ?? "Warrior unavailable";
        if (statsLabel != null)
            statsLabel.text = warrior == null
                ? "HP -  STR -  DEX -"
                : $"HP {warrior.maxHP}  STR {warrior.strength:0.#}  " +
                  $"DEX {warrior.dexterity:0.#}";
        if (statusLabel != null)
            statusLabel.text = entry?.Status == SquadManagementWarriorStatus.Assigned
                ? "ASSIGNED" : "RESERVE";
        if (selectButton != null)
        {
            selectButton.interactable = warrior != null;
            selectButton.onClick.AddListener(HandleSelected);
        }
        SetSelected(selected);
    }

    public void SetSelected(bool selected)
    {
        if (selectedFrame != null)
            selectedFrame.SetActive(selected);
    }

    private void HandleSelected()
    {
        if (!string.IsNullOrWhiteSpace(WarriorId))
            onSelected?.Invoke(WarriorId);
    }

    private void Unbind()
    {
        selectButton?.onClick.RemoveListener(HandleSelected);
        onSelected = null;
    }

    private void OnDestroy() => Unbind();
}
