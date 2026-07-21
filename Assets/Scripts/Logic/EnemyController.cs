using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyController : UnitController
{
    [Header("References")]
    public PlayerController playerController;

    [Header("Turn")]
    [Min(1)] public int maxStepsPerTurn = 4;
    [Min(1)] public int stopDistanceFromPlayer = 1;

    private void Update()
    {
        if (!TryPlaceOnSpawnCell())
            return;

        TickMovement();
    }

    public override void SetMapReferences(MapGenerator generator, MapRenderer renderer)
    {
        base.SetMapReferences(generator, renderer);
    }

    public void SetMapReferences(MapGenerator generator, MapRenderer renderer, PlayerController player)
    {
        SetMapReferences(generator, renderer);
        playerController = player;
    }

    public void ExecuteTurn(Action onComplete)
    {
        StartCoroutine(ExecuteTurnRoutine(onComplete));
    }

    private IEnumerator ExecuteTurnRoutine(Action onComplete)
    {
        while (!TryPlaceOnSpawnCell())
            yield return null;

        if (playerController == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        if (!TryBuildPath(CurrentCell, playerController.CurrentCell, out List<Vector2Int> path))
        {
            onComplete?.Invoke();
            yield break;
        }

        int maxApproachSteps = Mathf.Max(0, (path.Count - 1) - stopDistanceFromPlayer);
        int stepsToTake = Mathf.Min(maxStepsPerTurn, maxApproachSteps);
        if (stepsToTake <= 0)
        {
            onComplete?.Invoke();
            yield break;
        }

        QueuePathSteps(path, stepsToTake);

        while (QueuedStepCount > 0 || IsMovementBusy)
            yield return null;

        onComplete?.Invoke();
    }

    private bool TryPlaceOnSpawnCell()
    {
        if (hasPlaced)
            return true;

        if (mapGenerator == null)
            return false;

        Vector2Int origin = playerController != null ? playerController.CurrentCell : mapGenerator.GetCentralPlayableCell();
        Vector2Int spawnCell = FindFarthestPlayableCell(origin);
        return TryPlaceOnStartCell(spawnCell);
    }

    private Vector2Int FindFarthestPlayableCell(Vector2Int origin)
    {
        Vector2Int best = mapGenerator.GetCentralPlayableCell();
        int bestDist = -1;

        for (int x = 0; x < mapGenerator.width; x++)
        {
            for (int y = 0; y < mapGenerator.height; y++)
            {
                if (!mapGenerator.GetIsPlayable(x, y))
                    continue;

                int d = Mathf.Abs(x - origin.x) + Mathf.Abs(y - origin.y);
                if (d > bestDist)
                {
                    bestDist = d;
                    best = new Vector2Int(x, y);
                }
            }
        }

        return best;
    }
}
