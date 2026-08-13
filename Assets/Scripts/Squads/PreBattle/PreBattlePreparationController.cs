using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class PreBattlePreparationController : MonoBehaviour
{
    [SerializeField] private SquadSaveParticipant squadRepository;
    [SerializeField] private CommanderPortraitDatabase portraitDatabase;
    [SerializeField] private PreBattlePreparationView view;
    [SerializeField] private TurnSystem turnSystem;
    [SerializeField] private EquipmentDefinitionCatalog equipmentCatalog;

    private IReadOnlyList<PreBattleSquadOption> options = Array.Empty<PreBattleSquadOption>();
    private CommanderPortraitService portraitService;
    private string selectedSquadId;
    private bool listenersBound;
    private SquadEquipmentService equipmentService;
    private EquipmentSlotKind selectedEquipmentSlot = EquipmentSlotKind.SquadWeapon;

    public bool IsOpen { get; private set; }
    public string SelectedSquadId => selectedSquadId;
    public IReadOnlyList<PreBattleSquadOption> Options => options;

    public void Configure(
        SquadSaveParticipant repository,
        CommanderPortraitDatabase portraits,
        PreBattlePreparationView preparationView,
        TurnSystem owner)
    {
        Configure(repository, portraits, preparationView, owner, null);
    }

    public void Configure(
        SquadSaveParticipant repository,
        CommanderPortraitDatabase portraits,
        PreBattlePreparationView preparationView,
        TurnSystem owner,
        EquipmentDefinitionCatalog configuredEquipmentCatalog)
    {
        UnbindView();
        squadRepository = repository;
        portraitDatabase = portraits;
        view = preparationView;
        turnSystem = owner;
        equipmentCatalog = configuredEquipmentCatalog;
        equipmentService = equipmentCatalog != null
            ? new SquadEquipmentService(equipmentCatalog) : null;
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

        options = PreBattleSquadSelectionService.BuildOptions(
            squadRepository.Squads, equipmentCatalog);
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
        selectedEquipmentSlot = EquipmentSlotKind.SquadWeapon;
        view.SetSelected(option, ResolvePortrait(option.PortraitId));
        RefreshEquipmentView();
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
        options = PreBattleSquadSelectionService.BuildOptions(
            squadRepository?.Squads, equipmentCatalog);
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
        view.EquipmentSlotSelected += HandleEquipmentSlotSelected;
        view.EquipmentItemSelected += HandleEquipmentItemSelected;
        view.UnequipRequested += HandleUnequip;
        listenersBound = true;
    }

    private void UnbindView()
    {
        if (!listenersBound || view == null)
            return;
        view.SquadSelected -= HandleSquadSelected;
        view.ConfirmRequested -= HandleConfirm;
        view.CancelRequested -= HandleCancel;
        view.EquipmentSlotSelected -= HandleEquipmentSlotSelected;
        view.EquipmentItemSelected -= HandleEquipmentItemSelected;
        view.UnequipRequested -= HandleUnequip;
        listenersBound = false;
    }

    private void HandleSquadSelected(string squadId) => TrySelectSquad(squadId, out _);
    private void HandleConfirm() => TryConfirmSelection(out _);
    private void HandleCancel() => Cancel();

    private void HandleEquipmentSlotSelected(EquipmentSlotKind slot)
    {
        selectedEquipmentSlot = slot;
        RefreshEquipmentView();
    }

    private void HandleEquipmentItemSelected(string instanceId) =>
        TryEquipSelectedItem(instanceId, out _);

    private void HandleUnequip() => TryUnequipSelected(out _);

    public bool TryEquipSelectedItem(string instanceId, out string reason)
    {
        SquadData squad = squadRepository?.GetSquad(selectedSquadId);
        EquipmentComparison comparison = equipmentService != null
            ? equipmentService.Compare(squad, instanceId, selectedEquipmentSlot)
            : default;
        EquipmentOperationResult result = equipmentService != null
            ? equipmentService.TryEquip(squad, instanceId, selectedEquipmentSlot)
            : new EquipmentOperationResult(EquipmentOperationFailure.MissingDefinition,
                "Equipment catalog is unavailable.");
        reason = result.Reason;
        if (!result.Success)
        {
            view?.SetValidation(reason, true);
            return false;
        }
        RefreshAfterEquipmentMutation(
            $"Equipment updated. Damage {FormatDelta(comparison.BaseDamageDelta)}, " +
            $"STR {FormatDelta(comparison.StrengthDelta)}, " +
            $"ACC {FormatPercentDelta(comparison.AccuracyDelta)}.");
        return true;
    }

    public bool TryUnequipSelected(out string reason)
    {
        EquipmentOperationResult result = equipmentService != null
            ? equipmentService.TryUnequip(
                squadRepository?.GetSquad(selectedSquadId), selectedEquipmentSlot)
            : new EquipmentOperationResult(EquipmentOperationFailure.MissingDefinition,
                "Equipment catalog is unavailable.");
        reason = result.Reason;
        if (!result.Success)
        {
            view?.SetValidation(reason, true);
            return false;
        }
        RefreshAfterEquipmentMutation("Equipment removed from the selected slot.");
        return true;
    }

    private void RefreshAfterEquipmentMutation(string message)
    {
        options = PreBattleSquadSelectionService.BuildOptions(
            squadRepository.Squads, equipmentCatalog);
        PreBattleSquadOption option = FindOption(selectedSquadId);
        view.SetSelected(option, ResolvePortrait(option?.PortraitId));
        RefreshEquipmentView();
        view.SetValidation(message, false);
    }

    private void RefreshEquipmentView() => view?.RenderEquipment(
        squadRepository?.GetSquad(selectedSquadId), equipmentCatalog, equipmentService,
        selectedEquipmentSlot);

    private static string FormatDelta(float value) => value >= 0f
        ? $"+{value:0.##}" : value.ToString("0.##");

    private static string FormatPercentDelta(float value) => value >= 0f
        ? $"+{value:P0}" : value.ToString("P0");

    private void Awake()
    {
        portraitService = portraitDatabase != null
            ? new CommanderPortraitService(portraitDatabase)
            : null;
        equipmentService = equipmentCatalog != null
            ? new SquadEquipmentService(equipmentCatalog) : null;
    }

    private void OnEnable() => BindView();
    private void OnDisable() => UnbindView();
}
