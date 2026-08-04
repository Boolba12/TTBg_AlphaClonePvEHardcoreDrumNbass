using System;
using System.Collections.Generic;
using System.Linq;

public sealed class BattleResultBuilder
{
    private readonly List<InitialSnapshot> initial = new List<InitialSnapshot>();
    private string battleId;
    private string encounterId;
    private int battleSeed;
    private string startedUtc;
    private bool initialized;

    public bool Initialize(
        IReadOnlyList<SquadBattleController> participants,
        string configuredBattleId,
        string configuredEncounterId,
        int configuredBattleSeed,
        string configuredStartedUtc = null)
    {
        if (initialized || participants == null || participants.Count == 0 ||
            string.IsNullOrWhiteSpace(configuredBattleId))
        {
            return false;
        }

        HashSet<string> squadIds = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> memberIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (SquadBattleController controller in participants)
        {
            if (controller?.Runtime?.Data?.Commander == null ||
                !squadIds.Add(controller.SquadId) ||
                !memberIds.Add(controller.Runtime.Data.Commander.id))
            {
                initial.Clear();
                return false;
            }

            InitialSnapshot snapshot = new InitialSnapshot
            {
                Controller = controller,
                SquadId = controller.SquadId,
                CommanderId = controller.Runtime.Data.Commander.id,
                PortraitId = controller.Runtime.Data.CommanderPortraitId,
                Side = controller.Side,
                CommanderHP = controller.Runtime.State.commander.currentHP,
                Morale = controller.Runtime.State.currentMorale,
                RegistrationSequence = controller.RegistrationSequence
            };
            foreach (WarriorData warrior in controller.Runtime.Data.Warriors)
            {
                if (warrior == null || string.IsNullOrWhiteSpace(warrior.id) ||
                    !memberIds.Add(warrior.id))
                {
                    initial.Clear();
                    return false;
                }
                snapshot.WarriorIds.Add(warrior.id);
            }
            initial.Add(snapshot);
        }

        initial.Sort((left, right) =>
        {
            int sequence = left.RegistrationSequence.CompareTo(right.RegistrationSequence);
            return sequence != 0
                ? sequence
                : StringComparer.Ordinal.Compare(left.SquadId, right.SquadId);
        });
        battleId = configuredBattleId;
        encounterId = configuredEncounterId ?? string.Empty;
        battleSeed = configuredBattleSeed;
        startedUtc = string.IsNullOrWhiteSpace(configuredStartedUtc)
            ? DateTime.UtcNow.ToString("O")
            : configuredStartedUtc;
        initialized = true;
        return true;
    }

    public BattleOutcomeBuildResult Build(
        BattleResultType resultType,
        BattleSide winningSide,
        BattleSide losingSide,
        int rounds,
        int completedTurns,
        string completionUtc = null,
        IReadOnlyList<BattleAbilityUsageRecord> abilityUsages = null)
    {
        if (!initialized)
            return BattleOutcomeBuildResult.Fail("BattleResultBuilder is not initialized.");

        BattleOutcome outcome = new BattleOutcome
        {
            battleId = battleId,
            encounterId = encounterId,
            battleSeed = battleSeed,
            resultType = resultType,
            winningSide = winningSide,
            losingSide = losingSide,
            startedUtc = startedUtc,
            completedUtc = string.IsNullOrWhiteSpace(completionUtc)
                ? DateTime.UtcNow.ToString("O")
                : completionUtc,
            rounds = Math.Max(0, rounds),
            completedTurns = Math.Max(0, completedTurns)
        };

        foreach (InitialSnapshot snapshot in initial)
        {
            SquadBattleController controller = snapshot.Controller;
            if (controller?.Runtime?.State == null || controller.SquadId != snapshot.SquadId)
            {
                return BattleOutcomeBuildResult.Fail(
                    $"Battle participant '{snapshot.SquadId}' is unavailable at completion.");
            }

            SquadBattleResult result = new SquadBattleResult
            {
                squadId = snapshot.SquadId,
                commanderId = snapshot.CommanderId,
                portraitId = snapshot.PortraitId,
                side = snapshot.Side,
                initialCommanderHP = snapshot.CommanderHP,
                finalCommanderHP = Math.Max(0, controller.Runtime.State.commander.currentHP),
                commanderDefeatedInBattle = controller.Runtime.State.commander.defeated,
                initialMorale = snapshot.Morale,
                finalMorale = Math.Max(0f, controller.Runtime.State.currentMorale)
            };
            result.initialWarriorIds.AddRange(snapshot.WarriorIds);
            foreach (string warriorId in snapshot.WarriorIds)
            {
                WarriorBattleState state = controller.Runtime.State.warriors.Find(
                    warrior => warrior != null && warrior.warriorId == warriorId);
                if (state != null && !state.defeated && state.currentHP > 0)
                    result.survivingWarriorIds.Add(warriorId);
                else
                {
                    result.defeatedWarriorIds.Add(warriorId);
                    outcome.casualties.Add(new BattleCasualtyRecord
                    {
                        squadId = snapshot.SquadId,
                        warriorId = warriorId
                    });
                }
            }
            outcome.participantResults.Add(result);
            if (controller.Runtime.State.IsDefeated)
                outcome.defeatedSquadIds.Add(snapshot.SquadId);
            else
                outcome.survivingSquadIds.Add(snapshot.SquadId);
        }

        if (abilityUsages != null)
        {
            outcome.abilityUsages.AddRange(abilityUsages
                .Where(usage => usage != null)
                .OrderBy(usage => usage.squadId, StringComparer.Ordinal)
                .ThenBy(usage => usage.abilityId, StringComparer.Ordinal));
        }
        return BattleOutcomeBuildResult.Ok(outcome);
    }

    private sealed class InitialSnapshot
    {
        public SquadBattleController Controller;
        public string SquadId;
        public string CommanderId;
        public string PortraitId;
        public BattleSide Side;
        public int CommanderHP;
        public float Morale;
        public int RegistrationSequence;
        public readonly List<string> WarriorIds = new List<string>();
    }
}
