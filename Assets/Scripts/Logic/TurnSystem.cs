using System.Collections;
using UnityEngine;

public class TurnSystem : MonoBehaviour
{
    [Header("References")]
    public PlayerController playerController;
    public EnemyController enemyController;
    public CameraFollow cameraFollow;

    [Header("Timing")]
    [Range(0f, 1f)] public float cameraReturnDelay = 0.1f;

    private bool enemyTurnRunning;

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
        if (playerController != null)
            playerController.SetPlayerTurn(true);

        if (cameraFollow != null && playerController != null)
            cameraFollow.target = playerController.transform;
    }

    private void HandlePlayerTurnCompleted()
    {
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
    }
}
