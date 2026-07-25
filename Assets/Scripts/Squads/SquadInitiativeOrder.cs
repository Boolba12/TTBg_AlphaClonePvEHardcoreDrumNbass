using System.Collections.Generic;

public sealed class SquadInitiativeOrder
{
    private readonly List<SquadBattleController> entries = new List<SquadBattleController>();

    public IReadOnlyList<SquadBattleController> Entries => entries;

    public bool Register(SquadBattleController controller)
    {
        if (controller == null || !controller.IsInitialized || entries.Exists(
                entry => entry != null && entry.SquadId == controller.SquadId))
        {
            return false;
        }

        entries.Add(controller);
        entries.Sort((left, right) =>
            right.Runtime.Stats.Initiative.CompareTo(left.Runtime.Stats.Initiative));
        controller.Runtime.OnSquadDefeated += () => Remove(controller.SquadId);
        return true;
    }

    public void Remove(string squadId)
    {
        entries.RemoveAll(entry => entry == null || entry.SquadId == squadId);
    }
}
