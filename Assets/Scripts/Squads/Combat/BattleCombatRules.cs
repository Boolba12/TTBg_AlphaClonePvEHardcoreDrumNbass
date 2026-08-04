using UnityEngine;

[CreateAssetMenu(
    fileName = "BattleCombatRules",
    menuName = "Game/Battle/Combat Rules")]
public sealed class BattleCombatRules : ScriptableObject
{
    [Header("Hit chance (normalized 0..1)")]
    [SerializeField, Range(0f, 1f)] private float baseHitChance = 0.75f;
    [SerializeField, Range(0f, 1f)] private float minimumHitChance = 0.05f;
    [SerializeField, Range(0f, 1f)] private float maximumHitChance = 0.95f;

    [Header("Physical mitigation (normalized 0..1)")]
    [SerializeField, Range(0f, 1f)] private float maximumPhysicalArmorReduction = 0.8f;
    [SerializeField, Min(0)] private int minimumDamageOnHit = 1;

    public float BaseHitChance => Mathf.Clamp01(baseHitChance);
    public float MinimumHitChance => Mathf.Clamp01(minimumHitChance);
    public float MaximumHitChance => Mathf.Clamp(maximumHitChance, MinimumHitChance, 1f);
    public float MaximumPhysicalArmorReduction =>
        Mathf.Clamp01(maximumPhysicalArmorReduction);
    public int MinimumDamageOnHit => Mathf.Max(0, minimumDamageOnHit);

#if UNITY_EDITOR
    public void ConfigureDevelopment(
        float configuredBaseHitChance,
        float configuredMinimumHitChance,
        float configuredMaximumHitChance,
        float configuredMaximumArmorReduction,
        int configuredMinimumDamage)
    {
        baseHitChance = Mathf.Clamp01(configuredBaseHitChance);
        minimumHitChance = Mathf.Clamp01(configuredMinimumHitChance);
        maximumHitChance = Mathf.Clamp(
            configuredMaximumHitChance,
            minimumHitChance,
            1f);
        maximumPhysicalArmorReduction = Mathf.Clamp01(configuredMaximumArmorReduction);
        minimumDamageOnHit = Mathf.Max(0, configuredMinimumDamage);
    }
#endif
}
