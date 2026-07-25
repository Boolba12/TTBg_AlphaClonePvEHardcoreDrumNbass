using System.Collections.Generic;
using UnityEngine;

public sealed class SquadBattleBootstrap : MonoBehaviour
{
    [SerializeField] private SquadSaveParticipant squadRepository;
    [SerializeField] private List<SquadBattleController> playerControllers =
        new List<SquadBattleController>();
    [SerializeField] private List<SquadBattleController> enemyControllers =
        new List<SquadBattleController>();

    public SquadInitiativeOrder InitiativeOrder { get; } = new SquadInitiativeOrder();

    public void InitializeSelectedSquads(Vector2Int playerCell, Vector2Int enemyCell)
    {
        if (!BattleSquadSelectionContext.HasSelection)
        {
            Debug.LogWarning(
                "SquadBattleBootstrap: no squad selection was provided; existing legacy battle setup remains active.",
                this);
            return;
        }

        InitializeSide(
            BattleSquadSelectionContext.PlayerSquads,
            playerControllers,
            playerCell,
            "player");
        InitializeSide(
            BattleSquadSelectionContext.EnemySquads,
            enemyControllers,
            enemyCell,
            "enemy");
    }

    private void InitializeSide(
        IReadOnlyList<SquadData> selected,
        List<SquadBattleController> controllers,
        Vector2Int cell,
        string side)
    {
        if (selected.Count > controllers.Count)
        {
            Debug.LogWarning(
                $"SquadBattleBootstrap: {selected.Count} {side} squad(s) selected, but only {controllers.Count} controller(s) are configured.",
                this);
        }

        int count = Mathf.Min(selected.Count, controllers.Count);
        for (int i = 0; i < count; i++)
        {
            SquadData squad = selected[i];
            SquadValidationResult validation = squad?.Validate();
            if (validation == null || !validation.IsValid)
            {
                Debug.LogWarning($"SquadBattleBootstrap: skipped invalid {side} squad. {validation}", this);
                continue;
            }

            SquadBattleController controller = controllers[i];
            SquadBattleState restoredState = squadRepository != null
                ? squadRepository.GetRestoredBattleState(squad.Id)
                : null;
            if (controller == null || !controller.Initialize(squad, restoredState))
                continue;

            controller.Runtime.SetLogicalCell(cell.x, cell.y);
            squadRepository?.RegisterRuntime(controller.Runtime);
            InitiativeOrder.Register(controller);
        }
    }
}
