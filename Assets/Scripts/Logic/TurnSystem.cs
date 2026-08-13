using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TurnSystem : MonoBehaviour
{
    [Header("References")]
    public PlayerController playerController;
    public EnemyController enemyController;
    public CameraFollow cameraFollow;
    [SerializeField] private SquadSaveParticipant squadRepository;
    [SerializeField] private SaveSystemBehaviour saveSystem;
    [SerializeField] private PreBattlePreparationController preBattlePreparationController;

    [Header("Timing")]
    [Range(0f, 1f)] public float cameraReturnDelay = 0.1f;

    [Header("Battle Encounter")]
    public bool loadBattleOnEncounter = true;
    public string battleSceneName = "BattleScene";
    [Min(1)] public int encounterTriggerDistance = 1;
    [Min(0)] public int initiativeBonusOnEngage = 10;

    private bool enemyTurnRunning;
    private bool battleLoadingTriggered;
    private bool preBattlePreparationOpen;
    private bool isSubscribedToPlayer;
    private Vector2Int lastResolvedPlayerCell;
    private bool hasLastResolvedPlayerCell;

    public bool IsEnemyTurnRunning => enemyTurnRunning;
    public bool IsPreBattlePreparationOpen => preBattlePreparationOpen;
    public bool IsBattleLoadingTriggered => battleLoadingTriggered;
    public string CurrentTurnLabel => enemyTurnRunning ? "Enemy" : "Player";

    private void Awake()
    {
        TryAutoAssignReferences();
    }

    private void OnEnable()
    {
        TryAutoAssignReferences();
        HookPlayerEvents();
    }

    private void OnDisable()
    {
        UnhookPlayerEvents();
    }

    private void Start()
    {
        TryAutoAssignReferences();
        HookPlayerEvents();

        if (playerController != null && playerController.enemyController == null)
            playerController.enemyController = enemyController;

        if (enemyController != null && enemyController.playerController == null)
            enemyController.playerController = playerController;

        if (playerController != null)
            playerController.SetPlayerTurn(true);

        if (cameraFollow != null && playerController != null)
            cameraFollow.target = playerController.transform;

        if (playerController != null)
        {
            lastResolvedPlayerCell = playerController.CurrentCell;
            hasLastResolvedPlayerCell = true;
        }
    }

    private void Update()
    {
        TryAutoAssignReferences();
        HookPlayerEvents();

        if (enemyTurnRunning || battleLoadingTriggered || preBattlePreparationOpen){
            return;}

        if (playerController == null || enemyController == null)
            return;

        if (!playerController.useTurnSystem)
            return;

        if (!hasLastResolvedPlayerCell)
        {
            lastResolvedPlayerCell = playerController.CurrentCell;
            hasLastResolvedPlayerCell = true;
            return;
        }

        if (playerController.IsMovementInProgress){
            Debug.Log("TurnSystem: Player movement in progress, waiting for completion.");
            return;}

        if (playerController.CurrentCell == lastResolvedPlayerCell)
            return;

        HandlePlayerTurnCompleted();
    }

    private void TryAutoAssignReferences()
    {
        if (Application.isPlaying &&
            (playerController == null || enemyController == null || cameraFollow == null ||
            squadRepository == null || saveSystem == null ||
            preBattlePreparationController == null))
        {
            Debug.LogError(
                "TurnSystem: production dependencies must be assigned explicitly in the Inspector.",
                this);
            enabled = false;
        }
    }

    private void HookPlayerEvents()
    {
        if (isSubscribedToPlayer || playerController == null)
            return;

        playerController.OnTurnMoveCompleted += HandlePlayerTurnCompleted;
        isSubscribedToPlayer = true;
    }

    private void UnhookPlayerEvents()
    {
        if (!isSubscribedToPlayer || playerController == null)
            return;

        playerController.OnTurnMoveCompleted -= HandlePlayerTurnCompleted;
        isSubscribedToPlayer = false;
    }

    private void HandlePlayerTurnCompleted()
    {
        if (playerController != null)
        {
            lastResolvedPlayerCell = playerController.CurrentCell;
            hasLastResolvedPlayerCell = true;
        }

        if (TryTriggerBattleEncounter(EncounterInitiator.Player))
            return;

        if (enemyTurnRunning || enemyController == null)
            return;

        StartCoroutine(RunEnemyTurn());
    }

    private IEnumerator RunEnemyTurn()
    {
        Debug.Log("TurnSystem: Enemy turn started."); // Works inside global map enviorment

        enemyTurnRunning = true;

        if (playerController != null)
            playerController.SetPlayerTurn(false);

        if (cameraFollow != null)
            cameraFollow.target = enemyController.transform;

        bool enemyDone = false;
        enemyController.ExecuteTurn(() => enemyDone = true);

        while (!enemyDone)
            yield return null;

        if (cameraReturnDelay > 0f)
            yield return new WaitForSeconds(cameraReturnDelay);

        if (cameraFollow != null && playerController != null)
            cameraFollow.target = playerController.transform;

        if (playerController != null)
            playerController.SetPlayerTurn(true);

        enemyTurnRunning = false;
        TryTriggerBattleEncounter(EncounterInitiator.Enemy);
    }

    private bool TryTriggerBattleEncounter(EncounterInitiator initiator)
    {
        if (!loadBattleOnEncounter || battleLoadingTriggered || preBattlePreparationOpen)
            return false;

        if (playerController == null || enemyController == null)
            return false;

        if (playerController.mapRenderer == null || !playerController.mapRenderer.HasMap)
            return false;

        if (enemyController.mapRenderer == null || !enemyController.mapRenderer.HasMap)
            return false;

        Vector2Int playerCellNow = playerController.CurrentCell;
        Vector2Int enemyCellNow = enemyController.CurrentCell;
        int manhattanDistance = Mathf.Abs(playerCellNow.x - enemyCellNow.x) + Mathf.Abs(playerCellNow.y - enemyCellNow.y);

        if (manhattanDistance > encounterTriggerDistance)
            return false;

        MapGenerator mapGenerator = playerController.mapGenerator;
        if (mapGenerator != null)
        {
            Vector2Int playerEncounterCell = playerCellNow;
            Vector2Int enemyEncounterCell = enemyCellNow;

            // If both units end on the same cell, keep each unit's previous cell as encounter origin.
            if (playerEncounterCell == enemyEncounterCell)
            {
                if (playerController.LastCell != playerEncounterCell)
                    playerEncounterCell = playerController.LastCell;

                if (enemyController.LastCell != enemyEncounterCell)
                    enemyEncounterCell = enemyController.LastCell;
            }

            BiomeType playerBiome = mapGenerator.GetBiomeAt(playerEncounterCell.x, playerEncounterCell.y);
            BiomeType enemyBiome = mapGenerator.GetBiomeAt(enemyEncounterCell.x, enemyEncounterCell.y);

            string encounterId = BattleEncounterContext.CreateEncounterId(
                mapGenerator.seed,
                playerEncounterCell,
                enemyEncounterCell);
            if (ResolvedEncounterRegistry.IsResolved(encounterId))
                return false;

            BattleEncounterContext.SetEncounterData(
                mapGenerator.seed,
                playerEncounterCell,
                enemyEncounterCell,
                playerBiome,
                enemyBiome,
                initiator,
                initiativeBonusOnEngage);
        }

        if (string.IsNullOrWhiteSpace(battleSceneName))
        {
            Debug.LogError("TurnSystem: battleSceneName is empty. Cannot load battle scene.");
            return false;
        }

        if (!Application.CanStreamedLevelBeLoaded(battleSceneName))
        {
            Debug.LogError(
                $"TurnSystem: Scene '{battleSceneName}' cannot be loaded. " +
                "Use an existing scene name (e.g. Raw_Alpha_BattleMode) and add it to File > Build Profiles.");
            return false;
        }

        if (preBattlePreparationController == null)
        {
            Debug.LogError(
                "TurnSystem: production encounter requires an explicit PreBattlePreparationController.");
            BattleSquadSelectionContext.Clear();
            BattleEncounterContext.Clear();
            return false;
        }

        BattleSquadSelectionContext.Clear();
        preBattlePreparationOpen = true;
        if (playerController != null)
            playerController.SetPlayerTurn(false);
        if (!preBattlePreparationController.TryOpenForActiveEncounter(out string openReason))
        {
            preBattlePreparationOpen = false;
            if (playerController != null)
                playerController.SetPlayerTurn(true);
            BattleEncounterContext.Clear();
            Debug.LogError($"TurnSystem: Pre-Battle preparation could not open: {openReason}");
            return false;
        }
        return true;
    }

    public bool ConfirmPreBattleSelection(string squadId, out string reason)
    {
        if (!preBattlePreparationOpen || battleLoadingTriggered)
        {
            reason = "No confirmable Pre-Battle encounter is active.";
            return false;
        }
        if (!BattleEncounterContext.HasEncounterData ||
            string.IsNullOrWhiteSpace(BattleEncounterContext.EncounterId))
        {
            reason = "Active encounter data was lost before confirmation.";
            return false;
        }
        if (!PreBattleSquadSelectionService.TryResolveEligible(
                squadRepository,
                squadId,
                out _,
                out reason))
        {
            return false;
        }
        if (string.IsNullOrWhiteSpace(battleSceneName) ||
            !Application.CanStreamedLevelBeLoaded(battleSceneName))
        {
            reason = $"Battle scene '{battleSceneName}' is unavailable.";
            return false;
        }
        if (!BattleSquadSelectionContext.SetPersistentEncounterSelection(
                squadId,
                BattleEncounterContext.EncounterId,
                true))
        {
            reason = "Persistent battle selection context rejected the confirmed squad.";
            return false;
        }

        battleLoadingTriggered = true;
        preBattlePreparationOpen = false;
        if (saveSystem == null || !saveSystem.PrepareCurrentDataForSceneRestore(true))
        {
            battleLoadingTriggered = false;
            preBattlePreparationOpen = true;
            BattleSquadSelectionContext.Clear();
            reason = "Persistent save data could not be prepared for the battle scene.";
            return false;
        }

        preBattlePreparationController?.CloseFromTurnSystem();
        reason = null;
        SceneManager.LoadScene(battleSceneName, LoadSceneMode.Single);
        return true;
    }

    public void CancelPreBattlePreparation()
    {
        if (!preBattlePreparationOpen || battleLoadingTriggered)
            return;

        preBattlePreparationOpen = false;
        BattleSquadSelectionContext.Clear();
        BattleEncounterContext.Clear();
        preBattlePreparationController?.CloseFromTurnSystem();
        if (playerController != null)
        {
            playerController.SetPlayerTurn(true);
            lastResolvedPlayerCell = playerController.CurrentCell;
            hasLastResolvedPlayerCell = true;
        }
    }
}
