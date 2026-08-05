using System;
using System.Collections.Generic;
using System.Linq;

public sealed class BattleResultApplier
{
    private readonly SquadSaveParticipant repository;
    private readonly PostBattleRules rules;
    private readonly Func<string, IPostBattleRandomSource> randomFactory;

    public BattleResultApplier(
        SquadSaveParticipant squadRepository,
        PostBattleRules postBattleRules,
        Func<string, IPostBattleRandomSource> configuredRandomFactory)
    {
        repository = squadRepository;
        rules = postBattleRules;
        randomFactory = configuredRandomFactory;
    }

    public BattleResultApplicationResult Apply(BattleOutcome outcome)
    {
        if (repository == null)
            return BattleResultApplicationResult.Fail("Squad repository is missing.");
        if (outcome == null || outcome.schemaVersion != BattleOutcome.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(outcome.battleId))
        {
            return BattleResultApplicationResult.Fail("Battle outcome is invalid.");
        }
        if (repository.HasAppliedBattle(outcome.battleId))
        {
            outcome.persistentMutationsApplied = true;
            return BattleResultApplicationResult.Ok(true);
        }
        string rulesError = rules == null ? "Rules asset is missing." : null;
        if (rules == null || !rules.Validate(out rulesError))
            return BattleResultApplicationResult.Fail($"Post-battle rules are invalid. {rulesError}");
        if (randomFactory == null)
            return BattleResultApplicationResult.Fail("Post-battle random source is missing.");

        List<PlannedMutation> plans = new List<PlannedMutation>();
        HashSet<string> outcomeSquadIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (SquadBattleResult result in outcome.participantResults)
        {
            if (result == null || string.IsNullOrWhiteSpace(result.squadId) ||
                !outcomeSquadIds.Add(result.squadId))
            {
                return BattleResultApplicationResult.Fail(
                    "Outcome contains a missing or duplicate squad ID.");
            }
            if (result.side != BattleSide.Player)
                continue;

            SquadData squad = repository.GetSquad(result.squadId);
            if (squad == null)
            {
                return BattleResultApplicationResult.Fail(
                    $"Persistent Player squad '{result.squadId}' was not found.");
            }
            if (squad.Commander == null || squad.Commander.id != result.commanderId)
            {
                return BattleResultApplicationResult.Fail(
                    $"Commander ID mismatch for squad '{result.squadId}'.");
            }

            HashSet<string> initialIds = new HashSet<string>(
                result.initialWarriorIds ?? new List<string>(), StringComparer.Ordinal);
            HashSet<string> survivorIds = new HashSet<string>(
                result.survivingWarriorIds ?? new List<string>(), StringComparer.Ordinal);
            HashSet<string> defeatedIds = new HashSet<string>(
                result.defeatedWarriorIds ?? new List<string>(), StringComparer.Ordinal);
            HashSet<string> persistentIds = new HashSet<string>(
                squad.Warriors.Where(warrior => warrior != null).Select(warrior => warrior.id),
                StringComparer.Ordinal);
            if (!initialIds.SetEquals(persistentIds) ||
                survivorIds.Overlaps(defeatedIds) ||
                survivorIds.Count + defeatedIds.Count != initialIds.Count ||
                !survivorIds.IsSubsetOf(initialIds) || !defeatedIds.IsSubsetOf(initialIds))
            {
                return BattleResultApplicationResult.Fail(
                    $"Warrior ID partition is invalid for squad '{result.squadId}'.");
            }

            SquadBattleState postBattleState = CreateBattleState(result);
            CommanderPostBattleService commanderService = new CommanderPostBattleService(
                new DevelopmentCommanderPostBattleResolver(
                    rules,
                    randomFactory(result.commanderId),
                    outcome.battleId));
            CommanderPostBattleResult commanderOutcome =
                commanderService.Resolve(squad, postBattleState);
            if (commanderOutcome == null || commanderOutcome.commanderId != result.commanderId)
            {
                return BattleResultApplicationResult.Fail(
                    $"Commander outcome is invalid for squad '{result.squadId}'.");
            }

            PersistentSquadStatus status = commanderOutcome.permanentlyDead
                ? PersistentSquadStatus.CommanderLost
                : survivorIds.Count == 0
                    ? PersistentSquadStatus.InactiveNoWarriors
                    : PersistentSquadStatus.Active;
            plans.Add(new PlannedMutation
            {
                Squad = squad,
                Result = result,
                SurvivingWarriorIds = survivorIds,
                CommanderOutcome = commanderOutcome,
                Status = status,
                Debuff = commanderOutcome.outcomeType ==
                         CommanderPostBattleOutcomeType.SurvivedWithPermanentDebuff
                    ? rules.SurvivorDebuff
                    : null
            });
        }
        if (plans.Count == 0)
            return BattleResultApplicationResult.Fail("Outcome has no Player squad result.");

        string rollback = repository.CaptureState();
        try
        {
            foreach (PlannedMutation plan in plans)
            {
                if (!plan.Squad.ApplyPostBattleState(
                        plan.SurvivingWarriorIds,
                        plan.Status,
                        plan.Debuff,
                        outcome.battleId,
                        out string error))
                {
                    throw new InvalidOperationException(error);
                }
                plan.Result.commanderOutcome = plan.CommanderOutcome.outcomeType;
                plan.Result.permanentDebuffId = plan.CommanderOutcome.permanentDebuffId;
            }
            if (!repository.MarkBattleApplied(outcome.battleId))
                throw new InvalidOperationException("Outcome could not be marked as applied.");
            outcome.persistentMutationsApplied = true;
            return BattleResultApplicationResult.Ok();
        }
        catch (Exception exception)
        {
            repository.RestoreState(rollback);
            outcome.persistentMutationsApplied = false;
            return BattleResultApplicationResult.Fail(
                $"Post-battle transaction rolled back: {exception.Message}");
        }
    }

    private static SquadBattleState CreateBattleState(SquadBattleResult result)
    {
        SquadBattleState state = new SquadBattleState
        {
            squadId = result.squadId,
            commander = new CommanderBattleState
            {
                commanderId = result.commanderId,
                currentHP = Math.Max(0, result.finalCommanderHP),
                defeated = result.commanderDefeatedInBattle
            },
            currentMorale = Math.Max(0f, result.finalMorale)
        };
        foreach (string warriorId in result.initialWarriorIds)
        {
            bool defeated = result.defeatedWarriorIds.Contains(warriorId);
            state.warriors.Add(new WarriorBattleState
            {
                warriorId = warriorId,
                currentHP = defeated ? 0 : 1,
                defeated = defeated
            });
        }
        return state;
    }

    private sealed class PlannedMutation
    {
        public SquadData Squad;
        public SquadBattleResult Result;
        public HashSet<string> SurvivingWarriorIds;
        public CommanderPostBattleResult CommanderOutcome;
        public PersistentSquadStatus Status;
        public PersistentDebuffDefinition Debuff;
    }
}
