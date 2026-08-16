using System;
using System.Collections.Generic;
using UnityEngine;

public enum CoverType
{
    None,
    Half,
    Full
}

[Serializable]
public sealed class GridTacticalTerrainCellDefinition
{
    [SerializeField] private Vector2Int cell;
    [SerializeField] private bool blocksMovement = true;
    [SerializeField] private bool blocksLineOfSight;
    [SerializeField] private CoverType cover = CoverType.None;

    public Vector2Int Cell => cell;
    public bool BlocksMovement => blocksMovement;
    public bool BlocksLineOfSight => blocksLineOfSight;
    public CoverType Cover => cover;

    public GridTacticalTerrainCellDefinition(Vector2Int configuredCell,
        bool configuredBlocksMovement, bool configuredBlocksLineOfSight,
        CoverType configuredCover)
    {
        cell = configuredCell;
        blocksMovement = configuredBlocksMovement;
        blocksLineOfSight = configuredBlocksLineOfSight;
        cover = configuredCover;
    }
}

public interface IGridTacticalTerrain
{
    bool IsInside(Vector2Int cell);
    bool BlocksMovement(Vector2Int cell);
    bool BlocksLineOfSight(Vector2Int cell);
    CoverType GetCover(Vector2Int cell);
}

/// <summary>
/// Logical tactical metadata layered over the canonical generated map. It does
/// not generate cells, own squad occupancy, or infer gameplay from scene meshes.
/// </summary>
public sealed class GridTacticalTerrainService : MonoBehaviour, IGridTacticalTerrain
{
    [SerializeField] private MapGenerator mapGenerator;
    [SerializeField] private List<GridTacticalTerrainCellDefinition> cells =
        new List<GridTacticalTerrainCellDefinition>();

    private readonly Dictionary<Vector2Int, GridTacticalTerrainCellDefinition> lookup =
        new Dictionary<Vector2Int, GridTacticalTerrainCellDefinition>();

    public bool IsInitialized { get; private set; }
    public string FailureReason { get; private set; }
    public int DefinedCellCount => lookup.Count;
    public IReadOnlyList<GridTacticalTerrainCellDefinition> Cells => cells;

    public void Configure(MapGenerator generator,
        IReadOnlyList<GridTacticalTerrainCellDefinition> definitions)
    {
        mapGenerator = generator;
        cells = definitions != null
            ? new List<GridTacticalTerrainCellDefinition>(definitions)
            : new List<GridTacticalTerrainCellDefinition>();
        IsInitialized = false;
        FailureReason = null;
        lookup.Clear();
    }

    public bool Initialize()
    {
        if (IsInitialized)
            return true;
        lookup.Clear();
        FailureReason = null;
        if (mapGenerator == null || !mapGenerator.HasGeneratedData)
            return Fail("Canonical generated map is unavailable.");

        cells ??= new List<GridTacticalTerrainCellDefinition>();
        for (int i = 0; i < cells.Count; i++)
        {
            GridTacticalTerrainCellDefinition definition = cells[i];
            if (definition == null)
                return Fail($"Tactical terrain entry {i} is null.");
            if (!IsInside(definition.Cell))
                return Fail($"Tactical terrain cell {definition.Cell} is outside the generated map.");
            if (lookup.ContainsKey(definition.Cell))
                return Fail($"Duplicate tactical terrain cell {definition.Cell}.");
            lookup.Add(definition.Cell, definition);
        }

        IsInitialized = true;
        return true;
    }

    public bool IsInside(Vector2Int cell) => mapGenerator != null &&
        cell.x >= 0 && cell.y >= 0 &&
        cell.x < mapGenerator.Width && cell.y < mapGenerator.Height;

    public bool BlocksMovement(Vector2Int cell) => IsInitialized &&
        lookup.TryGetValue(cell, out GridTacticalTerrainCellDefinition definition) &&
        definition.BlocksMovement;

    public bool BlocksLineOfSight(Vector2Int cell) => IsInitialized &&
        lookup.TryGetValue(cell, out GridTacticalTerrainCellDefinition definition) &&
        definition.BlocksLineOfSight;

    public CoverType GetCover(Vector2Int cell) => IsInitialized &&
        lookup.TryGetValue(cell, out GridTacticalTerrainCellDefinition definition)
            ? definition.Cover
            : CoverType.None;

#if UNITY_EDITOR
    public bool SetRuntimeCellsForTests(
        IReadOnlyList<GridTacticalTerrainCellDefinition> definitions)
    {
        cells = definitions != null
            ? new List<GridTacticalTerrainCellDefinition>(definitions)
            : new List<GridTacticalTerrainCellDefinition>();
        IsInitialized = false;
        return Initialize();
    }
#endif

    private bool Fail(string reason)
    {
        IsInitialized = false;
        FailureReason = reason;
        lookup.Clear();
        return false;
    }
}
