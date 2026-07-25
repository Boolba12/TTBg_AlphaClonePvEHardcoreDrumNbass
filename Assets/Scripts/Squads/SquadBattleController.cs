using UnityEngine;

public sealed class SquadBattleController : MonoBehaviour
{
    [SerializeField] private UnitController movementController;
    [SerializeField] private SquadFormationView formationView;

    public SquadBattleRuntime Runtime { get; private set; }
    public string SquadId => Runtime?.Data?.Id;
    public bool IsInitialized => Runtime != null;
    public bool CanAct => Runtime != null && Runtime.CanAct;

    public bool Initialize(SquadData data, SquadBattleState restoredState = null)
    {
        if (Runtime != null)
        {
            Debug.LogWarning("SquadBattleController: already initialized.", this);
            return false;
        }

        SquadValidationResult validation = data?.Validate();
        if (validation == null || !validation.IsValid)
        {
            Debug.LogError($"SquadBattleController: invalid squad. {validation}", this);
            return false;
        }

        Runtime = new SquadBattleRuntime(data, restoredState);
        Runtime.OnSquadDefeated += HandleSquadDefeated;
        formationView?.Bind(Runtime);
        SyncLogicalCell();
        Runtime.NotifyCreated();
        return true;
    }

    public SquadDamageResult ReceiveFinalDamage(
        int finalDamage,
        SquadDamageDistribution distribution)
    {
        return Runtime != null
            ? Runtime.ApplyDamage(finalDamage, distribution)
            : new SquadDamageResult();
    }

    private void LateUpdate()
    {
        SyncLogicalCell();
    }

    private void HandleSquadDefeated()
    {
        if (movementController != null)
            movementController.enabled = false;
    }

    private void SyncLogicalCell()
    {
        if (Runtime == null || movementController == null)
            return;

        Vector2Int cell = movementController.CurrentCell;
        Runtime.SetLogicalCell(cell.x, cell.y);
    }
}
