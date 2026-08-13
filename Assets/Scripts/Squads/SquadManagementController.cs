using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class SquadManagementController : MonoBehaviour
{
    [SerializeField] private Button openButton;
    [SerializeField] private SquadManagementView view;
    [SerializeField] private SquadSaveParticipant squadRepository;
    [SerializeField] private EquipmentDefinitionCatalog equipmentCatalog;
    [SerializeField] private CommanderPortraitDatabase portraitDatabase;
    [SerializeField] private List<PersistentDebuffDefinition> persistentDebuffs = new();
    [SerializeField] private TurnSystem turnSystem;
    [SerializeField] private SaveSystemBehaviour saveSystem;

    private SquadManagementService managementService;
    private CommanderPortraitService portraitService;
    private string selectedSquadId;
    private string selectedItemInstanceId;
    private EquipmentSlotKind selectedSlot = EquipmentSlotKind.SquadWeapon;
    private SquadManagementInventoryFilter filter = SquadManagementInventoryFilter.All;
    private bool listenersBound;

    public bool IsOpen => view != null && view.IsVisible;
    public string SelectedSquadId => selectedSquadId;
    public string SelectedItemInstanceId => selectedItemInstanceId;
    public EquipmentSlotKind SelectedSlot => selectedSlot;
    public SquadManagementInventoryFilter Filter => filter;
    public Button OpenButton => openButton;

    public void Configure(Button configuredOpenButton, SquadManagementView configuredView,
        SquadSaveParticipant repository, EquipmentDefinitionCatalog catalog,
        CommanderPortraitDatabase portraits, IEnumerable<PersistentDebuffDefinition> debuffs,
        TurnSystem configuredTurnSystem, SaveSystemBehaviour configuredSaveSystem)
    {
        UnbindListeners();
        openButton = configuredOpenButton;
        view = configuredView;
        squadRepository = repository;
        equipmentCatalog = catalog;
        portraitDatabase = portraits;
        persistentDebuffs = debuffs != null
            ? new List<PersistentDebuffDefinition>(debuffs)
            : new List<PersistentDebuffDefinition>();
        turnSystem = configuredTurnSystem;
        saveSystem = configuredSaveSystem;
        BuildServices();
        BindListeners();
        view?.Hide();
    }

    public bool TryOpen(out string reason)
    {
        if (IsOpen)
        {
            reason = null;
            return true;
        }
        BuildServices();
        if (managementService == null || view == null || portraitService == null)
        {
            reason = "Squad Management dependencies are unavailable.";
            return false;
        }
        if (turnSystem == null)
        {
            reason = "Overworld turn system is unavailable.";
            return false;
        }
        if (!turnSystem.TrySetExternalModalOpen(true, out reason))
            return false;

        IReadOnlyList<PreBattleSquadOption> options = managementService.BuildSquadOptions();
        view.Show(options, portraitService.GetDisplaySprite);
        selectedSquadId = options.Count > 0 ? options[0].SquadId : string.Empty;
        selectedSlot = EquipmentSlotKind.SquadWeapon;
        selectedItemInstanceId = string.Empty;
        filter = SquadManagementInventoryFilter.All;
        view.SetFilter(filter);
        RefreshAll();
        view.SetOperationStatus(options.Count > 0
            ? "Persistent squad data loaded. Composition is read-only in v1."
            : "No persistent Player squads are available.", options.Count == 0);
        reason = null;
        return true;
    }

    public void Close()
    {
        if (!IsOpen) return;
        view.Hide();
        selectedItemInstanceId = string.Empty;
        turnSystem?.TrySetExternalModalOpen(false, out _);
    }

    public bool TrySelectSquad(string squadId, out string reason)
    {
        if (squadRepository?.GetSquad(squadId) == null)
        {
            reason = $"Persistent squad '{squadId}' is unavailable.";
            return false;
        }
        selectedSquadId = squadId;
        selectedItemInstanceId = string.Empty;
        RefreshAll();
        reason = null;
        return true;
    }

    public bool TryEquipSelected(out string reason)
    {
        if (string.IsNullOrWhiteSpace(selectedItemInstanceId))
        {
            reason = "Select a compatible owned item first.";
            view?.SetOperationStatus(reason, true);
            return false;
        }
        EquipmentOperationResult result = managementService.TryEquip(selectedSquadId,
            selectedItemInstanceId, selectedSlot);
        reason = result.Reason;
        if (!result.Success)
        {
            view?.SetOperationStatus(reason, true);
            return false;
        }
        RefreshAll();
        view.SetOperationStatus("Equipment updated in the persistent squad state.", false);
        return true;
    }

    public bool TryUnequipSelected(out string reason)
    {
        EquipmentOperationResult result = managementService.TryUnequip(
            selectedSquadId, selectedSlot);
        reason = result.Reason;
        if (!result.Success)
        {
            view?.SetOperationStatus(reason, true);
            return false;
        }
        selectedItemInstanceId = string.Empty;
        RefreshAll();
        view.SetOperationStatus("Equipment removed from the persistent squad state.", false);
        return true;
    }

    private void HandleOpen()
    {
        if (!TryOpen(out string reason) && !string.IsNullOrWhiteSpace(reason))
            Debug.LogWarning($"SquadManagement: {reason}", this);
    }

    private void HandleSquadSelected(string squadId) => TrySelectSquad(squadId, out _);

    private void HandleSlotSelected(EquipmentSlotKind slot)
    {
        selectedSlot = slot;
        selectedItemInstanceId = string.Empty;
        RefreshAll();
    }

    private void HandleInventoryItemSelected(string instanceId)
    {
        selectedItemInstanceId = instanceId;
        RefreshInventory();
        EquipmentOperationResult result = managementService.PreviewEquip(
            selectedSquadId, instanceId, selectedSlot, out EquipmentStatComparison comparison);
        EquipmentItemDefinition definition = ResolveDefinition(instanceId);
        view.RenderItemPreview(definition, result.Success ? comparison : null);
        if (!result.Success)
            view.SetOperationStatus(result.Reason, true);
    }

    private void HandleFilterSelected(SquadManagementInventoryFilter selectedFilter)
    {
        filter = selectedFilter;
        selectedItemInstanceId = string.Empty;
        view.SetFilter(filter);
        RefreshInventory();
        view.RenderItemPreview(null, null);
    }

    private void HandleEquip() => TryEquipSelected(out _);
    private void HandleUnequip() => TryUnequipSelected(out _);

    private void HandleSave()
    {
        if (saveSystem == null || saveSystem.IsBusy)
        {
            view.SetOperationStatus("Save system is unavailable or busy.", true);
            return;
        }
        saveSystem.SaveGame();
        SaveOperationResult result = saveSystem.LastOperationResult;
        view.SetOperationStatus(result.Success
            ? "Squad equipment and persistent state saved."
            : $"Save failed: {result.Error}", !result.Success);
    }

    private void RefreshAll()
    {
        SquadManagementDetails details = managementService.BuildDetails(selectedSquadId);
        Sprite portrait = details != null
            ? portraitService.GetDisplaySprite(details.PortraitId) : null;
        view.RenderDetails(details, portrait,
            managementService.GetEquippedDefinition(selectedSquadId,
                EquipmentSlotKind.SquadWeapon),
            managementService.GetEquippedDefinition(selectedSquadId,
                EquipmentSlotKind.CommanderWeapon),
            managementService.GetEquippedDefinition(selectedSquadId,
                EquipmentSlotKind.Armor),
            managementService.GetEquippedDefinition(selectedSquadId,
                EquipmentSlotKind.Accessory), selectedSlot);
        view.SetUnequipAvailable(managementService.GetEquippedDefinition(
            selectedSquadId, selectedSlot) != null);
        RefreshInventory();
        view.RenderItemPreview(ResolveDefinition(selectedItemInstanceId), null);
    }

    private void RefreshInventory()
    {
        view.RenderInventory(managementService.BuildInventory(selectedSquadId,
            filter, selectedSlot), selectedItemInstanceId);
    }

    private EquipmentItemDefinition ResolveDefinition(string instanceId)
    {
        SquadData squad = squadRepository?.GetSquad(selectedSquadId);
        if (squad == null || string.IsNullOrWhiteSpace(instanceId)) return null;
        for (int i = 0; i < squad.Equipment.OwnedItems.Count; i++)
        {
            EquipmentItemInstance item = squad.Equipment.OwnedItems[i];
            if (item != null && string.Equals(item.InstanceId, instanceId,
                    StringComparison.Ordinal) && equipmentCatalog.TryGetDefinition(
                    item.DefinitionId, out EquipmentItemDefinition definition))
                return definition;
        }
        return null;
    }

    private void BuildServices()
    {
        managementService = squadRepository != null && equipmentCatalog != null
            ? new SquadManagementService(squadRepository, equipmentCatalog,
                persistentDebuffs)
            : null;
        portraitService = portraitDatabase != null
            ? new CommanderPortraitService(portraitDatabase) : null;
    }

    private void BindListeners()
    {
        if (listenersBound) return;
        openButton?.onClick.AddListener(HandleOpen);
        if (view != null)
        {
            view.SquadSelected += HandleSquadSelected;
            view.EquipmentSlotSelected += HandleSlotSelected;
            view.InventoryItemSelected += HandleInventoryItemSelected;
            view.FilterSelected += HandleFilterSelected;
            view.EquipRequested += HandleEquip;
            view.UnequipRequested += HandleUnequip;
            view.SaveRequested += HandleSave;
            view.CloseRequested += Close;
        }
        listenersBound = true;
    }

    private void UnbindListeners()
    {
        if (!listenersBound) return;
        openButton?.onClick.RemoveListener(HandleOpen);
        if (view != null)
        {
            view.SquadSelected -= HandleSquadSelected;
            view.EquipmentSlotSelected -= HandleSlotSelected;
            view.InventoryItemSelected -= HandleInventoryItemSelected;
            view.FilterSelected -= HandleFilterSelected;
            view.EquipRequested -= HandleEquip;
            view.UnequipRequested -= HandleUnequip;
            view.SaveRequested -= HandleSave;
            view.CloseRequested -= Close;
        }
        listenersBound = false;
    }

    private void Awake() => BuildServices();
    private void OnEnable() => BindListeners();
    private void OnDisable()
    {
        if (IsOpen) Close();
        UnbindListeners();
    }

    private void Update()
    {
        if (IsOpen && Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
            Close();
    }
}
