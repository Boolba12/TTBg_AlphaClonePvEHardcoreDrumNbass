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
}
