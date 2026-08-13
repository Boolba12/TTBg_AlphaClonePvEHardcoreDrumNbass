using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class PreBattlePreparationController : MonoBehaviour
{
    [SerializeField] private SquadSaveParticipant squadRepository;
    [SerializeField] private CommanderPortraitDatabase portraitDatabase;
    [SerializeField] private PreBattlePreparationView view;
    [SerializeField] private TurnSystem turnSystem;

    private IReadOnlyList<PreBattleSquadOption> options = Array.Empty<PreBattleSquadOption>();
    private CommanderPortraitService portraitService;
    private string selectedSquadId;
    private bool listenersBound;

    public bool IsOpen { get; private set; }
    public string SelectedSquadId => selectedSquadId;
    public IReadOnlyList<PreBattleSquadOption> Options => options;

    public void Configure(
        SquadSaveParticipant repository,
        CommanderPortraitDatabase portraits,
        PreBattlePreparationView preparationView,
        TurnSystem owner)
    {
        UnbindView();
        squadRepository = repository;
        portraitDatabase = portraits;
        view = preparationView;
        turnSystem = owner;
        portraitService = portraitDatabase != null
            ? new CommanderPortraitService(portraitDatabase)
            : null;
        BindView();
        view?.Hide();
    }

    public bool TryOpenForActiveEncounter(out string reason)
    {
        if (IsOpen)
        {
            reason = "Pre-Battle preparation is already open.";
            return false;
        }
        if (!BattleEncounterContext.HasEncounterData ||
            string.IsNullOrWhiteSpace(BattleEncounterContext.EncounterId))
        {
            reason = "Active encounter data is unavailable.";
            return false;
        }
        if (squadRepository == null || view == null)
        {
            reason = "Pre-Battle preparation references are incomplete.";
            return false;
        }

        options = PreBattleSquadSelectionService.BuildOptions(squadRepository.Squads);
        selectedSquadId = null;
        view.Show(options, ResolvePortrait, BuildEncounterSummary());
        IsOpen = true;
        reason = null;
        return true;
    }

    public bool TrySelectSquad(string squadId, out string reason)
    {
        if (!IsOpen)
        {
            reason = "Pre-Battle preparation is not open.";
            return false;
        }

        PreBattleSquadOption option = FindOption(squadId);
        if (option == null || !option.IsAvailable)
        {
            reason = option?.UnavailableMessage ?? "Selected squad is not part of the persistent roster.";
            view.SetValidation(reason, true);
            return false;
        }

        selectedSquadId = option.SquadId;
        view.SetSelected(option, ResolvePortrait(option.PortraitId));
        view.SetValidation("Squad ready. Confirm to enter battle.", false);
        reason = null;
        return true;
    }

    public bool TryConfirmSelection(out string reason)
    {
        if (!IsOpen)
        {
            reason = "Pre-Battle preparation is not open.";
            return false;
        }
        if (!PreBattleSquadSelectionService.TryResolveEligible(
                squadRepository,
                selectedSquadId,
                out _,
                out reason))
        {
            RefreshOptionsAfterValidationFailure(reason);
            return false;
        }
        if (turnSystem == null || !turnSystem.ConfirmPreBattleSelection(selectedSquadId, out reason))
        {
            view.SetValidation(reason ?? "Battle transition could not be started.", true);
            return false;
        }

        CloseFromTurnSystem();
        return true;
    }

    public void Cancel()
    {
        if (!IsOpen)
            return;
        if (turnSystem != null)
            turnSystem.CancelPreBattlePreparation();
        else
        {
            BattleSquadSelectionContext.Clear();
            BattleEncounterContext.Clear();
            CloseFromTurnSystem();
        }
    }

    public void CloseFromTurnSystem()
    {
        IsOpen = false;
        selectedSquadId = null;
        view?.Hide();
    }

    private void RefreshOptionsAfterValidationFailure(string failureReason)
    {
        options = PreBattleSquadSelectionService.BuildOptions(squadRepository?.Squads);
        PreBattleSquadOption current = FindOption(selectedSquadId);
        if (current == null || !current.IsAvailable)
            selectedSquadId = null;
        view.Show(options, ResolvePortrait, BuildEncounterSummary());
        view.SetValidation(failureReason, true);
    }

    private PreBattleSquadOption FindOption(string squadId)
    {
        if (string.IsNullOrWhiteSpace(squadId))
            return null;
        for (int i = 0; i < options.Count; i++)
        {
            if (string.Equals(options[i].SquadId, squadId, StringComparison.Ordinal))
                return options[i];
        }
        return null;
    }

    private Sprite ResolvePortrait(string portraitId) =>
        portraitService?.GetDisplaySprite(portraitId);

    private static string BuildEncounterSummary()
    {
        return
            $"HOSTILE ENCOUNTER\n" +
            $"Initiative: {BattleEncounterContext.Initiator}\n" +
            $"Terrain: {BattleEncounterContext.PlayerBiome} / {BattleEncounterContext.EnemyBiome}\n" +
            "Enemy formation: Intelligence unavailable\n" +
            "Conditions: Standard tactical engagement";
    }

    private void BindView()
    {
        if (listenersBound || view == null)
            return;
        view.SquadSelected += HandleSquadSelected;
        view.ConfirmRequested += HandleConfirm;
        view.CancelRequested += HandleCancel;
        listenersBound = true;
    }

    private void UnbindView()
    {
        if (!listenersBound || view == null)
            return;
        view.SquadSelected -= HandleSquadSelected;
        view.ConfirmRequested -= HandleConfirm;
        view.CancelRequested -= HandleCancel;
        listenersBound = false;
    }

    private void HandleSquadSelected(string squadId) => TrySelectSquad(squadId, out _);
    private void HandleConfirm() => TryConfirmSelection(out _);
    private void HandleCancel() => Cancel();

    private void Awake()
    {
        portraitService = portraitDatabase != null
            ? new CommanderPortraitService(portraitDatabase)
            : null;
    }

    private void OnEnable() => BindView();
    private void OnDisable() => UnbindView();
}
