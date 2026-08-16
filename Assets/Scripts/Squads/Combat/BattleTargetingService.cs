using System.Collections.Generic;
using UnityEngine;

public sealed class BattleTargetingService
{
    private readonly bool allowDiagonalRange;
    private readonly GridLineOfSightService lineOfSightService;
    private readonly GridCoverService coverService;

    public BattleTargetingService(bool allowDiagonal)
        : this(allowDiagonal, null, null)
    {
    }

    public BattleTargetingService(bool allowDiagonal,
        GridLineOfSightService configuredLineOfSightService,
        GridCoverService configuredCoverService)
    {
        allowDiagonalRange = allowDiagonal;
        lineOfSightService = configuredLineOfSightService;
        coverService = configuredCoverService;
    }

    public BattleAttackValidationResult ValidateTarget(
        SquadBattleController attacker,
        SquadBattleController target,
        AttackDefinition definition)
    {
        return EvaluateTarget(attacker, target, definition).Validation;
    }

    public BattleAttackTargetEvaluation EvaluateTarget(
        SquadBattleController attacker,
        SquadBattleController target,
        AttackDefinition definition)
    {
        if (attacker == null || !attacker.IsInitialized)
        {
            return Reject(BattleAttackFailureReason.InvalidAttacker,
                "Attacker is unavailable.");
        }
        if (target == null || !target.IsInitialized)
        {
            return Reject(BattleAttackFailureReason.InvalidTarget,
                "Target squad is unavailable.");
        }
        if (target == attacker || target.SquadId == attacker.SquadId)
        {
            return Reject(BattleAttackFailureReason.SelfTarget,
                "A squad cannot attack itself.");
        }
        if (!target.CanAct || target.Runtime.State.IsDefeated)
        {
            return Reject(BattleAttackFailureReason.TargetDefeated,
                "Defeated squads cannot be targeted.");
        }
        if (definition == null)
        {
            return Reject(BattleAttackFailureReason.MissingDefinition,
                "Attack definition is missing.");
        }
        if (!definition.FriendlyFire && target.Side == attacker.Side)
        {
            return Reject(BattleAttackFailureReason.FriendlyTarget,
                "Friendly squads are not valid targets.");
        }
        if (attacker.GridAnchor == null || !attacker.GridAnchor.IsPlaced ||
            target.GridAnchor == null || !target.GridAnchor.IsPlaced)
        {
            return Reject(BattleAttackFailureReason.InvalidTarget,
                "Attacker or target has no valid grid cell.");
        }

        return EvaluateGridGeometry(
            attacker.GridAnchor.CurrentCell,
            target.GridAnchor.CurrentCell,
            definition);
    }

    public BattleAttackTargetEvaluation EvaluateGridGeometry(
        Vector2Int attackerCell,
        Vector2Int targetCell,
        AttackDefinition definition)
    {
        if (definition == null)
            return Reject(BattleAttackFailureReason.MissingDefinition,
                "Attack definition is missing.");

        int distance = GetGridDistance(attackerCell, targetCell, allowDiagonalRange);
        if (distance < definition.MinimumRange || distance > definition.MaximumRange)
        {
            BattleAttackFailureReason failure = definition.Delivery ==
                                                BattleAttackDelivery.Ranged
                ? distance < definition.MinimumRange
                    ? BattleAttackFailureReason.TargetTooClose
                    : BattleAttackFailureReason.TargetBeyondRange
                : BattleAttackFailureReason.TargetOutOfRange;
            string reason = distance < definition.MinimumRange
                ? $"Target is too close ({distance}); minimum range is {definition.MinimumRange}."
                : $"Target is out of range ({distance}); maximum range is {definition.MaximumRange}.";
            return new BattleAttackTargetEvaluation(
                BattleAttackValidationResult.Reject(failure, reason),
                distance,
                definition.Delivery == BattleAttackDelivery.Ranged
                    ? new LineOfSightResult(LineOfSightStatus.Invalid, null, null,
                        "Range validation failed before line of sight.")
                    : LineOfSightResult.NotRequired,
                GridCoverResult.None);
        }

        if (definition.Delivery != BattleAttackDelivery.Ranged)
        {
            return new BattleAttackTargetEvaluation(
                BattleAttackValidationResult.Accepted,
                distance,
                LineOfSightResult.NotRequired,
                GridCoverResult.None);
        }
        if (lineOfSightService == null || coverService == null)
        {
            return new BattleAttackTargetEvaluation(
                BattleAttackValidationResult.Reject(
                    BattleAttackFailureReason.ServiceNotInitialized,
                    "Ranged tactical LOS/cover services are unavailable."),
                distance,
                new LineOfSightResult(LineOfSightStatus.Invalid, null, null,
                    "Ranged tactical services are unavailable."),
                GridCoverResult.None);
        }

        LineOfSightResult lineOfSight = lineOfSightService.Evaluate(
            attackerCell, targetCell);
        if (!lineOfSight.HasLineOfSight)
        {
            return new BattleAttackTargetEvaluation(
                BattleAttackValidationResult.Reject(
                    BattleAttackFailureReason.LineOfSightBlocked,
                    lineOfSight.Reason),
                distance,
                lineOfSight,
                GridCoverResult.None);
        }
        GridCoverResult cover = coverService.Evaluate(attackerCell, targetCell);
        return new BattleAttackTargetEvaluation(
            BattleAttackValidationResult.Accepted,
            distance,
            lineOfSight,
            cover);
    }

    public List<SquadBattleController> GetValidTargets(
        SquadBattleController attacker,
        IReadOnlyList<SquadBattleController> candidates,
        AttackDefinition definition)
    {
        List<SquadBattleController> valid = new List<SquadBattleController>();
        if (candidates == null)
            return valid;
        for (int i = 0; i < candidates.Count; i++)
        {
            SquadBattleController candidate = candidates[i];
            if (ValidateTarget(attacker, candidate, definition).IsValid)
                valid.Add(candidate);
        }
        return valid;
    }

    public static int GetGridDistance(
        Vector2Int first,
        Vector2Int second,
        bool allowDiagonal)
    {
        int x = Mathf.Abs(first.x - second.x);
        int y = Mathf.Abs(first.y - second.y);
        return allowDiagonal ? Mathf.Max(x, y) : x + y;
    }

    private static BattleAttackTargetEvaluation Reject(
        BattleAttackFailureReason reason, string message)
    {
        return new BattleAttackTargetEvaluation(
            BattleAttackValidationResult.Reject(reason, message),
            0,
            new LineOfSightResult(LineOfSightStatus.Invalid, null, null, message),
            GridCoverResult.None);
    }
}
