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
    public const int CurrentEquipmentSchemaVersion = 2;
    public const int MinimumWarriors = 1;
    public const int MaximumWarriors = 8;

    [SerializeField] private string id;
    [SerializeField] private CommanderData commander;
    [SerializeField] private List<WarriorData> warriors = new List<WarriorData>();
    [SerializeField] private SquadStatModifiers permanentModifiers = new SquadStatModifiers();
    [SerializeField] private SquadEquipmentState equipment = new SquadEquipmentState();
    [SerializeField] private int equipmentSchemaVersion = CurrentEquipmentSchemaVersion;
    [SerializeField] private PersistentSquadStatus status = PersistentSquadStatus.Active;

    public string Id => id;
    public CommanderData Commander => commander;
    public IReadOnlyList<WarriorData> Warriors => warriors;
    public SquadStatModifiers PermanentModifiers => permanentModifiers;
    public SquadEquipmentState Equipment
    {
        get
        {
            equipment ??= new SquadEquipmentState();
            equipment.EnsureInitialized();
            return equipment;
        }
    }
    public int EquipmentSchemaVersion => equipmentSchemaVersion;
    public PersistentSquadStatus Status => status;
    public bool IsCompositionValid => commander != null && warriors != null &&
                                      warriors.Count <= MaximumWarriors;
    public bool IsBattleEligible => status == PersistentSquadStatus.Active &&
                                    IsCompositionValid &&
                                    warriors.Count >= MinimumWarriors;

    public string CommanderPortraitId
    {
        get => commander != null ? commander.CommanderPortraitId : string.Empty;
        set
        {
            if (commander != null)
                commander.CommanderPortraitId = value;
        }
    }

    public void MarkEquipmentSchemaCurrent() =>
        equipmentSchemaVersion = CurrentEquipmentSchemaVersion;

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
        status = this.warriors.Count >= MinimumWarriors
            ? PersistentSquadStatus.Active
            : PersistentSquadStatus.InactiveNoWarriors;
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
        if (status == PersistentSquadStatus.CommanderLost || commander == null)
        {
            error = "A squad without an available Commander cannot receive Warriors.";
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
        status = PersistentSquadStatus.Active;
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
        int index = warriors.FindIndex(warrior => warrior != null && warrior.id == warriorId);
        if (index < 0)
        {
            error = $"Warrior '{warriorId}' is not in this squad.";
            return false;
        }

        warriors.RemoveAt(index);
        if (warriors.Count == 0 && status != PersistentSquadStatus.CommanderLost)
            status = PersistentSquadStatus.InactiveNoWarriors;
        error = null;
        return true;
    }

    public bool TryReplaceWarrior(
        string assignedWarriorId,
        WarriorData reserveWarrior,
        bool battleActive,
        out WarriorData removedWarrior,
        out string error)
    {
        removedWarrior = null;
        warriors ??= new List<WarriorData>();
        if (battleActive)
        {
            error = "Squad composition cannot be changed during battle.";
            return false;
        }
        if (status == PersistentSquadStatus.CommanderLost || commander == null)
        {
            error = "A squad without an available Commander cannot rotate Warriors.";
            return false;
        }
        if (reserveWarrior == null || string.IsNullOrWhiteSpace(reserveWarrior.id))
        {
            error = "Reserve Warrior data is missing or invalid.";
            return false;
        }

        int index = warriors.FindIndex(warrior =>
            warrior != null && warrior.id == assignedWarriorId);
        if (index < 0)
        {
            error = $"Warrior '{assignedWarriorId}' is not in this squad.";
            return false;
        }
        if (ContainsMemberId(reserveWarrior.id))
        {
            error = $"Duplicate squad member ID '{reserveWarrior.id}'.";
            return false;
        }

        removedWarrior = warriors[index];
        warriors[index] = reserveWarrior;
        status = PersistentSquadStatus.Active;
        error = null;
        return true;
    }

    public WarriorData GetWarrior(string warriorId)
    {
        if (warriors == null || string.IsNullOrWhiteSpace(warriorId))
            return null;
        return warriors.Find(warrior => warrior != null && warrior.id == warriorId);
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
        else if (status == PersistentSquadStatus.InactiveNoWarriors && warriors.Count > 0)
            errors.Add("InactiveNoWarriors status requires an empty Warrior composition.");
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
        equipment ??= new SquadEquipmentState();
        equipment.EnsureInitialized();
        ValidateEquipment(errors);
        return new SquadValidationResult(errors);
    }

    private void ValidateEquipment(List<string> errors)
    {
        HashSet<string> instanceIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (EquipmentItemInstance item in equipment.OwnedItems)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.InstanceId) ||
                string.IsNullOrWhiteSpace(item.DefinitionId))
            {
                errors.Add("Owned equipment contains an incomplete instance.");
                continue;
            }
            if (!instanceIds.Add(item.InstanceId))
                errors.Add($"Duplicate equipment instance ID '{item.InstanceId}'.");
        }

        HashSet<string> equipped = new HashSet<string>(StringComparer.Ordinal);
        foreach (EquipmentSlotKind slot in new[] { EquipmentSlotKind.SquadWeapon,
                     EquipmentSlotKind.CommanderWeapon, EquipmentSlotKind.Armor,
                     EquipmentSlotKind.Accessory })
        {
            string instanceId = equipment.GetEquippedInstanceId(slot);
            if (string.IsNullOrWhiteSpace(instanceId))
                continue;
            if (!instanceIds.Contains(instanceId))
                errors.Add($"{slot} references unowned instance '{instanceId}'.");
            else if (!equipped.Add(instanceId))
                errors.Add($"Equipment instance '{instanceId}' is assigned to multiple slots.");
        }
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
    public string displayName;
    public int maxHP = 1;
    public float strength;
    public float dexterity;

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? id : displayName;

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
