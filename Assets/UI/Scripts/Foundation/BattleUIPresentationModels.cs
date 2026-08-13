using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct BattleSquadStatusModel
{
    public string SquadId { get; }
    public string CommanderId { get; }
    public Sprite CommanderPortrait { get; }
    public int CurrentHealth { get; }
    public int MaximumHealth { get; }
    public int CurrentActionPoints { get; }
    public int MaximumActionPoints { get; }
    public float CurrentMorale { get; }
    public float MaximumMorale { get; }
    public int LivingWarriors { get; }
    public int MaximumWarriors { get; }

    public BattleSquadStatusModel(
        string squadId,
        string commanderId,
        Sprite commanderPortrait,
        int currentHealth,
        int maximumHealth,
        int currentActionPoints,
        int maximumActionPoints,
        float currentMorale,
        float maximumMorale,
        int livingWarriors,
        int maximumWarriors)
    {
        SquadId = squadId ?? string.Empty;
        CommanderId = commanderId ?? string.Empty;
        CommanderPortrait = commanderPortrait;
        CurrentHealth = Math.Max(0, currentHealth);
        MaximumHealth = Math.Max(0, maximumHealth);
        CurrentActionPoints = Math.Max(0, currentActionPoints);
        MaximumActionPoints = Math.Max(0, maximumActionPoints);
        CurrentMorale = Math.Max(0f, currentMorale);
        MaximumMorale = Math.Max(0f, maximumMorale);
        LivingWarriors = Math.Max(0, livingWarriors);
        MaximumWarriors = Math.Max(0, maximumWarriors);
    }

    public static BattleSquadStatusModel FromRuntime(
        SquadBattleRuntime runtime,
        Sprite portrait)
    {
        if (runtime == null)
            return default;

        int livingWarriors = 0;
        if (runtime.State?.warriors != null)
        {
            foreach (WarriorBattleState warrior in runtime.State.warriors)
            {
                if (warrior != null && !warrior.defeated && warrior.currentHP > 0)
                    livingWarriors++;
            }
        }

        return new BattleSquadStatusModel(
            runtime.Data.Id,
            runtime.Data.Commander?.id,
            portrait,
            runtime.State.CurrentSquadHP,
            runtime.Stats.MaxHP,
            runtime.State.currentActionPoints,
            runtime.Stats.ActionPoints,
            runtime.State.currentMorale,
            runtime.Stats.Morale,
            livingWarriors,
            runtime.Data.Warriors?.Count ?? 0);
    }
}

public readonly struct InitiativeEntryModel
{
    public string SquadId { get; }
    public Sprite Portrait { get; }
    public float Initiative { get; }
    public BattleSide Side { get; }
    public SquadControlType ControlType { get; }
    public bool IsSelected { get; }
    public bool IsActive { get; }
    public bool IsDefeated { get; }

    public InitiativeEntryModel(
        string squadId,
        Sprite portrait,
        float initiative,
        BattleSide side,
        bool isSelected,
        bool isDefeated = false,
        SquadControlType controlType = SquadControlType.AI,
        bool isActive = false)
    {
        SquadId = squadId ?? string.Empty;
        Portrait = portrait;
        Initiative = initiative;
        Side = side;
        ControlType = controlType;
        IsSelected = isSelected;
        IsActive = isActive;
        IsDefeated = isDefeated;
    }
}

[Serializable]
public sealed class EquipmentSlotPresentationModel
{
    public string slotId;
    public EquipmentSlotKind kind;
    public string label;
    public Sprite icon;
    public bool occupied;
    public bool interactable;
}

[Serializable]
public sealed class CharacterScreenPresentationContract
{
    public string commanderId;
    public Sprite portrait;
    public List<string> statLabelKeys = new List<string>();
    public List<EquipmentSlotPresentationModel> equipmentSlots =
        new List<EquipmentSlotPresentationModel>();
    public List<string> compositionMemberIds = new List<string>();
    public List<string> debuffIds = new List<string>();
}

[Serializable]
public sealed class SquadScreenPresentationContract
{
    public string squadId;
    public CharacterScreenPresentationContract commander =
        new CharacterScreenPresentationContract();
    public List<string> compositionMemberIds = new List<string>();
    public List<string> debuffIds = new List<string>();
}
