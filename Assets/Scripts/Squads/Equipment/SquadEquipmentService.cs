using System;

/// <summary>
/// Sole domain owner for persistent equipment grants, compatibility, atomic
/// equip/unequip and non-mutating calculated-stat previews.
/// </summary>
public sealed class SquadEquipmentService
{
    private static readonly EquipmentSlotKind[] Slots =
    {
        EquipmentSlotKind.SquadWeapon,
        EquipmentSlotKind.CommanderWeapon,
        EquipmentSlotKind.Armor,
        EquipmentSlotKind.Accessory
    };

    private readonly EquipmentDefinitionCatalog catalog;
    public SquadEquipmentService(EquipmentDefinitionCatalog definitionCatalog) =>
        catalog = definitionCatalog;

    public EquipmentOperationResult GrantOwnedItem(SquadData squad, string instanceId,
        string definitionId)
    {
        if (squad == null)
            return Fail(EquipmentOperationFailure.MissingSquad, "Squad is missing.");
        if (catalog == null || !catalog.TryGetDefinition(definitionId, out _))
            return Fail(EquipmentOperationFailure.MissingDefinition,
                $"Equipment definition '{definitionId}' is unavailable.");
        return squad.Equipment.TryAddOwnedItem(
            new EquipmentItemInstance(instanceId, definitionId), out string reason)
            ? EquipmentOperationResult.Ok
            : Fail(EquipmentOperationFailure.DuplicateInstance, reason);
    }

    public EquipmentOperationResult GrantOwnedWeapon(SquadData squad, string instanceId,
        string definitionId) => GrantOwnedItem(squad, instanceId, definitionId);

    public EquipmentOperationResult TryEquip(SquadData squad, string instanceId,
        EquipmentSlotKind slot)
    {
        EquipmentOperationResult validation = ValidateEquip(squad, instanceId, slot,
            out _, out _);
        if (!validation.Success)
            return validation;
        squad.Equipment.SetEquippedInstanceId(slot, instanceId);
        return EquipmentOperationResult.Ok;
    }

    public EquipmentOperationResult TryUnequip(SquadData squad, EquipmentSlotKind slot)
    {
        if (squad == null)
            return Fail(EquipmentOperationFailure.MissingSquad, "Squad is missing.");
        if (!IsSupportedSlot(slot))
            return Fail(EquipmentOperationFailure.UnsupportedSlot,
                "Equipment slot is unsupported.");
        squad.Equipment.SetEquippedInstanceId(slot, null);
        return EquipmentOperationResult.Ok;
    }

    public EquipmentOperationResult ValidateEquip(SquadData squad, string instanceId,
        EquipmentSlotKind slot, out EquipmentItemInstance instance,
        out EquipmentItemDefinition definition)
    {
        instance = null;
        definition = null;
        if (squad == null)
            return Fail(EquipmentOperationFailure.MissingSquad, "Squad is missing.");
        if (!IsSupportedSlot(slot))
            return Fail(EquipmentOperationFailure.UnsupportedSlot,
                $"{slot} is unsupported.");
        instance = FindOwned(squad, instanceId);
        if (instance == null)
            return Fail(EquipmentOperationFailure.MissingInstance,
                $"Equipment instance '{instanceId}' is not owned by squad '{squad.Id}'.");
        if (catalog == null || !catalog.TryGetDefinition(instance.DefinitionId, out definition))
            return Fail(EquipmentOperationFailure.MissingDefinition,
                $"Equipment definition '{instance.DefinitionId}' is unavailable.");
        if (!definition.SupportsSlot(slot))
            return Fail(EquipmentOperationFailure.IncompatibleSlot,
                $"{definition.DisplayName} is incompatible with {slot}.");

        for (int i = 0; i < Slots.Length; i++)
        {
            EquipmentSlotKind other = Slots[i];
            if (other != slot && string.Equals(
                    squad.Equipment.GetEquippedInstanceId(other), instanceId,
                    StringComparison.Ordinal))
                return Fail(EquipmentOperationFailure.AlreadyEquippedElsewhere,
                    $"Equipment instance '{instanceId}' is already equipped in {other}.");
        }
        return EquipmentOperationResult.Ok;
    }

    public EquipmentComparison Compare(SquadData squad, string candidateInstanceId,
        EquipmentSlotKind slot)
    {
        Weapon current = ResolveEquippedWeapon(squad, slot);
        EquipmentItemInstance instance = FindOwned(squad, candidateInstanceId);
        Weapon candidate = null;
        if (instance != null)
            catalog?.TryGetWeapon(instance.DefinitionId, out candidate);
        return new EquipmentComparison(slot, current?.StableId, candidate?.StableId,
            (candidate?.StrengthBonus ?? 0f) - (current?.StrengthBonus ?? 0f),
            (candidate?.AccuracyBonus ?? 0f) - (current?.AccuracyBonus ?? 0f),
            (candidate?.CriticalChanceBonus ?? 0f) - (current?.CriticalChanceBonus ?? 0f),
            (candidate?.CriticalDamageBonus ?? 0f) - (current?.CriticalDamageBonus ?? 0f),
            (candidate?.BaseDamageBonus ?? 0) - (current?.BaseDamageBonus ?? 0),
            (candidate?.PrimaryScalingBonus ?? 0f) - (current?.PrimaryScalingBonus ?? 0f));
    }

    public EquipmentOperationResult PreviewEquip(SquadData squad, string instanceId,
        EquipmentSlotKind slot, out EquipmentStatComparison comparison)
    {
        comparison = default;
        EquipmentOperationResult validation = ValidateEquip(squad, instanceId, slot,
            out _, out EquipmentItemDefinition candidate);
        if (!validation.Success)
            return validation;

        EquipmentItemDefinition current = ResolveEquippedDefinition(squad, slot);
        SquadCalculatedStats currentStats = SquadStatsCalculator.Calculate(
            squad, null, BuildEquippedStatModifiers(squad));
        SquadCalculatedStats candidateStats = SquadStatsCalculator.Calculate(
            squad, null, BuildEquippedStatModifiers(squad, slot, candidate));
        comparison = new EquipmentStatComparison(slot, current?.StableId,
            candidate.StableId, currentStats, candidateStats);
        return EquipmentOperationResult.Ok;
    }

    public EquipmentItemDefinition ResolveEquippedDefinition(SquadData squad,
        EquipmentSlotKind slot)
    {
        EquipmentItemInstance item = FindOwned(squad,
            squad?.Equipment.GetEquippedInstanceId(slot));
        return item != null && catalog != null &&
               catalog.TryGetDefinition(item.DefinitionId,
                   out EquipmentItemDefinition definition)
            ? definition : null;
    }

    public Weapon ResolveEquippedWeapon(SquadData squad, EquipmentSlotKind slot) =>
        ResolveEquippedDefinition(squad, slot) as Weapon;

    public SquadStatModifiers BuildEquippedStatModifiers(SquadData squad) =>
        BuildEquippedStatModifiers(squad, null, null);

    private SquadStatModifiers BuildEquippedStatModifiers(SquadData squad,
        EquipmentSlotKind? overrideSlot, EquipmentItemDefinition overrideDefinition)
    {
        SquadStatModifiers result = new SquadStatModifiers();
        for (int i = 0; i < Slots.Length; i++)
        {
            EquipmentSlotKind slot = Slots[i];
            EquipmentItemDefinition definition = overrideSlot == slot
                ? overrideDefinition
                : ResolveEquippedDefinition(squad, slot);
            result = SquadStatModifiers.Combine(result, definition?.CreateStatModifiers());
        }
        return result;
    }

    public EquipmentItemInstance FindOwnedItem(SquadData squad, string instanceId) =>
        FindOwned(squad, instanceId);

    private static EquipmentItemInstance FindOwned(SquadData squad, string instanceId)
    {
        if (squad?.Equipment?.OwnedItems == null || string.IsNullOrWhiteSpace(instanceId))
            return null;
        for (int i = 0; i < squad.Equipment.OwnedItems.Count; i++)
        {
            EquipmentItemInstance item = squad.Equipment.OwnedItems[i];
            if (item != null && string.Equals(item.InstanceId, instanceId,
                    StringComparison.Ordinal))
                return item;
        }
        return null;
    }

    private static bool IsSupportedSlot(EquipmentSlotKind slot) =>
        slot >= EquipmentSlotKind.SquadWeapon && slot <= EquipmentSlotKind.Accessory;

    private static EquipmentOperationResult Fail(EquipmentOperationFailure failure,
        string reason) => new EquipmentOperationResult(failure, reason);
}
