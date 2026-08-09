using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BattleResultPanelView : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text summaryLabel;
    [SerializeField] private TMP_Text commanderLabel;
    [SerializeField] private TMP_Text saveStatusLabel;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button retrySaveButton;

    private bool listenersBound;

    public bool IsVisible => panelRoot != null && panelRoot.activeInHierarchy;
    public Button ContinueButton => continueButton;
    public Button RetrySaveButton => retrySaveButton;
    public string SaveStatus => saveStatusLabel != null ? saveStatusLabel.text : string.Empty;

    public event Action ContinueRequested;
    public event Action RetrySaveRequested;

    public void Configure(
        GameObject configuredRoot,
        TMP_Text configuredTitle,
        TMP_Text configuredSummary,
        TMP_Text configuredCommander,
        TMP_Text configuredSaveStatus,
        Button configuredContinue,
        Button configuredRetrySave)
    {
        UnbindListeners();
        panelRoot = configuredRoot;
        titleLabel = configuredTitle;
        summaryLabel = configuredSummary;
        commanderLabel = configuredCommander;
        saveStatusLabel = configuredSaveStatus;
        continueButton = configuredContinue;
        retrySaveButton = configuredRetrySave;
        BindListeners();
        Hide();
    }

    public void Show(BattleOutcome outcome, SaveOperationResult saveResult)
    {
        if (outcome == null)
        {
            ShowFailure("Battle result is unavailable.");
            return;
        }

        if (titleLabel != null)
            titleLabel.text = outcome.resultType.ToString().ToUpperInvariant();
        int playerCasualties = outcome.casualties.Count(casualty =>
            outcome.participantResults.Any(result => result.side == BattleSide.Player &&
                result.squadId == casualty.squadId));
        int survivingPlayerSquads = outcome.participantResults.Count(result =>
            result.side == BattleSide.Player &&
            !outcome.defeatedSquadIds.Contains(result.squadId));
        if (summaryLabel != null)
        {
            summaryLabel.text =
                $"Rounds: {outcome.rounds}\nCompleted turns: {outcome.completedTurns}\n" +
                $"Surviving squads: {survivingPlayerSquads}\nWarrior casualties: {playerCasualties}";
        }

        SquadBattleResult player = outcome.participantResults.FirstOrDefault(
            result => result.side == BattleSide.Player);
        if (commanderLabel != null)
        {
            commanderLabel.text = player == null
                ? "Commander outcome: unavailable"
                : $"Commander: {FormatCommanderOutcome(player)}";
        }
        ShowSaveState(saveResult);
        if (panelRoot != null)
        {
            Transform modalLayer = panelRoot.transform.parent;
            if (modalLayer != null && !modalLayer.gameObject.activeSelf)
                modalLayer.gameObject.SetActive(true);
            panelRoot.SetActive(true);
        }
    }

    public void ShowSaveState(SaveOperationResult result)
    {
        if (saveStatusLabel != null)
        {
            saveStatusLabel.text = result.Success
                ? "Autosave complete"
                : $"Autosave failed: {result.Error}";
        }
        if (continueButton != null)
            continueButton.interactable = result.Success;
        if (retrySaveButton != null)
            retrySaveButton.gameObject.SetActive(!result.Success);
    }

    public void ShowFailure(string error)
    {
        if (titleLabel != null)
            titleLabel.text = "RESULT ERROR";
        if (summaryLabel != null)
            summaryLabel.text = error ?? "Unknown battle result error.";
        if (commanderLabel != null)
            commanderLabel.text = string.Empty;
        if (saveStatusLabel != null)
            saveStatusLabel.text = "Result was not applied.";
        if (continueButton != null)
            continueButton.interactable = false;
        if (retrySaveButton != null)
            retrySaveButton.gameObject.SetActive(false);
        if (panelRoot != null)
        {
            Transform modalLayer = panelRoot.transform.parent;
            if (modalLayer != null && !modalLayer.gameObject.activeSelf)
                modalLayer.gameObject.SetActive(true);
            panelRoot.SetActive(true);
        }
    }

    public void Hide()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private static string FormatCommanderOutcome(SquadBattleResult result)
    {
        return result.commanderOutcome switch
        {
            CommanderPostBattleOutcomeType.SurvivedNormally => "survived",
            CommanderPostBattleOutcomeType.SurvivedWithPermanentDebuff =>
                $"survived with {result.permanentDebuffId}",
            CommanderPostBattleOutcomeType.Killed => "killed",
            _ => "pending"
        };
    }

    private void BindListeners()
    {
        if (listenersBound)
            return;
        continueButton?.onClick.AddListener(HandleContinue);
        retrySaveButton?.onClick.AddListener(HandleRetrySave);
        listenersBound = true;
    }

    private void UnbindListeners()
    {
        if (!listenersBound)
            return;
        continueButton?.onClick.RemoveListener(HandleContinue);
        retrySaveButton?.onClick.RemoveListener(HandleRetrySave);
        listenersBound = false;
    }

    private void HandleContinue() => ContinueRequested?.Invoke();
    private void HandleRetrySave() => RetrySaveRequested?.Invoke();
    private void OnEnable() => BindListeners();
    private void OnDisable() => UnbindListeners();
}
