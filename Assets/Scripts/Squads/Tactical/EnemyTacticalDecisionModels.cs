using System;
using UnityEngine;

public enum EnemyTacticalActionType
{
    None,
    BasicAttack,
    PowerStrike,
    SweepingBlow,
    Rally,
    MoveToAttack,
    EndTurn
}

public sealed class EnemyTacticalDecision
{
    public EnemyTacticalActionType ActionType { get; }
    public SquadBattleController Actor { get; }
    public SquadBattleController Target { get; }
    public AbilityDefinition Ability { get; }
    public SquadMovementPlan MovementPlan { get; }
    public string Reason { get; }

    public Vector2Int Destination => MovementPlan?.Destination ?? default;
    public int PathCost => MovementPlan?.ActionPointCost ?? 0;

    private EnemyTacticalDecision(
        EnemyTacticalActionType actionType,
        SquadBattleController actor,
        SquadBattleController target,
        AbilityDefinition ability,
        SquadMovementPlan movementPlan,
        string reason)
    {
        ActionType = actionType;
        Actor = actor;
        Target = target;
        Ability = ability;
        MovementPlan = movementPlan;
        Reason = reason ?? string.Empty;
    }

    public static EnemyTacticalDecision Attack(
        SquadBattleController actor,
        SquadBattleController target) =>
        new EnemyTacticalDecision(
            EnemyTacticalActionType.BasicAttack,
            actor,
            target,
            null,
            null,
            "A valid basic attack has first offensive priority.");

    public static EnemyTacticalDecision UseAbility(
        EnemyTacticalActionType actionType,
        SquadBattleController actor,
        SquadBattleController target,
        AbilityDefinition ability,
        string reason) =>
        new EnemyTacticalDecision(
            actionType,
            actor,
            target,
            ability,
            null,
            reason);

    public static EnemyTacticalDecision Move(
        SquadBattleController actor,
        SquadBattleController target,
        SquadMovementPlan movementPlan) =>
        new EnemyTacticalDecision(
            EnemyTacticalActionType.MoveToAttack,
            actor,
            target,
            null,
            movementPlan,
            "Move through the production movement pipeline to the nearest valid attack cell.");

    public static EnemyTacticalDecision End(
        SquadBattleController actor,
        string reason) =>
        new EnemyTacticalDecision(
            EnemyTacticalActionType.EndTurn,
            actor,
            null,
            null,
            null,
            reason);
}

[Serializable]
public sealed class EnemyTacticalTurnSummary
{
    public string squadId;
    public string selectedTargetId;
    public string lastDecision;
    public string endReason;
    public int actionCount;
    public int movementCount;
    public int basicAttackCount;
    public int abilityCount;
    public int actionPointsAtStart;
    public int actionPointsAtEnd;
    public Vector2Int selectedDestination;
    public int selectedPathCost;
}
