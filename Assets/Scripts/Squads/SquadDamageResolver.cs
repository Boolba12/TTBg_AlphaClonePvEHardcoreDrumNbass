using System;
using System.Collections.Generic;

public sealed class SquadDamageResult
{
    public int IncomingDamage { get; internal set; }
    public int AppliedDamage { get; internal set; }
    public int UnusedDamage { get; internal set; }
    public int CommanderDamage { get; internal set; }
    public List<string> DamagedWarriorIds { get; } = new List<string>();
    public List<string> DefeatedWarriorIds { get; } = new List<string>();
    public bool CommanderDefeated { get; internal set; }
    public bool SquadDefeated { get; internal set; }
}

public sealed class SquadDamageResolver
{
    /// <summary>
    /// Distributes damage that has already passed hit, critical, armor, resistance,
    /// and penetration calculations.
    /// </summary>
    public SquadDamageResult Resolve(
        SquadBattleState state,
        int finalDamage,
        SquadDamageDistribution distribution)
    {
        SquadDamageResult result = new SquadDamageResult
        {
            IncomingDamage = Math.Max(0, finalDamage)
        };

        if (state == null || state.IsDefeated || finalDamage <= 0)
            return result;

        if (distribution == SquadDamageDistribution.SingleTarget)
            ResolveSingleTarget(state, finalDamage, result);
        else
            ResolveArea(state, finalDamage, result);

        result.SquadDefeated = state.IsDefeated;
        return result;
    }

    private static void ResolveSingleTarget(
        SquadBattleState state,
        int damage,
        SquadDamageResult result)
    {
        WarriorBattleState target = FindFirstLivingWarrior(state);
        if (target != null)
        {
            ApplyToWarrior(target, damage, result);
            result.UnusedDamage = Math.Max(0, damage - result.AppliedDamage);
            return;
        }

        ApplyToCommander(state.commander, damage, result);
        result.UnusedDamage = Math.Max(0, damage - result.AppliedDamage);
    }

    private static void ResolveArea(
        SquadBattleState state,
        int damage,
        SquadDamageResult result)
    {
        int remaining = damage;
        if (state.warriors != null)
        {
            foreach (WarriorBattleState warrior in state.warriors)
            {
                if (remaining <= 0)
                    break;
                if (warrior == null || warrior.defeated || warrior.currentHP <= 0)
                    continue;

                int before = result.AppliedDamage;
                ApplyToWarrior(warrior, remaining, result);
                remaining -= result.AppliedDamage - before;

                if (!warrior.defeated)
                    break;
            }
        }

        if (remaining > 0 && FindFirstLivingWarrior(state) == null)
        {
            int before = result.AppliedDamage;
            ApplyToCommander(state.commander, remaining, result);
            remaining -= result.AppliedDamage - before;
        }

        result.UnusedDamage = Math.Max(0, remaining);
    }

    private static void ApplyToWarrior(
        WarriorBattleState warrior,
        int damage,
        SquadDamageResult result)
    {
        int applied = Math.Min(Math.Max(0, warrior.currentHP), damage);
        if (applied <= 0)
            return;

        warrior.currentHP -= applied;
        result.AppliedDamage += applied;
        result.DamagedWarriorIds.Add(warrior.warriorId);

        if (warrior.currentHP <= 0 && !warrior.defeated)
        {
            warrior.currentHP = 0;
            warrior.defeated = true;
            result.DefeatedWarriorIds.Add(warrior.warriorId);
        }
    }

    private static void ApplyToCommander(
        CommanderBattleState commander,
        int damage,
        SquadDamageResult result)
    {
        if (commander == null || commander.defeated || commander.currentHP <= 0)
            return;

        int applied = Math.Min(commander.currentHP, damage);
        commander.currentHP -= applied;
        result.AppliedDamage += applied;
        result.CommanderDamage += applied;

        if (commander.currentHP <= 0)
        {
            commander.currentHP = 0;
            commander.defeated = true;
            result.CommanderDefeated = true;
        }
    }

    private static WarriorBattleState FindFirstLivingWarrior(SquadBattleState state)
    {
        if (state?.warriors == null)
            return null;

        return state.warriors.Find(
            warrior => warrior != null && !warrior.defeated && warrior.currentHP > 0);
    }
}
