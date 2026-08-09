using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class EnemyTacticalDecisionService
{
    public const string PowerStrikeId = "DEV_PowerStrike";
    public const string SweepingBlowId = "DEV_SweepingBlow";
    public const string RallyId = "DEV_Rally";

    private readonly SquadBattleBootstrap squadBootstrap;
    private readonly MapGenerator mapGenerator;
    private readonly GridOccupancyService occupancy;
    private readonly SquadMovementService movementService;
    private readonly BattleAttackService attackService;
    private readonly BattleAbilityService abilityService;
    private readonly BattleCompletionController completionController;
    private readonly List<SquadBattleController> targetBuffer =
        new List<SquadBattleController>();

    public EnemyTacticalDecisionService(
        SquadBattleBootstrap bootstrap,
        MapGenerator generator,
        GridOccupancyService occupancyService,
        SquadMovementService movement,
        BattleAttackService attacks,
        BattleAbilityService abilities,
        BattleCompletionController completion)
    {
        squadBootstrap = bootstrap;
        mapGenerator = generator;
        occupancy = occupancyService;
        movementService = movement;
        attackService = attacks;
        abilityService = abilities;
        completionController = completion;
    }

    public EnemyTacticalDecision Decide(SquadBattleController actor)
    {
        string invalidReason = ValidateActor(actor);
        if (invalidReason != null)
            return EnemyTacticalDecision.End(actor, invalidReason);

        CollectTargets(actor);
        if (targetBuffer.Count == 0)
            return EnemyTacticalDecision.End(actor, "No living Player-side target remains.");

        targetBuffer.Sort((first, second) => CompareTargets(actor, first, second));

        EnemyTacticalDecision offensive = FindImmediateOffense(actor);
        if (offensive != null)
            return offensive;

        EnemyTacticalDecision movement = FindMovement(actor);
        AbilityDefinition rally = FindAbility(RallyId);
        if (rally != null && IsLowMorale(actor) &&
            abilityService.ValidateCommand(
                actor,
                actor,
                rally,
                BattleCommandAuthority.TacticalAI).IsValid)
        {
            if (movement == null || !EnablesSameTurnBasicAttack(actor, movement))
            {
                return EnemyTacticalDecision.UseAbility(
                    EnemyTacticalActionType.Rally,
                    actor,
                    actor,
                    rally,
                    "No immediate offense is available, Rally does not replace a same-turn attack, and morale is at or below 50%.");
            }
        }

        if (movement != null)
            return movement;

        return EnemyTacticalDecision.End(
            actor,
            "No valid or affordable deterministic action remains.");
    }

    public int CompareTargetPriority(
        SquadBattleController actor,
        SquadBattleController first,
        SquadBattleController second)
    {
        if (actor == null || first == null || second == null)
            throw new ArgumentNullException("AI target priority requires non-null participants.");
        return CompareTargets(actor, first, second);
    }

    private EnemyTacticalDecision FindImmediateOffense(SquadBattleController actor)
    {
        for (int i = 0; i < targetBuffer.Count; i++)
        {
            SquadBattleController target = targetBuffer[i];
            if (attackService.ValidateCommand(
                    actor,
                    target,
                    attackService.BasicAttack,
                    BattleCommandAuthority.TacticalAI).IsValid)
            {
                return EnemyTacticalDecision.Attack(actor, target);
            }
        }

        EnemyTacticalDecision powerStrike = FindOffensiveAbility(
            actor,
            PowerStrikeId,
            EnemyTacticalActionType.PowerStrike);
        if (powerStrike != null)
            return powerStrike;

        return FindOffensiveAbility(
            actor,
            SweepingBlowId,
            EnemyTacticalActionType.SweepingBlow);
    }

    private EnemyTacticalDecision FindOffensiveAbility(
        SquadBattleController actor,
        string abilityId,
        EnemyTacticalActionType actionType)
    {
        AbilityDefinition ability = FindAbility(abilityId);
        if (ability == null || ability.EffectType != BattleAbilityEffectType.PhysicalAttack)
            return null;

        for (int i = 0; i < targetBuffer.Count; i++)
        {
            SquadBattleController target = targetBuffer[i];
            if (abilityService.ValidateCommand(
                    actor,
                    target,
                    ability,
                    BattleCommandAuthority.TacticalAI).IsValid)
            {
                return EnemyTacticalDecision.UseAbility(
                    actionType,
                    actor,
                    target,
                    ability,
                    $"{ability.DisplayName} is the next valid offensive priority.");
            }
        }
        return null;
    }

    private EnemyTacticalDecision FindMovement(SquadBattleController actor)
    {
        AttackDefinition attack = attackService.BasicAttack;
        int availableActionPoints = actor.Runtime.State.currentActionPoints;
        if (attack == null || availableActionPoints <= 0)
            return null;

        Vector2Int start = actor.GridAnchor.CurrentCell;
        for (int i = 0; i < targetBuffer.Count; i++)
        {
            SquadBattleController target = targetBuffer[i];
            Vector2Int targetCell = target.GridAnchor.CurrentCell;
            if (IsAttackCell(start, targetCell, attack))
                continue;
            bool found = GridPathfinder.TryBuildPathToNearest(
                mapGenerator,
                start,
                movementService.AllowDiagonalMovement,
                cell => cell != start && cell != targetCell &&
                        IsAttackCell(cell, targetCell, attack) &&
                        occupancy.CanEnter(actor, cell),
                cell => occupancy.CanEnter(actor, cell),
                Mathf.Max(0, mapGenerator.Width * mapGenerator.Height),
                out List<Vector2Int> path,
                out Vector2Int _);
            if (!found)
                continue;

            int fullPathCost = Mathf.Max(0, path.Count - 1);
            if (fullPathCost == 0)
                continue;
            int reservedAttackBudget = availableActionPoints - attack.ActionPointCost;
            int movementCost = fullPathCost <= reservedAttackBudget
                ? fullPathCost
                : Mathf.Min(availableActionPoints, fullPathCost);
            if (movementCost <= 0)
                continue;
            Vector2Int destination = path[movementCost];

            if (movementService.TryBuildPlan(
                    actor,
                    destination,
                    out SquadMovementPlan plan,
                    BattleCommandAuthority.TacticalAI))
            {
                return EnemyTacticalDecision.Move(actor, target, plan);
            }
        }
        return null;
    }

    private bool IsAttackCell(
        Vector2Int candidate,
        Vector2Int target,
        AttackDefinition definition)
    {
        int distance = BattleTargetingService.GetGridDistance(
            candidate,
            target,
            movementService.AllowDiagonalMovement);
        return distance >= definition.MinimumRange &&
               distance <= definition.MaximumRange;
    }

    private bool EnablesSameTurnBasicAttack(
        SquadBattleController actor,
        EnemyTacticalDecision movement)
    {
        AttackDefinition attack = attackService.BasicAttack;
        if (actor == null || movement?.Target == null || attack == null)
            return false;
        return movement.PathCost + attack.ActionPointCost <=
               actor.Runtime.State.currentActionPoints &&
               IsAttackCell(
                   movement.Destination,
                   movement.Target.GridAnchor.CurrentCell,
                   attack);
    }

    private void CollectTargets(SquadBattleController actor)
    {
        targetBuffer.Clear();
        IReadOnlyList<SquadBattleController> participants =
            squadBootstrap.SpawnedControllers;
        for (int i = 0; i < participants.Count; i++)
        {
            SquadBattleController candidate = participants[i];
            if (candidate == null || candidate == actor || !candidate.IsInitialized ||
                candidate.Side != BattleSide.Player || !candidate.CanAct ||
                candidate.Runtime.State.IsDefeated || candidate.GridAnchor == null ||
                !candidate.GridAnchor.IsPlaced)
            {
                continue;
            }
            targetBuffer.Add(candidate);
        }
    }

    private int CompareTargets(
        SquadBattleController actor,
        SquadBattleController first,
        SquadBattleController second)
    {
        int firstDistance = BattleTargetingService.GetGridDistance(
            actor.GridAnchor.CurrentCell,
            first.GridAnchor.CurrentCell,
            movementService.AllowDiagonalMovement);
        int secondDistance = BattleTargetingService.GetGridDistance(
            actor.GridAnchor.CurrentCell,
            second.GridAnchor.CurrentCell,
            movementService.AllowDiagonalMovement);
        int comparison = firstDistance.CompareTo(secondDistance);
        if (comparison != 0)
            return comparison;
        comparison = first.Runtime.State.CurrentSquadHP.CompareTo(
            second.Runtime.State.CurrentSquadHP);
        if (comparison != 0)
            return comparison;
        return string.Compare(first.SquadId, second.SquadId, StringComparison.Ordinal);
    }

    private AbilityDefinition FindAbility(string stableId)
    {
        if (abilityService == null || abilityService.Abilities == null)
            return null;
        for (int i = 0; i < abilityService.Abilities.Count; i++)
        {
            AbilityDefinition ability = abilityService.Abilities[i];
            if (ability != null && string.Equals(
                    ability.StableId,
                    stableId,
                    StringComparison.Ordinal))
            {
                return ability;
            }
        }
        return null;
    }

    private static bool IsLowMorale(SquadBattleController actor)
    {
        float maximum = Mathf.Max(0f, actor.Runtime.Stats.Morale);
        return maximum > 0f && actor.Runtime.State.currentMorale <= maximum * 0.5f;
    }

    private string ValidateActor(SquadBattleController actor)
    {
        if (completionController == null ||
            completionController.State != BattleCompletionState.Running)
        {
            return "Battle completion state is no longer Running.";
        }
        if (actor == null || !actor.IsInitialized || !actor.CanAct ||
            actor.Runtime.State.IsDefeated)
        {
            return "Active AI squad is unavailable or defeated.";
        }
        if (actor.Side != BattleSide.Enemy || actor.ControlType != SquadControlType.AI)
            return "Active participant is not an Enemy-side AI squad.";
        if (actor.GridAnchor == null || !actor.GridAnchor.IsPlaced)
            return "Active AI squad has no committed grid cell.";
        if (mapGenerator == null || !mapGenerator.HasGeneratedData ||
            occupancy == null || !occupancy.IsInitialized)
        {
            return "Battlefield map or occupancy is unavailable.";
        }
        return null;
    }
}
