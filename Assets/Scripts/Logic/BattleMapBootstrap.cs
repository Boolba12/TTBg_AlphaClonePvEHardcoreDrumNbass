using System.Collections.Generic;
using UnityEngine;

public enum BattleCombatMode
{
    LegacyUnits,
    Squads
}

public class BattleMapBootstrap : MonoBehaviour
{
    [Header("References")]
    public MapGenerator mapGenerator;
    public MapRenderer mapRenderer;
    public PlayerController playerController;
    public EnemyController enemyController;
    [SerializeField] private SquadBattleBootstrap squadBattleBootstrap;

    [Header("Combat Mode")]
    [SerializeField] private BattleCombatMode combatMode = BattleCombatMode.LegacyUnits;
    [Tooltip("The one legacy player root used by legacy initialization.")]
    [SerializeField] private GameObject legacyPlayerRoot;
    [Tooltip("The one legacy enemy root used by legacy initialization.")]
    [SerializeField] private GameObject legacyEnemyRoot;
    [Tooltip("Obsolete duplicate legacy roots. These always remain inactive.")]
    [SerializeField] private GameObject[] obsoleteLegacyCombatRoots;

    [Header("Fallback")]
    public int fallbackSeed;

    [Header("Optional Battle Size Override")]
    public bool overrideBattleSize;
    [Min(4)] public int battleWidth = 16;
    [Min(4)] public int battleHeight = 16;
    [Min(4)] public int battlePlayableCount = 180;

    [Header("Battle Movement")]
    public bool enableDiagonalMovement = true;

    [Header("Startup")]
    [Range(0f, 3f)] public float bootstrapTimeoutSeconds = 1.5f;

    [Header("Battle Setup")]
    public GameObject battleContextMenuRoot;
    [SerializeField] private BattleContextMenuUI battleSetupUI;
    [Tooltip(
        "Development-only: confirms through BattleContextMenuUI once when squad mode " +
        "is using a valid Inspector development fallback. Disabled by default.")]
    [SerializeField] private bool enableDevelopmentSquadAutoConfirm;

    private bool hasBootstrapped;
    private bool usedDevelopmentAutoConfirm;

    public BattleCombatMode CombatMode => combatMode;
    public bool HasBootstrapped => hasBootstrapped;
    public bool UsedDevelopmentAutoConfirm => usedDevelopmentAutoConfirm;
    public GameObject LegacyPlayerRoot => legacyPlayerRoot;
    public GameObject LegacyEnemyRoot => legacyEnemyRoot;
    public IReadOnlyList<GameObject> ObsoleteLegacyCombatRoots =>
        obsoleteLegacyCombatRoots;

    private void Awake()
    {
        ApplyConfiguredCombatMode();
    }

    private void Start()
    {
        BattleSetupContext.Reset();
        StartCoroutine(BootstrapRoutine());
    }

    public void ConfigureSquadMode(
        SquadBattleBootstrap bootstrap,
        BattleContextMenuUI setupUI,
        GameObject canonicalPlayerRoot,
        GameObject canonicalEnemyRoot,
        GameObject[] obsoleteRoots,
        bool developmentAutoConfirm)
    {
        combatMode = BattleCombatMode.Squads;
        squadBattleBootstrap = bootstrap;
        battleSetupUI = setupUI;
        battleContextMenuRoot = setupUI != null ? setupUI.menuRoot : null;
        legacyPlayerRoot = canonicalPlayerRoot;
        legacyEnemyRoot = canonicalEnemyRoot;
        obsoleteLegacyCombatRoots = obsoleteRoots;
        enableDevelopmentSquadAutoConfirm = developmentAutoConfirm;
    }

    private System.Collections.IEnumerator BootstrapRoutine()
    {
        if (hasBootstrapped)
            yield break;

        if (mapGenerator == null || mapRenderer == null)
        {
            Debug.LogError(
                "BattleMapBootstrap: MapGenerator and MapRenderer must be assigned explicitly.",
                this);
            yield break;
        }

        if (battleContextMenuRoot != null)
            battleContextMenuRoot.SetActive(true);

        TryDevelopmentAutoConfirm();

        while (!BattleSetupContext.IsConfirmed)
            yield return null;

        if (overrideBattleSize)
        {
            mapGenerator.width = battleWidth;
            mapGenerator.height = battleHeight;
            mapGenerator.playableCount = Mathf.Min(
                battlePlayableCount,
                battleWidth * battleHeight);
        }

        int battleSeed = BattleEncounterContext.CreateBattleSeed(fallbackSeed);
        mapGenerator.seed = battleSeed;

        mapGenerator.Generate();
        mapRenderer.RenderMap();

        if (!mapGenerator.HasGeneratedData ||
            !TryGetStartCells(out Vector2Int playerSpawn, out Vector2Int enemySpawn))
        {
            Debug.LogError(
                "BattleMapBootstrap: generated map does not contain two distinct playable start cells.",
                this);
            yield break;
        }

        bool initialized = combatMode == BattleCombatMode.Squads
            ? SpawnSquads(playerSpawn, enemySpawn)
            : SpawnLegacyUnits(playerSpawn, enemySpawn);
        if (!initialized)
            yield break;

        hasBootstrapped = true;
        Debug.Log(
            $"BattleMapBootstrap: battle map generated with seed {battleSeed}; mode={combatMode}.",
            this);

        // Keep encounter data available if battle scene needs it later.
        // BattleEncounterContext.Clear();
    }

    private bool SpawnSquads(Vector2Int playerSpawn, Vector2Int enemySpawn)
    {
        SetCanonicalLegacyActive(false);
        SetObsoleteLegacyInactive();
        if (squadBattleBootstrap == null)
        {
            Debug.LogError(
                "BattleMapBootstrap: squad mode requires an explicit SquadBattleBootstrap reference.",
                this);
            return false;
        }

        return squadBattleBootstrap.InitializeSquads(
            mapGenerator,
            mapRenderer,
            playerSpawn,
            enemySpawn);
    }

    private bool SpawnLegacyUnits(Vector2Int playerSpawn, Vector2Int enemySpawn)
    {
        SetObsoleteLegacyInactive();
        if (!HasValidCanonicalLegacyPair())
        {
            Debug.LogError(
                "BattleMapBootstrap: legacy mode requires one explicit canonical player/enemy " +
                "root matching the configured PlayerController and EnemyController.",
                this);
            SetCanonicalLegacyActive(false);
            return false;
        }

        SetCanonicalLegacyActive(true);
        playerController.SetMapReferences(mapGenerator, mapRenderer);
        enemyController.SetMapReferences(mapGenerator, mapRenderer, playerController);
        playerController.allowDiagonalMovement = enableDiagonalMovement;
        enemyController.allowDiagonalMovement = enableDiagonalMovement;

        int playerUnitCount = Mathf.Max(1, BattleSetupContext.PlayerUnitCount);
        Debug.Log(
            $"BattleMapBootstrap: starting legacy battle with {playerUnitCount} player unit(s).",
            this);

        playerController.ForceSpawnAtCell(playerSpawn);
        enemyController.ForceSpawnAtCell(enemySpawn);
        return true;
    }

    private bool TryGetStartCells(
        out Vector2Int playerSpawn,
        out Vector2Int enemySpawn)
    {
        Vector2Int playerSource = BattleEncounterContext.HasEncounterData
            ? BattleEncounterContext.PlayerEncounterCell
            : Vector2Int.zero;
        Vector2Int enemySource = BattleEncounterContext.HasEncounterData
            ? BattleEncounterContext.EnemyEncounterCell
            : new Vector2Int(mapGenerator.width - 1, mapGenerator.height - 1);

        Vector2Int direction = enemySource - playerSource;
        bool horizontal = Mathf.Abs(direction.x) >= Mathf.Abs(direction.y);
        bool enemyOnPositiveSide = horizontal
            ? direction.x >= 0
            : direction.y >= 0;

        bool playerFound = TryFindSideSpawnCell(
            horizontal,
            !enemyOnPositiveSide,
            null,
            out playerSpawn);
        bool enemyFound = TryFindSideSpawnCell(
            horizontal,
            enemyOnPositiveSide,
            playerFound ? playerSpawn : (Vector2Int?)null,
            out enemySpawn);
        return playerFound && enemyFound && playerSpawn != enemySpawn;
    }

    private bool TryFindSideSpawnCell(
        bool horizontal,
        bool positiveSide,
        Vector2Int? avoidCell,
        out Vector2Int best)
    {
        best = default;
        bool found = false;
        float bestScore = float.MaxValue;

        float targetPrimary = horizontal
            ? (positiveSide ? mapGenerator.width - 1 : 0)
            : (positiveSide ? mapGenerator.height - 1 : 0);
        float targetSecondary = horizontal
            ? (mapGenerator.height - 1) * 0.5f
            : (mapGenerator.width - 1) * 0.5f;

        for (int x = 0; x < mapGenerator.width; x++)
        {
            for (int y = 0; y < mapGenerator.height; y++)
            {
                if (!mapGenerator.GetIsPlayable(x, y))
                    continue;

                Vector2Int cell = new Vector2Int(x, y);
                if (avoidCell.HasValue && cell == avoidCell.Value)
                    continue;

                float primary = horizontal ? x : y;
                float secondary = horizontal ? y : x;
                float score =
                    Mathf.Abs(primary - targetPrimary) * 10f +
                    Mathf.Abs(secondary - targetSecondary);

                if (score >= bestScore)
                    continue;

                bestScore = score;
                best = cell;
                found = true;
            }
        }

        return found;
    }

    public void ApplyConfiguredCombatMode()
    {
        SetCanonicalLegacyActive(combatMode == BattleCombatMode.LegacyUnits);
        SetObsoleteLegacyInactive();
    }

    public void ConfigureLegacyRoots(
        GameObject canonicalPlayerRoot,
        GameObject canonicalEnemyRoot,
        GameObject[] obsoleteRoots)
    {
        legacyPlayerRoot = canonicalPlayerRoot;
        legacyEnemyRoot = canonicalEnemyRoot;
        obsoleteLegacyCombatRoots = obsoleteRoots;
    }

    private void TryDevelopmentAutoConfirm()
    {
        if (!enableDevelopmentSquadAutoConfirm)
            return;

        if (combatMode != BattleCombatMode.Squads)
        {
            Debug.LogError(
                "BattleMapBootstrap: development squad auto-confirm is enabled outside squad mode.",
                this);
            return;
        }

        if (squadBattleBootstrap == null)
        {
            Debug.LogError(
                "BattleMapBootstrap: development auto-confirm requires an explicit " +
                "SquadBattleBootstrap reference.",
                this);
            return;
        }

        if (!squadBattleBootstrap.CanUseDevelopmentFallback(out string fallbackReason))
        {
            Debug.LogError(
                "BattleMapBootstrap: development auto-confirm refused because the development " +
                $"fallback is not the valid active source. {fallbackReason}",
                this);
            return;
        }

        if (battleSetupUI == null)
        {
            Debug.LogError(
                "BattleMapBootstrap: development auto-confirm requires an explicit " +
                "BattleContextMenuUI reference.",
                this);
            return;
        }

        if (!battleSetupUI.TryConfirmBattleSetup(out string setupReason))
        {
            Debug.LogError(
                $"BattleMapBootstrap: development auto-confirm refused. {setupReason}",
                this);
            return;
        }

        usedDevelopmentAutoConfirm = true;
        Debug.Log(
            "BattleMapBootstrap: battle setup auto-confirmed once because squad mode is " +
            "explicitly configured to use the validated Inspector development fallback.",
            this);
    }

    private bool HasValidCanonicalLegacyPair()
    {
        return playerController != null &&
               enemyController != null &&
               legacyPlayerRoot != null &&
               legacyEnemyRoot != null &&
               IsControllerUnderRoot(playerController.transform, legacyPlayerRoot.transform) &&
               IsControllerUnderRoot(enemyController.transform, legacyEnemyRoot.transform);
    }

    private static bool IsControllerUnderRoot(Transform controller, Transform root)
    {
        return controller == root || controller.IsChildOf(root);
    }

    private void SetCanonicalLegacyActive(bool active)
    {
        SetRootActive(legacyPlayerRoot, active);
        SetRootActive(legacyEnemyRoot, active);
    }

    private void SetObsoleteLegacyInactive()
    {
        if (obsoleteLegacyCombatRoots == null)
            return;

        foreach (GameObject root in obsoleteLegacyCombatRoots)
            SetRootActive(root, false);
    }

    private void SetRootActive(GameObject root, bool active)
    {
        if (root != null && root != gameObject)
            root.SetActive(active);
    }
}
