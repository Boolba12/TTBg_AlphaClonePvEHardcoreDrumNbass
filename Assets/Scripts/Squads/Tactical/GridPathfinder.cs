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

    public static bool TryBuildPathToNearest(
        MapGenerator mapGenerator,
        Vector2Int start,
        bool allowDiagonal,
        Func<Vector2Int, bool> isDestination,
        Func<Vector2Int, bool> canEnter,
        int maximumCost,
        out List<Vector2Int> path,
        out Vector2Int destination)
    {
        path = new List<Vector2Int>();
        destination = start;
        if (mapGenerator == null || !mapGenerator.HasGeneratedData ||
            !mapGenerator.GetIsPlayable(start.x, start.y) ||
            isDestination == null || maximumCost < 0)
        {
            return false;
        }

        Queue<Vector2Int> frontier = new Queue<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int> cameFrom =
            new Dictionary<Vector2Int, Vector2Int>();
        Dictionary<Vector2Int, int> costs = new Dictionary<Vector2Int, int>
        {
            [start] = 0
        };
        frontier.Enqueue(start);

        Vector2Int[] directions = allowDiagonal ? AllDirections : CardinalDirections;
        bool found = false;
        while (frontier.Count > 0)
        {
            Vector2Int current = frontier.Dequeue();
            int currentCost = costs[current];
            if (isDestination(current))
            {
                destination = current;
                found = true;
                break;
            }
            if (currentCost >= maximumCost)
                continue;

            foreach (Vector2Int direction in directions)
            {
                Vector2Int next = current + direction;
                if (costs.ContainsKey(next) ||
                    !mapGenerator.GetIsPlayable(next.x, next.y) ||
                    (canEnter != null && !canEnter(next)))
                {
                    continue;
                }

                costs[next] = currentCost + 1;
                cameFrom[next] = current;
                frontier.Enqueue(next);
            }
        }

        if (!found)
            return false;

        Vector2Int step = destination;
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
