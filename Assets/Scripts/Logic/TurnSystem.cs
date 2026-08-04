using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;

public class TurnSystem : MonoBehaviour
{
    [Header("References")]
    public PlayerController playerController;
    public EnemyController enemyController;
    public CameraFollow cameraFollow;
    [SerializeField] private SquadSaveParticipant squadRepository;
    [SerializeField] private SaveSystemBehaviour saveSystem;

    [Header("Timing")]
    [Range(0f, 1f)] public float cameraReturnDelay = 0.1f;

    [Header("Battle Encounter")]
    public bool loadBattleOnEncounter = true;
    public string battleSceneName = "BattleScene";
    [Min(1)] public int encounterTriggerDistance = 1;
    [Min(0)] public int initiativeBonusOnEngage = 10;

    private bool enemyTurnRunning;
    private bool battleLoadingTriggered;
    private bool isSubscribedToPlayer;
    private Vector2Int lastResolvedPlayerCell;
    private bool hasLastResolvedPlayerCell;

    public bool IsEnemyTurnRunning => enemyTurnRunning;
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

        if (enemyTurnRunning || battleLoadingTriggered)
            return;

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

        if (playerController.IsMovementInProgress)
            return;

        if (playerController.CurrentCell == lastResolvedPlayerCell)
            return;

        HandlePlayerTurnCompleted();
    }

    private void TryAutoAssignReferences()
    {
        if (playerController == null)
            playerController = FindAnyObjectByType<PlayerController>();

        if (enemyController == null)
            enemyController = FindAnyObjectByType<EnemyController>();

        if (cameraFollow == null)
            cameraFollow = FindAnyObjectByType<CameraFollow>();
        if (squadRepository == null)
            squadRepository = FindAnyObjectByType<SquadSaveParticipant>();
        if (saveSystem == null)
            saveSystem = FindAnyObjectByType<SaveSystemBehaviour>();
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
        if (!loadBattleOnEncounter || battleLoadingTriggered)
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

            if (squadRepository != null && squadRepository.Squads.Count > 0)
            {
                SquadData playerSquad = squadRepository.Squads.FirstOrDefault(
                    squad => squad != null && squad.IsBattleEligible);
                if (playerSquad == null)
                {
                    Debug.LogWarning(
                        "TurnSystem: no battle-eligible persistent Player squad is available.");
                    return false;
                }
                BattleSquadSelectionContext.SetSelection(
                    new[] { playerSquad },
                    null);
            }
            else
            {
                // The very first development encounter has no persistent roster yet.
                // Raw_Alpha_BattleMode creates and registers its explicit fallback once;
                // later encounters use only the restored eligible persistent squad.
                BattleSquadSelectionContext.Clear();
            }

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

        battleLoadingTriggered = true;

        saveSystem?.PrepareCurrentDataForSceneRestore(true);

        SceneManager.LoadScene(battleSceneName, LoadSceneMode.Single);
        return true;
    }
}
