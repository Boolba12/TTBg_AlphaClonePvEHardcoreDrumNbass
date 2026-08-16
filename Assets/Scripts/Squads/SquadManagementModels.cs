using System;
using System.Collections.Generic;

public enum SquadManagementInventoryFilter
{
    All,
    Weapons,
    Armor,
    Accessories
}

public sealed class SquadManagementInventoryEntry
{
    public SquadManagementInventoryEntry(EquipmentItemInstance instance,
        EquipmentItemDefinition definition, bool equipped, bool compatible)
    {
        Instance = instance;
        Definition = definition;
        Equipped = equipped;
        Compatible = compatible;
    }

    public EquipmentItemInstance Instance { get; }
    public EquipmentItemDefinition Definition { get; }
    public bool Equipped { get; }
    public bool Compatible { get; }
}

public readonly struct SquadManagementDebuffEntry
{
    public SquadManagementDebuffEntry(string stableId, string displayName,
        string description, string sourceBattleId)
    {
        StableId = stableId ?? string.Empty;
        DisplayName = displayName ?? stableId ?? string.Empty;
        Description = description ?? string.Empty;
        SourceBattleId = sourceBattleId ?? string.Empty;
    }

    public string StableId { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public string SourceBattleId { get; }
}

public sealed class SquadManagementDetails
{
    public string SquadId { get; set; }
    public string CommanderId { get; set; }
    public string PortraitId { get; set; }
    public CommanderRace Race { get; set; }
    public PersistentSquadStatus Status { get; set; }
    public SquadCalculatedStats Stats { get; set; }
    public IReadOnlyList<WarriorData> Warriors { get; set; }
    public IReadOnlyList<SquadManagementDebuffEntry> Debuffs { get; set; }
    public bool BattleReady { get; set; }
    public int ReserveCount { get; set; }
}

public enum SquadManagementWarriorStatus
{
    Assigned,
    Reserve
}

public sealed class SquadManagementWarriorEntry
{
    public SquadManagementWarriorEntry(
        WarriorData warrior,
        SquadManagementWarriorStatus status)
    {
        Warrior = warrior;
        Status = status;
    }

    public WarriorData Warrior { get; }
    public string WarriorId => Warrior?.id ?? string.Empty;
    public SquadManagementWarriorStatus Status { get; }
}

/// <summary>
/// Read-only domain projection used by the production management UI.
/// It never owns squads, inventory or calculated formulas.
/// </summary>
public sealed class SquadManagementService
{
    private readonly SquadSaveParticipant repository;
    private readonly EquipmentDefinitionCatalog catalog;
    private readonly SquadEquipmentService equipment;
    private readonly SquadRosterService roster;
    private readonly IReadOnlyList<PersistentDebuffDefinition> debuffDefinitions;

    public SquadManagementService(SquadSaveParticipant configuredRepository,
        EquipmentDefinitionCatalog configuredCatalog,
        IReadOnlyList<PersistentDebuffDefinition> configuredDebuffs = null)
    {
        repository = configuredRepository;
        catalog = configuredCatalog;
        equipment = configuredCatalog != null
            ? new SquadEquipmentService(configuredCatalog) : null;
        roster = new SquadRosterService(configuredRepository, configuredCatalog);
        debuffDefinitions = configuredDebuffs ?? Array.Empty<PersistentDebuffDefinition>();
    }

    public IReadOnlyList<PreBattleSquadOption> BuildSquadOptions() =>
        PreBattleSquadSelectionService.BuildOptions(repository?.Squads, catalog);

    public SquadManagementDetails BuildDetails(string squadId)
    {
        SquadData squad = repository?.GetSquad(squadId);
        if (squad == null)
            return null;
        List<SquadManagementDebuffEntry> debuffs = new();
        if (squad.Commander?.permanentDebuffs != null)
        {
            for (int i = 0; i < squad.Commander.permanentDebuffs.Count; i++)
            {
                PersistentDebuffRecord record = squad.Commander.permanentDebuffs[i];
                if (record == null) continue;
                PersistentDebuffDefinition definition = FindDebuff(record.debuffId);
                debuffs.Add(new SquadManagementDebuffEntry(record.debuffId,
                    definition?.DisplayName ?? record.debuffId,
                    definition?.Description ?? "Persistent effect definition unavailable.",
                    record.sourceBattleId));
            }
        }
        return new SquadManagementDetails
        {
            SquadId = squad.Id,
            CommanderId = squad.Commander?.id ?? string.Empty,
            PortraitId = squad.CommanderPortraitId,
            Race = squad.Commander?.race ?? default,
            Status = squad.Status,
            Stats = SquadStatsCalculator.Calculate(squad, catalog),
            Warriors = squad.Warriors,
            Debuffs = debuffs,
            BattleReady = PreBattleSquadSelectionService.Evaluate(
                squad, out _, out _),
            ReserveCount = repository.ReserveWarriors.Count
        };
    }

    public IReadOnlyList<SquadManagementWarriorEntry> BuildAssignedWarriors(string squadId)
    {
        List<SquadManagementWarriorEntry> entries = new();
        SquadData squad = repository?.GetSquad(squadId);
        for (int i = 0; squad?.Warriors != null && i < squad.Warriors.Count; i++)
            entries.Add(new SquadManagementWarriorEntry(
                squad.Warriors[i], SquadManagementWarriorStatus.Assigned));
        return entries;
    }

    public IReadOnlyList<SquadManagementWarriorEntry> BuildReserveWarriors()
    {
        List<SquadManagementWarriorEntry> entries = new();
        for (int i = 0; repository?.ReserveWarriors != null &&
                        i < repository.ReserveWarriors.Count; i++)
            entries.Add(new SquadManagementWarriorEntry(
                repository.ReserveWarriors[i], SquadManagementWarriorStatus.Reserve));
        return entries;
    }

    public SquadRosterOperationResult TryAddWarrior(string squadId, string warriorId) =>
        roster.TryAddWarrior(squadId, warriorId);

    public SquadRosterOperationResult TryRemoveWarrior(string squadId, string warriorId) =>
        roster.TryRemoveWarrior(squadId, warriorId);

    public SquadRosterOperationResult TryRotateWarrior(
        string squadId,
        string assignedWarriorId,
        string reserveWarriorId) =>
        roster.TryRotateWarrior(squadId, assignedWarriorId, reserveWarriorId);

    public SquadRosterOperationResult PreviewAdd(
        string squadId,
        string warriorId,
        out SquadCompositionStatPreview preview) =>
        roster.PreviewAdd(squadId, warriorId, out preview);

    public SquadRosterOperationResult PreviewRemove(
        string squadId,
        string warriorId,
        out SquadCompositionStatPreview preview) =>
        roster.PreviewRemove(squadId, warriorId, out preview);

    public IReadOnlyList<SquadManagementInventoryEntry> BuildInventory(string squadId,
        SquadManagementInventoryFilter filter, EquipmentSlotKind selectedSlot)
    {
        SquadData squad = repository?.GetSquad(squadId);
        List<SquadManagementInventoryEntry> entries = new();
        if (squad?.Equipment?.OwnedItems == null || catalog == null)
            return entries;
        for (int i = 0; i < squad.Equipment.OwnedItems.Count; i++)
        {
            EquipmentItemInstance instance = squad.Equipment.OwnedItems[i];
            if (instance == null ||
                !catalog.TryGetDefinition(instance.DefinitionId, out EquipmentItemDefinition definition) ||
                !MatchesFilter(definition.Category, filter))
                continue;
            entries.Add(new SquadManagementInventoryEntry(instance, definition,
                IsEquipped(squad, instance.InstanceId), definition.SupportsSlot(selectedSlot)));
        }
        entries.Sort((left, right) =>
        {
            int category = left.Definition.Category.CompareTo(right.Definition.Category);
            if (category != 0) return category;
            int display = string.Compare(left.Definition.DisplayName,
                right.Definition.DisplayName, StringComparison.Ordinal);
            return display != 0 ? display : string.Compare(left.Instance.InstanceId,
                right.Instance.InstanceId, StringComparison.Ordinal);
        });
        return entries;
    }

    public EquipmentOperationResult PreviewEquip(string squadId, string instanceId,
        EquipmentSlotKind slot, out EquipmentStatComparison comparison) =>
        equipment != null
            ? equipment.PreviewEquip(repository?.GetSquad(squadId), instanceId, slot,
                out comparison)
            : MissingCatalog(out comparison);

    public EquipmentOperationResult TryEquip(string squadId, string instanceId,
        EquipmentSlotKind slot) => equipment != null
        ? equipment.TryEquip(repository?.GetSquad(squadId), instanceId, slot)
        : new EquipmentOperationResult(EquipmentOperationFailure.MissingDefinition,
            "Equipment catalog is unavailable.");

    public EquipmentOperationResult TryUnequip(string squadId, EquipmentSlotKind slot) =>
        equipment != null
            ? equipment.TryUnequip(repository?.GetSquad(squadId), slot)
            : new EquipmentOperationResult(EquipmentOperationFailure.MissingDefinition,
                "Equipment catalog is unavailable.");

    public EquipmentItemDefinition GetEquippedDefinition(string squadId,
        EquipmentSlotKind slot) => equipment?.ResolveEquippedDefinition(
            repository?.GetSquad(squadId), slot);

    private EquipmentOperationResult MissingCatalog(out EquipmentStatComparison comparison)
    {
        comparison = default;
        return new EquipmentOperationResult(EquipmentOperationFailure.MissingDefinition,
            "Equipment catalog is unavailable.");
    }

    private PersistentDebuffDefinition FindDebuff(string stableId)
    {
        for (int i = 0; i < debuffDefinitions.Count; i++)
        {
            PersistentDebuffDefinition definition = debuffDefinitions[i];
            if (definition != null && string.Equals(definition.StableId, stableId,
                    StringComparison.Ordinal))
                return definition;
        }
        return null;
    }

    private static bool MatchesFilter(EquipmentItemCategory category,
        SquadManagementInventoryFilter filter) => filter switch
    {
        SquadManagementInventoryFilter.Weapons => category == EquipmentItemCategory.Weapon,
        SquadManagementInventoryFilter.Armor => category == EquipmentItemCategory.Armor,
        SquadManagementInventoryFilter.Accessories => category == EquipmentItemCategory.Accessory,
        _ => true
    };

    private static bool IsEquipped(SquadData squad, string instanceId)
    {
        foreach (EquipmentSlotKind slot in new[] { EquipmentSlotKind.SquadWeapon,
                     EquipmentSlotKind.CommanderWeapon, EquipmentSlotKind.Armor,
                     EquipmentSlotKind.Accessory })
            if (string.Equals(squad.Equipment.GetEquippedInstanceId(slot), instanceId,
                    StringComparison.Ordinal))
                return true;
        return false;
    }
}
