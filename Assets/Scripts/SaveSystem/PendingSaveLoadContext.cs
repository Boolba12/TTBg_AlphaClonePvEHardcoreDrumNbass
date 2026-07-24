public static class PendingSaveLoadContext
{
    public static GameSaveData Data { get; private set; }
    public static bool HasData => Data != null;

    public static void Set(GameSaveData data)
    {
        Data = data;
    }

    public static GameSaveData Take()
    {
        GameSaveData data = Data;
        Data = null;
        return data;
    }

    public static void Clear()
    {
        Data = null;
    }
}
