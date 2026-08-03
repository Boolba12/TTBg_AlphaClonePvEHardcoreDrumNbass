using System.Collections.Generic;
using UnityEngine;

public sealed class SquadFormationView : MonoBehaviour
{
    [SerializeField] private Transform modelsContainer;
    [SerializeField] private Transform commanderSlot;
    [SerializeField] private List<Transform> warriorSlots = new List<Transform>();
    [SerializeField] private GameObject commanderModelPrefab;
    [SerializeField] private GameObject warriorModelPrefab;

    private readonly List<GameObject> warriorModels = new List<GameObject>();
    private SquadBattleRuntime boundRuntime;
    private GameObject commanderModel;

    public int CommanderModelCount => commanderModel != null ? 1 : 0;
    public int WarriorModelCount => warriorModels.Count;
    public int ActiveWarriorModelCount =>
        warriorModels.FindAll(model => model != null && model.activeSelf).Count;

    public bool Bind(SquadBattleRuntime runtime)
    {
        if (runtime == null)
            return false;

        Unbind();
        if (!EnsureModels(runtime.Data.Warriors.Count))
            return false;

        boundRuntime = runtime;
        UpdateFormation(runtime.State);
        runtime.OnWarriorDefeated += HandleWarriorDefeated;
        runtime.OnCommanderDefeated += HandleCommanderDefeated;
        runtime.OnSquadCompositionChanged += HandleCompositionChanged;
        return true;
    }

    public void Configure(
        Transform container,
        Transform commander,
        List<Transform> warriors,
        GameObject commanderPrefab,
        GameObject warriorPrefab)
    {
        modelsContainer = container;
        commanderSlot = commander;
        warriorSlots = warriors ?? new List<Transform>();
        commanderModelPrefab = commanderPrefab;
        warriorModelPrefab = warriorPrefab;
    }

    public void UpdateFormation(SquadBattleState state)
    {
        if (commanderModel != null)
        {
            commanderModel.SetActive(state?.commander != null && !state.commander.defeated);
            PlaceAt(commanderModel.transform, commanderSlot);
        }

        int stateCount = state?.warriors?.Count ?? 0;
        for (int i = 0; i < warriorModels.Count; i++)
        {
            GameObject model = warriorModels[i];
            if (model == null)
                continue;

            bool alive = i < stateCount &&
                         state.warriors[i] != null &&
                         !state.warriors[i].defeated &&
                         state.warriors[i].currentHP > 0;
            model.SetActive(alive);
            Transform slot = i < warriorSlots.Count ? warriorSlots[i] : null;
            PlaceAt(model.transform, slot);
        }
    }

    private void OnDestroy()
    {
        Unbind();
    }

    private bool EnsureModels(int warriorCount)
    {
        if (modelsContainer == null || commanderSlot == null ||
            commanderModelPrefab == null || warriorModelPrefab == null)
        {
            Debug.LogError("SquadFormationView: model container, slots, or model prefabs are missing.", this);
            return false;
        }

        if (warriorCount < SquadData.MinimumWarriors ||
            warriorCount > SquadData.MaximumWarriors ||
            warriorSlots.Count < warriorCount)
        {
            Debug.LogError(
                $"SquadFormationView: cannot display {warriorCount} warrior(s) with {warriorSlots.Count} configured slot(s).",
                this);
            return false;
        }

        if (commanderModel == null)
        {
            commanderModel = Instantiate(commanderModelPrefab, modelsContainer);
            commanderModel.name = "CommanderModel";
        }

        while (warriorModels.Count < warriorCount)
        {
            GameObject model = Instantiate(warriorModelPrefab, modelsContainer);
            model.name = $"WarriorModel_{warriorModels.Count + 1:00}";
            warriorModels.Add(model);
        }

        for (int i = warriorCount; i < warriorModels.Count; i++)
            warriorModels[i]?.SetActive(false);

        return true;
    }

    private void HandleWarriorDefeated(string warriorId)
    {
        if (boundRuntime != null)
            UpdateFormation(boundRuntime.State);
    }

    private void HandleCommanderDefeated()
    {
        if (boundRuntime != null)
            UpdateFormation(boundRuntime.State);
    }

    private void HandleCompositionChanged()
    {
        if (boundRuntime != null)
            UpdateFormation(boundRuntime.State);
    }

    private void Unbind()
    {
        if (boundRuntime == null)
            return;

        boundRuntime.OnWarriorDefeated -= HandleWarriorDefeated;
        boundRuntime.OnCommanderDefeated -= HandleCommanderDefeated;
        boundRuntime.OnSquadCompositionChanged -= HandleCompositionChanged;
        boundRuntime = null;
    }

    private void OnValidate()
    {
        if (warriorSlots.Count > SquadData.MaximumWarriors)
            warriorSlots.RemoveRange(
                SquadData.MaximumWarriors,
                warriorSlots.Count - SquadData.MaximumWarriors);
    }

    private static void PlaceAt(Transform model, Transform slot)
    {
        if (model == null || slot == null)
            return;

        model.SetParent(slot, false);
        model.localPosition = Vector3.zero;
        model.localRotation = Quaternion.identity;
    }
}
