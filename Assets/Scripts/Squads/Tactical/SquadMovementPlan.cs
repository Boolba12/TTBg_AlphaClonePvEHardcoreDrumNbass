using System.Collections.Generic;
using UnityEngine;

public sealed class SquadMovementPlan
{
    private readonly List<Vector2Int> path;

    public SquadBattleController Squad { get; }
    public Vector2Int Destination { get; }
    public IReadOnlyList<Vector2Int> Path => path;
    public int ActionPointCost { get; }
    public bool IsValid { get; }
    public string UnavailableReason { get; }

    public SquadMovementPlan(
        SquadBattleController squad,
        Vector2Int destination,
        List<Vector2Int> movementPath,
        int actionPointCost,
        bool isValid,
        string unavailableReason)
    {
        Squad = squad;
        Destination = destination;
        path = movementPath ?? new List<Vector2Int>();
        ActionPointCost = actionPointCost;
        IsValid = isValid;
        UnavailableReason = unavailableReason;
    }
}
