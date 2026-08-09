using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BattleCompletionController : MonoBehaviour
{
    [SerializeField] private SquadBattleBootstrap squadBootstrap;
    [SerializeField] private BattleTurnController turnController;
    [SerializeField] private BattleCommandModeController commandMode;
    [SerializeField] private SquadMovementService movementService;
    [SerializeField] private MovementCommandController movementCommands;
    [SerializeField] private BattleAttackService attackService;
    [SerializeField] private AttackCommandController attackCommands;
    [SerializeField] private BattleAbilityService abilityService;
    [SerializeField] private AbilityCommandController abilityCommands;
    [SerializeField] private BattleHUDController battleHud;
    [SerializeField] private SquadSaveParticipant squadRepository;
    [SerializeField] private SaveSystemBehaviour saveSystem;
    [SerializeField] private PostBattleRules postBattleRules;
    [SerializeField] private BattleResultPanelView resultPanel;
    [SerializeField] private string returnSceneName = "first_try";

    private readonly Dictionary<string, Action> defeatHandlers =
        new Dictionary<string, Action>(StringComparer.Ordinal);
    private readonly BattleResultBuilder resultBuilder = new BattleResultBuilder();
    private bool initialized;
    private bool evaluationPending;
    private bool continueRequested;
    private int committedResolutionDepth;
    private string encounterId;
    private int battleSeed;
    private Func<BattleOutcome, BattleResultApplicationResult> applicationOverride;
    private Func<string, string, SaveOperationResult> autosaveOverride;
    private Action<string> sceneLoadOverride;
    private Func<string, IPostBattleRandomSource> randomFactoryOverride;

    public BattleCompletionState State { get; private set; } = BattleCompletionState.Running;
    public BattleOutcome Outcome { get; private set; }
    public string FailureReason { get; private set; }
    public int CompletionCount { get; private set; }
    public int AutosaveAttemptCount { get; private set; }
    public int TransitionRequestCount { get; private set; }
    public bool CanContinue => State == BattleCompletionState.Completed &&
                               Outcome != null && Outcome.persistentMutationsApplied &&
                               Outcome.autosaveSucceeded && !continueRequested;

    public event Action<BattleOutcome> OnBattleCompleting;
    public event Action<BattleOutcome> OnBattleCompleted;
    public event Action<string> OnBattleCompletionFailed;

    public void Configure(
        SquadBattleBootstrap bootstrap,
        BattleTurnController turns,
        BattleCommandModeController modes,
        SquadMovementService movement,
        MovementCommandController movementController,
        BattleAttackService attacks,
        AttackCommandController attackController,
        BattleHUDController hud,
        SquadSaveParticipant repository,
        SaveSystemBehaviour saves,
        PostBattleRules rules,
        BattleResultPanelView panel,
        string configuredReturnScene = "first_try")
    {
        UnbindListeners();
        squadBootstrap = bootstrap;
        turnController = turns;
        commandMode = modes;
        movementService = movement;
        movementCommands = movementController;
        attackService = attacks;
        attackCommands = attackController;
        battleHud = hud;
        squadRepository = repository;
        saveSystem = saves;
        postBattleRules = rules;
        resultPanel = panel;
        returnSceneName = configuredReturnScene;
    }

    public void ConfigureTestSeams(
        Func<BattleOutcome, BattleResultApplicationResult> apply,
        Func<string, string, SaveOperationResult> autosave,
        Action<string> sceneLoader = null,
        Func<string, IPostBattleRandomSource> randomFactory = null)
    {
        applicationOverride = apply;
        autosaveOverride = autosave;
        sceneLoadOverride = sceneLoader;
        randomFactoryOverride = randomFactory;
    }

    public void ConfigureAbilities(
        BattleAbilityService configuredAbilityService,
        AbilityCommandController configuredAbilityCommands)
    {
        abilityService = configuredAbilityService;
        abilityCommands = configuredAbilityCommands;
    }

    public bool Initialize(
        string configuredBattleId = null,
        string configuredStartedUtc = null)
    {
        if (initialized || squadBootstrap == null || !squadBootstrap.HasBootstrapped ||
            turnController == null || !turnController.HasStarted || commandMode == null ||
            movementService == null || movementCommands == null || attackService == null ||
            attackCommands == null || battleHud == null || squadRepository == null ||
            postBattleRules == null || resultPanel == null ||
            (saveSystem == null && autosaveOverride == null) ||
            string.IsNullOrWhiteSpace(returnSceneName))
        {
            return Fail("Battle completion dependencies are incomplete.");
        }

        encounterId = BattleEncounterContext.EncounterId ?? string.Empty;
        battleSeed = BattleEncounterContext.CreateBattleSeed(42);
        string battleId = string.IsNullOrWhiteSpace(configuredBattleId)
            ? $"battle-{Guid.NewGuid():N}"
            : configuredBattleId;
        if (!resultBuilder.Initialize(
                squadBootstrap.SpawnedControllers,
                battleId,
                encounterId,
                battleSeed,
                configuredStartedUtc))
        {
            return Fail("Initial battle participant snapshot is invalid.");
        }

        State = BattleCompletionState.Running;
        FailureReason = null;
        BindListeners();
        resultPanel.Hide();
        initialized = true;
        return true;
    }

    public bool EvaluateCompletion()
    {
        if (!initialized || State != BattleCompletionState.Running)
            return false;
        if (attackService.IsExecuting || committedResolutionDepth > 0)
        {
            evaluationPending = true;
            return false;
        }
        if (!TryResolveResult(out BattleResultType resultType,
                out BattleSide winningSide,
                out BattleSide losingSide))
        {
            evaluationPending = false;
            return false;
        }

        return Complete(resultType, winningSide, losingSide);
    }

    public bool BeginCommittedResolution()
    {
        if (!initialized || State != BattleCompletionState.Running)
            return false;
        committedResolutionDepth++;
        return true;
    }

    public bool EndCommittedResolution()
    {
        if (committedResolutionDepth <= 0)
            return false;
        committedResolutionDepth--;
        if (committedResolutionDepth == 0 && evaluationPending)
            EvaluateCompletion();
        return true;
    }

    public bool RetryAutosave()
    {
        if (State != BattleCompletionState.Completed || Outcome == null ||
            !Outcome.persistentMutationsApplied || Outcome.autosaveSucceeded)
        {
            return false;
        }
        SaveOperationResult result = AutosaveOutcome();
        resultPanel.ShowSaveState(result);
        return result.Success;
    }

    public bool ContinueToOverworld()
    {
        if (!CanContinue)
            return false;

        continueRequested = true;
        State = BattleCompletionState.Transitioning;
        string resolvedEncounterId = Outcome.resultType == BattleResultType.Victory
            ? Outcome.encounterId
            : string.Empty;
        BattleReturnData returnData = new BattleReturnData
        {
            outcome = Outcome,
            persistentMutationsApplied = true,
            targetScene = returnSceneName,
            autosaveSucceeded = true
        };
        if (!BattleReturnContext.Set(returnData) ||
            (saveSystem != null && !saveSystem.PrepareCurrentDataForSceneRestore()))
        {
            continueRequested = false;
            State = BattleCompletionState.Completed;
            return FailTransition("Return context could not be prepared.");
        }

        if (!string.IsNullOrWhiteSpace(resolvedEncounterId))
            ResolvedEncounterRegistry.MarkResolved(resolvedEncounterId);
        BattleSquadSelectionContext.Clear();
        BattleSetupContext.ClearConfirmation();
        BattleEncounterContext.Clear();
        TransitionRequestCount++;
        if (sceneLoadOverride != null)
            sceneLoadOverride(returnSceneName);
        else
            SceneManager.LoadSceneAsync(returnSceneName, LoadSceneMode.Single);
        return true;
    }

    private bool Complete(
        BattleResultType resultType,
        BattleSide winningSide,
        BattleSide losingSide)
    {
        State = BattleCompletionState.Completing;
        CompletionCount++;
        LockBattleCommands();
        BattleOutcomeBuildResult build = resultBuilder.Build(
            resultType,
            winningSide,
            losingSide,
            turnController.CurrentRound,
            turnController.CompletedTurnCount,
            null,
            abilityService?.CreateUsageRecords());
        if (!build.Success)
            return Fail(build.Error);

        Outcome = build.Outcome;
        OnBattleCompleting?.Invoke(Outcome);
        BattleResultApplicationResult applied = applicationOverride != null
            ? applicationOverride(Outcome)
            : CreateResultApplier().Apply(Outcome);
        if (!applied.Success)
            return Fail(applied.Error);

        Outcome.persistentMutationsApplied = true;
        SaveOperationResult saveResult = AutosaveOutcome();
        State = BattleCompletionState.Completed;
        resultPanel.Show(Outcome, saveResult);
        OnBattleCompleted?.Invoke(Outcome);
        return true;
    }

    private BattleResultApplier CreateResultApplier()
    {
        return new BattleResultApplier(
            squadRepository,
            postBattleRules,
            commanderId => randomFactoryOverride != null
                ? randomFactoryOverride(commanderId)
                : new SeededPostBattleRandomSource(
                    CreateStablePostBattleSeed(battleSeed, Outcome.battleId, commanderId)));
    }

    private SaveOperationResult AutosaveOutcome()
    {
        AutosaveAttemptCount++;
        string resolved = Outcome.resultType == BattleResultType.Victory
            ? Outcome.encounterId
            : string.Empty;
        SaveOperationResult result = autosaveOverride != null
            ? autosaveOverride(returnSceneName, resolved)
            : saveSystem.AutosaveBattleResult(returnSceneName, resolved);
        Outcome.autosaveSucceeded = result.Success;
        return result;
    }

    private bool TryResolveResult(
        out BattleResultType resultType,
        out BattleSide winningSide,
        out BattleSide losingSide)
    {
        bool playerAlive = squadBootstrap.SpawnedControllers.Any(
            controller => controller != null && controller.Side == BattleSide.Player &&
                          controller.CanAct);
        bool enemyAlive = squadBootstrap.SpawnedControllers.Any(
            controller => controller != null && controller.Side == BattleSide.Enemy &&
                          controller.CanAct);
        if (playerAlive && enemyAlive)
        {
            resultType = default;
            winningSide = default;
            losingSide = default;
            return false;
        }
        if (!playerAlive && !enemyAlive)
        {
            resultType = BattleResultType.Draw;
            winningSide = default;
            losingSide = default;
            return true;
        }
        resultType = playerAlive ? BattleResultType.Victory : BattleResultType.Defeat;
        winningSide = playerAlive ? BattleSide.Player : BattleSide.Enemy;
        losingSide = playerAlive ? BattleSide.Enemy : BattleSide.Player;
        return true;
    }

    private void LockBattleCommands()
    {
        commandMode.CancelAndLock();
        movementService.SetCommandsEnabled(false);
        movementCommands.SetBattleCommandsEnabled(false);
        attackService.SetCommandsEnabled(false);
        attackCommands.SetBattleCommandsEnabled(false);
        abilityService?.SetCommandsEnabled(false);
        abilityCommands?.SetBattleCommandsEnabled(false);
        turnController.StopBattleLifecycle();
        battleHud.SetBattleCommandsAvailable(false);
    }

    private void BindListeners()
    {
        foreach (SquadBattleController controller in squadBootstrap.SpawnedControllers)
        {
            if (controller?.Runtime == null || defeatHandlers.ContainsKey(controller.SquadId))
                continue;
            Action handler = HandleSquadDefeated;
            defeatHandlers.Add(controller.SquadId, handler);
            controller.Runtime.OnSquadDefeated += handler;
        }
        attackService.OnAttackResolved += HandleAttackResolved;
        resultPanel.ContinueRequested += HandleContinueRequested;
        resultPanel.RetrySaveRequested += HandleRetrySaveRequested;
    }

    private void UnbindListeners()
    {
        if (squadBootstrap != null)
        {
            foreach (SquadBattleController controller in squadBootstrap.SpawnedControllers)
            {
                if (controller?.Runtime != null &&
                    defeatHandlers.TryGetValue(controller.SquadId, out Action handler))
                {
                    controller.Runtime.OnSquadDefeated -= handler;
                }
            }
        }
        defeatHandlers.Clear();
        if (attackService != null)
            attackService.OnAttackResolved -= HandleAttackResolved;
        if (resultPanel != null)
        {
            resultPanel.ContinueRequested -= HandleContinueRequested;
            resultPanel.RetrySaveRequested -= HandleRetrySaveRequested;
        }
    }

    private void HandleSquadDefeated()
    {
        evaluationPending = true;
        if (!attackService.IsExecuting && committedResolutionDepth == 0)
            EvaluateCompletion();
    }

    private void HandleAttackResolved(BattleAttackResult _)
    {
        if (evaluationPending)
            EvaluateCompletion();
    }

    private void HandleContinueRequested() => ContinueToOverworld();
    private void HandleRetrySaveRequested() => RetryAutosave();

    private bool Fail(string reason)
    {
        FailureReason = reason ?? "Unknown battle completion failure.";
        State = BattleCompletionState.Failed;
        resultPanel?.ShowFailure(FailureReason);
        OnBattleCompletionFailed?.Invoke(FailureReason);
        Debug.LogError($"BattleCompletionController: {FailureReason}", this);
        return false;
    }

    private bool FailTransition(string reason)
    {
        FailureReason = reason;
        resultPanel?.ShowSaveState(SaveOperationResult.Fail(reason));
        Debug.LogError($"BattleCompletionController: {reason}", this);
        return false;
    }

    private static int CreateStablePostBattleSeed(
        int seed,
        string sourceBattleId,
        string commanderId)
    {
        unchecked
        {
            uint hash = 2166136261u ^ (uint)seed;
            string value = (sourceBattleId ?? string.Empty) + "|" +
                           (commanderId ?? string.Empty);
            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= 16777619u;
            }
            return (int)hash;
        }
    }

    private void OnDestroy() => UnbindListeners();
}
