using UnityEngine;

public enum BattleSide
{
    Player,
    Enemy
}

public enum SquadControlType
{
    Human,
    AI
}

public sealed class SquadBattleController : MonoBehaviour
{
    [SerializeField] private SquadGridAnchor gridAnchor;
    [SerializeField] private SquadFormationView formationView;

    private bool battleContextAssigned;
    private int registrationSequence = -1;

    public SquadBattleRuntime Runtime { get; private set; }
    public string SquadId => Runtime?.Data?.Id;
    public bool IsInitialized => Runtime != null;
    public bool CanAct => Runtime != null && Runtime.CanAct;
    public SquadGridAnchor GridAnchor => gridAnchor;
    public SquadFormationView FormationView => formationView;
    public bool HasBattleContext => battleContextAssigned;
    public BattleSide Side { get; private set; }
    public SquadControlType ControlType { get; private set; }
    public int RegistrationSequence => registrationSequence;

    public bool Initialize(SquadData data, SquadBattleState restoredState = null)
    {
        return InitializeInternal(data, restoredState);
    }

    public bool InitializeAtCell(
        SquadData data,
        SquadBattleState restoredState,
        MapGenerator mapGenerator,
        MapRenderer mapRenderer,
        Vector2Int cell,
        BattleSide side,
        SquadControlType controlType,
        int sequence)
    {
        if (!AssignBattleContext(side, controlType, sequence))
            return false;

        if (gridAnchor == null)
        {
            Debug.LogError("SquadBattleController: SquadGridAnchor is not configured.", this);
            return false;
        }

        if (!gridAnchor.PlaceAtCell(mapGenerator, mapRenderer, cell))
            return false;

        return InitializeInternal(data, restoredState);
    }

    public bool AssignBattleContext(
        BattleSide side,
        SquadControlType controlType,
        int sequence)
    {
        if (battleContextAssigned || Runtime != null || sequence < 0)
        {
            Debug.LogWarning(
                "SquadBattleController: battle context can only be assigned once before initialization.",
                this);
            return false;
        }

        Side = side;
        ControlType = controlType;
        registrationSequence = sequence;
        battleContextAssigned = true;
        return true;
    }

    public void Configure(SquadGridAnchor anchor, SquadFormationView view)
    {
        gridAnchor = anchor;
        formationView = view;
    }

    private bool InitializeInternal(SquadData data, SquadBattleState restoredState)
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
        if (gridAnchor != null)
            gridAnchor.CellChanged += HandleCellChanged;

        if (formationView != null && !formationView.Bind(Runtime))
        {
            Unsubscribe();
            Runtime = null;
            return false;
        }

        if (gridAnchor != null && gridAnchor.IsPlaced)
            HandleCellChanged(gridAnchor.CurrentCell);

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

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void HandleSquadDefeated()
    {
        if (gridAnchor != null)
            gridAnchor.enabled = false;
    }

    private void HandleCellChanged(Vector2Int cell)
    {
        Runtime?.SetLogicalCell(cell.x, cell.y);
    }

    private void Unsubscribe()
    {
        if (Runtime != null)
            Runtime.OnSquadDefeated -= HandleSquadDefeated;
        if (gridAnchor != null)
            gridAnchor.CellChanged -= HandleCellChanged;
    }
}
