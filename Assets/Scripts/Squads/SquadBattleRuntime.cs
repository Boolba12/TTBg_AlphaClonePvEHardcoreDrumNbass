using System;
using System.Collections.Generic;

public sealed class SquadBattleRuntime
{
    private readonly SquadDamageResolver damageResolver;
    private readonly Func<double> randomValue;

    public SquadData Data { get; }
    public SquadBattleState State { get; }
    public SquadCalculatedStats Stats { get; private set; }
    public bool CanAct => !State.IsDefeated;

    public event Action<SquadBattleRuntime> OnSquadCreated;
    public event Action<SquadCalculatedStats> OnSquadStatsChanged;
    public event Action OnSquadCompositionChanged;
    public event Action<int> OnSquadHPChanged;
    public event Action<string, int> OnWarriorDamaged;
    public event Action<string> OnWarriorDefeated;
    public event Action<int> OnCommanderDamaged;
    public event Action OnCommanderDefeated;
    public event Action OnSquadDefeated;
    public event Action<float> OnMoraleChanged;
    public event Action<int> OnActionPointsChanged;
    public event Action<PrimaryStatType> OnPrimaryStatIncreased;

    public SquadBattleRuntime(
        SquadData data,
        SquadBattleState restoredState = null,
        SquadDamageResolver damageResolver = null,
        Func<double> randomValue = null)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
        SquadValidationResult validation = data.Validate();
        if (!validation.IsValid)
            throw new ArgumentException($"Invalid squad: {validation}");

        Stats = SquadStatsCalculator.Calculate(data);
        State = restoredState ?? SquadBattleState.Create(data, Stats);
        this.damageResolver = damageResolver ?? new SquadDamageResolver();
        this.randomValue = randomValue ?? new Random().NextDouble;
        ReconcileRestoredState();
        RecalculateStats();
    }

    public void NotifyCreated()
    {
        OnSquadCreated?.Invoke(this);
    }

    public SquadDamageResult ApplyDamage(int finalDamage, SquadDamageDistribution distribution)
    {
        bool wasDefeated = State.IsDefeated;
        SquadDamageResult result = damageResolver.Resolve(State, finalDamage, distribution);
        if (result.AppliedDamage <= 0)
            return result;

        foreach (string warriorId in result.DamagedWarriorIds)
        {
            WarriorBattleState warrior = FindWarriorState(warriorId);
            OnWarriorDamaged?.Invoke(warriorId, warrior != null ? warrior.currentHP : 0);
        }
        foreach (string warriorId in result.DefeatedWarriorIds)
            OnWarriorDefeated?.Invoke(warriorId);
        if (result.DefeatedWarriorIds.Count > 0)
            OnSquadCompositionChanged?.Invoke();

        if (result.CommanderDamage > 0)
            OnCommanderDamaged?.Invoke(State.commander.currentHP);
        if (result.CommanderDefeated)
            OnCommanderDefeated?.Invoke();

        RecalculateStats();
        OnSquadHPChanged?.Invoke(State.CurrentSquadHP);

        if (!wasDefeated && State.IsDefeated)
            OnSquadDefeated?.Invoke();
        return result;
    }

    public bool TrySpendActionPoints(int amount)
    {
        if (!CanAct || amount < 0 || amount > State.currentActionPoints)
            return false;

        State.currentActionPoints = Math.Max(0, State.currentActionPoints - amount);
        OnActionPointsChanged?.Invoke(State.currentActionPoints);
        return true;
    }

    /// <summary>
    /// Narrow rollback hook for a command that spent AP but failed before applying
    /// any battle-state effect. Combat services must not use this as a general AP grant.
    /// </summary>
    internal bool RestoreActionPointsAfterFailedCommit(int amount)
    {
        if (!CanAct || amount <= 0)
            return false;

        State.currentActionPoints = Math.Min(
            Stats.ActionPoints,
            State.currentActionPoints + amount);
        OnActionPointsChanged?.Invoke(State.currentActionPoints);
        return true;
    }

    public void BeginTurn()
    {
        if (!CanAct)
            return;

        State.currentActionPoints = Stats.ActionPoints;
        State.turnCompleted = false;
        OnActionPointsChanged?.Invoke(State.currentActionPoints);
    }

    public void CompleteTurn()
    {
        if (CanAct)
            State.turnCompleted = true;
    }

    public float ApplyMoraleLoss(float incomingLoss)
    {
        if (!CanAct || incomingLoss <= 0)
            return 0;

        float loss = SquadStatsCalculator.CalculateMoraleLoss(incomingLoss, Stats.Resolve);
        State.currentMorale = Math.Max(0, State.currentMorale - loss);
        OnMoraleChanged?.Invoke(State.currentMorale);
        return loss;
    }

    public bool TryIncreaseUsedPrimaryStat(PrimaryStatType statType)
    {
        if (!CanAct || randomValue() >= Stats.ExperienceMultiplier)
            return false;

        switch (statType)
        {
            case PrimaryStatType.Strength:
                Data.Commander.baseStats.strength += 1;
                break;
            case PrimaryStatType.Dexterity:
                Data.Commander.baseStats.dexterity += 1;
                break;
            case PrimaryStatType.MagicalMastery:
                Data.Commander.baseStats.magicalMastery += 1;
                break;
            default:
                return false;
        }

        RecalculateStats();
        OnPrimaryStatIncreased?.Invoke(statType);
        return true;
    }

    public void SetLogicalCell(int x, int y)
    {
        State.logicalCell ??= new SquadCellData();
        State.logicalCell.x = x;
        State.logicalCell.y = y;
    }

    public void RecalculateStats()
    {
        Stats = SquadStatsCalculator.Calculate(Data, State);
        OnSquadStatsChanged?.Invoke(Stats);
    }

    private void ReconcileRestoredState()
    {
        State.squadId = Data.Id;
        State.temporaryModifiers ??= new SquadStatModifiers();
        State.temporaryEffectIds ??= new List<string>();
        State.logicalCell ??= new SquadCellData();

        if (State.commander == null || State.commander.commanderId != Data.Commander.id)
        {
            State.commander = new CommanderBattleState
            {
                commanderId = Data.Commander.id,
                currentHP = Math.Max(0, Data.Commander.baseStats.hp)
            };
        }
        State.commander.currentHP = Math.Max(0, State.commander.currentHP);
        State.commander.defeated = State.commander.defeated || State.commander.currentHP <= 0;

        Dictionary<string, WarriorBattleState> saved = new Dictionary<string, WarriorBattleState>();
        if (State.warriors != null)
        {
            foreach (WarriorBattleState warrior in State.warriors)
            {
                if (warrior != null && !string.IsNullOrWhiteSpace(warrior.warriorId) &&
                    !saved.ContainsKey(warrior.warriorId))
                {
                    saved.Add(warrior.warriorId, warrior);
                }
            }
        }

        State.warriors = new List<WarriorBattleState>();
        foreach (WarriorData warrior in Data.Warriors)
        {
            if (!saved.TryGetValue(warrior.id, out WarriorBattleState battleWarrior))
            {
                battleWarrior = new WarriorBattleState
                {
                    warriorId = warrior.id,
                    currentHP = Math.Max(0, warrior.maxHP)
                };
            }
            battleWarrior.currentHP = Math.Max(0, battleWarrior.currentHP);
            battleWarrior.defeated = battleWarrior.defeated || battleWarrior.currentHP <= 0;
            State.warriors.Add(battleWarrior);
        }

        State.currentActionPoints = Math.Max(0, State.currentActionPoints);
        State.currentMorale = Math.Max(0, State.currentMorale);
    }

    private WarriorBattleState FindWarriorState(string id)
    {
        return State.warriors.Find(warrior => warrior != null && warrior.warriorId == id);
    }
}
