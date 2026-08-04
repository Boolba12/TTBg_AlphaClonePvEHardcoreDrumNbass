#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Xml;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class SquadBattleSceneSmokeRunner
{
    private const string ScenePath = "Assets/Scenes/Raw_Alpha_BattleMode.unity";
    private const string ResultPath = "Logs/SquadBattleSceneSmokeResults.xml";
    private const string RunRequestPath =
        "Assets/Scripts/Squads/Editor/SquadBattleSceneSmoke.run-request";
    private const string StartedKey = "SquadBattleSceneSmoke.Started";
    private const string FinishedKey = "SquadBattleSceneSmoke.Finished";
    private const string PassedKey = "SquadBattleSceneSmoke.Passed";
    private const string StartTimeKey = "SquadBattleSceneSmoke.StartTime";
    private const string PhaseKey = "SquadBattleSceneSmoke.Phase";
    private const string DestinationXKey = "SquadBattleSceneSmoke.DestinationX";
    private const string DestinationYKey = "SquadBattleSceneSmoke.DestinationY";
    private const string InitialActionPointsKey = "SquadBattleSceneSmoke.InitialAP";
    private const string MovementCostKey = "SquadBattleSceneSmoke.MovementCost";
    private const string ExpectedMovementCountKey =
        "SquadBattleSceneSmoke.ExpectedMovementCount";
    private const int MaximumApproachMoves = 12;

    [InitializeOnLoadMethod]
    private static void ContinueRequestedRun()
    {
        if (!File.Exists(RunRequestPath))
            return;

        EditorApplication.delayCall += RegisterUpdate;
    }

    [MenuItem("Tools/Squads/Run Raw Alpha Play Mode Smoke %#&s")]
    public static void RunFromMenu()
    {
        if (!File.Exists(RunRequestPath))
            File.WriteAllText(RunRequestPath, "Run one Raw Alpha squad scene smoke test.");
        SessionState.EraseBool(StartedKey);
        SessionState.EraseBool(FinishedKey);
        SessionState.EraseBool(PassedKey);
        SessionState.SetInt(PhaseKey, 0);
        RegisterUpdate();
    }

    private static void RegisterUpdate()
    {
        EditorApplication.update -= UpdateRun;
        EditorApplication.update += UpdateRun;
    }

    private static void UpdateRun()
    {
        if (SessionState.GetBool(FinishedKey, false))
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                CleanupRequest();
            return;
        }

        if (!SessionState.GetBool(StartedKey, false))
        {
            if (EditorApplication.isCompiling ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.isDirty && activeScene.path != ScenePath)
            {
                Finish(
                    false,
                    "Another modified scene is active; smoke test refused to discard it.");
                return;
            }

            if (activeScene.path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            SessionState.SetBool(StartedKey, true);
            SessionState.SetString(
                StartTimeKey,
                EditorApplication.timeSinceStartup.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
            EditorApplication.isPlaying = true;
            return;
        }

        if (!EditorApplication.isPlaying)
            return;

        BattleMapBootstrap mapBootstrap =
            UnityEngine.Object.FindAnyObjectByType<BattleMapBootstrap>();
        BattleHUDController battleHud =
            UnityEngine.Object.FindAnyObjectByType<BattleHUDController>();
        SquadBattleTacticalBootstrap tacticalBootstrap =
            UnityEngine.Object.FindAnyObjectByType<SquadBattleTacticalBootstrap>();
        if (mapBootstrap != null &&
            mapBootstrap.HasBootstrapped &&
            battleHud != null &&
            battleHud.HasBoundPlayer &&
            tacticalBootstrap != null &&
            tacticalBootstrap.HasInitialized)
        {
            try
            {
                int phase = SessionState.GetInt(PhaseKey, 0);
                if (phase == 0)
                    BeginProductionMovement(mapBootstrap, battleHud, tacticalBootstrap);
                else if (phase == 1)
                    CompleteProductionMovementAndTurn(battleHud, tacticalBootstrap);
                else if (phase == 2)
                    CompleteAIAutoSkip(tacticalBootstrap);
            }
            catch (Exception exception)
            {
                Finish(false, exception.Message);
            }
            return;
        }

        double startTime = double.Parse(
            SessionState.GetString(StartTimeKey, "0"),
            System.Globalization.CultureInfo.InvariantCulture);
        if (EditorApplication.timeSinceStartup - startTime > 25)
        {
            Finish(
                false,
                $"Timed out waiting for map generation and squad bootstrap. " +
                $"MapBootstrapFound={mapBootstrap != null}, " +
                $"BattleHudFound={battleHud != null}, " +
                $"HudBound={battleHud != null && battleHud.HasBoundPlayer}, " +
                $"TacticalInitialized={tacticalBootstrap != null && tacticalBootstrap.HasInitialized}, " +
                $"SetupConfirmed={BattleSetupContext.IsConfirmed}, " +
                $"ActiveScene={SceneManager.GetActiveScene().name}.");
        }
    }

    private static void ValidateScene(
        BattleMapBootstrap mapBootstrap,
        BattleHUDController battleHud)
    {
        Require(
            mapBootstrap.CombatMode == BattleCombatMode.Squads,
            "BattleMapBootstrap is not in squad mode.");
        Require(
            mapBootstrap.UsedDevelopmentAutoConfirm,
            "The scene did not use its configured production confirmation pathway.");

        MapGenerator mapGenerator = mapBootstrap.mapGenerator;
        MapRenderer mapRenderer = mapBootstrap.mapRenderer;
        SquadBattleBootstrap squadBootstrap =
            UnityEngine.Object.FindAnyObjectByType<SquadBattleBootstrap>();
        SquadSaveParticipant squadRepository =
            UnityEngine.Object.FindAnyObjectByType<SquadSaveParticipant>();
        Require(
            mapGenerator != null && mapGenerator.HasGeneratedData,
            "Generated map data is missing.");
        Require(
            mapRenderer != null && mapRenderer.HasMap,
            "Rendered battle map is missing.");
        Require(
            squadBootstrap != null &&
            squadBootstrap.HasBootstrapped,
            "Squad bootstrap did not reach Initialized.");
        Require(
            squadBootstrap.SpawnedControllers.Count == 2,
            "Expected exactly two spawned squad controllers.");
        Require(
            squadBootstrap.InitiativeOrder.Entries.Count == 2,
            "Expected exactly two initiative entries.");
        Require(
            squadRepository != null && squadRepository.ActiveRuntimeCount == 2,
            "Expected exactly two registered squad runtimes.");

        SquadBattleController player = squadBootstrap.SpawnedControllers.Single(
            controller => controller.Side == BattleSide.Player);
        SquadBattleController enemy = squadBootstrap.SpawnedControllers.Single(
            controller => controller.Side == BattleSide.Enemy);
        Require(
            player != null && enemy != null &&
            player.Runtime != null && enemy.Runtime != null,
            "One runtime is missing.");
        Require(
            player.SquadId != enemy.SquadId,
            "Player and enemy squad IDs must be distinct.");
        Require(
            player.Side == BattleSide.Player &&
            player.ControlType == SquadControlType.Human,
            "Player squad does not have the Player/Human battle contract.");
        Require(
            enemy.Side == BattleSide.Enemy &&
            enemy.ControlType == SquadControlType.AI,
            "Enemy squad does not have the Enemy/AI battle contract.");
        Require(
            player.Runtime.Data.Commander.race == CommanderRace.Human,
            "Development player commander is not explicitly configured as Human.");
        Require(
            enemy.Runtime.Data.Commander.race == CommanderRace.Elf,
            "Development enemy commander is not explicitly configured as Elf.");
        Require(
            player.Side != enemy.Side,
            "Spawned controllers must belong to different battle sides.");
        Require(
            player.GridAnchor.CurrentCell != enemy.GridAnchor.CurrentCell,
            "Player and enemy cells must be distinct.");

        SquadBattleTacticalBootstrap[] tacticalBootstraps =
            UnityEngine.Object.FindObjectsByType<SquadBattleTacticalBootstrap>(
                FindObjectsInactive.Include);
        Require(tacticalBootstraps.Length == 1 && tacticalBootstraps[0].HasInitialized,
            "Expected one initialized tactical bootstrap.");
        Require(tacticalBootstraps[0].SuccessfulInitializationCount == 1,
            "Tactical services must initialize exactly once.");
        Require(UnityEngine.Object.FindObjectsByType<GridOccupancyService>(
                    FindObjectsInactive.Include).Length == 1 &&
                UnityEngine.Object.FindObjectsByType<BattleSquadSelectionController>(
                    FindObjectsInactive.Include).Length == 1 &&
                UnityEngine.Object.FindObjectsByType<BattleTurnController>(
                    FindObjectsInactive.Include).Length == 1 &&
                UnityEngine.Object.FindObjectsByType<SquadMovementService>(
                    FindObjectsInactive.Include).Length == 1 &&
                UnityEngine.Object.FindObjectsByType<MovementCommandController>(
                    FindObjectsInactive.Include).Length == 1 &&
                UnityEngine.Object.FindObjectsByType<BattleCommandModeController>(
                    FindObjectsInactive.Include).Length == 1 &&
                UnityEngine.Object.FindObjectsByType<BattleAttackService>(
                    FindObjectsInactive.Include).Length == 1 &&
                UnityEngine.Object.FindObjectsByType<AttackCommandController>(
                    FindObjectsInactive.Include).Length == 1,
            "Scene must contain exactly one owner for occupancy, selection, turns, " +
            "movement, command mode, and attack execution.");

        ValidateBattleHud(battleHud, squadBootstrap, player);

        foreach (SquadBattleController controller in
                 squadBootstrap.SpawnedControllers)
        {
            Require(
                controller.FormationView.CommanderModelCount == 1,
                $"{controller.SquadId} does not have one commander model.");
            Require(
                controller.FormationView.ActiveWarriorModelCount >= 1 &&
                controller.FormationView.ActiveWarriorModelCount <= 8,
                $"{controller.SquadId} has an invalid active warrior-model count.");
            Require(
                controller.GetComponent<UnitStats>() == null &&
                controller.GetComponent<PlayerController>() == null &&
                controller.GetComponent<EnemyController>() == null,
                $"{controller.SquadId} contains a forbidden legacy combat component.");
        }

        PlayerController[] legacyPlayers =
            UnityEngine.Object.FindObjectsByType<PlayerController>(
                FindObjectsInactive.Include);
        EnemyController[] legacyEnemies =
            UnityEngine.Object.FindObjectsByType<EnemyController>(
                FindObjectsInactive.Include);
        Require(
            legacyPlayers.All(controller => !controller.gameObject.activeInHierarchy),
            "An active legacy player controller duplicates the squad participant.");
        Require(
            legacyEnemies.All(controller => !controller.gameObject.activeInHierarchy),
            "An active legacy enemy controller duplicates the squad participant.");
    }

    private static void ValidateBattleHud(
        BattleHUDController battleHud,
        SquadBattleBootstrap squadBootstrap,
        SquadBattleController player)
    {
        BattleHUDController[] hudControllers =
            UnityEngine.Object.FindObjectsByType<BattleHUDController>(
                FindObjectsInactive.Include);
        Require(hudControllers.Length == 1, "Expected exactly one BattleHUDController.");
        Require(
            battleHud.gameObject.name == "BattleUIRoot",
            "The production HUD is not rooted at BattleUIRoot.");
        Require(
            battleHud.BoundPlayerController == player &&
            battleHud.BoundPlayerController.Side == BattleSide.Player,
            "Battle HUD is not bound through the explicit Player-side contract.");
        Require(
            battleHud.SuccessfulBindingCount == 1,
            "Battle HUD must complete production binding exactly once.");

        CanvasScaler scaler = battleHud.GetComponent<CanvasScaler>();
        Require(
            scaler != null &&
            scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize &&
            scaler.referenceResolution == new Vector2(1920, 1080),
            "Battle HUD CanvasScaler is not configured for responsive 16:9 scaling.");

        EventSystem[] eventSystems =
            UnityEngine.Object.FindObjectsByType<EventSystem>(
                FindObjectsInactive.Include);
        Require(eventSystems.Length == 1, "The scene must contain exactly one EventSystem.");

        BattleSquadStatusView statusView =
            battleHud.GetComponentInChildren<BattleSquadStatusView>(true);
        InitiativeQueueView initiativeView =
            battleHud.GetComponentInChildren<InitiativeQueueView>(true);
        BattleSquadStatusPresenter statusPresenter =
            battleHud.GetComponentInChildren<BattleSquadStatusPresenter>(true);
        Require(statusView != null && statusView.HasData, "Squad status view has no runtime data.");
        Require(
            statusView.CurrentModel.SquadId == player.SquadId,
            "Squad status view is showing the wrong squad.");
        Require(
            statusView.DisplayedPortrait != null,
            "Squad status view did not display the configured portrait or fallback.");
        CommanderPortraitDatabase portraitDatabase = statusPresenter?.PortraitDatabase;
        Require(portraitDatabase != null, "Battle HUD portrait database reference is missing.");
        Require(
            portraitDatabase.TryGetById(
                player.Runtime.Data.CommanderPortraitId,
                out CommanderPortraitEntry playerPortrait) &&
            playerPortrait != null &&
            playerPortrait.Sprite != null,
            "Development player portrait ID does not resolve to a real portrait.");
        Require(
            statusView.DisplayedPortrait == playerPortrait.Sprite &&
            statusView.DisplayedPortrait != portraitDatabase.FallbackPortrait,
            "Squad status view used fallback instead of the configured real portrait.");
        Require(
            initiativeView != null &&
            initiativeView.DisplayedCount == squadBootstrap.InitiativeOrder.Entries.Count &&
            initiativeView.DisplayedCount == 2,
            "Initiative queue does not display the two production entries.");
        Require(
            initiativeView.SpawnedEntries.Count >= 2,
            "Initiative queue did not create two reusable entry views.");
        CommanderPortraitService portraitService = new CommanderPortraitService(portraitDatabase, 3);
        for (int i = 0; i < squadBootstrap.InitiativeOrder.Entries.Count; i++)
        {
            SquadBattleController controller = squadBootstrap.InitiativeOrder.Entries[i];
            InitiativeEntryView entryView = initiativeView.SpawnedEntries[i];
            Sprite expectedPortrait = portraitService.GetDisplaySprite(
                controller.Runtime.Data.CommanderPortraitId);
            Require(
                entryView.DisplayedSquadId == controller.SquadId &&
                entryView.DisplayedPortrait == expectedPortrait &&
                expectedPortrait != portraitDatabase.FallbackPortrait,
                $"Initiative entry {i} is not showing its stable configured portrait.");
        }

        Transform hudLayer = battleHud.transform.Find("HUDLayer");
        float compactArea = AnchorArea(hudLayer.Find("TopBar") as RectTransform) +
                            AnchorArea(hudLayer.Find("TopRight_MinimapContainer") as RectTransform) +
                            AnchorArea(hudLayer.Find("SelectedSquadPanel") as RectTransform) +
                            AnchorArea(hudLayer.Find("BottomActionBar") as RectTransform);
        Require(
            compactArea >= 0.38f && compactArea <= 0.435f,
            $"Battle HUD compact anchor area {compactArea:0.000} is outside the approved range.");
        Require(
            battleHud.GetComponent<GraphicRaycaster>() != null,
            "Battle HUD is missing its UI raycaster.");
        Transform actionBar = hudLayer.Find("BottomActionBar");
        Require(
            actionBar.GetComponentsInChildren<PanelFrameView>(true).Length == 1,
            "Bottom action bar is not a single outer-frame body.");
        BattleActionControlView[] actions =
            actionBar.GetComponentsInChildren<BattleActionControlView>(true);
        Require(actions.Length == 7,
            "Bottom action bar does not expose seven staged action controls.");
        Require(actions.Count(action => action.gameObject.name == "Move") == 1 &&
                actions.Count(action => action.gameObject.name == "Attack") == 1 &&
                actions.Count(action => action.gameObject.name == "EndTurn") == 1,
            "Move, Attack, and EndTurn must each have one explicit HUD control.");
        BattleActionControlView attackAction =
            actions.Single(action => action.gameObject.name == "Attack");
        Require(attackAction.Button != null && attackAction.DisplayedIcon != null,
            "Attack must expose its configured HUD button and development weapon preview.");
        foreach (BattleActionControlView action in actions)
        {
            Require(action.Button != null && action.Button.targetGraphic != null &&
                    action.Button.targetGraphic.raycastTarget && action.DisplayedIcon != null,
                "An action control is missing its hit area or icon placeholder.");
        }

        Require(
            battleHud.TryBindFromProductionState() &&
            battleHud.SuccessfulBindingCount == 1,
            "Repeated HUD binding created a duplicate production subscription.");
    }

    private static void BeginProductionMovement(
        BattleMapBootstrap mapBootstrap,
        BattleHUDController battleHud,
        SquadBattleTacticalBootstrap tacticalBootstrap)
    {
        SquadBattleBootstrap squadBootstrap =
            UnityEngine.Object.FindAnyObjectByType<SquadBattleBootstrap>();
        BattleTurnController turns =
            UnityEngine.Object.FindAnyObjectByType<BattleTurnController>();
        BattleSquadSelectionController selection =
            UnityEngine.Object.FindAnyObjectByType<BattleSquadSelectionController>();
        GridOccupancyService occupancy =
            UnityEngine.Object.FindAnyObjectByType<GridOccupancyService>();
        SquadMovementService movement =
            UnityEngine.Object.FindAnyObjectByType<SquadMovementService>();
        MovementCommandController commands =
            UnityEngine.Object.FindAnyObjectByType<MovementCommandController>();
        AttackCommandController attackCommands =
            UnityEngine.Object.FindAnyObjectByType<AttackCommandController>();
        SquadBattleController player = squadBootstrap.SpawnedControllers.Single(
            controller => controller.Side == BattleSide.Player);
        SquadBattleController enemy = squadBootstrap.SpawnedControllers.Single(
            controller => controller.Side == BattleSide.Enemy);

        if (commands.MovementCommandCount == 0)
            ValidateScene(mapBootstrap, battleHud);

        // If the deterministic first entry is AI, wait for the configured development
        // auto-skip. The smoke still uses the same BattleTurnController flow as Play.
        if (turns.ActiveSquad != player)
            return;

        Require(selection.TrySelectTarget(player.SelectionTarget),
            "Production selection target did not select the Player squad.");
        Require(selection.SelectedSquad == player &&
                player.SelectionTarget.SelectionView.IsSelected,
            "Player selection state or selection ring was not activated.");
        Require(occupancy.OccupiedCellCount == 2 && occupancy.ReservationCount == 0,
            "Initial occupancy must contain exactly the two spawned squads.");

        int currentDistance = BattleTargetingService.GetGridDistance(
            player.GridAnchor.CurrentCell,
            enemy.GridAnchor.CurrentCell,
            movement.AllowDiagonalMovement);
        if (currentDistance == 1)
        {
            ExecuteProductionAttack(
                battleHud,
                tacticalBootstrap,
                player,
                enemy,
                movement,
                commands,
                attackCommands);
            return;
        }

        Require(commands.MovementCommandCount < MaximumApproachMoves,
            $"Player did not reach melee range within {MaximumApproachMoves} production moves.");
        SquadMovementPlan chosenPlan = null;
        int chosenDistance = int.MaxValue;
        for (int x = 0; x < mapBootstrap.mapGenerator.width; x++)
        {
            for (int y = 0; y < mapBootstrap.mapGenerator.height; y++)
            {
                Vector2Int candidate = new Vector2Int(x, y);
                if (!movement.TryBuildPlan(player, candidate, out SquadMovementPlan plan) ||
                    plan.ActionPointCost <= 0)
                {
                    continue;
                }
                int distance = BattleTargetingService.GetGridDistance(
                    candidate,
                    enemy.GridAnchor.CurrentCell,
                    movement.AllowDiagonalMovement);
                if (distance > chosenDistance ||
                    (distance == chosenDistance && chosenPlan != null &&
                     plan.ActionPointCost <= chosenPlan.ActionPointCost))
                {
                    continue;
                }
                chosenPlan = plan;
                chosenDistance = distance;
            }
        }
        Require(chosenPlan != null && chosenDistance < currentDistance,
            "No AP-valid production movement plan advanced toward melee range.");

        BattleActionControlView moveAction = battleHud
            .GetComponentsInChildren<BattleActionControlView>(true)
            .Single(action => action.gameObject.name == "Move");
        SquadPathPreviewView preview =
            UnityEngine.Object.FindAnyObjectByType<SquadPathPreviewView>();
        Require(moveAction.Button.interactable, "Move HUD control is not interactable.");
        int initialActionPoints = player.Runtime.State.currentActionPoints;
        moveAction.Button.onClick.Invoke();
        Require(commands.IsMovementTargeting,
            "Move HUD click did not enter production movement targeting.");
        if (commands.MovementCommandCount == 0)
        {
            commands.CancelMovementTargeting();
            Require(!commands.IsMovementTargeting && preview != null && !preview.IsVisible,
                "Cancelling production targeting did not clear its preview state.");
            moveAction.Button.onClick.Invoke();
            Require(commands.IsMovementTargeting,
                "Move HUD control did not re-enter targeting after cancellation.");
        }
        int expectedMovementCount = commands.MovementCommandCount + 1;
        Require(commands.TrySubmitTargetCell(chosenPlan.Destination),
            "Production movement command adapter rejected the chosen cell.");
        Require(!commands.TrySubmitTargetCell(chosenPlan.Destination) &&
                commands.MovementCommandCount == expectedMovementCount,
            "A repeated movement click started a duplicate command or coroutine.");

        SessionState.SetInt(DestinationXKey, chosenPlan.Destination.x);
        SessionState.SetInt(DestinationYKey, chosenPlan.Destination.y);
        SessionState.SetInt(InitialActionPointsKey, initialActionPoints);
        SessionState.SetInt(MovementCostKey, chosenPlan.ActionPointCost);
        SessionState.SetInt(ExpectedMovementCountKey, expectedMovementCount);
        SessionState.SetInt(PhaseKey, 1);
    }

    private static void CompleteProductionMovementAndTurn(
        BattleHUDController battleHud,
        SquadBattleTacticalBootstrap tacticalBootstrap)
    {
        SquadMovementService movement =
            UnityEngine.Object.FindAnyObjectByType<SquadMovementService>();
        if (movement.IsMoving)
            return;

        SquadBattleBootstrap squadBootstrap =
            UnityEngine.Object.FindAnyObjectByType<SquadBattleBootstrap>();
        BattleTurnController turns =
            UnityEngine.Object.FindAnyObjectByType<BattleTurnController>();
        BattleSquadSelectionController selection =
            UnityEngine.Object.FindAnyObjectByType<BattleSquadSelectionController>();
        GridOccupancyService occupancy =
            UnityEngine.Object.FindAnyObjectByType<GridOccupancyService>();
        MovementCommandController commands =
            UnityEngine.Object.FindAnyObjectByType<MovementCommandController>();
        SquadBattleController player = squadBootstrap.SpawnedControllers.Single(
            controller => controller.Side == BattleSide.Player);
        SquadBattleController enemy = squadBootstrap.SpawnedControllers.Single(
            controller => controller.Side == BattleSide.Enemy);
        Vector2Int destination = new Vector2Int(
            SessionState.GetInt(DestinationXKey, int.MinValue),
            SessionState.GetInt(DestinationYKey, int.MinValue));
        int initialActionPoints = SessionState.GetInt(InitialActionPointsKey, -1);
        int cost = SessionState.GetInt(MovementCostKey, -1);
        int expectedMovementCount = SessionState.GetInt(ExpectedMovementCountKey, -1);

        Require(commands.MovementCommandCount == expectedMovementCount,
            "Movement command did not complete exactly once.");
        Require(player.GridAnchor.CurrentCell == destination &&
                player.Runtime.State.logicalCell.x == destination.x &&
                player.Runtime.State.logicalCell.y == destination.y,
            "Visual, logical, and runtime cells did not commit together.");
        Require(player.Runtime.State.currentActionPoints == initialActionPoints - cost,
            "Movement AP was not charged at one point per path cell.");
        Require(Vector3.Distance(
                    player.transform.position,
                    player.GridAnchor.GetWorldPosition(destination)) < 0.01f,
            "Player root did not finish at the committed destination.");
        Require(occupancy.TryGetOccupant(destination, out SquadBattleController occupant) &&
                occupant == player && occupancy.OccupiedCellCount == 2 &&
                occupancy.ReservationCount == 0,
            "Occupancy did not atomically commit and clear its reservation.");

        InitiativeQueueView initiative =
            battleHud.GetComponentInChildren<InitiativeQueueView>(true);
        InitiativeEntryView playerEntry = initiative.SpawnedEntries.Single(
            entry => entry.DisplayedSquadId == player.SquadId);
        Require(playerEntry.DisplaysSelectedState && playerEntry.DisplaysActiveState,
            "Player initiative entry did not distinguish selected and active state before End Turn.");

        BattleActionControlView endTurnAction = battleHud
            .GetComponentsInChildren<BattleActionControlView>(true)
            .Single(action => action.gameObject.name == "EndTurn");
        BattleActionControlView moveAction = battleHud
            .GetComponentsInChildren<BattleActionControlView>(true)
            .Single(action => action.gameObject.name == "Move");
        SquadPathPreviewView preview =
            UnityEngine.Object.FindAnyObjectByType<SquadPathPreviewView>();
        Require(endTurnAction.Button.interactable, "EndTurn HUD control is not interactable.");
        if (expectedMovementCount == 1 && moveAction.Button.interactable)
        {
            moveAction.Button.onClick.Invoke();
            Require(commands.IsMovementTargeting,
                "Move targeting could not be re-entered before End Turn.");
        }
        endTurnAction.Button.onClick.Invoke();
        Require(commands.EndTurnCommandCount == expectedMovementCount &&
                turns.ActiveSquad == enemy,
            "EndTurn HUD click did not advance to the next initiative entry exactly once.");
        Require(!commands.IsMovementTargeting && preview != null && !preview.IsVisible,
            "End Turn did not clear movement targeting and its preview.");
        InitiativeEntryView enemyEntry = initiative.SpawnedEntries.Single(
            entry => entry.DisplayedSquadId == enemy.SquadId);
        Require(playerEntry.DisplaysSelectedState && !playerEntry.DisplaysActiveState &&
                enemyEntry.DisplaysActiveState && !enemyEntry.DisplaysSelectedState &&
                selection.SelectedSquad == player,
            "Selected and active initiative states were conflated after End Turn.");
        Require(tacticalBootstrap.SuccessfulInitializationCount == 1,
            "Tactical bootstrap ran more than once during production flow.");

        SessionState.SetInt(PhaseKey, 2);
    }

    private static void CompleteAIAutoSkip(
        SquadBattleTacticalBootstrap tacticalBootstrap)
    {
        BattleTurnController turns =
            UnityEngine.Object.FindAnyObjectByType<BattleTurnController>();
        SquadBattleBootstrap squadBootstrap =
            UnityEngine.Object.FindAnyObjectByType<SquadBattleBootstrap>();
        MovementCommandController commands =
            UnityEngine.Object.FindAnyObjectByType<MovementCommandController>();
        SquadBattleController player = squadBootstrap.SpawnedControllers.Single(
            controller => controller.Side == BattleSide.Player);
        int expectedMovementCount = SessionState.GetInt(ExpectedMovementCountKey, -1);
        if (turns.ActiveSquad != player || turns.CurrentRound < expectedMovementCount + 1)
            return;

        Require(commands.MovementCommandCount == expectedMovementCount &&
                commands.EndTurnCommandCount == expectedMovementCount,
            "AI placeholder or turn cycling duplicated a Human command.");
        Require(tacticalBootstrap.SuccessfulInitializationCount == 1,
            "Tactical bootstrap ran more than once during the turn cycle.");
        SessionState.SetInt(PhaseKey, 0);
    }

    private static void ExecuteProductionAttack(
        BattleHUDController battleHud,
        SquadBattleTacticalBootstrap tacticalBootstrap,
        SquadBattleController player,
        SquadBattleController enemy,
        SquadMovementService movement,
        MovementCommandController movementCommands,
        AttackCommandController attackCommands)
    {
        BattleAttackService attackService =
            UnityEngine.Object.FindAnyObjectByType<BattleAttackService>();
        BattleCommandModeController commandMode =
            UnityEngine.Object.FindAnyObjectByType<BattleCommandModeController>();
        GridOccupancyService occupancy =
            UnityEngine.Object.FindAnyObjectByType<GridOccupancyService>();
        SquadBattleBootstrap squadBootstrap =
            UnityEngine.Object.FindAnyObjectByType<SquadBattleBootstrap>();
        Require(attackService != null && attackService.IsInitialized,
            "Production attack service is not initialized.");
        Require(attackCommands != null && attackCommands.IsInitialized,
            "Production attack command controller is not initialized.");
        Require(player.AttackTarget != null && enemy.AttackTarget != null,
            "A spawned squad is missing its root attack target adapter.");

        // The smoke injects only the battle-scoped roll sequence; UI, validation,
        // AP commit, resolver, formation, and HUD all remain the production flow.
        attackService.SetRandomSourceForTests(new SmokeBattleRandomSource(0f, 0.99f));
        int initialActionPoints = player.Runtime.State.currentActionPoints;
        int initialEnemyHealth = enemy.Runtime.State.CurrentSquadHP;
        int initialLivingWarriors = enemy.Runtime.State.warriors.Count(
            warrior => warrior != null && !warrior.defeated);

        BattleActionControlView[] actions = battleHud
            .GetComponentsInChildren<BattleActionControlView>(true);
        BattleActionControlView moveAction = actions.Single(
            action => action.gameObject.name == "Move");
        BattleActionControlView attackAction = actions.Single(
            action => action.gameObject.name == "Attack");
        Require(moveAction.Button.interactable && attackAction.Button.interactable,
            "Move and Attack must both be available to the active adjacent Human squad.");

        moveAction.Button.onClick.Invoke();
        Require(movementCommands.IsMovementTargeting,
            "Production Move button did not enter Move mode before the exclusivity check.");
        attackAction.Button.onClick.Invoke();
        Require(!movementCommands.IsMovementTargeting &&
                attackCommands.IsAttackTargeting &&
                commandMode.ActiveMode == BattleCommandMode.Attack &&
                attackAction.IsSelectedAction,
            "Attack did not atomically replace Move as the single command mode.");
        Require(enemy.AttackTarget.TargetingView.State ==
                SquadAttackTargetVisualState.Available,
            "The adjacent enemy was not highlighted as an available attack target.");
        Require(player.AttackTarget.TargetingView.State ==
                SquadAttackTargetVisualState.None,
            "The attacker was incorrectly highlighted as its own target.");

        BattleAttackPreview preview = attackCommands.TryHoverTarget(enemy.AttackTarget);
        Require(preview.IsValid && preview.ActionPointCost ==
                attackService.BasicAttack.ActionPointCost,
            "Production target hover did not return a valid read-only attack preview.");
        Require(preview.TargetCurrentHealth == initialEnemyHealth &&
                preview.TargetLivingWarriors == initialLivingWarriors &&
                preview.HitChance > 0f && preview.PredictedDamage > 0,
            "Attack preview is missing target HP, composition, hit chance, or damage.");
        Require(battleHud.AbilityDetails != null &&
                battleHud.AbilityDetails.HasDetails &&
                battleHud.AbilityDetails.CurrentAttackPreview.IsValid &&
                battleHud.AbilityDetails.DisplayedPortrait != null,
            "Ability Info did not display the target portrait and attack preview.");
        Require(enemy.AttackTarget.TargetingView.State ==
                SquadAttackTargetVisualState.HoveredValid,
            "Enemy hover did not switch to the valid hover presentation.");

        enemy.AttackTarget.RequestConfirm();
        BattleAttackResult result = attackCommands.LastResult;
        Require(result != null && result.WasExecuted && result.Hit && !result.Critical,
            "Root target confirmation did not produce the injected deterministic hit.");
        Require(attackCommands.AttackCommandCount == 1,
            "One production target confirmation must execute exactly one attack.");
        Require(player.Runtime.State.currentActionPoints ==
                initialActionPoints - attackService.BasicAttack.ActionPointCost,
            "Attack AP was not committed exactly once through the runtime API.");
        Require(enemy.Runtime.State.CurrentSquadHP ==
                initialEnemyHealth - result.AppliedDamage,
            "Existing SquadDamageResolver did not apply the reported final damage.");
        Require(result.DefeatedWarriorIds.Count == 1 &&
                enemy.Runtime.State.warriors.Count(
                    warrior => warrior != null && !warrior.defeated) ==
                initialLivingWarriors - 1,
            "Single-target damage did not defeat exactly the first living Warrior.");
        Require(enemy.FormationView.ActiveWarriorModelCount ==
                initialLivingWarriors - 1,
            "Formation did not remove the defeated Warrior visual.");
        Require(squadBootstrap.InitiativeOrder.Entries.Count == 2 &&
                occupancy.OccupiedCellCount == 2,
            "A Warrior casualty incorrectly removed the squad from initiative or occupancy.");

        BattleSquadStatusView status =
            battleHud.GetComponentInChildren<BattleSquadStatusView>(true);
        Require(status != null && status.CurrentModel.CurrentActionPoints ==
                player.Runtime.State.currentActionPoints,
            "HUD did not refresh the active squad AP after the attack.");
        Require(!string.IsNullOrWhiteSpace(battleHud.AbilityDetails.LastResultFeedback) &&
                !string.IsNullOrWhiteSpace(enemy.AttackTarget.TargetingView.LastFeedback),
            "HUD or formation attack feedback did not render the resolved hit.");
        Require(commandMode.ActiveMode == BattleCommandMode.None &&
                !attackCommands.IsAttackTargeting &&
                !movementCommands.IsMovementTargeting &&
                !movement.IsMoving && !attackService.IsExecuting,
            "Move/Attack modes or execution locks remained active after resolution.");
        Require(UnityEngine.Object.FindObjectsByType<EventSystem>(
                    FindObjectsInactive.Include).Length == 1,
            "Attack flow created or removed an EventSystem.");
        Require(tacticalBootstrap.SuccessfulInitializationCount == 1,
            "Attack flow initialized the tactical composition more than once.");

        Finish(true,
            "Raw Alpha production startup, selection, Move approach, command-mode " +
            "exclusivity, Attack Button, target preview, deterministic hit, AP, existing " +
            "damage resolver, Warrior casualty, formation, HUD, initiative, and occupancy passed.");
    }

    private sealed class SmokeBattleRandomSource : IBattleRandomSource
    {
        private readonly float[] values;
        private int index;

        public SmokeBattleRandomSource(params float[] configuredValues)
        {
            values = configuredValues ?? Array.Empty<float>();
        }

        public float Next01()
        {
            if (values.Length == 0)
                return 0f;
            float value = values[Math.Min(index, values.Length - 1)];
            index++;
            return Mathf.Clamp01(value);
        }
    }

    private static float AnchorArea(RectTransform rect)
    {
        if (rect == null)
            return 0f;
        Vector2 size = rect.anchorMax - rect.anchorMin;
        return size.x * size.y;
    }

    private static void Finish(bool passed, string message)
    {
        WriteResult(passed, message);
        Debug.Log(
            $"Squad battle scene smoke: {(passed ? "PASSED" : "FAILED")} - {message}");
        SessionState.SetBool(FinishedKey, true);
        SessionState.SetBool(PassedKey, passed);
        if (EditorApplication.isPlaying)
            EditorApplication.isPlaying = false;
    }

    private static void CleanupRequest()
    {
        bool passed = SessionState.GetBool(PassedKey, false);
        EditorApplication.update -= UpdateRun;
        AssetDatabase.DeleteAsset(RunRequestPath);
        SessionState.EraseBool(StartedKey);
        SessionState.EraseBool(FinishedKey);
        SessionState.EraseBool(PassedKey);
        SessionState.EraseString(StartTimeKey);
        SessionState.EraseInt(PhaseKey);
        SessionState.EraseInt(DestinationXKey);
        SessionState.EraseInt(DestinationYKey);
        SessionState.EraseInt(InitialActionPointsKey);
        SessionState.EraseInt(MovementCostKey);
        SessionState.EraseInt(ExpectedMovementCountKey);
        if (Application.isBatchMode)
            EditorApplication.Exit(passed ? 0 : 1);
    }

    private static void WriteResult(bool passed, string message)
    {
        Directory.CreateDirectory("Logs");
        XmlWriterSettings settings = new XmlWriterSettings
        {
            Indent = true
        };
        using XmlWriter writer = XmlWriter.Create(ResultPath, settings);
        writer.WriteStartDocument();
        writer.WriteStartElement("test-run");
        writer.WriteAttributeString("total", "1");
        writer.WriteAttributeString("passed", passed ? "1" : "0");
        writer.WriteAttributeString("failed", passed ? "0" : "1");
        writer.WriteAttributeString("result", passed ? "Passed" : "Failed");
        writer.WriteStartElement("test-case");
        writer.WriteAttributeString("name", "RawAlphaSquadPlayModeSmoke");
        writer.WriteAttributeString("result", passed ? "Passed" : "Failed");
        if (!passed)
        {
            writer.WriteStartElement("failure");
            writer.WriteElementString("message", message);
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
