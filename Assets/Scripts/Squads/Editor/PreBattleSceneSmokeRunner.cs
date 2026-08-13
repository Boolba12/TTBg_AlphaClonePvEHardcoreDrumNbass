#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public static class PreBattleSceneSmokeRunner
{
    private const string OverworldScenePath = "Assets/Scenes/first_try.unity";
    private const string BattleSceneName = "Raw_Alpha_BattleMode";
    private const string RequestPath =
        "Assets/Scripts/Squads/Editor/PreBattleSceneSmoke.run-request";
    private const string ResultPath = "Logs/PreBattleSceneSmokeResults_2026-08-13.xml";
    private const string SelectedSquadId = "smoke-persistent-beta";
    private const string StartedKey = "PreBattleSmoke.Started";
    private const string FinishedKey = "PreBattleSmoke.Finished";
    private const string PassedKey = "PreBattleSmoke.Passed";
    private const string PhaseKey = "PreBattleSmoke.Phase";
    private const string AttemptsKey = "PreBattleSmoke.Attempts";
    private const string StartTimeKey = "PreBattleSmoke.StartTime";
    private const string RosterSnapshotKey = "PreBattleSmoke.RosterSnapshot";
    private const string RuntimeErrorKey = "PreBattleSmoke.RuntimeError";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void PreparePersistentRoster()
    {
        if (!File.Exists(RequestPath))
            return;

        BattleSquadSelectionContext.Clear();
        BattleEncounterContext.Clear();
        BattleReturnContext.Clear();
        PendingSaveLoadContext.Clear();
        ResolvedEncounterRegistry.Clear();
        Application.logMessageReceived -= CaptureRuntimeError;
        Application.logMessageReceived += CaptureRuntimeError;

        CommanderPortraitDatabase portraits =
            AssetDatabase.LoadAssetAtPath<CommanderPortraitDatabase>(
                "Assets/Art/CommanderPortraits/CommanderPortraitDatabase.asset");
        string portraitId = portraits?.Entries.FirstOrDefault(entry =>
            entry != null && entry.Race == CommanderRace.Human && entry.Sprite != null)?.Id;
        SquadSavePayload payload = new SquadSavePayload
        {
            squads = new List<SquadData>
            {
                CreateSquad("smoke-persistent-alpha", portraitId, 1, 6),
                CreateSquad(SelectedSquadId, portraitId, 3, 14)
            }
        };
        GameSaveData data = new GameSaveData
        {
            saveId = "prebattle-smoke",
            sceneName = "first_try",
            systems = new List<SystemSaveData>
            {
                new SystemSaveData { key = "squads", json = JsonUtility.ToJson(payload) }
            }
        };
        PendingSaveLoadContext.Set(data);
    }

    [InitializeOnLoadMethod]
    private static void ContinueRequestedRun()
    {
        if (File.Exists(RequestPath))
            EditorApplication.delayCall += RegisterUpdate;
    }

    [MenuItem("Tools/Squads/Run Pre-Battle Production Smoke")]
    public static void RunFromMenu()
    {
        if (!File.Exists(RequestPath))
            File.WriteAllText(RequestPath, "Run Pre-Battle production-equivalent smoke.");
        SessionState.EraseBool(StartedKey);
        SessionState.EraseBool(FinishedKey);
        SessionState.EraseBool(PassedKey);
        SessionState.EraseString(RuntimeErrorKey);
        SessionState.EraseString(RosterSnapshotKey);
        SessionState.SetInt(PhaseKey, 0);
        SessionState.SetInt(AttemptsKey, 0);
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
                Cleanup();
            return;
        }
        if (!SessionState.GetBool(StartedKey, false))
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            Scene active = SceneManager.GetActiveScene();
            if (active.isDirty && active.path != OverworldScenePath)
            {
                Finish(false, "Another modified scene is active; smoke refused to discard it.");
                return;
            }
            if (active.path != OverworldScenePath)
                EditorSceneManager.OpenScene(OverworldScenePath, OpenSceneMode.Single);
            SessionState.SetBool(StartedKey, true);
            SessionState.SetString(StartTimeKey,
                EditorApplication.timeSinceStartup.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
            EditorApplication.isPlaying = true;
            return;
        }
        if (!EditorApplication.isPlaying)
            return;

        try
        {
            if (SceneManager.GetActiveScene().name == BattleSceneName)
                ValidateBattleScene();
            else
                DriveOverworldPreparation();
            CheckTimeout();
        }
        catch (Exception exception)
        {
            Finish(false, exception.Message);
        }
    }

    private static void DriveOverworldPreparation()
    {
        SquadSaveParticipant repository = UnityEngine.Object.FindAnyObjectByType<SquadSaveParticipant>();
        PlayerController player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        EnemyController enemy = UnityEngine.Object.FindAnyObjectByType<EnemyController>();
        TurnSystem turn = UnityEngine.Object.FindAnyObjectByType<TurnSystem>();
        PreBattlePreparationController preparation =
            UnityEngine.Object.FindAnyObjectByType<PreBattlePreparationController>();
        PreBattlePreparationView view =
            UnityEngine.Object.FindAnyObjectByType<PreBattlePreparationView>();
        if (repository == null || player == null || enemy == null || turn == null ||
            preparation == null || view == null || player.mapGenerator == null ||
            !player.mapGenerator.HasGeneratedData || repository.Squads.Count != 2)
        {
            return;
        }

        int phase = SessionState.GetInt(PhaseKey, 0);
        if (preparation.IsOpen)
        {
            Require(view.IsVisible, "Pre-Battle controller opened without its production view.");
            Require(preparation.Options.Count == 2, "Persistent roster did not render two squad options.");
            Require(UnityEngine.Object.FindObjectsByType<EventSystem>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1,
                "first_try does not contain exactly one EventSystem.");

            if (phase <= 1)
            {
                Require(!view.ConfirmButton.interactable,
                    "Confirm must be disabled before explicit squad selection.");
                string snapshot = repository.CaptureState();
                SessionState.SetString(RosterSnapshotKey, snapshot);
                view.CancelButton.onClick.Invoke();
                Require(!preparation.IsOpen && !view.IsVisible,
                    "Cancel did not close Pre-Battle preparation.");
                Require(!BattleEncounterContext.HasEncounterData &&
                        !BattleSquadSelectionContext.HasSelection,
                    "Cancel left stale battle contexts.");
                Require(repository.CaptureState() == snapshot,
                    "Cancel mutated the persistent squad roster.");
                SessionState.SetInt(PhaseKey, 2);
                SessionState.SetInt(AttemptsKey, 0);
                return;
            }

            PreBattleSquadCardView card = UnityEngine.Object
                .FindObjectsByType<PreBattleSquadCardView>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate.SquadId == SelectedSquadId);
            Require(card != null && card.gameObject.activeInHierarchy,
                "Selected persistent squad card is unavailable.");
            card.SelectButton.onClick.Invoke();
            Require(preparation.SelectedSquadId == SelectedSquadId &&
                    view.ConfirmButton.interactable,
                "Production card selection did not enable Confirm for the exact stable ID.");
            view.ConfirmButton.onClick.Invoke();
            SessionState.SetInt(PhaseKey, 4);
            return;
        }

        if (turn.IsEnemyTurnRunning || player.IsMovementInProgress ||
            turn.IsBattleLoadingTriggered)
        {
            return;
        }
        if (phase == 0 || phase == 2 || phase == 1 || phase == 3)
        {
            Require(CommitOneStepTowardEnemy(player, enemy),
                "Could not issue a production overworld path request.");
            SessionState.SetInt(PhaseKey, phase < 2 ? 1 : 3);
            SessionState.SetInt(AttemptsKey, SessionState.GetInt(AttemptsKey, 0) + 1);
            Require(SessionState.GetInt(AttemptsKey, 0) <= 20,
                "Encounter did not reach Pre-Battle preparation within 20 production turns.");
        }
    }

    private static bool CommitOneStepTowardEnemy(PlayerController player, EnemyController enemy)
    {
        Vector2Int origin = player.CurrentCell;
        Vector2Int[] directions =
        {
            Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left,
            new Vector2Int(1, 1), new Vector2Int(1, -1),
            new Vector2Int(-1, -1), new Vector2Int(-1, 1)
        };
        Vector2Int destination = default;
        int bestDistance = int.MaxValue;
        bool found = false;
        for (int i = 0; i < directions.Length; i++)
        {
            Vector2Int candidate = origin + directions[i];
            if (candidate == enemy.CurrentCell || candidate.x < 0 || candidate.y < 0 ||
                candidate.x >= player.mapGenerator.width ||
                candidate.y >= player.mapGenerator.height ||
                !player.mapGenerator.GetIsPlayable(candidate.x, candidate.y))
            {
                continue;
            }
            int distance = Mathf.Max(
                Mathf.Abs(candidate.x - enemy.CurrentCell.x),
                Mathf.Abs(candidate.y - enemy.CurrentCell.y));
            if (distance < bestDistance)
            {
                bestDistance = distance;
                destination = candidate;
                found = true;
            }
        }
        return found && player.TryRequestPathToCell(destination) &&
               player.TryRequestPathToCell(destination);
    }

    private static void ValidateBattleScene()
    {
        BattleMapBootstrap map = UnityEngine.Object.FindAnyObjectByType<BattleMapBootstrap>();
        SquadBattleBootstrap squads = UnityEngine.Object.FindAnyObjectByType<SquadBattleBootstrap>();
        SaveSystemBehaviour save = UnityEngine.Object.FindAnyObjectByType<SaveSystemBehaviour>();
        if (map == null || squads == null || save == null || !map.HasBootstrapped)
            return;

        Require(save.StartupRestoreCompleted, "Battle startup did not wait for persistent restore.");
        Require(map.mapGenerator.Width == 32 && map.mapGenerator.Height == 32,
            "Pre-Battle transition changed the 32x32 production map.");
        Require(map.UsedConfirmedPreBattleAutoConfirm && !map.UsedDevelopmentAutoConfirm,
            "Battle did not use the confirmed production Pre-Battle startup pathway.");
        Require(squads.SpawnedControllers.Count == 2,
            "Battle did not create exactly two squad controllers.");
        SquadBattleController player = squads.SpawnedControllers.Single(controller =>
            controller.Side == BattleSide.Player);
        Require(player.SquadId == SelectedSquadId,
            $"Battle spawned '{player.SquadId}' instead of selected '{SelectedSquadId}'.");
        Require(player.Runtime.Data.Id == SelectedSquadId,
            "Battle runtime is not backed by the selected persistent SquadData identity.");
        Require(!BattleSquadSelectionContext.HasSelection,
            "Successful battle bootstrap did not consume selection context.");
        Require(UnityEngine.Object.FindObjectsByType<EventSystem>(
            FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1,
            "Raw battle does not contain exactly one EventSystem.");
        RequireNoRuntimeErrors();
        Finish(true,
            "first_try encounter opened Pre-Battle UI; Cancel was pure; exact stable-ID " +
            "selection entered the 32x32 battle through persistent restore and existing bootstrap.");
    }

    private static SquadData CreateSquad(
        string id, string portraitId, int warriorCount, float initiative)
    {
        List<WarriorData> warriors = new List<WarriorData>();
        for (int i = 0; i < warriorCount; i++)
        {
            warriors.Add(new WarriorData
            {
                id = $"{id}-warrior-{i}", maxHP = 8, strength = 2, dexterity = 2
            });
        }
        return new SquadData(id, new CommanderData
        {
            id = $"{id}-commander",
            race = CommanderRace.Human,
            commanderPortraitId = portraitId,
            baseStats = new SquadBaseStats
            {
                hp = 18, actionPoints = 8, initiative = initiative,
                strength = 6, dexterity = 5, morale = 60, accuracy = 0.1f,
                criticalChance = 0.1f, criticalDamage = 1.5f
            }
        }, warriors);
    }

    private static void CheckTimeout()
    {
        double started = double.Parse(SessionState.GetString(StartTimeKey, "0"),
            System.Globalization.CultureInfo.InvariantCulture);
        if (EditorApplication.timeSinceStartup - started > 120)
        {
            Finish(false,
                $"Timed out in phase {SessionState.GetInt(PhaseKey, -1)}; " +
                $"scene={SceneManager.GetActiveScene().name}.");
        }
    }

    private static void CaptureRuntimeError(string condition, string stack, LogType type)
    {
        if (type != LogType.Exception && !condition.Contains("NullReferenceException") &&
            !condition.Contains("MissingReferenceException") &&
            !condition.Contains("MissingComponentException"))
        {
            return;
        }
        if (string.IsNullOrEmpty(SessionState.GetString(RuntimeErrorKey, string.Empty)))
            SessionState.SetString(RuntimeErrorKey, $"{condition}\n{stack}");
    }

    private static void RequireNoRuntimeErrors()
    {
        string error = SessionState.GetString(RuntimeErrorKey, string.Empty);
        Require(string.IsNullOrEmpty(error), error);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void Finish(bool passed, string message)
    {
        if (passed && !string.IsNullOrEmpty(SessionState.GetString(RuntimeErrorKey, string.Empty)))
        {
            passed = false;
            message = SessionState.GetString(RuntimeErrorKey, string.Empty);
        }
        WriteResult(passed, message);
        Debug.Log($"Pre-Battle scene smoke: {(passed ? "PASSED" : "FAILED")} - {message}");
        SessionState.SetBool(FinishedKey, true);
        SessionState.SetBool(PassedKey, passed);
        if (EditorApplication.isPlaying)
            EditorApplication.isPlaying = false;
    }

    private static void Cleanup()
    {
        bool passed = SessionState.GetBool(PassedKey, false);
        EditorApplication.update -= UpdateRun;
        AssetDatabase.DeleteAsset(RequestPath);
        Application.logMessageReceived -= CaptureRuntimeError;
        SessionState.EraseBool(StartedKey);
        SessionState.EraseBool(FinishedKey);
        SessionState.EraseBool(PassedKey);
        SessionState.EraseInt(PhaseKey);
        SessionState.EraseInt(AttemptsKey);
        SessionState.EraseString(StartTimeKey);
        SessionState.EraseString(RosterSnapshotKey);
        SessionState.EraseString(RuntimeErrorKey);
        if (Application.isBatchMode)
            EditorApplication.Exit(passed ? 0 : 1);
    }

    private static void WriteResult(bool passed, string message)
    {
        Directory.CreateDirectory("Logs");
        using XmlWriter writer = XmlWriter.Create(ResultPath, new XmlWriterSettings { Indent = true });
        writer.WriteStartDocument();
        writer.WriteStartElement("test-run");
        writer.WriteAttributeString("total", "1");
        writer.WriteAttributeString("passed", passed ? "1" : "0");
        writer.WriteAttributeString("failed", passed ? "0" : "1");
        writer.WriteAttributeString("result", passed ? "Passed" : "Failed");
        writer.WriteStartElement("test-case");
        writer.WriteAttributeString("name", "PreBattleProductionFlowSmoke");
        writer.WriteAttributeString("result", passed ? "Passed" : "Failed");
        writer.WriteElementString("message", message ?? string.Empty);
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }
}
#endif
