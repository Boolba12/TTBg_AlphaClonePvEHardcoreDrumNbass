using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class SaveService
{
    public const int CurrentFormatVersion = 1;

    private readonly JsonSaveFileStorage storage;
    private readonly Dictionary<string, ISaveable> saveables = new Dictionary<string, ISaveable>();
    private readonly Dictionary<int, ISaveMigration> migrations = new Dictionary<int, ISaveMigration>();
    private bool operationInProgress;

    public bool IsBusy => operationInProgress;

    public SaveService(JsonSaveFileStorage storage)
    {
        this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    public void Register(ISaveable saveable)
    {
        if (saveable == null || string.IsNullOrWhiteSpace(saveable.SaveKey))
            throw new ArgumentException("A saveable must provide a non-empty unique key.");
        if (saveables.ContainsKey(saveable.SaveKey))
            throw new InvalidOperationException($"Duplicate save key '{saveable.SaveKey}'.");
        saveables.Add(saveable.SaveKey, saveable);
    }

    public void RegisterMigration(ISaveMigration migration)
    {
        if (migration == null)
            throw new ArgumentNullException(nameof(migration));
        migrations[migration.SourceVersion] = migration;
    }

    public GameSaveData CreateNew(string sceneName, double totalPlayTimeSeconds = 0)
    {
        return new GameSaveData
        {
            formatVersion = CurrentFormatVersion,
            saveId = Guid.NewGuid().ToString("N"),
            lastSavedUtc = DateTime.UtcNow.ToString("O"),
            sceneName = sceneName ?? string.Empty,
            totalPlayTimeSeconds = Math.Max(0, totalPlayTimeSeconds)
        };
    }

    public bool HasSave(string slotId) => storage.Exists(slotId);
    public SaveOperationResult Delete(string slotId) => storage.Delete(slotId);
    public SaveOperationResult Autosave(GameSaveData data) => Save("autosave", data);

    public SaveOperationResult Save(string slotId, GameSaveData data)
    {
        if (operationInProgress)
            return SaveOperationResult.Fail("Another save operation is already running.");
        if (data == null)
            return SaveOperationResult.Fail("Save data is null.");

        operationInProgress = true;
        try
        {
            data.formatVersion = CurrentFormatVersion;
            data.lastSavedUtc = DateTime.UtcNow.ToString("O");
            data.systems.Clear();

            foreach (ISaveable saveable in saveables.Values)
            {
                if (saveable is ICoreSaveDataContributor contributor)
                    contributor.CaptureCoreData(data);

                data.systems.Add(new SystemSaveData
                {
                    key = saveable.SaveKey,
                    json = saveable.CaptureState()
                });
            }

            return storage.Write(slotId, data);
        }
        catch (Exception exception)
        {
            return SaveOperationResult.Fail($"Save capture failed: {exception.Message}");
        }
        finally
        {
            operationInProgress = false;
        }
    }

    public SaveReadResult Read(string slotId)
    {
        SaveReadResult result = storage.Read(slotId);
        if (!result.Success)
            return result;

        GameSaveData data = result.Data;
        try
        {
            while (data.formatVersion < CurrentFormatVersion)
            {
                if (!migrations.TryGetValue(data.formatVersion, out ISaveMigration migration))
                    return SaveReadResult.Fail($"No migration exists for save version {data.formatVersion}.");
                data = migration.Migrate(data);
            }

            if (data.formatVersion > CurrentFormatVersion)
                return SaveReadResult.Fail($"Save version {data.formatVersion} is newer than supported version {CurrentFormatVersion}.");

            return SaveReadResult.Ok(data, result.RecoveredFromBackup);
        }
        catch (Exception exception)
        {
            return SaveReadResult.Fail($"Save migration failed: {exception.Message}");
        }
    }

    public SaveOperationResult Restore(GameSaveData data)
    {
        if (data == null)
            return SaveOperationResult.Fail("Save data is null.");

        try
        {
            HashSet<string> restoredKeys = new HashSet<string>();

            foreach (SystemSaveData entry in data.systems)
            {
                if (entry != null && saveables.TryGetValue(entry.key, out ISaveable saveable))
                {
                    saveable.RestoreState(entry.json);
                    restoredKeys.Add(entry.key);
                }
            }

            foreach (ISaveable saveable in saveables.Values)
            {
                if (!restoredKeys.Contains(saveable.SaveKey) && saveable is ICoreSaveDataContributor contributor)
                    contributor.RestoreCoreData(data);
            }
            return SaveOperationResult.Ok();
        }
        catch (Exception exception)
        {
            return SaveOperationResult.Fail($"Save restore failed: {exception.Message}");
        }
    }
}
