public interface ISaveable
{
    string SaveKey { get; }
    string CaptureState();
    void RestoreState(string json);
}

public interface ISaveMigration
{
    int SourceVersion { get; }
    GameSaveData Migrate(GameSaveData data);
}

public interface ICoreSaveDataContributor
{
    void CaptureCoreData(GameSaveData data);
    void RestoreCoreData(GameSaveData data);
}
