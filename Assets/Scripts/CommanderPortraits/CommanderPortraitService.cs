using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class CommanderPortraitService
{
    private readonly CommanderPortraitDatabase database;
    private readonly System.Random random;
    private readonly Dictionary<CommanderRace, CommanderRacePoolState> pools =
        new Dictionary<CommanderRace, CommanderRacePoolState>();

    public CommanderPortraitService(CommanderPortraitDatabase database, int? randomSeed = null)
    {
        this.database = database ? database : throw new ArgumentNullException(nameof(database));
        random = randomSeed.HasValue ? new System.Random(randomSeed.Value) : new System.Random();
    }

    public CommanderPortraitEntry GetRandomPortrait(CommanderRace race)
    {
        List<CommanderPortraitEntry> available = database.GetEntries(race);
        if (available.Count == 0)
        {
            Debug.LogWarning($"CommanderPortraitService: no portraits are registered for race '{race}'.");
            return null;
        }

        CommanderRacePoolState pool = GetPool(race);
        Reconcile(pool, available);
        if (pool.remainingIds.Count == 0)
            StartNewCycle(pool, available);

        while (pool.remainingIds.Count > 0)
        {
            string id = pool.remainingIds[pool.remainingIds.Count - 1];
            pool.remainingIds.RemoveAt(pool.remainingIds.Count - 1);
            if (database.TryGetById(id, out CommanderPortraitEntry entry) && entry.Race == race)
                return entry;
        }

        Debug.LogWarning($"CommanderPortraitService: portrait pool for '{race}' contains no valid entries.");
        return null;
    }

    public CommanderPortraitEntry AssignPortraitIfMissing(
        ICommanderPortraitTarget target,
        CommanderRace race)
    {
        if (target == null)
            throw new ArgumentNullException(nameof(target));

        if (!string.IsNullOrWhiteSpace(target.CommanderPortraitId) &&
            database.TryGetById(target.CommanderPortraitId, out CommanderPortraitEntry existing) &&
            existing.Race == race)
        {
            return existing;
        }

        if (!string.IsNullOrWhiteSpace(target.CommanderPortraitId))
        {
            Debug.LogWarning(
                $"CommanderPortraitService: portrait '{target.CommanderPortraitId}' is missing or has the wrong race. Reassigning.");
        }

        CommanderPortraitEntry assigned = GetRandomPortrait(race);
        target.CommanderPortraitId = assigned != null ? assigned.Id : string.Empty;
        return assigned;
    }

    public Sprite GetDisplaySprite(string portraitId)
    {
        return database.TryGetById(portraitId, out CommanderPortraitEntry entry)
            ? entry.Sprite
            : database.FallbackPortrait;
    }

    public CommanderPortraitPoolState CaptureState()
    {
        CommanderPortraitPoolState state = new CommanderPortraitPoolState();
        foreach (CommanderRacePoolState pool in pools.Values)
            state.races.Add(Clone(pool));
        return state;
    }

    public void RestoreState(CommanderPortraitPoolState state)
    {
        pools.Clear();
        if (state?.races == null)
            return;

        foreach (CommanderRacePoolState saved in state.races)
        {
            if (saved == null || pools.ContainsKey(saved.race))
                continue;

            CommanderRacePoolState pool = Clone(saved);
            Reconcile(pool, database.GetEntries(pool.race));
            pools.Add(pool.race, pool);
        }
    }

    private CommanderRacePoolState GetPool(CommanderRace race)
    {
        if (!pools.TryGetValue(race, out CommanderRacePoolState pool))
        {
            pool = new CommanderRacePoolState { race = race };
            pools.Add(race, pool);
        }
        return pool;
    }

    private void Reconcile(CommanderRacePoolState pool, List<CommanderPortraitEntry> available)
    {
        pool.knownIds ??= new List<string>();
        pool.remainingIds ??= new List<string>();
        HashSet<string> validIds = new HashSet<string>();
        foreach (CommanderPortraitEntry entry in available)
            validIds.Add(entry.Id);

        RemoveInvalidAndDuplicate(pool.knownIds, validIds);
        RemoveInvalidAndDuplicate(pool.remainingIds, validIds);

        HashSet<string> known = new HashSet<string>(pool.knownIds);
        foreach (string id in validIds)
        {
            if (known.Add(id))
            {
                pool.knownIds.Add(id);
                int insertionIndex = random.Next(pool.remainingIds.Count + 1);
                pool.remainingIds.Insert(insertionIndex, id);
            }
        }
    }

    private void StartNewCycle(CommanderRacePoolState pool, List<CommanderPortraitEntry> available)
    {
        pool.knownIds.Clear();
        pool.remainingIds.Clear();
        foreach (CommanderPortraitEntry entry in available)
        {
            if (!pool.knownIds.Contains(entry.Id))
            {
                pool.knownIds.Add(entry.Id);
                pool.remainingIds.Add(entry.Id);
            }
        }
        Shuffle(pool.remainingIds);
    }

    private void Shuffle(List<string> values)
    {
        for (int i = values.Count - 1; i > 0; i--)
        {
            int swapIndex = random.Next(i + 1);
            (values[i], values[swapIndex]) = (values[swapIndex], values[i]);
        }
    }

    private static void RemoveInvalidAndDuplicate(List<string> ids, HashSet<string> validIds)
    {
        HashSet<string> seen = new HashSet<string>();
        ids.RemoveAll(id => string.IsNullOrWhiteSpace(id) || !validIds.Contains(id) || !seen.Add(id));
    }

    private static CommanderRacePoolState Clone(CommanderRacePoolState source)
    {
        return new CommanderRacePoolState
        {
            race = source.race,
            knownIds = source.knownIds != null ? new List<string>(source.knownIds) : new List<string>(),
            remainingIds = source.remainingIds != null ? new List<string>(source.remainingIds) : new List<string>()
        };
    }
}
