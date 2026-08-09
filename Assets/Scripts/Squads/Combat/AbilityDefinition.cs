using UnityEngine;
using UnityEngine.InputSystem;

public enum BattleAbilityTargetType
{
    Self,
    EnemySquad
}

public enum BattleAbilityEffectType
{
    PhysicalAttack,
    RestoreMorale
}

[CreateAssetMenu(
    fileName = "AbilityDefinition",
    menuName = "Game/Battle/Ability Definition")]
public sealed class AbilityDefinition : ScriptableObject
{
    [SerializeField] private string stableId;
    [SerializeField] private string displayName;
    [SerializeField, TextArea] private string description;
    [SerializeField] private Sprite icon;
    [SerializeField, Min(0)] private int actionPointCost;
    [SerializeField, Min(0)] private int cooldownRounds;
    [SerializeField] private BattleAbilityTargetType targetType;
    [SerializeField, Min(0)] private int minimumRange;
    [SerializeField, Min(0)] private int maximumRange;
    [SerializeField] private BattleAbilityEffectType effectType;
    [SerializeField] private AttackDefinition attackEffect;
    [SerializeField, Min(0f)] private float moraleRestore;
    [SerializeField] private bool hasProgressionStat;
    [SerializeField] private PrimaryStatType progressionStat;
    [SerializeField] private Key hotkey = Key.None;
    [SerializeField] private bool developmentOnly = true;

    public string StableId => stableId;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon != null ? icon : attackEffect?.PreviewSprite;
    public int ActionPointCost => actionPointCost;
    public int CooldownRounds => cooldownRounds;
    public BattleAbilityTargetType TargetType => targetType;
    public int MinimumRange => minimumRange;
    public int MaximumRange => maximumRange;
    public BattleAbilityEffectType EffectType => effectType;
    public AttackDefinition AttackEffect => attackEffect;
    public float MoraleRestore => moraleRestore;
    public bool HasProgressionStat => hasProgressionStat;
    public PrimaryStatType ProgressionStat => progressionStat;
    public Key Hotkey => hotkey;
    public bool DevelopmentOnly => developmentOnly;
    public SquadDamageDistribution DamageDistribution =>
        attackEffect != null
            ? attackEffect.Distribution
            : SquadDamageDistribution.SingleTarget;
    public BattleDamageType DamageType =>
        attackEffect != null ? attackEffect.DamageType : BattleDamageType.Physical;

    public bool Validate(out string reason)
    {
        if (string.IsNullOrWhiteSpace(stableId))
            reason = "Ability stable ID is missing.";
        else if (string.IsNullOrWhiteSpace(displayName))
            reason = "Ability display name is missing.";
        else if (maximumRange < minimumRange)
            reason = "Ability maximum range is below its minimum range.";
        else if (hotkey == Key.None)
            reason = "Ability hotkey is missing.";
        else if (effectType == BattleAbilityEffectType.PhysicalAttack &&
                 targetType != BattleAbilityTargetType.EnemySquad)
            reason = "Damaging ability requires an enemy-squad target.";
        else if (effectType == BattleAbilityEffectType.PhysicalAttack &&
                 attackEffect == null)
            reason = "Damaging ability requires an attack effect.";
        else if (effectType == BattleAbilityEffectType.PhysicalAttack &&
                 !attackEffect.Validate(out reason))
            reason = reason ?? "Damaging ability attack effect is invalid.";
        else if (effectType == BattleAbilityEffectType.PhysicalAttack &&
                 (attackEffect.ActionPointCost != actionPointCost ||
                  attackEffect.MinimumRange != minimumRange ||
                  attackEffect.MaximumRange != maximumRange))
            reason = "Ability AP/range must match its attack effect.";
        else if (effectType == BattleAbilityEffectType.RestoreMorale &&
                 (targetType != BattleAbilityTargetType.Self || moraleRestore <= 0f))
            reason = "Morale ability requires a positive self-target restore.";
        else
            reason = null;
        return reason == null;
    }

#if UNITY_EDITOR
    public void ConfigureDevelopmentAttack(
        string id,
        string configuredName,
        string configuredDescription,
        int configuredActionPointCost,
        int configuredCooldown,
        Key configuredHotkey,
        AttackDefinition configuredAttack,
        Sprite configuredIcon)
    {
        stableId = id;
        displayName = configuredName;
        description = configuredDescription;
        icon = configuredIcon;
        actionPointCost = Mathf.Max(0, configuredActionPointCost);
        cooldownRounds = Mathf.Max(0, configuredCooldown);
        targetType = BattleAbilityTargetType.EnemySquad;
        minimumRange = 1;
        maximumRange = 1;
        effectType = BattleAbilityEffectType.PhysicalAttack;
        attackEffect = configuredAttack;
        moraleRestore = 0f;
        hasProgressionStat = true;
        progressionStat = PrimaryStatType.Strength;
        hotkey = configuredHotkey;
        developmentOnly = true;
    }

    public void ConfigureDevelopmentRally(
        string id,
        string configuredName,
        string configuredDescription,
        int configuredActionPointCost,
        int configuredCooldown,
        float configuredMoraleRestore,
        Key configuredHotkey,
        Sprite configuredIcon)
    {
        stableId = id;
        displayName = configuredName;
        description = configuredDescription;
        icon = configuredIcon;
        actionPointCost = Mathf.Max(0, configuredActionPointCost);
        cooldownRounds = Mathf.Max(0, configuredCooldown);
        targetType = BattleAbilityTargetType.Self;
        minimumRange = 0;
        maximumRange = 0;
        effectType = BattleAbilityEffectType.RestoreMorale;
        attackEffect = null;
        moraleRestore = Mathf.Max(0f, configuredMoraleRestore);
        hasProgressionStat = false;
        hotkey = configuredHotkey;
        developmentOnly = true;
    }
#endif
}
