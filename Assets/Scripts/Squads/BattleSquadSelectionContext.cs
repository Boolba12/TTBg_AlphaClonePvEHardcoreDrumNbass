using System.Collections.Generic;

public static class BattleSquadSelectionContext
{
    private static readonly List<SquadData> PlayerSelection = new List<SquadData>();
    private static readonly List<SquadData> EnemySelection = new List<SquadData>();

    public static IReadOnlyList<SquadData> PlayerSquads => PlayerSelection;
    public static IReadOnlyList<SquadData> EnemySquads => EnemySelection;
    public static bool HasSelection => PlayerSelection.Count > 0 || EnemySelection.Count > 0;

    public static void SetSelection(
        IEnumerable<SquadData> playerSquads,
        IEnumerable<SquadData> enemySquads)
    {
        PlayerSelection.Clear();
        EnemySelection.Clear();
        AddValidReferences(PlayerSelection, playerSquads);
        AddValidReferences(EnemySelection, enemySquads);
    }

    public static void Clear()
    {
        PlayerSelection.Clear();
        EnemySelection.Clear();
    }

    private static void AddValidReferences(List<SquadData> target, IEnumerable<SquadData> source)
    {
        if (source == null)
            return;

        HashSet<string> ids = new HashSet<string>();
        foreach (SquadData squad in source)
        {
            if (squad != null && !string.IsNullOrWhiteSpace(squad.Id) && ids.Add(squad.Id))
                target.Add(squad);
        }
    }
}
