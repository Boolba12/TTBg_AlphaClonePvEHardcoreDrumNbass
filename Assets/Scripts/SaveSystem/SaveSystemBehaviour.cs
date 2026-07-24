using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SaveSystemBehaviour : MonoBehaviour
{
    [SerializeField] private string defaultSlotId = "slot-0";
    [SerializeField] private List<MonoBehaviour> participants = new List<MonoBehaviour>();

    private SaveService service;
    private GameSaveData currentData;
    private bool loadInProgress;

    public bool IsBusy => loadInProgress || (service != null && service.IsBusy);
    public string SaveDirectory => System.IO.Path.Combine(Application.persistentDataPath, "Saves");

    private void Awake()
    {
        service = new SaveService(new JsonSaveFileStorage());
        RegisterConfiguredParticipants();
    }

    private IEnumerator Start()
    {
        if (!PendingSaveLoadContext.HasData)
            yield break;

        yield return null;
        currentData = PendingSaveLoadContext.Take();
        Report(service.Restore(currentData), "load");
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
        Report(service.Save(slotId, currentData), "save");
    }

    public void Autosave()
    {
        EnsureCurrentData();
        currentData.sceneName = SceneManager.GetActiveScene().name;
        currentData.totalPlayTimeSeconds = Time.realtimeSinceStartupAsDouble;
        Report(service.Autosave(currentData), "autosave");
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
