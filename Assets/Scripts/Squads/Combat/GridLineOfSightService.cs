using System;
using System.Collections.Generic;
using UnityEngine;

public enum LineOfSightStatus
{
    NotRequired,
    Clear,
    Blocked,
    Invalid
}

public readonly struct LineOfSightResult
{
    public LineOfSightResult(LineOfSightStatus status,
        IReadOnlyList<Vector2Int> traversedCells, Vector2Int? blockingCell,
        string reason)
    {
        Status = status;
        TraversedCells = traversedCells ?? Array.Empty<Vector2Int>();
        BlockingCell = blockingCell;
        Reason = reason ?? string.Empty;
    }

    public LineOfSightStatus Status { get; }
    public bool HasLineOfSight => Status == LineOfSightStatus.Clear ||
                                  Status == LineOfSightStatus.NotRequired;
    public IReadOnlyList<Vector2Int> TraversedCells { get; }
    public Vector2Int? BlockingCell { get; }
    public string Reason { get; }

    public static LineOfSightResult NotRequired => new LineOfSightResult(
        LineOfSightStatus.NotRequired, Array.Empty<Vector2Int>(), null,
        "Line of sight is not required for this attack.");
}

/// <summary>
/// Deterministic center-to-center supercover traversal. Exact corner crossings
/// include both touching orthogonal cells so thin diagonal corner gaps cannot be
/// used to shoot through logical blockers.
/// </summary>
public sealed class GridLineOfSightService
{
    private readonly IGridTacticalTerrain terrain;

    public GridLineOfSightService(IGridTacticalTerrain configuredTerrain)
    {
        terrain = configuredTerrain;
    }

    public LineOfSightResult Evaluate(Vector2Int start, Vector2Int target)
    {
        if (terrain == null || !terrain.IsInside(start) || !terrain.IsInside(target))
        {
            return new LineOfSightResult(LineOfSightStatus.Invalid,
                Array.Empty<Vector2Int>(), null,
                "Line of sight endpoints are outside the tactical map contract.");
        }

        IReadOnlyList<Vector2Int> cells = BuildSupercoverLine(start, target);
        for (int i = 1; i < cells.Count - 1; i++)
        {
            Vector2Int cell = cells[i];
            if (!terrain.BlocksLineOfSight(cell))
                continue;
            return new LineOfSightResult(LineOfSightStatus.Blocked, cells, cell,
                $"Line of sight is blocked by cell {cell}.");
        }
        return new LineOfSightResult(LineOfSightStatus.Clear, cells, null,
            "Line of sight is clear.");
    }

    public static IReadOnlyList<Vector2Int> BuildSupercoverLine(
        Vector2Int start, Vector2Int target)
    {
        List<Vector2Int> result = new List<Vector2Int> { start };
        if (start == target)
            return result;

        int deltaX = target.x - start.x;
        int deltaY = target.y - start.y;
        int countX = Mathf.Abs(deltaX);
        int countY = Mathf.Abs(deltaY);
        int stepX = Math.Sign(deltaX);
        int stepY = Math.Sign(deltaY);
        int x = start.x;
        int y = start.y;
        int advancedX = 0;
        int advancedY = 0;

        while (advancedX < countX || advancedY < countY)
        {
            long xDecision = (1L + 2L * advancedX) * countY;
            long yDecision = (1L + 2L * advancedY) * countX;
            if (xDecision == yDecision)
            {
                AddUnique(result, new Vector2Int(x + stepX, y));
                AddUnique(result, new Vector2Int(x, y + stepY));
                x += stepX;
                y += stepY;
                advancedX++;
                advancedY++;
            }
            else if (xDecision < yDecision)
            {
                x += stepX;
                advancedX++;
            }
            else
            {
                y += stepY;
                advancedY++;
            }
            AddUnique(result, new Vector2Int(x, y));
        }
        return result;
    }

    private static void AddUnique(List<Vector2Int> cells, Vector2Int cell)
    {
        if (cells.Count == 0 || cells[cells.Count - 1] != cell)
            cells.Add(cell);
    }
}
