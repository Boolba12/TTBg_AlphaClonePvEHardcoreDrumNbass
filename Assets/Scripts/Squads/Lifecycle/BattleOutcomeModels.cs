using System;
using System.Collections.Generic;

public enum BattleResultType
{
    Victory,
    Defeat,
    Draw
}

public enum BattleCompletionState
{
    Running,
    Completing,
    Completed,
    Transitioning,
    Failed
}

public enum CommanderPostBattleOutcomeType
{
    Pending,
    SurvivedNormally,
    SurvivedWithPermanentDebuff,
    Killed
}

[Serializable]
public sealed class BattleCasualtyRecord
{
    public string squadId;
    public string warriorId;
}

[Serializable]
public sealed class BattleAbilityUsageRecord
{
    public string squadId;
    public string abilityId;
    public int uses;
}

[Serializable]
public sealed class SquadBattleResult
{
    public string squadId;
    public string commanderId;
    public string portraitId;
    public BattleSide side;
    public List<string> initialWarriorIds = new List<string>();
    public List<string> survivingWarriorIds = new List<string>();
    public List<string> defeatedWarriorIds = new List<string>();
    public int initialCommanderHP;
    public int finalCommanderHP;
    public bool commanderDefeatedInBattle;
    public float initialMorale;
    public float finalMorale;
    public CommanderPostBattleOutcomeType commanderOutcome =
        CommanderPostBattleOutcomeType.Pending;
    public string permanentDebuffId;
    public bool persistentProgressionChanged;
}

[Serializable]
public sealed class BattleOutcome
{
    public const int CurrentSchemaVersion = 1;

    public int schemaVersion = CurrentSchemaVersion;
    public string battleId;
    public string encounterId;
    public BattleResultType resultType;
    public BattleSide winningSide;
    public BattleSide losingSide;
    public int battleSeed;
    public string startedUtc;
    public string completedUtc;
    public int rounds;
    public int completedTurns;
    public List<SquadBattleResult> participantResults = new List<SquadBattleResult>();
    public List<string> defeatedSquadIds = new List<string>();
    public List<string> survivingSquadIds = new List<string>();
    public List<BattleCasualtyRecord> casualties = new List<BattleCasualtyRecord>();
    public List<BattleAbilityUsageRecord> abilityUsages =
        new List<BattleAbilityUsageRecord>();
    public bool persistentMutationsApplied;
    public bool autosaveSucceeded;
}

public readonly struct BattleOutcomeBuildResult
{
    public bool Success { get; }
    public BattleOutcome Outcome { get; }
    public string Error { get; }

    private BattleOutcomeBuildResult(bool success, BattleOutcome outcome, string error)
    {
        Success = success;
        Outcome = outcome;
        Error = error;
    }

    public static BattleOutcomeBuildResult Ok(BattleOutcome outcome) =>
        new BattleOutcomeBuildResult(true, outcome, null);

    public static BattleOutcomeBuildResult Fail(string error) =>
        new BattleOutcomeBuildResult(false, null, error);
}

public readonly struct BattleResultApplicationResult
{
    public bool Success { get; }
    public string Error { get; }
    public bool AlreadyApplied { get; }

    private BattleResultApplicationResult(bool success, string error, bool alreadyApplied)
    {
        Success = success;
        Error = error;
        AlreadyApplied = alreadyApplied;
    }

    public static BattleResultApplicationResult Ok(bool alreadyApplied = false) =>
        new BattleResultApplicationResult(true, null, alreadyApplied);

    public static BattleResultApplicationResult Fail(string error) =>
        new BattleResultApplicationResult(false, error, false);
}
