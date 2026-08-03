using System.Collections.Generic;
using UnityEngine;

public enum SquadBootstrapState
{
    NotInitialized,
    Initializing,
    Initialized,
    Failed
}

public sealed class SquadBattleBootstrap : MonoBehaviour
{
    [Header("Composition")]
    [SerializeField] private SquadBattleController squadBattlePrefab;
    [SerializeField] private Transform squadContainer;
    [SerializeField] private SquadSaveParticipant squadRepository;

    [Header("Saved-roster mapping (optional)")]
    [Tooltip("When empty, the first valid saved squad is used for the player.")]
    [SerializeField] private string savedPlayerSquadId;
    [Tooltip("When empty, the next valid distinct saved squad is used for the enemy.")]
    [SerializeField] private string savedEnemySquadId;

    [Header("Development fallback")]
    [SerializeField] private bool enableDevelopmentFallback;
    [SerializeField] private SquadData developmentPlayerSquad;
    [SerializeField] private SquadData developmentEnemySquad;

    [Header("Diagnostics")]
    [SerializeField] private bool enableDevelopmentLogs = true;

    private readonly List<SquadBattleController> spawnedControllers =
        new List<SquadBattleController>();
    private int nextRegistrationSequence;

    public SquadInitiativeOrder InitiativeOrder { get; } = new SquadInitiativeOrder();
    public SquadBootstrapState State { get; private set; } = SquadBootstrapState.NotInitialized;
    public IReadOnlyList<SquadBattleController> SpawnedControllers => spawnedControllers;
    public bool HasBootstrapped => State == SquadBootstrapState.Initialized;
    public string FailureReason { get; private set; }
    public bool DevelopmentFallbackEnabled => enableDevelopmentFallback;

    public bool InitializeSquads(
        MapGenerator mapGenerator,
        MapRenderer mapRenderer,
        Vector2Int playerCell,
        Vector2Int enemyCell)
    {
        if (State != SquadBootstrapState.NotInitialized)
        {
            Debug.LogWarning(
                $"SquadBattleBootstrap: ignored repeated initialization while state is {State}.",
                this);
            return false;
        }

        FailureReason = null;
        nextRegistrationSequence = 0;
        State = SquadBootstrapState.Initializing;

        if (!ValidateCompositionReferences(mapGenerator, mapRenderer))
            return Fail("required composition references or generated map data are missing");

        if (playerCell == enemyCell)
            return Fail($"player and enemy start cells both resolve to {playerCell}");

        if (!IsPlayableCell(mapGenerator, playerCell) ||
            !IsPlayableCell(mapGenerator, enemyCell))
        {
            return Fail(
                $"start cells must be distinct playable cells (player {playerCell}, enemy {enemyCell})");
        }

        if (!TryResolveSquads(
                out SquadData playerSquad,
                out SquadData enemySquad,
                out string playerSource,
                out string enemySource,
                out bool consumeSelection,
                out string sourceError))
        {
            return Fail(sourceError);
        }

        if (!SpawnSquad(
                BattleSide.Player,
                SquadControlType.Human,
                playerSquad,
                playerSource,
                playerCell,
                mapGenerator,
                mapRenderer) ||
            !SpawnSquad(
                BattleSide.Enemy,
                SquadControlType.AI,
                enemySquad,
                enemySource,
                enemyCell,
                mapGenerator,
                mapRenderer))
        {
            return Fail("one or more squad representations could not be created");
        }

        State = SquadBootstrapState.Initialized;
        if (consumeSelection)
        {
            BattleSquadSelectionContext.Consume();
            Log("consumed BattleSquadSelectionContext after both battle participants were created.");
        }
        Log(
            $"completed with {spawnedControllers.Count} squad(s) and " +
            $"{InitiativeOrder.Entries.Count} initiative entry/entries.");
        return true;
    }

    public bool ResetFailedStateForRetry()
    {
        if (State != SquadBootstrapState.Failed)
            return false;

        CleanupPartialBootstrap();
        FailureReason = null;
        nextRegistrationSequence = 0;
        State = SquadBootstrapState.NotInitialized;
        return true;
    }

    public bool CanUseDevelopmentFallback(out string reason)
    {
        if (BattleSquadSelectionContext.HasSelection)
        {
            reason =
                "BattleSquadSelectionContext contains data, so development fallback must not mask it.";
            return false;
        }

        if (squadRepository != null && squadRepository.Squads.Count > 0)
        {
            reason =
                "SquadSaveParticipant contains roster data, so development fallback is not active.";
            return false;
        }

        if (!enableDevelopmentFallback)
        {
            reason = "Development fallback is disabled.";
            return false;
        }

        return ValidateDevelopmentFallback(out reason);
    }

    public void Configure(
        SquadBattleController prefab,
        Transform container,
        SquadSaveParticipant repository,
        bool developmentFallbackEnabled,
        SquadData playerFallback,
        SquadData enemyFallback,
        bool developmentLogsEnabled = true)
    {
        squadBattlePrefab = prefab;
        squadContainer = container;
        squadRepository = repository;
        enableDevelopmentFallback = developmentFallbackEnabled;
        developmentPlayerSquad = playerFallback;
        developmentEnemySquad = enemyFallback;
        enableDevelopmentLogs = developmentLogsEnabled;
    }

    private bool SpawnSquad(
        BattleSide side,
        SquadControlType controlType,
        SquadData data,
        string source,
        Vector2Int cell,
        MapGenerator mapGenerator,
        MapRenderer mapRenderer)
    {
        SquadValidationResult validation = data?.Validate();
        if (validation == null || !validation.IsValid)
        {
            Debug.LogError(
                $"SquadBattleBootstrap: invalid {side.ToString().ToLowerInvariant()} squad. {validation}",
                this);
            return false;
        }

        SquadBattleController controller = Instantiate(squadBattlePrefab, squadContainer);
        controller.name = $"{side}Squad_{data.Id}";
        int registrationSequence = nextRegistrationSequence++;

        if (!controller.InitializeAtCell(
                data,
                null,
                mapGenerator,
                mapRenderer,
                cell,
                side,
                controlType,
                registrationSequence))
        {
            controller.gameObject.SetActive(false);
            DestroyObject(controller.gameObject);
            return false;
        }

        if (!InitiativeOrder.Register(controller))
        {
            Debug.LogError(
                $"SquadBattleBootstrap: initiative rejected squad '{data.Id}'.",
                this);
            controller.gameObject.SetActive(false);
            DestroyObject(controller.gameObject);
            return false;
        }

        spawnedControllers.Add(controller);
        squadRepository?.RegisterRuntime(controller.Runtime);
        Log(
            $"{side.ToString().ToLowerInvariant()} source={source}, id={data.Id}, " +
            $"control={controlType}, sequence={registrationSequence}, warriors={data.Warriors.Count}, " +
            $"cell={cell}; runtime created and initiative registered.");
        return true;
    }

    private bool TryResolveSquads(
        out SquadData playerSquad,
        out SquadData enemySquad,
        out string playerSource,
        out string enemySource,
        out bool consumeSelection,
        out string error)
    {
        playerSquad = null;
        enemySquad = null;
        playerSource = null;
        enemySource = null;
        consumeSelection = false;
        error = null;

        bool contextProvided =
            BattleSquadSelectionContext.PlayerSquads.Count > 0 ||
            BattleSquadSelectionContext.EnemySquads.Count > 0;
        if (contextProvided)
        {
            playerSquad = FirstValid(BattleSquadSelectionContext.PlayerSquads);
            enemySquad = FirstValid(
                BattleSquadSelectionContext.EnemySquads,
                playerSquad?.Id);
            if (playerSquad == null || enemySquad == null)
            {
                error =
                    "BattleSquadSelectionContext was provided but did not contain one valid, distinct squad per side; fallback was not used";
                return false;
            }

            playerSource = "BattleSquadSelectionContext";
            enemySource = "BattleSquadSelectionContext";
            consumeSelection = true;
            return true;
        }

        if (squadRepository != null && squadRepository.Squads.Count > 0)
        {
            playerSquad = ResolveSavedSquad(savedPlayerSquadId, null);
            enemySquad = ResolveSavedSquad(savedEnemySquadId, playerSquad?.Id);
            if (playerSquad == null || enemySquad == null)
            {
                error =
                    "SquadSaveParticipant contains roster data but could not resolve one valid, distinct squad per side; development fallback was not used";
                return false;
            }

            playerSource = "SquadSaveParticipant";
            enemySource = "SquadSaveParticipant";
            return true;
        }

        if (!CanUseDevelopmentFallback(out error))
        {
            if (!enableDevelopmentFallback &&
                !BattleSquadSelectionContext.HasSelection &&
                (squadRepository == null || squadRepository.Squads.Count == 0))
            {
                error =
                    "no squad selection or saved roster is available and development fallback is disabled";
            }
            return false;
        }

        playerSquad = developmentPlayerSquad;
        enemySquad = developmentEnemySquad;
        playerSource = "Inspector development fallback";
        enemySource = "Inspector development fallback";
        return true;
    }

    private bool ValidateDevelopmentFallback(out string reason)
    {
        SquadValidationResult playerValidation = developmentPlayerSquad?.Validate();
        SquadValidationResult enemyValidation = developmentEnemySquad?.Validate();
        if (playerValidation == null || !playerValidation.IsValid ||
            enemyValidation == null || !enemyValidation.IsValid ||
            developmentPlayerSquad.Id == developmentEnemySquad.Id)
        {
            reason =
                $"Development fallback squads are invalid or not distinct. " +
                $"Player: {playerValidation} Enemy: {enemyValidation}";
            return false;
        }

        reason = null;
        return true;
    }

    private SquadData ResolveSavedSquad(string configuredId, string excludedId)
    {
        if (!string.IsNullOrWhiteSpace(configuredId))
        {
            SquadData configured = squadRepository.GetSquad(configuredId);
            return IsValidDistinct(configured, excludedId) ? configured : null;
        }

        return FirstValid(squadRepository.Squads, excludedId);
    }

    private bool ValidateCompositionReferences(
        MapGenerator mapGenerator,
        MapRenderer mapRenderer)
    {
        return mapGenerator != null &&
               mapRenderer != null &&
               mapGenerator.HasGeneratedData &&
               squadBattlePrefab != null &&
               squadContainer != null;
    }

    private static SquadData FirstValid(
        IReadOnlyList<SquadData> squads,
        string excludedId = null)
    {
        if (squads == null)
            return null;

        for (int i = 0; i < squads.Count; i++)
        {
            SquadData candidate = squads[i];
            if (IsValidDistinct(candidate, excludedId))
                return candidate;
        }

        return null;
    }

    private static bool IsValidDistinct(SquadData squad, string excludedId)
    {
        SquadValidationResult validation = squad?.Validate();
        return validation != null &&
               validation.IsValid &&
               squad.Id != excludedId;
    }

    private static bool IsPlayableCell(MapGenerator mapGenerator, Vector2Int cell)
    {
        return cell.x >= 0 &&
               cell.x < mapGenerator.width &&
               cell.y >= 0 &&
               cell.y < mapGenerator.height &&
               mapGenerator.GetIsPlayable(cell.x, cell.y);
    }

    private bool Fail(string reason)
    {
        CleanupPartialBootstrap();
        FailureReason = reason;
        State = SquadBootstrapState.Failed;
        Debug.LogError($"SquadBattleBootstrap: bootstrap failed: {reason}.", this);
        return false;
    }

    private void CleanupPartialBootstrap()
    {
        for (int i = spawnedControllers.Count - 1; i >= 0; i--)
        {
            SquadBattleController controller = spawnedControllers[i];
            if (controller != null)
            {
                squadRepository?.UnregisterRuntime(controller.SquadId);
                controller.gameObject.SetActive(false);
                DestroyObject(controller.gameObject);
            }
        }

        spawnedControllers.Clear();
        InitiativeOrder.Clear();
    }

    private void OnDestroy()
    {
        InitiativeOrder.Clear();
    }

    private void Log(string message)
    {
        if (enableDevelopmentLogs)
            Debug.Log($"SquadBattleBootstrap: {message}", this);
    }

    private static void DestroyObject(GameObject target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            Object.Destroy(target);
        else
            Object.DestroyImmediate(target);
    }
}
