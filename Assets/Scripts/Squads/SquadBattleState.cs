using System;
using System.Collections.Generic;

[Serializable]
public sealed class SquadBattleState
{
    public string squadId;
    public CommanderBattleState commander = new CommanderBattleState();
    public List<WarriorBattleState> warriors = new List<WarriorBattleState>();
    public int currentActionPoints;
    public float currentMorale;
    public SquadStatModifiers temporaryModifiers = new SquadStatModifiers();
    public List<string> temporaryEffectIds = new List<string>();
    public SquadCellData logicalCell = new SquadCellData();
    public bool turnCompleted;
    public int initiativeOrder;

    public bool IsDefeated => commander == null || commander.defeated || commander.currentHP <= 0;
    public int CurrentSquadHP
    {
        get
        {
            int total = commander != null && !commander.defeated ? Math.Max(0, commander.currentHP) : 0;
            if (warriors != null)
            {
                foreach (WarriorBattleState warrior in warriors)
                {
                    if (warrior != null && !warrior.defeated)
                        total += Math.Max(0, warrior.currentHP);
                }
            }
            return total;
        }
    }

    public static SquadBattleState Create(SquadData data, SquadCalculatedStats stats)
    {
        int totalWarriorHP = 0;
        foreach (WarriorData warrior in data.Warriors)
            totalWarriorHP += Math.Max(0, warrior.maxHP);

        SquadBattleState state = new SquadBattleState
        {
            squadId = data.Id,
            commander = new CommanderBattleState
            {
                commanderId = data.Commander.id,
                currentHP = Math.Max(0, stats.MaxHP - totalWarriorHP),
                defeated = false
            },
            currentActionPoints = stats.ActionPoints,
            currentMorale = stats.Morale
        };

        foreach (WarriorData warrior in data.Warriors)
        {
            state.warriors.Add(new WarriorBattleState
            {
                warriorId = warrior.id,
                currentHP = Math.Max(0, warrior.maxHP),
                defeated = warrior.maxHP <= 0
            });
        }
        return state;
    }
}

[Serializable]
public sealed class CommanderBattleState
{
    public string commanderId;
    public int currentHP;
    public bool defeated;
}

[Serializable]
public sealed class WarriorBattleState
{
    public string warriorId;
    public int currentHP;
    public bool defeated;
}

[Serializable]
public sealed class SquadCellData
{
    public int x;
    public int y;
}

public enum SquadDamageDistribution
{
    SingleTarget,
    Area
}

public enum PrimaryStatType
{
    Strength,
    Dexterity,
    MagicalMastery
}
