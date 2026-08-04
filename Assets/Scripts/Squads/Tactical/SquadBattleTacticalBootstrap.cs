using System.Collections;
using UnityEngine;

public sealed class SquadBattleTacticalBootstrap : MonoBehaviour
{
    [SerializeField] private SquadBattleBootstrap squadBootstrap;
    [SerializeField] private GridOccupancyService occupancy;
    [SerializeField] private BattleSquadSelectionController selection;
    [SerializeField] private BattleTurnController turns;
    [SerializeField] private SquadMovementService movement;
    [SerializeField] private BattleCommandModeController commandMode;
    [SerializeField] private BattleAttackService attackService;
    [SerializeField] private MovementCommandController commands;
    [SerializeField] private AttackCommandController attackCommands;
    [SerializeField] private BattleCompletionController completion;

    public bool HasInitialized { get; private set; }
    public string FailureReason { get; private set; }
    public int SuccessfulInitializationCount { get; private set; }

    public void Configure(
        SquadBattleBootstrap bootstrap,
        GridOccupancyService occupancyService,
        BattleSquadSelectionController selectionController,
        BattleTurnController turnController,
        SquadMovementService movementService,
        BattleCommandModeController modeController,
        BattleAttackService configuredAttackService,
        MovementCommandController commandController,
        AttackCommandController configuredAttackCommands,
        BattleCompletionController configuredCompletion = null)
    {
        squadBootstrap = bootstrap;
        occupancy = occupancyService;
        selection = selectionController;
        turns = turnController;
        movement = movementService;
        commandMode = modeController;
        attackService = configuredAttackService;
        commands = commandController;
        attackCommands = configuredAttackCommands;
        completion = configuredCompletion;
    }

    private IEnumerator Start()
    {
        if (squadBootstrap == null)
        {
            Fail("SquadBattleBootstrap reference is missing.");
            yield break;
        }

        while (squadBootstrap.State == SquadBootstrapState.NotInitialized ||
               squadBootstrap.State == SquadBootstrapState.Initializing)
        {
            yield return null;
        }

        if (squadBootstrap.State != SquadBootstrapState.Initialized)
        {
            Fail($"Squad bootstrap failed: {squadBootstrap.FailureReason}");
            yield break;
        }

        if (HasInitialized)
            yield break;
        if (occupancy == null || !occupancy.Initialize(squadBootstrap.SpawnedControllers))
        {
            Fail("GridOccupancyService could not register the spawned squad cells.");
            yield break;
        }
        if (selection == null || !selection.Initialize())
        {
            Fail("BattleSquadSelectionController could not initialize.");
            yield break;
        }
        if (movement == null || !movement.Initialize())
        {
            Fail("SquadMovementService could not initialize.");
            yield break;
        }
        if (turns == null || !turns.StartBattle())
        {
            Fail("BattleTurnController could not start the initiative loop.");
            yield break;
        }
        if (commandMode == null)
        {
            Fail("BattleCommandModeController is missing.");
            yield break;
        }
        commandMode.ResetForBattle();
        if (attackService == null || !attackService.Initialize())
        {
            Fail("BattleAttackService could not initialize.");
            yield break;
        }
        if (commands == null || !commands.Initialize())
        {
            Fail("MovementCommandController could not bind production commands.");
            yield break;
        }
        if (attackCommands == null || !attackCommands.Initialize())
        {
            Fail("AttackCommandController could not bind production commands.");
            yield break;
        }
        if (completion != null && !completion.Initialize())
        {
            Fail("BattleCompletionController could not initialize.");
            yield break;
        }

        HasInitialized = true;
        SuccessfulInitializationCount++;
        Debug.Log(
            "SquadBattleTacticalBootstrap: selection, turns, occupancy, movement, attack, and battle lifecycle initialized once.",
            this);
    }

    private void Fail(string reason)
    {
        FailureReason = reason;
        Debug.LogError($"SquadBattleTacticalBootstrap: {reason}", this);
    }
}
