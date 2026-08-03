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
        if (mapBootstrap != null &&
            mapBootstrap.HasBootstrapped &&
            battleHud != null &&
            battleHud.HasBoundPlayer)
        {
            try
            {
                ValidateScene(mapBootstrap, battleHud);
                Finish(true, "Raw Alpha squad scene and Battle HUD smoke test passed.");
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
        if (EditorApplication.timeSinceStartup - startTime > 15)
        {
            Finish(
                false,
                $"Timed out waiting for map generation and squad bootstrap. " +
                $"MapBootstrapFound={mapBootstrap != null}, " +
                $"BattleHudFound={battleHud != null}, " +
                $"HudBound={battleHud != null && battleHud.HasBoundPlayer}, " +
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
        Require(
            actionBar.GetComponentsInChildren<BattleActionControlView>(true).Length == 6,
            "Bottom action bar does not expose six disabled visual action controls.");
        foreach (BattleActionControlView action in
                 actionBar.GetComponentsInChildren<BattleActionControlView>(true))
        {
            Require(
                action.Button != null &&
                !action.Button.interactable &&
                action.Button.targetGraphic != null &&
                action.Button.targetGraphic.raycastTarget &&
                action.DisplayedIcon != null,
                "A disabled action control is missing its hit area or icon placeholder.");
        }

        int initialHealth = player.Runtime.State.CurrentSquadHP;
        player.Runtime.ApplyDamage(1, SquadDamageDistribution.SingleTarget);
        Require(
            statusView.CurrentModel.CurrentHealth == player.Runtime.State.CurrentSquadHP &&
            statusView.CurrentModel.CurrentHealth < initialHealth,
            "HP UI did not update through SquadBattleRuntime.ApplyDamage.");

        int rendersBeforeActionPointChange = statusView.RenderCount;
        int initialActionPoints = player.Runtime.State.currentActionPoints;
        Require(
            initialActionPoints > 0 && player.Runtime.TrySpendActionPoints(1),
            "SquadBattleRuntime.TrySpendActionPoints could not exercise the HUD binding.");
        Require(
            statusView.CurrentModel.CurrentActionPoints == initialActionPoints - 1,
            "AP UI did not update through SquadBattleRuntime.TrySpendActionPoints.");
        Require(
            statusView.RenderCount == rendersBeforeActionPointChange + 1,
            "One AP runtime event produced duplicate HUD refreshes.");

        Require(
            battleHud.TryBindFromProductionState() &&
            battleHud.SuccessfulBindingCount == 1,
            "Repeated HUD binding created a duplicate production subscription.");
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
