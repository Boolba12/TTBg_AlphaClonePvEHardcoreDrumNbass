using System.Collections.Generic;

public enum BattleSquadSelectionKind
{
    None,
    DirectData,
    PersistentEncounter
}

public static class BattleSquadSelectionContext
{
    private static readonly List<SquadData> PlayerSelection = new List<SquadData>();
    private static readonly List<SquadData> EnemySelection = new List<SquadData>();
    private static readonly List<string> PlayerSelectionIds = new List<string>();
    private static readonly List<string> EnemySelectionIds = new List<string>();

    public static IReadOnlyList<SquadData> PlayerSquads => PlayerSelection;
    public static IReadOnlyList<SquadData> EnemySquads => EnemySelection;
    public static IReadOnlyList<string> PlayerSquadIds => PlayerSelectionIds;
    public static IReadOnlyList<string> EnemySquadIds => EnemySelectionIds;
    public static BattleSquadSelectionKind Kind { get; private set; }
    public static string EncounterId { get; private set; }
    public static bool AllowConfiguredEncounterEnemy { get; private set; }
    public static bool HasSelection => PlayerSelectionIds.Count > 0 || EnemySelectionIds.Count > 0;

    public static void SetSelection(
        IEnumerable<SquadData> playerSquads,
        IEnumerable<SquadData> enemySquads)
    {
        Clear();
        AddValidReferences(PlayerSelection, PlayerSelectionIds, playerSquads);
        AddValidReferences(EnemySelection, EnemySelectionIds, enemySquads);
        Kind = HasSelection ? BattleSquadSelectionKind.DirectData : BattleSquadSelectionKind.None;
    }

    public static bool SetPersistentEncounterSelection(
        string playerSquadId,
        string encounterId,
        bool allowConfiguredEncounterEnemy)
    {
        Clear();
        if (string.IsNullOrWhiteSpace(playerSquadId) || string.IsNullOrWhiteSpace(encounterId))
            return false;

        PlayerSelectionIds.Add(playerSquadId);
        EncounterId = encounterId;
        AllowConfiguredEncounterEnemy = allowConfiguredEncounterEnemy;
        Kind = BattleSquadSelectionKind.PersistentEncounter;
        return true;
    }

    public static void Clear()
    {
        PlayerSelection.Clear();
        EnemySelection.Clear();
        PlayerSelectionIds.Clear();
        EnemySelectionIds.Clear();
        Kind = BattleSquadSelectionKind.None;
        EncounterId = null;
        AllowConfiguredEncounterEnemy = false;
    }

    public static bool Consume()
    {
        bool hadSelection = HasSelection;
        Clear();
        return hadSelection;
    }

    private static void AddValidReferences(
        List<SquadData> target,
        List<string> targetIds,
        IEnumerable<SquadData> source)
    {
        if (source == null)
            return;

        HashSet<string> ids = new HashSet<string>();
        foreach (SquadData squad in source)
        {
            if (squad != null && !string.IsNullOrWhiteSpace(squad.Id) && ids.Add(squad.Id))
            {
                target.Add(squad);
                targetIds.Add(squad.Id);
            }
        }
    }
}
