using UnityEngine;

[CreateAssetMenu(fileName = "ArmorDefinition", menuName = "Game/Equipment/Armor Definition")]
public sealed class ArmorDefinition : EquipmentItemDefinition
{
    [SerializeField] private float physicalArmorModifier;
    [SerializeField] private float magicalResistanceModifier;

    public override EquipmentItemCategory Category => EquipmentItemCategory.Armor;
    public float PhysicalArmorModifier => physicalArmorModifier;
    public float MagicalResistanceModifier => magicalResistanceModifier;

    public override bool SupportsSlot(EquipmentSlotKind slot) =>
        slot == EquipmentSlotKind.Armor;

    public override SquadStatModifiers CreateStatModifiers() => new SquadStatModifiers
    {
        physicalArmor = physicalArmorModifier,
        magicalResistance = magicalResistanceModifier
    };

    protected override string ValidateDefinition()
    {
        if (physicalArmorModifier < 0f || magicalResistanceModifier < 0f)
            return $"Armor '{StableId}' has a negative development modifier.";
        return null;
    }

#if UNITY_EDITOR
    public void ConfigureDevelopment(string id, string label, string details,
        Sprite preview, float physicalArmor, float magicalResistance)
    {
        ConfigureCore(id, label, details, preview, null);
        physicalArmorModifier = Mathf.Max(0f, physicalArmor);
        magicalResistanceModifier = Mathf.Max(0f, magicalResistance);
    }
#endif
}
