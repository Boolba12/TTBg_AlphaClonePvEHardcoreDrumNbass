using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class SquadSavePayload
{
    public int rosterSchemaVersion = 1;
    public List<SquadData> squads = new List<SquadData>();
    public List<WarriorData> reserveWarriors = new List<WarriorData>();
    public List<string> deceasedWarriorIds = new List<string>();
    public List<SquadBattleState> activeBattles = new List<SquadBattleState>();
    public List<string> appliedBattleIds = new List<string>();
}

public sealed class SquadSaveParticipant : MonoBehaviour, ISaveable
{
    [SerializeField] private List<SquadData> squads = new List<SquadData>();
    [Header("Player roster")]
    [SerializeField] private List<WarriorData> reserveWarriors = new List<WarriorData>();
    [SerializeField] private List<string> deceasedWarriorIds = new List<string>();
    [Tooltip("Adds deterministic development Warriors only when their stable IDs are absent.")]
    [SerializeField] private bool ensureDevelopmentReserve;
    [SerializeField, Range(0, 16)] private int developmentReserveSize = 8;
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
    public IReadOnlyList<WarriorData> ReserveWarriors => reserveWarriors;
    public IReadOnlyList<string> DeceasedWarriorIds => deceasedWarriorIds;
    public int ActiveRuntimeCount => activeRuntimes.Count;
    public bool IsCompositionLocked => activeRuntimes.Count > 0 ||
                                       BattleSquadSelectionContext.HasSelection;
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

    public void ConfigureDevelopmentReserve(bool enabled, int size = 8)
    {
        ensureDevelopmentReserve = enabled;
        developmentReserveSize = Math.Max(0, Math.Min(16, size));
        EnsureDevelopmentReserveInitialized();
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
        if (!CanOwnSquadMembers(squad, out error))
            return false;

        squads.Add(squad);
        error = null;
        return true;
    }

    public SquadData GetSquad(string squadId)
    {
        return squads.Find(squad => squad != null && squad.Id == squadId);
    }

    public WarriorData GetReserveWarrior(string warriorId)
    {
        if (string.IsNullOrWhiteSpace(warriorId))
            return null;
        return reserveWarriors.Find(warrior => warrior != null && warrior.id == warriorId);
    }

    public string GetAssignedSquadId(string warriorId)
    {
        if (string.IsNullOrWhiteSpace(warriorId))
            return null;
        for (int i = 0; i < squads.Count; i++)
        {
            SquadData squad = squads[i];
            if (squad?.GetWarrior(warriorId) != null)
                return squad.Id;
        }
        return null;
    }

    public bool TryAddReserveWarrior(WarriorData warrior, out string error)
    {
        reserveWarriors ??= new List<WarriorData>();
        deceasedWarriorIds ??= new List<string>();
        List<string> validation = new List<string>();
        warrior?.Validate(validation);
        if (warrior == null || validation.Count > 0)
        {
            error = warrior == null ? "Reserve Warrior data is missing."
                : string.Join(" ", validation);
            return false;
        }
        if (ContainsPersistentEntityId(warrior.id))
        {
            error = $"Persistent roster already contains ID '{warrior.id}'.";
            return false;
        }
        reserveWarriors.Add(warrior);
        SortReserve();
        error = null;
        return true;
    }

    public bool ValidateRosterInvariants(out string error)
    {
        HashSet<string> squadIds = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> memberIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < squads.Count; i++)
        {
            SquadData squad = squads[i];
            SquadValidationResult validation = squad?.Validate();
            if (validation == null || !validation.IsValid || !squadIds.Add(squad.Id))
            {
                error = $"Persistent squad at index {i} is invalid or duplicated. {validation}";
                return false;
            }
            if (!memberIds.Add(squad.Commander.id))
            {
                error = $"Duplicate persistent member ID '{squad.Commander.id}'.";
                return false;
            }
            for (int warriorIndex = 0; warriorIndex < squad.Warriors.Count; warriorIndex++)
            {
                WarriorData warrior = squad.Warriors[warriorIndex];
                if (warrior == null || !memberIds.Add(warrior.id))
                {
                    error = $"Duplicate or missing persistent Warrior in squad '{squad.Id}'.";
                    return false;
                }
            }
        }
        for (int i = 0; i < reserveWarriors.Count; i++)
        {
            WarriorData warrior = reserveWarriors[i];
            List<string> validation = new List<string>();
            warrior?.Validate(validation);
            if (warrior == null || validation.Count > 0 || !memberIds.Add(warrior.id))
            {
                error = "Reserve contains invalid or duplicate Warrior data.";
                return false;
            }
        }
        HashSet<string> deceased = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < deceasedWarriorIds.Count; i++)
        {
            string id = deceasedWarriorIds[i];
            if (string.IsNullOrWhiteSpace(id) || !deceased.Add(id) || memberIds.Contains(id))
            {
                error = $"Deceased Warrior ID '{id}' is invalid, duplicate, or still living.";
                return false;
            }
        }
        error = null;
        return true;
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
            reserveWarriors = new List<WarriorData>(reserveWarriors),
            deceasedWarriorIds = new List<string>(deceasedWarriorIds),
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
        reserveWarriors = new List<WarriorData>();
        deceasedWarriorIds = new List<string>();
        HashSet<string> squadIds = new HashSet<string>();
        HashSet<string> memberIds = new HashSet<string>(StringComparer.Ordinal);
        if (payload?.deceasedWarriorIds != null)
        {
            foreach (string warriorId in payload.deceasedWarriorIds)
            {
                if (!string.IsNullOrWhiteSpace(warriorId) &&
                    !deceasedWarriorIds.Contains(warriorId))
                    deceasedWarriorIds.Add(warriorId);
            }
        }
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
                if (!squadIds.Add(squad.Id) || !TryRegisterMembers(squad, memberIds))
                {
                    Debug.LogWarning($"Squad save: skipped duplicate squad or member IDs for '{squad.Id}'.");
                    continue;
                }
                squads.Add(squad);
            }
        }
        if (payload?.reserveWarriors != null)
        {
            foreach (WarriorData warrior in payload.reserveWarriors)
            {
                List<string> validation = new List<string>();
                warrior?.Validate(validation);
                if (warrior == null || validation.Count > 0 ||
                    deceasedWarriorIds.Contains(warrior.id) || !memberIds.Add(warrior.id))
                {
                    Debug.LogWarning("Squad save: skipped invalid or duplicate Reserve Warrior.");
                    continue;
                }
                reserveWarriors.Add(warrior);
            }
        }
        SortReserve();

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
            EnsureDevelopmentReserveInitialized();
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
        EnsureDevelopmentReserveInitialized();
    }

    internal int FindReserveIndex(string warriorId) => reserveWarriors.FindIndex(
        warrior => warrior != null && warrior.id == warriorId);

    internal WarriorData RemoveReserveAt(int index)
    {
        WarriorData warrior = reserveWarriors[index];
        reserveWarriors.RemoveAt(index);
        return warrior;
    }

    internal void InsertReserveAt(int index, WarriorData warrior)
    {
        reserveWarriors.Insert(Math.Max(0, Math.Min(index, reserveWarriors.Count)), warrior);
        SortReserve();
    }

    internal void AddReserveUnchecked(WarriorData warrior)
    {
        reserveWarriors.Add(warrior);
        SortReserve();
    }

    internal void ReplaceReserveAt(int index, WarriorData warrior)
    {
        reserveWarriors[index] = warrior;
        SortReserve();
    }

    internal bool MarkWarriorsDeceased(IEnumerable<string> warriorIds, out string error)
    {
        deceasedWarriorIds ??= new List<string>();
        if (warriorIds == null)
        {
            error = "Defeated Warrior IDs are missing.";
            return false;
        }
        foreach (string warriorId in warriorIds)
        {
            if (string.IsNullOrWhiteSpace(warriorId))
            {
                error = "A defeated Warrior ID is missing.";
                return false;
            }
            if (GetAssignedSquadId(warriorId) != null)
            {
                error = $"Defeated Warrior '{warriorId}' is still assigned to a squad.";
                return false;
            }
            reserveWarriors.RemoveAll(warrior => warrior != null && warrior.id == warriorId);
            if (!deceasedWarriorIds.Contains(warriorId))
                deceasedWarriorIds.Add(warriorId);
        }
        deceasedWarriorIds.Sort(StringComparer.Ordinal);
        error = null;
        return true;
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

    private bool CanOwnSquadMembers(SquadData squad, out string error)
    {
        if (ContainsPersistentEntityId(squad.Commander.id))
        {
            error = $"Persistent roster already contains ID '{squad.Commander.id}'.";
            return false;
        }
        for (int i = 0; i < squad.Warriors.Count; i++)
        {
            WarriorData warrior = squad.Warriors[i];
            if (warrior == null || ContainsPersistentEntityId(warrior.id))
            {
                error = $"Persistent roster already contains Warrior ID '{warrior?.id}'.";
                return false;
            }
        }
        error = null;
        return true;
    }

    private bool ContainsPersistentEntityId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || deceasedWarriorIds.Contains(id) ||
            reserveWarriors.Exists(warrior => warrior != null && warrior.id == id))
            return true;
        for (int i = 0; i < squads.Count; i++)
        {
            SquadData squad = squads[i];
            if (squad?.Commander?.id == id || squad?.GetWarrior(id) != null)
                return true;
        }
        return false;
    }

    private static bool TryRegisterMembers(SquadData squad, ISet<string> memberIds)
    {
        if (squad?.Commander == null || !memberIds.Add(squad.Commander.id))
            return false;
        List<string> added = new List<string> { squad.Commander.id };
        for (int i = 0; i < squad.Warriors.Count; i++)
        {
            WarriorData warrior = squad.Warriors[i];
            if (warrior == null || !memberIds.Add(warrior.id))
            {
                for (int rollback = 0; rollback < added.Count; rollback++)
                    memberIds.Remove(added[rollback]);
                return false;
            }
            added.Add(warrior.id);
        }
        return true;
    }

    private void EnsureDevelopmentReserveInitialized()
    {
        if (!ensureDevelopmentReserve)
            return;
        reserveWarriors ??= new List<WarriorData>();
        deceasedWarriorIds ??= new List<string>();
        for (int i = 1; i <= developmentReserveSize; i++)
        {
            string id = $"dev-reserve-warrior-{i:00}";
            if (ContainsPersistentEntityId(id))
                continue;
            reserveWarriors.Add(new WarriorData
            {
                id = id,
                displayName = $"Reserve Warrior {i:00}",
                maxHP = 8 + i % 3,
                strength = 1.5f + (i % 4) * .5f,
                dexterity = 1f + (i % 3) * .5f
            });
        }
        SortReserve();
    }

    private void SortReserve() => reserveWarriors.Sort((left, right) =>
        StringComparer.Ordinal.Compare(left?.id, right?.id));

    private void Awake() => EnsureDevelopmentReserveInitialized();
}
