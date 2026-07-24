public readonly struct SaveOperationResult
{
    public bool Success { get; }
    public string Error { get; }

    private SaveOperationResult(bool success, string error)
    {
        Success = success;
        Error = error;
    }

    public static SaveOperationResult Ok() => new SaveOperationResult(true, null);
    public static SaveOperationResult Fail(string error) => new SaveOperationResult(false, error);
}

public readonly struct SaveReadResult
{
    public bool Success { get; }
    public GameSaveData Data { get; }
    public string Error { get; }
    public bool RecoveredFromBackup { get; }

    private SaveReadResult(bool success, GameSaveData data, string error, bool recoveredFromBackup)
    {
        Success = success;
        Data = data;
        Error = error;
        RecoveredFromBackup = recoveredFromBackup;
    }

    public static SaveReadResult Ok(GameSaveData data, bool recovered = false) =>
        new SaveReadResult(true, data, null, recovered);

    public static SaveReadResult Fail(string error) =>
        new SaveReadResult(false, null, error, false);
}
