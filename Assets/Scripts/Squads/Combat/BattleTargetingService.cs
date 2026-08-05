using System.Collections.Generic;
using UnityEngine;

public sealed class BattleTargetingService
{
    private readonly bool allowDiagonalRange;

    public BattleTargetingService(bool allowDiagonal)
    {
        allowDiagonalRange = allowDiagonal;
    }

    public BattleAttackValidationResult ValidateTarget(
        SquadBattleController attacker,
        SquadBattleController target,
        AttackDefinition definition)
    {
        if (attacker == null || !attacker.IsInitialized)
        {
            return BattleAttackValidationResult.Reject(
                BattleAttackFailureReason.InvalidAttacker,
                "Attacker is unavailable.");
        }
        if (target == null || !target.IsInitialized)
        {
            return BattleAttackValidationResult.Reject(
                BattleAttackFailureReason.InvalidTarget,
                "Target squad is unavailable.");
        }
        if (target == attacker || target.SquadId == attacker.SquadId)
        {
            return BattleAttackValidationResult.Reject(
                BattleAttackFailureReason.SelfTarget,
                "A squad cannot attack itself.");
        }
        if (!target.CanAct || target.Runtime.State.IsDefeated)
        {
            return BattleAttackValidationResult.Reject(
                BattleAttackFailureReason.TargetDefeated,
                "Defeated squads cannot be targeted.");
        }
        if (definition == null)
        {
            return BattleAttackValidationResult.Reject(
                BattleAttackFailureReason.MissingDefinition,
                "Attack definition is missing.");
        }
        if (!definition.FriendlyFire && target.Side == attacker.Side)
        {
            return BattleAttackValidationResult.Reject(
                BattleAttackFailureReason.FriendlyTarget,
                "Friendly squads are not valid targets.");
        }
        if (attacker.GridAnchor == null || !attacker.GridAnchor.IsPlaced ||
            target.GridAnchor == null || !target.GridAnchor.IsPlaced)
        {
            return BattleAttackValidationResult.Reject(
                BattleAttackFailureReason.InvalidTarget,
                "Attacker or target has no valid grid cell.");
        }

        int distance = GetGridDistance(
            attacker.GridAnchor.CurrentCell,
            target.GridAnchor.CurrentCell,
            allowDiagonalRange);
        if (distance < definition.MinimumRange || distance > definition.MaximumRange)
        {
            return BattleAttackValidationResult.Reject(
                BattleAttackFailureReason.TargetOutOfRange,
                $"Target is {distance} cell(s) away; valid range is " +
                $"{definition.MinimumRange}-{definition.MaximumRange}.");
        }
        return BattleAttackValidationResult.Accepted;
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
}
