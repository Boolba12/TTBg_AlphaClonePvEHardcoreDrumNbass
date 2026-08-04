using UnityEngine;
using UnityEngine.InputSystem;

public enum BattleAttackCategory
{
    Basic
}

public enum BattleDamageType
{
    Physical,
    Magical
}

public enum BattleAttackDelivery
{
    Melee,
    Ranged
}

public enum AttackScalingStat
{
    Strength,
    Dexterity,
    MagicalMastery
}

[CreateAssetMenu(
    fileName = "AttackDefinition",
    menuName = "Game/Battle/Attack Definition")]
public sealed class AttackDefinition : ScriptableObject
{
    [SerializeField] private string stableId;
    [SerializeField] private string displayName;
    [SerializeField] private BattleAttackCategory category = BattleAttackCategory.Basic;
    [SerializeField] private BattleDamageType damageType = BattleDamageType.Physical;
    [SerializeField] private SquadDamageDistribution distribution =
        SquadDamageDistribution.SingleTarget;
    [SerializeField] private BattleAttackDelivery delivery = BattleAttackDelivery.Melee;
    [SerializeField, Min(0)] private int baseDamage = 2;
    [SerializeField, Min(0)] private int actionPointCost = 2;
    [SerializeField, Min(0)] private int minimumRange = 1;
    [SerializeField, Min(0)] private int maximumRange = 1;
    [SerializeField] private AttackScalingStat primaryScalingStat = AttackScalingStat.Strength;
    [SerializeField, Min(0f)] private float primaryStatScaling = 0.5f;
    [SerializeField] private bool criticalEnabled = true;
    [SerializeField] private bool friendlyFire;
    [SerializeField] private Key hotkey = Key.A;
    [SerializeField] private BattleWeaponDefinition optionalWeaponReference;
    [SerializeField] private Sprite previewSprite;
    [SerializeField] private GameObject modelPrefab;

    public string StableId => stableId;
    public string DisplayName => displayName;
    public BattleAttackCategory Category => category;
    public BattleDamageType DamageType => damageType;
    public SquadDamageDistribution Distribution => distribution;
    public BattleAttackDelivery Delivery => delivery;
    public int BaseDamage => baseDamage;
    public int ActionPointCost => actionPointCost;
    public int MinimumRange => minimumRange;
    public int MaximumRange => maximumRange;
    public AttackScalingStat PrimaryScalingStat => primaryScalingStat;
    public float PrimaryStatScaling => primaryStatScaling;
    public bool CriticalEnabled => criticalEnabled;
    public bool FriendlyFire => friendlyFire;
    public Key Hotkey => hotkey;
    public BattleWeaponDefinition OptionalWeaponReference => optionalWeaponReference;
    public Sprite PreviewSprite => previewSprite != null
        ? previewSprite
        : optionalWeaponReference != null ? optionalWeaponReference.icon : null;
    public GameObject ModelPrefab => modelPrefab != null
        ? modelPrefab
        : optionalWeaponReference != null ? optionalWeaponReference.weaponPrefab : null;

    public bool Validate(out string reason)
    {
        if (string.IsNullOrWhiteSpace(stableId))
            reason = "Attack stable ID is missing.";
        else if (string.IsNullOrWhiteSpace(displayName))
            reason = "Attack display name is missing.";
        else if (maximumRange < minimumRange)
            reason = "Attack maximum range is below its minimum range.";
        else if (distribution != SquadDamageDistribution.SingleTarget)
            reason = "Only SingleTarget attacks are supported in this stage.";
        else if (damageType != BattleDamageType.Physical || delivery != BattleAttackDelivery.Melee)
            reason = "Only physical melee attacks are supported in this stage.";
        else
            reason = null;
        return reason == null;
    }

#if UNITY_EDITOR
    public void ConfigureDevelopment(
        string id,
        string configuredDisplayName,
        int configuredBaseDamage,
        int configuredActionPointCost,
        float strengthScaling,
        Sprite configuredPreview,
        GameObject configuredModel)
    {
        stableId = id;
        displayName = configuredDisplayName;
        category = BattleAttackCategory.Basic;
        damageType = BattleDamageType.Physical;
        distribution = SquadDamageDistribution.SingleTarget;
        delivery = BattleAttackDelivery.Melee;
        baseDamage = Mathf.Max(0, configuredBaseDamage);
        actionPointCost = Mathf.Max(0, configuredActionPointCost);
        minimumRange = 1;
        maximumRange = 1;
        primaryScalingStat = AttackScalingStat.Strength;
        primaryStatScaling = Mathf.Max(0f, strengthScaling);
        criticalEnabled = true;
        friendlyFire = false;
        hotkey = Key.A;
        optionalWeaponReference = null;
        previewSprite = configuredPreview;
        modelPrefab = configuredModel;
    }
#endif
}
