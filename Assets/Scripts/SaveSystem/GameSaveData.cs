using System;
using System.Collections.Generic;

[Serializable]
public sealed class GameSaveData
{
    public int formatVersion = SaveService.CurrentFormatVersion;
    public string saveId;
    public string lastSavedUtc;
    public string sceneName;
    public double totalPlayTimeSeconds;
    public PlayerProgressData playerProgress = new PlayerProgressData();
    public List<SystemSaveData> systems = new List<SystemSaveData>();
}

[Serializable]
public sealed class PlayerProgressData
{
    public int mapSeed;
    public bool hasOverworldPositions;
    public Int2Data playerCell = new Int2Data();
    public Int2Data enemyCell = new Int2Data();
}

[Serializable]
public sealed class Int2Data
{
    public int x;
    public int y;

    public Int2Data()
    {
    }

    public Int2Data(int x, int y)
    {
        this.x = x;
        this.y = y;
    }
}

[Serializable]
public sealed class SystemSaveData
{
    public string key;
    public string json;
}
