using System;
using System.Collections.Generic;

public enum BattleAbilityFailureReason
{
    None,
    ServiceNotInitialized,
    BattleCompleted,
    MissingDefinition,
    InvalidDefinition,
    InvalidCaster,
    CasterDefeated,
    CasterNotActive,
    CasterNotHumanControlled,
    CasterNotPlayerSide,
    CasterNotSelected,
    MovementInProgress,
    CommandInProgress,
    InsufficientActionPoints,
    CooldownActive,
    InvalidTarget,
    NoTargetsInRange,
    MoraleAlreadyFull,
    RuntimeFailure
}

public readonly struct BattleAbilityValidationResult
{
    public BattleAbilityValidationResult(
        BattleAbilityFailureReason failureReason,
        string reason)
    {
        FailureReason = failureReason;
        Reason = reason;
    }

    public BattleAbilityFailureReason FailureReason { get; }
    public string Reason { get; }
    public bool IsValid => FailureReason == BattleAbilityFailureReason.None;

    public static BattleAbilityValidationResult Accepted =>
        new BattleAbilityValidationResult(BattleAbilityFailureReason.None, null);

    public static BattleAbilityValidationResult Reject(
        BattleAbilityFailureReason reason,
        string message) => new BattleAbilityValidationResult(reason, message);
}

[Serializable]
public sealed class BattleAbilityRuntimeState
{
    public string squadId;
    public string abilityId;
    public int remainingCooldown;
    public int usesThisBattle;

    public bool IsAvailable => remainingCooldown <= 0;

    internal void BeginCooldown(int rounds)
    {
        remainingCooldown = Math.Max(0, rounds);
        usesThisBattle++;
    }

    internal bool AdvanceOwnerTurn()
    {
        if (remainingCooldown <= 0)
            return false;
        remainingCooldown--;
        return true;
    }
}

public readonly struct BattleAbilityPreview
{
    public BattleAbilityPreview(
        string casterId,
        string targetId,
        string abilityId,
        BattleAbilityValidationResult validation,
        int actionPointCost,
        int remainingCooldown,
        BattleAttackPreview attackPreview,
        float currentMorale,
        float maximumMorale,
        float predictedMoraleRestore)
    {
        CasterId = casterId ?? string.Empty;
        TargetId = targetId ?? string.Empty;
        AbilityId = abilityId ?? string.Empty;
        Validation = validation;
        ActionPointCost = Math.Max(0, actionPointCost);
        RemainingCooldown = Math.Max(0, remainingCooldown);
        AttackPreview = attackPreview;
        CurrentMorale = Math.Max(0f, currentMorale);
        MaximumMorale = Math.Max(0f, maximumMorale);
        PredictedMoraleRestore = Math.Max(0f, predictedMoraleRestore);
    }

    public string CasterId { get; }
    public string TargetId { get; }
    public string AbilityId { get; }
    public BattleAbilityValidationResult Validation { get; }
    public bool IsValid => Validation.IsValid;
    public int ActionPointCost { get; }
    public int RemainingCooldown { get; }
    public BattleAttackPreview AttackPreview { get; }
    public float CurrentMorale { get; }
    public float MaximumMorale { get; }
    public float PredictedMoraleRestore { get; }
}

[Serializable]
public sealed class BattleAbilityResult
{
    private readonly List<string> defeatedWarriorIds = new List<string>();

    public string CasterSquadId { get; internal set; }
    public string AbilityId { get; internal set; }
    public string TargetSquadId { get; internal set; }
    public string WeaponDefinitionId { get; internal set; }
    public bool WasExecuted { get; internal set; }
    public int ActionPointsSpent { get; internal set; }
    public int CooldownApplied { get; internal set; }
    public bool Hit { get; internal set; }
    public bool Critical { get; internal set; }
    public int Damage { get; internal set; }
    public IReadOnlyList<string> DefeatedWarriorIds => defeatedWarriorIds;
    public float MoraleRestored { get; internal set; }
    public bool CommanderDefeated { get; internal set; }
    public bool SquadDefeated { get; internal set; }
    public bool ProgressionIncreased { get; internal set; }
    public BattleAbilityFailureReason FailureReason { get; internal set; }
    public string FailureMessage { get; internal set; }
    public bool Succeeded => WasExecuted && FailureReason == BattleAbilityFailureReason.None;

    internal void CopyAttackResult(BattleAttackResult attack)
    {
        if (attack == null)
            return;
        WasExecuted = attack.WasExecuted;
        ActionPointsSpent = attack.ActionPointsSpent;
        Hit = attack.Hit;
        Critical = attack.Critical;
        Damage = attack.AppliedDamage;
        WeaponDefinitionId = attack.WeaponDefinitionId;
        CommanderDefeated = attack.CommanderDefeated;
        SquadDefeated = attack.SquadDefeated;
        foreach (string id in attack.DefeatedWarriorIds)
        {
            if (!string.IsNullOrWhiteSpace(id) && !defeatedWarriorIds.Contains(id))
                defeatedWarriorIds.Add(id);
        }
    }
}
