using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PreBattleSquadCardView : MonoBehaviour
{
    [SerializeField] private Button selectButton;
    [SerializeField] private Image portraitImage;
    [SerializeField] private TMP_Text squadLabel;
    [SerializeField] private TMP_Text commanderLabel;
    [SerializeField] private TMP_Text compositionLabel;
    [SerializeField] private TMP_Text statusLabel;
    [SerializeField] private GameObject selectedFrame;

    private string squadId;
    private Action<string> selectionCallback;

    public string SquadId => squadId;
    public Button SelectButton => selectButton;
    public bool IsSelected => selectedFrame != null && selectedFrame.activeSelf;

    public void Configure(
        Button button,
        Image portrait,
        TMP_Text squad,
        TMP_Text commander,
        TMP_Text composition,
        TMP_Text status,
        GameObject selectionFrame)
    {
        selectButton = button;
        portraitImage = portrait;
        squadLabel = squad;
        commanderLabel = commander;
        compositionLabel = composition;
        statusLabel = status;
        selectedFrame = selectionFrame;
    }

    public void Bind(
        PreBattleSquadOption option,
        Sprite portrait,
        bool selected,
        Action<string> onSelected)
    {
        Unbind();
        squadId = option?.SquadId ?? string.Empty;
        selectionCallback = onSelected;
        if (portraitImage != null)
        {
            portraitImage.sprite = portrait;
            portraitImage.enabled = portrait != null;
        }
        if (squadLabel != null)
            squadLabel.text = string.IsNullOrWhiteSpace(squadId) ? "Unidentified squad" : $"Squad {squadId}";
        if (commanderLabel != null)
        {
            string commander = string.IsNullOrWhiteSpace(option?.CommanderId)
                ? "Unavailable"
                : option.CommanderId;
            commanderLabel.text = $"Commander {commander} · {option?.Race}";
        }
        if (compositionLabel != null)
            compositionLabel.text = $"Warriors {option?.LivingWarriors ?? 0}/{option?.MaximumWarriors ?? SquadData.MaximumWarriors}";
        if (statusLabel != null)
        {
            statusLabel.text = option != null && option.IsAvailable
                ? "READY"
                : option?.UnavailableMessage ?? "Unavailable";
        }
        if (selectButton != null)
        {
            selectButton.interactable = option != null && option.IsAvailable;
            selectButton.onClick.AddListener(HandleSelected);
        }
        SetSelected(selected);
    }

    public void SetSelected(bool selected)
    {
        if (selectedFrame != null)
            selectedFrame.SetActive(selected);
    }

    private void HandleSelected() => selectionCallback?.Invoke(squadId);

    private void Unbind()
    {
        selectButton?.onClick.RemoveListener(HandleSelected);
        selectionCallback = null;
    }

    private void OnDestroy() => Unbind();
}
