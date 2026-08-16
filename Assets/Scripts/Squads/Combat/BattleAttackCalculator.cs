using UnityEngine;

public sealed class BattleAttackCalculator
{
    private readonly BattleCombatRules rules;

    public BattleAttackCalculator(BattleCombatRules configuredRules)
    {
        rules = configuredRules;
    }

    public float CalculateHitChance(
        SquadCalculatedStats attacker,
        SquadCalculatedStats target,
        WeaponCombatSnapshot weapon = null,
        float rangeModifier = 0f,
        float coverModifier = 0f,
        float otherModifier = 0f)
    {
        if (rules == null)
            return 0f;
        return Mathf.Clamp(
            rules.BaseHitChance + attacker.Accuracy - target.Evasion +
            rangeModifier + coverModifier + otherModifier,
            rules.MinimumHitChance,
            rules.MaximumHitChance);
    }

    public float CalculateCriticalChance(
        SquadCalculatedStats attacker,
        AttackDefinition definition)
    {
        return definition != null && definition.CriticalEnabled
            ? Mathf.Clamp01(attacker.CriticalChance)
            : 0f;
    }

    public float CalculateRawDamage(
        SquadCalculatedStats attacker,
        AttackDefinition definition,
        bool critical,
        WeaponCombatSnapshot weapon = null)
    {
        if (definition == null)
            return 0f;

        float scalingStat = definition.PrimaryScalingStat switch
        {
            AttackScalingStat.Strength => attacker.Strength,
            AttackScalingStat.Dexterity => attacker.Dexterity,
            AttackScalingStat.MagicalMastery => attacker.MagicalMastery,
            _ => 0f
        };
        float raw = Mathf.Max(
            0f,
            definition.BaseDamage + (weapon?.BaseDamageBonus ?? 0) +
            scalingStat * (definition.PrimaryStatScaling +
                           (weapon?.PrimaryScalingBonus ?? 0f)));
        if (critical)
            raw *= Mathf.Max(1f, attacker.CriticalDamage);
        return raw;
    }

    public BattleDamageCalculation CalculateDamage(
        SquadCalculatedStats attacker,
        SquadCalculatedStats target,
        AttackDefinition definition,
        bool critical,
        WeaponCombatSnapshot weapon = null)
    {
        float raw = CalculateRawDamage(attacker, definition, critical, weapon);
        float reduction = definition != null &&
                          definition.DamageType == BattleDamageType.Physical &&
                          rules != null
            ? Mathf.Clamp(target.PhysicalArmor, 0f, rules.MaximumPhysicalArmorReduction)
            : 0f;
        float mitigated = Mathf.Max(0f, raw * (1f - reduction));
        int rounded = RoundDamage(mitigated);
        if (raw > 0f && rules != null)
            rounded = Mathf.Max(rules.MinimumDamageOnHit, rounded);
        return new BattleDamageCalculation(raw, mitigated, rounded, reduction);
    }

    /// <summary>
    /// Damage is rounded to the nearest integer; an exact .5 is rounded upward.
    /// </summary>
    public static int RoundDamage(float value)
    {
        return Mathf.Max(0, Mathf.FloorToInt(Mathf.Max(0f, value) + 0.5f));
    }
}
