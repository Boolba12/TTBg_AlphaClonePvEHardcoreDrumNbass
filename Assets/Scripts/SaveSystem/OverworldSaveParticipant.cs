using System;
using UnityEngine;

public sealed class OverworldSaveParticipant : MonoBehaviour, ISaveable, ICoreSaveDataContributor
{
    [SerializeField] private MapGenerator mapGenerator;
    [SerializeField] private MapRenderer mapRenderer;
    [SerializeField] private MapRockPlacer mapRockPlacer;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private EnemyController enemyController;

    public string SaveKey => "overworld";

    public string CaptureState()
    {
        OverworldState state = new OverworldState
        {
            mapSeed = mapGenerator != null ? mapGenerator.seed : 0,
            playerCell = ToData(playerController != null ? playerController.CurrentCell : Vector2Int.zero),
            enemyCell = ToData(enemyController != null ? enemyController.CurrentCell : Vector2Int.zero)
        };
        return JsonUtility.ToJson(state);
    }

    public void RestoreState(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return;

        OverworldState state = JsonUtility.FromJson<OverworldState>(json);
        Restore(state.mapSeed, state.playerCell, state.enemyCell);
    }

    public void CaptureCoreData(GameSaveData data)
    {
        data.playerProgress.mapSeed = mapGenerator != null ? mapGenerator.seed : 0;
        data.playerProgress.hasOverworldPositions = playerController != null && enemyController != null;
        if (playerController != null)
            data.playerProgress.playerCell = ToData(playerController.CurrentCell);
        if (enemyController != null)
            data.playerProgress.enemyCell = ToData(enemyController.CurrentCell);
    }

    public void RestoreCoreData(GameSaveData data)
    {
        PlayerProgressData progress = data.playerProgress;
        if (progress == null || !progress.hasOverworldPositions)
            return;

        Restore(progress.mapSeed, progress.playerCell, progress.enemyCell);
    }

    private void Restore(int seed, Int2Data playerCell, Int2Data enemyCell)
    {
        if (mapGenerator == null || mapRenderer == null)
            throw new InvalidOperationException("Overworld save references are not configured.");

        mapGenerator.seed = seed;
        mapGenerator.Generate();
        mapRenderer.RenderMap();

        if (playerController != null)
        {
            playerController.SetMapReferences(mapGenerator, mapRenderer);
            playerController.ForceSpawnAtCell(ToVector(playerCell));
        }

        if (enemyController != null)
        {
            enemyController.SetMapReferences(mapGenerator, mapRenderer, playerController);
            enemyController.ForceSpawnAtCell(ToVector(enemyCell));
        }

        if (mapRockPlacer != null)
            mapRockPlacer.PlaceEnvironment();
    }

    private static Int2Data ToData(Vector2Int value) => new Int2Data(value.x, value.y);
    private static Vector2Int ToVector(Int2Data value) =>
        value == null ? Vector2Int.zero : new Vector2Int(value.x, value.y);

    [Serializable]
    private sealed class OverworldState
    {
        public int mapSeed;
        public Int2Data playerCell;
        public Int2Data enemyCell;
    }
}
