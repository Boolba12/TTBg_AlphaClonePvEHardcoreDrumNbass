using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public enum EquipmentSlotKind
{
    SquadWeapon,
    CommanderWeapon,
    Armor,
    Accessory
}

[Serializable]
public sealed class EquipmentItemInstance
{
    [SerializeField] private string instanceId;
    [SerializeField] private string definitionId;

    public string InstanceId => instanceId;
    public string DefinitionId => definitionId;

    public EquipmentItemInstance(string configuredInstanceId, string configuredDefinitionId)
    {
        instanceId = configuredInstanceId;
        definitionId = configuredDefinitionId;
    }
}

[Serializable]
public sealed class SquadEquipmentState
{
    [SerializeField] private List<EquipmentItemInstance> ownedItems =
        new List<EquipmentItemInstance>();
    [SerializeField] private string squadWeaponInstanceId;
    [SerializeField] private string commanderWeaponInstanceId;
    [SerializeField] private string armorInstanceId;
    [SerializeField] private string accessoryInstanceId;

    public IReadOnlyList<EquipmentItemInstance> OwnedItems => ownedItems;
    public string SquadWeaponInstanceId => squadWeaponInstanceId;
    public string CommanderWeaponInstanceId => commanderWeaponInstanceId;
    public string ArmorInstanceId => armorInstanceId;
    public string AccessoryInstanceId => accessoryInstanceId;

    public void EnsureInitialized() => ownedItems ??= new List<EquipmentItemInstance>();

    public string GetEquippedInstanceId(EquipmentSlotKind slot) => slot switch
    {
        EquipmentSlotKind.SquadWeapon => squadWeaponInstanceId,
        EquipmentSlotKind.CommanderWeapon => commanderWeaponInstanceId,
        EquipmentSlotKind.Armor => armorInstanceId,
        EquipmentSlotKind.Accessory => accessoryInstanceId,
        _ => string.Empty
    };

    internal void SetEquippedInstanceId(EquipmentSlotKind slot, string instanceId)
    {
        switch (slot)
        {
            case EquipmentSlotKind.SquadWeapon:
                squadWeaponInstanceId = instanceId;
                break;
            case EquipmentSlotKind.CommanderWeapon:
                commanderWeaponInstanceId = instanceId;
                break;
            case EquipmentSlotKind.Armor:
                armorInstanceId = instanceId;
                break;
            case EquipmentSlotKind.Accessory:
                accessoryInstanceId = instanceId;
                break;
        }
    }

    internal bool TryAddOwnedItem(EquipmentItemInstance item, out string reason)
    {
        EnsureInitialized();
        if (item == null || string.IsNullOrWhiteSpace(item.InstanceId) ||
            string.IsNullOrWhiteSpace(item.DefinitionId))
        {
            reason = "Equipment instance identity is incomplete.";
            return false;
        }
        for (int i = 0; i < ownedItems.Count; i++)
        {
            if (ownedItems[i] != null &&
                string.Equals(ownedItems[i].InstanceId, item.InstanceId, StringComparison.Ordinal))
            {
                reason = $"Duplicate equipment instance ID '{item.InstanceId}'.";
                return false;
            }
        }
        ownedItems.Add(item);
        reason = null;
        return true;
    }
}

public enum EquipmentOperationFailure
{
    None,
    MissingSquad,
    MissingInstance,
    DuplicateInstance,
    MissingDefinition,
    IncompatibleSlot,
    AlreadyEquippedElsewhere,
    UnsupportedSlot
}

public readonly struct EquipmentOperationResult
{
    public EquipmentOperationResult(EquipmentOperationFailure failure, string reason)
    {
        Failure = failure;
        Reason = reason;
    }
    public EquipmentOperationFailure Failure { get; }
    public string Reason { get; }
    public bool Success => Failure == EquipmentOperationFailure.None;
    public static EquipmentOperationResult Ok =>
        new EquipmentOperationResult(EquipmentOperationFailure.None, null);
}

public readonly struct EquipmentComparison
{
    public EquipmentComparison(EquipmentSlotKind slot, string currentDefinitionId,
        string candidateDefinitionId, float strengthDelta, float accuracyDelta,
        float criticalChanceDelta, float criticalDamageDelta, int baseDamageDelta,
        float scalingDelta)
    {
        Slot = slot;
        CurrentDefinitionId = currentDefinitionId ?? string.Empty;
        CandidateDefinitionId = candidateDefinitionId ?? string.Empty;
        StrengthDelta = strengthDelta;
        AccuracyDelta = accuracyDelta;
        CriticalChanceDelta = criticalChanceDelta;
        CriticalDamageDelta = criticalDamageDelta;
        BaseDamageDelta = baseDamageDelta;
        ScalingDelta = scalingDelta;
    }
    public EquipmentSlotKind Slot { get; }
    public string CurrentDefinitionId { get; }
    public string CandidateDefinitionId { get; }
    public float StrengthDelta { get; }
    public float AccuracyDelta { get; }
    public float CriticalChanceDelta { get; }
    public float CriticalDamageDelta { get; }
    public int BaseDamageDelta { get; }
    public float ScalingDelta { get; }
}

public readonly struct EquipmentStatComparison
{
    public EquipmentStatComparison(EquipmentSlotKind slot,
        string currentDefinitionId, string candidateDefinitionId,
        SquadCalculatedStats currentStats, SquadCalculatedStats candidateStats)
    {
        Slot = slot;
        CurrentDefinitionId = currentDefinitionId ?? string.Empty;
        CandidateDefinitionId = candidateDefinitionId ?? string.Empty;
        CurrentStats = currentStats;
        CandidateStats = candidateStats;
    }

    public EquipmentSlotKind Slot { get; }
    public string CurrentDefinitionId { get; }
    public string CandidateDefinitionId { get; }
    public SquadCalculatedStats CurrentStats { get; }
    public SquadCalculatedStats CandidateStats { get; }
}
