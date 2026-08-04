using System;
using System.Collections.Generic;

[Serializable]
public sealed class BattleReturnData
{
    public BattleOutcome outcome;
    public bool persistentMutationsApplied;
    public string targetScene;
    public bool autosaveSucceeded;
    public string autosaveError;
}

public static class BattleReturnContext
{
    private static BattleReturnData data;

    public static bool HasData => data != null;

    public static bool Set(BattleReturnData value)
    {
        if (value?.outcome == null || string.IsNullOrWhiteSpace(value.targetScene))
            return false;
        data = value;
        return true;
    }

    public static bool TryPeek(out BattleReturnData value)
    {
        value = data;
        return value != null;
    }

    public static bool TryConsume(out BattleReturnData value)
    {
        value = data;
        data = null;
        return value != null;
    }

    public static void Clear() => data = null;
}

public static class ResolvedEncounterRegistry
{
    private static readonly HashSet<string> Resolved =
        new HashSet<string>(StringComparer.Ordinal);

    public static IReadOnlyCollection<string> EncounterIds => Resolved;

    public static bool IsResolved(string encounterId) =>
        !string.IsNullOrWhiteSpace(encounterId) && Resolved.Contains(encounterId);

    public static bool MarkResolved(string encounterId) =>
        !string.IsNullOrWhiteSpace(encounterId) && Resolved.Add(encounterId);

    public static void Restore(IEnumerable<string> encounterIds)
    {
        Resolved.Clear();
        if (encounterIds == null)
            return;
        foreach (string encounterId in encounterIds)
        {
            if (!string.IsNullOrWhiteSpace(encounterId))
                Resolved.Add(encounterId);
        }
    }

    public static void Clear() => Resolved.Clear();
}
