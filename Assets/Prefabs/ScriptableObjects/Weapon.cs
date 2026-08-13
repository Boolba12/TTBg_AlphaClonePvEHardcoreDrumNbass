using UnityEngine;

public enum WeaponClass
{
    Sword,
    Dagger,
    Estoc,
    Falchion,
    Greatsword,
    Mace,
    Other
}

/// <summary>
/// Canonical persistent weapon definition. Ownership stores only this stable ID;
/// battle runtime copies the scalar combat profile into an immutable snapshot.
/// </summary>
[CreateAssetMenu(fileName = "Weapon", menuName = "Game/Equipment/Weapon Definition")]
public class Weapon : EquipmentItemDefinition
{
    [SerializeField] private WeaponClass weaponClass = WeaponClass.Other;

    [Header("Compatibility")]
    [SerializeField] private bool supportsSquadWeapon = true;
    [SerializeField] private bool supportsCommanderWeapon = true;

    [Header("Development combat profile")]
    [SerializeField] private int baseDamageBonus;
    [SerializeField] private float primaryScalingBonus;
    [SerializeField] private float strengthBonus;
    [SerializeField] private float accuracyBonus;
    [SerializeField] private float criticalChanceBonus;
    [SerializeField] private float criticalDamageBonus;

    public WeaponClass Class => weaponClass;
    public override EquipmentItemCategory Category => EquipmentItemCategory.Weapon;
    public GameObject WeaponPrefab => ModelPrefab;
    public int BaseDamageBonus => baseDamageBonus;
    public float PrimaryScalingBonus => primaryScalingBonus;
    public float StrengthBonus => strengthBonus;
    public float AccuracyBonus => accuracyBonus;
    public float CriticalChanceBonus => criticalChanceBonus;
    public float CriticalDamageBonus => criticalDamageBonus;

    public override bool SupportsSlot(EquipmentSlotKind slot) => slot switch
    {
        EquipmentSlotKind.SquadWeapon => supportsSquadWeapon,
        EquipmentSlotKind.CommanderWeapon => supportsCommanderWeapon,
        _ => false
    };

    protected override string ValidateDefinition()
    {
        string reason;
        if (!supportsSquadWeapon && !supportsCommanderWeapon)
            reason = $"Weapon '{StableId}' has no compatible equipment slot.";
        else if (baseDamageBonus < 0 || primaryScalingBonus < 0f ||
                 strengthBonus < 0f || accuracyBonus < 0f ||
                 criticalChanceBonus < 0f || criticalDamageBonus < 0f)
            reason = $"Weapon '{StableId}' has a negative development combat value.";
        else
            reason = null;
        return reason;
    }

    public override SquadStatModifiers CreateStatModifiers() => new SquadStatModifiers
    {
        strength = strengthBonus,
        accuracy = accuracyBonus,
        criticalChance = criticalChanceBonus,
        criticalDamage = criticalDamageBonus
    };

#if UNITY_EDITOR
    public void ConfigureDevelopment(
        string id,
        string configuredDisplayName,
        string configuredDescription,
        WeaponClass configuredClass,
        Sprite configuredPreview,
        GameObject configuredPrefab,
        int configuredDamageBonus,
        float configuredScalingBonus,
        float configuredStrengthBonus,
        float configuredAccuracyBonus,
        float configuredCriticalChanceBonus,
        float configuredCriticalDamageBonus)
    {
        ConfigureCore(id, configuredDisplayName, configuredDescription,
            configuredPreview, configuredPrefab);
        weaponClass = configuredClass;
        supportsSquadWeapon = true;
        supportsCommanderWeapon = true;
        baseDamageBonus = Mathf.Max(0, configuredDamageBonus);
        primaryScalingBonus = Mathf.Max(0f, configuredScalingBonus);
        strengthBonus = Mathf.Max(0f, configuredStrengthBonus);
        accuracyBonus = Mathf.Max(0f, configuredAccuracyBonus);
        criticalChanceBonus = Mathf.Max(0f, configuredCriticalChanceBonus);
        criticalDamageBonus = Mathf.Max(0f, configuredCriticalDamageBonus);
    }
#endif
}
