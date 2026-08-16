using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct GridCoverResult
{
    public GridCoverResult(CoverType coverType,
        IReadOnlyList<Vector2Int> evaluatedCells)
    {
        CoverType = coverType;
        EvaluatedCells = evaluatedCells ?? Array.Empty<Vector2Int>();
    }

    public CoverType CoverType { get; }
    public IReadOnlyList<Vector2Int> EvaluatedCells { get; }

    public static GridCoverResult None => new GridCoverResult(
        CoverType.None, Array.Empty<Vector2Int>());
}

/// <summary>
/// Directional v1 cover: evaluate the target-adjacent logical cell on the side
/// facing the attacker. Exact diagonals evaluate both facing orthogonal cells.
/// </summary>
public sealed class GridCoverService
{
    private readonly IGridTacticalTerrain terrain;

    public GridCoverService(IGridTacticalTerrain configuredTerrain)
    {
        terrain = configuredTerrain;
    }

    public GridCoverResult Evaluate(Vector2Int attacker, Vector2Int target)
    {
        if (terrain == null || !terrain.IsInside(attacker) || !terrain.IsInside(target) ||
            attacker == target)
        {
            return GridCoverResult.None;
        }

        int deltaX = attacker.x - target.x;
        int deltaY = attacker.y - target.y;
        int absoluteX = Mathf.Abs(deltaX);
        int absoluteY = Mathf.Abs(deltaY);
        List<Vector2Int> evaluated = new List<Vector2Int>(2);

        if (absoluteX >= absoluteY && deltaX != 0)
            evaluated.Add(target + new Vector2Int(Math.Sign(deltaX), 0));
        if (absoluteY >= absoluteX && deltaY != 0)
            evaluated.Add(target + new Vector2Int(0, Math.Sign(deltaY)));

        CoverType strongest = CoverType.None;
        for (int i = 0; i < evaluated.Count; i++)
        {
            CoverType candidate = terrain.GetCover(evaluated[i]);
            if ((int)candidate > (int)strongest)
                strongest = candidate;
        }
        return new GridCoverResult(strongest, evaluated);
    }
}
