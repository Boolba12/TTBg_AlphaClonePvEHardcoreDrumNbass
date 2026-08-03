using System.Collections.Generic;
using System;

public sealed class SquadInitiativeOrder
{
    private readonly List<SquadBattleController> entries = new List<SquadBattleController>();
    private readonly Dictionary<string, Action> defeatHandlers =
        new Dictionary<string, Action>();

    public IReadOnlyList<SquadBattleController> Entries => entries;

    public bool Register(SquadBattleController controller)
    {
        if (controller == null || !controller.IsInitialized || !controller.CanAct || entries.Exists(
                entry => entry != null && entry.SquadId == controller.SquadId))
        {
            return false;
        }

        entries.Add(controller);
        Resort();
        string squadId = controller.SquadId;
        Action handler = () => Remove(squadId);
        defeatHandlers.Add(squadId, handler);
        controller.Runtime.OnSquadDefeated += handler;
        return true;
    }

    /// <summary>
    /// Stable battle ordering: initiative descending, assigned registration sequence
    /// ascending, then ordinal squad ID as the final deterministic fallback.
    /// </summary>
    public void Resort()
    {
        entries.Sort(CompareControllers);
    }

    public void Remove(string squadId)
    {
        SquadBattleController controller = entries.Find(
            entry => entry != null && entry.SquadId == squadId);
        if (controller?.Runtime != null &&
            defeatHandlers.TryGetValue(squadId, out Action handler))
        {
            controller.Runtime.OnSquadDefeated -= handler;
        }

        defeatHandlers.Remove(squadId);
        entries.RemoveAll(entry => entry == null || entry.SquadId == squadId);
    }

    public void Clear()
    {
        string[] squadIds = new string[defeatHandlers.Keys.Count];
        defeatHandlers.Keys.CopyTo(squadIds, 0);
        foreach (string squadId in squadIds)
            Remove(squadId);
        entries.Clear();
    }

    private static int CompareControllers(
        SquadBattleController left,
        SquadBattleController right)
    {
        int initiativeComparison =
            right.Runtime.Stats.Initiative.CompareTo(left.Runtime.Stats.Initiative);
        if (initiativeComparison != 0)
            return initiativeComparison;

        int leftSequence = left.HasBattleContext
            ? left.RegistrationSequence
            : int.MaxValue;
        int rightSequence = right.HasBattleContext
            ? right.RegistrationSequence
            : int.MaxValue;
        int sequenceComparison = leftSequence.CompareTo(rightSequence);
        if (sequenceComparison != 0)
            return sequenceComparison;

        return StringComparer.Ordinal.Compare(left.SquadId, right.SquadId);
    }
}
