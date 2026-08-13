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
    private readonly IReadOnlyList<PersistentDebuffDefinition> debuffDefinitions;

    public SquadManagementService(SquadSaveParticipant configuredRepository,
        EquipmentDefinitionCatalog configuredCatalog,
        IReadOnlyList<PersistentDebuffDefinition> configuredDebuffs = null)
    {
        repository = configuredRepository;
        catalog = configuredCatalog;
        equipment = configuredCatalog != null
            ? new SquadEquipmentService(configuredCatalog) : null;
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
            Debuffs = debuffs
        };
    }

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
