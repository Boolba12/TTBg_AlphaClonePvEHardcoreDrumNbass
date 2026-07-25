using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class SquadSavePayload
{
    public List<SquadData> squads = new List<SquadData>();
    public List<SquadBattleState> activeBattles = new List<SquadBattleState>();
}

public sealed class SquadSaveParticipant : MonoBehaviour, ISaveable
{
    [SerializeField] private List<SquadData> squads = new List<SquadData>();
    [SerializeField] private bool saveActiveBattleState = true;

    private readonly Dictionary<string, SquadBattleRuntime> activeRuntimes =
        new Dictionary<string, SquadBattleRuntime>();
    private readonly Dictionary<string, SquadBattleState> restoredBattles =
        new Dictionary<string, SquadBattleState>();

    public string SaveKey => "squads";
    public IReadOnlyList<SquadData> Squads => squads;

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

    public string CaptureState()
    {
        SquadSavePayload payload = new SquadSavePayload
        {
            squads = new List<SquadData>(squads)
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
        if (payload?.activeBattles == null)
            return;

        foreach (SquadBattleState battle in payload.activeBattles)
        {
            if (battle == null || string.IsNullOrWhiteSpace(battle.squadId) ||
                GetSquad(battle.squadId) == null || restoredBattles.ContainsKey(battle.squadId))
            {
                continue;
            }
            restoredBattles.Add(battle.squadId, battle);
        }
    }
}
