using System;
using System.Collections.Generic;

[Serializable]
public sealed class CommanderPortraitPoolState
{
    public List<CommanderRacePoolState> races = new List<CommanderRacePoolState>();
}

[Serializable]
public sealed class CommanderRacePoolState
{
    public CommanderRace race;
    public List<string> knownIds = new List<string>();
    public List<string> remainingIds = new List<string>();
}
