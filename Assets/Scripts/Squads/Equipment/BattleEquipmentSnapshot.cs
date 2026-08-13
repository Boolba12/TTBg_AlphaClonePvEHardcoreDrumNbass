using System;
using UnityEngine;

public sealed class WeaponCombatSnapshot
{
    public WeaponCombatSnapshot(Weapon definition)
    {
        if (definition == null) return;
        DefinitionId = definition.StableId;
        DisplayName = definition.DisplayName;
        Class = definition.Class;
        PreviewSprite = definition.PreviewSprite;
        ModelPrefab = definition.WeaponPrefab;
        BaseDamageBonus = definition.BaseDamageBonus;
        PrimaryScalingBonus = definition.PrimaryScalingBonus;
        StrengthBonus = definition.StrengthBonus;
        AccuracyBonus = definition.AccuracyBonus;
        CriticalChanceBonus = definition.CriticalChanceBonus;
        CriticalDamageBonus = definition.CriticalDamageBonus;
    }
    public string DefinitionId { get; } = string.Empty;
    public string DisplayName { get; } = string.Empty;
    public WeaponClass Class { get; }
    public Sprite PreviewSprite { get; }
    public GameObject ModelPrefab { get; }
    public int BaseDamageBonus { get; }
    public float PrimaryScalingBonus { get; }
    public float StrengthBonus { get; }
    public float AccuracyBonus { get; }
    public float CriticalChanceBonus { get; }
    public float CriticalDamageBonus { get; }
}

public sealed class BattleEquipmentSnapshot
{
    public static readonly BattleEquipmentSnapshot Empty =
        new BattleEquipmentSnapshot(null, null, null, null);

    public BattleEquipmentSnapshot(Weapon squadWeapon, Weapon commanderWeapon) :
        this(squadWeapon, commanderWeapon, null, null) { }

    public BattleEquipmentSnapshot(Weapon squadWeapon, Weapon commanderWeapon,
        ArmorDefinition armor, AccessoryDefinition accessory)
    {
        SquadWeapon = squadWeapon != null ? new WeaponCombatSnapshot(squadWeapon) : null;
        CommanderWeapon = commanderWeapon != null ? new WeaponCombatSnapshot(commanderWeapon) : null;
        ArmorDefinitionId = armor?.StableId ?? string.Empty;
        AccessoryDefinitionId = accessory?.StableId ?? string.Empty;
        SquadStatModifiers weaponModifiers = SquadStatModifiers.Combine(
            squadWeapon?.CreateStatModifiers(), commanderWeapon?.CreateStatModifiers());
        StatModifiers = SquadStatModifiers.Combine(weaponModifiers,
            SquadStatModifiers.Combine(armor?.CreateStatModifiers(),
                accessory?.CreateStatModifiers()));
    }
    public WeaponCombatSnapshot SquadWeapon { get; }
    public WeaponCombatSnapshot CommanderWeapon { get; }
    public string ArmorDefinitionId { get; }
    public string AccessoryDefinitionId { get; }
    public SquadStatModifiers StatModifiers { get; }

    public WeaponCombatSnapshot GetWeaponForAttack(AttackDefinition definition)
    {
        return definition?.WeaponSlot == EquipmentSlotKind.CommanderWeapon
            ? CommanderWeapon
            : SquadWeapon;
    }

    public static bool TryCreate(SquadData squad, EquipmentDefinitionCatalog catalog,
        out BattleEquipmentSnapshot snapshot, out string reason)
    {
        snapshot = Empty;
        if (squad == null) { reason = "Squad data is missing."; return false; }
        if (catalog == null)
        {
            bool empty = string.IsNullOrWhiteSpace(squad.Equipment.SquadWeaponInstanceId) &&
                         string.IsNullOrWhiteSpace(squad.Equipment.CommanderWeaponInstanceId) &&
                         string.IsNullOrWhiteSpace(squad.Equipment.ArmorInstanceId) &&
                         string.IsNullOrWhiteSpace(squad.Equipment.AccessoryInstanceId);
            reason = empty ? null : "Equipment catalog is missing for an equipped squad.";
            return empty;
        }
        SquadEquipmentService service = new SquadEquipmentService(catalog);
        Weapon squadWeapon = service.ResolveEquippedWeapon(squad, EquipmentSlotKind.SquadWeapon);
        Weapon commanderWeapon = service.ResolveEquippedWeapon(squad, EquipmentSlotKind.CommanderWeapon);
        ArmorDefinition armor = service.ResolveEquippedDefinition(
            squad, EquipmentSlotKind.Armor) as ArmorDefinition;
        AccessoryDefinition accessory = service.ResolveEquippedDefinition(
            squad, EquipmentSlotKind.Accessory) as AccessoryDefinition;
        if (!string.IsNullOrWhiteSpace(squad.Equipment.SquadWeaponInstanceId) && squadWeapon == null)
        { reason = "Squad Weapon snapshot could not resolve its owned definition."; return false; }
        if (!string.IsNullOrWhiteSpace(squad.Equipment.CommanderWeaponInstanceId) && commanderWeapon == null)
        { reason = "Commander Weapon snapshot could not resolve its owned definition."; return false; }
        if (!string.IsNullOrWhiteSpace(squad.Equipment.ArmorInstanceId) && armor == null)
        { reason = "Armor snapshot could not resolve its owned definition."; return false; }
        if (!string.IsNullOrWhiteSpace(squad.Equipment.AccessoryInstanceId) && accessory == null)
        { reason = "Accessory snapshot could not resolve its owned definition."; return false; }
        snapshot = new BattleEquipmentSnapshot(squadWeapon, commanderWeapon, armor, accessory);
        reason = null;
        return true;
    }
}
