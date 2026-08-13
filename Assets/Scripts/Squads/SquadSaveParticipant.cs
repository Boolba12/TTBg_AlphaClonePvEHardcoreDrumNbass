using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class SquadSavePayload
{
    public List<SquadData> squads = new List<SquadData>();
    public List<SquadBattleState> activeBattles = new List<SquadBattleState>();
    public List<string> appliedBattleIds = new List<string>();
}

public sealed class SquadSaveParticipant : MonoBehaviour, ISaveable
{
    [SerializeField] private List<SquadData> squads = new List<SquadData>();
    [Tooltip("Development-only until production mid-battle restore is integrated.")]
    [SerializeField] private bool saveActiveBattleState;
    [Header("Equipment migration")]
    [SerializeField] private EquipmentDefinitionCatalog equipmentCatalog;
    [SerializeField] private bool grantDevelopmentEquipmentToLegacySquads;

    private readonly Dictionary<string, SquadBattleRuntime> activeRuntimes =
        new Dictionary<string, SquadBattleRuntime>();
    private readonly Dictionary<string, SquadBattleState> restoredBattles =
        new Dictionary<string, SquadBattleState>();
    private readonly HashSet<string> appliedBattleIds =
        new HashSet<string>(StringComparer.Ordinal);

    public string SaveKey => "squads";
    public IReadOnlyList<SquadData> Squads => squads;
    public int ActiveRuntimeCount => activeRuntimes.Count;
    public bool HasAppliedBattle(string battleId) =>
        !string.IsNullOrWhiteSpace(battleId) && appliedBattleIds.Contains(battleId);

    public bool MarkBattleApplied(string battleId)
    {
        return !string.IsNullOrWhiteSpace(battleId) && appliedBattleIds.Add(battleId);
    }

    public void SetActiveBattleStateSaving(bool enabled)
    {
        saveActiveBattleState = enabled;
    }

    public void ConfigureEquipmentMigration(
        EquipmentDefinitionCatalog catalog,
        bool enabled)
    {
        equipmentCatalog = catalog;
        grantDevelopmentEquipmentToLegacySquads = enabled;
        MigrateLegacyEquipment();
    }

    public bool TryAddSquad(SquadData squad, out string error)
    {
        SquadValidationResult validation = squad?.Validate();
        if (validation == null || !validation.IsValid)
        {
            error = $"Invalid squad. {validation}";
            return false;
        }
        if (squads.Exists(existing => existing != null && existing.Id == squad.Id))
        {
            error = $"Duplicate squad ID '{squad.Id}'.";
            return false;
        }

        squads.Add(squad);
        error = null;
        return true;
    }

    public SquadData GetSquad(string squadId)
    {
        return squads.Find(squad => squad != null && squad.Id == squadId);
    }

    public SquadBattleState GetRestoredBattleState(string squadId)
    {
        restoredBattles.TryGetValue(squadId, out SquadBattleState state);
        return state;
    }

    public void RegisterRuntime(SquadBattleRuntime runtime)
    {
        if (runtime?.Data == null)
            return;
        activeRuntimes[runtime.Data.Id] = runtime;
    }

    public void UnregisterRuntime(string squadId)
    {
        if (!string.IsNullOrWhiteSpace(squadId))
            activeRuntimes.Remove(squadId);
    }

    public string CaptureState()
    {
        SquadSavePayload payload = new SquadSavePayload
        {
            squads = new List<SquadData>(squads),
            appliedBattleIds = new List<string>(appliedBattleIds)
        };

        if (saveActiveBattleState)
        {
            foreach (SquadBattleRuntime runtime in activeRuntimes.Values)
                payload.activeBattles.Add(runtime.State);
        }
        return JsonUtility.ToJson(payload);
    }

    public void RestoreState(string json)
    {
        SquadSavePayload payload = string.IsNullOrWhiteSpace(json)
            ? new SquadSavePayload()
            : JsonUtility.FromJson<SquadSavePayload>(json);

        squads = new List<SquadData>();
        HashSet<string> squadIds = new HashSet<string>();
        if (payload?.squads != null)
        {
            foreach (SquadData squad in payload.squads)
            {
                SquadValidationResult validation = squad?.Validate();
                if (validation == null || !validation.IsValid)
                {
                    Debug.LogWarning($"Squad save: skipped invalid squad. {validation}");
                    continue;
                }
                if (!squadIds.Add(squad.Id))
                {
                    Debug.LogWarning($"Squad save: skipped duplicate squad ID '{squad.Id}'.");
                    continue;
                }
                squads.Add(squad);
            }
        }

        activeRuntimes.Clear();
        restoredBattles.Clear();
        appliedBattleIds.Clear();
        if (payload?.appliedBattleIds != null)
        {
            foreach (string battleId in payload.appliedBattleIds)
            {
                if (!string.IsNullOrWhiteSpace(battleId))
                    appliedBattleIds.Add(battleId);
            }
        }
        if (payload?.activeBattles == null)
        {
            MigrateLegacyEquipment();
            return;
        }

        foreach (SquadBattleState battle in payload.activeBattles)
        {
            if (battle == null || string.IsNullOrWhiteSpace(battle.squadId) ||
                GetSquad(battle.squadId) == null || restoredBattles.ContainsKey(battle.squadId))
            {
                continue;
            }
            restoredBattles.Add(battle.squadId, battle);
        }
        MigrateLegacyEquipment();
    }

    private void MigrateLegacyEquipment()
    {
        if (!grantDevelopmentEquipmentToLegacySquads || equipmentCatalog == null)
            return;
        SquadEquipmentService service = new SquadEquipmentService(equipmentCatalog);
        foreach (SquadData squad in squads)
        {
            if (squad == null)
                continue;
            foreach (EquipmentItemDefinition definition in
                     equipmentCatalog.EnumerateDefinitions())
            {
                if (definition == null || HasDefinition(squad, definition.StableId))
                    continue;
                service.GrantOwnedItem(squad,
                    $"{squad.Id}-dev-item-{definition.StableId}", definition.StableId);
            }
            if (equipmentCatalog.Weapons.Count > 0)
                service.TryEquip(squad,
                    $"{squad.Id}-dev-item-{equipmentCatalog.Weapons[0].StableId}",
                    EquipmentSlotKind.SquadWeapon);
            if (equipmentCatalog.Weapons.Count > 1)
                service.TryEquip(squad,
                    $"{squad.Id}-dev-item-{equipmentCatalog.Weapons[1].StableId}",
                    EquipmentSlotKind.CommanderWeapon);
            squad.MarkEquipmentSchemaCurrent();
        }
    }

    private static bool HasDefinition(SquadData squad, string definitionId)
    {
        for (int i = 0; i < squad.Equipment.OwnedItems.Count; i++)
        {
            EquipmentItemInstance item = squad.Equipment.OwnedItems[i];
            if (item != null && string.Equals(item.DefinitionId, definitionId,
                    StringComparison.Ordinal))
                return true;
        }
        return false;
    }
}
