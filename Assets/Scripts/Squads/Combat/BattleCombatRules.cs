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

    [Header("Directional cover hit modifiers")]
    [SerializeField, Range(-1f, 0f)] private float halfCoverHitModifier = -0.20f;
    [SerializeField, Range(-1f, 0f)] private float fullCoverHitModifier = -0.40f;

    [Header("Physical mitigation (normalized 0..1)")]
    [SerializeField, Range(0f, 1f)] private float maximumPhysicalArmorReduction = 0.8f;
    [SerializeField, Min(0)] private int minimumDamageOnHit = 1;

    public float BaseHitChance => Mathf.Clamp01(baseHitChance);
    public float MinimumHitChance => Mathf.Clamp01(minimumHitChance);
    public float MaximumHitChance => Mathf.Clamp(maximumHitChance, MinimumHitChance, 1f);
    public float MaximumPhysicalArmorReduction =>
        Mathf.Clamp01(maximumPhysicalArmorReduction);
    public int MinimumDamageOnHit => Mathf.Max(0, minimumDamageOnHit);
    public float HalfCoverHitModifier => Mathf.Clamp(halfCoverHitModifier, -1f, 0f);
    public float FullCoverHitModifier => Mathf.Clamp(fullCoverHitModifier, -1f, 0f);

    public float GetCoverHitModifier(CoverType cover) => cover switch
    {
        CoverType.Half => HalfCoverHitModifier,
        CoverType.Full => FullCoverHitModifier,
        _ => 0f
    };

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

    public void ConfigureDevelopmentCover(float configuredHalfCoverModifier,
        float configuredFullCoverModifier)
    {
        halfCoverHitModifier = Mathf.Clamp(configuredHalfCoverModifier, -1f, 0f);
        fullCoverHitModifier = Mathf.Clamp(configuredFullCoverModifier, -1f, 0f);
    }
#endif
}
