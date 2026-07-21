using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TurnSystem : MonoBehaviour
{
    [Header("References")]
    public PlayerController playerController;
    public EnemyController enemyController;
    public CameraFollow cameraFollow;

    [Header("Timing")]
    [Range(0f, 1f)] public float cameraReturnDelay = 0.1f;

    [Header("Battle Encounter")]
    public bool loadBattleOnEncounter = true;
    public string battleSceneName = "BattleScene";
    [Min(1)] public int encounterTriggerDistance = 1;
    [Min(0)] public int initiativeBonusOnEngage = 10;

    private bool enemyTurnRunning;
    private bool battleLoadingTriggered;

    public bool IsEnemyTurnRunning => enemyTurnRunning;
    public string CurrentTurnLabel => enemyTurnRunning ? "Enemy" : "Player";

    private void OnEnable()
    {
        if (playerController != null)
            playerController.OnTurnMoveCompleted += HandlePlayerTurnCompleted;
    }

    private void OnDisable()
    {
        if (playerController != null)
            playerController.OnTurnMoveCompleted -= HandlePlayerTurnCompleted;
    }

    private void Start()
    {
        if (playerController != null && playerController.enemyController == null)
            playerController.enemyController = enemyController;

        if (enemyController != null && enemyController.playerController == null)
            enemyController.playerController = playerController;

        if (playerController != null)
            playerController.SetPlayerTurn(true);

        if (cameraFollow != null && playerController != null)
            cameraFollow.target = playerController.transform;
    }

    private void HandlePlayerTurnCompleted()
    {
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

        SceneManager.LoadScene(battleSceneName, LoadSceneMode.Single);
        return true;
    }
}
