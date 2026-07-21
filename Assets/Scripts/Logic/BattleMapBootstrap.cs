using UnityEngine;

public class BattleMapBootstrap : MonoBehaviour
{
    [Header("References")]
    public MapGenerator mapGenerator;
    public MapRenderer mapRenderer;
    public PlayerController playerController;
    public EnemyController enemyController;

    [Header("Fallback")]
    public int fallbackSeed = 0;

    [Header("Optional Battle Size Override")]
    public bool overrideBattleSize;
    [Min(4)] public int battleWidth = 16;
    [Min(4)] public int battleHeight = 16;
    [Min(4)] public int battlePlayableCount = 180;

    [Header("Startup")]
    [Range(0f, 3f)] public float bootstrapTimeoutSeconds = 1.5f;

    private bool hasBootstrapped;

    private void Start()
    {
        StartCoroutine(BootstrapRoutine());
    }

    private System.Collections.IEnumerator BootstrapRoutine()
    {
        if (hasBootstrapped)
            yield break;

        float deadline = Time.realtimeSinceStartup + bootstrapTimeoutSeconds;

        while ((mapGenerator == null || mapRenderer == null) && Time.realtimeSinceStartup < deadline)
        {
            TryAutoAssignReferences();
            yield return null;
        }

        TryAutoAssignReferences();

        if (mapGenerator == null || mapRenderer == null)
        {
            Debug.LogError("BattleMapBootstrap: missing MapGenerator/MapRenderer references. Assign them on this component or ensure they exist in battle scene.");
            yield break;
        }

        if (overrideBattleSize)
        {
            mapGenerator.width = battleWidth;
            mapGenerator.height = battleHeight;
            mapGenerator.playableCount = Mathf.Min(battlePlayableCount, battleWidth * battleHeight);
        }

        int battleSeed = BattleEncounterContext.CreateBattleSeed(fallbackSeed);
        mapGenerator.seed = battleSeed;

        mapGenerator.Generate();
        mapRenderer.RenderMap();

        SpawnBattleUnits();
        hasBootstrapped = true;

        Debug.Log($"BattleMapBootstrap: battle map generated with seed {battleSeed}.");

        // Keep encounter data available if battle scene needs it later.
        // BattleEncounterContext.Clear();
    }

    private void TryAutoAssignReferences()
    {
        if (mapGenerator == null)
            mapGenerator = FindAnyObjectByType<MapGenerator>();

        if (mapRenderer == null)
            mapRenderer = FindAnyObjectByType<MapRenderer>();

        if (playerController == null)
            playerController = FindAnyObjectByType<PlayerController>();

        if (enemyController == null)
            enemyController = FindAnyObjectByType<EnemyController>();
    }

    private void SpawnBattleUnits()
    {
        if (playerController == null || enemyController == null)
            return;

        playerController.SetMapReferences(mapGenerator, mapRenderer);
        enemyController.SetMapReferences(mapGenerator, mapRenderer, playerController);

        Vector2Int playerSource = BattleEncounterContext.HasEncounterData
            ? BattleEncounterContext.PlayerEncounterCell
            : new Vector2Int(0, 0);
        Vector2Int enemySource = BattleEncounterContext.HasEncounterData
            ? BattleEncounterContext.EnemyEncounterCell
            : new Vector2Int(mapGenerator.width - 1, mapGenerator.height - 1);

        Vector2Int dir = enemySource - playerSource;
        bool horizontal = Mathf.Abs(dir.x) >= Mathf.Abs(dir.y);

        // Player starts opposite enemy as in chess-style sides.
        bool enemyOnPositiveSide;
        if (horizontal)
            enemyOnPositiveSide = dir.x >= 0;
        else
            enemyOnPositiveSide = dir.y >= 0;

        Vector2Int playerSpawn = FindSideSpawnCell(horizontal, !enemyOnPositiveSide);
        Vector2Int enemySpawn = FindSideSpawnCell(horizontal, enemyOnPositiveSide, playerSpawn);

        playerController.ForceSpawnAtCell(playerSpawn);
        enemyController.ForceSpawnAtCell(enemySpawn);
    }

    private Vector2Int FindSideSpawnCell(bool horizontal, bool positiveSide, Vector2Int? avoidCell = null)
    {
        int width = mapGenerator.width;
        int height = mapGenerator.height;

        Vector2Int best = mapGenerator.GetCentralPlayableCell();
        float bestScore = float.MaxValue;

        float targetPrimary = horizontal
            ? (positiveSide ? width - 1 : 0)
            : (positiveSide ? height - 1 : 0);
        float targetSecondary = horizontal
            ? (height - 1) * 0.5f
            : (width - 1) * 0.5f;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (!mapGenerator.GetIsPlayable(x, y))
                    continue;

                Vector2Int cell = new Vector2Int(x, y);
                if (avoidCell.HasValue && cell == avoidCell.Value)
                    continue;

                float primary = horizontal ? x : y;
                float secondary = horizontal ? y : x;

                // Primary axis is weighted heavily to enforce opposite-side spawns.
                float score = Mathf.Abs(primary - targetPrimary) * 10f + Mathf.Abs(secondary - targetSecondary);

                if (score < bestScore)
                {
                    bestScore = score;
                    best = cell;
                }
            }
        }

        return best;
    }
}
