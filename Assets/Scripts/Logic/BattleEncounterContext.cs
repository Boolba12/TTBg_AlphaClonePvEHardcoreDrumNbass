using UnityEngine;

public enum EncounterInitiator
{
    None,
    Player,
    Enemy
}

public static class BattleEncounterContext
{
    public static bool HasEncounterData { get; private set; }
    public static int OverworldSeed { get; private set; }
    public static Vector2Int PlayerEncounterCell { get; private set; }
    public static Vector2Int EnemyEncounterCell { get; private set; }
    public static BiomeType PlayerBiome { get; private set; }
    public static BiomeType EnemyBiome { get; private set; }
    public static EncounterInitiator Initiator { get; private set; }
    public static int PlayerInitiativeBonus { get; private set; }
    public static int EnemyInitiativeBonus { get; private set; }

    public static void SetEncounterData(
        int overworldSeed,
        Vector2Int playerCell,
        Vector2Int enemyCell,
        BiomeType playerBiome,
        BiomeType enemyBiome,
        EncounterInitiator initiator,
        int initiativeBonus)
    {
        OverworldSeed = overworldSeed;
        PlayerEncounterCell = playerCell;
        EnemyEncounterCell = enemyCell;
        PlayerBiome = playerBiome;
        EnemyBiome = enemyBiome;
        Initiator = initiator;
        PlayerInitiativeBonus = initiator == EncounterInitiator.Player ? initiativeBonus : 0;
        EnemyInitiativeBonus = initiator == EncounterInitiator.Enemy ? initiativeBonus : 0;
        HasEncounterData = true;
    }

    public static void Clear()
    {
        HasEncounterData = false;
        Initiator = EncounterInitiator.None;
        PlayerInitiativeBonus = 0;
        EnemyInitiativeBonus = 0;
    }

    public static int CreateBattleSeed(int fallbackSeed = 0)
    {
        if (!HasEncounterData)
            return fallbackSeed;

        unchecked
        {
            int hash = 17;
            hash = hash * 31 + OverworldSeed;
            hash = hash * 31 + PlayerEncounterCell.x;
            hash = hash * 31 + PlayerEncounterCell.y;
            hash = hash * 31 + EnemyEncounterCell.x;
            hash = hash * 31 + EnemyEncounterCell.y;
            hash = hash * 31 + (int)PlayerBiome;
            hash = hash * 31 + (int)EnemyBiome;
            return hash;
        }
    }
}
