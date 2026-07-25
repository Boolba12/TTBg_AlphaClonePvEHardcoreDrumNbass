using System.Collections.Generic;
using UnityEngine;

public sealed class SquadFormationView : MonoBehaviour
{
    [SerializeField] private GameObject commanderModel;
    [SerializeField] private Transform commanderSlot;
    [SerializeField] private List<GameObject> warriorModels = new List<GameObject>();
    [SerializeField] private List<Transform> warriorSlots = new List<Transform>();

    public void Bind(SquadBattleRuntime runtime)
    {
        if (runtime == null)
            return;

        UpdateFormation(runtime.State);
        runtime.OnWarriorDefeated += _ => UpdateFormation(runtime.State);
        runtime.OnCommanderDefeated += () => UpdateFormation(runtime.State);
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

    private void OnValidate()
    {
        if (warriorModels.Count > SquadData.MaximumWarriors)
            warriorModels.RemoveRange(
                SquadData.MaximumWarriors,
                warriorModels.Count - SquadData.MaximumWarriors);
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
