using System;
using System.Collections.Generic;
using UnityEngine;

public enum PersistentSquadStatus
{
    Active,
    InactiveNoWarriors,
    CommanderLost
}

[Serializable]
public sealed class PersistentDebuffRecord
{
    public string debuffId;
    public string sourceBattleId;
}

[Serializable]
public sealed class SquadData : ICommanderPortraitTarget
{
    public const int MinimumWarriors = 1;
    public const int MaximumWarriors = 8;

    [SerializeField] private string id;
    [SerializeField] private CommanderData commander;
    [SerializeField] private List<WarriorData> warriors = new List<WarriorData>();
    [SerializeField] private SquadStatModifiers permanentModifiers = new SquadStatModifiers();
    [SerializeField] private PersistentSquadStatus status = PersistentSquadStatus.Active;

    public string Id => id;
    public CommanderData Commander => commander;
    public IReadOnlyList<WarriorData> Warriors => warriors;
    public SquadStatModifiers PermanentModifiers => permanentModifiers;
    public PersistentSquadStatus Status => status;
    public bool IsBattleEligible => status == PersistentSquadStatus.Active &&
                                    warriors != null && warriors.Count >= MinimumWarriors;

    public string CommanderPortraitId
    {
        get => commander != null ? commander.CommanderPortraitId : string.Empty;
        set
        {
            if (commander != null)
                commander.CommanderPortraitId = value;
        }
    }

    public SquadData(
        string id,
        CommanderData commander,
        IEnumerable<WarriorData> warriors,
        SquadStatModifiers permanentModifiers = null)
    {
        this.id = id;
        this.commander = commander;
        this.warriors = warriors != null ? new List<WarriorData>(warriors) : new List<WarriorData>();
        this.permanentModifiers = permanentModifiers ?? new SquadStatModifiers();
        status = PersistentSquadStatus.Active;
    }

    public bool TryAddWarrior(WarriorData warrior, bool battleActive, out string error)
    {
        warriors ??= new List<WarriorData>();
        if (battleActive)
        {
            error = "Squad composition cannot be changed during battle.";
            return false;
        }
        if (warrior == null)
        {
            error = "Warrior data is missing.";
            return false;
        }
        if (warriors.Count >= MaximumWarriors)
        {
            error = $"A squad cannot contain more than {MaximumWarriors} warriors.";
            return false;
        }
        if (ContainsMemberId(warrior.id))
        {
            error = $"Duplicate squad member ID '{warrior.id}'.";
            return false;
        }

        warriors.Add(warrior);
        error = null;
        return true;
    }

    public bool TryRemoveWarrior(string warriorId, bool battleActive, out string error)
    {
        warriors ??= new List<WarriorData>();
        if (battleActive)
        {
            error = "Squad composition cannot be changed during battle.";
            return false;
        }
        if (warriors.Count <= MinimumWarriors)
        {
            error = "A squad must retain at least one warrior.";
            return false;
        }

        int index = warriors.FindIndex(warrior => warrior != null && warrior.id == warriorId);
        if (index < 0)
        {
            error = $"Warrior '{warriorId}' is not in this squad.";
            return false;
        }

        warriors.RemoveAt(index);
        error = null;
        return true;
    }

    public SquadValidationResult Validate()
    {
        List<string> errors = new List<string>();
        if (string.IsNullOrWhiteSpace(id))
            errors.Add("Squad ID is missing.");
        if (commander == null)
            errors.Add("Commander is required.");
        else
            commander.Validate(errors);

        if (warriors == null)
            errors.Add("Warrior collection is missing.");
        else if (status == PersistentSquadStatus.Active && warriors.Count < MinimumWarriors)
            errors.Add("An active squad requires at least one warrior.");
        else if (warriors.Count > MaximumWarriors)
            errors.Add($"No more than {MaximumWarriors} warriors are allowed.");

        HashSet<string> ids = new HashSet<string>();
        if (commander != null && !string.IsNullOrWhiteSpace(commander.id))
            ids.Add(commander.id);

        if (warriors != null)
        {
            foreach (WarriorData warrior in warriors)
            {
                if (warrior == null)
                {
                    errors.Add("Warrior data is missing.");
                    continue;
                }
                warrior.Validate(errors);
                if (!string.IsNullOrWhiteSpace(warrior.id) && !ids.Add(warrior.id))
                    errors.Add($"Duplicate member ID '{warrior.id}'.");
            }
        }

        permanentModifiers ??= new SquadStatModifiers();
        return new SquadValidationResult(errors);
    }

    internal bool ApplyPostBattleState(
        ISet<string> survivingWarriorIds,
        PersistentSquadStatus newStatus,
        PersistentDebuffDefinition debuff,
        string sourceBattleId,
        out string error)
    {
        if (survivingWarriorIds == null)
        {
            error = "Surviving Warrior IDs are missing.";
            return false;
        }

        HashSet<string> known = new HashSet<string>();
        foreach (WarriorData warrior in warriors)
        {
            if (warrior != null)
                known.Add(warrior.id);
        }
        foreach (string survivorId in survivingWarriorIds)
        {
            if (!known.Contains(survivorId))
            {
                error = $"Unknown surviving Warrior ID '{survivorId}'.";
                return false;
            }
        }

        warriors.RemoveAll(warrior =>
            warrior == null || !survivingWarriorIds.Contains(warrior.id));
        status = newStatus;
        permanentModifiers ??= new SquadStatModifiers();
        commander.permanentDebuffIds ??= new List<string>();
        commander.permanentDebuffs ??= new List<PersistentDebuffRecord>();
        if (debuff != null && !commander.permanentDebuffIds.Contains(debuff.StableId))
        {
            commander.permanentDebuffIds.Add(debuff.StableId);
            commander.permanentDebuffs.Add(new PersistentDebuffRecord
            {
                debuffId = debuff.StableId,
                sourceBattleId = sourceBattleId ?? string.Empty
            });
            if (debuff.ResolveModifier != 0f)
                permanentModifiers.resolve += debuff.ResolveModifier;
        }

        error = null;
        return true;
    }

    private bool ContainsMemberId(string memberId)
    {
        if (string.IsNullOrWhiteSpace(memberId))
            return true;
        if (commander != null && commander.id == memberId)
            return true;
        return warriors.Exists(warrior => warrior != null && warrior.id == memberId);
    }
}

[Serializable]
public sealed class CommanderData
{
    public string id;
    public CommanderRace race;
    public string commanderPortraitId;
    public SquadBaseStats baseStats = new SquadBaseStats();
    public List<string> permanentDebuffIds = new List<string>();
    public List<PersistentDebuffRecord> permanentDebuffs =
        new List<PersistentDebuffRecord>();

    public string CommanderPortraitId
    {
        get => commanderPortraitId;
        set => commanderPortraitId = value;
    }

    public void Validate(List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(id))
            errors.Add("Commander ID is missing.");
        if (baseStats == null)
            errors.Add("Commander base stats are missing.");
        else
            baseStats.Validate("Commander", errors);
        permanentDebuffIds ??= new List<string>();
        permanentDebuffs ??= new List<PersistentDebuffRecord>();
    }
}

[Serializable]
public sealed class WarriorData
{
    public string id;
    public int maxHP = 1;
    public float strength;
    public float dexterity;

    public void Validate(List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(id))
            errors.Add("Warrior ID is missing.");
        if (maxHP < 0 || strength < 0 || dexterity < 0)
            errors.Add($"Warrior '{id}' has negative base stats.");
    }
}

public sealed class SquadValidationResult
{
    public IReadOnlyList<string> Errors { get; }
    public bool IsValid => Errors.Count == 0;

    public SquadValidationResult(IReadOnlyList<string> errors)
    {
        Errors = errors ?? Array.Empty<string>();
    }

    public override string ToString() => string.Join(" ", Errors);
}
