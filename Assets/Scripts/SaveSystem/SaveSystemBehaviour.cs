using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;

public sealed class SaveSystemBehaviour : MonoBehaviour
{
    [SerializeField] private string defaultSlotId = "slot-0";
    [SerializeField] private List<MonoBehaviour> participants = new List<MonoBehaviour>();

    private SaveService service;
    private GameSaveData currentData;
    private bool loadInProgress;

    public bool IsBusy => loadInProgress || (service != null && service.IsBusy);
    public string SaveDirectory => System.IO.Path.Combine(Application.persistentDataPath, "Saves");
    public GameSaveData CurrentData => currentData;
    public SaveOperationResult LastOperationResult { get; private set; }
    public bool StartupRestoreCompleted { get; private set; }

    private void Awake()
    {
        service = new SaveService(new JsonSaveFileStorage());
        RegisterConfiguredParticipants();
    }

    public void ConfigureParticipants(IEnumerable<MonoBehaviour> configuredParticipants)
    {
        participants = configuredParticipants == null
            ? new List<MonoBehaviour>()
            : new List<MonoBehaviour>(configuredParticipants);
    }

#if UNITY_EDITOR
    public bool ConfigureStorageRootForTests(string rootPath)
    {
        if (IsBusy || string.IsNullOrWhiteSpace(rootPath))
            return false;
        service = new SaveService(new JsonSaveFileStorage(rootPath));
        currentData = null;
        LastOperationResult = default;
        RegisterConfiguredParticipants();
        return true;
    }
#endif

    private IEnumerator Start()
    {
        if (!PendingSaveLoadContext.HasData)
        {
            StartupRestoreCompleted = true;
            yield break;
        }

        yield return null;
        currentData = PendingSaveLoadContext.Take();
        Report(service.Restore(currentData), "load");
        StartupRestoreCompleted = true;
    }

    public void NewGame()
    {
        PendingSaveLoadContext.Clear();
        currentData = service.CreateNew(SceneManager.GetActiveScene().name, Time.realtimeSinceStartupAsDouble);
    }

    public void SaveGame()
    {
        SaveGame(defaultSlotId);
    }

    public void SaveGame(string slotId)
    {
        EnsureCurrentData();
        currentData.sceneName = SceneManager.GetActiveScene().name;
        currentData.totalPlayTimeSeconds = Time.realtimeSinceStartupAsDouble;
        LastOperationResult = service.Save(slotId, currentData);
        Report(LastOperationResult, "save");
    }

    public void Autosave()
    {
        EnsureCurrentData();
        currentData.sceneName = SceneManager.GetActiveScene().name;
        currentData.totalPlayTimeSeconds = Time.realtimeSinceStartupAsDouble;
        LastOperationResult = service.Autosave(currentData);
        Report(LastOperationResult, "autosave");
    }

    public SaveOperationResult AutosaveBattleResult(
        string returnSceneName,
        string resolvedEncounterId)
    {
        if (service == null)
            return SaveOperationResult.Fail("Save service is not initialized.");
        if (string.IsNullOrWhiteSpace(returnSceneName))
            return SaveOperationResult.Fail("Return scene name is missing.");

        EnsureCurrentData();
        currentData.sceneName = returnSceneName;
        currentData.totalPlayTimeSeconds = Time.realtimeSinceStartupAsDouble;
        currentData.playerProgress ??= new PlayerProgressData();
        if (BattleEncounterContext.HasEncounterData)
        {
            currentData.playerProgress.mapSeed = BattleEncounterContext.OverworldSeed;
            currentData.playerProgress.hasOverworldPositions = true;
            currentData.playerProgress.playerCell = new Int2Data(
                BattleEncounterContext.PlayerEncounterCell.x,
                BattleEncounterContext.PlayerEncounterCell.y);
            currentData.playerProgress.enemyCell = new Int2Data(
                BattleEncounterContext.EnemyEncounterCell.x,
                BattleEncounterContext.EnemyEncounterCell.y);
        }
        if (!string.IsNullOrWhiteSpace(resolvedEncounterId))
            ResolvedEncounterRegistry.MarkResolved(resolvedEncounterId);
        currentData.playerProgress.resolvedEncounterIds =
            new List<string>(ResolvedEncounterRegistry.EncounterIds);

        LastOperationResult = service.Autosave(
            currentData,
            new HashSet<string>(StringComparer.Ordinal) { "overworld" });
        Report(LastOperationResult, "battle-result autosave");
        return LastOperationResult;
    }

    public bool PrepareCurrentDataForSceneRestore(bool createIfMissing = false)
    {
        if (currentData == null && createIfMissing)
            EnsureCurrentData();
        if (currentData == null)
            return false;
        SaveOperationResult capture = service.CaptureForSceneTransition(currentData);
        if (!capture.Success)
        {
            LastOperationResult = capture;
            Debug.LogError($"SaveSystem: scene-transition capture failed. {capture.Error}");
            return false;
        }
        PendingSaveLoadContext.Set(currentData);
        return true;
    }

    public void LoadGame()
    {
        LoadGame(defaultSlotId);
    }

    public void LoadGame(string slotId)
    {
        if (!loadInProgress)
            StartCoroutine(LoadRoutine(slotId));
    }

    public bool HasSave()
    {
        return service.HasSave(defaultSlotId);
    }

    public void DeleteSave()
    {
        Report(service.Delete(defaultSlotId), "delete");
    }

    private IEnumerator LoadRoutine(string slotId)
    {
        loadInProgress = true;
        SaveReadResult readResult = service.Read(slotId);
        if (!readResult.Success)
        {
            Debug.LogWarning($"SaveSystem: load failed. {readResult.Error}");
            loadInProgress = false;
            yield break;
        }

        currentData = readResult.Data;
        if (readResult.RecoveredFromBackup)
            Debug.LogWarning("SaveSystem: the main save was invalid; data was recovered from its backup.");

        string activeScene = SceneManager.GetActiveScene().name;
        if (!string.IsNullOrWhiteSpace(currentData.sceneName) && currentData.sceneName != activeScene)
        {
            PendingSaveLoadContext.Set(currentData);
            AsyncOperation operation = SceneManager.LoadSceneAsync(currentData.sceneName, LoadSceneMode.Single);
            if (operation == null)
            {
                PendingSaveLoadContext.Clear();
                Debug.LogError($"SaveSystem: scene '{currentData.sceneName}' could not be loaded.");
            }
            loadInProgress = false;
            yield break;
        }

        yield return null;
        Report(service.Restore(currentData), "load");
        loadInProgress = false;
    }

    private void RegisterConfiguredParticipants()
    {
        foreach (MonoBehaviour participant in participants)
        {
            if (participant is ISaveable saveable)
                service.Register(saveable);
            else if (participant != null)
                Debug.LogWarning($"SaveSystem: '{participant.name}' does not implement ISaveable.", participant);
        }
    }

    private void EnsureCurrentData()
    {
        if (currentData == null)
            NewGame();
    }

    private static void Report(SaveOperationResult result, string operation)
    {
        if (!result.Success)
            Debug.LogWarning($"SaveSystem: {operation} failed. {result.Error}");
    }
}
