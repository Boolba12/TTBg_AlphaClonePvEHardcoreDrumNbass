using System;
using UnityEngine;

public sealed class SquadGridAnchor : MonoBehaviour
{
    [SerializeField] private float worldHeightOffset = 0.12f;

    private MapGenerator mapGenerator;
    private MapRenderer mapRenderer;

    public Vector2Int CurrentCell { get; private set; }
    public bool IsPlaced { get; private set; }

    public event Action<Vector2Int> CellChanged;

    public bool PlaceAtCell(
        MapGenerator generator,
        MapRenderer renderer,
        Vector2Int cell)
    {
        if (generator == null || renderer == null)
        {
            Debug.LogError("SquadGridAnchor: map references are missing.", this);
            return false;
        }

        if (!generator.HasGeneratedData || !generator.GetIsPlayable(cell.x, cell.y))
        {
            Debug.LogError(
                $"SquadGridAnchor: cell {cell} is not a playable generated-map cell.",
                this);
            return false;
        }

        mapGenerator = generator;
        mapRenderer = renderer;
        CurrentCell = cell;
        IsPlaced = true;
        transform.position = mapRenderer.GetCellWorldCenter(cell) + Vector3.up * worldHeightOffset;
        CellChanged?.Invoke(cell);
        return true;
    }

    public bool TryMoveToCell(Vector2Int cell)
    {
        return mapGenerator != null && mapRenderer != null &&
               PlaceAtCell(mapGenerator, mapRenderer, cell);
    }

    public Vector3 GetWorldPosition(Vector2Int cell)
    {
        return mapRenderer != null
            ? mapRenderer.GetCellWorldCenter(cell) + Vector3.up * worldHeightOffset
            : transform.position;
    }

    public bool CanCommitCell(Vector2Int cell)
    {
        return mapGenerator != null && mapRenderer != null &&
               mapGenerator.HasGeneratedData &&
               mapGenerator.GetIsPlayable(cell.x, cell.y);
    }

    /// <summary>
    /// Commits logical state after SquadMovementService has animated the root to the cell.
    /// Production movement must not call TryMoveToCell because that method teleports immediately.
    /// </summary>
    public bool CommitVisualArrival(Vector2Int cell)
    {
        if (!CanCommitCell(cell))
            return false;

        CurrentCell = cell;
        IsPlaced = true;
        transform.position = GetWorldPosition(cell);
        CellChanged?.Invoke(cell);
        return true;
    }
}
