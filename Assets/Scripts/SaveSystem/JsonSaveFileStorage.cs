using System;
using System.IO;
using System.Text;
using UnityEngine;

public sealed class JsonSaveFileStorage
{
    private const string SaveFolderName = "Saves";
    private const string SaveExtension = ".json";
    private const string BackupExtension = ".bak";
    private const string TemporaryExtension = ".tmp";

    private readonly string directoryPath;

    public JsonSaveFileStorage(string rootPath = null)
    {
        directoryPath = Path.Combine(rootPath ?? Application.persistentDataPath, SaveFolderName);
    }

    public string GetSavePath(string slotId) => Path.Combine(directoryPath, SanitizeSlotId(slotId) + SaveExtension);
    public string GetBackupPath(string slotId) => GetSavePath(slotId) + BackupExtension;
    public bool Exists(string slotId) => File.Exists(GetSavePath(slotId)) || File.Exists(GetBackupPath(slotId));

    public SaveOperationResult Write(string slotId, GameSaveData data)
    {
        string mainPath = GetSavePath(slotId);
        string backupPath = GetBackupPath(slotId);
        string temporaryPath = mainPath + TemporaryExtension;

        try
        {
            Directory.CreateDirectory(directoryPath);
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));

            if (File.Exists(mainPath))
            {
                File.Replace(temporaryPath, mainPath, backupPath, true);
            }
            else
            {
                File.Move(temporaryPath, mainPath);
            }

            return SaveOperationResult.Ok();
        }
        catch (Exception exception)
        {
            TryDelete(temporaryPath);
            return SaveOperationResult.Fail($"Could not write save '{mainPath}': {exception.Message}");
        }
    }

    public SaveReadResult Read(string slotId)
    {
        string mainPath = GetSavePath(slotId);
        SaveReadResult mainResult = TryReadFile(mainPath, false);
        if (mainResult.Success)
            return mainResult;

        string backupPath = GetBackupPath(slotId);
        SaveReadResult backupResult = TryReadFile(backupPath, true);
        if (backupResult.Success)
            return backupResult;

        return SaveReadResult.Fail($"Save is unavailable. Main: {mainResult.Error} Backup: {backupResult.Error}");
    }

    public SaveOperationResult Delete(string slotId)
    {
        try
        {
            TryDelete(GetSavePath(slotId));
            TryDelete(GetBackupPath(slotId));
            TryDelete(GetSavePath(slotId) + TemporaryExtension);
            return SaveOperationResult.Ok();
        }
        catch (Exception exception)
        {
            return SaveOperationResult.Fail($"Could not delete save: {exception.Message}");
        }
    }

    private static SaveReadResult TryReadFile(string path, bool recovered)
    {
        try
        {
            if (!File.Exists(path))
                return SaveReadResult.Fail($"'{path}' does not exist.");

            string json = File.ReadAllText(path, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json))
                return SaveReadResult.Fail($"'{path}' is empty.");

            GameSaveData data = JsonUtility.FromJson<GameSaveData>(json);
            if (data == null || string.IsNullOrWhiteSpace(data.saveId))
                return SaveReadResult.Fail($"'{path}' contains invalid save data.");

            data.playerProgress ??= new PlayerProgressData();
            data.systems ??= new System.Collections.Generic.List<SystemSaveData>();
            return SaveReadResult.Ok(data, recovered);
        }
        catch (Exception exception)
        {
            return SaveReadResult.Fail($"Could not read '{path}': {exception.Message}");
        }
    }

    private static string SanitizeSlotId(string slotId)
    {
        if (string.IsNullOrWhiteSpace(slotId))
            return "autosave";

        foreach (char invalid in Path.GetInvalidFileNameChars())
            slotId = slotId.Replace(invalid, '_');

        return slotId.Trim();
    }

    private static void TryDelete(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }
}
