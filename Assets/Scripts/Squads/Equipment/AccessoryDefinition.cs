using UnityEngine;

[CreateAssetMenu(fileName = "AccessoryDefinition",
    menuName = "Game/Equipment/Accessory Definition")]
public sealed class AccessoryDefinition : EquipmentItemDefinition
{
    [SerializeField] private float resolveModifier;
    [SerializeField] private float initiativeModifier;
    [SerializeField] private float accuracyModifier;
    [SerializeField] private float criticalChanceModifier;

    public override EquipmentItemCategory Category => EquipmentItemCategory.Accessory;
    public float ResolveModifier => resolveModifier;
    public float InitiativeModifier => initiativeModifier;
    public float AccuracyModifier => accuracyModifier;
    public float CriticalChanceModifier => criticalChanceModifier;

    public override bool SupportsSlot(EquipmentSlotKind slot) =>
        slot == EquipmentSlotKind.Accessory;

    public override SquadStatModifiers CreateStatModifiers() => new SquadStatModifiers
    {
        resolve = resolveModifier,
        initiative = initiativeModifier,
        accuracy = accuracyModifier,
        criticalChance = criticalChanceModifier
    };

    protected override string ValidateDefinition()
    {
        if (resolveModifier < 0f || initiativeModifier < 0f ||
            accuracyModifier < 0f || criticalChanceModifier < 0f)
            return $"Accessory '{StableId}' has a negative development modifier.";
        return null;
    }

#if UNITY_EDITOR
    public void ConfigureDevelopment(string id, string label, string details,
        Sprite preview, float resolve, float initiative, float accuracy,
        float criticalChance)
    {
        ConfigureCore(id, label, details, preview, null);
        resolveModifier = Mathf.Max(0f, resolve);
        initiativeModifier = Mathf.Max(0f, initiative);
        accuracyModifier = Mathf.Max(0f, accuracy);
        criticalChanceModifier = Mathf.Max(0f, criticalChance);
    }
#endif
}
