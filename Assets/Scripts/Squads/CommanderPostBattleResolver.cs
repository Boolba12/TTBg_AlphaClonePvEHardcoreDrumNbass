using System;

[Serializable]
public sealed class CommanderPostBattleResult
{
    public string commanderId;
    public bool permanentlyDead;
    public bool survived;
    public string permanentDebuffId;
    public CommanderPostBattleOutcomeType outcomeType;
    public string sourceBattleId;
}

public interface ICommanderPostBattleResolver
{
    CommanderPostBattleResult Resolve(CommanderData commander, SquadBattleState battleState);
}

/// <summary>
/// Explicit integration point for a future survival table and permanent-debuff database.
/// No survival probabilities are assumed by the squad system.
/// </summary>
public sealed class CommanderPostBattleService
{
    private readonly ICommanderPostBattleResolver resolver;

    public CommanderPostBattleService(ICommanderPostBattleResolver resolver)
    {
        this.resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    public CommanderPostBattleResult ResolveAndApply(SquadData squad, SquadBattleState battleState)
    {
        if (squad?.Commander == null)
            throw new ArgumentException("A commander is required.");

        CommanderPostBattleResult result = resolver.Resolve(squad.Commander, battleState);
        squad.Commander.permanentDebuffIds ??= new System.Collections.Generic.List<string>();
        if (result != null && result.survived && !string.IsNullOrWhiteSpace(result.permanentDebuffId) &&
            !squad.Commander.permanentDebuffIds.Contains(result.permanentDebuffId))
        {
            squad.Commander.permanentDebuffIds.Add(result.permanentDebuffId);
        }
        return result;
    }

    public CommanderPostBattleResult Resolve(SquadData squad, SquadBattleState battleState)
    {
        if (squad?.Commander == null)
            throw new ArgumentException("A commander is required.");
        return resolver.Resolve(squad.Commander, battleState);
    }
}
