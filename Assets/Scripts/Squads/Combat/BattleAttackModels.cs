using System;
using System.Collections.Generic;

public enum BattleAttackFailureReason
{
    None,
    ServiceNotInitialized,
    MissingDefinition,
    InvalidDefinition,
    BattleNotStarted,
    BattleCompleted,
    InvalidAttacker,
    AttackerDefeated,
    AttackerNotActive,
    AttackerNotHumanControlled,
    AttackerNotPlayerSide,
    AttackerNotSelected,
    MovementInProgress,
    AttackInProgress,
    InsufficientActionPoints,
    NoTargetsInRange,
    InvalidTarget,
    SelfTarget,
    FriendlyTarget,
    TargetDefeated,
    TargetOutOfRange,
    RuntimeFailure
}

public readonly struct BattleAttackValidationResult
{
    public BattleAttackValidationResult(
        BattleAttackFailureReason failureReason,
        string reason)
    {
        FailureReason = failureReason;
        Reason = reason;
    }

    public bool IsValid => FailureReason == BattleAttackFailureReason.None;
    public BattleAttackFailureReason FailureReason { get; }
    public string Reason { get; }

    public static BattleAttackValidationResult Accepted =>
        new BattleAttackValidationResult(BattleAttackFailureReason.None, null);

    public static BattleAttackValidationResult Reject(
        BattleAttackFailureReason reason,
        string message) => new BattleAttackValidationResult(reason, message);
}

public readonly struct BattleDamageCalculation
{
    public BattleDamageCalculation(
        float rawDamage,
        float mitigatedDamage,
        int appliedDamage,
        float armorReduction)
    {
        RawDamage = Math.Max(0f, rawDamage);
        MitigatedDamage = Math.Max(0f, mitigatedDamage);
        AppliedDamage = Math.Max(0, appliedDamage);
        ArmorReduction = Math.Max(0f, armorReduction);
    }

    public float RawDamage { get; }
    public float MitigatedDamage { get; }
    public int AppliedDamage { get; }
    public float ArmorReduction { get; }
}

public readonly struct BattleAttackPreview
{
    public BattleAttackPreview(
        string attackerId,
        string targetId,
        string attackId,
        BattleAttackValidationResult validation,
        int actionPointCost,
        float hitChance,
        float criticalChance,
        int predictedDamage,
        int predictedCriticalDamage,
        BattleDamageType damageType,
        int targetCurrentHealth,
        int targetMaximumHealth,
        int targetLivingWarriors)
    {
        AttackerId = attackerId ?? string.Empty;
        TargetId = targetId ?? string.Empty;
        AttackId = attackId ?? string.Empty;
        Validation = validation;
        ActionPointCost = Math.Max(0, actionPointCost);
        HitChance = Math.Clamp(hitChance, 0f, 1f);
        CriticalChance = Math.Clamp(criticalChance, 0f, 1f);
        PredictedDamage = Math.Max(0, predictedDamage);
        PredictedCriticalDamage = Math.Max(0, predictedCriticalDamage);
        DamageType = damageType;
        TargetCurrentHealth = Math.Max(0, targetCurrentHealth);
        TargetMaximumHealth = Math.Max(0, targetMaximumHealth);
        TargetLivingWarriors = Math.Max(0, targetLivingWarriors);
    }

    public string AttackerId { get; }
    public string TargetId { get; }
    public string AttackId { get; }
    public BattleAttackValidationResult Validation { get; }
    public bool IsValid => Validation.IsValid;
    public int ActionPointCost { get; }
    public float HitChance { get; }
    public float CriticalChance { get; }
    public int PredictedDamage { get; }
    public int PredictedCriticalDamage { get; }
    public BattleDamageType DamageType { get; }
    public int TargetCurrentHealth { get; }
    public int TargetMaximumHealth { get; }
    public int TargetLivingWarriors { get; }
}

[Serializable]
public sealed class BattleAttackResult
{
    private readonly List<string> defeatedWarriorIds = new List<string>();

    public string AttackerId { get; internal set; }
    public string TargetId { get; internal set; }
    public string AttackId { get; internal set; }
    public bool WasExecuted { get; internal set; }
    public int ActionPointsSpent { get; internal set; }
    public bool Hit { get; internal set; }
    public bool Critical { get; internal set; }
    public float RawDamage { get; internal set; }
    public float MitigatedDamage { get; internal set; }
    public int AppliedDamage { get; internal set; }
    public IReadOnlyList<string> DefeatedWarriorIds => defeatedWarriorIds;
    public bool CommanderDamaged { get; internal set; }
    public bool CommanderDefeated { get; internal set; }
    public bool SquadDefeated { get; internal set; }
    public BattleAttackFailureReason FailureReason { get; internal set; }
    public string FailureMessage { get; internal set; }
    public bool Succeeded => WasExecuted && FailureReason == BattleAttackFailureReason.None;

    internal void AddDefeatedWarriors(IReadOnlyList<string> ids)
    {
        if (ids == null)
            return;
        for (int i = 0; i < ids.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(ids[i]) && !defeatedWarriorIds.Contains(ids[i]))
                defeatedWarriorIds.Add(ids[i]);
        }
    }
}
