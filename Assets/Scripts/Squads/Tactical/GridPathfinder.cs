using System;
using System.Collections.Generic;
using UnityEngine;

public static class GridPathfinder
{
    private static readonly Vector2Int[] CardinalDirections =
    {
        Vector2Int.right,
        Vector2Int.left,
        Vector2Int.up,
        Vector2Int.down
    };

    private static readonly Vector2Int[] AllDirections =
    {
        Vector2Int.right,
        Vector2Int.left,
        Vector2Int.up,
        Vector2Int.down,
        new Vector2Int(1, 1),
        new Vector2Int(1, -1),
        new Vector2Int(-1, 1),
        new Vector2Int(-1, -1)
    };

    public static bool TryBuildPath(
        MapGenerator mapGenerator,
        Vector2Int start,
        Vector2Int target,
        bool allowDiagonal,
        Func<Vector2Int, bool> canEnter,
        out List<Vector2Int> path)
    {
        path = new List<Vector2Int>();
        if (mapGenerator == null || !mapGenerator.HasGeneratedData ||
            !mapGenerator.GetIsPlayable(start.x, start.y) ||
            !mapGenerator.GetIsPlayable(target.x, target.y))
        {
            return false;
        }

        if (start == target)
        {
            path.Add(start);
            return true;
        }

        Queue<Vector2Int> frontier = new Queue<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int> cameFrom =
            new Dictionary<Vector2Int, Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int> { start };
        frontier.Enqueue(start);

        Vector2Int[] directions = allowDiagonal ? AllDirections : CardinalDirections;
        bool found = false;
        while (frontier.Count > 0)
        {
            Vector2Int current = frontier.Dequeue();
            if (current == target)
            {
                found = true;
                break;
            }

            foreach (Vector2Int direction in directions)
            {
                Vector2Int next = current + direction;
                if (visited.Contains(next) ||
                    !mapGenerator.GetIsPlayable(next.x, next.y) ||
                    (canEnter != null && !canEnter(next)))
                {
                    continue;
                }

                visited.Add(next);
                cameFrom[next] = current;
                frontier.Enqueue(next);
            }
        }

        if (!found)
            return false;

        Vector2Int step = target;
        path.Add(step);
        while (step != start)
        {
            step = cameFrom[step];
            path.Add(step);
        }
        path.Reverse();
        return true;
    }
}
