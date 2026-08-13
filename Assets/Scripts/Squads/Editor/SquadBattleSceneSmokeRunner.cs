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
    private const string ExpectedEndTurnCountKey =
        "SquadBattleSceneSmoke.ExpectedEndTurnCount";
    private const string PlayerHPBeforeAITurnKey =
        "SquadBattleSceneSmoke.PlayerHPBeforeAITurn";
    private const string EnemyCellXBeforeAITurnKey =
        "SquadBattleSceneSmoke.EnemyCellXBeforeAITurn";
    private const string EnemyCellYBeforeAITurnKey =
        "SquadBattleSceneSmoke.EnemyCellYBeforeAITurn";
    private const string AICommittedActionsBeforeKey =
        "SquadBattleSceneSmoke.AICommittedActionsBefore";
    private const string StorageConfiguredKey =
        "SquadBattleSceneSmoke.StorageConfigured";
    private const string ExpectedBattleIdKey =
        "SquadBattleSceneSmoke.ExpectedBattleId";
    private const string ExpectedEncounterIdKey =
        "SquadBattleSceneSmoke.ExpectedEncounterId";
    private const string RallyUsedKey = "SquadBattleSceneSmoke.RallyUsed";
    private const string PowerStrikeUsedKey = "SquadBattleSceneSmoke.PowerStrikeUsed";
    private const string SweepingBlowUsedKey = "SquadBattleSceneSmoke.SweepingBlowUsed";
    private const string MinimapInactivityStartKey =
        "SquadBattleSceneSmoke.MinimapInactivityStart";
    private const string RuntimeRegressionErrorKey =
        "SquadBattleSceneSmoke.RuntimeRegressionError";
    private const string SmokeSaveRoot = "Temp/BattleLifecycleSmoke";
    private const int MaximumApproachMoves = 12;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void PrepareProductionEncounter()
    {
        if (!File.Exists(RunRequestPath))
            return;

        SessionState.EraseString(RuntimeRegressionErrorKey);
        Application.logMessageReceived -= CaptureRuntimeRegressionError;
        Application.logMessageReceived += CaptureRuntimeRegressionError;
        BattleReturnContext.Clear();
        PendingSaveLoadContext.Clear();
        ResolvedEncounterRegistry.Clear();
        BattleSquadSelectionContext.Clear();
        BattleEncounterContext.SetEncounterData(
            1701,
            new Vector2Int(2, 2),
            new Vector2Int(3, 2),
            BiomeType.Plains,
            BiomeType.Plains,
            EncounterInitiator.Player,
            10);
    }

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
        SessionState.EraseBool(StorageConfiguredKey);
        SessionState.EraseBool(RallyUsedKey);
        SessionState.EraseBool(PowerStrikeUsedKey);
        SessionState.EraseBool(SweepingBlowUsedKey);
        SessionState.EraseString(RuntimeRegressionErrorKey);
        SessionState.SetInt(PhaseKey, 10);
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

        int activePhase = SessionState.GetInt(PhaseKey, 0);
        if (activePhase == 4)
        {
            try
            {
                CompleteOverworldReturn();
            }
            catch (Exception exception)
            {
                Finish(false, exception.Message);
            }
            return;
        }

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
                int phase = activePhase;
                if (phase == 10)
                    BeginProductionMinimap(mapBootstrap, battleHud);
                else if (phase == 11)
                    CompleteMinimapCollapse();
                else if (phase == 12)
                    CompleteMinimapExpand();
                else if (phase == 0)
                    BeginProductionMovement(mapBootstrap, battleHud, tacticalBootstrap);
                else if (phase == 1)
                    CompleteProductionMovementAndTurn(battleHud, tacticalBootstrap);
                else if (phase == 2)
                    CompleteEnemyTacticalAITurn(tacticalBootstrap);
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
        if (EditorApplication.timeSinceStartup - startTime > 90)
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
            mapGenerator.Width == 32 && mapGenerator.Height == 32 &&
            mapGenerator.PotentialCellCount == 1024,
            $"Production map is {mapGenerator.Width}x{mapGenerator.Height}, not 32x32.");
        Require(
            mapGenerator.PlayableCellCount > 0 &&
            mapGenerator.PlayableCellCount < mapGenerator.PotentialCellCount,
            "Playable/non-playable generated-map contract is invalid.");
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
                    FindObjectsInactive.Include).Length == 1 &&
                UnityEngine.Object.FindObjectsByType<BattleAbilityService>(
                    FindObjectsInactive.Include).Length == 1 &&
                UnityEngine.Object.FindObjectsByType<AbilityCommandController>(
                    FindObjectsInactive.Include).Length == 1 &&
                UnityEngine.Object.FindObjectsByType<EnemyTacticalAIController>(
                    FindObjectsInactive.Include).Length == 1,
            "Scene must contain exactly one owner for occupancy, selection, turns, " +
            "movement, command mode, attack execution, ability execution, and Enemy AI.");
        BattleTurnController productionTurns =
            UnityEngine.Object.FindAnyObjectByType<BattleTurnController>();
        EnemyTacticalAIController productionAI =
            UnityEngine.Object.FindAnyObjectByType<EnemyTacticalAIController>();
        Require(!productionTurns.DevelopmentAutoSkipAIEnabled &&
                productionAI != null && productionAI.IsInitialized,
            "Production Enemy Tactical AI is not initialized or development auto-skip remains enabled.");

        TacticalCameraController[] cameras =
            UnityEngine.Object.FindObjectsByType<TacticalCameraController>(
                FindObjectsInactive.Include);
        TacticalMinimapController[] minimaps =
            UnityEngine.Object.FindObjectsByType<TacticalMinimapController>(
                FindObjectsInactive.Include);
        Require(cameras.Length == 1 && cameras[0].IsInitialized,
            "Expected one initialized TacticalCameraController.");
        Require(minimaps.Length == 1 && minimaps[0].IsInitialized &&
                minimaps[0].SuccessfulInitializationCount == 1,
            "Expected one initialized TacticalMinimapController owner.");
        RequireNoRuntimeRegressionErrors();

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
        Require(actions.Length == 8,
            "Bottom action bar does not expose eight production action controls.");
        Require(actions.Count(action => action.gameObject.name == "Move") == 1 &&
                actions.Count(action => action.gameObject.name == "Attack") == 1 &&
                actions.Count(action => action.gameObject.name == "EndTurn") == 1,
            "Move, Attack, and EndTurn must each have one explicit HUD control.");
        Require(actions.Count(action => action.gameObject.name == "PowerStrike") == 1 &&
                actions.Count(action => action.gameObject.name == "SweepingBlow") == 1 &&
                actions.Count(action => action.gameObject.name == "Rally") == 1,
            "Power Strike, Sweeping Blow, and Rally must each have one HUD control.");
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

    private static void BeginProductionMinimap(
        BattleMapBootstrap mapBootstrap,
        BattleHUDController battleHud)
    {
        TacticalMinimapController minimap =
            UnityEngine.Object.FindAnyObjectByType<TacticalMinimapController>();
        if (minimap == null || !minimap.IsInitialized)
            return;

        ValidateScene(mapBootstrap, battleHud);
        ConfigureSmokeStorage();
        MapGenerator generator = mapBootstrap.mapGenerator;
        SquadBattleBootstrap squads =
            UnityEngine.Object.FindAnyObjectByType<SquadBattleBootstrap>();
        BattleSquadSelectionController selection =
            UnityEngine.Object.FindAnyObjectByType<BattleSquadSelectionController>();
        BattleCommandModeController modes =
            UnityEngine.Object.FindAnyObjectByType<BattleCommandModeController>();
        MovementCommandController movement =
            UnityEngine.Object.FindAnyObjectByType<MovementCommandController>();
        GridOccupancyService occupancy =
            UnityEngine.Object.FindAnyObjectByType<GridOccupancyService>();
        TacticalCameraController camera =
            UnityEngine.Object.FindAnyObjectByType<TacticalCameraController>();
        SquadBattleController player = squads.SpawnedControllers.Single(
            controller => controller.Side == BattleSide.Player);
        BattleTurnController turns =
            UnityEngine.Object.FindAnyObjectByType<BattleTurnController>();

        Require(camera.TurnFocusCount >= 1 &&
                camera.LastTurnFocusSquadId == turns.ActiveSquad?.SquadId,
            "Tactical camera did not focus the first/current active squad through the turn event.");
        Vector3 beforeArrowPan = camera.ControlledCamera.transform.position;
        int keyboardPanBefore = camera.KeyboardPanCount;
        float footprintCenterX = camera.CurrentFootprint.Average(point => point.x);
        Vector2 arrowDirection = footprintCenterX <= camera.MapBounds.center.x
            ? Vector2.right
            : Vector2.left;
        Require(camera.PanFromKeyboard(arrowDirection, 0.25f) &&
                camera.KeyboardPanCount == keyboardPanBefore + 1 &&
                camera.ControlledCamera.transform.position != beforeArrowPan,
            "Production Arrow-key pan contract did not move the tactical camera.");

        Require(minimap.GridPresenter.GridGraphic.PotentialElementCount == 1024,
            "Minimap static layer did not represent all 1024 potential cells.");
        Require(minimap.GridPresenter.GridGraphic.PlayableElementCount ==
                generator.PlayableCellCount,
            "Minimap playable layer differs from MapGenerator truth.");
        Require(minimap.GridPresenter.GridGraphic.BuildCount == 1,
            "Minimap static grid was rebuilt more than once.");
        Require(minimap.MarkerPresenter.MarkerCount == 2,
            "Expected exactly two production minimap squad markers.");
        Require(minimap.ViewportPresenter.RefreshCount > 0,
            "Minimap camera viewport did not receive its initial footprint.");

        int movementCommandsBefore = movement.MovementCommandCount;
        int apBefore = player.Runtime.State.currentActionPoints;
        int occupiedBefore = occupancy.OccupiedCellCount;
        Vector2Int playerCellBefore = player.GridAnchor.CurrentCell;
        SquadBattleController selectionBefore = selection.SelectedSquad;
        BattleCommandMode modeBefore = modes.ActiveMode;
        float fieldOfViewBefore = camera.ControlledCamera.fieldOfView;
        int cameraChangesBefore = camera.PositionChangeCount;
        Vector2Int[] playable = EnumeratePlayable(generator);
        Vector2Int clickCell = playable
            .OrderByDescending(cell =>
                (mapBootstrap.mapRenderer.GetCellWorldCenter(cell) - camera.MapBounds.center)
                .sqrMagnitude)
            .First();
        Vector2Int dragCell = playable
            .OrderByDescending(cell =>
                (cell - clickCell).sqrMagnitude)
            .First(cell => cell != clickCell);

        ExecuteMinimapPointer(
            minimap.InteractionController,
            minimap.Mapper.GridToNormalized(clickCell),
            ExecuteEvents.pointerClickHandler);
        Require(minimap.InteractionController.AcceptedFocusCount == 1 &&
                camera.PositionChangeCount > cameraChangesBefore,
            "Production minimap click did not focus the tactical camera.");

        PointerEventData drag = CreateMinimapPointer(
            minimap.InteractionController,
            minimap.Mapper.GridToNormalized(clickCell));
        ExecuteEvents.Execute(
            minimap.InteractionController.gameObject,
            drag,
            ExecuteEvents.beginDragHandler);
        drag.position = GetMinimapScreenPoint(
            minimap.InteractionController,
            minimap.Mapper.GridToNormalized(dragCell));
        ExecuteEvents.Execute(
            minimap.InteractionController.gameObject,
            drag,
            ExecuteEvents.dragHandler);
        ExecuteEvents.Execute(
            minimap.InteractionController.gameObject,
            drag,
            ExecuteEvents.endDragHandler);
        Require(minimap.InteractionController.DragCount == 1,
            "Production minimap drag did not pan the tactical camera.");

        PointerEventData scroll = CreateMinimapPointer(
            minimap.InteractionController,
            minimap.Mapper.GridToNormalized(dragCell));
        scroll.scrollDelta = Vector2.up;
        ExecuteEvents.Execute(
            minimap.InteractionController.gameObject,
            scroll,
            ExecuteEvents.scrollHandler);
        Require(minimap.InteractionController.ScrollCount == 1 &&
                !Mathf.Approximately(camera.ControlledCamera.fieldOfView, fieldOfViewBefore),
            "Production minimap scroll did not zoom through TacticalCameraController.");
        Require(minimap.ViewportPresenter.RefreshCount > 1,
            "Camera pan/zoom did not update the minimap viewport overlay.");

        Require(movement.MovementCommandCount == movementCommandsBefore &&
                player.Runtime.State.currentActionPoints == apBefore &&
                occupancy.OccupiedCellCount == occupiedBefore &&
                player.GridAnchor.CurrentCell == playerCellBefore &&
                selection.SelectedSquad == selectionBefore &&
                modes.ActiveMode == modeBefore,
            "Minimap camera interaction leaked into a battlefield gameplay command.");
        RequireNoRuntimeRegressionErrors();

        LogMapPerformanceSanity(mapBootstrap, minimap, playable);
        SessionState.SetString(
            MinimapInactivityStartKey,
            Time.unscaledTime.ToString(
                System.Globalization.CultureInfo.InvariantCulture));
        SessionState.SetInt(PhaseKey, 11);
    }

    private static void CompleteMinimapCollapse()
    {
        TacticalMinimapController minimap =
            UnityEngine.Object.FindAnyObjectByType<TacticalMinimapController>();
        if (minimap == null || !minimap.IsInitialized ||
            minimap.CollapseController.State != MinimapCollapseState.Collapsed)
        {
            return;
        }
        float started = float.Parse(
            SessionState.GetString(MinimapInactivityStartKey, "0"),
            System.Globalization.CultureInfo.InvariantCulture);
        Require(Time.unscaledTime - started >= 10f,
            "Minimap collapsed before ten unscaled seconds of inactivity.");
        Require(minimap.CollapseController.ActiveAnimationCount == 0,
            "Minimap retained a duplicate animation after collapse.");
        Require(minimap.CollapseController.ExpandButton != null,
            "Collapsed minimap has no explicit reopen button.");
        minimap.CollapseController.ExpandButton.onClick.Invoke();
        Require(minimap.CollapseController.State == MinimapCollapseState.Expanding,
            "Collapsed minimap button did not start expansion.");
        SessionState.SetInt(PhaseKey, 12);
    }

    private static void CompleteMinimapExpand()
    {
        TacticalMinimapController minimap =
            UnityEngine.Object.FindAnyObjectByType<TacticalMinimapController>();
        if (minimap == null ||
            minimap.CollapseController.State != MinimapCollapseState.Expanded)
        {
            return;
        }
        Require(minimap.CollapseController.ActiveAnimationCount == 0 &&
                minimap.SuccessfulInitializationCount == 1,
            "Minimap reopen duplicated animation or initialization ownership.");
        SessionState.SetInt(PhaseKey, 0);
    }

    private static void ExecuteMinimapPointer<T>(
        MinimapInteractionController interaction,
        Vector2 normalized,
        ExecuteEvents.EventFunction<T> handler)
        where T : IEventSystemHandler
    {
        PointerEventData pointer = CreateMinimapPointer(interaction, normalized);
        ExecuteEvents.Execute(interaction.gameObject, pointer, handler);
    }

    private static PointerEventData CreateMinimapPointer(
        MinimapInteractionController interaction,
        Vector2 normalized)
    {
        Require(EventSystem.current != null, "Production EventSystem is unavailable.");
        return new PointerEventData(EventSystem.current)
        {
            button = PointerEventData.InputButton.Left,
            position = GetMinimapScreenPoint(interaction, normalized)
        };
    }

    private static Vector2 GetMinimapScreenPoint(
        MinimapInteractionController interaction,
        Vector2 normalized)
    {
        RectTransform rect = interaction.transform as RectTransform;
        Rect localRect = rect.rect;
        Vector3 local = new Vector3(
            Mathf.Lerp(localRect.xMin, localRect.xMax, normalized.x),
            Mathf.Lerp(localRect.yMin, localRect.yMax, normalized.y));
        return RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(local));
    }

    private static Vector2Int[] EnumeratePlayable(MapGenerator generator)
    {
        System.Collections.Generic.List<Vector2Int> cells =
            new System.Collections.Generic.List<Vector2Int>();
        for (int x = 0; x < generator.Width; x++)
        for (int y = 0; y < generator.Height; y++)
            if (generator.GetIsPlayable(x, y))
                cells.Add(new Vector2Int(x, y));
        return cells.ToArray();
    }

    private static void LogMapPerformanceSanity(
        BattleMapBootstrap bootstrap,
        TacticalMinimapController minimap,
        Vector2Int[] playable)
    {
        Vector2Int start = bootstrap.mapGenerator.GetStartCell();
        Vector2Int shortTarget = playable.First(cell =>
            cell != start &&
            BattleTargetingService.GetGridDistance(start, cell, true) == 1);
        Vector2Int longTarget = playable
            .OrderByDescending(cell =>
                BattleTargetingService.GetGridDistance(start, cell, true))
            .First();
        long shortStarted = System.Diagnostics.Stopwatch.GetTimestamp();
        GridPathfinder.TryBuildPath(
            bootstrap.mapGenerator, start, shortTarget, true, null, out _);
        double shortMs =
            (System.Diagnostics.Stopwatch.GetTimestamp() - shortStarted) * 1000d /
            System.Diagnostics.Stopwatch.Frequency;
        long longStarted = System.Diagnostics.Stopwatch.GetTimestamp();
        GridPathfinder.TryBuildPath(
            bootstrap.mapGenerator, start, longTarget, true, null, out _);
        double longMs =
            (System.Diagnostics.Stopwatch.GetTimestamp() - longStarted) * 1000d /
            System.Diagnostics.Stopwatch.Frequency;
        Debug.Log(
            $"Battle map performance sanity: generated={bootstrap.mapGenerator.PotentialCellCount}, " +
            $"playable={bootstrap.mapGenerator.PlayableCellCount}, " +
            $"minimapStatic={minimap.GridPresenter.GridGraphic.PotentialElementCount}, " +
            $"generationMs={bootstrap.mapGenerator.LastGenerationMilliseconds:F2}, " +
            $"renderMs={bootstrap.mapRenderer.LastRenderMilliseconds:F2}, " +
            $"minimapBuildMs={minimap.GridPresenter.LastBuildMilliseconds:F2}, " +
            $"shortPathMs={shortMs:F3}, longPathMs={longMs:F3}.");
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
        {
            ValidateScene(mapBootstrap, battleHud);
            ConfigureSmokeStorage();
        }

        // If the deterministic first entry is AI, wait for the production AI owner to
        // finish through the same BattleTurnController flow as ordinary Play.
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
        TacticalMinimapController minimap =
            UnityEngine.Object.FindAnyObjectByType<TacticalMinimapController>();
        RectTransform marker = minimap?.MarkerPresenter.GetMarkerRect(player.SquadId);
        Vector2 expectedMarker = minimap != null
            ? minimap.Mapper.GridToNormalized(destination)
            : Vector2.zero;
        Require(marker != null &&
                Mathf.Abs(marker.anchorMin.x - expectedMarker.x) < 0.0001f &&
                Mathf.Abs(marker.anchorMin.y - expectedMarker.y) < 0.0001f,
            "SquadGridAnchor.CellChanged did not relocate the minimap marker.");

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
        PrepareAITurnSmoke(player, enemy);
        TacticalCameraController camera =
            UnityEngine.Object.FindAnyObjectByType<TacticalCameraController>();
        int turnFocusBefore = camera.TurnFocusCount;
        endTurnAction.Button.onClick.Invoke();
        Require(commands.EndTurnCommandCount == expectedMovementCount &&
                turns.ActiveSquad == enemy,
            "EndTurn HUD click did not advance to the next initiative entry exactly once.");
        Require(camera.TurnFocusCount == turnFocusBefore + 1 &&
                camera.LastTurnFocusSquadId == enemy.SquadId,
            "Active squad change did not focus the Enemy exactly once for its turn.");
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
        RequireNoRuntimeRegressionErrors();

        SessionState.SetInt(ExpectedEndTurnCountKey, commands.EndTurnCommandCount);
        SessionState.SetInt(PhaseKey, 2);
    }

    private static void CompleteEnemyTacticalAITurn(
        SquadBattleTacticalBootstrap tacticalBootstrap)
    {
        BattleTurnController turns =
            UnityEngine.Object.FindAnyObjectByType<BattleTurnController>();
        SquadBattleBootstrap squadBootstrap =
            UnityEngine.Object.FindAnyObjectByType<SquadBattleBootstrap>();
        MovementCommandController commands =
            UnityEngine.Object.FindAnyObjectByType<MovementCommandController>();
        EnemyTacticalAIController enemyAI =
            UnityEngine.Object.FindAnyObjectByType<EnemyTacticalAIController>();
        GridOccupancyService occupancy =
            UnityEngine.Object.FindAnyObjectByType<GridOccupancyService>();
        SquadBattleController player = squadBootstrap.SpawnedControllers.Single(
            controller => controller.Side == BattleSide.Player);
        SquadBattleController enemy = squadBootstrap.SpawnedControllers.Single(
            controller => controller.Side == BattleSide.Enemy);
        int expectedMovementCount = SessionState.GetInt(ExpectedMovementCountKey, -1);
        int expectedEndTurnCount = SessionState.GetInt(ExpectedEndTurnCountKey, -1);
        if (enemyAI == null || enemyAI.IsExecutingTurn || turns.ActiveSquad != player ||
            enemyAI.CompletedTurnCount < expectedEndTurnCount)
            return;

        Require(commands.MovementCommandCount == expectedMovementCount &&
                commands.EndTurnCommandCount == expectedEndTurnCount,
            "Enemy Tactical AI or turn cycling duplicated a Human command.");
        Require(enemyAI.CompletedTurnCount >= expectedEndTurnCount &&
                enemyAI.BegunTurnCount == enemyAI.CompletedTurnCount &&
                enemyAI.EndTurnRequestCount == enemyAI.CompletedTurnCount,
            "Enemy Tactical AI did not own and finish exactly one routine per AI turn.");
        Require(enemyAI.PeakConcurrentRoutineCount == 1 &&
                enemyAI.DuplicateBeginRejectedCount == 0,
            "Enemy Tactical AI created duplicate or concurrent turn routines.");
        Require(enemyAI.MovementActionCount + enemyAI.BasicAttackActionCount +
                enemyAI.AbilityActionCount >= enemyAI.CompletedTurnCount,
            "Enemy Tactical AI ended a production turn without a committed tactical action.");
        int actionsBefore = SessionState.GetInt(AICommittedActionsBeforeKey, -1);
        int actionsAfter = enemyAI.MovementActionCount +
                           enemyAI.BasicAttackActionCount +
                           enemyAI.AbilityActionCount;
        Require(actionsAfter > actionsBefore,
            "Enemy Tactical AI did not commit a production action during this turn.");
        int playerHPBefore = SessionState.GetInt(PlayerHPBeforeAITurnKey, -1);
        Vector2Int enemyCellBefore = new Vector2Int(
            SessionState.GetInt(EnemyCellXBeforeAITurnKey, int.MinValue),
            SessionState.GetInt(EnemyCellYBeforeAITurnKey, int.MinValue));
        Require(player.Runtime.State.CurrentSquadHP < playerHPBefore ||
                enemy.GridAnchor.CurrentCell != enemyCellBefore,
            "Enemy Tactical AI neither moved nor damaged its selected Player target.");
        Require(occupancy.TryGetOccupiedCell(enemy, out Vector2Int occupiedCell) &&
                occupiedCell == enemy.GridAnchor.CurrentCell &&
                occupancy.ReservationCount == 0,
            "Enemy Tactical AI movement did not finish with canonical occupancy state.");
        TacticalCameraController camera =
            UnityEngine.Object.FindAnyObjectByType<TacticalCameraController>();
        Require(camera.LastTurnFocusSquadId == player.SquadId &&
                camera.TurnFocusCount >= 3,
            "Camera did not return focus to Player when the AI turn completed.");
        RequireNoRuntimeRegressionErrors();
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
        BattleTurnController turns =
            UnityEngine.Object.FindAnyObjectByType<BattleTurnController>();
        BattleCompletionController completion =
            UnityEngine.Object.FindAnyObjectByType<BattleCompletionController>();
        BattleAbilityService abilityService =
            UnityEngine.Object.FindAnyObjectByType<BattleAbilityService>();
        AbilityCommandController abilityCommands =
            UnityEngine.Object.FindAnyObjectByType<AbilityCommandController>();
        Require(attackService != null && attackService.IsInitialized,
            "Production attack service is not initialized.");
        Require(attackCommands != null && attackCommands.IsInitialized,
            "Production attack command controller is not initialized.");
        Require(abilityService != null && abilityService.IsInitialized,
            "Production ability service is not initialized.");
        Require(abilityCommands != null && abilityCommands.IsInitialized,
            "Production ability command controller is not initialized.");
        Require(completion != null && completion.State == BattleCompletionState.Running,
            "Battle completion owner is missing or not Running before the final command.");
        Require(player.AttackTarget != null && enemy.AttackTarget != null,
            "A spawned squad is missing its root attack target adapter.");

        BattleActionControlView[] actions = battleHud
            .GetComponentsInChildren<BattleActionControlView>(true);
        BattleActionControlView moveAction = actions.Single(
            action => action.gameObject.name == "Move");
        BattleActionControlView attackAction = actions.Single(
            action => action.gameObject.name == "Attack");
        BattleActionControlView endTurnAction = actions.Single(
            action => action.gameObject.name == "EndTurn");

        if (!SessionState.GetBool(RallyUsedKey, false))
        {
            AbilityDefinition rally = abilityService.Abilities.Single(
                ability => ability.StableId == "DEV_Rally");
            if (EndPlayerTurnWhenActionPointsAreInsufficient(
                    rally.ActionPointCost,
                    endTurnAction,
                    movementCommands,
                    turns,
                    enemy,
                    commandMode))
            {
                return;
            }

            BattleActionControlView rallyAction = actions.Single(
                action => action.gameObject.name == "Rally");
            int initialAbilityCount = abilityCommands.AbilityCommandCount;
            int rallyInitialActionPoints = player.Runtime.State.currentActionPoints;
            int initialHealth = player.Runtime.State.CurrentSquadHP;
            float fullMorale = player.Runtime.State.currentMorale;
            float appliedLoss = player.Runtime.ApplyMoraleLoss(10f);
            float damagedMorale = player.Runtime.State.currentMorale;
            Require(appliedLoss > 0f && damagedMorale < fullMorale,
                "Controlled runtime morale loss did not create a valid Rally target state.");
            Require(rallyAction.Button.interactable,
                "Rally production control is not available after controlled morale loss.");

            rallyAction.Button.onClick.Invoke();
            BattleAbilityResult rallyResult = abilityCommands.LastResult;
            BattleAbilityRuntimeState rallyState = abilityService.GetRuntimeState(
                player.SquadId,
                rally.StableId);
            Require(rallyResult != null && rallyResult.Succeeded &&
                    rallyResult.AbilityId == rally.StableId &&
                    rallyResult.TargetSquadId == player.SquadId &&
                    rallyResult.MoraleRestored > 0f && rallyResult.Damage == 0,
                "Rally did not execute as a non-damaging self-target ability.");
            Require(abilityCommands.AbilityCommandCount == initialAbilityCount + 1 &&
                    player.Runtime.State.currentActionPoints ==
                    rallyInitialActionPoints - rally.ActionPointCost &&
                    player.Runtime.State.CurrentSquadHP == initialHealth &&
                    player.Runtime.State.currentMorale > damagedMorale &&
                    player.Runtime.State.currentMorale <= player.Runtime.Stats.Morale,
                "Rally did not commit AP and bounded morale exactly once through runtime APIs.");
            Require(rallyState != null &&
                    rallyState.remainingCooldown == rally.CooldownRounds &&
                    rallyState.usesThisBattle == 1,
                "Rally cooldown or battle-scoped usage state was not committed once.");
            Require(commandMode.ActiveMode == BattleCommandMode.None &&
                    !abilityCommands.IsAbilityTargeting &&
                    battleHud.AbilityDetails.CurrentAbilityPreview.AbilityId == rally.StableId &&
                    !string.IsNullOrWhiteSpace(battleHud.AbilityDetails.LastResultFeedback),
                "Rally did not leave production command mode and Ability Info in a resolved state.");
            SessionState.SetBool(RallyUsedKey, true);
            return;
        }

        if (!SessionState.GetBool(PowerStrikeUsedKey, false))
        {
            AbilityDefinition powerStrike = abilityService.Abilities.Single(
                ability => ability.StableId == "DEV_PowerStrike");
            if (EndPlayerTurnWhenActionPointsAreInsufficient(
                    powerStrike.ActionPointCost,
                    endTurnAction,
                    movementCommands,
                    turns,
                    enemy,
                    commandMode))
            {
                return;
            }

            ExecuteProductionDamageAbility(
                battleHud,
                player,
                enemy,
                attackService,
                abilityService,
                abilityCommands,
                movementCommands,
                commandMode,
                actions.Single(action => action.gameObject.name == "PowerStrike"),
                powerStrike,
                SquadDamageDistribution.SingleTarget);
            SessionState.SetBool(PowerStrikeUsedKey, true);
            return;
        }

        if (!SessionState.GetBool(SweepingBlowUsedKey, false))
        {
            AbilityDefinition sweepingBlow = abilityService.Abilities.Single(
                ability => ability.StableId == "DEV_SweepingBlow");
            if (EndPlayerTurnWhenActionPointsAreInsufficient(
                    sweepingBlow.ActionPointCost,
                    endTurnAction,
                    movementCommands,
                    turns,
                    enemy,
                    commandMode))
            {
                return;
            }

            ExecuteProductionDamageAbility(
                battleHud,
                player,
                enemy,
                attackService,
                abilityService,
                abilityCommands,
                movementCommands,
                commandMode,
                actions.Single(action => action.gameObject.name == "SweepingBlow"),
                sweepingBlow,
                SquadDamageDistribution.Area);
            SessionState.SetBool(SweepingBlowUsedKey, true);
            return;
        }

        if (player.Runtime.State.currentActionPoints <
            attackService.BasicAttack.ActionPointCost)
        {
            Require(endTurnAction.Button.interactable,
                "End Turn is unavailable after the player exhausts attack AP.");
            int expectedEndTurns = movementCommands.EndTurnCommandCount + 1;
            PrepareAITurnSmoke(player, enemy);
            endTurnAction.Button.onClick.Invoke();
            Require(movementCommands.EndTurnCommandCount == expectedEndTurns &&
                    turns.ActiveSquad == enemy,
                "Production End Turn did not hand control to the AI squad after attacks.");
            Require(commandMode.ActiveMode == BattleCommandMode.None,
                "End Turn did not clear the command mode after attacks.");
            SessionState.SetInt(ExpectedEndTurnCountKey, expectedEndTurns);
            SessionState.SetInt(PhaseKey, 2);
            return;
        }

        // The smoke injects only the battle-scoped roll sequence; UI, validation,
        // AP commit, resolver, formation, and HUD all remain the production flow.
        attackService.SetRandomSourceForTests(new SmokeBattleRandomSource(0f, 0.99f));
        int expectedAttackCount = attackCommands.AttackCommandCount + 1;
        int initialActionPoints = player.Runtime.State.currentActionPoints;
        int initialEnemyHealth = enemy.Runtime.State.CurrentSquadHP;
        int initialLivingWarriors = enemy.Runtime.State.warriors.Count(
            warrior => warrior != null && !warrior.defeated);

        Require(moveAction.Button.interactable && attackAction.Button.interactable,
            "Move and Attack must both be available to the active adjacent Human squad.");

        if (attackCommands.AttackCommandCount == 0)
        {
            moveAction.Button.onClick.Invoke();
            Require(movementCommands.IsMovementTargeting,
                "Production Move button did not enter Move mode before the exclusivity check.");
        }
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
        Require(result != null &&
                result.WeaponDefinitionId ==
                player.Runtime.Equipment.SquadWeapon?.DefinitionId,
            "Production basic attack did not use the immutable Squad Weapon snapshot.");
        Require(result != null && result.WasExecuted && result.Hit && !result.Critical,
            "Root target confirmation did not produce the injected deterministic hit.");
        Require(attackCommands.AttackCommandCount == expectedAttackCount,
            "One production target confirmation must execute exactly one attack.");
        Require(player.Runtime.State.currentActionPoints ==
                initialActionPoints - attackService.BasicAttack.ActionPointCost,
            "Attack AP was not committed exactly once through the runtime API.");
        Require(enemy.Runtime.State.CurrentSquadHP ==
                initialEnemyHealth - result.AppliedDamage,
            "Existing SquadDamageResolver did not apply the reported final damage.");
        int finalLivingWarriors = enemy.Runtime.State.warriors.Count(
            warrior => warrior != null && !warrior.defeated);
        Require(finalLivingWarriors <= initialLivingWarriors &&
                enemy.FormationView.ActiveWarriorModelCount == finalLivingWarriors,
            "Formation did not match the authoritative runtime composition after damage.");
        if (result.DefeatedWarriorIds.Count > 0)
        {
            Require(finalLivingWarriors == initialLivingWarriors - 1,
                "Single-target damage defeated more than its one formation target.");
        }

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

        if (!enemy.Runtime.State.IsDefeated)
        {
            Require(squadBootstrap.InitiativeOrder.Entries.Count == 2 &&
                    occupancy.OccupiedCellCount == 2,
                "A surviving squad was incorrectly removed from initiative or occupancy.");
            return;
        }

        BattleResultPanelView resultPanel =
            UnityEngine.Object.FindAnyObjectByType<BattleResultPanelView>();
        Require(completion.State == BattleCompletionState.Completed &&
                completion.CompletionCount == 1 &&
                completion.AutosaveAttemptCount == 1,
            "Enemy Commander defeat did not complete and autosave the battle exactly once.");
        Require(completion.Outcome != null &&
                completion.Outcome.resultType == BattleResultType.Victory &&
                completion.Outcome.winningSide == BattleSide.Player &&
                completion.Outcome.participantResults.Count == 2 &&
                completion.Outcome.defeatedSquadIds.Contains(enemy.SquadId) &&
                completion.Outcome.casualties.Count > 0,
            "Versioned BattleOutcome does not contain the real participants and casualties.");
        Require(completion.Outcome.abilityUsages.Count == 3 &&
                completion.Outcome.abilityUsages.All(usage =>
                    usage.squadId == player.SquadId && usage.uses == 1) &&
                completion.Outcome.abilityUsages.Any(usage =>
                    usage.abilityId == "DEV_Rally") &&
                completion.Outcome.abilityUsages.Any(usage =>
                    usage.abilityId == "DEV_PowerStrike") &&
                completion.Outcome.abilityUsages.Any(usage =>
                    usage.abilityId == "DEV_SweepingBlow"),
            "BattleOutcome did not summarize the three production ability commands exactly once.");
        SquadBattleResult enemyResult = completion.Outcome.participantResults.Single(
            participant => participant.squadId == enemy.SquadId);
        Require(enemyResult.initialWarriorIds.Count > 0 &&
                enemyResult.defeatedWarriorIds.Count == enemyResult.initialWarriorIds.Count &&
                enemyResult.survivingWarriorIds.Count == 0 &&
                enemyResult.commanderDefeatedInBattle,
            "Enemy result does not preserve the stable Warrior and Commander defeat state.");
        Require(completion.Outcome.persistentMutationsApplied &&
                completion.Outcome.autosaveSucceeded,
            "Battle result was not applied and autosaved before Continue became available.");
        Require(turns.IsBattleLocked && turns.ActiveSquad == null &&
                commandMode.IsLocked && commandMode.ActiveMode == BattleCommandMode.None &&
                !movement.CommandsEnabled && !attackService.CommandsEnabled &&
                !abilityService.CommandsEnabled,
            "Completion did not lock the turn loop and command services.");
        Require(actions.All(action => !action.Button.interactable),
            "One or more action controls remained enabled after battle completion.");
        Require(squadBootstrap.InitiativeOrder.Entries.Count == 1 &&
                occupancy.OccupiedCellCount == 1,
            "Defeated squad was not released by the established initiative/occupancy rule.");
        Require(resultPanel != null && resultPanel.IsVisible &&
                resultPanel.ContinueButton != null &&
                resultPanel.ContinueButton.interactable &&
                resultPanel.SaveStatus == "Autosave complete",
            "Battle result ModalLayer panel or its successful save state is unavailable.");

        string savePath = GetSmokeSavePath();
        Require(File.Exists(savePath) && new FileInfo(savePath).Length > 0,
            $"Battle autosave was not written through SaveService at '{savePath}'.");
        SessionState.SetString(ExpectedBattleIdKey, completion.Outcome.battleId);
        SessionState.SetString(ExpectedEncounterIdKey, completion.Outcome.encounterId);
        SessionState.SetInt(PhaseKey, 4);
        resultPanel.ContinueButton.onClick.Invoke();
        Require(completion.State == BattleCompletionState.Transitioning &&
                completion.TransitionRequestCount == 1,
            "Production Continue did not request exactly one return transition.");
    }

    private static bool EndPlayerTurnWhenActionPointsAreInsufficient(
        int requiredActionPoints,
        BattleActionControlView endTurnAction,
        MovementCommandController movementCommands,
        BattleTurnController turns,
        SquadBattleController enemy,
        BattleCommandModeController commandMode)
    {
        SquadBattleController active = turns.ActiveSquad;
        if (active != null &&
            active.Runtime.State.currentActionPoints >= requiredActionPoints)
        {
            return false;
        }

        Require(active != null && active.Side == BattleSide.Player,
            "Only the active Player squad may cycle its turn for an AP-gated smoke command.");
        Require(endTurnAction.Button.interactable,
            "End Turn is unavailable while preparing the next production command.");
        int expectedEndTurns = movementCommands.EndTurnCommandCount + 1;
        PrepareAITurnSmoke(active, enemy);
        endTurnAction.Button.onClick.Invoke();
        Require(movementCommands.EndTurnCommandCount == expectedEndTurns &&
                turns.ActiveSquad == enemy,
            "Production End Turn did not hand control to the AI squad for AP recovery.");
        Require(commandMode.ActiveMode == BattleCommandMode.None,
            "End Turn did not clear the command mode while preparing an ability.");
        SessionState.SetInt(
            ExpectedMovementCountKey,
            movementCommands.MovementCommandCount);
        SessionState.SetInt(ExpectedEndTurnCountKey, expectedEndTurns);
        SessionState.SetInt(PhaseKey, 2);
        return true;
    }

    private static void ExecuteProductionDamageAbility(
        BattleHUDController battleHud,
        SquadBattleController player,
        SquadBattleController enemy,
        BattleAttackService attackService,
        BattleAbilityService abilityService,
        AbilityCommandController abilityCommands,
        MovementCommandController movementCommands,
        BattleCommandModeController commandMode,
        BattleActionControlView abilityAction,
        AbilityDefinition definition,
        SquadDamageDistribution expectedDistribution)
    {
        Require(definition != null && definition.AttackEffect != null &&
                definition.DamageDistribution == expectedDistribution,
            $"{definition?.DisplayName ?? "Ability"} does not expose the required existing resolver distribution.");
        Require(abilityAction != null && abilityAction.Button.interactable,
            $"{definition.DisplayName} production control is unavailable.");

        attackService.SetRandomSourceForTests(new SmokeBattleRandomSource(0f, 0.99f));
        int initialAbilityCount = abilityCommands.AbilityCommandCount;
        int initialActionPoints = player.Runtime.State.currentActionPoints;
        int initialEnemyHealth = enemy.Runtime.State.CurrentSquadHP;
        int initialLivingWarriors = enemy.Runtime.State.warriors.Count(
            warrior => warrior != null && !warrior.defeated);

        abilityAction.Button.onClick.Invoke();
        Require(abilityCommands.IsAbilityTargeting &&
                abilityCommands.SelectedAbility == definition &&
                commandMode.ActiveMode == BattleCommandMode.Ability &&
                abilityAction.IsSelectedAction &&
                !movementCommands.IsMovementTargeting,
            $"{definition.DisplayName} did not enter the exclusive production Ability mode.");
        Require(enemy.AttackTarget.TargetingView.State ==
                SquadAttackTargetVisualState.Available &&
                player.AttackTarget.TargetingView.State == SquadAttackTargetVisualState.None,
            $"{definition.DisplayName} did not highlight only its valid enemy squad target.");

        BattleAbilityPreview preview = abilityCommands.TryHoverTarget(enemy.AttackTarget);
        Require(preview.IsValid && preview.AbilityId == definition.StableId &&
                preview.TargetId == enemy.SquadId &&
                preview.ActionPointCost == definition.ActionPointCost &&
                preview.AttackPreview.IsValid &&
                preview.AttackPreview.PredictedDamage > 0,
            $"{definition.DisplayName} production hover did not expose a valid read-only preview.");
        Require(battleHud.AbilityDetails.HasDetails &&
                battleHud.AbilityDetails.CurrentAbilityPreview.AbilityId ==
                definition.StableId &&
                battleHud.AbilityDetails.DisplayedPortrait != null,
            $"Ability Info did not render {definition.DisplayName} with target presentation.");

        enemy.AttackTarget.RequestConfirm();
        BattleAbilityResult result = abilityCommands.LastResult;
        BattleAbilityRuntimeState state = abilityService.GetRuntimeState(
            player.SquadId,
            definition.StableId);
        Require(result != null && result.Succeeded && result.Hit && !result.Critical &&
                result.AbilityId == definition.StableId && result.Damage > 0,
            $"{definition.DisplayName} did not resolve through the injected deterministic attack roll.");
        Require(abilityCommands.AbilityCommandCount == initialAbilityCount + 1 &&
                player.Runtime.State.currentActionPoints ==
                initialActionPoints - definition.ActionPointCost,
            $"{definition.DisplayName} did not commit one command and one AP cost.");
        Require(enemy.Runtime.State.CurrentSquadHP == initialEnemyHealth - result.Damage,
            $"{definition.DisplayName} bypassed or disagreed with the existing damage resolver.");
        int finalLivingWarriors = enemy.Runtime.State.warriors.Count(
            warrior => warrior != null && !warrior.defeated);
        Require(finalLivingWarriors <= initialLivingWarriors &&
                enemy.FormationView.ActiveWarriorModelCount == finalLivingWarriors,
            $"{definition.DisplayName} did not synchronize runtime casualties and formation.");
        Require(state != null && state.remainingCooldown == definition.CooldownRounds &&
                state.usesThisBattle == 1,
            $"{definition.DisplayName} cooldown or usage count was not applied exactly once.");
        Require(commandMode.ActiveMode == BattleCommandMode.None &&
                !abilityCommands.IsAbilityTargeting &&
                !movementCommands.IsMovementTargeting &&
                !abilityService.IsExecuting && !attackService.IsExecuting &&
                !string.IsNullOrWhiteSpace(battleHud.AbilityDetails.LastResultFeedback),
            $"{definition.DisplayName} left command locks, targeting, or feedback unresolved.");
        Require(!enemy.Runtime.State.IsDefeated,
            $"{definition.DisplayName} unexpectedly completed the enemy squad before the smoke sequence finished.");
    }

    private static void PrepareAITurnSmoke(
        SquadBattleController player,
        SquadBattleController enemy)
    {
        EnemyTacticalAIController enemyAI =
            UnityEngine.Object.FindAnyObjectByType<EnemyTacticalAIController>();
        BattleAttackService attacks =
            UnityEngine.Object.FindAnyObjectByType<BattleAttackService>();
        Require(enemyAI != null && attacks != null,
            "Production AI or attack service is missing before the AI turn.");
        SessionState.SetInt(
            PlayerHPBeforeAITurnKey,
            player.Runtime.State.CurrentSquadHP);
        SessionState.SetInt(
            EnemyCellXBeforeAITurnKey,
            enemy.GridAnchor.CurrentCell.x);
        SessionState.SetInt(
            EnemyCellYBeforeAITurnKey,
            enemy.GridAnchor.CurrentCell.y);
        SessionState.SetInt(
            AICommittedActionsBeforeKey,
            enemyAI.MovementActionCount + enemyAI.BasicAttackActionCount +
            enemyAI.AbilityActionCount);
        attacks.SetRandomSourceForTests(new SmokeBattleRandomSource(0f, 0.99f));
    }

    private static void ConfigureSmokeStorage()
    {
        if (SessionState.GetBool(StorageConfiguredKey, false))
            return;

        SaveSystemBehaviour[] saveSystems =
            UnityEngine.Object.FindObjectsByType<SaveSystemBehaviour>(
                FindObjectsInactive.Include);
        Require(saveSystems.Length == 1,
            "Raw Alpha must contain exactly one SaveSystemBehaviour for lifecycle smoke.");
        string savePath = GetSmokeSavePath();
        DeleteIfPresent(savePath);
        DeleteIfPresent(savePath + ".bak");
        DeleteIfPresent(savePath + ".tmp");
        Require(saveSystems[0].ConfigureStorageRootForTests(GetSmokeSaveRoot()),
            "The production save owner rejected the isolated lifecycle smoke storage root.");
        SessionState.SetBool(StorageConfiguredKey, true);
    }

    private static string GetSmokeSaveRoot()
    {
        DirectoryInfo project = Directory.GetParent(Application.dataPath);
        Require(project != null, "Could not resolve the Unity project root for smoke storage.");
        return Path.GetFullPath(Path.Combine(project.FullName, SmokeSaveRoot));
    }

    private static string GetSmokeSavePath() =>
        Path.Combine(GetSmokeSaveRoot(), "Saves", "autosave.json");

    private static void DeleteIfPresent(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    private static void CompleteOverworldReturn()
    {
        double startTime = double.Parse(
            SessionState.GetString(StartTimeKey, "0"),
            System.Globalization.CultureInfo.InvariantCulture);
        if (SceneManager.GetActiveScene().name != "first_try")
        {
            if (EditorApplication.timeSinceStartup - startTime > 90)
            {
                throw new InvalidOperationException(
                    $"Timed out waiting for first_try after Continue. " +
                    $"ActiveScene={SceneManager.GetActiveScene().name}.");
            }
            return;
        }

        OverworldBattleResultReceiver receiver =
            UnityEngine.Object.FindAnyObjectByType<OverworldBattleResultReceiver>();
        SaveSystemBehaviour[] saveSystems =
            UnityEngine.Object.FindObjectsByType<SaveSystemBehaviour>(
                FindObjectsInactive.Include);
        SquadSaveParticipant[] repositories =
            UnityEngine.Object.FindObjectsByType<SquadSaveParticipant>(
                FindObjectsInactive.Include);
        if (receiver == null || !receiver.HasConsumedResult ||
            PendingSaveLoadContext.HasData ||
            saveSystems.Length != 1 || saveSystems[0].IsBusy)
        {
            if (EditorApplication.timeSinceStartup - startTime > 90)
            {
                throw new InvalidOperationException(
                    "Timed out waiting for the overworld to restore and consume the saved result.");
            }
            return;
        }

        string expectedBattleId = SessionState.GetString(ExpectedBattleIdKey, string.Empty);
        string expectedEncounterId = SessionState.GetString(ExpectedEncounterIdKey, string.Empty);
        Require(receiver.ConsumeCount == 1 && receiver.LastOutcome != null &&
                receiver.LastOutcome.battleId == expectedBattleId &&
                receiver.LastOutcome.encounterId == expectedEncounterId &&
                receiver.LastOutcome.resultType == BattleResultType.Victory,
            "first_try did not consume the expected BattleOutcome exactly once.");
        Require(!BattleReturnContext.HasData && !BattleEncounterContext.HasEncounterData &&
                !BattleSetupContext.IsConfirmed,
            "A battle-only or return context remained stale after overworld restore.");
        Require(ResolvedEncounterRegistry.IsResolved(expectedEncounterId),
            "The victorious encounter was not marked resolved after return.");
        Require(repositories.Length == 1,
            "first_try must contain exactly one persistent squad repository.");

        SquadBattleResult playerResult = receiver.LastOutcome.participantResults.Single(
            participant => participant.side == BattleSide.Player);
        SquadData persistentPlayer = repositories[0].GetSquad(playerResult.squadId);
        Require(persistentPlayer != null &&
                persistentPlayer.Commander.id == playerResult.commanderId &&
                persistentPlayer.CommanderPortraitId == playerResult.portraitId &&
                repositories[0].HasAppliedBattle(expectedBattleId),
            "The returned persistent player squad does not match the applied battle result.");
        Require(new System.Collections.Generic.HashSet<string>(
                    persistentPlayer.Warriors.Select(warrior => warrior.id))
                .SetEquals(playerResult.survivingWarriorIds),
            "Persistent Warrior membership was not restored from stable survivor IDs.");

        GameSaveData currentData = saveSystems[0].CurrentData;
        Require(currentData != null && currentData.sceneName == "first_try" &&
                currentData.playerProgress.resolvedEncounterIds.Contains(expectedEncounterId),
            "The restored autosave is missing its return scene or resolved encounter.");
        SystemSaveData squadSection = currentData.systems.Single(
            section => section.key == repositories[0].SaveKey);
        SquadSavePayload savedSquads = JsonUtility.FromJson<SquadSavePayload>(squadSection.json);
        Require(savedSquads != null && savedSquads.activeBattles.Count == 0 &&
                savedSquads.appliedBattleIds.Contains(expectedBattleId),
            "Autosave persisted active battle runtime or omitted applied-result idempotency data.");
        Require(UnityEngine.Object.FindObjectsByType<EventSystem>(
                    FindObjectsInactive.Include).Length == 1,
            "first_try must contain exactly one EventSystem after return.");
        Require(File.Exists(GetSmokeSavePath()),
            "The isolated battle-result autosave disappeared during scene return.");

        Finish(true,
            "Raw Alpha 32x32 production startup, tactical camera, minimap click/drag/zoom, " +
            "event-driven markers, viewport, ten-second collapse/reopen, movement, " +
            "Rally, Power Strike, Sweeping Blow, " +
            "repeated physical attacks, casualties, " +
            "Commander defeat, idempotent completion, command lock, result ModalLayer, " +
            "autosave, Continue, first_try restore, persistent squad state, encounter " +
            "resolution, and one-shot return-context consumption passed.");
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

    private static void CaptureRuntimeRegressionError(
        string condition,
        string stackTrace,
        LogType type)
    {
        if (type != LogType.Exception &&
            !condition.Contains("MissingComponentException") &&
            !condition.Contains("MissingReferenceException") &&
            !condition.Contains("NullReferenceException"))
        {
            return;
        }

        if (string.IsNullOrEmpty(
                SessionState.GetString(RuntimeRegressionErrorKey, string.Empty)))
        {
            SessionState.SetString(
                RuntimeRegressionErrorKey,
                $"Runtime exception during production scene smoke: {condition}\n{stackTrace}");
        }
    }

    private static void RequireNoRuntimeRegressionErrors()
    {
        string error = SessionState.GetString(RuntimeRegressionErrorKey, string.Empty);
        Require(string.IsNullOrEmpty(error), error);
    }

    private static void Finish(bool passed, string message)
    {
        if (passed && !string.IsNullOrEmpty(
                SessionState.GetString(RuntimeRegressionErrorKey, string.Empty)))
        {
            passed = false;
            message = SessionState.GetString(RuntimeRegressionErrorKey, string.Empty);
        }
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
        SessionState.EraseBool(StorageConfiguredKey);
        SessionState.EraseBool(RallyUsedKey);
        SessionState.EraseBool(PowerStrikeUsedKey);
        SessionState.EraseBool(SweepingBlowUsedKey);
        SessionState.EraseString(StartTimeKey);
        SessionState.EraseString(ExpectedBattleIdKey);
        SessionState.EraseString(ExpectedEncounterIdKey);
        SessionState.EraseString(MinimapInactivityStartKey);
        SessionState.EraseString(RuntimeRegressionErrorKey);
        Application.logMessageReceived -= CaptureRuntimeRegressionError;
        SessionState.EraseInt(PhaseKey);
        SessionState.EraseInt(DestinationXKey);
        SessionState.EraseInt(DestinationYKey);
        SessionState.EraseInt(InitialActionPointsKey);
        SessionState.EraseInt(MovementCostKey);
        SessionState.EraseInt(ExpectedMovementCountKey);
        SessionState.EraseInt(ExpectedEndTurnCountKey);
        SessionState.EraseInt(PlayerHPBeforeAITurnKey);
        SessionState.EraseInt(EnemyCellXBeforeAITurnKey);
        SessionState.EraseInt(EnemyCellYBeforeAITurnKey);
        SessionState.EraseInt(AICommittedActionsBeforeKey);
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
        writer.WriteAttributeString("name", "RawAlphaBattleLifecyclePlayModeSmoke");
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
